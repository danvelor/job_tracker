using FluentAssertions;
using JobTracker.Common.Application;
using JobTracker.Common.Domain;
using JobTracker.Modules.Jobs.Application.Jobs.CancelJob;
using JobTracker.Modules.Jobs.Application.Jobs.CompleteJob;
using JobTracker.Modules.Jobs.Application.Jobs.RescheduleJob;
using JobTracker.Modules.Jobs.Application.Jobs.StartJob;
using JobTracker.Modules.Jobs.Domain;
using Moq;

namespace JobTracker.Modules.Jobs.Application.UnitTests;

/// <summary>
/// The four transition handlers share one shape — load, refuse if absent, call
/// the aggregate, return its Result, save on success — so they share one set of
/// tests. What each must prove separately is that it calls the *right*
/// aggregate method and that it does not save when the aggregate refused.
/// </summary>
public sealed class TransitionCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid JobId = Guid.NewGuid();
    private static readonly Guid Organization = Guid.NewGuid();

    private readonly Mock<IJobRepository> _repository = new();
    private readonly Mock<IPartyRepository> _parties = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FixedTimeProvider _time = new(Now);

    private static Job AScheduledJob() =>
        Job.Create(
            "Roof repair", null,
            Address.Create("12 Elm St", "Springfield", "IL", "62701", 39.78m, -89.65m).Value,
            new DateOnly(2026, 3, 14), Guid.NewGuid(), Guid.NewGuid(), Organization, Now).Value;

    private static Job AnInProgressJob()
    {
        var job = AScheduledJob();
        job.Start(Now.AddHours(1));
        return job;
    }

    /// <summary>The roster answers yes unless a test says otherwise.</summary>
    public TransitionCommandHandlerTests() =>
        _parties
            .Setup(p => p.AssigneeExistsAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

    private void Returns(Job? job) =>
        _repository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

    private void SavedOnce() =>
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

    private void NeverSaved() =>
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);

    // ---- start -----------------------------------------------------------

    [Fact]
    public async Task StartJob_starts_a_Scheduled_job_and_saves()
    {
        var job = AScheduledJob();
        Returns(job);
        var handler = new StartJobCommandHandler(_repository.Object, _unitOfWork.Object, _time);

        var result = await handler.Handle(new StartJobCommand(JobId, Organization), default);

        result.IsSuccess.Should().BeTrue();
        job.Status.Should().Be(JobStatus.InProgress);
        SavedOnce();
    }

    [Fact]
    public async Task StartJob_reports_not_found_for_an_unknown_identifier()
    {
        Returns(null);
        var handler = new StartJobCommandHandler(_repository.Object, _unitOfWork.Object, _time);

        var result = await handler.Handle(new StartJobCommand(JobId, Organization), default);

        result.Error.Should().Be(JobErrors.NotFound);
        NeverSaved();
    }

    [Fact]
    public async Task StartJob_does_not_save_when_the_aggregate_refuses()
    {
        Returns(AnInProgressJob());
        var handler = new StartJobCommandHandler(_repository.Object, _unitOfWork.Object, _time);

        var result = await handler.Handle(new StartJobCommand(JobId, Organization), default);

        // A refused transition changed nothing, so saving would write an
        // unchanged aggregate and, worse, drain an outbox that has no event.
        result.Error.Should().Be(JobErrors.NotScheduled);
        NeverSaved();
    }

    // ---- complete --------------------------------------------------------

    [Fact]
    public async Task CompleteJob_completes_an_InProgress_job_with_its_photos()
    {
        var job = AnInProgressJob();
        Returns(job);
        var handler = new CompleteJobCommandHandler(_repository.Object, _unitOfWork.Object, _time);

        var command = new CompleteJobCommand(
            JobId, Organization, "data:image/png;base64,AAA",
            [new NewPhotoInput("p1.jpg", "ridge")]);

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        job.Status.Should().Be(JobStatus.Completed);
        job.Photos.Should().ContainSingle().Which.Caption.Should().Be("ridge");
        SavedOnce();
    }

    [Fact]
    public async Task CompleteJob_stamps_the_photo_capture_time_from_the_clock()
    {
        var job = AnInProgressJob();
        Returns(job);
        var handler = new CompleteJobCommandHandler(_repository.Object, _unitOfWork.Object, _time);

        await handler.Handle(
            new CompleteJobCommand(JobId, Organization, "sig", [new NewPhotoInput("p1.jpg", null)]),
            default);

        job.Photos.Single().CapturedAt.Should().Be(Now);
    }

    [Fact]
    public async Task CompleteJob_surfaces_the_signature_rule_from_the_aggregate()
    {
        Returns(AnInProgressJob());
        var handler = new CompleteJobCommandHandler(_repository.Object, _unitOfWork.Object, _time);

        var result = await handler.Handle(
            new CompleteJobCommand(JobId, Organization, "  ", []), default);

        result.Error.Should().Be(JobErrors.SignatureRequired);
        NeverSaved();
    }

    // ---- cancel ----------------------------------------------------------

    [Fact]
    public async Task CancelJob_cancels_with_its_reason_and_saves()
    {
        var job = AScheduledJob();
        Returns(job);
        var handler = new CancelJobCommandHandler(_repository.Object, _unitOfWork.Object, _time);

        var result = await handler.Handle(
            new CancelJobCommand(JobId, Organization, "Weather"), default);

        result.IsSuccess.Should().BeTrue();
        job.CancellationReason.Should().Be("Weather");
        SavedOnce();
    }

    [Fact]
    public async Task CancelJob_surfaces_the_terminal_rule_from_the_aggregate()
    {
        var job = AScheduledJob();
        job.Cancel(Now, "Weather");
        Returns(job);
        var handler = new CancelJobCommandHandler(_repository.Object, _unitOfWork.Object, _time);

        var result = await handler.Handle(
            new CancelJobCommand(JobId, Organization, "again"), default);

        result.Error.Should().Be(JobErrors.Terminal);
        NeverSaved();
    }

    // ---- reschedule ------------------------------------------------------

    [Fact]
    public async Task RescheduleJob_moves_the_date_and_saves()
    {
        var job = AScheduledJob();
        Returns(job);
        var handler = new RescheduleJobCommandHandler(_repository.Object, _parties.Object, _unitOfWork.Object, _time);
        var later = new DateOnly(2026, 4, 1);

        var result = await handler.Handle(
            new RescheduleJobCommand(JobId, Organization, later, Guid.NewGuid()), default);

        result.IsSuccess.Should().BeTrue();
        job.ScheduledDate.Should().Be(later);
        SavedOnce();
    }

    [Fact]
    public async Task RescheduleJob_surfaces_BR_1_from_the_aggregate()
    {
        Returns(AScheduledJob());
        var handler = new RescheduleJobCommandHandler(_repository.Object, _parties.Object, _unitOfWork.Object, _time);

        var result = await handler.Handle(
            new RescheduleJobCommand(JobId, Organization, new DateOnly(2020, 1, 1), Guid.NewGuid()),
            default);

        result.Error.Should().Be(JobErrors.ScheduledInThePast);
        NeverSaved();
    }


    [Fact]
    public async Task RescheduleJob_refuses_a_crew_member_from_another_organization()
    {
        Returns(AScheduledJob());
        _parties
            .Setup(p => p.AssigneeExistsAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var handler = new RescheduleJobCommandHandler(
            _repository.Object, _parties.Object, _unitOfWork.Object, _time);

        var result = await handler.Handle(
            new RescheduleJobCommand(JobId, Organization, new DateOnly(2026, 4, 1), Guid.NewGuid()),
            default);

        // Reassignment is the other door into the same hole as creation: the
        // foreign key sees every roster row, and only the tenant-filtered
        // lookup knows which ones are ours.
        result.Error.Should().Be(JobErrors.AssigneeNotOnTheRoster);
        NeverSaved();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
