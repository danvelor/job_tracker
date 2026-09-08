# Technical Assessment -- Senior Fullstack Engineer (Next.js + .NET)

**Duration:** 12 hours maximum
**Modality:** Take-home, open-book (you may consult documentation, but all code must be your own)
**Stack:** TypeScript / Next.js 15 (App Router) + .NET 9 / PostgreSQL

---

## Context

You are building **JobTracker**, a multi-tenant job management system for a roofing company. The system allows office staff to create, assign, schedule, and complete roofing jobs. When a job is completed, an invoice must be generated asynchronously, and a notification must be sent to the customer. The system uses a message queue for reliable async processing.

The codebase follows a **Modular Monolith** on the backend (.NET) and **Feature Sliced Design (FSD) + Atomic Design** on the frontend (Next.js).

---

## Part 1 -- TypeScript Deep Dive (90 min)

### 1.1 Generic Constraints and Conditional Types

Implement the following utility types and functions. All must compile with `strict: true`.

```typescript
// A) Create a type `DeepReadonly<T>` that recursively makes all properties 
//    readonly, including nested objects, arrays, Maps, and Sets.
//    - Primitives remain as-is
//    - Arrays become ReadonlyArray<DeepReadonly<Item>>
//    - Maps become ReadonlyMap<K, DeepReadonly<V>>
//    - Sets become ReadonlySet<DeepReadonly<V>>

// B) Create a type `PathKeys<T>` that produces a union of all dot-notation 
//    paths to leaf properties of an object type.
//    Example: PathKeys<{ a: { b: string; c: { d: number } } }> 
//    => "a.b" | "a.c.d"

// C) Create a generic function `createTypedEventEmitter<Events>()` where 
//    Events is a record mapping event names to payload types. The returned
//    emitter must enforce:
//    - `.on(event, handler)` — handler signature matches the event payload
//    - `.emit(event, payload)` — payload must match the event type
//    - `.off(event, handler)` — same handler reference
//    The implementation must use no `any` casts.
```

### 1.2 Advanced Generics: Type-Safe Builder

```typescript
// Implement a `QueryBuilder<T>` class that chains methods and infers
// the return type at each step:
//
//   const result = new QueryBuilder<Job>()
//     .select('id', 'title', 'status')       // narrows to Pick<Job, 'id' | 'title' | 'status'>
//     .where('status', 'eq', 'completed')     // validates 'status' exists and value matches type
//     .orderBy('title', 'asc')                // validates 'title' exists in selected fields
//     .limit(10)
//     .build();                               // returns { query: string; params: unknown[] }
//
// Requirements:
// - Each `.select()` call narrows the available fields for `.where()` and `.orderBy()`
// - `.where()` third argument must be assignable to the field's type
// - Use template literal types for the generated query string type
// - No `any` or `as unknown as X` casts
```

### 1.3 Discriminated Unions and Exhaustive Pattern Matching

```typescript
// Model the following domain using discriminated unions:
//
// A Job can be in one of these states: Draft, Scheduled, InProgress, 
// Completed, Cancelled.
// Each state carries different data:
//   - Draft: { notes?: string }
//   - Scheduled: { scheduledDate: Date; assigneeId: string }
//   - InProgress: { startedAt: Date; assigneeId: string; photos: string[] }
//   - Completed: { startedAt: Date; completedAt: Date; assigneeId: string; 
//                   photos: string[]; signatureUrl: string }
//   - Cancelled: { cancelledAt: Date; reason: string }
//
// A) Define the discriminated union type `JobState`
// B) Write a function `transitionJob(current: JobState, action: JobAction): JobState`
//    that enforces valid state transitions at the TYPE level:
//    - Draft -> Scheduled
//    - Scheduled -> InProgress | Cancelled
//    - InProgress -> Completed | Cancelled
//    - Completed and Cancelled are terminal
//    Invalid transitions must be compile-time errors.
// C) Write an exhaustive `getJobSummary(state: JobState): string` function 
//    that uses the `never` trick to ensure all states are handled.
```

---

## Part 2 -- Next.js Architecture and Patterns (120 min)

### 2.1 Server Components, Client Boundary, and `server-only`

Build the route `/jobs` with the following requirements:

1. `app/jobs/page.tsx` must be a **Server Component** that:
   - Imports `server-only` at the top of the file
   - Fetches the initial job list using a use case from the DI container (not a Server Action)
   - Passes the data as props to the client view component
   - Uses `<Suspense>` with a skeleton fallback for the job list

2. `app/jobs/loading.tsx` -- route-level loading skeleton

3. `app/jobs/error.tsx` -- error boundary with retry capability

4. `app/jobs/not-found.tsx` -- custom 404 for invalid job routes

5. Create the client view as a Feature Sliced Design view:
   ```
   presentation/views/jobs/
   ├── components/
   │   └── organisms/
   │       └── jobs-client.component.tsx     ← 'use client', thin shell
   ├── features/
   │   ├── create-job/                        ← verb slice
   │   │   ├── hooks/use-create-job.hook.ts
   │   │   ├── components/organisms/create-job-modal.component.tsx
   │   │   └── index.ts
   │   ├── filter-jobs/                       ← verb slice
   │   │   ├── hooks/use-filter-jobs.hook.ts
   │   │   ├── components/molecules/job-filter-bar.component.tsx
   │   │   └── index.ts
   │   └── complete-job/                      ← verb slice
   │       ├── hooks/use-complete-job.hook.ts
   │       ├── components/organisms/complete-job-modal.component.tsx
   │       └── index.ts
   ├── hooks/
   │   └── use-jobs-page.hook.ts             ← orchestrates slices
   └── index.ts                              ← public API
   ```

6. **Organisms must be thin shells** -- all state and handlers live in hooks.

7. Server Actions must be used **only for mutations** (create-job, complete-job). Reads/queries must be fetched in the Server Component and passed as props, or use client-side SWR/React Query.

### 2.2 State Management with Zustand or Redux

Implement a `useJobsStore` (Zustand) or Redux slice that:

1. Manages: `jobs[]`, `selectedJobIds`, `filters`, `pagination`, `sortConfig`
2. Uses **selectors** to prevent unnecessary re-renders
3. Derives `filteredJobs` with a selector (NOT `useEffect` + `setState`)
4. Handles **optimistic updates** for job status changes (with rollback on failure)
5. Must NOT duplicate server state -- use only for client-side UI state

### 2.3 React Design Patterns

In the organisms/hooks you created, demonstrate:

1. **Controlled Component pattern** -- parent owns state, child receives value + onChange
2. **Compound Component pattern** -- for the filter bar (e.g., `<FilterBar><FilterBar.Status /><FilterBar.DateRange /></FilterBar>`)
3. **useReducer** for the create-job form (multiple related fields that change together)
4. **useMemo** for derived state (e.g., computing totals, filtering)
5. **Error boundaries** -- wrap the job list in an ErrorBoundary component
6. **Ternary for conditional rendering** (not `&&`)

---

## Part 3 -- .NET Modular Monolith with DDD (120 min)

### 3.1 Domain Layer -- Aggregates, Entities, Value Objects

Inside the `Jobs` module, implement:

1. **Aggregate Root:** `Job`
   - Properties: `Id`, `Title`, `Description`, `Address` (Value Object), `Status` (enum), `ScheduledDate`, `AssigneeId`, `CustomerId`, `OrganizationId` (tenant)
   - Invariants:
     - A Job cannot be scheduled in the past
     - A Job cannot transition from Completed/Cancelled to any other state
     - Only Scheduled jobs can move to InProgress
   - Must raise domain events: `JobCreatedDomainEvent`, `JobCompletedDomainEvent`, `JobCancelledDomainEvent`

2. **Value Object:** `Address`
   - Properties: `Street`, `City`, `State`, `ZipCode`, `Latitude`, `Longitude`
   - Equality is structural (not by reference)
   - Must extend the shared `ValueObject` base class

3. **Entity:** `JobPhoto`
   - Belongs to a `Job` aggregate
   - Properties: `Id`, `Url`, `CapturedAt`, `Caption`
   - Cannot be accessed directly -- only through the aggregate root

4. **Domain Events:**
   - `JobCreatedDomainEvent` -- triggers notification to assigned crew
   - `JobCompletedDomainEvent` -- triggers invoice generation and customer notification

### 3.2 Application Layer -- CQRS with MediatR

Implement the following use cases:

1. **Command:** `CreateJobCommand` + `CreateJobCommandHandler`
   - Validates input with FluentValidation
   - Uses the repository to persist
   - Returns `Result<Guid>` (not exceptions)

2. **Command:** `CompleteJobCommand` + `CompleteJobCommandHandler`
   - Validates state transition
   - Raises `JobCompletedDomainEvent`
   - Returns `Result<Unit>`

3. **Query:** `SearchJobsQuery` + `SearchJobsQueryHandler`
   - Supports pagination, filtering by status, date range, assignee
   - Returns `Result<PagedList<JobResponse>>`
   - Uses **read-optimized** query (no tracking, projections)

4. Naming conventions:
   - Commands: `sealed`, end with `Command`
   - CommandHandlers: `internal sealed`, end with `CommandHandler`
   - Queries: `sealed`, end with `Query`
   - QueryHandlers: `internal sealed`, end with `QueryHandler`
   - Validators: `internal sealed`, end with `Validator`

### 3.3 Infrastructure Layer -- Repository and Unit of Work

1. **Repository:** `IJobRepository` (domain) + `JobRepository` (infrastructure)
   - The domain interface defines: `GetByIdAsync`, `AddAsync`, `SearchAsync`
   - The infrastructure implements with EF Core
   - Use partial classes to split large repositories by responsibility

2. **Unit of Work:**
   - `SaveChangesAsync()` persists changes + dispatches domain events via outbox
   - Domain events are intercepted before save (`InsertOutboxMessagesInterceptor`)

3. **EF Core Configuration:**
   - Use dedicated `jobs` schema for data isolation
   - Configure `Address` as an owned type
   - Store enums as strings
   - snake_case column naming

### 3.4 Async Processing -- Hangfire + Outbox

1. When `JobCompletedDomainEvent` is published:
   - An **integration event** `JobCompletedIntegrationEvent` is created in the `Jobs.IntegrationEvents` project
   - The outbox interceptor persists it in the same transaction
   - A Hangfire background job polls the outbox and dispatches:
     - **Invoice generation** (another bounded context -- Billing module)
     - **Customer notification** (email via SendGrid)

2. Explain (in comments or a brief markdown section):
   - Why use domain events WITHIN the module vs integration events ACROSS modules
   - Why the outbox pattern ensures **at-least-once** delivery
   - How **idempotency** is guaranteed in the invoice handler (idempotency key based on JobId + CompletedAt)

---

## Part 4 -- Database Design with PostgreSQL (60 min)

### 4.1 Schema Design

Design the `jobs` schema with these tables:

```sql
-- Create the schema and tables for the Jobs module.
-- Requirements:
-- 1. jobs table: id (UUID, PK), title, description, status (text enum), 
--    street, city, state, zip_code, latitude, longitude (owned value object),
--    scheduled_date, assignee_id (FK), customer_id (FK), organization_id (tenant),
--    created_at, updated_at
-- 2. job_photos table: id (UUID, PK), job_id (FK), url, captured_at, caption
-- 3. outbox_messages table: id, type, content (jsonb), occurred_on, processed_on
-- 4. Add appropriate indexes for: 
--    - Multi-tenant queries (organization_id)
--    - Status-based filtering
--    - Date range queries
--    - Full-text search on title + description
-- 5. Explain your normalization decisions vs denormalization trade-offs 
--    (when would you denormalize and why)
```

### 4.2 Query Optimization

Write an optimized query for:

```sql
-- Search jobs for a specific tenant with:
-- - Full-text search on title + description
-- - Filter by status (multiple statuses)
-- - Filter by date range
-- - Pagination (cursor-based, not OFFSET)
-- - Include photo count per job
-- Explain your indexing strategy and why cursor-based pagination 
-- is preferred over OFFSET for large datasets.
```

### 4.3 Denormalization vs Integration Events

Write a brief analysis (200-300 words):
- When would you **denormalize** the customer name into the jobs table vs. joining from the Contacts module?
- When would you use **integration events** to sync data across bounded contexts instead of denormalization?
- What are the consistency trade-offs of each approach?

---

## Part 5 -- Testing (90 min)

### 5.1 Unit Tests (Jest -- Frontend)

Write tests for:

1. **`useCreateJob` hook** -- test the reducer, validation logic, and server action call
2. **`JobState` transitions** -- test all valid/invalid state transitions
3. **`DeepReadonly<T>`** -- compile-time type tests using `expectTypeOf` (from `vitest`)
4. **`useJobsStore`** -- test selectors, optimistic updates, and rollback

### 5.2 Unit Tests (.NET -- Backend)

Write tests for:

1. **`Job` aggregate** -- test invariant enforcement (cannot schedule in past, valid transitions)
2. **`CreateJobCommandHandler`** -- mock repository, verify domain event raised
3. **`Address` value object** -- test structural equality
4. **Architecture tests** (NetArchTest) -- verify naming conventions, layer dependencies

### 5.3 E2E Tests (Playwright)

Write a Playwright test that:

1. Navigates to `/jobs`
2. Creates a new job using the modal
3. Verifies the job appears in the table
4. Filters by status
5. Completes the job
6. Verifies the status changes to "Completed"

Requirements:
- Use Page Object Model pattern
- Use `data-testid` attributes for selectors
- Handle loading states and async operations
- Take screenshots on failure

---

## Part 6 -- System Design (60 min)

### 6.1 Architecture Diagram

Draw (ASCII or any diagramming tool) the complete system architecture showing:

1. **Frontend:** Next.js App Router layers (Server Components -> Client Components -> Zustand/Redux)
2. **Backend:** Modular Monolith layers (API -> Application -> Domain -> Infrastructure)
3. **Async pipeline:** Outbox -> Hangfire/RabbitMQ -> Invoice generation + Notifications
4. **Database:** PostgreSQL with schema-per-module isolation
5. **Cross-cutting:** Authentication, multi-tenancy, error handling

### 6.2 Design Principles Analysis

For each principle below, provide a **concrete example** from the code you wrote and explain how it applies:

1. **SOLID** -- one example per principle (S, O, L, I, D) from your implementation
2. **GRASP** -- Information Expert, Creator, Controller, Low Coupling, High Cohesion
3. **Idempotency** -- where and how you ensured idempotent operations
4. **Eventual Consistency** -- how domain events + outbox achieve eventual consistency
5. **Bounded Context** -- how Jobs and Billing modules communicate without tight coupling
6. **Open Host Service (OHS)** -- how the Jobs module exposes public contracts for other modules

### 6.3 GoF Design Patterns

Identify and explain **at least 5** GoF patterns present in your solution:

| Pattern | Where Used | Problem Solved |
|---------|-----------|---------------|
| (example) Repository | `IJobRepository` + `JobRepository` | Abstracts persistence, enables testing with mocks |
| ... | ... | ... |

Candidates to consider: Repository, Unit of Work, Observer (domain events), Builder (QueryBuilder), Strategy (validation), Factory (aggregate creation), Mediator (MediatR), Template Method (base repository), State (job state machine), Command (CQRS commands).

---

## Deliverables

1. **Frontend project** -- Next.js with TypeScript (strict mode), all files under the FSD structure
2. **Backend project** -- .NET 9 solution with the Jobs module following Clean Architecture
3. **SQL file** -- Schema, migrations, and optimized queries
4. **Test files** -- Jest, Playwright, xUnit
5. **Architecture diagram** -- any format (ASCII, draw.io, Excalidraw)
6. **README.md** -- setup instructions, architectural decisions, and assumptions

---

---

# RUBRIC

> **This rubric is provided to the candidate.** Each section is scored independently. The total is **100 points**. A passing score is **70/100** with no section scoring below 40% of its weight.

---

## Section 1: TypeScript Mastery (15 points)

| Criteria | Excellent (100%) | Good (75%) | Acceptable (50%) | Insufficient (<50%) |
|----------|-----------------|-----------|------------------|---------------------|
| **DeepReadonly + PathKeys** (5 pts) | Handles all cases (nested objects, arrays, Maps, Sets, tuples). Compiles with no `any`. Recursive depth is bounded. | Handles objects and arrays. Minor gap (e.g., no Map/Set support). | Handles flat objects only. Uses `any` in 1-2 places. | Does not compile or uses `any` extensively. |
| **Type-Safe Builder** (5 pts) | Full chaining with narrowing at each step. No unsafe casts. `.where()` validates field type. Template literal query string. | Chaining works, narrowing mostly correct. Minor gap in `.orderBy()` validation. | Builder works but no field narrowing. Uses `as` casts. | Builder does not chain or loses type info. |
| **State Machine Types** (5 pts) | All transitions enforced at compile-time. `never` exhaustiveness check. Clean discriminated unions. | Transitions compile-checked for most cases. Exhaustiveness present. | Runtime-only transition checks. Types are present but not discriminated. | No discriminated unions. Transition logic has type holes. |

---

## Section 2: Next.js Architecture (20 points)

| Criteria | Excellent (100%) | Good (75%) | Acceptable (50%) | Insufficient (<50%) |
|----------|-----------------|-----------|------------------|---------------------|
| **Server/Client boundary** (5 pts) | `page.tsx` is Server Component with `server-only`. `'use client'` only on leaf components. Data fetched server-side, passed as props. | Correct boundary but missing `server-only` import. | `page.tsx` is server but fetches via Server Actions for reads. | `page.tsx` has `'use client'`. |
| **FSD structure** (5 pts) | Perfect slice anatomy: verb names, barrel exports, no cross-slice imports, organisms are thin shells, all state in hooks. | Structure mostly correct. 1-2 slices have non-verb names or minor import violations. | FSD folders exist but organisms own state, imports bypass `index.ts`. | No FSD structure. Components are unorganized. |
| **Error/Loading/Suspense** (5 pts) | `error.tsx`, `loading.tsx`, `not-found.tsx` present. `<Suspense>` with skeleton fallback for async sections. Error boundary with retry. | All three files present. Suspense used but skeleton is basic. | Only `error.tsx` present. No Suspense boundaries. | No error handling files. |
| **State management** (5 pts) | Zustand/Redux with typed selectors, optimistic updates with rollback, derived state via selectors (not useEffect). No server state duplication. | Store works, selectors present, optimistic updates functional but rollback incomplete. | Store works but no selectors, uses `useEffect` for derived state. | No store or Redux misuse (e.g., storing API responses in Redux). |

---

## Section 3: .NET Modular Monolith + DDD (25 points)

| Criteria | Excellent (100%) | Good (75%) | Acceptable (50%) | Insufficient (<50%) |
|----------|-----------------|-----------|------------------|---------------------|
| **Aggregate Design** (7 pts) | `Job` enforces all invariants in domain methods. State changes raise domain events. `Address` is proper Value Object with structural equality. `JobPhoto` only accessible via aggregate root. No anemic domain model. | Invariants enforced. Value Object present but missing equality override. Photos accessible directly. | Job has basic validation. No domain events from aggregate. Address is a DTO, not a Value Object. | Anemic model (public setters, no invariants). |
| **CQRS + MediatR** (6 pts) | Commands/Queries correctly separated. Naming conventions followed (`sealed`, `internal sealed`). Result pattern for errors. FluentValidation wired. Handlers are focused (single responsibility). | CQRS present. Minor naming deviation. Result pattern used. | Commands exist but queries also use commands. No validation. | No CQRS separation. Exceptions for flow control. |
| **Repository + UoW** (5 pts) | Domain interface + infra implementation. Partial classes for large repos. Unit of Work with domain event dispatch. Proper DI registration. | Pattern correct. No partial classes. UoW present. | Repository exists but no interface/implementation split. | Direct DbContext usage in handlers. |
| **Outbox + Hangfire** (4 pts) | Outbox persisted in same transaction. Hangfire polls and dispatches. Integration events separated from domain events. Idempotency key on handlers. | Outbox works. Integration events present. Missing idempotency. | Outbox exists but no background processing. | No outbox. Events dispatched in-process only. |
| **Clean Architecture layers** (3 pts) | Four layers with correct dependency direction. No inner layer referencing outer. `IntegrationEvents` project for public contracts. Architecture tests enforce conventions. | Layers present. 1 dependency violation. | Layers exist but domain references infrastructure. | No layer separation. |

---

## Section 4: PostgreSQL (10 points)

| Criteria | Excellent (100%) | Good (75%) | Acceptable (50%) | Insufficient (<50%) |
|----------|-----------------|-----------|------------------|---------------------|
| **Schema design** (4 pts) | Proper normalization. Value Object mapped as owned columns. Multi-tenant column. Correct FK relationships. UUID primary keys. | Schema correct. Minor: no tenant column or missing FK. | Tables exist but no constraints, no indexes. | Schema incomplete or incorrect types. |
| **Indexing + optimization** (3 pts) | Composite indexes for tenant + status. GIN index for full-text search. Cursor-based pagination. Explains why cursor > OFFSET. | Indexes present. Uses OFFSET but explains trade-offs. | Basic B-tree indexes. No full-text search. | No indexes. |
| **Normalization analysis** (3 pts) | Clear explanation of when to denormalize (read-heavy, cross-module joins). Integration events for eventual consistency. Trade-off analysis is concrete (latency vs consistency). | Good analysis. Missing one trade-off. | Brief analysis. Does not mention integration events. | No analysis provided. |

---

## Section 5: Testing (15 points)

| Criteria | Excellent (100%) | Good (75%) | Acceptable (50%) | Insufficient (<50%) |
|----------|-----------------|-----------|------------------|---------------------|
| **Frontend unit tests** (5 pts) | Hook tests with proper act() wrapping. Type-level tests. Store tests cover optimistic updates + rollback. Mocks are typed. >80% branch coverage. | Tests pass. Some branches missed. Mocks use `any` in 1-2 places. | Tests exist but are shallow (happy path only). | No tests or tests don't run. |
| **Backend unit tests** (5 pts) | Aggregate invariant tests. Handler tests with mocked dependencies (Moq). Architecture tests (NetArchTest). FluentAssertions used. | Tests cover main paths. Architecture tests present. | Happy path only. No architecture tests. | No tests. |
| **E2E Playwright** (5 pts) | Page Object Model. `data-testid` selectors. Handles async/loading. Screenshots on failure. Covers create + filter + complete flow. | POM used. Full flow covered. Missing screenshot on failure. | E2E test exists but uses fragile selectors (CSS/text). | No E2E test. |

---

## Section 6: System Design and Principles (15 points)

| Criteria | Excellent (100%) | Good (75%) | Acceptable (50%) | Insufficient (<50%) |
|----------|-----------------|-----------|------------------|---------------------|
| **Architecture diagram** (3 pts) | Complete diagram: FE layers, BE layers, async pipeline (outbox -> queue -> handlers), DB schemas, cross-cutting. Clear, readable. | Diagram covers most layers. Missing async pipeline detail. | Basic box diagram. Missing important layers. | No diagram. |
| **SOLID + GRASP** (5 pts) | Concrete code examples for ALL 5 SOLID principles AND at least 3 GRASP principles. Examples are from the actual solution, not textbook definitions. | Examples for 4/5 SOLID + 2 GRASP. Concrete but brief. | Examples for 3/5 SOLID. No GRASP. Generic explanations. | Fewer than 3 principles with examples. |
| **GoF patterns** (4 pts) | 5+ patterns identified with correct names, locations, and problems solved. Patterns are genuinely applied (not forced). | 4 patterns correctly identified. | 3 patterns. 1 is incorrectly classified. | Fewer than 3 or mostly incorrect. |
| **DDD concepts** (3 pts) | Explains bounded contexts, OHS/public contracts, domain vs integration events, eventual consistency, and idempotency with concrete examples. | 4/5 concepts explained. | 3/5 concepts. Explanations are vague. | Fewer than 3 concepts. |

---

## Scoring Summary

| Section | Weight | Your Score |
|---------|--------|-----------|
| 1. TypeScript Mastery | 15 pts | ___ / 15 |
| 2. Next.js Architecture | 20 pts | ___ / 20 |
| 3. .NET Modular Monolith + DDD | 25 pts | ___ / 25 |
| 4. PostgreSQL | 10 pts | ___ / 10 |
| 5. Testing | 15 pts | ___ / 15 |
| 6. System Design and Principles | 15 pts | ___ / 15 |
| **TOTAL** | **100 pts** | **___ / 100** |

### Passing Criteria

- **Minimum total:** 70 / 100
- **No section below 40%** of its weight (e.g., Section 3 must be at least 10/25)
- **Code must compile and run** -- partial implementations are accepted but must not break the build
- **Tests must pass** -- failing tests count against the Testing section

### Bonus Points (up to +10)

| Bonus | Points | Criteria |
|-------|--------|---------|
| CI/CD pipeline | +3 | Docker Compose for local dev + GitHub Actions for CI (lint, test, build) |
| OpenTelemetry | +2 | Distributed tracing across frontend and backend |
| Rate limiting | +2 | API rate limiting with sliding window algorithm |
| Accessibility | +1 | ARIA labels, keyboard navigation, screen reader tested |
| Performance | +2 | Lighthouse score >90, React Profiler evidence of no unnecessary re-renders |

---

## Submission Guidelines

1. Submit as a **GitHub repository** (or zip file) with separate directories for frontend and backend
2. Include a `docker-compose.yml` that runs the full stack (PostgreSQL + Backend + Frontend)
3. Include a `README.md` with:
   - Setup instructions
   - Architectural decisions and trade-offs
   - What you would improve given more time
4. Commit history should reflect incremental progress (not a single squashed commit)
