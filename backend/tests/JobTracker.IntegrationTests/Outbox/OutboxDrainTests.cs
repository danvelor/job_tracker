using FluentAssertions;
using JobTracker.Modules.Jobs.Domain;
using JobTracker.Modules.Jobs.Domain.Events;
using JobTracker.Modules.Jobs.Infrastructure.Outbox;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JobTracker.IntegrationTests.Outbox;

/// <summary>
/// Hangfire runs the processor; these tests call it directly.
///
/// A test that starts a job server and waits for a ten-second tick is slow,
/// time-dependent, and tests Hangfire. What is worth testing is the drain: the
/// locking, the ordering, and what a failure leaves behind. One test elsewhere
/// asserts the recurring job is registered, so the wiring cannot rot into
/// decoration.
/// </summary>
public sealed class OutboxDrainTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly RecordingPublisher _publisher = new();

    private OutboxProcessor Processor(int batchSize = 20) =>
        new(Context, _publisher, TimeProvider.System,
            Options.Create(new OutboxOptions { BatchSize = batchSize }));

    /// <summary>A processor on its own context, so two can hold locks at once.</summary>
    private OutboxProcessor ConcurrentProcessor(RecordingPublisher publisher, int batchSize = 20) =>
        new(ContextFor(Organization), publisher, TimeProvider.System,
            Options.Create(new OutboxOptions { BatchSize = batchSize }));

    private async Task SeedJobs(int count)
    {
        for (var index = 0; index < count; index++)
        {
            Context.Jobs.Add(Job.Create(
                $"Job {index}", null,
                Address.Create("12 Elm St", "Springfield", "IL", "62701", 39.78m, -89.65m).Value,
                new DateOnly(2099, 3, 14), Assignee, Customer, Organization, Now).Value);
        }

        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    private Task<int> Unprocessed() =>
        Context.Database.SqlQuery<int>(
            $"""select count(*)::int as "Value" from jobs.outbox_messages where processed_on is null""")
            .SingleAsync();

    private Task ResetProcessedOn() =>
        Context.Database.ExecuteSqlAsync($"update jobs.outbox_messages set processed_on = null");

    private Task<string?> AnyError() =>
        Context.Database.SqlQuery<string?>(
            $"""select error as "Value" from jobs.outbox_messages limit 1""").SingleAsync();

    // ---- the happy path --------------------------------------------------

    [Fact]
    public async Task Draining_publishes_each_event_and_stamps_the_row()
    {
        await SeedJobs(1);

        await Processor().DrainAsync(default);

        _publisher.Published.Should().ContainSingle().Which.Should().BeOfType<JobCreatedDomainEvent>();
        (await Unprocessed()).Should().Be(0);
    }

    [Fact]
    public async Task An_empty_outbox_is_not_an_error()
    {
        await Processor().DrainAsync(default);

        _publisher.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task The_oldest_message_is_drained_first()
    {
        await SeedJobs(3);
        var oldest = await Context.Database
            .SqlQuery<Guid>(
                $"""select id as "Value" from jobs.outbox_messages order by occurred_on limit 1""")
            .SingleAsync();

        await Processor(batchSize: 1).DrainAsync(default);

        _publisher.Published.Should().ContainSingle().Which.Id.Should().Be(oldest);
    }

    [Fact]
    public async Task A_batch_smaller_than_the_backlog_leaves_the_rest_for_the_next_poll()
    {
        await SeedJobs(5);

        await Processor(batchSize: 2).DrainAsync(default);

        (await Unprocessed()).Should().Be(3);
    }

    // ---- what a failure leaves -------------------------------------------

    [Fact]
    public async Task A_handler_that_throws_leaves_the_row_unprocessed()
    {
        // At-least-once (4.3): a row leaves the pipeline only once its
        // consequence is known to have happened. Stamping a row whose handler
        // threw is exactly how a message gets lost in silence.
        _publisher.Fail = true;
        await SeedJobs(1);

        await Processor().DrainAsync(default);

        (await Unprocessed()).Should().Be(1);
    }

    [Fact]
    public async Task A_failure_is_recorded_on_the_row_for_whoever_reads_the_table()
    {
        _publisher.Fail = true;
        await SeedJobs(1);

        await Processor().DrainAsync(default);

        (await AnyError()).Should().Contain("deliberate");
    }

    [Fact]
    public async Task One_poison_message_does_not_stop_the_others()
    {
        // A drain that stopped at the first failure would stop every unrelated
        // job in the system from billing or notifying.
        await SeedJobs(3);
        _publisher.FailFirstOnly = true;

        await Processor().DrainAsync(default);

        (await Unprocessed()).Should().Be(1);
        _publisher.Published.Should().HaveCount(3);
    }

    [Fact]
    public async Task A_failed_message_is_retried_on_the_next_drain()
    {
        _publisher.Fail = true;
        await SeedJobs(1);
        await Processor().DrainAsync(default);

        _publisher.Fail = false;
        await Processor().DrainAsync(default);

        (await Unprocessed()).Should().Be(0);
    }

    // ---- replay and concurrency ------------------------------------------

    [Fact]
    public async Task A_replayed_message_is_published_again_rather_than_skipped()
    {
        await SeedJobs(1);
        await Processor().DrainAsync(default);
        await ResetProcessedOn();

        await Processor().DrainAsync(default);

        // The pipeline does not deduplicate; the consumers do (4.5). Asserting
        // it here is what stops someone later "fixing" a duplicate in the wrong
        // place and quietly turning at-least-once into at-most-once.
        _publisher.Published.Should().HaveCount(2);
    }

    [Fact]
    public async Task A_second_drain_cannot_take_rows_the_first_is_holding()
    {
        // The overlap is forced rather than hoped for. An earlier version of
        // this test ran two drains with Task.WhenAll and passed with the row
        // locking removed entirely — the two never actually overlapped, so it
        // proved nothing. Here the first drain stops inside its first publish,
        // holding its locks, and the second runs to completion while it waits.
        //
        // With FOR UPDATE SKIP LOCKED the second finds nothing and returns.
        // Without it the second reads the same unprocessed rows and every
        // consequence happens twice.
        await SeedJobs(5);

        var selected = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var blocking = new RecordingPublisher { OnFirstPublish = (selected, release) };
        var second = new RecordingPublisher();

        var first = ConcurrentProcessor(blocking, batchSize: 5).DrainAsync(default);
        await selected.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await ConcurrentProcessor(second, batchSize: 5).DrainAsync(default);

        release.SetResult();
        await first;

        second.Published.Should().BeEmpty();
        blocking.Published.Should().HaveCount(5);
        (await Unprocessed()).Should().Be(0);
    }

    private sealed class RecordingPublisher : IPublisher
    {
        private readonly List<Common.Domain.IDomainEvent> _published = [];

        public bool Fail { get; set; }
        public bool FailFirstOnly { get; set; }

        /// <summary>
        /// Signals once the drain has selected its batch, then waits. It is
        /// what turns "two drains ran" into "two drains overlapped".
        /// </summary>
        public (TaskCompletionSource Selected, TaskCompletionSource Release)? OnFirstPublish { get; set; }

        public IReadOnlyList<Common.Domain.IDomainEvent> Published => _published;

        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Publish((INotification)notification, cancellationToken);

        public Task Publish<TNotification>(
            TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            _published.Add((Common.Domain.IDomainEvent)notification!);

            if (OnFirstPublish is { } gate && _published.Count == 1)
            {
                gate.Selected.SetResult();
                return gate.Release.Task;
            }

            return Fail || (FailFirstOnly && _published.Count == 1)
                ? Task.FromException(new InvalidOperationException("a deliberate failure"))
                : Task.CompletedTask;
        }
    }
}
