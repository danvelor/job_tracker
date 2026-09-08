# JobTracker — Product Requirements Document

| | |
|---|---|
| **Product** | JobTracker — job management for roofing contractors |
| **Document** | Product requirements (what the system does and why) |
| **Companions** | `context/architecture.md` (how it is built), `context/design.md` (what it looks like) |
| **Source** | `context/request/technical-assessment-fullstack-senior 3 1.md` |

This document is written in business language. It contains no technology
decisions: no framework, database, or library appears here by design. Where a
requirement exists only because the assessment rubric rewards it, it is recorded
in the traceability matrix at the end rather than dressed up as a product need.

---

## 1. Product overview

A roofing contractor's office staff manage work from intake to close-out.
JobTracker holds that workflow: staff create a job for a customer at an address,
schedule it for a date, assign it to a crew member, track it while the crew is
on the roof, and close it out with photographic evidence and the customer's
signature.

Closing a job has consequences beyond the job record. The customer must be
billed, and the customer must be told the work is done. Neither of those should
make the office staff wait, and neither may be silently lost if the billing or
messaging system is briefly unavailable.

The system is **multi-tenant**. Several independent roofing companies use one
installation. An organization is a hard data boundary: no user ever sees, counts,
searches, or is notified about a job belonging to another organization.

### Why this product exists

Roofing work is scheduled against weather and crew availability, executed away
from the office, and disputed after the fact. The three things that create value
here are therefore: knowing what is scheduled and to whom, holding proof that
the work was done, and billing promptly once it was.

---

## 2. Actors and roles

| Actor | Description | Responsibilities in scope |
|---|---|---|
| **Office staff** | Employee of a tenant organization working in the back office | Creates jobs, schedules and assigns them, searches and filters the job list, records start, records completion with photos and signature, cancels jobs with a reason |
| **Crew member** (assignee) | Field worker a job is assigned to | Receives a notification when assigned a job. Does not operate the system in this scope — see out of scope |
| **Customer** | The property owner the job is performed for | Receives a notification when the job is completed, and is billed for it |
| **Organization** | The tenant | Not a user. The boundary that partitions every record and every query in the system |

Office staff is the only actor who operates the interface. The crew member and
the customer are recipients of notifications. This keeps the interface single-role
and puts the whole of the workflow in one place.

---

## 3. Domain glossary

| Term | Meaning |
|---|---|
| **Job** | A unit of roofing work for one customer at one address, owned by one organization. The central concept of the system |
| **Organization** | A roofing company using the system. The tenant boundary |
| **Customer** | The party the job is performed for and billed to |
| **Assignee** | The crew member responsible for executing a job |
| **Address** | Where the work happens: street, city, state, ZIP, and geographic coordinates. Identified by its values, not by an identity of its own |
| **Job photo** | A photograph taken during execution, evidencing the state of the work. Belongs to exactly one job and has no meaning apart from it |
| **Signature** | The customer's acceptance of completed work, captured at close-out |
| **Invoice** | The billing document raised for a completed job. Lives in a separate area of the business from job execution |
| **Notification** | A message sent to a crew member or a customer as a consequence of something happening to a job |

**Address** and **job photo** are deliberately described as dependent on the job.
That dependence is a product statement, not a technical one: an address with no
job is not a thing the business tracks, and a photo detached from its job is
worthless as evidence.

---

## 4. Job lifecycle

A job is in exactly one of five states.

| State | Meaning | Data the job carries in this state |
|---|---|---|
| **Draft** | Recorded but not yet committed to a date | Optional notes |
| **Scheduled** | Committed to a date and a crew member | Scheduled date, assignee |
| **InProgress** | The crew is on site | Start timestamp, assignee, photos |
| **Completed** | Work finished and accepted by the customer | Start and completion timestamps, assignee, photos, signature |
| **Cancelled** | Called off | Cancellation timestamp, reason |

### Permitted transitions

```
  Draft ─────────────▶ Scheduled ─────────────▶ InProgress ─────────────▶ Completed
                            │                        │
                            └────────▶ Cancelled ◀───┘
```

| From | To | Trigger |
|---|---|---|
| Draft | Scheduled | Staff sets a date and an assignee |
| Scheduled | InProgress | Staff records that the crew has started |
| Scheduled | Cancelled | Staff cancels, with a reason |
| InProgress | Completed | Staff records completion with photos and signature |
| InProgress | Cancelled | Staff cancels, with a reason |

**Completed and Cancelled are terminal.** No transition leaves either state. A
job that was closed in error is not reopened; the correction is a new job.

Draft exists because a job is often recorded before a date is agreed with the
customer. Creating a job through the interface goes straight to Scheduled,
because the creation form collects a date and an assignee — see `FR-1`.

---

## 5. Functional requirements

### FR-1 — Create a job

Office staff record a new job by supplying a title, an optional description, the
service address, a scheduled date, an assignee, and the customer.

The created job is **Scheduled**, not Draft: the information the form collects is
exactly what Scheduled requires. Draft remains reachable in the model for jobs
captured without a date, but the interface does not produce one.

*Accepted when:* the job exists, belongs to the acting user's organization, is in
state Scheduled, and appears in that organization's job list.

### FR-2 — Schedule and assign

A scheduled date and an assignee are recorded against the job. Both are set at
creation (`FR-1`) and may be corrected while the job is Scheduled.

*Accepted when:* the job carries the date and assignee, and `BR-1` is enforced.

### FR-3 — Start a job

Office staff record that the crew has begun. The system captures the start
timestamp.

*Accepted when:* the job moves from Scheduled to InProgress, the start timestamp
is recorded, and the transition is refused from any other state per `BR-3`.

### FR-4 — Complete a job

Office staff close the job out, supplying the customer's signature and any
photographs taken on site. The system captures the completion timestamp.

*Accepted when:* the job moves from InProgress to Completed, carries the
signature and photos, and triggers `FR-9` and `FR-10`.

### FR-5 — Cancel a job

Office staff call a job off, supplying a reason. The reason is mandatory: a
cancelled job with no explanation is not auditable.

*Accepted when:* the job moves to Cancelled from Scheduled or InProgress, carries
the timestamp and reason, and is refused from a terminal state per `BR-2`.

### FR-6 — Search and filter jobs

Office staff narrow the job list by free text over title and description, by one
or more states, by a scheduled-date range, and by assignee. Filters combine.

*Accepted when:* results honour every active filter simultaneously and contain
only jobs of the acting user's organization.

### FR-7 — Page through results

The job list is paged. Paging remains correct and responsive on a list far larger
than one page, and does not skip or repeat rows when the underlying data changes
between pages.

*Accepted when:* successive pages return disjoint, ordered, complete results.

### FR-8 — Notify the crew on assignment

When a job is created and assigned, the assignee is notified.

*Accepted when:* creating a job produces exactly one notification to the
assignee, and creating the job does not wait for it.

### FR-9 — Raise an invoice on completion

Completing a job raises an invoice for the customer. Billing is a separate area
of the business: office staff neither wait for it nor see it fail.

*Accepted when:* an invoice exists for every completed job, exactly one per
completion, and completion succeeds even when billing is unavailable at that
moment.

### FR-10 — Notify the customer on completion

Completing a job notifies the customer that the work is done.

*Accepted when:* exactly one notification per completion reaches the customer,
and completion succeeds even when the messaging system is unavailable at that
moment.

### FR-11 — Isolate organizations

Every read and every write is confined to the acting user's organization.

*Accepted when:* no listing, search, count, aggregate, direct fetch by
identifier, or notification can reach a record of another organization.

---

## 6. Business rules

| # | Rule | Rationale |
|---|---|---|
| **BR-1** | A job cannot be scheduled in the past | A date already gone is a data-entry error, not a plan. Enforced at the moment of writing, against the system clock |
| **BR-2** | A job in a terminal state never changes state | Completed and Cancelled are the audit record. Corrections are new jobs |
| **BR-3** | Only a Scheduled job can start | Work that was never scheduled has no date, assignee, or customer agreement behind it |
| **BR-4** | Completion requires a customer signature | The signature is the product's proof of acceptance; completion without it has no evidentiary value |
| **BR-5** | Cancellation requires a reason | A cancelled job with no explanation cannot be reviewed later |
| **BR-6** | A job belongs to exactly one organization, fixed at creation | The tenant boundary cannot move. Reassigning a job across organizations is not an operation the business has |

Rules are enforced by the job itself, wherever the job is used. A rule that only
holds when a particular screen or endpoint is involved is not enforced.

---

## 7. Non-functional requirements

### NFR-1 — Tenant isolation is structural

Isolation does not depend on any caller remembering to filter. It is enforced at
the lowest layer that touches data, so that a query written without a tenant
condition still cannot return another organization's rows.

### NFR-2 — Consequences of completion are never lost

Invoicing and customer notification survive a crash, restart, or outage between
the moment a job is completed and the moment they are carried out. If the job was
completed, the invoice and the notification eventually happen. Delivery is
guaranteed **at least once**.

### NFR-3 — Repeated delivery causes no duplicate effects

Because delivery is at-least-once, the same completion may be handled more than
once. A customer is billed once per completed job and notified once per
completed job regardless of how many times the consequence is delivered.

### NFR-4 — Consequences are eventually consistent

An invoice does not exist at the instant a job is completed. Office staff see the
completion immediately; billing and notification follow within seconds. The
product accepts this window in exchange for `NFR-2`.

### NFR-5 — Listing stays responsive as data grows

Paging cost does not grow with the page number: reaching page 500 is as cheap as
reaching page 2.

### NFR-6 — Records are auditable

Every job carries when it was created and when it was last changed, and every
state change carries its own timestamp.

### NFR-7 — The delivered system builds and its checks pass

A reviewer can start the system from a clean checkout with no credentials and
no external service account, and exercise the whole workflow including the
asynchronous consequences of completion.

---

## 8. Acceptance criteria — end-to-end

The following walkthrough is the product's definition of working, and is
automated as the end-to-end test.

1. Office staff open the job list for their organization
2. They create a job through the creation form, supplying title, address,
   scheduled date, assignee, and customer
3. The new job appears in the list in state **Scheduled**
4. The assignee is notified of the assignment (`FR-8`)
5. Staff narrow the list by state and find the job
6. Staff record that the crew has started; the job shows **InProgress**
7. Staff complete the job, supplying signature and photos
8. The job shows **Completed** in the list
9. Shortly afterwards, an invoice exists for the job (`FR-9`) and the customer has
   been notified (`FR-10`), neither of which the staff waited for

Step 6 is explicit because a job cannot go from Scheduled to Completed: `BR-3`
and the lifecycle require a start. The assessment's end-to-end description
(lines 320-327) omits it; the walkthrough above restores it so the flow is
executable against the domain rules the same document specifies.

---

## 9. Out of scope

| Not included | Why |
|---|---|
| Payment collection, tax, invoice PDFs | Billing exists here only far enough to prove that completing a job raises an invoice in a separate area of the business |
| Real email or SMS delivery to live recipients | Delivery is simulated: each notification is recorded with a status, and reaching `Sent` is the proof. Delivering to real addresses requires an account and adds nothing to what is being demonstrated |
| Crew-facing mobile application | The crew is a notification recipient in this scope. Photos and signature are captured by office staff |
| Customer portal | Customers receive notifications; they do not log in |
| Customer and crew **administration** | The system holds a roster of both so that a job can name who it is for and who is doing it, but creating, editing and deactivating them belongs to areas of the business outside this workflow. The roster is fixed at installation |
| Weather, routing, inventory, payroll | Adjacent roofing concerns, none of which the job workflow depends on |
| Self-service sign-up and organization provisioning | Organizations and users exist; creating them is an administrative concern outside this workflow |

---

## 10. Traceability matrix

Product requirements against their origin in the assessment and the rubric
section that scores them.

| Requirement | Assessment source | Rubric section | Points at stake |
|---|---|---|---|
| Product overview, multi-tenancy | line 11 | 3 — Aggregate design, 4 — Schema design | 7 + 4 |
| Job lifecycle and states | lines 70-78, 170-174 | 1 — State machine types, 3 — Aggregate design | 5 + 7 |
| `FR-1` create | lines 195-198 | 3 — CQRS, 2 — FSD structure | 6 + 5 |
| `FR-2` schedule and assign | lines 170, 172 | 3 — Aggregate design | 7 |
| `FR-3` start | line 174 | 3 — Aggregate design, 1 — State machine | 7 + 5 |
| `FR-4` complete | lines 76-77, 200-203 | 3 — CQRS, 3 — Outbox | 6 + 4 |
| `FR-5` cancel | lines 78, 175 | 3 — Aggregate design | 7 |
| `FR-6` search and filter | lines 205-208, 279-284 | 3 — CQRS, 4 — Indexing | 6 + 3 |
| `FR-7` paging | lines 207, 283 | 4 — Indexing and optimization | 3 |
| `FR-8` notify crew | line 188 | 3 — Outbox and Hangfire | 4 |
| `FR-9` invoice | lines 189, 240 | 3 — Outbox, 6 — DDD concepts | 4 + 3 |
| `FR-10` notify customer | lines 189, 241 | 3 — Outbox and Hangfire | 4 |
| `FR-11` tenant isolation | lines 170, 261, 347 | 4 — Schema design, 6 — Diagram | 4 + 3 |
| `BR-1` no past scheduling | line 172 | 3 — Aggregate, 5 — Backend tests | 7 + 5 |
| `BR-2` terminal states | line 173 | 3 — Aggregate, 5 — Backend tests | 7 + 5 |
| `BR-3` start only from Scheduled | line 174 | 3 — Aggregate, 5 — Backend tests | 7 + 5 |
| `BR-4` signature on completion | line 77 | 3 — Aggregate design | 7 |
| `NFR-1` structural isolation | line 347 | 6 — Architecture diagram | 3 |
| `NFR-2` at-least-once | lines 11, 245 | 3 — Outbox, 6 — DDD concepts | 4 + 3 |
| `NFR-3` no duplicate effects | line 246 | 3 — Outbox, 6 — SOLID and GRASP | 4 + 5 |
| `NFR-4` eventual consistency | line 356 | 6 — DDD concepts | 3 |
| `NFR-5` paging cost | lines 283, 286 | 4 — Indexing and optimization | 3 |
| `NFR-7` builds and runs | lines 472-473, 490 | Passing gate for every section | all |
| Acceptance walkthrough | lines 320-327 | 5 — E2E Playwright | 5 |

### Requirements with no product justification

These exist because the rubric scores them. They are recorded here so they are
not dropped as arbitrary, and they are specified in `context/architecture.md`
rather than invented as business needs.

| Requirement | Assessment source | Rubric section | Points |
|---|---|---|---|
| Partial classes splitting the repository | line 222 | 3 — Repository and UoW | 5 |
| `sealed` / `internal sealed` naming conventions | lines 210-215 | 3 — CQRS and MediatR | 6 |
| Ternary rather than `&&` for conditional rendering | line 159 | 2 — React patterns | part of 20 |
| Compound component for the filter bar | line 155 | 2 — React patterns | part of 20 |
| `useReducer` for the creation form | line 156 | 2 — React patterns | part of 20 |
| Type-level utilities (`DeepReadonly`, `PathKeys`) | lines 24-34 | 1 — TypeScript mastery | 5 |
| Typed event emitter | lines 36-42 | 1 — TypeScript mastery | part of 15 |
| Chainable query builder | lines 47-63 | 1 — Type-safe builder | 5 |

### Assessment contradictions resolved in this document

| Contradiction | Resolution |
|---|---|
| The end-to-end flow (lines 320-327) completes a job without starting it, which `BR-3` and lines 85 and 174 forbid | The walkthrough in section 8 adds the explicit start step |
| Creation is ambiguous between Draft (line 73) and a job carrying `ScheduledDate` and `AssigneeId` (line 170) | Creation produces a Scheduled job. Draft stays in the model, unreachable from the interface (`FR-1`) |
