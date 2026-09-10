using FluentAssertions;
using JobTracker.Common.Application;
using JobTracker.Modules.Jobs.Application.Abstractions;
using JobTracker.Modules.Jobs.Application.Notifications;
using JobTracker.Modules.Jobs.Domain;
using JobTracker.Modules.Jobs.Domain.Events;
using JobTracker.Modules.Jobs.IntegrationEvents;
using JobTracker.Modules.Jobs.Infrastructure;
using JobTracker.Modules.Jobs.Infrastructure.Notifications;
using JobTracker.Modules.Jobs.Infrastructure.Outbox;
using JobTracker.Modules.Jobs.Infrastructure.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JobTracker.IntegrationTests.Pipeline;

public sealed class NotificationPipelineTests(PostgresFixture postgres)
    : IntegrationTestBase(postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly InlineQueue _queue = new();

    private Job Seed()
    {
        var job = Job.Create(
            "Ridge tile replacement", null,
            Address.Create("12 Elm St", "Springfield", "IL", "62701", 39.78m, -89.65m).Value,
            new DateOnly(2099, 3, 14), Assignee, Customer, Organization, Now).Value;
        Context.Jobs.Add(job);
        return job;
    }

    private async Task Drain()
    {
        var unitOfWork = new UnitOfWork(Context);
        var notifications = new NotificationRepository(Context);

        var handler = new NotifyAssigneeOnJobCreatedHandler(
            notifications,
            new PartyRepository(Context),
            new JobRepository(Context),
            _queue,
            unitOfWork,
            TimeProvider.System);

        var processor = new OutboxProcessor(
            Context, new SingleHandlerPublisher(handler), Tenant, _queue, TimeProvider.System,
            Options.Create(new OutboxOptions()));

        await processor.DrainAsync(default);
        Context.ChangeTracker.Clear();
    }

    private async Task CompleteTheJob(Guid id)
    {
        var job = await new JobRepository(Context).GetByIdAsync(id);
        job!.Start(Now.AddHours(1));
        job.Complete(Now.AddHours(6), "sig", []);
        await Context.SaveChangesAsync();
    }

    private async Task CancelTheJob(Guid id)
    {
        var job = await new JobRepository(Context).GetByIdAsync(id);
        job!.Cancel(Now.AddHours(2), "Weather closed the site");
        await Context.SaveChangesAsync();
    }

    private Task ResetProcessedOn() =>
        Context.Database.ExecuteSqlAsync($"update jobs.outbox_messages set processed_on = null");

    [Fact]
    public async Task Creating_a_job_notifies_the_assignee()
    {
        Seed();
        await Context.SaveChangesAsync();

        await Drain();

        var notification = await Context.Notifications.SingleAsync();
        notification.Recipient.Should().Be("J. Ortiz");
        notification.Status.Should().Be(NotificationStatus.Pending);
    }

    [Fact]
    public async Task The_notification_names_the_job_it_is_about()
    {
        Seed();
        await Context.SaveChangesAsync();

        await Drain();

        (await Context.Notifications.SingleAsync()).Body.Should().Contain("Ridge tile replacement");
    }

    [Fact]
    public async Task The_notification_is_scoped_to_its_organization()
    {
        Seed();
        await Context.SaveChangesAsync();
        await Drain();

        await using var theirs = ContextFor(OtherOrganization);

        (await theirs.Notifications.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A_replayed_creation_does_not_notify_twice()
    {
        Seed();
        await Context.SaveChangesAsync();
        await Drain();
        await ResetProcessedOn();

        await Drain();

        (await Context.Notifications.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task A_duplicate_is_absorbed_rather_than_left_retrying_for_ever()
    {
        Seed();
        await Context.SaveChangesAsync();
        await Drain();
        await ResetProcessedOn();

        await Drain();

        var unprocessed = await Context.Database.SqlQuery<int>(
            $"""select count(*)::int as "Value" from jobs.outbox_messages where processed_on is null""")
            .SingleAsync();

        unprocessed.Should().Be(0);
    }

    [Fact]
    public async Task The_unique_constraint_is_real_and_not_only_the_handler_being_careful()
    {
        Seed();
        await Context.SaveChangesAsync();
        await Drain();
        var existing = await Context.Notifications.AsNoTracking().SingleAsync();

        var duplicate = Notification.Draft(
            existing.SourceEventId, Organization, existing.Recipient, "again", "again",
            Now).Value;
        Context.Notifications.Add(duplicate);

        var save = async () => await Context.SaveChangesAsync();

        await save.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Sending_moves_the_record_from_Pending_to_Sent()
    {
        Seed();
        await Context.SaveChangesAsync();
        await Drain();

        await RunQueuedSends();

        var notification = await Context.Notifications.AsNoTracking().SingleAsync();
        notification.Status.Should().Be(NotificationStatus.Sent);
        notification.SentAt.Should().NotBeNull();
    }

    [Fact]
    public async Task A_send_that_is_retried_after_success_does_nothing_and_reports_success()
    {
        Seed();
        await Context.SaveChangesAsync();
        await Drain();
        await RunQueuedSends();
        var sentAt = (await Context.Notifications.AsNoTracking().SingleAsync()).SentAt;

        await RunQueuedSends(replay: true);

        (await Context.Notifications.AsNoTracking().SingleAsync()).SentAt.Should().Be(sentAt);
    }

    [Fact]
    public async Task A_refused_transport_records_the_reason_rather_than_losing_it()
    {
        Seed();
        await Context.SaveChangesAsync();
        await Drain();

        await RunQueuedSends(sender: new RefusingSender());

        var notification = await Context.Notifications.AsNoTracking().SingleAsync();
        notification.Status.Should().Be(NotificationStatus.Failed);
        notification.FailureReason.Should().Contain("refused");
    }

    private async Task RunQueuedSends(bool replay = false, INotificationSender? sender = null)
    {
        var handler = new SendNotificationCommandHandler(
            new NotificationRepository(Context),
            sender ?? new LoggingNotificationSender(NullLogger<LoggingNotificationSender>.Instance),
            new UnitOfWork(Context),
            TimeProvider.System);

        foreach (var command in replay ? _queue.Sent : _queue.Drain())
        {
            await handler.Handle(command, default);
        }

        Context.ChangeTracker.Clear();
    }

    private sealed class InlineQueue : IBackgroundQueue
    {
        private readonly List<SendNotificationCommand> _queued = [];

        public IReadOnlyList<SendNotificationCommand> Sent => _queued;

        public void Enqueue<TRequest>(TRequest request, Guid organizationId)
            where TRequest : notnull =>
            _queued.Add((SendNotificationCommand)(object)request);

        public void Flush() { }

        public IEnumerable<SendNotificationCommand> Drain() => _queued.ToList();
    }

    private sealed class RefusingSender : INotificationSender
    {
        public Task<SendOutcome> SendAsync(
            string recipient, string subject, string body,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(SendOutcome.Refused("the transport refused the address"));
    }

    private sealed class SingleHandlerPublisher(
        INotificationHandler<JobCreatedDomainEvent> handler) : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Publish((INotification)notification, cancellationToken);

        public Task Publish<TNotification>(
            TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification =>
            notification is JobCreatedDomainEvent created
                ? handler.Handle(created, cancellationToken)
                : Task.CompletedTask;
    }

    [Fact]
    public async Task Completing_a_job_notifies_the_customer_at_their_email()
    {
        var job = Seed();
        await Context.SaveChangesAsync();
        await Drain();
        await CompleteTheJob(job.Id);

        await DrainCompletions();

        var customer = await Context.Notifications.AsNoTracking()
            .SingleAsync(notification => notification.Recipient.Contains("@"));
        customer.Recipient.Should().Be("ops@acme.test");
    }

    [Fact]
    public async Task The_crew_notification_and_the_customer_notification_coexist()
    {
        var job = Seed();
        await Context.SaveChangesAsync();
        await Drain();
        await CompleteTheJob(job.Id);

        await DrainCompletions();

        (await Context.Notifications.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task A_replayed_completion_does_not_notify_the_customer_twice()
    {
        var job = Seed();
        await Context.SaveChangesAsync();
        await Drain();
        await CompleteTheJob(job.Id);
        await DrainCompletions();
        await ResetProcessedOn();

        await DrainCompletions();

        (await Context.Notifications.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Completing_a_job_publishes_the_contract_rather_than_the_domain_event()
    {
        var job = Seed();
        await Context.SaveChangesAsync();
        await Drain();
        await CompleteTheJob(job.Id);
        var bus = new RecordingBus();

        await DrainCompletions(bus);

        var published = bus.Published.Should().ContainSingle().Subject;
        published.JobId.Should().Be(job.Id);
        published.StartedAt.Should().Be(Now.AddHours(1));
        published.CompletedAt.Should().Be(Now.AddHours(6));
    }

    private async Task DrainCompletions(IEventBus? bus = null)
    {
        var unitOfWork = new UnitOfWork(Context);
        var notifications = new NotificationRepository(Context);

        var customer = new NotifyCustomerOnJobCompletedHandler(
            notifications, new PartyRepository(Context), new JobRepository(Context),
            _queue, unitOfWork, TimeProvider.System);

        var publish = new PublishJobCompletedHandler(bus ?? new RecordingBus());

        var processor = new OutboxProcessor(
            Context, new CompletionPublisher(customer, publish), Tenant, _queue, TimeProvider.System,
            Options.Create(new OutboxOptions()));

        await processor.DrainAsync(default);
        Context.ChangeTracker.Clear();
    }

    private sealed class RecordingBus : IEventBus
    {
        private readonly List<JobCompletedIntegrationEvent> _published = [];

        public IReadOnlyList<JobCompletedIntegrationEvent> Published => _published;

        public Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default)
            where T : class
        {
            if (integrationEvent is JobCompletedIntegrationEvent completed)
            {
                _published.Add(completed);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class CompletionPublisher(
        INotificationHandler<JobCompletedDomainEvent> customer,
        INotificationHandler<JobCompletedDomainEvent> publish) : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Publish((INotification)notification, cancellationToken);

        public async Task Publish<TNotification>(
            TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            if (notification is JobCompletedDomainEvent completed)
            {
                await customer.Handle(completed, cancellationToken);
                await publish.Handle(completed, cancellationToken);
            }
        }
    }

    [Fact]
    public async Task Cancelling_a_job_notifies_the_assignee()
    {
        var job = Seed();
        await Context.SaveChangesAsync();
        await Drain();
        await CancelTheJob(job.Id);

        await DrainCancellations();

        var cancellation = await Context.Notifications.AsNoTracking()
            .SingleAsync(notification => notification.Subject.Contains("cancelled"));
        cancellation.Recipient.Should().Be("J. Ortiz");
        cancellation.Status.Should().Be(NotificationStatus.Pending);
    }

    [Fact]
    public async Task The_cancellation_notification_carries_the_reason()
    {
        var job = Seed();
        await Context.SaveChangesAsync();
        await Drain();
        await CancelTheJob(job.Id);

        await DrainCancellations();

        var cancellation = await Context.Notifications.AsNoTracking()
            .SingleAsync(notification => notification.Subject.Contains("cancelled"));
        cancellation.Body.Should().Contain("Ridge tile replacement");
        cancellation.Body.Should().Contain("Weather closed the site");
    }

    [Fact]
    public async Task A_replayed_cancellation_does_not_notify_twice()
    {
        var job = Seed();
        await Context.SaveChangesAsync();
        await Drain();
        await CancelTheJob(job.Id);
        await DrainCancellations();
        await ResetProcessedOn();

        await DrainCancellations();

        (await Context.Notifications.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task A_cancellation_with_no_crew_assigned_notifies_nobody()
    {
        var job = Seed();
        await Context.SaveChangesAsync();
        await Drain();
        await Context.Database.ExecuteSqlAsync($"update jobs.jobs set assignee_id = null");
        await CancelTheJob(job.Id);

        await DrainCancellations();

        (await Context.Notifications.CountAsync()).Should().Be(1);
    }

    private async Task DrainCancellations()
    {
        var handler = new NotifyAssigneeOnJobCancelledHandler(
            new NotificationRepository(Context), new PartyRepository(Context),
            new JobRepository(Context), _queue, new UnitOfWork(Context), TimeProvider.System);

        var processor = new OutboxProcessor(
            Context, new CancellationPublisher(handler), Tenant, _queue, TimeProvider.System,
            Options.Create(new OutboxOptions()));

        await processor.DrainAsync(default);
        Context.ChangeTracker.Clear();
    }

    private sealed class CancellationPublisher(
        INotificationHandler<JobCancelledDomainEvent> handler) : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Publish((INotification)notification, cancellationToken);

        public Task Publish<TNotification>(
            TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification =>
            notification is JobCancelledDomainEvent cancelled
                ? handler.Handle(cancelled, cancellationToken)
                : Task.CompletedTask;
    }
}
