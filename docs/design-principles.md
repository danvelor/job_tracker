# JobTracker — Design principles, patterns and system diagram

| | |
|---|---|
| **Document** | The reviewer-facing answer to part 6 of the assessment |
| **Companions** | `context/architecture.md` (structure and binding rules), `context/design.md` (concrete artefacts), `context/prd.md` (what the product does) |
| **Source** | `context/request/technical-assessment-fullstack-senior 3 1.md`, lines 337-369 |

Every example below names a type and a file from this solution. Where a
principle is satisfied by something the reader can check — an architecture test,
a compiler error, a test suite that runs against two implementations — the check
is named, because a principle that cannot be violated by accident is worth more
than one that is merely described.

---

## 1. System architecture

```
┌──────────────────────────────────────────────────────────────────────────────┐
│ BROWSER                                                                      │
│                                                                              │
│  presentation/views/jobs/                                                    │
│    components/organisms/jobs-client  'use client', thin shell                │
│         │                                                                    │
│         ├── hooks/use-jobs-page ──── SWR ────────┐  server state: job rows   │
│         │        │                               │  keyed by filter+cursor,  │
│         │        │                               │  seeded via fallbackData  │
│         │        ├── features/create-job         │                           │
│         │        ├── features/filter-jobs        │                           │
│         │        ├── features/complete-job       │                           │
│         │        ├── features/start-job          │                           │
│         │        └── features/cancel-job         │                           │
│         │              │  (slices never import each other;                   │
│         │              │   they meet at shared/events, §4 Indirection)       │
│         │              ▼                                                     │
│         └── presentation/stores/jobs-ui.store  Zustand: filters, sort,       │
│                    cursor, selection, optimisticStatus, rollbackSnapshot     │
└───────────────────────────────────┬──────────────────────────────────────────┘
                                    │  Server Actions (mutations only)
┌───────────────────────────────────┴──────────────────────────────────────────┐
│ NEXT.JS SERVER                                                               │
│  app/jobs/page.tsx   'server-only'.  container.searchJobs.execute(filters)   │
│       │              no await — the promise crosses into <Suspense>          │
│  core/application/use-cases  ──▶  core/application/ports/JobsPort            │
│                                        ▲                    ▲                │
│                        HttpJobsAdapter │                    │ InMemoryJobs-  │
│                        (bearer token,  │                    │ Adapter        │
│                         ProblemDetails │                    │ (Playwright,   │
│                         → CoreError)   │                    │  hook tests)   │
└────────────────────────────────────────┼─────────────────────────────────────┘
                                         │  HTTPS + JWT (org claim)
┌────────────────────────────────────────┴─────────────────────────────────────┐
│ .NET 9 MODULAR MONOLITH — JobTracker.Api (composition root)                  │
│                                                                              │
│  CROSS-CUTTING ─ JWT auth ─ ITenantContext ─ rate limit ─ ProblemDetails ─    │
│                  correlation id ─ MediatR pipeline (validation, logging)     │
│                                                                              │
│   ┌── Jobs module ──────────────────┐   ┌── Billing module ───────────────┐  │
│   │ Presentation   endpoints        │   │ (no endpoints — D-04)           │  │
│   │      ▼                          │   │                                 │  │
│   │ Application    commands,        │   │ Application  GenerateInvoiceOn- │  │
│   │                queries,         │   │              JobCompletedHandler│  │
│   │                validators       │   │      ▼                          │  │
│   │      ▼                          │   │ Domain       Invoice aggregate  │  │
│   │ Domain         Job aggregate    │   │      ▼                          │  │
│   │                Address VO       │   │ Infrastructure                  │  │
│   │                JobPhoto         │   └─────────────▲───────────────────┘  │
│   │      ▼                          │                 │ compiles only        │
│   │ Infrastructure DbContext, repo  │                 │ against ───┐         │
│   │                notifications    │                 │            │         │
│   └───────────┬─────────────────────┘                 │            │         │
│               │                                                    │         │
│        Jobs.IntegrationEvents  ── Open Host Service ───────────────┘         │
│               (public contract: primitives only, no dependencies)            │
│                                                                              │
│  ASYNC PIPELINE                                                              │
│    Job.Create() / Job.Complete() raise their domain events                   │
│         ▼                                                                    │
│    InsertOutboxMessagesInterceptor ─── same transaction ──▶ outbox_messages  │
│         ▼  Hangfire recurring job, 10s, FOR UPDATE SKIP LOCKED               │
│    OutboxProcessor ──▶ MediatR publish                                       │
│         ├─▶ NotifyAssigneeOnJobCreated       (FR-8, never leaves Jobs)       │
│         ├─▶ JobCompletedDomainEventHandler ─▶ JobCompletedIntegrationEvent   │
│         │        ├─▶ Billing: generate invoice          (FR-9)               │
│         │        └─▶ NotifyCustomerOnJobCompleted       (FR-10)              │
│         └─▶ processed_on stamped                                             │
│                             │ Hangfire fire-and-forget, backoff              │
│                             ▼                                                │
│           INotificationSender ──▶ log line ──▶ row moves to Sent             │
└──────────────────────────────────┬───────────────────────────────────────────┘
                                   │
┌──────────────────────────────────┴───────────────────────────────────────────┐
│ POSTGRESQL 17 — one database, one connection, schema per module              │
│                                                                              │
│  schema jobs        jobs, job_photos, assignees, customers,                  │
│                     notifications, outbox_messages                           │
│                     (notifications unique on source_event_id + recipient)    │
│  schema billing     invoices  (unique on job_id + job_completed_at)          │
│  schema hangfire    Hangfire's own tables                                    │
│                                                                              │
│  Tenant isolation: organization_id leads every index, and an EF global       │
│  query filter compares it to ITenantContext on every tenant-scoped entity    │
└──────────────────────────────────────────────────────────────────────────────┘
```

Four things the diagram is meant to make visible. The **`<Suspense>` boundary is
real** because the promise crosses it unresolved (D-11). The **outbox row and the
job's state change share one transaction**, which is the whole of `NFR-2`.
**Billing's only inbound arrow comes from `Jobs.IntegrationEvents`**, never from
`Jobs.Domain`, and it is one-way: nothing needs to learn that an invoice was
raised, so Billing publishes no contract of its own. And **`organization_id` is enforced below the query**, not by the
caller remembering it.

---

## 2. SOLID

### S — Single responsibility

Completing a job has four consequences, and each is a separate type:

| Type | Its one job |
|---|---|
| `Job.Complete(...)` | Decide whether completion is legal and record it |
| `InsertOutboxMessagesInterceptor` | Move raised domain events into `outbox_messages` during `SaveChanges` |
| `OutboxProcessor` | Read unprocessed rows, publish, stamp `processed_on` |
| `JobCompletedDomainEventHandler` | Translate the domain event into the published contract |

None of them knows what the next does. The test that this is real: the outbox
can be drained by a message broker instead of Hangfire by replacing
`OutboxProcessor` alone — nothing in `Domain`, `Application` or the contracts
moves (`context/architecture.md` D-03).

The frontend counterpart is `useCreateJob`: the reducer is a pure state
transition, validation is a pure function from values to errors, and the Server
Action performs the I/O. The reducer and the validator are testable with no
React in the process.

### O — Open for extension, closed for modification

`IPipelineBehavior<TRequest, TResponse>` in `Common.Application`. Validation and
logging apply to every handler in both modules, and **not one handler mentions
either**. Adding an authorisation behaviour is a registration, not an edit:

```csharp
// Common.Application — the behaviour
internal sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>

// JobTracker.Api — the extension point
services.AddMediatR(cfg => cfg.AddOpenBehavior(typeof(ValidationBehavior<,>)));
```

`CreateJobCommandHandler` was written before the behaviour existed and did not
change when it arrived.

### L — Liskov substitution

`JobsPort` has two implementations, and the substitution is **proven by
execution rather than asserted**: the entire Playwright acceptance suite runs
against `InMemoryJobsAdapter`, and a smoke run of the same flow runs against
`HttpJobsAdapter` over the Compose stack (`context/architecture.md` 5.4, 8).

What makes this LSP rather than mere interface conformance is that the
substitutability is behavioural, not structural. Both adapters must return the
same `CoreError.kind` for the same failure — `'conflict'` when an invariant
refuses a transition, `'not-found'` for an unknown id — so a caller written
against one cannot be surprised by the other. The in-memory adapter therefore
reproduces the backend's refusals; it is not a happy-path stub.

### I — Interface segregation

`IJobRepository` declares exactly three members:

```csharp
public interface IJobRepository          // Jobs.Domain
{
    Task<Job?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(Job job, CancellationToken ct);
    Task<IReadOnlyList<Job>> SearchAsync(JobSearchCriteria criteria, CancellationToken ct);
}
```

There is deliberately **no generic `IRepository<T>`** with `Update`, `Delete`,
`Count` and `GetAll`. A `Job` is never deleted — `BR-2` says a closed job is
corrected by a new job — so a `Delete` a caller can see is a `Delete` someone
eventually calls. `ITenantScoped` is a single-member interface for the same
reason: an entity declares that it is tenant-scoped and nothing else.

### D — Dependency inversion

The abstraction is owned by the layer that needs it, not by the layer that
implements it. `IJobRepository` lives in `Jobs.Domain`; `JobRepository` lives in
`Jobs.Infrastructure`; `Jobs.Application` references the first and **cannot
reference the second** — `JobTracker.ArchitectureTests` fails the build if it
ever does.

The frontend mirrors it exactly: `core/application/ports/jobs.port.ts` declares
the port, `infrastructure/adapters/` implements it, `core/di/container.ts`
selects one from configuration, and `core/` imports nothing from React, Next or
any adapter. That import rule is what lets one use case serve a Server
Component, a Server Action and a unit test unchanged.

---

## 3. GRASP

### Information Expert

`Job.Complete(completedAt, signatureUrl, photos)` decides whether completion is
legal, because `Job` is the only holder of `Status`, `StartedAt` and the photo
collection. `CompleteJobCommandHandler` does not read the status and branch on
it; it loads, calls, and maps the `Result`:

```csharp
Result result = job.Complete(timeProvider.GetUtcNow(), command.SignatureUrl, command.Photos);
if (result.IsFailure) return result;
await unitOfWork.SaveChangesAsync(ct);
```

The anemic alternative — `if (job.Status != JobStatus.InProgress) return Error...`
in the handler — would place the rule where the data is not, and would need
repeating in every caller.

### Creator

`Job` creates `JobPhoto`, because `Job` aggregates photos and holds the data
they need. This is enforced rather than encouraged: `JobPhoto`'s constructor is
`internal`, so nothing outside the module can build one, and `Job` exposes no
`AddPhoto` — photos arrive only through `Complete`, which is the only moment the
business produces them.

`Job.Create(...)` is likewise the only way a `Job` comes into existence, and it
returns `Result<Job>`, so a job that violates `BR-1` is never constructed rather
than being constructed and then rejected.

### Controller

Two thin controllers, one per side.

On the backend, the endpoints in `Jobs.Presentation` bind the request, send one
MediatR message, and map `Result` to `ProblemDetails`. They hold no rule; a
transition refused by an invariant becomes `409` there and nowhere else.

On the frontend, `use-jobs-page.hook.ts` is the controller for the view. It
unwraps the promise with `use()`, seeds SWR, composes the five slice hooks and
returns what `JobsClient` renders. Its own state is nothing — it wires.

### Low coupling

`Billing` reacts to a completed job and **cannot name a job**. It compiles
against `Jobs.IntegrationEvents`, a project that itself references nothing, and
receives a record of primitives:

```csharp
public sealed record JobCompletedIntegrationEvent(
    Guid JobId, Guid CustomerId, Guid OrganizationId,
    DateTimeOffset CompletedAt, decimal AmountBasis);
```

An architecture test asserts that no `Billing.*` type references any `Jobs.*`
type outside `Jobs.IntegrationEvents`. The coupling that remains is a contract
of five primitives, which is the smallest coupling that still lets the two
modules cooperate.

### High cohesion

A module is a vertical slice, and the shared kernel is kept a kernel by one
rule: *nothing about jobs, invoices, tenancy or scheduling belongs in `Common`;
if a type would only ever be used by one module, it lives in that module.*
`Common.Domain` therefore holds six primitives — `Entity`, `AggregateRoot`,
`ValueObject`, `Result`, `Error`, `IDomainEvent` — and no policy. Without that
rule a shared kernel accumulates until the modules are coupled through it, which
is the usual way a modular monolith stops being modular.

### Also present

| Principle | Where |
|---|---|
| **Polymorphism** | One `JobCompletedDomainEvent` has several `INotificationHandler<>` implementations — translate to the integration contract, notify the customer — and the publisher calls each without knowing which exist. Adding a consequence is adding a class, never editing a `switch` |
| **Pure fabrication** | `OutboxProcessor` and `InsertOutboxMessagesInterceptor` name nothing in the business vocabulary. They exist so the domain never learns about delivery |
| **Indirection** | The typed event emitter between frontend slices, and MediatR between senders and handlers. Both exist to remove a direct reference, and in the frontend's case to remove an import cycle (`context/architecture.md` 9.4) |
| **Protected variations** | `ITenantContext` hides where the tenant came from — today a JWT claim, tomorrow an identity provider — from every handler that scopes by it |

---

## 4. GoF patterns

| Pattern | Where used | Problem solved |
|---|---|---|
| **Repository** | `IJobRepository` (Domain) + `JobRepository` (Infrastructure, `internal sealed partial`) | Abstracts persistence behind a domain-shaped interface; handlers are unit-testable with Moq and never see EF |
| **Unit of Work** | `IUnitOfWork.SaveChangesAsync` over the module's `DbContext` | Makes the aggregate's state change and its outbox rows one atomic commit, which is what `NFR-2` rests on |
| **Observer** | `AggregateRoot.Raise(...)`, drained by the interceptor and republished through MediatR | `Job` records that it completed without knowing that Billing and Notifications exist |
| **Mediator** | MediatR between `Jobs.Presentation` and the handlers | An endpoint holds no reference to a handler, which is why handlers can be `internal` — the modifier does real work here |
| **Command** | `CreateJobCommand`, `CompleteJobCommand`, `StartJobCommand`, `CancelJobCommand` | A request as an object, which is what makes a validation and logging pipeline possible at all |
| **Builder** | `QueryBuilder<T>` in `shared/query-builder/` | Stepwise construction where each step **narrows the type** available to the next: after `.select('id','title')`, `.where('status', …)` does not compile |
| **Strategy** | `JobsPort` with `HttpJobsAdapter` and `InMemoryJobsAdapter` | Behaviour swapped by configuration with no change to callers. It is what lets the E2E suite run with no backend and no database |
| **Factory Method** | `Job.Create(...)`, `Address.Create(...)`, both returning `Result<T>` | Creation that can fail, without exceptions and without a half-built aggregate ever existing |
| **State** | Backend: `JobStatus` plus `Reschedule`/`Start`/`Complete`/`Cancel`. Frontend: the `JobState` union plus `transitionJob` | Legal transitions live in one place. On the frontend they live in the **type system**: an invalid transition is a compile error, not a runtime check (D-10) |
| **Template Method** | `ValueObject.Equals` / `GetHashCode` in `Common.Domain`, deferring to the abstract `GetEqualityComponents()` | The equality algorithm is written once; `Address` supplies only its six components |
| **Adapter** | `HttpJobsAdapter` mapping `ProblemDetails` ↔ `CoreError` | Two error vocabularies reconciled at the boundary, so the core sees one failure vocabulary regardless of transport |
| **Decorator / Chain of Responsibility** | `IPipelineBehavior` wrapping every handler | Validation and logging added around a handler without modifying it — the mechanism behind the O in SOLID above |
| **Composite** | The compound `<JobFilterBar>` with `.Search`, `.Status`, `.DateRange`, `.Assignee`, `.Clear` | The caller decides which filters appear and in what order; the root does not know the set |

**Patterns deliberately not used**, because the rubric asks whether they are
genuinely applied. There is no generic `IRepository<T>` base class — it would be
a Template Method forced onto three methods and would widen the interface past
what `Jobs` uses (see I, above). There is no Abstract Factory over the two
`JobsPort` adapters: one `if` at the composition root is the honest amount of
machinery for two implementations chosen once at startup. And there is no
Singleton anywhere; lifetime is the container's concern, not a type's.

---

## 5. DDD concepts

### Bounded context

`Jobs` and `Billing` are separate contexts in one process. The boundary is
enforced three ways: separate projects with no reference between their internals,
separate PostgreSQL schemas so a cross-module join fails rather than silently
working, and an architecture test that fails the build on a violation.

The word *job* means different things on each side, which is the point of a
bounded context rather than a shared model. To `Jobs`, a job is an aggregate with
a lifecycle, photos and a signature. To `Billing`, it is a `JobId` on an invoice.
Sharing one `Job` class between them would force `Billing`'s needs into a model
governed by roofing-work rules.

### Open Host Service

`Jobs.IntegrationEvents` **references nothing**. That is the definition doing
work: a project with no dependencies cannot leak a module's internals, so the
contract stays publishable. It is the only surface another module may compile
against, and the compiler — not a code review — is what enforces it.

### Domain events vs integration events

| | Domain event | Integration event |
|---|---|---|
| Audience | Inside the module | Across modules |
| Payload | May carry the module's own types | Primitives only |
| Stability | Changes with the module | Versioned; a change breaks consumers |
| Example | `JobCompletedDomainEvent` | `JobCompletedIntegrationEvent` |

The integration event is deliberately **poorer** than the domain event:
consumers receive an identifier, a customer, a timestamp and an amount basis —
not a `Job`. Collapsing the two would mean either exposing `Jobs.Domain` to
`Billing`, or letting `Billing`'s needs dictate the shape of an event internal to
`Jobs`. Both are the coupling the boundary exists to prevent.

One ordering detail matters and is easy to get wrong. The outbox stores the
**domain** event, not the integration event (D-13). The interceptor runs during
`SaveChanges`, whereas the integration event is produced by a handler that only
runs after the domain event is published — which happens after the commit.
Storing the domain event is what preserves the transactional guarantee.

### Eventual consistency

An invoice does not exist at the instant a job is completed. Office staff see
the completion immediately; the invoice and the notification follow within
seconds (`NFR-4`). The product accepts that window in exchange for the guarantee
that neither is ever lost (`NFR-2`), and the interface is built to match: A5
states that completion "does not wait for them and does not claim they have
happened."

The window is bounded by the outbox poll interval, which is a number in
configuration rather than a property of the design.

### Idempotency

At-least-once delivery makes repeated handling certain, not hypothetical: a
crash between a handler succeeding and `processed_on` being stamped replays the
message. One rule answers it:

> **Idempotency is a property each consumer must have, and each one earns it
> with a unique constraint on the data it writes.**

| Consumer | Its key |
|---|---|
| `GenerateInvoiceOnJobCompletedHandler` | `uq_invoices_idempotency (job_id, job_completed_at)` |
| `NotifyCustomerOnJobCompletedHandler` | `uq_notifications_idempotency (source_event_id, recipient)` |
| `NotifyAssigneeOnJobCreatedHandler` | the same constraint, a different row |
| `JobCompletedDomainEventHandler` | none, and none is needed — it only translates and publishes, and the three above absorb a duplicate |

```sql
CONSTRAINT uq_invoices_idempotency      UNIQUE (job_id, job_completed_at)
CONSTRAINT uq_notifications_idempotency UNIQUE (source_event_id, recipient)
```

The first is the key line 246 names by hand. The second is its counterpart for
`FR-8` and `FR-10`, so a customer is billed once and notified once per
completion no matter how often the message is delivered.

What makes one rule sufficient is that every key derives from something **stable
across replays**: a completion timestamp does not change, and `source_event_id`
is the outbox row's own identifier.

**Why there is no generic deduplication table.** A table keyed by
`(outbox_message_id, handler_name)` is the usual generic answer, and this design
dropped it deliberately. It protected nothing the constraints above do not, its
key included a handler name and so broke on a rename, and it would have forced
Billing to write into the `jobs` schema — contradicting the schema boundary that
makes the modules separable. Idempotency enforced at the level of the data
outlives the dispatcher that happens to deliver the message.
