using JobTracker.Common.Domain;
using JobTracker.Modules.Jobs.Domain.Events;

namespace JobTracker.Modules.Jobs.Domain;

public sealed class Job : AggregateRoot, ITenantScoped
{
    private readonly List<JobPhoto> _photos = [];

    private Job(Guid id) : base(id) { }

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

    private bool IsTerminal => Status is JobStatus.Completed or JobStatus.Cancelled;

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
            Status = JobStatus.Scheduled,
            ScheduledDate = scheduledDate,
            AssigneeId = assigneeId,
            CustomerId = customerId,
            OrganizationId = organizationId,
        };

        job.Raise(new JobCreatedDomainEvent(job.Id, assigneeId) { OrganizationId = organizationId });

        return Result.Success(job);
    }

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

        Raise(new JobCompletedDomainEvent(Id, CustomerId, StartedAt!.Value, completedAt)
        {
            OrganizationId = OrganizationId,
        });

        return Result.Success();
    }

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

        Raise(new JobCancelledDomainEvent(Id, AssigneeId, reason) { OrganizationId = OrganizationId });

        return Result.Success();
    }
}
