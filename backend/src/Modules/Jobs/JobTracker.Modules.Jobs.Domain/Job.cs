using JobTracker.Common.Domain;
using JobTracker.Modules.Jobs.Domain.Events;

namespace JobTracker.Modules.Jobs.Domain;

/// <summary>
/// The aggregate root and the only entry point to its data. Every rule in
/// prd.md section 6 is enforced inside an intention-named method and returns a
/// <see cref="Result"/> rather than throwing: the model is not anemic because
/// the rules live with the data they constrain.
/// </summary>
public sealed class Job : AggregateRoot, ITenantScoped
{
    private readonly List<JobPhoto> _photos = [];

    private Job(Guid id) : base(id) { }

    // EF only.
    private Job() { }

    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Address Address { get; private set; } = null!;
    public JobStatus Status { get; private set; }
    public DateOnly? ScheduledDate { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }
    public string? SignatureUrl { get; private set; }
    public Guid? AssigneeId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid OrganizationId { get; private set; }

    public IReadOnlyCollection<JobPhoto> Photos => _photos.AsReadOnly();

    /// <summary>BR-2. Completed and Cancelled are the audit record.</summary>
    private bool IsTerminal => Status is JobStatus.Completed or JobStatus.Cancelled;

    /// <summary>
    /// <paramref name="now"/> is a parameter rather than a read of
    /// <c>DateTimeOffset.UtcNow</c>, so BR-1 is testable without freezing a
    /// global clock. Handlers supply it from <c>TimeProvider</c>.
    /// </summary>
    public static Result<Job> Create(
        string title,
        string? description,
        Address address,
        DateOnly scheduledDate,
        Guid assigneeId,
        Guid customerId,
        Guid organizationId,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure<Job>(JobErrors.TitleRequired);
        }

        if (scheduledDate < DateOnly.FromDateTime(now.UtcDateTime))
        {
            return Result.Failure<Job>(JobErrors.ScheduledInThePast);
        }

        var job = new Job(Guid.NewGuid())
        {
            Title = title,
            Description = description,
            Address = address,
            // D-14: creation produces a Scheduled job. Draft stays in the model
            // for a job captured without a date, unreachable from here.
            Status = JobStatus.Scheduled,
            ScheduledDate = scheduledDate,
            AssigneeId = assigneeId,
            CustomerId = customerId,
            OrganizationId = organizationId,
        };

        job.Raise(new JobCreatedDomainEvent(job.Id, assigneeId, organizationId));

        return Result.Success(job);
    }

    /// <summary>FR-2. Enforces BR-1 and BR-2.</summary>
    public Result Reschedule(DateOnly scheduledDate, Guid assigneeId, DateTimeOffset now)
    {
        if (IsTerminal)
        {
            return Result.Failure(JobErrors.Terminal);
        }

        if (scheduledDate < DateOnly.FromDateTime(now.UtcDateTime))
        {
            return Result.Failure(JobErrors.ScheduledInThePast);
        }

        ScheduledDate = scheduledDate;
        AssigneeId = assigneeId;
        Status = JobStatus.Scheduled;

        return Result.Success();
    }

    /// <summary>FR-3. Enforces BR-2 and BR-3.</summary>
    public Result Start(DateTimeOffset startedAt)
    {
        if (IsTerminal)
        {
            return Result.Failure(JobErrors.Terminal);
        }

        if (Status != JobStatus.Scheduled)
        {
            return Result.Failure(JobErrors.NotScheduled);
        }

        Status = JobStatus.InProgress;
        StartedAt = startedAt;

        return Result.Success();
    }

    /// <summary>FR-4. Enforces BR-2 and BR-4.</summary>
    public Result Complete(
        DateTimeOffset completedAt,
        string signatureUrl,
        IEnumerable<NewJobPhoto> photos)
    {
        if (IsTerminal)
        {
            return Result.Failure(JobErrors.Terminal);
        }

        if (string.IsNullOrWhiteSpace(signatureUrl))
        {
            return Result.Failure(JobErrors.SignatureRequired);
        }

        if (Status != JobStatus.InProgress)
        {
            return Result.Failure(JobErrors.NotInProgress);
        }

        Status = JobStatus.Completed;
        CompletedAt = completedAt;
        SignatureUrl = signatureUrl;

        foreach (var photo in photos)
        {
            _photos.Add(new JobPhoto(Guid.NewGuid(), photo.Url, photo.CapturedAt, photo.Caption));
        }

        Raise(new JobCompletedDomainEvent(Id, CustomerId, OrganizationId, completedAt));

        return Result.Success();
    }

    /// <summary>FR-5. Enforces BR-2 and BR-5.</summary>
    public Result Cancel(DateTimeOffset cancelledAt, string reason)
    {
        if (IsTerminal)
        {
            return Result.Failure(JobErrors.Terminal);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(JobErrors.ReasonRequired);
        }

        Status = JobStatus.Cancelled;
        CancelledAt = cancelledAt;
        CancellationReason = reason;

        Raise(new JobCancelledDomainEvent(Id, reason));

        return Result.Success();
    }
}
