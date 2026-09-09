using FluentAssertions;
using JobTracker.Modules.Jobs.Domain.Events;

namespace JobTracker.Modules.Jobs.Domain.UnitTests;

public sealed class JobTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Future = new(2026, 3, 14);
    private static readonly Guid Assignee = Guid.NewGuid();
    private static readonly Guid Customer = Guid.NewGuid();
    private static readonly Guid Organization = Guid.NewGuid();

    private static Address AnAddress() =>
        Address.Create("12 Elm St", "Springfield", "IL", "62701", 39.78m, -89.65m).Value;

    private static Job AScheduledJob() =>
        Job.Create("Roof repair", null, AnAddress(), Future, Assignee, Customer, Organization, Now)
            .Value;

    private static Job AnInProgressJob()
    {
        var job = AScheduledJob();
        job.Start(Now.AddHours(1));
        return job;
    }

    private static IEnumerable<NewJobPhoto> NoPhotos() => [];

    // ---- creation --------------------------------------------------------

    [Fact]
    public void A_created_job_is_Scheduled_rather_than_Draft()
    {
        // D-14: the creation form collects exactly what Scheduled requires, and
        // the acceptance walkthrough cannot complete a job never scheduled.
        AScheduledJob().Status.Should().Be(JobStatus.Scheduled);
    }

    [Fact]
    public void Creation_records_the_organization_the_job_belongs_to()
    {
        AScheduledJob().OrganizationId.Should().Be(Organization);
    }

    [Fact]
    public void Creation_raises_JobCreatedDomainEvent()
    {
        AScheduledJob().DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<JobCreatedDomainEvent>();
    }

    [Fact]
    public void A_job_cannot_be_created_without_a_title()
    {
        Job.Create("   ", null, AnAddress(), Future, Assignee, Customer, Organization, Now)
            .Error.Should().Be(JobErrors.TitleRequired);
    }

    [Fact]
    public void A_job_cannot_be_scheduled_in_the_past()
    {
        var yesterday = DateOnly.FromDateTime(Now.UtcDateTime).AddDays(-1);

        Job.Create("Roof repair", null, AnAddress(), yesterday, Assignee, Customer, Organization, Now)
            .Error.Should().Be(JobErrors.ScheduledInThePast);
    }

    [Fact]
    public void A_job_scheduled_for_today_is_accepted()
    {
        // BR-1 says "in the past", and today is not past. An off-by-one here
        // would refuse every same-day job, which is most of them.
        var today = DateOnly.FromDateTime(Now.UtcDateTime);

        Job.Create("Roof repair", null, AnAddress(), today, Assignee, Customer, Organization, Now)
            .IsSuccess.Should().BeTrue();
    }

    // ---- start -----------------------------------------------------------

    [Fact]
    public void A_Scheduled_job_starts_and_records_when()
    {
        var job = AScheduledJob();
        var startedAt = Now.AddHours(1);

        job.Start(startedAt).IsSuccess.Should().BeTrue();

        job.Status.Should().Be(JobStatus.InProgress);
        job.StartedAt.Should().Be(startedAt);
    }

    [Fact]
    public void A_job_that_already_started_cannot_start_again()
    {
        AnInProgressJob().Start(Now.AddHours(2)).Error.Should().Be(JobErrors.NotScheduled);
    }

    // ---- complete --------------------------------------------------------

    [Fact]
    public void An_InProgress_job_completes_with_a_signature()
    {
        var job = AnInProgressJob();
        var completedAt = Now.AddHours(6);

        job.Complete(completedAt, "data:image/png;base64,AAA", NoPhotos())
            .IsSuccess.Should().BeTrue();

        job.Status.Should().Be(JobStatus.Completed);
        job.CompletedAt.Should().Be(completedAt);
    }

    [Fact]
    public void Completion_without_a_signature_is_refused()
    {
        AnInProgressJob().Complete(Now.AddHours(6), "   ", NoPhotos())
            .Error.Should().Be(JobErrors.SignatureRequired);
    }

    [Fact]
    public void A_job_that_never_started_cannot_be_completed()
    {
        AScheduledJob().Complete(Now.AddHours(6), "sig", NoPhotos())
            .Error.Should().Be(JobErrors.NotInProgress);
    }

    [Fact]
    public void Completion_attaches_the_photos_it_was_given()
    {
        var job = AnInProgressJob();
        var photos = new[] { new NewJobPhoto("p1.jpg", Now, "ridge") };

        job.Complete(Now.AddHours(6), "sig", photos);

        job.Photos.Should().ContainSingle().Which.Url.Should().Be("p1.jpg");
    }

    [Fact]
    public void Completion_raises_JobCompletedDomainEvent()
    {
        var job = AnInProgressJob();
        job.ClearDomainEvents();

        job.Complete(Now.AddHours(6), "sig", NoPhotos());

        job.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<JobCompletedDomainEvent>();
    }

    // ---- cancel ----------------------------------------------------------

    [Fact]
    public void A_Scheduled_job_cancels_with_a_reason()
    {
        var job = AScheduledJob();

        job.Cancel(Now.AddHours(1), "Weather").IsSuccess.Should().BeTrue();

        job.Status.Should().Be(JobStatus.Cancelled);
        job.CancellationReason.Should().Be("Weather");
    }

    [Fact]
    public void An_InProgress_job_can_also_be_cancelled()
    {
        AnInProgressJob().Cancel(Now.AddHours(2), "Customer withdrew")
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Cancellation_without_a_reason_is_refused()
    {
        AScheduledJob().Cancel(Now.AddHours(1), "  ").Error.Should().Be(JobErrors.ReasonRequired);
    }

    [Fact]
    public void Cancellation_raises_JobCancelledDomainEvent()
    {
        var job = AScheduledJob();
        job.ClearDomainEvents();

        job.Cancel(Now.AddHours(1), "Weather");

        job.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<JobCancelledDomainEvent>();
    }

    // ---- BR-2, terminal states -------------------------------------------

    [Fact]
    public void A_completed_job_refuses_every_further_transition()
    {
        var job = AnInProgressJob();
        job.Complete(Now.AddHours(6), "sig", NoPhotos());

        job.Start(Now.AddHours(7)).Error.Should().Be(JobErrors.Terminal);
        job.Cancel(Now.AddHours(7), "changed our mind").Error.Should().Be(JobErrors.Terminal);
        job.Reschedule(Future.AddDays(1), Assignee, Now).Error.Should().Be(JobErrors.Terminal);
    }

    [Fact]
    public void A_cancelled_job_refuses_every_further_transition()
    {
        var job = AScheduledJob();
        job.Cancel(Now.AddHours(1), "Weather");

        job.Start(Now.AddHours(2)).Error.Should().Be(JobErrors.Terminal);
        job.Complete(Now.AddHours(2), "sig", NoPhotos()).Error.Should().Be(JobErrors.Terminal);
        job.Reschedule(Future.AddDays(1), Assignee, Now).Error.Should().Be(JobErrors.Terminal);
    }

    [Fact]
    public void A_terminal_job_raises_no_further_domain_event()
    {
        var job = AScheduledJob();
        job.Cancel(Now.AddHours(1), "Weather");
        job.ClearDomainEvents();

        job.Cancel(Now.AddHours(2), "again");

        // A refused transition must not announce a consequence that did not
        // happen: the outbox would carry it downstream regardless.
        job.DomainEvents.Should().BeEmpty();
    }

    // ---- reschedule ------------------------------------------------------

    [Fact]
    public void A_Scheduled_job_can_be_rescheduled()
    {
        var job = AScheduledJob();
        var later = Future.AddDays(3);

        job.Reschedule(later, Assignee, Now).IsSuccess.Should().BeTrue();

        job.ScheduledDate.Should().Be(later);
    }

    [Fact]
    public void Rescheduling_into_the_past_is_refused()
    {
        var yesterday = DateOnly.FromDateTime(Now.UtcDateTime).AddDays(-1);

        AScheduledJob().Reschedule(yesterday, Assignee, Now)
            .Error.Should().Be(JobErrors.ScheduledInThePast);
    }

    // ---- reachability ----------------------------------------------------

    [Fact]
    public void Photos_are_exposed_read_only_and_have_no_other_way_in()
    {
        // Line 185: JobPhoto is reachable only through the aggregate root.
        // There is no AddPhoto — photos arrive through Complete, which is the
        // only moment the business produces them.
        typeof(Job).GetProperty(nameof(Job.Photos))!.PropertyType
            .Should().Be(typeof(IReadOnlyCollection<JobPhoto>));

        typeof(Job).GetMethods().Select(method => method.Name)
            .Should().NotContain("AddPhoto");
    }

    // ---- errors name the input that failed --------------------------------

    [Fact]
    public void A_refusal_names_the_field_the_caller_must_fix()
    {
        // Design A5 point 3: the form lights up the field that failed rather
        // than showing a banner. The aggregate is what knows which value was
        // wrong, and the field name is a contract with the client in the same
        // way the error code already is — so it belongs on the Error and not
        // in a translation table somewhere in Presentation.
        JobErrors.ScheduledInThePast.FieldErrors.Should().ContainKey("ScheduledDate");
        JobErrors.TitleRequired.FieldErrors.Should().ContainKey("Title");
        JobErrors.SignatureRequired.FieldErrors.Should().ContainKey("SignatureUrl");
        JobErrors.ReasonRequired.FieldErrors.Should().ContainKey("Reason");
    }

    [Fact]
    public void Every_named_field_matches_a_property_the_client_actually_sends()
    {
        // A field name nothing on the wire is called is worse than none: the
        // form highlights nothing and the developer trusts the highlight.
        JobErrors.ScheduledInThePast.FieldErrors!.Keys
            .Should().BeSubsetOf([nameof(Job.ScheduledDate)]);
        JobErrors.SignatureRequired.FieldErrors!.Keys
            .Should().BeSubsetOf([nameof(Job.SignatureUrl)]);
    }

    [Fact]
    public void A_state_refusal_names_no_field_because_no_field_is_wrong()
    {
        // BR-2 and BR-3 are about the job, not about the request. Naming a
        // field would tell the user to change something that was fine.
        JobErrors.Terminal.FieldErrors.Should().BeNull();
        JobErrors.NotScheduled.FieldErrors.Should().BeNull();
        JobErrors.NotFound.FieldErrors.Should().BeNull();
    }
}
