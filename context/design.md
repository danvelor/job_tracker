# JobTracker — Design

| | |
|---|---|
| **Document** | Concrete artefacts: what the interface looks like, and the shapes the code takes |
| **Companions** | `context/prd.md` (what it does), `context/architecture.md` (how it is structured and the rules) |
| **Split rule** | Architecture holds strategy and rules; this document holds the artefact |

Two parts. **Part A** designs the interface. **Part B** fixes the concrete
shapes — file inventory, type signatures, store contents, class members, API and
DDL. Part A is the source both the components and the Playwright page objects
derive from; Part B is the source the implementation derives from.

---

# Part A — Interface design

## A1. Screen inventory

| Screen | Route | Purpose |
|---|---|---|
| Job list | `/jobs` | The working surface. Search, filter, page, and act on jobs |
| Job detail | `/jobs/[id]` | A single job with its photos and history. Exists so that `not-found.tsx` has a route that can genuinely 404 (line 110) |
| Create job | modal over `/jobs` | `FR-1` |
| Complete job | modal over `/jobs` | `FR-4` |

Starting a job (`FR-3`) and cancelling one (`FR-5`) are inline row actions with
no modal. Starting carries no data beyond intent, and cancelling collects only a
reason, which fits in a small inline field. Deliberately **no browser
`confirm()`** anywhere: a modal dialog blocks the page and would freeze an
automated run.

## A2. Job list layout

```
┌────────────────────────────────────────────────────────────────────────────┐
│  JobTracker                                            Acme Roofing        │
├────────────────────────────────────────────────────────────────────────────┤
│                                                                            │
│  Jobs                                                    [ + New job ]     │
│                                                                            │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │ ⌕ Search title or description   │ Status ▾ │ From ▭  To ▭ │ Crew ▾  │   │
│  │                                                          [ Clear ]  │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│                                                                            │
│  Showing 24 jobs · 2 selected                                              │
│                                                                            │
│  ┌────┬────────────────┬──────────────┬───────────┬──────────┬──────────┐  │
│  │ ☐  │ TITLE          │ ADDRESS      │ SCHEDULED │ CREW     │ STATUS   │  │
│  ├────┼────────────────┼──────────────┼───────────┼──────────┼──────────┤  │
│  │ ☑  │ Roof repair    │ 12 Elm St    │ Mar 14    │ J. Ortiz │ Scheduled│  │
│  │    │                │ Springfield  │           │          │ ▸ Start  │  │
│  ├────┼────────────────┼──────────────┼───────────┼──────────┼──────────┤  │
│  │ ☐  │ Gutter reline  │ 8 Oak Ave    │ Mar 15    │ M. Ruiz  │ InProgres│  │
│  │    │                │ Springfield  │           │          │ ▸Complete│  │
│  ├────┼────────────────┼──────────────┼───────────┼──────────┼──────────┤  │
│  │ ☐  │ Shingle swap   │ 44 Pine Rd   │ Mar 09    │ J. Ortiz │ Completed│  │
│  └────┴────────────────┴──────────────┴───────────┴──────────┴──────────┘  │
│                                                                            │
│                            [ Load more ]                                   │
└────────────────────────────────────────────────────────────────────────────┘
```

Three deliberate choices in this layout:

**"Load more", not numbered pages.** Paging is keyset, so a page number is not a
value the system has — it would require a count and an offset, which is exactly
what `NFR-5` rejects. The control reflects the mechanism honestly.

**Row actions come from the state machine.** A row renders only the transitions
its current state permits, asked of `transitionJob` rather than hard-coded per
column. A Completed row offers nothing, because the model says so.

**The count is of loaded rows, not of matches.** "Showing 24 jobs" describes what
was fetched. A total would need a second aggregate query per keystroke, which
buys a number nobody acts on.

## A3. Atomic inventory

| Level | Component | Notes |
|---|---|---|
| Atom | `Button` | Variants: primary, secondary, ghost, danger. Owns its pending state visually, not logically |
| Atom | `TextInput`, `Textarea`, `Select`, `DateInput`, `Checkbox` | All controlled: `value` in, `onChange` out, no internal state |
| Atom | `StatusBadge` | Maps a `JobStatus` to a colour and label |
| Atom | `Spinner`, `SkeletonBlock` | |
| Atom | `Label`, `FieldError` | |
| Molecule | `FormField` | `Label` + control + `FieldError`, wired by `htmlFor` and `aria-describedby` |
| Molecule | `SearchField` | Debounced text input |
| Molecule | `StatusFilter` | Multi-select over `JobStatus` |
| Molecule | `DateRangeFilter` | Two `DateInput`s with a from ≤ to guard |
| Molecule | `AssigneeFilter` | |
| Molecule | `JobRow` | One table row, including its permitted actions. Receives them as props — see below |
| Molecule | `SelectionSummary` | Loaded count and selection count |
| Molecule | `SignaturePad` | Canvas producing a data URL. The signature `FR-4` requires |
| Molecule | `PhotoList` | Thumbnails with captions |
| Molecule | `JobFilterBar` | The compound root. Placed as a molecule to match line 125 |
| Organism | `JobsTable` | Header, rows, empty and loading states |
| Organism | `CreateJobModal` | |
| Organism | `CompleteJobModal` | |
| Organism | `JobsClient` | The `'use client'` shell. Composes the others and wires nothing itself |
| Organism | `JobsErrorBoundary` | Wraps the table (line 158) |

Every organism in this list is a **thin shell**: it receives props and renders.
Its state and handlers come from the slice hook named in Part B.

**`JobRow` is the one place where that rule prevents an import cycle**, so it is
worth stating rather than leaving implicit. `JobRow` is a shared molecule: it
lives in `presentation/components/molecules/`, below the view. The actions it
renders — Start, Complete, Cancel — belong to slices that sit *above* it and
that already import atoms from the same folder. Importing a slice to reach its
handler would close the loop `components/ → views/jobs/features/ → components/`.

It therefore takes `onStart`, `onComplete` and `onCancel` as props, supplied by
`JobsTable` from the orchestrator, and imports nothing from any slice. Which of
the three it renders is decided by `transitionJob` against the row's status
(A2), not by knowing who handles them. The same reasoning applies to every
shared molecule that offers an action: the action arrives as a prop, never as an
import. `context/architecture.md` 9.4 has the full argument and the
`import/no-cycle` rule that enforces it.

## A4. View states

Each state is a distinct rendering, not a variation of the loaded one.

| State | Trigger | Rendering |
|---|---|---|
| **Skeleton** | The jobs promise has not resolved | Five `SkeletonBlock` rows in the table body, inside the Suspense boundary |
| **Route transition** | Navigating to `/jobs` | `loading.tsx`, a page-level skeleton including the filter bar |
| **Loaded** | Rows present | The table |
| **Empty — no jobs** | Zero rows, no active filter | "No jobs yet" with the create action as the call to action |
| **Empty — no matches** | Zero rows, filter active | "No jobs match these filters" with a clear-filters action. A different message because the remedy is different |
| **Error** | The query failed | `error.tsx` with the message and a retry driven by `reset()` |
| **Render error** | A component threw | `JobsErrorBoundary` replaces the table only; filters and header survive |
| **Optimistic pending** | A status change is in flight | The row shows the target status with reduced opacity and `aria-busy`; its actions are disabled |
| **Rolled back** | The mutation failed | The prior status returns and an inline error appears on the row |
| **Loading more** | "Load more" pressed | The button shows a spinner; existing rows stay interactive |

The two empty states are separated on purpose: telling a user with active
filters that they have "no jobs yet" is wrong and sends them to create a
duplicate.

## A5. Interaction flows

### Create a job (`FR-1`)

1. `+ New job` opens `CreateJobModal`; focus moves to the title field
2. The form collects title, description, address (street, city, state, ZIP,
   latitude, longitude), scheduled date, assignee, customer
3. Field-level validation runs on blur; the submit button stays enabled so that
   pressing it reveals all remaining errors at once rather than hiding the way
   forward
4. Submitting calls the create Server Action
5. On success the modal closes, SWR revalidates, and the new row appears in
   state **Scheduled**
6. On failure the modal stays open and the error appears above the actions, with
   field errors attached to their fields

### Filter (`FR-6`)

1. Every control writes to the Zustand filter state
2. Text search is debounced by 300 ms; the other controls apply immediately
3. The filter state is part of the SWR key, so a change refetches from the
   server. The cursor resets
4. `Clear` restores the defaults in one action

### Start a job (`FR-3`)

1. `Start` appears only on Scheduled rows, because the state machine says so
2. Pressing it writes `InProgress` into the optimistic overlay and stores the
   previous status
3. The row renders `InProgress` immediately, `aria-busy`, actions disabled
4. On success the overlay clears and SWR revalidates
5. On failure the previous status returns and the row shows the error

### Complete a job (`FR-4`)

1. `Complete` appears only on InProgress rows
2. `CompleteJobModal` collects the signature via `SignaturePad` and any photos
3. Submit is refused without a signature (`BR-4`), with the reason stated
4. On submit the same optimistic path runs, targeting `Completed`
5. On success the row shows **Completed** and offers no further action
6. The invoice and the customer notification follow asynchronously. The interface
   does not wait for them and does not claim they have happened (`NFR-4`)

## A6. The compound filter bar

Line 155 asks for a compound component. The API:

```tsx
<JobFilterBar>
  <JobFilterBar.Search />
  <JobFilterBar.Status />
  <JobFilterBar.DateRange />
  <JobFilterBar.Assignee />
  <JobFilterBar.Clear />
</JobFilterBar>
```

The root owns no state either. It provides a context carrying the current filter
values and their change handlers, all sourced from `useFilterJobs`. Each child
reads that context, so adding a filter means adding a child and a field — not
threading another pair of props through the root.

What the pattern buys here specifically: the caller controls **which filters
appear and in what order** without the root knowing the set. A narrower screen
can render three of the five children with no change to the root.

## A7. Controlled component contracts

Line 154 asks for the controlled pattern. Every input atom follows one contract:

| Prop | Meaning |
|---|---|
| `value` | The current value. The component never holds it |
| `onChange` | Reports a new value to the owner. Receives the value, not the DOM event |
| `error` | Message to display. The component does not decide validity |
| `disabled`, `name`, `id` | Passed through |

`onChange` receives the value rather than the event on purpose: it keeps the
owner free of DOM detail and makes the atom trivial to drive from a test.

The consequence at the top of the tree is that no atom or molecule can hold
state the owner cannot see, which is what makes `useReducer` in the create form
authoritative rather than one of two competing sources of truth.

## A8. The `data-testid` contract

This table is the shared source for the components and the Playwright page
objects. Convention: `{area}-{element}[-{qualifier}]`, kebab-case. Row-scoped
identifiers carry the job identifier.

| Identifier | Element |
|---|---|
| `jobs-page` | Page root |
| `jobs-new-button` | Opens the create modal |
| `jobs-filter-bar` | Filter bar root |
| `filter-search-input` | Text search |
| `filter-status-select` | Status multi-select |
| `filter-status-option-{status}` | One status option |
| `filter-date-from`, `filter-date-to` | Date range |
| `filter-assignee-select` | Assignee |
| `filter-clear-button` | Clear |
| `jobs-selection-summary` | Loaded and selected counts |
| `jobs-table` | Table root |
| `jobs-table-skeleton` | Suspense fallback, and only that |
| `jobs-route-skeleton` | `loading.tsx`, the route transition. A separate id because it is a separate event: sharing one made both match a single locator during the streaming handoff |
| `jobs-empty-no-jobs` | Empty, no filter |
| `jobs-empty-no-matches` | Empty, filter active |
| `jobs-error` | Error state root |
| `jobs-error-retry` | Retry. The route boundary only: `JobsErrorBoundary` renders `jobs-error` without one, so this is what tells the two apart |
| `jobs-not-found` | `not-found.tsx`, reached through `notFound()` in `[id]/page.tsx` |
| `jobs-not-found-back` | The way back to the list from the 404 |
| `jobs-load-more` | Load more |
| `job-row-{id}` | One row |
| `job-row-{id}-title` | Title cell |
| `job-row-{id}-status` | Status badge. The assertion target for state changes |
| `job-row-{id}-select` | Row checkbox |
| `job-row-{id}-start` | Start action |
| `job-row-{id}-complete` | Complete action |
| `job-row-{id}-cancel` | Cancel action |
| `job-row-{id}-cancel-reason` | Inline reason field, required by `BR-5` |
| `job-row-{id}-error` | Row-level error after a rollback |
| `create-job-modal` | Modal root |
| `create-job-title`, `create-job-description` | Fields |
| `create-job-street`, `create-job-city`, `create-job-state`, `create-job-zip`, `create-job-latitude`, `create-job-longitude` | Address fields |
| `create-job-scheduled-date`, `create-job-assignee`, `create-job-customer` | Fields |
| `create-job-submit`, `create-job-cancel` | Actions |
| `create-job-error` | Form-level error |
| `create-job-field-error-{field}` | Field-level error |
| `complete-job-modal` | Modal root |
| `complete-job-signature` | Signature pad |
| `complete-job-photos` | Photo input |
| `complete-job-submit`, `complete-job-cancel` | Actions |
| `complete-job-error` | Form-level error |

Every asynchronous assertion in the end-to-end suite waits on one of
`job-row-{id}-status`, `jobs-table-skeleton` disappearing, or a modal root
unmounting. No test waits on a fixed timeout, and no selector uses a CSS class or
visible text.

**Steps 4 and 9 of the walkthrough have no identifier here, on purpose.** The
crew notification and the invoice never reach the interface — A5 step 6 states
that completion does not claim they happened — so there is nothing for a
`data-testid` to point at. The smoke run asserts them against Postgres instead,
polling `billing.invoices` by `job_id` and `jobs.notifications` by
`source_event_id` until they appear or a bounded timeout expires
(`context/architecture.md` 8.1). Adding a testid for them would mean putting a
claim on screen that `NFR-4` says the product must not make.

---

# Part B — Technical design

## B1. Frontend file map

```
frontend/src/
├── app/
│   ├── layout.tsx
│   └── jobs/
│       ├── page.tsx                    'server-only'. Resolves the use case, passes the promise
│       ├── loading.tsx                 route-level skeleton
│       ├── error.tsx                   'use client'. Retry via reset()
│       ├── not-found.tsx               404 for an unknown job id
│       └── [id]/page.tsx               job detail; calls notFound() when absent
│
├── core/
│   ├── domain/job/
│   │   ├── job-status.type.ts          JobStatus
│   │   ├── job-state.type.ts           JobState, JobAction, AllowedAction
│   │   ├── transition-job.ts           transitionJob, generic signature
│   │   ├── get-job-summary.ts          exhaustive, never-checked
│   │   └── index.ts
│   ├── application/
│   │   ├── ports/jobs.port.ts          JobsPort
│   │   ├── use-cases/search-jobs.use-case.ts
│   │   ├── use-cases/create-job.use-case.ts
│   │   ├── use-cases/start-job.use-case.ts
│   │   ├── use-cases/complete-job.use-case.ts
│   │   └── use-cases/cancel-job.use-case.ts
│   └── di/container.ts                 resolves the adapter from configuration
│
├── infrastructure/adapters/
│   ├── http-jobs.adapter.ts            bearer token, DTO mapping, ProblemDetails mapping
│   └── in-memory-jobs.adapter.ts       seeded array; default in dev and CI
│
├── presentation/
│   ├── components/                     shared atoms and molecules (A3)
│   │   ├── atoms/
│   │   └── molecules/
│   ├── stores/jobs-ui.store.ts         Zustand. UI state only (B3)
│   └── views/jobs/
│       ├── components/organisms/
│       │   ├── jobs-client.component.tsx        'use client' shell
│       │   ├── jobs-table.component.tsx
│       │   ├── jobs-table-skeleton.component.tsx
│       │   └── jobs-error-boundary.component.tsx
│       ├── features/
│       │   ├── create-job/
│       │   │   ├── hooks/use-create-job.hook.ts
│       │   │   ├── components/organisms/create-job-modal.component.tsx
│       │   │   ├── actions/create-job.action.ts        'use server'
│       │   │   └── index.ts
│       │   ├── filter-jobs/
│       │   │   ├── hooks/use-filter-jobs.hook.ts
│       │   │   ├── components/molecules/job-filter-bar.component.tsx
│       │   │   └── index.ts
│       │   ├── complete-job/
│       │   │   ├── hooks/use-complete-job.hook.ts
│       │   │   ├── components/organisms/complete-job-modal.component.tsx
│       │   │   ├── actions/complete-job.action.ts      'use server'
│       │   │   └── index.ts
│       │   ├── start-job/                              addition, FR-3
│       │   │   ├── hooks/use-start-job.hook.ts
│       │   │   ├── actions/start-job.action.ts         'use server'
│       │   │   └── index.ts                            no component: row action
│       │   └── cancel-job/                             addition, FR-5
│       │       ├── hooks/use-cancel-job.hook.ts
│       │       ├── components/molecules/cancel-reason-field.component.tsx
│       │       ├── actions/cancel-job.action.ts        'use server'
│       │       └── index.ts
│       ├── hooks/use-jobs-page.hook.ts              orchestrates the slices
│       └── index.ts                                 public API of the view
│
└── shared/
    ├── types/deep-readonly.type.ts
    ├── types/path-keys.type.ts
    ├── query-builder/query-builder.ts
    └── events/typed-event-emitter.ts
```

The structure under `presentation/views/jobs/` follows lines 114-133 exactly.
Additions beyond it — `jobs-table`, the skeleton, the error boundary, the
`actions/` folders and two extra slices — are placed inside the structure the
assessment dictates rather than beside it.

Server Actions live inside their slice because they belong to the verb. A slice
with a mutation owns the whole path from the click to the API call.

**Why `start-job` and `cancel-job` exist.** The assessment names three slices and
two mutations (line 138: "create-job, complete-job"), because it never
anticipated a start step. `BR-3` forbids Scheduled → Completed, so the acceptance
walkthrough needs one (`context/prd.md` §8 step 6, D-14), and `FR-5` needs a
cancel. They are additions, and the rule above decides their shape: a mutation
belongs to a slice, so putting them in `use-jobs-page.hook.ts` would give the
orchestrator handler bodies and contradict both B4 and `context/architecture.md`
5.6. `start-job` carries no component because starting collects no data — the
button lives on `JobRow` and receives `onStart` as a prop (A3).

## B2. Type contracts

### Job state machine (lines 68-89)

```ts
type JobStatus = 'Draft' | 'Scheduled' | 'InProgress' | 'Completed' | 'Cancelled';

type JobState =
  | { readonly status: 'Draft';      readonly notes?: string }
  | { readonly status: 'Scheduled';  readonly scheduledDate: Date; readonly assigneeId: string }
  | { readonly status: 'InProgress'; readonly startedAt: Date; readonly assigneeId: string;
                                     readonly photos: readonly string[] }
  | { readonly status: 'Completed';  readonly startedAt: Date; readonly completedAt: Date;
                                     readonly assigneeId: string; readonly photos: readonly string[];
                                     readonly signatureUrl: string }
  | { readonly status: 'Cancelled';  readonly cancelledAt: Date; readonly reason: string };

type JobAction =
  | { readonly type: 'SCHEDULE'; readonly scheduledDate: Date; readonly assigneeId: string }
  | { readonly type: 'START';    readonly startedAt: Date }
  | { readonly type: 'COMPLETE'; readonly completedAt: Date; readonly signatureUrl: string }
  | { readonly type: 'CANCEL';   readonly cancelledAt: Date; readonly reason: string };
```

The transition table lives in the type system, not in a `switch`:

```ts
type AllowedAction = {
  Draft:      'SCHEDULE';
  Scheduled:  'START' | 'CANCEL';
  InProgress: 'COMPLETE' | 'CANCEL';
  Completed:  never;     // terminal
  Cancelled:  never;     // terminal
};

type TransitionTarget = {
  SCHEDULE: 'Scheduled';
  START:    'InProgress';
  COMPLETE: 'Completed';
  CANCEL:   'Cancelled';
};

type ActionFor<S extends JobState> =
  Extract<JobAction, { type: AllowedAction[S['status']] }>;

type ResultOf<A extends JobAction> =
  Extract<JobState, { status: TransitionTarget[A['type']] }>;

function transitionJob<S extends JobState, A extends ActionFor<S>>(
  current: S,
  action: A,
): ResultOf<A>;
```

**Why this signature rather than the one in line 81.** `transitionJob(current: JobState, …)`
accepts the entire union, so every state is assignable and no call can ever be a
compile error — it cannot satisfy line 87. Constraining `A` by the *narrowed*
type of `current` is what makes an invalid pair unrepresentable. For a terminal
state, `AllowedAction[S['status']]` is `never`, so `ActionFor<S>` is `never` and
no argument can satisfy `A`: the call fails to compile, which is exactly what
line 86 asks for.

The consequence, stated plainly: the caller must narrow before calling. Given a
value of type `JobState`, one must discriminate on `status` first. That is
correct behaviour, not a limitation.

`getJobSummary` switches on `status` and closes with a `default` branch that
assigns the value to `never`, so adding a state breaks the build at that line.

### Type-level utilities (lines 24-34)

```ts
type Primitive = string | number | boolean | bigint | symbol | null | undefined;
type Prev = [never, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9];

type DeepReadonly<T, D extends Prev[number] = 9> =
  [D] extends [never]                       ? T
  : T extends Primitive                     ? T
  : T extends (...args: never[]) => unknown ? T
  : T extends Date                          ? T
  : T extends Map<infer K, infer V>         ? ReadonlyMap<K, DeepReadonly<V, Prev[D]>>
  : T extends Set<infer V>                  ? ReadonlySet<DeepReadonly<V, Prev[D]>>
  : T extends readonly [unknown, ...unknown[]]
      ? { readonly [I in keyof T]: DeepReadonly<T[I], Prev[D]> }
  : T extends ReadonlyArray<infer I>        ? ReadonlyArray<DeepReadonly<I, Prev[D]>>
  : { readonly [K in keyof T]: DeepReadonly<T[K], Prev[D]> };
```

`D` bounds the recursion, which the rubric asks for explicitly. The tuple case
precedes the array case because a tuple also matches `ReadonlyArray` and would
otherwise lose its positional types. `Date` and functions are treated as leaves;
freezing their members has no meaning.

```ts
type Leaf = Primitive | Date | ReadonlyArray<unknown>
          | Map<unknown, unknown> | Set<unknown> | ((...args: never[]) => unknown);

type PathKeys<T, D extends Prev[number] = 9> =
  [D] extends [never] ? never : {
    [K in keyof T & string]:
      T[K] extends Leaf ? K : `${K}.${PathKeys<T[K], Prev[D]>}`;
  }[keyof T & string];
```

Leaf paths only, per the example in line 33: `PathKeys<{a:{b:string;c:{d:number}}}>`
yields `"a.b" | "a.c.d"` and not the intermediate `"a"`.

`D` bounds the recursion for the same reason `DeepReadonly` does, reusing the
same `Prev` tuple. Without it a self-referential type makes the compiler give up
with *"Type instantiation is excessively deep"* instead of producing a union —
and the two utilities sitting side by side with only one of them bounded is the
kind of asymmetry that reads as an oversight.

### Typed event emitter (lines 36-42)

```ts
type EventMap = Record<string, unknown>;

interface TypedEventEmitter<E extends EventMap> {
  on<K extends keyof E>(event: K, handler: (payload: E[K]) => void): void;
  off<K extends keyof E>(event: K, handler: (payload: E[K]) => void): void;
  emit<K extends keyof E>(event: K, payload: E[K]): void;
}

function createTypedEventEmitter<E extends EventMap>(): TypedEventEmitter<E>;
```

The implementation stores handlers in a **mapped type**, not a flat `Map`:

```ts
type HandlerStore<E extends EventMap> = {
  [K in keyof E]?: Set<(payload: E[K]) => void>;
};
```

That is what removes the need for a cast. Indexing a mapped type with a generic
`K extends keyof E` preserves the link between the key and its payload, whereas
`Map<keyof E, Set<(p: E[keyof E]) => void>>` collapses the payload to a union and
forces an assertion on retrieval. The prohibition in line 42 is satisfiable only
by choosing the right storage shape.

The application's event map, used as the cross-slice bus (architecture 5.7):

```ts
type JobsEventMap = {
  'job:status-changed': { jobId: string; status: JobStatus };
  'job:rollback':       { jobId: string; previous: JobStatus; error: string };
  'jobs:invalidate':    void;
};
```

### Query builder (lines 47-63)

```ts
type ComparisonOperator = 'eq' | 'neq' | 'gt' | 'gte' | 'lt' | 'lte' | 'like' | 'in';

declare class QueryBuilder<
  T,
  Selected extends keyof T = keyof T,
  Query extends string = '',
> {
  select<K extends keyof T & string>(
    ...fields: readonly [K, ...K[]]
  ): QueryBuilder<T, K, `SELECT ${string}`>;

  where<K extends Selected & string, Op extends ComparisonOperator>(
    field: K, operator: Op, value: T[K],
  ): QueryBuilder<T, Selected, `${Query} WHERE ${K} ${Op}`>;

  orderBy<K extends Selected & string, Dir extends 'asc' | 'desc'>(
    field: K, direction: Dir,
  ): QueryBuilder<T, Selected, `${Query} ORDER BY ${K} ${Uppercase<Dir>}`>;

  limit<N extends number>(count: N): QueryBuilder<T, Selected, `${Query} LIMIT ${N}`>;

  build(): { query: Query; params: readonly unknown[] };
}
```

Three things this achieves. `Selected` narrows after `select`, so `where` and
`orderBy` accept only selected fields — line 59. `value: T[K]` ties the third
argument to the field's own type — line 60. `Query` accumulates as a template
literal type — line 61. `build()` returns `Query`, which is assignable to the
`string` line 56 specifies, so both hold at once.

**`Op`, `Dir` and `N` are generic parameters, not plain ones.** Written as
`operator: ComparisonOperator`, the accumulated type would splice the whole
eight-member union into the literal and produce a union of eight strings — then
sixteen, then thirty-two down the chain. Capturing the argument's literal type is
what makes `.where('status','eq','Completed')` accumulate `"… WHERE status eq"`
and not a combinatorial fan-out. The type test asserts the exact string, so the
distinction is checked rather than assumed.

Its role in the application is stated in `context/architecture.md` section 5.7:
the compile-time validation is what is used; the rendered SQL is asserted in
tests and mirrored by `database/queries.sql`, never sent from the browser.

### Port and transfer shapes

```ts
interface JobsPort {
  search(query: JobSearchQuery):                  Promise<Result<PagedJobs, CoreError>>;
  getById(id: string):                            Promise<Result<JobDetail, CoreError>>;
  create(input: CreateJobInput):                  Promise<Result<string, CoreError>>;
  start(id: string):                              Promise<Result<void, CoreError>>;
  complete(id: string, input: CompleteJobInput):  Promise<Result<void, CoreError>>;
  cancel(id: string, reason: string):             Promise<Result<void, CoreError>>;
}

type JobSearchQuery = {
  readonly text?: string;
  readonly statuses?: readonly JobStatus[];
  readonly scheduledFrom?: string;   // ISO date
  readonly scheduledTo?: string;
  readonly assigneeId?: string;
  readonly cursor?: string | null;
  readonly limit: number;
};

type PagedJobs = { readonly items: readonly JobSummary[]; readonly nextCursor: string | null };

type JobSummary = {
  readonly id: string;
  readonly title: string;
  readonly status: JobStatus;
  readonly scheduledDate: string;
  readonly assigneeId: string;
  readonly assigneeName: string;
  readonly address: { readonly street: string; readonly city: string; readonly state: string };
  readonly photoCount: number;
};

type CoreError = { readonly code: string; readonly message: string;
                   readonly kind: 'validation' | 'not-found' | 'conflict' | 'unauthorized' | 'failure';
                   readonly fieldErrors?: Readonly<Record<string, string>> };
```

The remaining shapes the port refers to:

```ts
type Result<T, E> = { readonly ok: true; readonly value: T }
                  | { readonly ok: false; readonly error: E };

type JobDetail = JobSummary & {
  readonly description: string | null;
  readonly address: { readonly street: string; readonly city: string;
                      readonly state: string; readonly zipCode: string;
                      readonly latitude: number; readonly longitude: number };
  readonly startedAt: string | null;
  readonly completedAt: string | null;
  readonly signatureUrl: string | null;
  readonly photos: readonly { readonly id: string; readonly url: string;
                              readonly capturedAt: string;
                              readonly caption: string | null }[];
};
```

`Result` is a discriminated union mirroring the backend's, so both sides speak
the same failure vocabulary.

**`assigneeName` comes from a join, against a read-only roster the `jobs` schema
owns** (D-26). `jobs.assignees` and `jobs.customers` are seeded by migration and
have no write path: no command creates one, no endpoint mutates one. They exist
because three controls in A8 — `filter-assignee-select`, `create-job-assignee`
and `create-job-customer` — need something to offer, and because a table showing
a UUID is useless.

```ts
type Party = { readonly id: string; readonly name: string };

interface JobsPort {
  // …
  assignees(): Promise<Result<readonly Party[], CoreError>>;
  customers(): Promise<Result<readonly Party[], CoreError>>;
}
```

This is the local replica that `docs/normalization.md` describes: when a Contacts
module exists, an integration event maintains these rows instead of a seed, and
nothing else in the design moves. `customer_name` still does not sit on
`jobs.jobs` — it is joined — so the normalized position that document takes still
holds.

## B3. Store contents and selectors

```ts
type JobsUiState = DeepReadonly<{
  filters: {
    text: string;
    statuses: JobStatus[];
    scheduledFrom: string | null;
    scheduledTo: string | null;
    assigneeId: string | null;
  };
  pageSize: number;
  sortConfig: { field: JobSortField; direction: 'asc' | 'desc' };   // see D-18
  selectedJobIds: string[];
  optimisticStatus: Record<string, JobStatus>;
  rollbackSnapshot: Record<string, JobStatus>;
}>;
```

where `type JobSortField = 'scheduledDate' | 'title'`.

Note what is **absent**: there is no `jobs` array, and no cursor. Rows belong to
SWR (`context/architecture.md` D-05), and so does the record of which pages have
been loaded — `useSWRInfinite` derives each page's cursor from the one before
it, so a second copy in the store could only drift from it (D-40). What the
store owns of pagination is `pageSize`, which is a preference rather than a
position. `DeepReadonly` on the state type means a selector's consumer cannot
mutate what it was handed.

`JobSortField` is a closed union rather than `PathKeys<JobSummary>` because the
keyset cursor has to order by the same key the query does, and every sortable
field therefore needs a covering index (D-18). An open sort field would compile
and then page incorrectly.

The store is created without immer: `DeepReadonly` state and a mutating `set`
draft are contradictory, so every action returns a new object.

| Action | Effect |
|---|---|
| `setFilter(patch)` | Merges filter fields |
| `clearFilters()` | Restores defaults |
| `setPageSize(size)` | How many rows a page asks for |
| `setSort(field, direction)` | Replaces `sortConfig` |
| `toggleSelection(id)` / `clearSelection()` | Selection |
| `beginOptimistic(id, target, previous)` | Writes `optimisticStatus[id]` and `rollbackSnapshot[id]` |
| `commitOptimistic(id)` | Drops both entries; the row reverts to server ownership |
| `rollbackOptimistic(id)` | Restores from the snapshot, drops both entries |

| Selector | Returns |
|---|---|
| `selectFilters` | The filter object. The SWR key derives from it |
| `selectSortConfig`, `selectSelectedIds`, `selectCursor` | Slices, each subscribing to one field only |
| `selectIsSelected(id)` | Parameterised, so a row re-renders only when its own selection changes |
| `selectOptimisticStatus(id)` | The in-flight status for one row, or `undefined` |
| `makeVisibleJobsSelector(rows)` | A memoised selector factory: takes SWR's rows, overlays `optimisticStatus`, and attaches selection. This is `filteredJobs` in the sense line 146 requires — derived by a selector, with no `useEffect` and no second copy. It does **not** sort: ordering is server-side (D-18) |

Line 157 asks for `useMemo` on derived state, and there are exactly two places
where it is the right tool rather than decoration:

| Where | What it memoises | Why it matters |
|---|---|---|
| `useJobsPage` | `makeVisibleJobsSelector(rows)` — the factory is rebuilt only when SWR's rows change, so the selector identity is stable across unrelated store updates | Without it the factory is a new function on every render, the selector's memo is defeated, and every row re-renders when any filter changes |
| `SelectionSummary` | The loaded and selected counts, over `rows` and `selectedJobIds` | A count recomputed per keystroke on a debounced search field is the textbook case, and it is what line 157 means by "computing totals" |

Neither wraps a cheap scalar. `useMemo` around a value that costs less than the
comparison is noise, and the rubric scores the pattern being *applied*, not
sprinkled.

Every selector is consumed with a shallow equality comparator so a component
re-renders only when the slice it reads actually changes.

## B4. Hook contracts

### `useJobsPage(jobsPromise)`

The orchestrator (line 132). Unwraps the promise with `use()`, seeds SWR with it
as `fallbackData`, composes the three slice hooks, and returns exactly what
`JobsClient` renders. It holds no state of its own — it wires.

### `useCreateJob()`

`useReducer` (line 156), because the form's fields change together and its
submission has a lifecycle:

```ts
type CreateJobValues = {
  title: string; description: string;
  address: { street: string; city: string; state: string; zipCode: string;
             latitude: string; longitude: string };
  scheduledDate: string; assigneeId: string; customerId: string;
};

type CreateJobFormState = {
  values: CreateJobValues;
  errors: Partial<Record<PathKeys<CreateJobValues>, string>>;
  touched: Partial<Record<PathKeys<CreateJobValues>, boolean>>;
  status: 'idle' | 'submitting' | 'failed';
  formError: string | null;
};

type CreateJobFormAction =
  | { type: 'FIELD_CHANGED'; field: PathKeys<CreateJobValues>; value: string }
  | { type: 'FIELD_BLURRED'; field: PathKeys<CreateJobValues> }
  | { type: 'SUBMIT_STARTED' }
  | { type: 'SUBMIT_FAILED'; formError: string;
      fieldErrors?: Partial<Record<PathKeys<CreateJobValues>, string>> }
  | { type: 'SUBMIT_SUCCEEDED' }
  | { type: 'RESET' };
```

`PathKeys<CreateJobValues>` types every field address, so `address.zipCode` is
valid and `address.zip` does not compile. Validation is a pure function from
values to errors, called by the reducer — which keeps it testable with no React
at all.

### `useFilterJobs()`

Reads and writes the filter slice, debounces text by 300 ms, and produces the
context value the compound bar's children consume (A6). Returns
`{ filters, setText, toggleStatus, setDateRange, setAssignee, clear }`.

### `useCompleteJob()`

Owns the modal's open state, the signature data URL, the photo list, and the
optimistic sequence: `beginOptimistic` → Server Action → `commitOptimistic` and
SWR revalidation, or `rollbackOptimistic` with the error. Refuses submission
without a signature (`BR-4`) before any call is made.

### `useStartJob()` and `useCancelJob()`

The same optimistic sequence as `useCompleteJob`, without a modal (A1). Both
return `{ run, isPending, error }` keyed by job id, so two rows can be in flight
at once and a failure surfaces on its own row (`job-row-{id}-error`).

`useStartJob` carries no input at all. `useCancelJob` owns the inline reason
field and refuses submission with an empty reason (`BR-5`) before any call is
made, which mirrors how `useCompleteJob` treats the signature: the rule is
checked twice, once where the user can be told and once where it cannot be
bypassed.

Neither hook is imported by the other slices or by `JobRow`. The row receives
`onStart` and `onCancel` as props from `JobsTable`, which the orchestrator wires
— see A3 and `context/architecture.md` 9.4.

## B5. Backend class map

### `Job` aggregate

```csharp
public sealed class Job : AggregateRoot, ITenantScoped
{
    private readonly List<JobPhoto> _photos = [];

    public string Title { get; private set; }
    public string? Description { get; private set; }
    public Address Address { get; private set; }
    public JobStatus Status { get; private set; }
    public DateOnly? ScheduledDate { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }
    public string? SignatureUrl { get; private set; }
    public Guid? AssigneeId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid OrganizationId { get; private set; }

    public IReadOnlyCollection<JobPhoto> Photos => _photos.AsReadOnly();

    private Job() { }   // EF only

    public static Result<Job> Create(
        string title, string? description, Address address,
        DateOnly scheduledDate, Guid assigneeId, Guid customerId,
        Guid organizationId, DateTimeOffset now);            // BR-1, BR-6, D-14

    public Result Reschedule(DateOnly scheduledDate, Guid assigneeId, DateTimeOffset now); // BR-1, BR-2
    public Result Start(DateTimeOffset startedAt);                                          // BR-2, BR-3
    public Result Complete(DateTimeOffset completedAt, string signatureUrl,
                           IEnumerable<NewJobPhoto> photos);                                // BR-2, BR-4
    public Result Cancel(DateTimeOffset cancelledAt, string reason);                        // BR-2, BR-5
}
```

`now` is a parameter rather than a read of `DateTimeOffset.UtcNow`, so `BR-1` is
testable without manipulating a global clock. Handlers supply it from
`TimeProvider`.

There is no public setter and no `AddPhoto`. Photos arrive only through
`Complete`, which is the only moment the business produces them — that is what
"only accessible via the aggregate root" (line 185) means in practice.

### The repository signature (D-25)

```csharp
// Jobs.Domain
public sealed record JobSearchResult(
    Guid Id, string Title, JobStatus Status, DateOnly? ScheduledDate,
    Guid? AssigneeId, string? AssigneeName,
    string Street, string City, string State, int PhotoCount);

public sealed record JobSearchCriteria(
    string? Text, IReadOnlyList<JobStatus>? Statuses,
    DateOnly? From, DateOnly? To, Guid? AssigneeId,
    JobSortField Sort, string? Cursor, int Limit);

public interface IJobRepository
{
    Task<Job?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(Job job, CancellationToken ct);
    Task<IReadOnlyList<JobSearchResult>> SearchAsync(JobSearchCriteria criteria, CancellationToken ct);
}
```

`SearchAsync` returns `IReadOnlyList`, not `PagedList<T>`, because `PagedList<T>`
lives in `Common.Application` and `Domain` may reference only `Common.Domain`
(architecture 9.2). **The repository runs the query; the handler builds the
envelope**: it asks for `Limit + 1`, and if an extra row comes back it derives
`nextCursor` from the last returned row's ordering key and returns
`Result<PagedList<JobResponse>>` as line 207 requires.

This is also what gives line 222 its substance. `JobRepository.Reads.cs` carries
the projection with the keyset predicate, the full-text filter and the lateral
photo count; `JobRepository.Writes.cs` carries `AddAsync`. The partial split is
justified by what is in the files rather than by the instruction to make one.

### Remaining members

| Type | Modifiers | Notes |
|---|---|---|
| `Address` | `public sealed`, extends `ValueObject` | Six components; `GetEqualityComponents` yields all six |
| `NewJobPhoto` | `public readonly record struct`, in `Jobs.Domain` | The input to `Complete`: `Url`, `CapturedAt`, `Caption`. A parameter shape, not an entity — the aggregate turns each into a `JobPhoto` and assigns its identity |
| `JobPhoto` | `public sealed`, extends `Entity` | Constructor `internal`, so only the aggregate creates one. The **class** must be public: `Job.Photos` is a public member, and a public member cannot expose an `internal` type (CS0053). Reachability is enforced by the constructor and by the absence of `AddPhoto`, not by the class modifier |
| `JobCreatedDomainEvent` | `public sealed record` | Job id, assignee id, organization id |
| `JobCompletedDomainEvent` | `public sealed record` | Job id, customer id, completion timestamp |
| `JobCancelledDomainEvent` | `public sealed record` | Job id, assignee id (nullable), reason. The assignee is what `FR-12` notifies; nullable because a job cancelled before it was scheduled never reached a crew |
| `JobCompletedIntegrationEvent` | `public sealed record`, in `Jobs.IntegrationEvents` | Primitives only: job id, customer id, organization id, completed-at, amount basis |
| `CreateJobCommand` | `public sealed` | Returns `Result<Guid>` |
| `CreateJobCommandHandler` | `internal sealed` | |
| `CreateJobCommandValidator` | `internal sealed` | FluentValidation |
| `CompleteJobCommand` / `Handler` / `Validator` | as above | Returns `Result<Unit>` |
| `StartJobCommand` / `Handler` | as above | `FR-3`. Returns `Result<Unit>`. No validator: the command carries only the id, and `BR-3` is the aggregate's to enforce |
| `CancelJobCommand` / `Handler` / `Validator` | as above | `FR-5`. Returns `Result<Unit>`. The validator enforces a non-empty reason (`BR-5`) before the aggregate is loaded |
| `RescheduleJobCommand` / `Handler` / `Validator` | as above | `FR-2`. Returns `Result<Unit>`. Maps to `Job.Reschedule`; `BR-1` stays in the aggregate |
| `SearchJobsQuery` / `QueryHandler` | `public sealed` / `internal sealed` | Projection, `AsNoTracking`, returns `Result<PagedList<JobResponse>>` |
| `GetJobByIdQuery` / `QueryHandler` | `public sealed` / `internal sealed` | Projection, `AsNoTracking`, returns `Result<JobDetailResponse>`. Serves `GET /api/jobs/{id}`, the route that makes `not-found.tsx` genuine |
| `IJobRepository` | in `Jobs.Domain` | `GetByIdAsync`, `AddAsync`, `SearchAsync` — see the signature below |
| `JobSearchResult` | `public sealed record`, in `Jobs.Domain` | The projection `SearchAsync` returns. Not an aggregate: no identity, no behaviour (D-25) |
| `Assignee`, `Customer` | `public sealed`, extend `Entity`, in `Jobs.Domain` | Read-only rosters (D-26). No factory, no mutating method — EF materialises them and nothing else writes one |
| `IPartyRepository` | in `Jobs.Domain` | `ListAssigneesAsync`, `ListCustomersAsync`. Two reads, no writes, because there is no write path |
| `ListAssigneesQuery`, `ListCustomersQuery` / handlers | `public sealed` / `internal sealed` | Serve the two pickers. Projection, `AsNoTracking` |
| `JobSearchCriteria` | `public sealed record`, in `Jobs.Domain` | The Specification `SearchAsync` takes: text, statuses, date range, assignee, sort field, cursor, limit |
| `Notification` | `public sealed`, extends `Entity`, in `Jobs.Domain` | `Pending → Sent \| Failed`. `MarkSent` refuses from any state but `Pending` (D-22) |
| `INotificationSender` | in `Jobs.Application` | Port. Recipient, subject, body in; success or a reason out |
| `LoggingNotificationSender` | `internal sealed`, in `Jobs.Infrastructure` | Writes one structured log line. Delivery is simulated; the record is not |
| `NotifyAssigneeOnJobCreatedHandler` | `internal sealed`, in `Jobs.Application` | `FR-8`. Consumes the **domain** event: it never crosses a module boundary |
| `NotifyAssigneeOnJobCancelledHandler` | `internal sealed`, in `Jobs.Application` | `FR-12`. Consumes the **domain** event, and never crosses a boundary either — the internal event that does real work (D-37) |
| `NotifyCustomerOnJobCompletedHandler` | `internal sealed`, in `Jobs.Application` | `FR-10`. Consumes the integration event alongside Billing |
| `JobRepository` | `internal sealed partial`, in `Infrastructure` | Split across `JobRepository.Reads.cs` and `JobRepository.Writes.cs` (line 222) |
| `JobConfiguration` | `internal sealed` | `IEntityTypeConfiguration<Job>`; owned `Address`, enum as text |
| `Invoice` | `public sealed`, in `Billing.Domain` | Own invariants; unique on `(job_id, completed_at)` |
| `GenerateInvoiceOnJobCompletedHandler` | `internal sealed`, in `Billing.Application` | Consumes the integration event; idempotent per architecture 4.5 |

## B6. API contract

All routes are tenant-scoped by the `org` claim; none takes an organization
parameter, because accepting one would invite forging it.

| Method | Route | Body | Success | Failures |
|---|---|---|---|---|
| `POST` | `/api/jobs` | `CreateJobRequest` | `201` + `{ id }` | `400` validation, `409` invariant, `401` |
| `GET` | `/api/jobs` | query string: `text`, `statuses`, `scheduledFrom`, `scheduledTo`, `assigneeId`, `sort`, `cursor`, `limit` | `200` + `{ items, nextCursor }` | `400`, `401` |
| `GET` | `/api/jobs/{id}` | — | `200` + `JobDetailResponse` | `404`, `401` |
| `POST` | `/api/jobs/{id}/start` | — | `204` | `404`, `409`, `401` |
| `POST` | `/api/jobs/{id}/complete` | `CompleteJobRequest` | `204` | `400`, `404`, `409`, `401` |
| `POST` | `/api/jobs/{id}/cancel` | `{ reason }` | `204` | `400`, `404`, `409`, `401` |
| `PATCH` | `/api/jobs/{id}/schedule` | `{ scheduledDate, assigneeId }` | `204` | `400`, `404`, `409`, `401`. `FR-2`: correcting a Scheduled job. `409` when the job has left Scheduled (`BR-2`); **`400`** when the date is past — ~~`409`~~, superseded by **D-32**, because the create row gives the same rule a 400 and BR-1 refuses a value rather than a state |
| `GET` | `/api/assignees` | — | `200` + `[{ id, name }]` | `401`. Read-only roster; there is no `POST` |
| `GET` | `/api/customers` | — | `200` + `[{ id, name }]` | `401`. Read-only roster; there is no `POST` |
| `POST` | `/auth/dev-token` | — | `200` + `{ token }` | Registered only in Development |

Errors are `application/problem+json` carrying `type`, `title`, `status`,
`detail`, an `errorCode`, and `errors` keyed by field for validation failures.
Status transitions rejected by an invariant return **409**, not 400: the request
was well-formed and the state refused it.

## B7. Database DDL

The source for `database/schema.sql`. EF migrations produce the same shape; the
SQL file is the annotated deliverable (line 377).

```sql
CREATE SCHEMA IF NOT EXISTS jobs;
CREATE SCHEMA IF NOT EXISTS billing;

-- Read-only rosters (D-26). Seeded by migration; no command writes them and no
-- endpoint mutates them. They exist so the assignee and customer pickers have
-- something to offer and so a job row can show a name instead of a UUID.
-- When a Contacts module exists, an integration event maintains these rows
-- instead of the seed — see docs/normalization.md.
CREATE TABLE jobs.assignees (
    id              uuid PRIMARY KEY,
    organization_id uuid NOT NULL,
    name            text NOT NULL
);

CREATE TABLE jobs.customers (
    id              uuid PRIMARY KEY,
    organization_id uuid NOT NULL,
    name            text NOT NULL,
    email           text NOT NULL          -- FR-10 needs somewhere to notify
);

CREATE TABLE jobs.jobs (
    id                  uuid        PRIMARY KEY,
    organization_id     uuid        NOT NULL,
    title               text        NOT NULL,
    description         text        NULL,
    status              text        NOT NULL
        CHECK (status IN ('Draft','Scheduled','InProgress','Completed','Cancelled')),
    street              text        NOT NULL,
    city                text        NOT NULL,
    state               text        NOT NULL,
    zip_code            text        NOT NULL,
    latitude            numeric(9,6)  NOT NULL,
    longitude           numeric(9,6)  NOT NULL,
    scheduled_date      date        NULL,
    started_at          timestamptz NULL,
    completed_at        timestamptz NULL,
    cancelled_at        timestamptz NULL,
    cancellation_reason text        NULL,
    signature_url       text        NULL,
    assignee_id         uuid        NULL     REFERENCES jobs.assignees(id),
    customer_id         uuid        NOT NULL REFERENCES jobs.customers(id),
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),   -- kept current by the trigger below

    CONSTRAINT ck_jobs_completed_has_signature
        CHECK (status <> 'Completed' OR signature_url IS NOT NULL),
    CONSTRAINT ck_jobs_cancelled_has_reason
        CHECK (status <> 'Cancelled' OR cancellation_reason IS NOT NULL)
);

-- NFR-6 requires updated_at to be true, and a DEFAULT only fires on INSERT.
-- A trigger keeps it honest for anything that writes the row, including psql;
-- EF's SaveChanges is not the only writer this schema has to survive.
CREATE FUNCTION jobs.touch_updated_at() RETURNS trigger
    LANGUAGE plpgsql AS $$
BEGIN
    NEW.updated_at := now();
    RETURN NEW;
END;
$$;

CREATE TRIGGER tr_jobs_touch_updated_at
    BEFORE UPDATE ON jobs.jobs
    FOR EACH ROW EXECUTE FUNCTION jobs.touch_updated_at();

CREATE TABLE jobs.job_photos (
    id          uuid        PRIMARY KEY,
    job_id      uuid        NOT NULL REFERENCES jobs.jobs(id) ON DELETE CASCADE,
    url         text        NOT NULL,
    captured_at timestamptz NOT NULL,
    caption     text        NULL
);

CREATE TABLE jobs.outbox_messages (
    id           uuid        PRIMARY KEY,
    type         text        NOT NULL,
    content      jsonb       NOT NULL,
    occurred_on  timestamptz NOT NULL,
    processed_on timestamptz NULL,
    error        text        NULL
);

CREATE TABLE jobs.notifications (
    id              uuid        PRIMARY KEY,
    organization_id uuid        NOT NULL,
    source_event_id uuid        NOT NULL,
    recipient       text        NOT NULL,
    channel         text        NOT NULL CHECK (channel IN ('Email')),
    subject         text        NOT NULL,
    body            text        NOT NULL,
    status          text        NOT NULL
        CHECK (status IN ('Pending','Sent','Failed')),
    sent_at         timestamptz NULL,
    failure_reason  text        NULL,
    created_at      timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT ck_notifications_sent_has_timestamp
        CHECK (status <> 'Sent' OR sent_at IS NOT NULL),
    CONSTRAINT uq_notifications_idempotency UNIQUE (source_event_id, recipient)
);

CREATE TABLE billing.invoices (
    id              uuid          PRIMARY KEY,
    organization_id uuid          NOT NULL,
    job_id          uuid          NOT NULL,
    customer_id     uuid          NOT NULL,
    amount          numeric(12,2) NOT NULL CHECK (amount > 0),
    job_completed_at timestamptz  NOT NULL,
    issued_at       timestamptz   NOT NULL DEFAULT now(),

    CONSTRAINT uq_invoices_idempotency UNIQUE (job_id, job_completed_at)
);
```

Four constraints deserve comment. `ck_jobs_completed_has_signature` enforces
`BR-4` at the level of the data, so a row violating it cannot exist even if
written by something other than the aggregate.

`uq_invoices_idempotency` is the business idempotency key line 246 asks for by
name, and `uq_notifications_idempotency` is its counterpart for `FR-8` and
`FR-10`. Together they are the whole of the idempotency mechanism (D-23): there
is no generic consumer table, because each consumer earns idempotency with a
constraint on what it writes, and both keys derive from values that are stable
across replays — a completion timestamp and an outbox row identifier.

`ck_notifications_sent_has_timestamp` keeps the record honest: a notification
cannot claim to have been sent without recording when.

`assignee_id` and `customer_id` **do** carry foreign keys, because the rosters
they point at live in this schema (D-26). Lines 261-262 ask for exactly that.

`billing.invoices.job_id` has **no** foreign key into `jobs.jobs`, and that
absence is the interesting one: it is the module boundary itself. A constraint
there would let the database enforce a relationship the two modules deliberately
express through a contract of primitives, and would make extracting Billing to
its own database a redesign rather than a migration. The rule is not "no foreign
keys" — it is *a foreign key stays inside the schema that owns both ends*.

### Indexes

```sql
-- Default ordering: no filtered column stands in front of the sort keys, so the
-- index supplies the order for the unfiltered list and for any multi-status filter.
CREATE INDEX ix_jobs_tenant_keyset
    ON jobs.jobs (organization_id, coalesce(scheduled_date, '-infinity'::date) DESC, id DESC);

-- Single-status filter: the equality is a scan boundary, not a post-filter.
CREATE INDEX ix_jobs_tenant_status_keyset
    ON jobs.jobs (organization_id, status,
                  coalesce(scheduled_date, '-infinity'::date) DESC, id DESC);

CREATE INDEX ix_jobs_tenant_title_keyset
    ON jobs.jobs (organization_id, title, id);

CREATE INDEX ix_jobs_tenant_assignee
    ON jobs.jobs (organization_id, assignee_id);

CREATE INDEX ix_jobs_search
    ON jobs.jobs
    USING GIN (to_tsvector('english', title || ' ' || coalesce(description, '')));

CREATE INDEX ix_job_photos_job ON jobs.job_photos (job_id);

CREATE INDEX ix_outbox_unprocessed
    ON jobs.outbox_messages (occurred_on)
    WHERE processed_on IS NULL;

CREATE INDEX ix_invoices_tenant_job ON billing.invoices (organization_id, job_id);

CREATE INDEX ix_notifications_pending
    ON jobs.notifications (created_at)
    WHERE status = 'Pending';

CREATE INDEX ix_assignees_tenant ON jobs.assignees (organization_id);
CREATE INDEX ix_customers_tenant ON jobs.customers (organization_id);
```

### The search query

The source for `database/queries.sql`, implementing lines 279-284.

```sql
SELECT  j.id, j.title, j.status, j.scheduled_date,
        j.assignee_id, a.name AS assignee_name,
        j.street, j.city, j.state,
        coalesce(p.photo_count, 0) AS photo_count
FROM    jobs.jobs j
LEFT JOIN jobs.assignees a ON a.id = j.assignee_id
LEFT JOIN LATERAL (
        SELECT count(*) AS photo_count
        FROM   jobs.job_photos ph
        WHERE  ph.job_id = j.id
) p ON true
WHERE   j.organization_id = $1
  AND   ($2::text[] IS NULL OR j.status = ANY ($2))
  AND   ($3::date   IS NULL OR j.scheduled_date >= $3)
  AND   ($4::date   IS NULL OR j.scheduled_date <= $4)
  AND   ($5::uuid   IS NULL OR j.assignee_id = $5)
  AND   ($6::text   IS NULL OR
         to_tsvector('english', j.title || ' ' || coalesce(j.description, ''))
         @@ websearch_to_tsquery('english', $6))
  AND   ($7::date IS NULL OR
         (coalesce(j.scheduled_date, '-infinity'::date), j.id) < ($7, $8))  -- keyset cursor
ORDER BY coalesce(j.scheduled_date, '-infinity'::date) DESC, j.id DESC
LIMIT   $9;
```

The lateral count avoids grouping the whole result set: it runs once per returned
row, against `ix_job_photos_job`, so its cost is bounded by the page size rather
than by the number of matches. The keyset predicate compares the ordered pair, so
paging never skips or repeats a row when data changes between pages — the
argument in `context/architecture.md` section 6.4.

**The ordering key is `coalesce(scheduled_date, '-infinity')`, not the column.**
A `Draft` job has no date, and over the bare column the row comparison
`(scheduled_date, id) < ($7, $8)` yields NULL rather than true for such a row, so
`WHERE` drops it — dateless jobs vanish after the first page. `-infinity` is a
real date, so the expression is total, the comparison always defined, and those
jobs sort last. The two keyset indexes are declared over the same expression, or
the planner could not use them.

**The `($n IS NULL OR …)` form is for reading, not for planning.** It lets this
file present one query instead of thirty-two, but a predicate the planner cannot
fold discourages index use. The EF Core implementation composes the `WHERE`
clause from the filters that are actually set, so the statement it sends carries
only live predicates — which is what lets the planner choose between
`ix_jobs_tenant_keyset` and `ix_jobs_tenant_status_keyset`.
`database/queries.sql` carries the expanded variants and their
`EXPLAIN (ANALYZE, BUFFERS)` output.

The query above is the `scheduledDate` ordering. Sorting by title substitutes
`(j.title, j.id)` in both the keyset predicate and the `ORDER BY`, served by
`ix_jobs_tenant_title_keyset`; `title` is `NOT NULL`, so that variant needs no
coalesce. Those two are the whole sortable set by D-18, so `database/queries.sql`
carries both variants explicitly rather than building an `ORDER BY` from input.

## B8. Traceability matrix

| Design artefact | Assessment source | Rubric criterion | Points |
|---|---|---|---|
| `JobState`, `JobAction`, transition table (B2) | lines 68-87 | 1 — State machine types | 5 |
| `transitionJob` generic signature (B2) | lines 81-87 | 1 — State machine types | 5 |
| `getJobSummary` exhaustiveness (B2) | lines 88-89 | 1 — State machine types | 5 |
| `DeepReadonly` with bounded depth and tuples (B2) | lines 24-29, 396 | 1 — DeepReadonly and PathKeys | 5 |
| `PathKeys` leaf paths (B2) | lines 31-34 | 1 — DeepReadonly and PathKeys | 5 |
| `createTypedEventEmitter` mapped-type storage (B2) | lines 36-42 | 1 — TypeScript mastery | part of 15 |
| `QueryBuilder` narrowing and template literal (B2) | lines 47-63 | 1 — Type-safe builder | 5 |
| File map matching the prescribed tree (B1) | lines 112-134 | 2 — FSD structure | 5 |
| Server Actions inside slices, mutations only (B1) | line 138 | 2 — Server/client boundary | 5 |
| Route files: page, loading, error, not-found (B1) | lines 100-110 | 2 — Error, loading, Suspense | 5 |
| Store contents with no `jobs` array (B3) | lines 144-148 | 2 — State management | 5 |
| Selectors including `makeVisibleJobsSelector` (B3) | lines 145-146 | 2 — State management | 5 |
| Optimistic actions and rollback (B3) | line 147 | 2 — State management | 5 |
| `useCreateJob` reducer (B4) | line 156 | 2 — React patterns | part of 20 |
| Compound filter bar (A6) | line 155 | 2 — React patterns | part of 20 |
| Controlled atom contract (A7) | line 154 | 2 — React patterns | part of 20 |
| Ternary rendering, view states (A4) | line 159 | 2 — React patterns | part of 20 |
| Error boundary around the table (A4) | line 158 | 2 — Error, loading, Suspense | 5 |
| `start-job` and `cancel-job` slices (B1, B4) | line 174, prd `FR-3`/`FR-5` | 2 — FSD structure, 5 — E2E | 5 + 5 |
| `Job` members and invariant methods (B5) | lines 169-175 | 3 — Aggregate design | 7 |
| `Address` value object (B5) | lines 177-180 | 3 — Aggregate design | 7 |
| `JobPhoto` internal constructor (B5) | lines 182-185 | 3 — Aggregate design | 7 |
| Command, query and validator modifiers (B5) | lines 210-215 | 3 — CQRS and MediatR | 6 |
| `IJobRepository` and partial implementation (B5) | lines 219-222 | 3 — Repository and UoW | 5 |
| `JobCompletedIntegrationEvent` primitives only (B5) | line 237 | 3 — Outbox, 6 — DDD concepts | 4 + 3 |
| `Invoice` with idempotency constraint (B5, B7) | line 246 | 3 — Outbox and Hangfire | 4 |
| `jobs.notifications` and its idempotency constraint (B5, B7) | lines 188, 241 | 3 — Outbox and Hangfire, 6 — DDD concepts | 4 + 3 |
| `SearchAsync` returning a projected read model (B5) | lines 208, 220 | 3 — Repository and UoW, 3 — CQRS | 5 + 6 |
| API contract and ProblemDetails (B6) | lines 198, 203, 347 | 3 — CQRS, 2 — Error handling | 6 + 5 |
| Schema, owned columns, text enums (B7) | lines 229-232, 259-264 | 4 — Schema design | 4 |
| Index set (B7) | lines 265-269 | 4 — Indexing and optimization | 3 |
| Search query with lateral count and keyset (B7) | lines 279-284 | 4 — Indexing and optimization | 3 |
| `data-testid` contract (A8) | lines 331-333 | 5 — E2E Playwright | 5 |
| Acceptance flow states and waits (A4, A5) | lines 320-333 | 5 — E2E Playwright | 5 |
