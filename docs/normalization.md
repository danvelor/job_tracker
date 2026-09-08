# Normalization, denormalization and integration events

| | |
|---|---|
| **Document** | The reviewer-facing answer to part 4.3 (and 4.1 point 5) of the assessment |
| **Companions** | `context/architecture.md` 6.5 states the position; `context/design.md` B7 is the schema that implements it |
| **Source** | `context/request/technical-assessment-fullstack-senior 3 1.md`, lines 270, 289-294 |

## The position this schema takes

`jobs.jobs` stores `customer_id` and `assignee_id` as identifiers and copies no
names. A customer who corrects the spelling of their name does not require
rewriting job rows, and there is exactly one place where that name is true.

## When to denormalize the customer name into `jobs`

Three conditions have to hold **together**, and the job list satisfies all
three.

*The read is frequent and latency-sensitive.* The list is the product's working
surface; it is re-read on every keystroke of a debounced search and on every
page of a keyset scan.

*The join would cross a bounded context.* `Contacts` owns customers. Joining
would mean either a foreign key across schemas — recreating exactly the coupling
that schema-per-module exists to prevent — or a second round trip per page.

*The copied value tolerates being briefly stale.* A customer name displayed a few
seconds after a rename is not a defect. Contrast `scheduled_date`, which drives
`BR-1` and must never be a copy.

Where these do not hold, normalize. Anything a rule is evaluated against stays
normalized regardless of read cost, because a stale copy that an invariant reads
is not a performance trade-off — it is a correctness bug.

## When integration events instead

They are not alternatives; the event is *how* a correct denormalization is
maintained. The wrong way to place `customer_name` on `jobs` is a scheduled job
that re-reads Contacts, or a foreign key across the boundary. The right way is a
`CustomerRenamedIntegrationEvent` that `Jobs` consumes and applies to its own
copy — the same outbox path that already carries `JobCompletedIntegrationEvent`
to `Billing`, so it inherits at-least-once delivery and the idempotency
guarantees of `context/architecture.md` 4.5.

Use an integration event alone, with no copy, when the consumer needs to *act*
rather than to *display*: `Billing` does not store a job's title, it raises an
invoice and forgets.

## The consistency trade-off, concretely

| | Join across the boundary | Denormalized copy, synced by event |
|---|---|---|
| Consistency | Strong: one row, always current | Eventual: a window of seconds after a rename |
| Read cost | A join per page, and a cross-schema dependency | None — the value is on the row already |
| Write cost | None | One event published, one consumer, one update |
| Failure mode | A slow or unavailable Contacts query degrades the job list | A dropped event leaves a stale name until the next one |
| Coupling | Compile-time and schema-level | A versioned contract of primitives |

The honest summary: denormalization buys read latency and module independence
with write complexity and a bounded staleness window. It is worth it for a
display value on a hot path, and never worth it for a value a business rule
reads.

## What this schema actually does

It stays normalized. `customer_name` is **not** on `jobs.jobs` today, because the
list is served from a seeded set where the join does not yet exist to be
avoided, and adding a denormalized column with no consumer would be speculation
rather than design. The analysis above is what would justify adding it, and the
integration-event machinery that would maintain it is already in place — which
is the point worth making: the decision is a migration and a handler, not a
redesign.
