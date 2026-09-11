# Open questions 

---

## 1. Async processing (3.4, item 2)

### Why use domain events WITHIN the module vs integration events ACROSS modules

Domain events and integration events differ in who owns the contract.
A domain event's contract is internal. Only Jobs subscribes to it, so I own both ends: I can add a field, rename one or drop the event, and the only cost is recompiling Jobs.
An integration event's contract is not the type, it is each one of its properties. Billing compiles against every field

### Why the outbox pattern ensures at-least-once delivery

Because saving the message is part of saving the job, and the message stays in the table until the work it describes has actually been done.

### How idempotency is guaranteed in the invoice handler (idempotency key based on JobId + CompletedAt)

If a send fails it is retried, and that retry can deliver the same event twice. The database has a UNIQUE (job_id, job_completed_at) constraint, so the second insert is rejected, there is exactly one invoice per completed job thanks to the property job_completed_at within the event.

---

## 2. Normalization vs denormalization (4.1, item 5)

### Explain your normalization decisions vs denormalization trade-offs (when would you denormalize and why)

I normalized customer and assignee into their own entities, so a join from jobs reads the name from a single place instead of carrying a name column on jobs itself. If a customer or assignee is ever renamed, that is one UPDATE on one row rather than one per job that references them.

I would denormalize the name onto jobs only if the customers table belonged to another module.

---

## 3. Indexing strategy and cursor pagination (4.2)

### Explain your indexing strategy

organization_id first for the tenant, followed immediately by the sort keys.

### why cursor-based pagination is preferred over OFFSET for large datasets.

In a simply terms, OFFSET counts and KEYSET searches. OFFSET needs to count the records to know where it's going, whereas KEYSET knows where it's going because it uses for example an ID or a date to go directly to the record.

---

## 4. Denormalization vs integration events (4.3)

Denormalize the customer table in different situations: first, when the master table is in a separate module; second, when the job is a reporting table that requires accessing data with the lowest possible latency; and additionally, when the “names” properties, by business rule, do not need to change in the history.

I use integration events when I need to perform a specific action, such as saving a record in another module, and it’s not necessary to display all the data, for example, in billing, the customer’s name isn’t included. Given what was mentioned above, this could lead to future inconsistencies, but it improves latency by having the data ready to be displayed.

---

## 5. Design principles (6.2)

### SOLID

**Single responsibility**

Completing a job has four consequences and each is a separate type. Job.Complete() decides whether completion is legal and records it. InsertOutboxMessagesInterceptor moves the raised events into outbox_messages during SaveChanges. OutboxProcessor reads unprocessed rows, publishes them and stamps processed_on. PublishJobCompletedHandler translates the domain event into the public contract.

**Open for extension, closed for modification**

ValidationBehavior<TRequest, TResponse> in Common.Application wraps every handler in both modules: it runs the request's validators and returns a failed Result without reaching the handler.

**Liskov substitution**

JobsPort has two implementations: HttpJobsAdapter, which talks to the .NET API, and InMemoryJobsAdapter, which needs no backend and no database. The container picks one from configuration and nothing above it knows which.

**Interface segregation**

IJobRepository declares exactly three members — GetByIdAsync, AddAsync and SearchAsync — because those are the three the application uses.

**Dependency inversion**

IJobRepository lives in Jobs.Domain. JobRepository lives in Jobs.Infrastructure. Jobs.Application references Jobs.Domain and does not reference Jobs.Infrastructure at all 

### GRASP

**Expert**
Job.Complete(completedAt, signatureUrl, photos) decides whether completion is legal. Not because Job is the aggregate root, It is because Status and StartedAt are private set and live on Job, so Job is the only thing that can evaluate the rule


**Creator**

Job creates its own domain events. Job.Complete() ends with Raise(new JobCompletedDomainEvent(Id, CustomerId, StartedAt!.Value, completedAt)), because Job holds the data the event needs and the event is part of the same state change.

**Controller**

The CompleteJob endpoint does exactly three things: it builds the command from the request, sends one MediatR message without knowing which handler will take it, and maps the Result to a status code 

**Low coupling**
Billing reacts to a completed job and cannot name a job. Its only using into Jobs is Jobs.IntegrationEvents, a project that itself references nothing.

**High cohesion**

The Jobs module is highly cohesive because everything needed to manage a job lives inside it such as the aggregate and its invariants, its domain events, the commands and queries, the repository, the endpoints and its own schema.

### Idempotency

The outbox retries until it succeeds, so the same message can arrive twice. Without protection that means two invoices.
Every consumer solves it the same way: a unique constraint on the data it writes. (job_id, job_completed_at) for the invoice, (source_event_id, recipient) for notifications.

### Eventual consistency

An invoice does not exist at the instant a job is completed. It appears a few seconds later.
The trade is deliberate: it is never lost, because the event that triggers it is written in the same transaction as the completion, and retried until it succeeds.

### Bounded context

Jobs and Billing run in the same application, but each one has its own idea of what a job is. For Jobs it's an aggregate with a lifecycle, photos and a signature. For Billing it's just an id on an invoice


---

## 6. GoF patterns (6.3)

| Pattern | Where | Problem solved |
|---|---|---|
| **Repository** | `IJobRepository` + `JobRepository` | Persistence behind a domain-shaped interface. Handlers are testable with Moq and never see EF |
| **Unit of Work** | `IUnitOfWork.SaveChangesAsync` | Makes the state change and its outbox rows one atomic commit, which is what at-least-once rests on |
| **Observer** | `AggregateRoot.Raise`, drained by the interceptor, republished by MediatR | `Job` records that it completed without knowing Billing and Notifications exist |
| **Mediator** | MediatR between endpoints and handlers | An endpoint holds no reference to a handler, which is why handlers can be `internal` |
| **Command** | `CreateJobCommand`, `CompleteJobCommand`, `StartJobCommand`, `CancelJobCommand` | A request as an object, which is the only reason a validation pipeline is possible at all |
| **State** | Backend `JobStatus` + `Start`/`Complete`/`Cancel`. Frontend the `JobState` union + `transitionJob` | Legal transitions in one place. On the frontend they live in the type system: an invalid transition is a build error |
| **Strategy** | `JobsPort` with two adapters | Behaviour swapped by configuration. It is what lets the browser suite run with no backend and no database |
| **Factory Method** | `Job.Create`, `Address.Create`, both returning `Result<T>` | Creation that can fail without exceptions, and without a half-built aggregate ever existing |
| **Builder** | `QueryBuilder<T>` | Stepwise construction where each step narrows the type available to the next: after `.select('id','title')`, `.where('status', ...)` does not compile |
| **Template Method** | `ValueObject.Equals` deferring to `GetEqualityComponents()` | Equality written once. `Address` supplies only its six components |
| **Adapter** | `HttpJobsAdapter` mapping `ProblemDetails` to `CoreError` | Two error vocabularies reconciled at the boundary, so the core sees one |
| **Decorator** | `IPipelineBehavior` wrapping every handler | Behaviour added around a handler without modifying it — the mechanism behind the O above |
| **Composite** | `<JobFilterBar>` with `.Search`, `.Status`, `.DateRange`, `.Assignee`, `.Clear` | The caller decides which filters appear and in what order |

---

## 7. Assumptions

### Frontend

**No login screen.** The Next server gets a token from a seeded endpoint and
reads go through a Route Handler, so the bearer token never reaches the browser.
A real identity provider replaces one function.

**The page size is 3.** Small on purpose: the seeded list has four jobs, and a
page size above that leaves "load more" unreachable and therefore unreviewable.

### Backend

**Billing owns pricing.** Jobs sends the labour window and Billing holds the
rate. The assessment mentions an "amount basis" that does not exist — a `Job` has
no price and nothing gives it one. Putting a price on `Job` is scope nobody asked
for; a flat fee makes Billing a stub with extra steps.

**Delivery is simulated.** `LoggingNotificationSender` writes one line and the
row moves to `Sent`. What is graded is the reliability of the pipeline, and the
pipeline is exercised identically whether the last hop is an SMTP socket or a log
write. `jobs.notifications` is better evidence than an inbox, because a test can
assert on a table.

**Cancelling a job notifies the crew.** The assessment requires
`JobCancelledDomainEvent` to be raised and never gives it a consumer. A crew that
learns of a cancellation by arriving on site learned too late, and the rule
already forces a reason to exist for them to read. Beyond the assessment, and
labelled as such.

### Database

**One database, one connection, a schema per module.** Isolation is by schema
rather than by instance, so a cross-module join fails instead of silently
working, and the modules can still commit in one transaction.

**One database for reads and writes**
CQRS here separates the models, not the stores.
