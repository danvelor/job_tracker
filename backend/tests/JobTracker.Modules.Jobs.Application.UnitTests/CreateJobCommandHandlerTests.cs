using FluentAssertions;
using JobTracker.Common.Application;
using JobTracker.Modules.Jobs.Application.Jobs.CreateJob;
using JobTracker.Modules.Jobs.Domain;
using JobTracker.Modules.Jobs.Domain.Events;
using Moq;

namespace JobTracker.Modules.Jobs.Application.UnitTests;

public sealed class CreateJobCommandHandlerTests
{
    private readonly Mock<IJobRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private readonly FixedTimeProvider _time =
        new(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));

    private CreateJobCommandHandler Handler() => new(_repository.Object, _unitOfWork.Object, _time);

    private static CreateJobCommand AValidCommand() => new(
        "Roof repair", null,
        "12 Elm St", "Springfield", "IL", "62701", 39.78m, -89.65m,
        new DateOnly(2026, 3, 14), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public async Task It_persists_the_job_and_returns_its_identifier()
    {
        var result = await Handler().Handle(AValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(Guid.Empty);
        _repository.Verify(r => r.AddAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task The_persisted_job_carries_the_JobCreatedDomainEvent()
    {
        Job? captured = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
            .Callback<Job, CancellationToken>((job, _) => captured = job);

        await Handler().Handle(AValidCommand(), CancellationToken.None);

        // The event must be on the aggregate when it reaches the repository:
        // the outbox interceptor reads it during SaveChanges, so an event
        // raised after this point would never make it into the transaction.
        captured!.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<JobCreatedDomainEvent>();
    }

    [Fact]
    public async Task An_invalid_address_fails_without_touching_the_repository()
    {
        var command = AValidCommand() with { City = "" };

        var result = await Handler().Handle(command, CancellationToken.None);

        result.Error.Should().Be(JobErrors.AddressIncomplete);
        _repository.VerifyNoOtherCalls();
        _unitOfWork.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_past_date_fails_and_nothing_is_saved()
    {
        var command = AValidCommand() with { ScheduledDate = new DateOnly(2020, 1, 1) };

        var result = await Handler().Handle(command, CancellationToken.None);

        result.Error.Should().Be(JobErrors.ScheduledInThePast);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task It_reads_the_clock_from_TimeProvider_rather_than_from_the_system()
    {
        // The handler supplies `now`; the aggregate never reads it. A date that
        // is "today" only under the injected clock proves which one was used.
        var command = AValidCommand() with { ScheduledDate = new DateOnly(2026, 3, 1) };

        var result = await Handler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task A_failing_command_returns_a_failure_rather_than_throwing()
    {
        // Result<Guid> is built through the same path the pipeline behaviour
        // uses, and a malformed failure would surface as a cast exception
        // rather than as a Result.
        var command = AValidCommand() with { Title = "  " };

        var act = async () => await Handler().Handle(command, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
