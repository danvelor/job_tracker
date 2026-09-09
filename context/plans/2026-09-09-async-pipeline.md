# Asynchronous Pipeline Implementation Plan (Plan 4)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make a completed job produce an invoice and two notifications without Jobs and Billing knowing about each other — and prove the pipeline survives a crash at every point.

**Architecture:** An EF interceptor writes domain events into `jobs.outbox_messages` inside the same transaction as the state change, so the two cannot disagree. A Hangfire recurring job drains the outbox with `FOR UPDATE SKIP LOCKED` and republishes through MediatR. One handler translates `JobCompletedDomainEvent` into a published contract of primitives; Billing consumes that and nothing else. Delivery is at-least-once, so every consumer earns idempotency with a unique constraint.

**Tech Stack:** Hangfire 1.8.25 (`Core`, `AspNetCore`, `PostgreSql`), EF Core 9 `SaveChangesInterceptor`, MediatR 12, `System.Text.Json`.

**Spec:** `context/architecture.md` §4 in full (4.1 event kinds, 4.2 the outbox, 4.3 at-least-once, 4.4 Hangfire's two roles, 4.5 idempotency, 4.6 notifications), §3.5 (Billing), §8.1 (walkthrough steps 4 and 9), §8.2 case 5 and case 9; `context/design.md` B5 (class map) and B7 (`outbox_messages`, `notifications`, `billing.invoices`)

**Rubric reach:** Outbox + Hangfire (4). It also turns walkthrough steps 4 and 9 green, which is the half of the definition of done in 8.1 that nothing executable covers today.

## Global Constraints

- Every command prefixed:
  ```bash
  PATH="/opt/homebrew/opt/dotnet@9/bin:$PATH" dotnet …
  ```
- **Billing never references `Jobs.Domain`.** It learns a job completed from a record of primitives. A layer rule reads the project graph and says so
- **Every consumer is idempotent through a unique constraint** (4.5). No generic dedup table — D-23 removed it and the reasons still hold
- **A handler that fails must leave `processed_on` null.** At-least-once is the guarantee (4.3); a row stamped before its consequence is a lost message
- Test-driven (D-28). Warnings are errors. **After a deliberate red, `dotnet clean`**
- **Licence check (D-20), already done rather than assumed.** Hangfire 1.8.25's `LICENSE.md` reads: *"multi-licensed under the terms of the licenses listed in this file"*, LGPL v3 among them, and referencing the unmodified package satisfies it. The paid tier is the separate `Hangfire.Pro.*` packages, which this plan never adds. `LicenceRules` gets an assertion so a future `Hangfire.Pro` reference fails a test rather than a reviewer's `dotnet restore`

---

## Two gaps in the spec, resolved here

Both were found reading the design against the code, and both would otherwise have been decided silently mid-task.

**`JobCompletedIntegrationEvent` carries an "amount basis" (design B5) that does not exist.** A `Job` has no price, no rate and no line items, and nothing in the PRD gives it one. Three ways out: add a price to `Job` (scope nothing asks for), have Billing charge a flat fee (then D-04's "real domain rather than a stub" is a stub with extra steps), or send what Jobs actually knows and let Billing price it.

The third is the only one that makes the boundary mean something: **Jobs says what happened, Billing decides what it costs.** The contract carries `StartedAt` and `CompletedAt`; Billing owns a labour rate and a minimum charge, which is a rule Jobs must not know. `JobCompletedDomainEvent` gains `StartedAt` — it is internal, and 4.1 says an internal event may change shape freely. Recorded as **D-33**.

**`source_event_id` is "the outbox row's identifier" (4.5), which a handler cannot see.** A handler receives a deserialised domain event, not the row that carried it. Passing the row id through ambient scoped state would work and would be the one piece of hidden context in the system.

Instead the domain event carries its own identity, generated once when it is raised, frozen by serialisation, and identical on every replay — which is the condition 4.5 actually requires. The outbox row then uses that same value as its primary key, so 4.5's wording stays literally true and the same event cannot be enqueued twice. Recorded as **D-34**.

---

## Task 0: Record D-32, which is referenced and missing

**Files:**
- Modify: `context/architecture.md` §11

`backend/tests/JobTracker.IntegrationTests/Api/JobEndpointTests.cs:254` says *"Recorded as D-32"* and nothing is. A dangling decision reference is worse than none: a reader goes looking and concludes the log is unreliable.

- [ ] **Step 1: Add D-32 to the decision log**

| Decision | Alternatives | Why | Cost |
|---|---|---|---|
| **D-32** BR-1 answers 400 on both `POST /api/jobs` and `PATCH /api/jobs/{id}/schedule` | 409 on the PATCH, as design B6's row says | B6 gives one rule two status codes. BR-1 refuses a *value*: the same date is refused whether the job is new or being corrected, and the caller fixes it the same way. 409 is for refusals where nothing the caller sent was wrong — a terminal job, a job not yet started | Design B6's reschedule row is now wrong and is annotated as superseded |

- [ ] **Step 2: Annotate the B6 row in `context/design.md` and commit**

---

## File Structure

```
backend/src/
├── Common/JobTracker.Common.Domain/
│   └── DomainEvent.cs                      base record carrying the event's identity
├── Common/JobTracker.Common.Infrastructure/
│   └── EventBus.cs                         IEventBus over MediatR
├── Modules/Jobs/
│   ├── JobTracker.Modules.Jobs.IntegrationEvents/   (new project)
│   │   └── JobCompletedIntegrationEvent.cs
│   ├── …Jobs.Domain/
│   │   ├── Notification.cs  NotificationErrors.cs  INotificationRepository.cs
│   │   └── Events/JobCompletedDomainEvent.cs        gains StartedAt
│   ├── …Jobs.Application/
│   │   ├── Abstractions/INotificationSender.cs  IBackgroundQueue.cs
│   │   └── Notifications/
│   │       ├── NotifyAssigneeOnJobCreatedHandler.cs      FR-8
│   │       ├── NotifyCustomerOnJobCompletedHandler.cs    FR-10
│   │       ├── PublishJobCompletedHandler.cs             domain -> contract
│   │       └── SendNotificationCommand.cs                the fire-and-forget unit
│   └── …Jobs.Infrastructure/
│       ├── Outbox/OutboxMessage.cs  InsertOutboxMessagesInterceptor.cs
│       │        OutboxProcessor.cs  OutboxSerializer.cs
│       ├── Notifications/LoggingNotificationSender.cs  NotificationRepository.cs
│       └── HangfireBackgroundQueue.cs
└── Modules/Billing/                        (three new projects)
    ├── JobTracker.Modules.Billing.Domain/          Invoice, IInvoiceRepository, InvoiceErrors
    ├── JobTracker.Modules.Billing.Application/     GenerateInvoiceOnJobCompletedHandler, LabourRate
    └── JobTracker.Modules.Billing.Infrastructure/  BillingDbContext, migrations, BillingModule

backend/tests/
├── JobTracker.Modules.Billing.UnitTests/   (new project)
└── JobTracker.IntegrationTests/
    ├── Outbox/OutboxTransactionTests.cs    case 5 of §8.2
    ├── Outbox/OutboxDrainTests.cs          the drain, and what a crash leaves
    ├── Pipeline/NotificationTests.cs       FR-8, FR-10, case 9
    ├── Pipeline/BillingTests.cs            FR-9, case 9
    └── Api/PipelineEndToEndTests.cs        walkthrough steps 4 and 9
```

---

### Task 1: The outbox, and the transaction that makes it worth having

**Files:**
- Create: `Common.Domain/DomainEvent.cs`
- Modify: the three `Jobs.Domain/Events/*.cs`, `JobCompletedDomainEvent` gains `StartedAt`
- Create: `Jobs.Infrastructure/Outbox/OutboxMessage.cs`, `OutboxSerializer.cs`, `InsertOutboxMessagesInterceptor.cs`, `Configurations/OutboxMessageConfiguration.cs`
- Create: a migration
- Create: `tests/JobTracker.IntegrationTests/Outbox/OutboxTransactionTests.cs`

**Interfaces:**
- Consumes: `IDomainEvent`, `AggregateRoot.DomainEvents`, `JobsDbContext`
- Produces: `DomainEvent` (base record), `OutboxMessage`, `InsertOutboxMessagesInterceptor`

Case 5 of §8.2, and the one a unit test genuinely cannot reach: a unit test shows the interceptor was called, not that the write is one transaction.

- [ ] **Step 1: Write the failing transaction tests**

```csharp
using FluentAssertions;
using JobTracker.Modules.Jobs.Domain;
using JobTracker.Modules.Jobs.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.IntegrationTests.Outbox;

public sealed class OutboxTransactionTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private Task<int> OutboxRows() =>
        Context.Database.SqlQuery<int>(
            $"""select count(*)::int as "Value" from jobs.outbox_messages""").SingleAsync();

    [Fact]
    public async Task Creating_a_job_writes_its_domain_event_to_the_outbox()
    {
        Context.Jobs.Add(AJob());
        await Context.SaveChangesAsync();

        (await OutboxRows()).Should().Be(1);
    }

    [Fact]
    public async Task A_rollback_leaves_neither_the_job_nor_its_outbox_row()
    {
        await using var transaction = await Context.Database.BeginTransactionAsync();
        Context.Jobs.Add(AJob());
        await Context.SaveChangesAsync();
        await transaction.RollbackAsync();
        Context.ChangeTracker.Clear();

        // NFR-2. There is no window in which consequences are recorded for a
        // completion that rolled back — which is the half of the guarantee a
        // "was the interceptor called" unit test says nothing about.
        (await Context.Jobs.CountAsync()).Should().Be(0);
        (await OutboxRows()).Should().Be(0);
    }

    [Fact]
    public async Task A_failed_write_leaves_no_outbox_row_behind()
    {
        // A job pointing at an assignee that does not exist: the constraint
        // rejects it, and the interceptor has already staged the outbox row by
        // then. If the two were not one transaction, the row would survive and
        // the system would notify a crew about a job that does not exist.
        Context.Jobs.Add(AJobAssignedTo(Guid.NewGuid()));

        var save = async () => await Context.SaveChangesAsync();

        await save.Should().ThrowAsync<DbUpdateException>();
        Context.ChangeTracker.Clear();
        (await OutboxRows()).Should().Be(0);
    }

    [Fact]
    public async Task The_outbox_row_carries_the_event_identity_as_its_key()
    {
        var job = AJob();
        var eventId = job.DomainEvents.Single().Id;
        Context.Jobs.Add(job);
        await Context.SaveChangesAsync();

        // D-34. The row's key is the event's own identity, so a replay is the
        // same source_event_id and the consumers' unique constraints hold.
        var stored = await Context.Database
            .SqlQuery<Guid>($"""select id as "Value" from jobs.outbox_messages""").SingleAsync();

        stored.Should().Be(eventId);
    }

    [Fact]
    public async Task The_aggregate_is_left_with_no_events_to_raise_twice()
    {
        var job = AJob();
        Context.Jobs.Add(job);
        await Context.SaveChangesAsync();

        // Cleared by the interceptor after copying. Left in place, a second
        // SaveChanges on the same instance would enqueue them again.
        job.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Completing_a_job_enqueues_exactly_one_event()
    {
        var job = AJob();
        Context.Jobs.Add(job);
        await Context.SaveChangesAsync();
        job.Start(Now.AddHours(1));
        job.Complete(Now.AddHours(6), "sig", []);
        await Context.SaveChangesAsync();

        (await OutboxRows()).Should().Be(2);
    }
}
```

`AJob` and `AJobAssignedTo` follow the helpers already in `SearchTests`.

- [ ] **Step 2: Run to verify they fail**

Expected: `relation "jobs.outbox_messages" does not exist`.

- [ ] **Step 3: Give a domain event its identity**

`Common.Domain/DomainEvent.cs`:

```csharp
namespace JobTracker.Common.Domain;

/// <summary>
/// The identity every domain event carries (D-34).
///
/// It is generated once, when the aggregate raises the event, and frozen by
/// serialisation into the outbox — so a message replayed after a crash arrives
/// with the same identity it had the first time. That stability is the whole
/// condition architecture 4.5 puts on an idempotency key, and it is why a
/// consumer can key off this value without any ambient context telling it which
/// outbox row it came from.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTimeOffset OccurredOn { get; init; } = DateTimeOffset.UtcNow;
}
```

`IDomainEvent` gains `Guid Id { get; }` and `DateTimeOffset OccurredOn { get; }`, and the three event records change from `: IDomainEvent` to `: DomainEvent`.

`OccurredOn` reading a clock contradicts the rule that time is a parameter — and it is deliberate. That rule protects *business* decisions from an untestable clock; this is a record of when a thing was written, never read by a rule, and threading `now` into a record initialiser would put a parameter on every event for no test's benefit. Say so in the comment.

- [ ] **Step 4: Write `OutboxMessage` and its configuration**

```csharp
namespace JobTracker.Modules.Jobs.Infrastructure.Outbox;

/// <summary>
/// A serialised domain event waiting to be published. Infrastructure, not
/// domain: nothing in the model knows the outbox exists, which is what lets the
/// poller be replaced by a broker without touching a rule (D-03).
/// </summary>
internal sealed class OutboxMessage
{
    private OutboxMessage() { }

    internal OutboxMessage(Guid id, string type, string content, DateTimeOffset occurredOn)
    {
        Id = id;
        Type = type;
        Content = content;
        OccurredOn = occurredOn;
    }

    public Guid Id { get; private init; }
    public string Type { get; private init; } = string.Empty;
    public string Content { get; private init; } = string.Empty;
    public DateTimeOffset OccurredOn { get; private init; }
    public DateTimeOffset? ProcessedOn { get; private set; }
    public string? Error { get; private set; }

    /// <summary>
    /// Stamped only after every handler returned (4.3). Stamping earlier would
    /// turn a crash into a lost message rather than a repeated one.
    /// </summary>
    internal void MarkProcessed(DateTimeOffset processedOn) => ProcessedOn = processedOn;

    /// <summary>
    /// The row stays unprocessed on purpose: the error is a note for a reader,
    /// not a tombstone. The next drain tries again.
    /// </summary>
    internal void RecordFailure(string error) => Error = error;
}
```

The configuration maps `content` as `jsonb` and adds the partial index from design B7:

```sql
CREATE INDEX ix_outbox_unprocessed ON jobs.outbox_messages (occurred_on) WHERE processed_on IS NULL;
```

Partial because the drain only ever asks for unprocessed rows, and the processed ones are the ones that accumulate — a full index would grow without bound to serve a query that never reads them.

- [ ] **Step 5: Write `OutboxSerializer`**

```csharp
using System.Text.Json;
using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Infrastructure.Outbox;

/// <summary>
/// Type names are resolved against an allowlist built from the module's own
/// assembly rather than through <c>TypeNameHandling</c>. The rows are ones we
/// wrote, so this is not an untrusted input today — but a deserialiser that will
/// construct any type named in its input is a gadget waiting for the day
/// something else can write that column, and the allowlist costs one dictionary.
/// </summary>
internal static class OutboxSerializer
{
    private static readonly IReadOnlyDictionary<string, Type> Known =
        typeof(Domain.Job).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false } && type.IsAssignableTo(typeof(IDomainEvent)))
            .ToDictionary(type => type.FullName!);

    public static string NameOf(IDomainEvent domainEvent) => domainEvent.GetType().FullName!;

    public static string Serialize(IDomainEvent domainEvent) =>
        JsonSerializer.Serialize(domainEvent, domainEvent.GetType());

    public static IDomainEvent Deserialize(string type, string content) =>
        Known.TryGetValue(type, out var clrType)
            ? (IDomainEvent)JsonSerializer.Deserialize(content, clrType)!
            // A row naming a type this build does not have is a deployment
            // problem, and failing loudly leaves the row unprocessed for the
            // build that does — which is exactly the right outcome.
            : throw new InvalidOperationException($"No domain event type named {type}.");
}
```

- [ ] **Step 6: Write the interceptor**

```csharp
using JobTracker.Common.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace JobTracker.Modules.Jobs.Infrastructure.Outbox;

/// <summary>
/// Architecture 4.2. It runs inside <c>SaveChangesAsync</c>, so the outbox rows
/// and the state change commit or roll back together — there is no window in
/// which a job is completed and its consequences are absent.
///
/// It persists the <em>domain</em> event, not the integration event, and that
/// is a deliberate deviation from assessment lines 236-238. The integration
/// event is produced by a handler that runs after the domain event is
/// published, which is after the commit; an interceptor cannot see something
/// that does not exist yet. Translating downstream is the ordering that
/// actually holds the guarantee (D-13).
/// </summary>
internal sealed class InsertOutboxMessagesInterceptor(TimeProvider time) : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            AddOutboxMessages(eventData.Context);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddOutboxMessages(DbContext context)
    {
        var aggregates = context.ChangeTracker.Entries<AggregateRoot>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();

        var messages = aggregates
            .SelectMany(aggregate => aggregate.DomainEvents)
            .Select(domainEvent => new OutboxMessage(
                // The event's own identity, not a fresh one (D-34).
                domainEvent.Id,
                OutboxSerializer.NameOf(domainEvent),
                OutboxSerializer.Serialize(domainEvent),
                domainEvent.OccurredOn))
            .ToList();

        // Cleared after copying, and before the write returns: an aggregate
        // saved twice in one request would otherwise enqueue its events again.
        foreach (var aggregate in aggregates)
        {
            aggregate.ClearDomainEvents();
        }

        context.Set<OutboxMessage>().AddRange(messages);

        _ = time;
    }
}
```

The `_ = time` is a placeholder the next step removes — the interceptor needs no clock, because `OccurredOn` comes from the event. Drop the constructor parameter.

- [ ] **Step 7: Register the interceptor, generate the migration, run green**

`AddJobsModule` adds `.AddInterceptors(new InsertOutboxMessagesInterceptor())`.

- [ ] **Step 8: Commit**

---

### Task 2: The drain

**Files:**
- Create: `Jobs.Infrastructure/Outbox/OutboxProcessor.cs`, `OutboxOptions.cs`
- Create: `tests/JobTracker.IntegrationTests/Outbox/OutboxDrainTests.cs`
- Modify: `JobsModule` (Hangfire registration), `Program.cs`
- Modify: `Directory.Packages.props`, `LicenceRules.cs`

**Interfaces:**
- Consumes: `OutboxMessage`, `OutboxSerializer`, `IPublisher`
- Produces: `OutboxProcessor.DrainAsync(CancellationToken)`

**Hangfire runs the processor; the tests call it directly.** A test that starts a Hangfire server and waits for a ten-second tick is slow and time-dependent, and it would be testing Hangfire. What is worth testing is the drain: the locking, the ordering, what a failure leaves behind. One test asserts the recurring job is registered, so the wiring cannot be dead.

- [ ] **Step 1: Write the failing drain tests**

```csharp
    [Fact]
    public async Task Draining_publishes_each_event_and_stamps_the_row()
    {
        await SeedCompletedJob();

        await Processor().DrainAsync(default);

        Handled.Should().ContainSingle();
        (await UnprocessedRows()).Should().Be(0);
    }

    [Fact]
    public async Task A_handler_that_throws_leaves_the_row_unprocessed()
    {
        // At-least-once (4.3): a row is only removed from the pipeline after
        // its consequence is known to have happened. Stamping a row whose
        // handler threw is how a message gets lost silently.
        Failing = true;
        await SeedCompletedJob();

        await Processor().DrainAsync(default);

        (await UnprocessedRows()).Should().Be(1);
    }

    [Fact]
    public async Task A_failure_is_recorded_on_the_row_for_a_reader()
    {
        Failing = true;
        await SeedCompletedJob();

        await Processor().DrainAsync(default);

        (await ErrorOf(default)).Should().Contain("deliberate");
    }

    [Fact]
    public async Task One_bad_message_does_not_stop_the_others()
    {
        // A poison message that halted the drain would stop every unrelated
        // job in the system from billing or notifying.
        await SeedTwoJobs(failFirst: true);

        await Processor().DrainAsync(default);

        (await UnprocessedRows()).Should().Be(1);
        Handled.Should().ContainSingle();
    }

    [Fact]
    public async Task A_replayed_message_is_published_again_rather_than_skipped()
    {
        await SeedCompletedJob();
        await Processor().DrainAsync(default);
        await ResetProcessedOn();

        await Processor().DrainAsync(default);

        // The pipeline does not deduplicate; the consumers do (4.5). Asserting
        // it here is what stops someone "fixing" a duplicate in the wrong place.
        Handled.Should().HaveCount(2);
    }

    [Fact]
    public async Task Two_concurrent_drains_never_handle_the_same_row()
    {
        await SeedManyJobs(20);

        await Task.WhenAll(Processor().DrainAsync(default), Processor().DrainAsync(default));

        // FOR UPDATE SKIP LOCKED. Without it two workers read the same batch
        // and every consequence happens twice — which the constraints absorb,
        // but at the cost of doing all the work twice on every poll.
        Handled.Select(message => message.Id).Should().OnlyHaveUniqueItems();
        (await UnprocessedRows()).Should().Be(0);
    }

    [Fact]
    public async Task The_oldest_message_is_drained_first()
    {
        await SeedManyJobs(3);

        await Processor(batchSize: 1).DrainAsync(default);

        Handled.Single().Id.Should().Be(await OldestRowId());
    }

    [Fact]
    public async Task A_batch_larger_than_the_backlog_is_not_an_error()
    {
        await Processor().DrainAsync(default);

        Handled.Should().BeEmpty();
    }
```

- [ ] **Step 2: Run to verify they fail, then write `OutboxProcessor`**

```csharp
/// <summary>
/// Architecture 4.4, role 1. Every ten seconds it takes a batch of unprocessed
/// rows, publishes each through MediatR, and stamps the ones that succeeded.
///
/// The batch is selected FOR UPDATE SKIP LOCKED inside an explicit transaction,
/// so several workers poll concurrently and each row is handled once. Without
/// it two workers read the same batch and every consequence happens twice —
/// which the consumers' constraints absorb, but at the cost of doing all the
/// work twice on every poll.
/// </summary>
internal sealed class OutboxProcessor(
    JobsDbContext context, IPublisher publisher, TimeProvider time, IOptions<OutboxOptions> options)
{
    public async Task DrainAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var batch = await context.Set<OutboxMessage>()
            .FromSql($"""
                      select * from jobs.outbox_messages
                      where processed_on is null
                      order by occurred_on
                      limit {options.Value.BatchSize}
                      for update skip locked
                      """)
            .ToListAsync(cancellationToken);

        foreach (var message in batch)
        {
            // Per message, not per batch: one poison message must not stop
            // every unrelated job in the system from billing or notifying.
            try
            {
                await publisher.Publish(
                    OutboxSerializer.Deserialize(message.Type, message.Content), cancellationToken);

                message.MarkProcessed(time.GetUtcNow());
            }
            catch (Exception exception)
            {
                // Left unprocessed on purpose. The next drain tries again, and
                // the error is a note for whoever reads the table.
                message.RecordFailure(exception.ToString());
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
```

`catch (Exception)` is the one place a broad catch is right, and the comment says why: the alternative is a single failure stopping the queue.

- [ ] **Step 3: Wire Hangfire**

`AddJobsModule` gains the storage and the recurring job; `Program.cs` adds the server and, in Development, the dashboard. Add an integration test asserting the recurring job is registered under its expected identifier, so the wiring cannot rot into decoration.

- [ ] **Step 4: Assert the licence pin**

```csharp
    [Fact]
    public void Hangfire_is_the_open_source_package_rather_than_Pro()
    {
        // D-20. Hangfire's LICENSE.md is multi-licensed with LGPL v3 among the
        // options, which referencing the unmodified package satisfies. The paid
        // tier is the separate Hangfire.Pro.* packages, and a reviewer running
        // dotnet restore without a licence is who finds out otherwise.
        AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetName().Name)
            .Should().NotContain(name => name!.StartsWith("Hangfire.Pro", StringComparison.Ordinal));
    }
```

- [ ] **Step 5: Verify green and commit**

---

### Task 3: Notifications, FR-8

**Files:**
- Create: `Jobs.Domain/Notification.cs`, `NotificationErrors.cs`, `INotificationRepository.cs`
- Create: `Jobs.Application/Abstractions/INotificationSender.cs`, `IBackgroundQueue.cs`
- Create: `Jobs.Application/Notifications/NotifyAssigneeOnJobCreatedHandler.cs`, `SendNotification.cs`
- Create: `Jobs.Infrastructure/Notifications/LoggingNotificationSender.cs`, `NotificationRepository.cs`, `Configurations/NotificationConfiguration.cs`, `HangfireBackgroundQueue.cs`
- Create: a migration
- Create: `tests/…Domain.UnitTests/NotificationTests.cs`, `tests/JobTracker.IntegrationTests/Pipeline/NotificationTests.cs`

**Interfaces:**
- Consumes: `JobCreatedDomainEvent`, `IPartyRepository`
- Produces: `Notification`, `INotificationSender`, `IBackgroundQueue`

- [ ] **Step 1: Write the failing domain tests**

`Notification` is `Pending → Sent | Failed`, and `MarkSent` refuses from any state but `Pending` — the same terminal-state rule as BR-2, and it gets the same treatment: a test that a terminal notification refuses every further transition, and one that a refused transition changes nothing.

- [ ] **Step 2: Write `Notification`**

```csharp
/// <summary>
/// A record inside Jobs rather than a module (D-22). Notifying has no
/// invariants of its own beyond this one lifecycle, and a module would be the
/// anemic kind D-04 gave Billing a real domain in order to avoid.
///
/// Delivery is simulated; the record is not. A reviewer verifies FR-8 and FR-10
/// with `select status, recipient from jobs.notifications`, which is stronger
/// evidence than a log line and is what makes NFR-3 checkable in the data.
/// </summary>
public sealed class Notification : Entity, ITenantScoped
{
    public static Result<Notification> Draft(
        Guid sourceEventId, Guid organizationId, string recipient,
        string subject, string body, DateTimeOffset now) { /* … */ }

    public Result MarkSent(DateTimeOffset sentAt) { /* refuses unless Pending */ }

    public Result MarkFailed(string reason, DateTimeOffset failedAt) { /* … */ }
}
```

- [ ] **Step 3: Write the failing integration tests, including case 9**

```csharp
    [Fact]
    public async Task Creating_a_job_notifies_the_assignee()
    {
        // FR-8, through the real pipeline: save, drain, read the table.
        await CreateJobThroughTheRepository();
        await Drain();

        var notification = await Context.Notifications.SingleAsync();
        notification.Recipient.Should().Be("J. Ortiz");
    }

    [Fact]
    public async Task A_replayed_creation_does_not_notify_twice()
    {
        // Case 9 of §8.2, and the whole of 4.5: the constraint is what makes
        // the handler idempotent, and a test that never inserts twice never
        // checks it.
        await CreateJobThroughTheRepository();
        await Drain();
        await ResetProcessedOn();
        await Drain();

        (await Context.Notifications.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task The_duplicate_is_absorbed_rather_than_thrown()
    {
        // If the handler let the unique-constraint violation escape, the outbox
        // row would stay unprocessed forever and the drain would retry it every
        // ten seconds until someone noticed.
        await CreateJobThroughTheRepository();
        await Drain();
        await ResetProcessedOn();
        await Drain();

        (await UnprocessedRows()).Should().Be(0);
    }

    [Fact]
    public async Task The_notification_is_scoped_to_its_organization()
    {
        /* the other tenant's context sees none */
    }

    [Fact]
    public async Task Sending_moves_it_from_Pending_to_Sent()
    {
        /* run the queued send inline, assert status and sent_at */
    }
```

- [ ] **Step 4: Write the handler, the sender and the queue port**

`IBackgroundQueue` is a port in `Jobs.Application` with `HangfireBackgroundQueue` behind it. Application must not name Hangfire — the same reason it must not name EF — and the port is what lets the tests run the send inline and deterministically instead of standing up a job server.

- [ ] **Step 5: Migration, run green, commit**

---

### Task 4: The contract, and FR-10

**Files:**
- Create: `JobTracker.Modules.Jobs.IntegrationEvents/` (project), `JobCompletedIntegrationEvent.cs`
- Create: `Common.Infrastructure/EventBus.cs`
- Create: `Jobs.Application/Notifications/PublishJobCompletedHandler.cs`, `NotifyCustomerOnJobCompletedHandler.cs`
- Modify: `JobCompletedDomainEvent` gains `StartedAt`
- Create: architecture rule additions

**Interfaces:**
- Consumes: `JobCompletedDomainEvent`, `IEventBus`
- Produces: `JobCompletedIntegrationEvent` — the only thing Billing may see

- [ ] **Step 1: Write the contract**

```csharp
namespace JobTracker.Modules.Jobs.IntegrationEvents;

/// <summary>
/// Jobs' public statement, and deliberately poorer than the domain event: a
/// consumer gets identifiers and timestamps, never a <c>Job</c>.
///
/// It carries StartedAt and CompletedAt rather than an amount, because Jobs
/// does not know what work costs and must not learn (D-33). Billing owns the
/// rate. That division is what makes the boundary worth having — Jobs says what
/// happened, Billing decides what it is worth.
/// </summary>
public sealed record JobCompletedIntegrationEvent(
    Guid EventId,
    Guid JobId,
    Guid CustomerId,
    Guid OrganizationId,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);
```

`EventId` travels so a consumer's idempotency key can derive from it — the same stability argument as D-34, carried across the boundary.

- [ ] **Step 2: Write the failing tests, then the two handlers**

`PublishJobCompletedHandler` translates and publishes; it holds no idempotency key and needs none (4.5), because all three consumers absorb a duplicate. `NotifyCustomerOnJobCompletedHandler` writes a second `jobs.notifications` row keyed `(source_event_id, recipient)` — a different recipient from FR-8's, which is why one constraint serves both.

- [ ] **Step 3: Add the architecture rules**

```csharp
    [Fact]
    public void The_integration_events_project_carries_primitives_only()
    {
        // A contract that references a domain type is a contract that drags the
        // domain across the boundary the moment a consumer compiles against it.
        ProjectReferencesOf("Modules/Jobs/JobTracker.Modules.Jobs.IntegrationEvents")
            .Should().BeEmpty();
    }
```

That one rule is worth more than several: a project with no references cannot leak anything.

- [ ] **Step 4: Commit**

---

### Task 5: Billing

**Files:**
- Create: `JobTracker.Modules.Billing.Domain/` (project), `Invoice.cs`, `InvoiceErrors.cs`, `IInvoiceRepository.cs`
- Create: `JobTracker.Modules.Billing.Application/` (project), `LabourRate.cs`, `GenerateInvoiceOnJobCompletedHandler.cs`
- Create: `JobTracker.Modules.Billing.Infrastructure/` (project), `BillingDbContext.cs`, `Configurations/InvoiceConfiguration.cs`, `Repositories/InvoiceRepository.cs`, `BillingModule.cs`, migrations
- Create: `tests/JobTracker.Modules.Billing.UnitTests/` (project)
- Create: `tests/JobTracker.IntegrationTests/Pipeline/BillingTests.cs`

**Interfaces:**
- Consumes: `JobCompletedIntegrationEvent` and nothing else from Jobs
- Produces: `billing.invoices`

- [ ] **Step 1: Write the failing domain tests**

`Invoice` has real invariants: a positive amount, an immutable issue date, and the labour window it was priced from. The pricing rule lives in Billing:

```csharp
/// <summary>
/// What Billing knows that Jobs must not: labour is charged by the hour with a
/// minimum call-out. A completion of eleven minutes still costs the minimum,
/// which is why the amount can be positive for a window that rounds to nothing
/// — and why the CHECK on the column is not the same rule as this one.
/// </summary>
public sealed record LabourRate(decimal PerHour, decimal Minimum)
```

Tests: a two-hour job costs twice the hourly rate; a five-minute job costs the minimum; a completion before its start is refused rather than priced negative.

- [ ] **Step 2: Write the failing integration tests**

```csharp
    [Fact]
    public async Task Completing_a_job_raises_an_invoice()
    {
        // FR-9, and the second half of walkthrough step 9.
    }

    [Fact]
    public async Task A_replayed_completion_raises_no_second_invoice()
    {
        // uq_invoices_idempotency (job_id, job_completed_at) — the key line 246
        // asks for by name, and case 9 of §8.2.
    }

    [Fact]
    public async Task The_invoice_amount_comes_from_the_labour_window()
    {
    }

    [Fact]
    public async Task Billing_writes_only_into_its_own_schema()
    {
        // 6.1. A module that writes into another's schema has a boundary in the
        // diagram and none in the database.
    }
```

- [ ] **Step 3: Write the module, register it, run green**

- [ ] **Step 4: Add the layer rule that matters most here**

```csharp
    [Fact]
    public void Billing_cannot_see_the_Jobs_domain()
    {
        // §3.5's whole claim. Billing learns a job completed from a record of
        // primitives, and this is what stops that from being an aspiration.
        ProjectReferencesOf("Modules/Billing/JobTracker.Modules.Billing.Application")
            .Should().NotContain("JobTracker.Modules.Jobs.Domain");
        ProjectReferencesOf("Modules/Billing/JobTracker.Modules.Billing.Application")
            .Should().Contain("JobTracker.Modules.Jobs.IntegrationEvents");
    }
```

- [ ] **Step 5: Commit**

---

### Task 6: Steps 4 and 9, over HTTP

**Files:**
- Create: `tests/JobTracker.IntegrationTests/Api/PipelineEndToEndTests.cs`

**Interfaces:**
- Consumes: the API from plan 3C, the pipeline from tasks 1-5
- Produces: the executable form of walkthrough steps 4 and 9

The browser-level smoke against Compose is plan 5. This proves the same two steps one layer down, which is what makes plan 5 a confirmation rather than a debugging session.

- [ ] **Step 1: Write the test**

```csharp
    [Fact]
    public async Task The_whole_walkthrough_leaves_an_invoice_and_two_notifications()
    {
        var id = await CreateJob();                       // steps 1-3
        await DrainUntilQuiet();                          // step 4
        await Start(id);
        await Complete(id);                               // steps 6-8
        await DrainUntilQuiet();                          // step 9

        // Neither consequence appears in the interface — design A5 step 6 says
        // completion does not claim they happened — so the assertion reads the
        // database the system just wrote. That is the normal shape of an
        // integration test, not a leaked abstraction (§8.1).
        (await Invoices(id)).Should().ContainSingle();
        (await Notifications()).Should().HaveCount(2);
    }
```

`DrainUntilQuiet` polls with a bounded timeout rather than sleeping, because NFR-4 promises the consequences arrive *within seconds*. Asserting on a bound is asserting on eventual consistency; sleeping a fixed interval pretends it is synchronous and fails on a slow machine.

- [ ] **Step 2: Run green, record D-33 and D-34, commit**

---

## Self-Review

**Spec coverage.** Architecture 4.1's two event kinds: the contract project in task 4, with `JobCreated` and `JobCancelled` staying internal as the counter-examples 4.4 names. 4.2's interceptor and 4.3's at-least-once: task 1 and task 2, including the rollback case a unit test cannot reach. 4.4's two Hangfire roles: the recurring drain in task 2, the fire-and-forget send in task 3. 4.5's table of four consumers: notifications in tasks 3 and 4, the invoice in task 5, and the translating handler which needs none. 4.6's three pieces: task 3. §8.2 case 5 is task 1 and case 9 is tasks 3 and 5. §8.1's steps 4 and 9: task 6.

**What this plan deliberately does not test.** Hangfire's scheduler. A test that starts a job server and waits for a tick would be slow, time-dependent, and would be testing Hangfire rather than this system. The processor is called directly, and one test asserts the recurring job is registered so the wiring cannot rot into decoration. If that trade is wrong, the fix is a single slow test in plan 5's smoke, not six of them here.

**The riskiest step, named.** `FOR UPDATE SKIP LOCKED` through `FromSql` returns tracked entities, and EF must map every column of `outbox_messages` for it to work — a mismatch surfaces as a runtime error on the first drain rather than at compile time. The concurrency test is what catches it, and it is written before the processor exists.

**One thing left for plan 5.** The Hangfire dashboard is registered in Development only, and nothing yet protects it. In a Compose stack it is reachable to anyone who can reach the API. Plan 5 either puts it behind the same authorization as everything else or removes it; leaving it as an open route would be the same class of mistake the fallback policy was added to prevent.
