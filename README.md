# JobTracker

Multi-tenant job management system for a roofing company. Office staff create,
assign, schedule and complete roofing jobs; completing a job asynchronously
generates an invoice and notifies the customer through a reliable outbox-backed
message pipeline.

Built as a technical assessment for a Senior Fullstack Engineer position.

## Stack

| Layer    | Technology |
|----------|------------|
| Frontend | TypeScript (strict), Next.js 15 App Router, Zustand, Vitest, Playwright |
| Backend  | .NET 9, Modular Monolith + DDD, CQRS (MediatR), EF Core, Hangfire, xUnit |
| Database | PostgreSQL (schema per module) |
| Infra    | Docker Compose, GitHub Actions |

## Architecture

- **Backend:** Modular Monolith. Each module owns a Clean Architecture stack
  (`Domain` -> `Application` -> `Infrastructure`, exposed through `Api`) and a
  dedicated PostgreSQL schema. Modules communicate only through published
  integration-event contracts, never through each other's internals.
- **Frontend:** Feature Sliced Design + Atomic Design. Server Components own
  data fetching; Client Components are thin shells whose state lives in hooks.
- **Async pipeline:** domain events are captured into a transactional outbox on
  `SaveChanges`, then polled by a Hangfire recurring job which dispatches
  integration events to the Billing module and the notification handler.

## Repository layout

```
.
|-- frontend/          Next.js 15 application (FSD)
|-- backend/           .NET 9 solution (modular monolith)
|-- database/          Schema, migrations and optimized queries
|-- docs/              Architecture diagram and design analysis
|-- context/request/   Original assessment specification
`-- docker-compose.yml Full local stack
```

## Getting started

Setup instructions are added as each part of the stack lands. See
`docs/` for architectural decisions, trade-offs and assumptions.

## Status

Work in progress. Repository initialized; implementation follows the plan in
`docs/`.
