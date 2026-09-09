using FluentAssertions;
using JobTracker.Common.Application;
using JobTracker.Modules.Jobs.Application.Abstractions;
using JobTracker.Modules.Jobs.Application.Notifications;
using JobTracker.Modules.Jobs.Domain;
using JobTracker.Modules.Jobs.Domain.Events;
using JobTracker.Modules.Jobs.Infrastructure;
using JobTracker.Modules.Jobs.Infrastructure.Notifications;
using JobTracker.Modules.Jobs.Infrastructure.Outbox;
using JobTracker.Modules.Jobs.Infrastructure.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JobTracker.IntegrationTests.Pipeline;

/// <summary>
/// FR-8 through the real pipeline: save, drain, read the table. The publisher
/// here is the one handler under test rather than a container, so a failure
/// names the handler instead of the wiring.
/// </summary>
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
            Context, new SingleHandlerPublisher(handler), TimeProvider.System,
            Options.Create(new OutboxOptions()));

        await processor.DrainAsync(default);
        Context.ChangeTracker.Clear();
    }

    private Task ResetProcessedOn() =>
        Context.Database.ExecuteSqlAsync($"update jobs.outbox_messages set processed_on = null");

    // ---- FR-8 ------------------------------------------------------------

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

        // A message saying only "you have a job" makes the crew open the app to
        // find out which one, which is most of the value of notifying at all.
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

    // ---- case 9 of architecture 8.2 --------------------------------------

    [Fact]
    public async Task A_replayed_creation_does_not_notify_twice()
    {
        Seed();
        await Context.SaveChangesAsync();
        await Drain();
        await ResetProcessedOn();

        await Drain();

        // The whole of 4.5: the key is stable across a replay, so the second
        // pass finds the row and stops. A test that never replays never checks
        // it, and at-least-once delivery guarantees production will.
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

        // If the handler let the constraint violation escape, the outbox row
        // would stay unprocessed and the drain would retry it every poll until
        // somebody noticed.
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

        // The handler's ExistsAsync check is a courtesy; this is the guarantee.
        // Two workers racing past the check at the same moment are stopped
        // here and nowhere else.
        await save.Should().ThrowAsync<DbUpdateException>();
    }

    // ---- sending ---------------------------------------------------------

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

        // Hangfire retries a job whose result it did not record. Reporting
        // failure here would have it retry for ever; re-sending would tell a
        // real person twice.
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

    /// <summary>
    /// Runs nothing; it records. The test decides when a send happens, which is
    /// what makes these assertions deterministic without a job server.
    /// </summary>
    private sealed class InlineQueue : IBackgroundQueue
    {
        private readonly List<SendNotificationCommand> _queued = [];

        public IReadOnlyList<SendNotificationCommand> Sent => _queued;

        public void Enqueue<TRequest>(TRequest request) where TRequest : notnull =>
            _queued.Add((SendNotificationCommand)(object)request);

        public IEnumerable<SendNotificationCommand> Drain() => _queued.ToList();
    }

    private sealed class RefusingSender : INotificationSender
    {
        public Task<SendOutcome> SendAsync(
            string recipient, string subject, string body,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(SendOutcome.Refused("the transport refused the address"));
    }

    /// <summary>
    /// Publishes to exactly one handler. A DI container would prove the wiring
    /// too, and would make a failure here ambiguous between the handler and the
    /// registration — the wiring has its own test.
    /// </summary>
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
}
