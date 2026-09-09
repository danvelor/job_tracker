# JobTracker — Architecture

| | |
|---|---|
| **Document** | How the system is structured, and the rules development must obey |
| **Companions** | `context/prd.md` (what it does), `context/design.md` (concrete artefacts) |
| **Binding rules** | Section 9. `CLAUDE.md` points there; NetArchTest enforces it |
| **Source** | `context/request/technical-assessment-fullstack-senior 3 1.md` |

This document holds structure, boundaries and rationale. It does not hold
concrete artefacts: exact DDL, file inventories and type signatures live in
`context/design.md`. When a fact could sit in either, the rule is **strategy
here, artefact there**.

---

## 1. Stack

| Concern | Choice | Notes |
|---|---|---|
| Backend runtime | .NET 9 | Mandated |
| Frontend framework | Next.js 15, App Router | Mandated |
| Language | C# 13, TypeScript 5.x with `strict: true` | `strict` is mandated (line 21) |
| Database | PostgreSQL 17 | Mandated |
| Mediator | MediatR **12.x, pinned** | v13 and later are commercially licensed. Pinning keeps the deliverable buildable by a reviewer with no licence |
| Validation | FluentValidation 11 | Mandated (line 196) |
| ORM | EF Core 9 with Npgsql | |
| Background jobs | Hangfire 1.8 with `Hangfire.PostgreSql` | Reuses the same Postgres instance in a `hangfire` schema |
| Server state (frontend) | SWR 2 | Owns fetched data. Lighter than React Query and sufficient: optimistic behaviour is implemented in the store, not the fetcher |
| Client state (frontend) | Zustand 5 | UI state only. See section 5.5 |
| Styling | Tailwind CSS v4 | Utility-first keeps atoms free of their own stylesheets and adds no runtime |
| Backend tests | xUnit, Moq, **FluentAssertions 7.x, pinned**, NetArchTest, Testcontainers.PostgreSql | Mandated (lines 313-316, 440). FluentAssertions 8 and later are commercially licensed, so the pin is the same defence as MediatR's: a reviewer must be able to `dotnet test` without buying anything (D-20) |
| Frontend tests | Jest with `next/jest`, React Testing Library, `expect-type` | See decision D-07 |
| End-to-end tests | Playwright | Mandated |
| Local orchestration | Docker Compose | Mandated (line 490) |
| CI | GitHub Actions | Bonus, in scope (+3) |

Out of scope by decision: OpenTelemetry and the accessibility bonus. Rate
limiting is in scope (+2).

---

## 2. Solution topology

```
job_tracker/
├── CLAUDE.md                  binding rules and pointers, loaded every session
├── docker-compose.yml         postgres + backend + frontend
├── .github/workflows/ci.yml   lint, typecheck, test, build
├── context/                   prd.md, architecture.md, design.md, request/
├── database/                  schema.sql, indexes.sql, queries.sql (deliverable 3)
├── docs/
│   ├── design-principles.md   part 6: diagram, SOLID, GRASP, GoF, DDD concepts
│   └── normalization.md       part 4.3: denormalization vs integration events
├── backend/
│   ├── JobTracker.sln
│   ├── Directory.Packages.props                      central pins, incl. the two in D-20
│   ├── src/
│   │   ├── Api/JobTracker.Api/                       host and composition root
│   │   ├── Common/
│   │   │   ├── JobTracker.Common.Domain/             Entity, AggregateRoot, ValueObject, Result, IDomainEvent
│   │   │   ├── JobTracker.Common.Application/        IUnitOfWork, PagedList, pipeline behaviours
│   │   │   ├── JobTracker.Common.Infrastructure/     outbox interceptor, tenant context, Hangfire wiring
│   │   │   └── JobTracker.Common.Presentation/       ProblemDetails mapping, endpoint conventions
│   │   └── Modules/
│   │       ├── Jobs/
│   │       │   ├── ...Jobs.Domain/
│   │       │   ├── ...Jobs.Application/
│   │       │   ├── ...Jobs.Infrastructure/
│   │       │   ├── ...Jobs.IntegrationEvents/        public contract, referenced by other modules
│   │       │   └── ...Jobs.Presentation/
│   │       └── Billing/
│   │           ├── ...Billing.Domain/
│   │           ├── ...Billing.Application/
│   │           └── ...Billing.Infrastructure/
│   └── tests/
│       ├── ...Jobs.Domain.UnitTests/
│       ├── ...Jobs.Application.UnitTests/
│       ├── ...Billing.Application.UnitTests/         invariants, handler, double delivery
│       ├── JobTracker.IntegrationTests/              Testcontainers: real Postgres, real EF
│       └── JobTracker.ArchitectureTests/
└── frontend/
    └── src/
        ├── app/                    App Router routes only
        ├── core/                   domain and application, framework-agnostic
        ├── infrastructure/         adapters implementing core ports
        ├── presentation/           views, components, stores
        └── shared/                 type-level utilities
```

---

## 3. Backend — modular monolith

### 3.1 Module anatomy

Every module is a vertical slice with its own four layers, plus a published
contract project **when the module publishes something**. Jobs has one; Billing
does not, for the reason given in 3.5:

| Project | Contains | May reference |
|---|---|---|
| `Domain` | Aggregates, entities, value objects, domain events, repository interfaces, domain errors | `Common.Domain` only |
| `Application` | Commands, queries, handlers, validators, DTOs, domain-event handlers, integration-event handlers | own `Domain`, `Common.Application`, own and **other modules'** `IntegrationEvents` |
| `Infrastructure` | `DbContext`, EF configurations, repository implementations, outbox processing, adapters | own `Application`, own `Domain`, `Common.Infrastructure` |
| `IntegrationEvents` | Public event contracts. Records with primitive members and no behaviour | nothing |
| `Presentation` | HTTP endpoints | own `Application`, `Common.Presentation` |

`IntegrationEvents` references nothing on purpose. It is the module's Open Host
Service: the only surface another module is allowed to compile against, and it
stays free of any dependency that could leak a module's internals.

**Jobs has five projects and Billing has three, and both counts are right.** The
four layers that line 344 names — API, Application, Domain, Infrastructure — are
what a module with an HTTP surface needs, and deliverable 2 asks for them of
*the Jobs module* by name. Billing has no HTTP surface, so it has no
`Presentation` (D-04), and it publishes no contract, so it has no
`IntegrationEvents` (D-24). Adding either as an empty project would turn a
count into the requirement, when the requirement is the dependency direction —
which Billing's three projects obey exactly as Jobs' five do.

### 3.2 Dependency direction

```
                     ┌──────────────────────┐
                     │   JobTracker.Api     │  composition root, references everything
                     └──────────┬───────────┘
                                │
        ┌───────────────────────┼───────────────────────┐
        ▼                       ▼                       ▼
  Presentation           Infrastructure           (module registration)
        │                       │
        └──────────┬────────────┘
                   ▼
              Application ──────▶ Jobs.IntegrationEvents  (own contract, publishes)
                   ▼
                Domain
                   │
                   ▼
            Common.Domain
```

Two rules, both enforced by architecture tests:

1. **No inward reference to an outer layer.** `Domain` never names
   `Application`, `Infrastructure` or `Presentation`. `Application` never names
   `Infrastructure`.
2. **No module names another module's internals.** A module may reference
   another module's `IntegrationEvents` and nothing else. `Jobs` and `Billing`
   share no type outside those projects.

The only place both modules meet is `JobTracker.Api`, which composes them and
knows nothing about either domain.

### 3.3 Shared kernel

`Common.Domain` carries the primitives every module's domain needs and nothing
that expresses policy:

- `Entity` — identity equality on `Id`
- `AggregateRoot` — `Entity` plus a domain-event collection with `Raise` and
  `ClearDomainEvents`
- `ValueObject` — structural equality by comparing an ordered projection of
  components, with `Equals`, `GetHashCode` and the equality operators
- `Result` and `Result<T>` — success or an `Error`, so application flow never
  uses exceptions for expected failures
- `Error` — code plus message, mapped to a response shape in `Presentation`
- `IDomainEvent` — marker, a MediatR `INotification`

`Common.Application` carries `IUnitOfWork`, `PagedList<T>`, `IEventBus`, and the
MediatR pipeline behaviours for validation and logging.

Two of those need their shape pinned, because both are easy to assume wrongly:

- **`PagedList<T>` is cursor-shaped, not page-shaped.** It is
  `{ IReadOnlyList<T> Items; string? NextCursor; bool HasMore }` — no `Page`,
  no `PageSize`, and above all **no `TotalCount`**. A total would require a
  second aggregate query per keystroke, which is exactly the cost `NFR-5`
  rejects, and `context/design.md` A2 already decides the interface shows loaded
  rows rather than matches. Line 207 mandates the type name; it says nothing
  about its members
- **`IEventBus` publishes integration events.** One method,
  `PublishAsync<T>(T integrationEvent, CancellationToken)`, implemented in
  `Common.Infrastructure` over MediatR. It exists so that a module publishing a
  contract does not name the mediator, which is what would have to change if
  the modules ever became separate deployables (D-03)

A rule that matters for keeping the kernel a kernel: **nothing about jobs,
invoices, tenancy or scheduling belongs in `Common`**. If a type would only ever
be used by one module, it lives in that module.

### 3.4 Jobs module

`Job` is the aggregate root and the only entry point to its data. It owns:

- an `Address` value object, structurally equal and mapped as owned columns
- a private collection of `JobPhoto` entities, exposed only as
  `IReadOnlyCollection` and mutated only through aggregate methods
- its `JobStatus`, changed only through intention-named methods —
  `Reschedule`, `Start`, `Complete`, `Cancel` — never through a setter

Every invariant in `context/prd.md` section 6 is enforced inside those methods
and returns a `Result` rather than throwing. `Job` has no public setters at all;
the model is not anemic because the rules live with the data they constrain.

Because `BR-1` compares against the current time, the aggregate takes the
current instant as a parameter rather than reading the clock. The handler
supplies it from `TimeProvider`, which makes the invariant testable without
freezing global state.

Domain events raised: `JobCreatedDomainEvent`, `JobCompletedDomainEvent`,
`JobCancelledDomainEvent`.

### 3.5 Billing module

Billing exists to prove that a completed job produces an invoice without the two
modules being coupled. It has a real domain rather than a stub handler:

- `Invoice` aggregate with its own invariants (a positive amount, an immutable
  issue date, one invoice per job completion)
- `IInvoiceRepository` in its domain, EF implementation in its infrastructure
- an integration-event handler in its application layer consuming
  `Jobs.IntegrationEvents.JobCompletedIntegrationEvent`
- its own `billing` schema

Billing never references `Jobs.Domain`. It learns that a job completed from a
contract record carrying primitives, which is the whole point of the boundary.

**Billing publishes no contract of its own, and has no `IntegrationEvents`
project.** Nothing needs to learn that an invoice was raised: `context/prd.md`
section 9 puts collection, tax and invoice documents out of scope, and
`context/design.md` A5 step 6 states that the interface does not claim the
invoice has happened. An empty contract project would document an intention
rather than a capability. The flow is one-way — Jobs publishes, Billing consumes
— and the rule in 3.1 is already in place for the day that changes (D-24).

### 3.6 Command and query separation

The application layer of every module is split in two: commands that change
state and queries that report it, each with its own handler, resolved by
MediatR. Section 9.1 fixes the names and modifiers and NetArchTest enforces
them.

**What is separated is responsibility, not storage.** This is worth stating
because CQRS is often read as "a write database and a read database", and that
is a different thing. Here there is one PostgreSQL instance, one `DbContext` per
module, one set of tables, and no event sourcing. Commands and queries meet the
same rows; what differs is the model each uses to do it. The consequence is that
this system is **strongly consistent**: a job written by a command is visible to
the very next query, with no projection lag to reason about.

A separate read store buys two things — read capacity scaled independently, and
a shape the transactional schema cannot serve cheaply — and it charges eventual
consistency between the two sides for them. Neither benefit applies to a job
list served from an indexed table in the same database, so the price is not
worth paying, and `NFR-5` is met by the keyset index rather than by a second
copy of the data.

**The asymmetry it does buy is real.** The two sides load different things
because they need different things:

| | Write side | Read side |
|---|---|---|
| Loads | The whole `Job` aggregate, tracked | A projection, `AsNoTracking` |
| Why | An invariant cannot be checked on a partial object: `Complete` needs the status, the start time and the photo collection at once | Nobody needs an aggregate to paint a table row, and materialising one per row would pull photos and change-tracking state that the response discards |
| Returns | `Result` or `Result<Guid>` — an outcome, not data | `Result<PagedList<JobResponse>>` — data, no side effect |
| Example | `IJobRepository.GetByIdAsync` → `Job` | `IJobRepository.SearchAsync` → `JobSearchResult` (D-25) |

That is why the same repository interface returns an aggregate from one method
and a record from another. It looks inconsistent until the two methods are read
as serving opposite sides.

The practical payoff is that the two sides change for different reasons. Adding
`BR-7` would touch the aggregate and no query. Adding a column to the job table
would touch the projection and no invariant. A single model would have both
edits landing in the same class, which is how a service class becomes a
thousand lines.

**Where the separation is not pushed further.** No command handler returns data
beyond the identifier of what it created, and no query handler writes — a query
that logged an audit row would be the usual way this erodes. There is no
separate read model, no denormalised view table, and no second database, for the
reason above. If the job list ever outgrew the index, the first step would be a
materialised view refreshed from the outbox, and only the query handler would
change — which is the property the separation exists to preserve.

---

## 4. Asynchronous pipeline

### 4.1 Domain events versus integration events

Both are "something happened", and the difference is who is allowed to care.

A **domain event** is internal to a module. It names something meaningful in
that module's own language (`JobCompletedDomainEvent`), it may carry the
aggregate's own types, and it may change shape whenever the module changes,
because only the module compiles against it. Its purpose is decoupling *within*
a boundary: `Job` records that it completed without knowing that anything reacts.

An **integration event** is a published contract. It crosses the module boundary,
so it carries primitives only, is versioned, and cannot change without breaking
consumers. `Jobs.IntegrationEvents.JobCompletedIntegrationEvent` is the module's
public statement, and it is deliberately poorer than the domain event: consumers
get a job identifier, a customer identifier, a completion timestamp and an
amount basis — not a `Job`.

Collapsing the two would mean either exposing `Jobs.Domain` to `Billing`, or
letting `Billing`'s needs dictate the shape of an internal event. Both are the
coupling the boundary exists to prevent.

### 4.2 Transactional outbox

`InsertOutboxMessagesInterceptor` hooks EF Core's `SavingChangesAsync`. Before
the write, it collects domain events from every tracked aggregate, serialises
each into `jobs.outbox_messages`, and clears them from the aggregates.

Because the interceptor runs inside the same `SaveChangesAsync`, the outbox rows
and the job's state change commit or roll back **together**. There is no window
in which a job is completed but its consequences are absent, and none in which
consequences are recorded for a completion that rolled back.

**Deviation from the assessment, and why.** Lines 236-238 describe the
interceptor persisting the *integration* event. That ordering cannot work: the
interceptor runs during `SaveChanges`, whereas the integration event is produced
by a handler that only runs after the domain event is published — which happens
after the commit. Persisting the domain event and translating it downstream is
the ordering that actually holds the transactional guarantee. The integration
event type still lives in `Jobs.IntegrationEvents` exactly as line 237 requires.

### 4.3 Why the outbox gives at-least-once delivery

A message is only ever removed from the pipeline after its consequence is known
to have been carried out. Concretely: the row is written in the job's
transaction, so it cannot be lost; `processed_on` is stamped only after the
handlers return; and a crash at any point leaves the row unprocessed, so the
next poll picks it up again.

What this cannot give is exactly-once. A crash *between* the handler succeeding
and `processed_on` being stamped will replay the message. That is not a defect to
be engineered away — it is the reason `NFR-3` exists and section 4.5 is
mandatory.

### 4.4 Hangfire's two roles

Hangfire appears twice, in roles worth keeping distinct:

1. **A recurring job drains the outbox.** Every ten seconds it selects a batch
   of unprocessed rows with `FOR UPDATE SKIP LOCKED`, so several workers can
   poll concurrently without handling the same row twice. It deserialises each
   domain event, publishes it through MediatR, and stamps `processed_on`.
2. **Fire-and-forget jobs perform each outbound send.** The notification handler
   does not call the sender inline; it enqueues a job. Each send then gets its
   own retry schedule with exponential backoff, and a slow or failing transport
   cannot stall the outbox drain.

Both are durable because Hangfire persists its own state in Postgres, in the
`hangfire` schema.

The pipeline end to end:

```
Job.Create()                              Job.Complete()
  │ raises JobCreatedDomainEvent            │ raises JobCompletedDomainEvent
  └──────────────────┬─────────────────────-┘
                     ▼
InsertOutboxMessagesInterceptor ── same transaction ──▶ jobs.outbox_messages
                     │
                     ▼  [Hangfire recurring job, 10s]
OutboxProcessor  ── FOR UPDATE SKIP LOCKED ──▶ MediatR publish
     │
     ├─▶ NotifyAssigneeOnJobCreatedHandler        (FR-8, stays inside Jobs)
     │        └─▶ jobs.notifications  Pending ─┐
     │                                         │
     ├─▶ JobCompletedDomainEventHandler ──▶ JobCompletedIntegrationEvent
     │        │                                │
     │        ├─▶ Billing: GenerateInvoiceOnJobCompletedHandler   (FR-9)
     │        │        └─▶ billing.invoices, unique (job_id, job_completed_at)
     │        │
     │        └─▶ NotifyCustomerOnJobCompletedHandler             (FR-10)
     │                 └─▶ jobs.notifications  Pending ─┤
     │                                                  │
     └─▶ processed_on stamped                           │
                              Hangfire fire-and-forget  │
                                                        ▼
                              INotificationSender ──▶ log line ──▶ MarkSent
```

**`FR-8` is the counter-example that makes section 4.1 concrete.**
`JobCreatedDomainEvent` never becomes an integration event, because notifying the
assignee stays inside Jobs and no other module needs to know. `JobCompleted`
does cross, because Billing exists. One event of each kind is what makes the
distinction demonstrable rather than merely stated. `JobCancelledDomainEvent` is
a second internal-only case: cancelling neither bills nor notifies.

The poller is the only piece a message broker would replace. Nothing in
`Domain`, `Application` or the contracts would change, which is why a broker is
not warranted while both modules share a process and a database — see D-03.

### 4.5 Idempotency

One rule, applied by every consumer:

> **Idempotency is a property each consumer must have, and each one earns it with
> a unique constraint on the data it writes.**

| Consumer | Its key |
|---|---|
| `GenerateInvoiceOnJobCompletedHandler` | `uq_invoices_idempotency (job_id, job_completed_at)` — the key line 246 asks for by name |
| `NotifyCustomerOnJobCompletedHandler` | `uq_notifications_idempotency (source_event_id, recipient)` |
| `NotifyAssigneeOnJobCreatedHandler` | the same constraint, a different row |
| `JobCompletedDomainEventHandler` | none, and none is needed: it only translates and publishes, and all three consumers above absorb a duplicate |

The condition that makes the rule sufficient is that every key derives from
something **stable across replays**. `(job_id, completed_at)` does not change,
and `source_event_id` is the outbox row's identifier, which does not either.

**Why there is no generic deduplication table.** An earlier design had
`outbox_message_consumers` keyed by `(outbox_message_id, handler_name)`, and it
was dropped (D-23). It protected nothing the constraints above do not, its key
included a handler name and therefore broke on a rename, and it forced Billing to
write into the `jobs` schema — contradicting the boundary in 6.1. A consumer with
no natural business key would justify bringing it back; none exists.

### 4.6 Notifications

Notification lives **inside Jobs**, as a record rather than a module (D-22).
There is no `Notifications` module: notifying has no invariants and no lifecycle
of its own, so a module would be the anemic kind that D-04 gave Billing a real
domain in order to avoid.

Three pieces:

| Piece | Where | What it does |
|---|---|---|
| `Notification` | `Jobs.Domain` | An entity with `Pending → Sent \| Failed`. `MarkSent` refuses from any state but `Pending`, which is the same terminal-state rule as `BR-2` |
| `INotificationSender` | `Jobs.Application` | The port. Takes a recipient, a subject and a body; reports success or a reason |
| `LoggingNotificationSender` | `Jobs.Infrastructure` | The adapter. Writes one structured log line and returns success |

The handler creates the row as `Pending`, the Hangfire fire-and-forget job calls
the sender, and the handler then calls `MarkSent` or `MarkFailed`. **Delivery is
simulated; the record is not.** A reviewer verifies `FR-8` and `FR-10` with
`select status, recipient from jobs.notifications`, which is stronger evidence
than a log line alone and is what makes `NFR-3` enforceable in the data.

No mail transport is involved and no container serves one. Line 241 names
SendGrid; real deliverability is out of scope by `context/prd.md` section 9 and
the rubric scores none of it — what is graded is the reliability of the
pipeline, and the pipeline is fully exercised whether the last hop is an SMTP
socket or a log write (D-08).

---

## 5. Frontend architecture

### 5.1 Layers

```
src/
├── app/               Next routes. Composition only: no business logic, no fetching logic
├── core/
│   ├── domain/        JobState union, transitions, invariants. No framework imports
│   └── application/   use cases and the ports they depend on
├── infrastructure/    adapters implementing core ports
├── presentation/      views, shared components, stores
└── shared/            type-level utilities used across layers
```

`core` imports nothing from React, Next or any adapter. That is what makes the
use cases callable from a Server Component, a Server Action, and a test with no
change.

### 5.2 Server and client boundary

`app/jobs/page.tsx` is a Server Component. It imports `server-only` as its first
statement, so any accidental client import of that module fails the build rather
than leaking server code into the bundle.

It resolves a use case from the DI container and invokes it. `'use client'`
appears only on leaf components that need interactivity, never on a page.

**Reads and writes take different paths, as line 138 requires.** Queries are
executed in the Server Component or by SWR on the client. Server Actions exist
only for the two mutations — create and complete — and live inside their feature
slice.

### 5.3 Making Suspense real

Line 104 asks for `<Suspense>` with a skeleton, and line 103 asks the page to
pass data as props. Done naively those conflict: `await`ing the use case in the
page resolves the data before React renders, so the boundary never suspends and
the skeleton is never seen.

The page therefore **does not await**. It passes the unresolved promise as a prop
into a component inside the boundary, which unwraps it with `use()`:

```
page.tsx (server)
  const jobsPromise = container.searchJobs.execute(filters)   // no await
  <Suspense fallback={<JobsTableSkeleton />}>
    <JobsClient jobsPromise={jobsPromise} />                   // 'use client'
  </Suspense>
```

Both requirements now hold literally, and the skeleton is real. `loading.tsx`
still covers the route-level navigation transition, which is a different event
from the list resolving.

### 5.4 Ports and adapters

`core/application` declares `JobsPort` — the operations the use cases need, in
domain terms. Two adapters implement it:

| Adapter | Role |
|---|---|
| `HttpJobsAdapter` | Calls the .NET API. Attaches the bearer token, maps DTOs to core types, maps `ProblemDetails` to core errors |
| `InMemoryJobsAdapter` | Holds a seeded array. Default in development and CI |

The container selects one from configuration. The consequence that justifies the
second adapter: **Playwright runs against the in-memory adapter**, so the
end-to-end suite needs neither the backend nor Postgres in CI, and the same
seeded data serves as the fixture for hook tests. One smoke run exercises the
HTTP adapter against the full Compose stack.

### 5.5 State ownership

Section 2.2 of the assessment contradicts itself: point 1 asks the store to hold
`jobs[]`, point 5 forbids duplicating server state, and the rubric (line 409)
scores point 5 while marking stored API responses as insufficient. Point 5 wins.

| State | Owner | Why |
|---|---|---|
| Job rows | **SWR**, keyed by the active filter and cursor, hydrated from the Server Component's props via `fallbackData` | Server-owned data with a cache, revalidation and request deduplication already solved |
| `filters`, `pagination`, `sortConfig`, `selectedJobIds` | **Zustand** | Genuine client state. Survives navigation, belongs to no server record |
| `optimisticStatus: Record<JobId, JobStatus>` | **Zustand** | An overlay, not a copy. Holds only in-flight intent |
| `rollbackSnapshot` | **Zustand** | The prior value of each optimistic entry, so a failure restores exactly what was there |

`filteredJobs` is a **selector**, never an effect. It reads SWR's rows, applies
the optimistic overlay on top, and marks selection. It is derived state in the
strict sense: no `useEffect`, no second copy, nothing to fall out of sync.

**Filtering and ordering happen server-side**, because both must agree with the
keyset cursor. `filters` and `sortConfig` are client state that participates in
the SWR key; the selector derives the view model from whatever the server
returned. Re-sorting the loaded page in the browser would order one page while
later pages followed a different order, which is why the selector does not
sort — see D-18.

Optimistic status change and rollback:

1. The hook writes the intended status into `optimisticStatus` and the previous
   one into `rollbackSnapshot`
2. The selector immediately renders the new status
3. The Server Action runs
4. On success the overlay entry is dropped and SWR revalidates, so the row
   returns to being server-owned
5. On failure the snapshot is restored and the error surfaces

### 5.6 Feature Sliced Design

The view structure is dictated by lines 114-133 and is followed exactly:
`presentation/views/jobs/` with `components/organisms/`, verb slices under
`features/`, an orchestrating `hooks/use-jobs-page.hook.ts`, and a barrel
`index.ts`.

**The assessment contradicts itself here, and the tree wins.** Line 13 names
Feature Sliced Design, whose layers are top-level and whose `features/` is one of
them; lines 114-133 nest `features/` inside the view, which canonical FSD does
not permit. Both cannot hold. What the rubric scores (line 407) is slice
anatomy — verb names, barrel exports, no cross-slice imports, thin organisms,
state in hooks — none of which depends on layer names, and all of which the
dictated tree carries. D-19 records the choice; 9.4 records what it costs.

Three rules give the structure its value, and all three are lint-enforced:

1. **A slice is a verb.** `create-job`, not `job-form`. The folder names an
   action a user takes
2. **Slices never import each other.** Anything shared moves up to the view or
   to `presentation/components`. Where two slices must react to each other, they
   do it through the typed event emitter (section 5.7), not an import
3. **Every import crosses a barrel.** Reaching into `features/create-job/hooks/…`
   from outside the slice is a violation; `features/create-job` is the address

**Organisms are thin shells.** An organism reads props and renders. It declares
no `useState`, no handler bodies, no data access. Everything lives in the slice's
hook. The practical test: deleting the hook should leave the organism with
nothing to do, and rendering the organism in a test should require no mocks
beyond its props.

### 5.7 Where the Part 1 utilities are used

They are integrated rather than parked in a folder, because the rubric (line 451)
rewards patterns that are genuinely applied.

| Utility | Real use |
|---|---|
| `JobState`, `transitionJob`, `getJobSummary` | `core/domain/job/`. The UI asks the state machine which actions a row offers, so an invalid action cannot be rendered |
| `createTypedEventEmitter` | The cross-slice bus. `complete-job` emits `job:status-changed`; the store and `filter-jobs` subscribe. This is what lets rule 2 of section 5.6 hold without slices importing each other |
| `PathKeys` | Types every field address in the create-job reducer, so `address.zip` fails to compile where `address.zipCode` is meant. Nested form state is exactly the case dot-notation paths exist for |
| `DeepReadonly` | Applied to the store's state type, so a selector consumer cannot mutate state it was handed |
| `QueryBuilder` | Builds the typed filter descriptor for `SearchJobs`, validating field names and value types at compile time. Its `.build()` SQL rendering is **not** sent over the wire — that would be an injection vector — it is asserted in tests as the canonical form of the equivalent server query, and it is the same shape `database/queries.sql` implements |

The `QueryBuilder` note is deliberate. The honest value of the builder here is
compile-time validation of filters; claiming the frontend ships SQL to the server
would be worse engineering, and a reviewer would notice.

---

## 6. Data architecture

### 6.1 Schema per module

| Schema | Owner | Contents |
|---|---|---|
| `jobs` | Jobs module | `jobs`, `job_photos`, `assignees`, `customers`, `notifications`, `outbox_messages` |
| `billing` | Billing module | `invoices` |
| `hangfire` | Infrastructure | Hangfire's own tables |

One database, one connection, separate schemas. A module's `DbContext` is
configured with its own default schema and maps only its own tables, so a query
that reaches across modules fails to compile rather than quietly joining.

This is the cheapest form of the boundary that still means something: it makes
the coupling visible and makes extraction to separate databases a migration
rather than a redesign.

### 6.2 Mapping rules

- **UUID primary keys**, generated in the domain, not by the database, so an
  aggregate is fully valid before it is persisted
- **`Address` as an owned type**, flattened to `street`, `city`, `state`,
  `zip_code`, `latitude`, `longitude` on `jobs`. It has no table because it has
  no identity
- **Enums as text with a `CHECK` constraint**. Readable in a psql session,
  diff-friendly in migrations, and safe against ordinal drift, which is what
  makes integer-backed enums dangerous across deployments
- **snake_case** everywhere, applied by an EF naming convention rather than
  per-property attributes
- **`created_at` / `updated_at`** on every aggregate table (`NFR-6`)
- `organization_id` on every tenant-scoped table, never nullable

### 6.3 Indexing strategy

| Index | Shape | Serves |
|---|---|---|
| `ix_jobs_tenant_keyset` | `(organization_id, coalesce(scheduled_date, '-infinity') DESC, id DESC)` | The default ordering. Serves the unfiltered list and any multi-status filter, because the sort keys are not preceded by a filtered column — see below |
| `ix_jobs_tenant_status_keyset` | `(organization_id, status, coalesce(scheduled_date, '-infinity') DESC, id DESC)` | A **single**-status filter, where the equality on `status` narrows the scan and the ordering still comes from the index |
| `ix_jobs_tenant_title_keyset` | `(organization_id, title, id)` | The second sortable field, with its own keyset support (D-18). `title` is `NOT NULL`, so it needs no coalesce |
| `ix_jobs_tenant_assignee` | `(organization_id, assignee_id)` | Filtering by assignee |
| `ix_jobs_search` | GIN over `to_tsvector('english', title \|\| ' ' \|\| coalesce(description, ''))` | Full-text search on title and description |
| `ix_job_photos_job` | `(job_id)` | Photo counts per job without a sequential scan |
| `ix_outbox_unprocessed` | Partial on `(occurred_on)` where `processed_on IS NULL` | The poller reads only the unprocessed tail, so index size tracks the backlog rather than total history |

The leading column of every tenant-scoped index is `organization_id`, so tenant
filtering is never a post-filter.

**Why there are two keyset indexes rather than one.** A single composite index
`(organization_id, status, scheduled_date DESC, id DESC)` cannot supply the sort
order when `status` is unconstrained or matched against several values: the scan
returns rows grouped by status, each group internally ordered, and PostgreSQL
has to sort the whole matched set to produce a global order. That is the cost
profile keyset exists to remove, and it would hit the **most common** query of
all — opening the list with no status filter.

`ix_jobs_tenant_keyset` removes the filtered column from in front of the sort
keys, so the index supplies the ordering directly and `status` becomes a cheap
filter on the rows it returns. The composite index earns its place only for a
single-status filter, where the equality is a scan boundary rather than a
post-filter. Which index the planner actually picks is a question for
`EXPLAIN (ANALYZE, BUFFERS)`, not for assertion; `database/queries.sql` carries
the plans.

Both keyset indexes order by `coalesce(scheduled_date, '-infinity')` rather than
by the column, for the reason in 6.4.

### 6.4 Cursor pagination

Paging is keyset. The cursor encodes the last row's ordering pair; the next page
asks for rows strictly beyond it. `id` is always the final component, so the
ordering is total and the cursor unambiguous.

**`scheduled_date` is nullable, and a naive keyset silently loses those rows.**
A `Draft` job carries no date. Two things then go wrong at once: `ORDER BY
scheduled_date DESC` puts NULLs *first* in PostgreSQL, and the row comparison
`(scheduled_date, id) < ($1, $2)` evaluates to NULL — not true — for every row
whose date is NULL, so `WHERE` discards it. The rows do not merely sort oddly;
after the first page they **disappear from the result entirely**.

The fix is to order by a total expression instead of a nullable column:

```sql
ORDER BY coalesce(scheduled_date, '-infinity'::date) DESC, id DESC
```

`-infinity` is a real date value, so the expression is never NULL, the
comparison is always defined, and dateless jobs sort last under `DESC` — which
is where a job with no date belongs on a schedule. The keyset predicate uses the
same expression, and so does the index, which is why 6.3 declares it over
`coalesce(...)` rather than over the bare column.

This costs nothing today, because D-14 means the interface never produces a
Draft. It is written this way because a keyset cursor over a nullable column is
a correctness bug waiting for the first row that has one.

The pair follows the **active sort field**, which is why the sortable set is
closed at two values rather than open (D-18): `(scheduled_date, id)` by default,
`(title, id)` when sorting by title. Each has a covering index, so neither
ordering degrades into a sort of the whole matched set.

**Why not `OFFSET`.** `OFFSET n` makes the database produce and discard `n` rows,
so cost grows linearly with page depth — page 500 is 500 times page 1 (`NFR-5`).
Worse, offsets are unstable under concurrent writes: an insertion before the
current position shifts every subsequent page, so rows are skipped or repeated.
Keyset reads an index range from a known position; cost is flat and the boundary
is a value, not a count.

**Why the cursor is not `ts_rank`.** Ordering by relevance is tempting and it
does work formally, with `id` as a tiebreaker. But a GIN index provides no
ordered access, so ranking forces Postgres to compute and sort the entire matched
set on every page — precisely the cost profile keyset exists to avoid. Full-text
search is therefore a **filter** (`@@ websearch_to_tsquery`) while the sort stays
on the indexed pair. The trade-off is explicit: search results come back
chronologically rather than by relevance.

### 6.5 Normalization position

The schema is normalized. `customer_id` and `assignee_id` are foreign keys into
read-only rosters this schema owns (D-26), not copied names, so a customer
renaming themselves updates one row rather than every job they appear on.

Denormalization becomes right when three conditions hold together: the read is
frequent and latency-sensitive, the joined data is owned by another bounded
context so the join would cross a module boundary, and the copied value tolerates
being briefly stale. A customer name on the job list satisfies all three, and the
correct way to place it there is an integration event from Contacts that updates
the copy — not a foreign key across schemas, which would recreate the coupling
the modules exist to avoid.

The full analysis lives in `docs/normalization.md` as the reviewer-facing
deliverable for lines 270 and 289-294. The position taken here is the one the
schema implements.

---

## 7. Cross-cutting concerns

### 7.1 Authentication

The assessment never specifies authentication. It appears once, as a
cross-cutting box in the diagram requirement (line 347): no provider, no flow,
no token format. But multi-tenancy is a hard requirement and has nowhere to
obtain `OrganizationId` from, so authentication has to be designed rather than
assumed.

The API issues and validates its own JWTs:

- HS256 with a symmetric key supplied by configuration, never committed
- Issuer and audience validated; lifetime validated
- Claims: `sub` (user identifier), `org` (organization identifier), `name`
- `POST /auth/dev-token` returns a token for a seeded user and organization.
  **It is only registered when the environment is Development**, so it cannot
  exist in a deployed environment

The frontend has no login screen, because office staff is the only role and user
administration is out of scope (`context/prd.md` section 9). The Next server
obtains a development token and attaches it as a bearer header from the HTTP
adapter, server-side only — the token never reaches the browser. A real
deployment replaces this single step with an identity provider; nothing else in
the design moves, which is the point of keeping token acquisition behind the
adapter.

### 7.2 Multi-tenancy

Isolation is enforced in four places, deliberately redundantly, because a single
forgotten `WHERE` clause is the entire failure mode (`NFR-1`):

1. **Middleware** resolves `ITenantContext` from the validated `org` claim. A
   request without one is rejected before reaching a handler
2. **EF global query filter** on every tenant-scoped entity compares
   `OrganizationId` against `ITenantContext`. A query written with no tenant
   condition still cannot return another organization's rows
3. **The aggregate** requires `organizationId` at creation and never exposes a
   setter, so a job cannot be moved between tenants (`BR-6`)
4. **An architecture test** asserts that every entity implementing
   `ITenantScoped` has a query filter configured, so adding a table cannot
   silently omit the protection

Layer 2 does the work; layers 1, 3 and 4 exist so that a mistake in layer 2 is
caught rather than exploited.

### 7.3 Error handling

Expected failures are values, not exceptions. Application code returns
`Result<T>` carrying an `Error` with a code, a message and a type. Exceptions are
reserved for genuine defects.

`Common.Presentation` maps error types to responses as `ProblemDetails`:

| Error type | Status |
|---|---|
| Validation | 400 |
| NotFound | 404 |
| Conflict — an invariant refused the operation | 409 |
| Unauthorized | 401 |
| Failure — unexpected | 500 |

The `HttpJobsAdapter` maps `ProblemDetails` back into core error types, so the
frontend's use cases see the same failure vocabulary regardless of adapter. On
the client, `app/jobs/error.tsx` provides retry via `reset()`, and a dedicated
`ErrorBoundary` wraps the job list (line 158) so a render failure in one table
does not blank the page.

### 7.4 Rate limiting

`Microsoft.AspNetCore.RateLimiting` with a **sliding window** limiter,
partitioned by the `org` claim so one tenant's traffic cannot exhaust another's
allowance, falling back to remote IP for unauthenticated requests. Rejections
return 429 with `Retry-After`. Bonus scope (+2), and .NET 9 ships the sliding
window algorithm the bonus names, so this is configuration rather than
implementation.

### 7.5 Observability

Structured logging through `ILogger` with a correlation identifier attached by
middleware and propagated into Hangfire jobs, so an asynchronous consequence can
be traced back to the request that caused it. The Hangfire dashboard is exposed
at `/hangfire`, which makes the whole async pipeline inspectable without reading
logs. Distributed tracing is out of scope by decision (D-15).

---

## 8. Testing strategy

| Layer | Tools | What is tested |
|---|---|---|
| Domain | xUnit, FluentAssertions | Every invariant in `context/prd.md` section 6; valid and invalid transitions; `Address` structural equality including inequality and hash consistency; that `JobPhoto` cannot be added except through the aggregate |
| Application | xUnit, Moq | Handler orchestration with mocked repository and unit of work; that completion raises `JobCompletedDomainEvent`; that failures return `Result` rather than throwing; validator rules |
| Billing | xUnit, Moq | `Invoice`'s own invariants; `GenerateInvoiceOnJobCompletedHandler` with a mocked repository; and **the same integration event delivered twice, asserting one invoice** — which is where the idempotency claim of 4.5 is actually proven rather than described |
| Integration | xUnit, Testcontainers.PostgreSql | Everything only a real database can answer — section 8.2 |
| Architecture | NetArchTest | Every rule in section 9; layer dependency direction; no module referencing another's internals; tenant query filter present on all tenant-scoped entities |
| Frontend behaviour | Jest, React Testing Library | `useCreateJob` reducer transitions and validation; the Server Action call; store selectors; optimistic update and rollback, with `act()` around every state change |
| Frontend types | `expect-type`, `tsc --noEmit` | `DeepReadonly` over nested objects, arrays, `Map`, `Set` and tuples; `PathKeys` output; `QueryBuilder` narrowing across the chain; that invalid `transitionJob` calls are compile errors |
| End-to-end | Playwright | The full acceptance walkthrough, Page Object Model, `data-testid` selectors, screenshots on failure |

Two operational details that decide whether these tests are real:

**Type tests need their own gate.** `expect-type` asserts at compile time, so a
broken type assertion does not fail Jest — the file simply does not type-check
while the test run stays green. `tsc --noEmit` is therefore a required CI step,
not a convenience. Negative assertions ("this must not compile") are written with
`@ts-expect-error`, which fails the build when the error it expects stops
occurring.

**Billing is tested, because D-04 is a claim that needs evidence.** That decision
gave Billing a domain of its own on the grounds that an `Invoice` with invariants
demonstrates a bounded context that is not anemic. A module whose invariants
nothing exercises demonstrates the opposite, so `Billing.Application.UnitTests`
exists to make the claim checkable.

**End-to-end runs against the in-memory adapter.** The suite needs no backend and
no database, so it is fast and deterministic in CI. A single smoke run exercises
the HTTP adapter against the full Compose stack, which is where integration is
actually proven.

### 8.1 Definition of done

The scope is cut until this holds, and no further:

> The in-memory suite passes in CI, **and** the smoke run completes all nine
> steps of the acceptance walkthrough (`context/prd.md` section 8) against the
> Compose stack.

Two of the nine steps cannot be proven without the backend, and they are the two
that matter most, because they are the asynchronous ones:

| Walkthrough step | In-memory suite | Smoke against Compose |
|---|---|---|
| 1. Open the job list | ✓ | ✓ |
| 2. Create a job through the form | ✓ | ✓ |
| 3. It appears as **Scheduled** | ✓ | ✓ |
| **4. The assignee is notified** (`FR-8`) | — | ✓ |
| 5. Narrow by status and find it | ✓ | ✓ |
| 6. Record that the crew started | ✓ | ✓ |
| 7. Complete with signature and photos | ✓ | ✓ |
| 8. It shows **Completed** | ✓ | ✓ |
| **9. An invoice exists and the customer was notified** (`FR-9`, `FR-10`) | — | ✓ |

Without the smoke run, nothing executable covers the outbox, the Hangfire drain,
Billing or the notification handlers — the whole asynchronous half of the system
would be proven only by unit tests of its parts.

**How the smoke run observes steps 4 and 9.** Neither consequence appears in the
interface: `context/design.md` A5 step 6 states that completion does not wait for
them and does not claim they happened. So the `data-testid` contract cannot help,
and the test queries Postgres directly — `billing.invoices` by `job_id`, and
`jobs.notifications` by `source_event_id` and status. An integration test reading
the database the system just wrote is the normal shape of an integration test,
not a leaked abstraction; the alternative, a diagnostics endpoint, would add
public surface that D-04 declined to give Billing.

Both assertions poll with a bounded timeout rather than sleeping, because
`NFR-4` promises the consequences arrive *within seconds*, not immediately — the
window is the poll interval, and asserting on it is asserting on eventual
consistency rather than pretending it is synchronous.

### 8.2 Integration tests

`JobTracker.IntegrationTests` starts a PostgreSQL container with
Testcontainers, applies the migrations, and exercises the infrastructure layer
against it. It exists for two reasons, and the second is the one that decided
it (D-27).

The first is that a whole category of production code has no other test. EF
configuration, a global query filter and a keyset predicate are not behaviour a
mock can report on; they are behaviour a database has.

The second is that **development is test-driven** (D-28), and the red-green loop
needs a test that runs in seconds. The smoke run of 8.1 does cover this
ground, but as a feedback loop it is unusable: no one brings up the whole
Compose stack to find out whether `Address` mapped to six columns. Without a
suite at this level, the entire infrastructure layer would be written blind and
verified at the end, which is the opposite of the discipline.

| # | What it proves | Why a unit test cannot |
|---|---|---|
| 1 | Migrations apply to an empty database | There is nothing to assert against otherwise |
| 2 | `Address` round-trips through six flattened columns | Owned-type mapping is EF configuration, not code a mock sees |
| 3 | `JobStatus` is stored as text and column names are snake_case | A convention is either applied by the provider or it is not |
| 4 | A query written with **no** tenant condition returns only the acting organization's rows | `NFR-1` in full. A mocked repository proves nothing about a global query filter |
| 5 | A job's state change and its outbox rows commit together, and a rollback leaves neither | `NFR-2`. A unit test shows the interceptor was called, not that the transaction is one |
| 6 | Successive keyset pages are disjoint and complete, **including a row whose `scheduled_date` is null** | This is the regression guard for the bug in 6.4: over a bare column the comparison yields NULL and the row silently disappears after page one |
| 7 | Full-text search matches title and description through `websearch_to_tsquery` | The GIN expression index and the query expression have to be the same expression, or neither works |
| 8 | The lateral photo count is right per row | |
| 9 | `uq_invoices_idempotency` and `uq_notifications_idempotency` actually reject a duplicate | The constraint is the whole of 4.5; a test that never inserts twice never checks it |

**These tests assert behaviour, never query plans.** It is tempting to assert
that the planner chose `ix_jobs_tenant_keyset`, and it would be a bad test: the
choice depends on table statistics and row counts, so it fails on a small
fixture for reasons that have nothing to do with a defect. The plans belong in
`database/queries.sql` as captured `EXPLAIN (ANALYZE, BUFFERS)` output — evidence
a reader can check — while the suite asserts that paging returns the right rows
in the right order.

This does not replace the smoke run. Section 8.1 proves the *nine steps* end to
end through a browser; 8.2 proves the *infrastructure* in isolation, fast enough
to drive a red-green cycle. They overlap on purpose: one is a feedback loop, the
other is the definition of done.

### 8.3 How development proceeds

Every line of production code is written to satisfy a test that was **watched
failing first** (D-28). A test written afterwards passes immediately, which
proves nothing about whether it can catch the defect it describes.

**Outside-in, one slice of the walkthrough at a time.** The acceptance suite is
fully specified before any code exists — `context/design.md` A8 fixes every
selector and `context/prd.md` section 8 fixes the nine steps — so the outer red
is available from the first day. It is not written all at once, because a suite
that stays red for days leaves CI red for days:

```
outer red:  steps 1-3   open the list, create, appears Scheduled
                ↓  inner unit cycles until green
outer red:  + step 5    filter by status
                ↓
outer red:  + steps 6-8 start, complete, shows Completed
                ↓
outer red:  + steps 4, 9   the asynchronous pair, against Compose
```

Every commit is green; the failing outer step is the list of what is missing.
Steps 4 and 9 come last because they need the backend, the outbox draining and
Billing writing — the critical path, and the slowest thing to turn green.

**Architecture tests get two mechanisms, because NetArchTest is unusually easy to
write so that it can never fail.** A wrong suffix, a case mismatch, or
`BeSealed()` where `BeSealed().And().BeNotPublic()` was meant, all yield an
assertion over an empty set — which passes.

1. **A non-emptiness guard on every rule, permanently.** Each rule asserts that
   the set it examines is non-empty before asserting anything about it. The rule
   cannot pass vacuously today against a solution with no types, nor in a year
   when a rename leaves it pointing at nothing
2. **A deliberate red, once per rule.** The first `CreateJobCommandHandler` is
   written `public`, the rule is watched failing, and then the modifier is
   corrected. Fifteen seconds, and it is the only thing that demonstrates the
   assertion is wired to what its name claims

**What is exempt, and the rule that decides it.** An artefact is outside the
cycle when either condition holds:

- **(a)** no test can exist before it does — a bootstrapping problem
- **(b)** its correctness already *is* the pass or fail of a CI job

| Artefact | Condition |
|---|---|
| `.sln`, `.csproj` | (a) — a test lives inside a `.csproj`; none can assert that projects exist |
| `docker-compose.yml` | (b) — the `smoke` job fails when it is wrong |
| Both `Dockerfile`s | (b) — the `images` job builds them |
| `.github/workflows/ci.yml` | (b) — it is tested by running |
| `jest.config` (`coverageThreshold`) | (b) — self-enforcing: coverage below the threshold fails the run |
| `tsconfig` (`strict: true`) | (b) — the `expect-type` assertions only hold under `strict`; turning it off fails the types job |

The rule is stated rather than the list, so a new artefact classifies itself.
Anything meeting neither condition gets a test, including one that looks like
configuration:

**The licence pins are not exempt.** Nothing fails today if MediatR moves to 13
or FluentAssertions to 8: it compiles, the tests pass, and the defect appears
when a reviewer runs `dotnet restore` without a licence and cannot build the
deliverable — which is the failure D-20 calls a gate rather than a preference.
Two assertions in `JobTracker.ArchitectureTests` close it:

```csharp
[Fact]
public void Mediatr_stays_below_the_commercially_licensed_major()
    => typeof(IMediator).Assembly.GetName().Version!.Major.Should().Be(12);

[Fact]
public void FluentAssertions_stays_below_the_commercially_licensed_major()
    => typeof(AssertionExtensions).Assembly.GetName().Version!.Major.Should().BeLessThan(8);
```

They assert on the **loaded assembly** rather than on the XML of
`Directory.Packages.props`, which checks what was actually restored and catches a
transitive bump the file would not mention. Section 9 says a convention that is
not enforced is a suggestion; this is what stops D-20 from being one.

---

## 9. Binding conventions

These are the rules development must follow. `CLAUDE.md` points here; the tests
named in section 8 enforce them. A convention that is not enforced is a
suggestion, so every rule below has a check behind it.

### 9.1 Backend naming and modifiers

| Element | Rule |
|---|---|
| Command | `public sealed`, suffix `Command` |
| Command handler | `internal sealed`, suffix `CommandHandler` |
| Query | `public sealed`, suffix `Query` |
| Query handler | `internal sealed`, suffix `QueryHandler` |
| Validator | `internal sealed`, suffix `Validator` |
| Domain event | `public sealed record`, suffix `DomainEvent`, in `Domain` |
| Integration event | `public sealed record`, suffix `IntegrationEvent`, in `IntegrationEvents`, primitive members only |
| Repository interface | `I{Aggregate}Repository`, in `Domain` |
| Repository implementation | `internal sealed partial`, in `Infrastructure` |
| EF configuration | `internal sealed`, implements `IEntityTypeConfiguration<T>` |

Handlers are `internal` because nothing outside the module may invoke them
directly; MediatR resolves them by contract. That is the modifier doing real
work rather than decorating.

### 9.2 Backend structural rules

- Aggregates expose **no public setters**. State changes go through
  intention-named methods
- Handlers return `Result` or `Result<T>`. No exception is thrown for an expected
  failure
- `Domain` references only `Common.Domain`
- `Application` never references `Infrastructure`
- A module references another module only through its `IntegrationEvents`
- Read queries use projections and no change tracking. This binds the
  repository's read side too: `SearchAsync` returns a projected read model, not
  aggregates (D-25)
- The repository is split with `partial` across files by responsibility —
  reads in one, writes in another. Line 222 asks for this; with three methods it
  is a convention rather than a necessity, and it is followed as specified

### 9.3 Frontend rules

| Rule | Check |
|---|---|
| File names are kebab-case with a role suffix: `*.component.tsx`, `*.hook.ts`, `*.store.ts`, `*.adapter.ts`, `*.action.ts`, `*.type.ts` | lint |
| Feature slices are named as verbs | review, listed in `context/design.md` |
| No slice imports another slice | lint (import boundaries) |
| Every import into a slice goes through its `index.ts` | lint |
| No slice imports the view's barrel, or anything above itself | lint (import boundaries) |
| The import graph is acyclic | lint (`import/no-cycle`) |
| The cross-slice bus is unidirectional: slices emit, the view and the store react | review |
| Organisms declare no `useState` and no handler bodies | review |
| Conditional rendering uses a ternary, never `&&` | lint |
| No `any`, no `as unknown as X` | lint, `tsc` |
| Server Actions are used only for mutations | review |
| Every element the end-to-end suite touches carries `data-testid` | `context/design.md` contract |

The prohibition on `&&` comes from line 159. It has a real justification worth
stating: `&&` renders `0` and `''` when the left operand is falsy but not
boolean, which is a recurring source of stray characters in tables of counts.

### 9.4 Why the import graph has no cycles

Feature Sliced Design guarantees acyclicity structurally: layers import strictly
downward, and slices within a layer never import each other. The tree the
assessment dictates (lines 114-133) nests `features/` inside the view rather than
placing it in a layer of its own, so that guarantee has to be reconstructed from
the rules in 9.3. Three of them do it, and they are worth reading together:

```
                    app/jobs/page.tsx
                            |
                            v
        views/jobs/components/organisms/jobs-client
                            |
                            v
              views/jobs/hooks/use-jobs-page
                            |
        +-------------------+-------------------+
        v                   v                   v
  features/create-job  features/filter-jobs  features/complete-job
        |                   |                   |
        +-------------------+-------------------+
                            v
        presentation/components/   presentation/stores/
                            |
                            v
                     core/     shared/
```

Every edge points downward. Nesting plus downward-only imports is in fact a
stricter constraint than FSD's layering, not a looser one.

The edge that would close a cycle is the one that is missing on purpose.
`use-complete-job` needs SWR to revalidate, but SWR lives in `use-jobs-page`,
which sits above it. Importing it would produce `slice -> view -> slice`. The
typed event emitter inverts that dependency: the slice emits `jobs:invalidate`,
the orchestrator subscribes, and both depend on `shared/events` instead of on
each other. That is the emitter's structural job, and it is why section 5.7
treats it as load-bearing rather than as a demonstration of a type utility.

**Where this guarantee is weaker than FSD's.** FSD stays acyclic no matter how
many pages exist, because `features/` is a top-level layer any page may import.
Here, acyclicity holds because there is exactly one view. A second view wanting
`create-job` would have to either duplicate the slice or import
`views/jobs/features/create-job`, and that second option is an import between
slices of the same layer — the exact edge FSD forbids because it is where cycles
begin. With one view the point is theoretical; it is recorded because it is the
real difference between the two structures, and D-19 chooses knowingly.

**Two runtime caveats the lint rule cannot see.** A barrel can manufacture a
false cycle that breaks tree-shaking and leaves a binding `undefined` at module
initialisation, which is why the barrel rule works in both directions: into a
slice through its `index.ts`, and never out of a slice into the view's. And the
emitter makes the *import* graph acyclic without making the *runtime* graph
acyclic — nothing in the type system stops `complete-job` from emitting an event
that `filter-jobs` answers with an event that feeds back. The bus is therefore
declared unidirectional: slices emit, the view and the store react, and no
subscriber emits in response to what it received.

---

## 10. Deployment

### 10.1 Compose stack

| Service | Image | Depends on | Notes |
|---|---|---|---|
| `postgres` | `postgres:17-alpine` | — | Named volume; healthcheck via `pg_isready` |
| `backend` | built, multi-stage | `postgres` healthy | Applies EF migrations at startup, then serves |
| `frontend` | built, `output: 'standalone'` | `backend` started | |

`depends_on` uses `condition: service_healthy` for Postgres rather than a plain
dependency, because a started container is not an accepting one and migrations
fail against a database still recovering.

A reviewer needs `docker compose up` and nothing else: no credentials, no
accounts, no manual migration step (`NFR-7`).

### 10.2 Continuous integration

| Job | Steps |
|---|---|
| `backend` | restore, build with warnings as errors, unit tests, architecture tests |
| `integration` | Testcontainers brings up PostgreSQL, applies migrations, runs `JobTracker.IntegrationTests`. Needs Docker, which the runner already provides for `smoke` |
| `frontend` | install, lint, `tsc --noEmit`, Jest with `coverageThreshold` enforced |
| `e2e` | build frontend with the in-memory adapter, run Playwright, upload failure screenshots |
| `smoke` | `docker compose up --wait`, run the smoke spec against the HTTP adapter, assert steps 4 and 9 against Postgres, tear down |
| `images` | build both Dockerfiles to prove the Compose stack still builds |

`tsc --noEmit` is a separate step from Jest for the reason given in section 8.

**Coverage is a gate, not a report.** The rubric asks for more than 80% branch
coverage, so `jest.config` sets `coverageThreshold.global` to `{ branches: 80,
functions: 80, lines: 80, statements: 80 }` and the run fails below it.
Collecting coverage and printing it changes nothing about whether the tests are
good; failing the build when it drops is the only version of the requirement
that has teeth.

---

## 11. Decision log

| # | Decision | Alternative rejected | Rationale | Cost accepted |
|---|---|---|---|---|
| **D-01** | `JobsPort` with an HTTP adapter and an in-memory adapter | A single HTTP adapter | Playwright and hook tests run with no backend or database, so the two halves of the project fail independently rather than together | A second implementation to keep in step with the port |
| **D-02** | The API issues and validates its own JWT; tenant from the `org` claim | A documented stub; NextAuth with bearer tokens | Real enforcement of `NFR-1` without an external identity provider or a login flow the scope does not include | Token acquisition in development is a seeded endpoint, not a real sign-in |
| **D-03** | Hangfire polling the outbox; no message broker | RabbitMQ | Both modules share a process and a database, so a broker adds a container, a consumer topology and reconnection handling for no behavioural gain. Line 345 offers the choice; the rubric names only Hangfire | Throughput is bounded by the poll interval. A broker becomes right when modules become separate deployables, and only the poller changes |
| **D-04** | Billing has a domain and an application layer | A single handler and a table; a full four-layer module with endpoints | An `Invoice` aggregate with invariants demonstrates a bounded context that is not anemic. No rubric criterion asks for Billing endpoints | More surface than the minimum |
| **D-05** | SWR owns job rows; Zustand owns UI state and an optimistic overlay | `jobs[]` in the store, hydrated from props | Satisfies point 5 of section 2.2 and line 409, which is the criterion that scores. Derived state comes from a selector with no second copy to desynchronise | Deviates from the letter of point 1 of section 2.2 |
| **D-06** | Part 1 utilities are integrated into the application | Kept in `lib/` with tests only | Line 451 rewards patterns that are genuinely applied. The typed emitter in particular is what lets slices stay mutually unaware | Integration constrains their shape to real use |
| **D-07** | Jest with `expect-type`, plus `tsc --noEmit` in CI | Vitest for everything | Honours the runner named in the heading of section 5.1 | An extra dependency, more configuration, and a separate CI step because type assertions do not fail Jest |
| **D-08** | Delivery is simulated: `LoggingNotificationSender` writes a line and the row moves to `Sent` | A real SendGrid account; MailHog as an inspectable SMTP sink | SendGrid appears once in the assessment (line 241) and nowhere in the rubric; what is graded is the pipeline, which is exercised identically whichever way the last hop goes. Dropping MailHog removes a container from Compose, and `jobs.notifications` is better evidence than an inbox because a test can assert on it | Real deliverability is not demonstrated, and the reviewer reads a table rather than seeing an email arrive |
| **D-09** | MediatR pinned to 12.x | Latest MediatR; a hand-rolled dispatcher | v13 and later require a commercial licence, which would make the deliverable unbuildable for a reviewer | Pinned to a version that will age |
| **D-10** | `transitionJob` takes a generic parameter constrained to the current state | The signature as written in line 81 | The literal signature accepts the whole union, so no call can ever be a compile error — it cannot satisfy line 87 | The caller must narrow the state before calling, which is correct but is a different signature |
| **D-11** | The page passes an unresolved promise; a child unwraps it with `use()` | `await` in `page.tsx` | With `await`, data is resolved before render and the Suspense boundary never suspends, so the skeleton line 104 asks for would never appear | A less obvious data flow that needs the comment it has |
| **D-12** | Keyset cursor on `(scheduled_date DESC, id DESC)`; full-text search as a filter | Cursor over `ts_rank` | GIN offers no ordered access, so ranking sorts the whole matched set per page — the cost profile keyset exists to remove | Results are chronological, not relevance-ordered |
| **D-13** | The outbox stores domain events; a handler translates to an integration event downstream | Storing the integration event, as lines 236-238 describe | The interceptor runs during `SaveChanges`, before any handler produces an integration event. Storing the domain event is what preserves the transactional guarantee | Deviates from the literal wording; the contract type still lives in `IntegrationEvents` as line 237 requires |
| **D-14** | Creating a job through the interface produces a **Scheduled** job | Creating a Draft | The creation form collects exactly what Scheduled requires (line 170), and the acceptance walkthrough cannot complete a job that was never scheduled | Draft remains in the model but unreachable from the interface |
| **D-15** | Bonus scope is CI/CD (+3) and rate limiting (+2) | All five bonuses | Both are close to free: Compose is already a delivery requirement, and .NET 9 ships the sliding window limiter | Accessibility (+1) and OpenTelemetry (+2) are forgone |
| **D-16** | Tailwind CSS v4 | CSS Modules | Atoms carry no stylesheet of their own and there is no runtime cost | Markup carries utility classes |
| **D-17** | SWR rather than React Query | React Query | Optimistic behaviour lives in the store by D-05, so the fetcher only needs caching, deduplication and revalidation | Fewer built-in mutation primitives, which D-05 does not need |
| **D-18** | Sorting is server-side and closed to `scheduledDate` and `title` | Free client-side sorting over any column | The keyset cursor must order by the same key the query does. Sorting the loaded page in the browser would order one page differently from the next, which is a correctness bug rather than a limitation. Closing the set means every ordering has a covering index | Only two sortable columns. A third needs an index, which is the honest cost of correct paging |
| **D-19** | The dictated tree wins over canonical Feature Sliced Design | The canonical layers (`app`, `pages`/`views`, `widgets`, `features`, `entities`, `shared`) with `features/` hoisted to the top level | Line 13 names FSD, but lines 114-133 dictate a path — `presentation/views/jobs/` — with `features/` nested inside the view, which canonical FSD does not permit. The two cannot both hold. The rubric (line 407) scores slice *anatomy*, not layer names: verb names, barrel exports, no cross-slice imports, thin organisms, state in hooks. All five hold in the dictated tree, and it is the tree the reviewer wrote | No `entities` or `widgets` layer, and acyclicity is reconstructed from the rules in 9.3 rather than guaranteed by layering — see 9.4. A second view would have to duplicate a slice or reach into this one |
| **D-20** | Every dependency is checked for a licence that a reviewer cannot satisfy, and pinned below it: MediatR `12.x`, FluentAssertions `7.x` | Taking the latest of each | FluentAssertions 8 (Xceed) and MediatR 13 both moved to commercial licences. An unbuildable deliverable scores nothing regardless of its contents, so this is a gate, not a preference | Two dependencies pinned to versions that will age. `Directory.Packages.props` holds both pins in one place so the reason is visible |
| **D-21** | *Reversed by D-26.* Originally: no foreign key on `assignee_id` or `customer_id`, on the grounds that both identifiers belong to modules outside this system's scope | — | The premise stopped holding. The interface needs an assignee picker, a customer picker and an assignee filter (`context/design.md` A8), so the system has to hold that roster whether or not it wants to. Once `jobs` owns both ends, the foreign key crosses no boundary | — |
| **D-22** | Notifications are a record inside Jobs: `jobs.notifications`, `INotificationSender`, and an adapter that writes to the log | A third `Notifications` module with its own four layers | Notifying has no invariants and no lifecycle of its own, so the module would be anemic — and D-04 gave Billing a module precisely *because* `Invoice` has both. A separate module would also force `FR-8` across a boundary and into a `JobAssignedIntegrationEvent`: a public contract the domain does not ask for, created by the shape of the code rather than by the business | Notification is expressed in the language of Jobs. If it ever grows templates, channels or preferences, extracting it is a new module rather than a refactor |
| **D-23** | One idempotency mechanism: every consumer earns it with a unique constraint on the data it writes. `outbox_message_consumers` is dropped | The generic consumer table — shared, per-module, or in a common schema | Line 246 names exactly one key, `JobId + CompletedAt`, and that is `uq_invoices_idempotency`. The generic table appears nowhere in the assessment, protects nothing the constraints do not, keys on a handler name that a rename invalidates, and forced Billing to write into the `jobs` schema against 6.1 | A future consumer with no natural business key would be unprotected. That day the table returns; today no such consumer exists |
| **D-24** | Billing publishes no contract; there is no `Billing.IntegrationEvents` project | Keeping it with an `InvoiceRaisedIntegrationEvent` and no consumer; inventing a consumer in Jobs | Nothing needs to learn that an invoice was raised — `context/prd.md` section 9 excludes collection, tax and documents, and A5 step 6 says the interface does not claim it happened. The 3.2 diagram drew an arrow with no type behind it, and one false arrow costs the credibility of the three that are correct | The flow is one-way. Billing reads as a consumer, which is what it is |
| **D-25** | `IJobRepository.SearchAsync` returns `IReadOnlyList<JobSearchResult>`, a projection declared in `Jobs.Domain`; the handler builds the `PagedList<JobResponse>` envelope | Returning `Job` aggregates; or letting the query handler reach the `DbContext` directly | Lines 220 and 208 contradict each other — one puts `SearchAsync` in the domain, the other demands projections without tracking. Aggregates fail 208; bypassing the repository lands on the rubric's *Insufficient* descriptor for Repository + UoW ("Direct DbContext usage in handlers", line 419) and leaves a dead method in the interface the assessment asked for | The domain declares a read model, which strict CQRS would place in the application layer (3.6). `JobSearchCriteria` is modelled as a Specification, where such a type does belong |
| **D-26** | `jobs.assignees` and `jobs.customers` are read-only rosters in the `jobs` schema, seeded by migration, with real foreign keys from `jobs.jobs` and two `GET` endpoints for the pickers | Keeping a hardcoded map in `Jobs.Application` mirrored in `InMemoryJobsAdapter`; denormalising the names onto `jobs.jobs` | Three controls in A8 need a roster, and under the definition of done in 8.1 the smoke run creates a job through the form against the real backend — two hardcoded maps can disagree and fail at step 2 rather than where the cause is. `context/prd.md` §9 excludes crew and customer *administration*, not their existence; these tables have no write path. `docs/normalization.md` calls a local replica the correct home for such a name, and `customer_name` still is not a column on `jobs.jobs` | Two tables and a seed the assessment never asked for, and D-21 reversed. In exchange, lines 261-262 are satisfied literally and the rubric's "correct FK relationships" stops being forfeited |
| **D-27** | `JobTracker.IntegrationTests` runs the infrastructure layer against a real PostgreSQL started by Testcontainers | Relying on the 8.1 smoke run as the only integration coverage | EF configuration, the tenant query filter, the outbox transaction and the keyset predicate are behaviour a database has and a mock cannot report. The keyset case is a proven risk, not a hypothetical: the null-date bug in 6.4 was found by reading and nothing would have caught its return. And under D-28 the red-green loop needs a test that runs in seconds, which `docker compose up` is not | A test project and a Docker dependency in CI, both of which the `smoke` job already required. Runs slower than the unit suites, so it is a separate CI job |
| **D-28** | All development is test-driven: no production code without a test that was watched failing first | Writing tests after each unit of work | Tests written afterwards pass immediately, which proves nothing about whether they can catch the defect they describe. It also produces the incremental commit history line 495 asks for, rather than a history reconstructed to look incremental | Scaffolding and configuration are exempt under the two conditions in 8.3 — but the licence pins of D-20 are not, and get assertions of their own. Type-level assertions verify red through `tsc --noEmit` rather than the test runner |
| **D-29** | Reads reach the .NET API through a Next Route Handler at `app/api/jobs/route.ts`; SWR fetches that | Fetching the API directly from the browser; or a Server Action | 5.5 has SWR refetch when a filter changes but never says against what. Assessment line 138 bars Server Actions from reads, and 7.1 keeps the bearer token off the browser, so the client cannot call .NET directly either. The Route Handler is the only remaining place, and it is where the token lives from plan 3C on | A hop the architecture implied and never wrote down; recorded here after the fact, having been decided while writing plan 2A |
| **D-30** | The tenant query filter is declared in `JobsDbContext.OnModelCreating`, closing over the context, rather than in each `IEntityTypeConfiguration` | Declaring it alongside the rest of each entity's mapping, where a reader would look for it | `IEntityTypeConfiguration` is static and has no context to close over. A filter written there captures nothing and silently compares every row against `Guid.Empty`, which returns an empty list rather than an error — the failure mode is a tenant seeing nothing, and the next one along is a tenant seeing everything | The filter is not where the rest of an entity's mapping lives, so two architecture tests enforce it instead of proximity: every `ITenantScoped` entity has a filter, and every entity with an `organization_id` declares itself scoped |
| **D-31** | The `coalesce` sentinel in the search ordering is emitted as a SQL literal through `EF.Constant(DateOnly.MinValue)`, not as a captured value | Leaving EF to parameterise it, as it does by default | EF emitted `coalesce(scheduled_date, $1)`, and PostgreSQL cannot match a bind parameter against the expression an index is declared over. `ix_jobs_tenant_keyset` would have existed and never been used, with nothing to say so — the query stays correct, only slow, which is the failure mode that survives a test suite. With `EF.Constant` the SQL reads `coalesce(scheduled_date, DATE '-infinity')`, character-for-character the indexed expression | Found by reading the generated SQL rather than by a failing test, so a test now compares the stored index definition against what the repository emits. Npgsql's infinity mapping is what turns `DateOnly.MinValue` into `'-infinity'` |
| **D-32** | BR-1 answers **400** on both `POST /api/jobs` and `PATCH /api/jobs/{id}/schedule` | 409 on the PATCH, as design B6's row says | B6 gives one rule two status codes. BR-1 refuses a *value*: the same date is refused whether the job is new or being corrected, and the caller fixes it the same way. 409 is reserved for refusals where nothing the caller sent was wrong — a terminal job, a job that never started | Design B6's reschedule row is superseded and annotated as such. Found by a test written from B6 that then failed against B6's other row |
| **D-33** | `JobCompletedIntegrationEvent` carries the labour window (`StartedAt`, `CompletedAt`) and Billing owns the rate | Adding a price to `Job`; or Billing charging a flat fee | Design B5 names an "amount basis" that does not exist — a `Job` has no price and nothing in the PRD gives it one. A price on `Job` is scope nobody asked for; a flat fee makes D-04's "real domain rather than a stub" a stub with extra steps. Sending what Jobs knows and letting Billing price it is the only option where the boundary means something: Jobs says what happened, Billing decides what it is worth | `JobCompletedDomainEvent` gained a field, which an internal event may do freely (4.1). Billing carries a rate and a call-out minimum that a real deployment would configure |
| **D-34** | A domain event carries its own identity, generated when it is raised, and the outbox row adopts it as its primary key | Passing the outbox row's identifier to handlers through ambient scoped state | 4.5 says `source_event_id` is the row's identifier, and a handler receives a deserialised event rather than the row. Making the two the same value keeps 4.5 literally true, leaves the system with no hidden context, and makes it impossible to enqueue one event twice | `IDomainEvent` gained two members and a base record. `OccurredOn` reads a clock, which the time-as-a-parameter rule otherwise forbids — it is an audit stamp no rule reads, and threading `now` into every initialiser would buy no test anything |
| **D-35** | Every domain event carries its `OrganizationId`, and the outbox drain sets the tenant from it before publishing | Letting background handlers query with the filter lifted; or a tenant resolved from configuration | The drain runs with no request, no principal and no claim, so every tenant-scoped query a handler makes has nothing to filter by — not subtly, but as an exception on the first read. Found by the first end-to-end run. Lifting the filter for background work would make NFR-1 hold only for HTTP, which is where it is least needed | `ITenantContextSetter` exists alongside `ITenantContext`: reading a tenant is something every layer does, setting one is something exactly two places may do. A handler that could set its own tenant could read another's data |
| **D-36** | Background work carries its tenant explicitly, and queued work is dispatched only after the transaction that produced it commits | Enqueueing on the spot and letting Hangfire's retry absorb the race | Found by the Compose stack, not by a test. Both notifications sat at `Pending` while every Hangfire job reported success. Three faults compounded: the send job ran with no claim so its query threw (the same shape as D-35, in the path D-35 did not cover); the job was queued inside the outbox transaction, so the worker read a row no other connection could see yet; and `MediatorJobRunner` discarded the handler's `Result`, so a failure was reported as success — no retry, no log, nothing anywhere saying why | `IBackgroundQueue` gained `Flush`, and only the owner of a transaction calls it. Every earlier test ran the send inline on a context whose tenant was already set, so none of them could have caught any of this — which is the argument for the smoke run existing at all |

---

## 12. Traceability matrix

| Architectural element | Assessment source | Rubric criterion | Points |
|---|---|---|---|
| Module anatomy and four layers | lines 13, 376 | 3 — Clean Architecture layers | 3 |
| `IntegrationEvents` as public contract | lines 237, 358 | 3 — Clean Architecture layers, 6 — DDD concepts | 3 + 3 |
| `Job` aggregate, invariants, domain events | lines 169-175 | 3 — Aggregate design | 7 |
| `Address` owned value object | lines 177-180 | 3 — Aggregate design, 4 — Schema design | 7 + 4 |
| `JobPhoto` reachable only via the root | lines 182-185 | 3 — Aggregate design | 7 |
| Command and query separation, and its limits (section 3.6) | lines 191-215 | 3 — CQRS and MediatR | 6 |
| CQRS naming and modifiers (section 9.1) | lines 210-215 | 3 — CQRS and MediatR | 6 |
| `Result` pattern, FluentValidation | lines 196-203 | 3 — CQRS and MediatR | 6 |
| Repository interface plus implementation, partial split | lines 219-222 | 3 — Repository and UoW | 5 |
| Unit of work dispatching through the outbox | lines 224-226 | 3 — Repository and UoW | 5 |
| Outbox interceptor in the same transaction | line 238 | 3 — Outbox and Hangfire | 4 |
| Hangfire polling and dispatch | lines 239-241 | 3 — Outbox and Hangfire | 4 |
| Domain versus integration events (section 4.1) | line 244 | 6 — DDD concepts | 3 |
| At-least-once rationale (section 4.3) | line 245 | 6 — DDD concepts | 3 |
| Idempotency by constraint, proven by a double-delivery test (4.5, 8) | line 246 | 3 — Outbox, 6 — SOLID and GRASP | 4 + 5 |
| Schema per module | lines 229, 346 | 4 — Schema design | 4 |
| Enums as text, snake_case, owned type | lines 230-232 | 4 — Schema design | 4 |
| Indexing strategy (section 6.3) | lines 265-269 | 4 — Indexing and optimization | 3 |
| Keyset cursor and the `OFFSET` argument | lines 283, 286 | 4 — Indexing and optimization | 3 |
| Normalization position (section 6.5, `docs/normalization.md`) | lines 270, 289-294 | 4 — Normalization analysis | 3 |
| `server-only`, Server Component, props | lines 100-103 | 2 — Server/client boundary | 5 |
| Suspense with a real skeleton | line 104 | 2 — Error, loading, Suspense | 5 |
| FSD slice rules (section 5.6) | lines 112-136 | 2 — FSD structure | 5 |
| Server Actions for mutations only | line 138 | 2 — Server/client boundary | 5 |
| State ownership and selectors (section 5.5) | lines 142-148 | 2 — State management | 5 |
| Optimistic update with rollback | line 147 | 2 — State management | 5 |
| Part 1 utilities in real use (section 5.7) | lines 24-63 | 1 — TypeScript mastery, 6 — GoF patterns | 15 + 4 |
| Authentication and tenancy (sections 7.1, 7.2) | line 347 | 6 — Architecture diagram | 3 |
| Error handling (section 7.3) | lines 108, 158, 347 | 2 — Error, loading, Suspense | 5 |
| Rate limiting (section 7.4) | line 481 | Bonus | +2 |
| Architecture diagram, SOLID, GRASP, GoF, DDD concepts (`docs/design-principles.md`) | lines 337-369 | 6 — all four criteria | 15 |
| Testing strategy (section 8) | lines 300-333 | 5 — all three criteria | 15 |
| Architecture tests enforcing section 9 | lines 316, 421 | 3 — Clean Architecture, 5 — Backend tests | 3 + 5 |
| Integration tests over a real PostgreSQL (section 8.2) | lines 208, 229-232, 283 | 3 — Repository and UoW, 4 — Indexing | 5 + 3 |
| Test-driven development, outside-in (section 8.3) | lines 300-333, 495 | 5 — all three criteria | 15 |
| Compose stack and CI (section 10) | lines 479, 490 | Bonus, and the build gate | +3 |
