# JobTracker

Multi-tenant job management for a roofing contractor. Office staff schedule,
assign and complete roofing jobs; completing one asynchronously raises an
invoice and notifies the customer through a transactional outbox.

Built as a technical assessment for a Senior Fullstack Engineer position.

---

## Run it

You need Docker. Nothing else — no .NET SDK, no Node, no credentials, no
database to create and no migration to apply.

```bash
docker compose up --build
```

Then open **<http://localhost:3000/jobs>**.

The first build takes a few minutes. When it finishes, three containers are
running: PostgreSQL, the .NET API on `:8080`, and the Next.js app on `:3000`.
The API applies both modules' migrations and seeds the crew and customer
rosters before it starts serving, so the interface works on the first click.

| | |
|---|---|
| Interface | <http://localhost:3000/jobs> |
| API | <http://localhost:8080> |
| OpenAPI | <http://localhost:8080/openapi/v1.json> |
| Background jobs | <http://localhost:8080/hangfire> — the outbox drain and every send. Development only, and refused from a public address |
| Database | `localhost:5432`, `jobtracker` / `jobtracker` |

Watch the asynchronous half work: create a job, complete it, and within a few
seconds an invoice and two notifications appear.

```bash
docker compose exec postgres psql -U jobtracker -d jobtracker \
  -c "select recipient, status from jobs.notifications" \
  -c "select amount, job_id from billing.invoices"
```

Neither is announced in the interface, and that is deliberate — completion does
not wait for them and does not claim they happened.

To stop, and to discard the data:

```bash
docker compose down -v
```

### Running the pieces separately

```bash
# Backend — needs PostgreSQL on 5432
dotnet run --project backend/src/Api/JobTracker.Api

# Frontend — no backend needed; uses the in-memory adapter
npm --prefix frontend run dev
```

The frontend serves the in-memory adapter unless `JOBTRACKER_API_URL` is set.
That is what lets the browser suite run with neither backend nor database.

---

## Verify it

```bash
# Backend: 310 tests — domain, application, architecture, integration
dotnet test backend/JobTracker.sln

# Frontend: 232 tests, with an 80% coverage gate that fails the run
npm --prefix frontend run test:coverage

# Types. Separate on purpose: expect-type assertions fail at compile time,
# so a broken type test leaves Jest green
npm --prefix frontend run typecheck

# Browser, against the in-memory adapter: 7 tests, no backend required
npm --prefix frontend run test:e2e

# The acceptance walkthrough against the running stack — all nine steps
docker compose up --build --wait
npm --prefix frontend run test:smoke
```

The integration suite starts its own PostgreSQL through Testcontainers, so it
needs Docker but no setup.

**The smoke run is the definition of done.** The other suites prove the parts;
this one proves the system, and it is the only place the outbox, Hangfire,
Billing and the notification handlers are exercised together over a network.

---

## What it does

Nine steps, from `context/prd.md` §8, each covered by the smoke run:

1. Open the job list
2. Create a job through the form
3. It appears as **Scheduled**
4. **The assigned crew is notified** — asynchronous
5. Narrow the list by status and find it
6. Record that the crew started
7. Complete it with a signature and photos
8. It shows **Completed**
9. **An invoice exists and the customer was notified** — asynchronous

Step 6 is not in the assessment's flow. It is in ours because `BR-2` forbids
completing a job that never started, so the flow as written cannot succeed.

---

## Architecture

```
┌─ BROWSER ────────────────────────────────────────────────────────────┐
│  Feature Sliced Design + Atomic Design                               │
│  Thin client shells; SWR owns server state, Zustand owns UI state    │
└───────────────────────────────┬──────────────────────────────────────┘
                                │  Server Actions (mutations only)
┌─ NEXT.JS SERVER ──────────────┴──────────────────────────────────────┐
│  Server Components fetch; an unresolved promise crosses into         │
│  <Suspense> so the skeleton is real                                  │
│  core/ ──▶ JobsPort ──┬── HttpJobsAdapter  (bearer token, ProblemDetails) │
│                       └── InMemoryJobsAdapter  (tests, no backend)   │
└───────────────────────────────┬──────────────────────────────────────┘
                                │  HTTP + JWT carrying an `org` claim
┌─ .NET 9 MODULAR MONOLITH ─────┴──────────────────────────────────────┐
│  JWT · tenant context · rate limit · ProblemDetails · MediatR        │
│                                                                      │
│  ┌── Jobs ─────────────────┐        ┌── Billing ────────────────┐   │
│  │ Presentation            │        │ (no endpoints — D-04)     │   │
│  │ Application  CQRS       │        │ Application               │   │
│  │ Domain       aggregate  │        │ Domain  Invoice           │   │
│  │ Infrastructure  EF      │        │ Infrastructure  EF        │   │
│  │ schema: jobs            │        │ schema: billing           │   │
│  └───────────┬─────────────┘        └────────────▲──────────────┘   │
│              │  domain event                     │ integration event │
│              ▼                                   │  (primitives only)│
│      jobs.outbox_messages ──▶ Hangfire drain ────┘                   │
│      (same transaction)        FOR UPDATE SKIP LOCKED                │
└──────────────────────────────────────────────────────────────────────┘
```

The full diagram, plus SOLID, GRASP, GoF and DDD analysis, is in
[`docs/design-principles.md`](docs/design-principles.md).

---

## Decisions and trade-offs

Thirty-eight decisions are recorded with their alternatives, rationale and cost
in [`context/architecture.md` §11](context/architecture.md). The ones a reviewer
is most likely to want explained:

**The assessment contradicts itself in six places, and each resolution is
recorded rather than silently chosen.** The clearest: `transitionJob(current:
JobState, action: JobAction)` can never fail at compile time, because any action
is assignable to the union. A generic signature constrained by the narrowed
state is what makes an invalid transition a build error (D-10).

**Completing a job does not wait for its invoice.** The API answers 204 and the
interface makes no claim about billing. The alternative is a slow endpoint that
fails when Billing does, for a consequence the user is not waiting on.

**The outbox stores domain events, not integration events.** The assessment says
otherwise, and that ordering cannot work: the integration event is produced by a
handler that runs after the commit, so an interceptor cannot serialise something
that does not exist yet (D-13).

**Billing has a real domain, not a stub handler.** An `Invoice` with its own
invariants and its own pricing rule is what makes the module boundary
demonstrable — Jobs says what happened, Billing decides what it is worth. Jobs
does not know the labour rate and must not learn it (D-04, D-33).

**No message broker.** Both modules share a process and a database, so a broker
adds a container, a topology and reconnection handling for no behavioural gain.
Only the poller would change (D-03).

**Delivery is simulated.** `LoggingNotificationSender` writes a line and the row
moves to `Sent`. Real deliverability is out of scope and the rubric scores none
of it; `jobs.notifications` is better evidence than an inbox, because a test can
assert on a table (D-08).

**Every dependency is checked for its licence and pinned below a commercial
major.** MediatR 13 and FluentAssertions 8 both moved to paid licences, and an
unbuildable deliverable scores nothing. Tests assert the pins, because a comment
is not a check (D-20).

**Tenant isolation is enforced below the query.** A global EF filter means a
query written with no tenant condition still cannot return another
organization's rows, and architecture tests fail the build if a tenant-scoped
entity has no filter. Background work declares its tenant explicitly, which is
a thing the Compose stack taught us rather than something we knew (D-30, D-35,
D-36).

---

## Assumptions

**Cancelling a job notifies the crew. The assessment does not ask for this.**
It requires `JobCancelledDomainEvent` to be raised (line 175) and never gives
it a consumer: only creation and completion are said to notify (lines 188-189).
We read that omission as a gap rather than a rule. A crew that learns of a
cancellation by arriving on site has learned too late, and `BR-5` already
forces a reason to exist for them to read — so `FR-12` notifies the assignee,
with the reason, down the same outbox path as `FR-8`.

The crew and not the customer, because the reason is written for internal
review and is not always the customer's business. A job cancelled before it was
ever assigned notifies nobody.

It also sharpens the argument the assessment *does* ask for (line 244).
Cancelling now does real work and still never leaves the module, which settles
what sends an event across a boundary: another module needing it, not the event
having consequences. Beforehand cancellation was an internal event with no
consumer at all, and "internal" could be read as "nothing happens". Recorded as
D-37, and the end-to-end test asserts that cancelling tells the crew and still
never bills.

**One organization is seeded, and the rosters are fixed.** Crew and customer
administration is out of scope, so both are read-only rosters seeded by
migration. Tenant isolation is enforced and tested against a second
organization, but only one is reachable through the interface — see the first
item under *What I would improve* below.

---

## What I would improve given more time

**A second tenant in the interface.** The backend enforces isolation and the
tests prove it, but a reviewer only sees one organization. A tenant switcher
would make `NFR-1` visible rather than merely tested.

**Notification retry beyond Hangfire's default.** A send that fails is recorded
as `Failed` and retried on Hangfire's backoff, but there is no dead-letter view
and no alert. A table of permanently failed notifications is the kind of thing
that matters on day two and never on day one.

**OpenTelemetry.** Declined as a decision (D-15), and it is the bonus criterion
I would take next: a correlation identifier that survives from the browser
through the API into a Hangfire job would make the asynchronous half far easier
to reason about than the dashboard does.

**Cursor pagination in the interface.** The API pages by keyset and the read
model carries `nextCursor`, but the list does not yet offer a "load more". The
measurement that justified keyset over `OFFSET` is captured in
[`database/queries.sql`](database/queries.sql) — 23 buffers against 30,243 for
the same page 30,000 rows deep — and the interface does not exercise it.

**A Contacts module.** The crew and customer rosters live in the `jobs` schema
because three controls need them and administration is out of scope. The
reasoning, and what would change, is in
[`docs/normalization.md`](docs/normalization.md).

**Accessibility beyond the basics.** Every interactive element has an
accessible name and the table is keyboard-navigable, but nothing has been tested
with a screen reader, and that is the only way to know.

---

## Where things are

```
.
├── frontend/            Next.js 15, App Router, FSD + Atomic Design
│   ├── src/core/        Domain and application — imports no React, no Next
│   ├── src/infrastructure/  The two JobsPort adapters
│   ├── src/presentation/    Views, slices, components, stores
│   └── e2e/             Playwright: the in-memory suite and the smoke run
├── backend/
│   ├── src/Common/      Shared kernel: Result, Error, the outbox contracts
│   ├── src/Modules/Jobs/     Domain, Application, Infrastructure, Presentation
│   ├── src/Modules/Billing/  Domain, Application, Infrastructure
│   ├── src/Api/         The composition root — the only project that sees all
│   └── tests/           Unit, architecture and integration suites
├── database/            Annotated schema, indexes, and captured query plans
├── docs/                Design principles, the diagram, normalization
├── context/             Requirements, architecture, design, decision log, plans
└── docker-compose.yml
```

`context/` is the working record: the requirements this was built from, the
architecture it commits to, the decision log, and the eight implementation plans
it was built through. The git history follows those plans one task at a time.
