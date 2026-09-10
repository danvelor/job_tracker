using FluentAssertions;
using JobTracker.Modules.Jobs.Application.Abstractions;
using JobTracker.Modules.Jobs.Domain;
using JobTracker.Modules.Jobs.Domain.Events;
using JobTracker.Modules.Jobs.Infrastructure;
using JobTracker.Modules.Jobs.Infrastructure.Outbox;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JobTracker.IntegrationTests.Outbox;

public sealed class DeferredEnqueueTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task A_job_enqueued_inside_a_transaction_is_not_visible_until_it_commits()
    {
        var id = Guid.NewGuid();

        await using var transaction = await Context.Database.BeginTransactionAsync();
        Context.Notifications.Add(Notification.Draft(
            Guid.NewGuid(), Organization, "J. Ortiz", "subject", "body",
            DateTimeOffset.UtcNow).Value);
        await Context.SaveChangesAsync();

        await using var other = ContextFor(Organization);
        var seenByAnotherConnection = await other.Notifications.AsNoTracking().CountAsync();

        await transaction.CommitAsync();

        seenByAnotherConnection.Should().Be(0);
        (await other.Notifications.AsNoTracking().CountAsync()).Should().Be(1);
    }

    [Fact]
    public void The_queue_holds_work_until_it_is_flushed()
    {
        var queue = new RecordingQueue();

        queue.Enqueue(new { Work = 1 }, Organization);

        queue.Dispatched.Should().BeEmpty();

        queue.Flush();

        queue.Dispatched.Should().HaveCount(1);
    }

    [Fact]
    public async Task The_processor_flushes_only_once_the_drain_has_committed()
    {
        Context.Jobs.Add(Job.Create(
            "Ridge tile replacement", null,
            Address.Create("12 Elm St", "Springfield", "IL", "62701", 39.78m, -89.65m).Value,
            new DateOnly(2099, 3, 14), Assignee, Customer, Organization,
            new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero)).Value);
        await Context.SaveChangesAsync();

        var queue = new RecordingQueue
        {
            RowVisibleElsewhere = () =>
            {
                using var other = ContextFor(Organization);
                return other.Notifications.AsNoTracking().Any();
            },
        };

        var handler = new RecordingNotificationHandler(Context, queue, Organization);
        var processor = new OutboxProcessor(
            Context, new SingleHandlerPublisher(handler), Tenant, queue, TimeProvider.System,
            Options.Create(new OutboxOptions()));

        await processor.DrainAsync(default);

        queue.SawTheRow.Should().BeTrue();
    }

    private sealed class RecordingQueue : IBackgroundQueue
    {
        private readonly List<object> _pending = [];
        private readonly List<object> _dispatched = [];

        public IReadOnlyList<object> Dispatched => _dispatched;
        public Func<bool>? RowVisibleElsewhere { get; init; }
        public bool SawTheRow { get; private set; }

        public void Enqueue<TRequest>(TRequest request, Guid organizationId)
            where TRequest : notnull => _pending.Add(request);

        public void Flush()
        {
            SawTheRow = RowVisibleElsewhere?.Invoke() ?? true;
            _dispatched.AddRange(_pending);
            _pending.Clear();
        }
    }

    private sealed class RecordingNotificationHandler(
        JobsDbContext context, IBackgroundQueue queue, Guid organizationId)
        : INotificationHandler<JobCreatedDomainEvent>
    {
        public async Task Handle(
            JobCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
        {
            var notification = Notification.Draft(
                domainEvent.Id, organizationId, "J. Ortiz", "subject", "body",
                DateTimeOffset.UtcNow).Value;

            context.Notifications.Add(notification);
            await context.SaveChangesAsync(cancellationToken);

            queue.Enqueue(new { notification.Id }, organizationId);
        }
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
}
