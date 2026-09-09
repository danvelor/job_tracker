# JobTracker

Multi-tenant job management for roofing contractors. Next.js 15 frontend, .NET 9
modular monolith backend, PostgreSQL. Built as a technical assessment.

## Context documents

Read the relevant one before working in that area. They are authoritative; this
file is only the summary that is always loaded.

| Document | Holds |
|---|---|
| `context/prd.md` | What the product does. Requirements `FR-*`, rules `BR-*`, non-functionals `NFR-*` |
| `context/architecture.md` | Structure, boundaries, rationale. **Section 9 is the binding rule set.** Section 11 is the decision log |
| `context/design.md` | Concrete artefacts. Part A interface, Part B types, store, classes, API, DDL |
| `context/request/` | The original assessment |

When something is unspecified, check the decision log (architecture section 11)
before deciding it again.

## Working rules

**Language.** Conversation is in Spanish. Everything written to a file is in
English — code, identifiers, comments, commit messages, documentation, test
names, diagram labels.

**Commits.** Never run `git commit` without being asked for that commit. Never
put Claude or Anthropic attribution in a commit message, a PR body, or a
`Co-Authored-By` trailer. Author stays `Daniel Velez <danvelor@gmail.com>` via
this repository's local config.

**Pushing** is the user's action: GitHub authentication here is interactive.

## Non-negotiables

1. **The build stays green.** A partial implementation is acceptable; a broken
   build is not. Never leave the tree in a state that does not compile.
2. **Tests pass.** A failing test is worse than an absent one. The red phase of
   TDD is the exception that proves it: red lives inside the cycle and is never
   committed. What must be green is every commit, not every moment.
3. **Development is test-driven.** No production code without a test that was
   watched failing first (architecture 8.3, D-28). Scaffolding and configuration
   are exempt under the two conditions in 8.3; the licence pins are not.
4. **No `any`, no `as unknown as X`** in TypeScript. No public setters on a
   .NET aggregate.

## Backend conventions

Enforced by `JobTracker.ArchitectureTests`. Full detail in architecture 9.1-9.2.

| Element | Modifiers and suffix |
|---|---|
| Command | `public sealed`, `…Command` |
| Command handler | `internal sealed`, `…CommandHandler` |
| Query | `public sealed`, `…Query` |
| Query handler | `internal sealed`, `…QueryHandler` |
| Validator | `internal sealed`, `…Validator` |
| Domain event | `public sealed record`, `…DomainEvent`, in `Domain` |
| Integration event | `public sealed record`, `…IntegrationEvent`, primitives only, in `IntegrationEvents` |
| Repository | `I{Aggregate}Repository` in `Domain`; `internal sealed partial` in `Infrastructure` |
| EF configuration | `internal sealed`, `IEntityTypeConfiguration<T>` |

Structural rules:

- `Domain` references only `Common.Domain`. `Application` never references
  `Infrastructure`. No inward reference to an outer layer
- A module references another module **only** through its `IntegrationEvents`
- Handlers return `Result` / `Result<T>`. Exceptions are for defects, not for
  expected failures
- Aggregate state changes go through intention-named methods, never a setter
- The current time is a parameter, never read inside a domain method
- Read queries use projections and `AsNoTracking`

## Frontend conventions

Enforced by ESLint and `tsc`. Full detail in architecture 9.3.

- File names are kebab-case with a role suffix: `*.component.tsx`, `*.hook.ts`,
  `*.store.ts`, `*.adapter.ts`, `*.action.ts`, `*.type.ts`
- Feature slices are named as **verbs** (`create-job`, not `job-form`)
- **No slice imports another slice.** Shared code moves up; cross-slice
  communication goes through the typed event emitter
- Every import into a slice goes through its `index.ts`, and no slice imports
  the view's barrel or anything above itself. `import/no-cycle` enforces both —
  see architecture 9.4
- The cross-slice bus is one-way: slices emit, the view and the store react. A
  subscriber never emits in response to what it received
- **Organisms are thin shells:** no `useState`, no handler bodies. State lives in
  the slice hook
- Conditional rendering uses a ternary, never `&&`
- Server Actions are only for mutations, and live inside their slice
- `core/` imports nothing from React, Next or any adapter
- Every element the end-to-end suite touches carries a `data-testid` from the
  contract in design A8
- Server state belongs to SWR. The Zustand store holds **no** `jobs` array

## Commands

Projects are scaffolded incrementally; a command exists once its project does.

```bash
# Backend
dotnet build backend/JobTracker.sln
dotnet test  backend/JobTracker.sln

# Frontend
npm --prefix frontend run lint
npm --prefix frontend run typecheck    # tsc --noEmit — the gate for type-level tests
npm --prefix frontend test             # Jest
npm --prefix frontend run test:e2e     # Playwright, against the in-memory adapter

# Full stack
docker compose up --build              # postgres + mailhog + backend + frontend
```

`npm run typecheck` is not optional: `expect-type` assertions fail at compile
time, so a broken type test leaves Jest green.

## Traps in the assessment

Already resolved — see architecture section 11. Do not re-derive them.

| Trap | Resolution |
|---|---|
| `transitionJob(current: JobState, …)` can never fail at compile time | Generic signature constrained by the narrowed state (D-10) |
| `await` in `page.tsx` makes `<Suspense>` decorative | Pass the unresolved promise, unwrap with `use()` (D-11) |
| Store must hold `jobs[]` **and** not duplicate server state | SWR owns rows; Zustand holds UI state and an optimistic overlay (D-05) |
| The outbox interceptor cannot persist an integration event | The outbox stores domain events; a handler translates downstream (D-13) |
| The end-to-end flow completes a job without starting it | The walkthrough adds the start step (D-14) |
| MediatR v13+ needs a commercial licence | Pinned to 12.x (D-09) |
