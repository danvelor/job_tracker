# Type Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the framework-agnostic half of the frontend — the Part 1 type utilities, the job state machine, the ports, the in-memory adapter and the use cases — so that plan 2 can build a UI on top of something already proven.

**Architecture:** Everything here lives under `frontend/src/shared/` and `frontend/src/core/`, and imports nothing from React, Next or any adapter. That import rule is what lets one use case serve a Server Component, a Server Action and a unit test unchanged. The Part 1 utilities are built here and consumed by plan 2 in real positions rather than parked in a folder (D-06): the typed emitter becomes the cross-slice bus, `PathKeys` types the create-job reducer, `DeepReadonly` types the store.

**Tech Stack:** TypeScript 5.x `strict`, Next.js 15 (scaffolded here, used in plan 2), Jest with `next/jest`, `expect-type`, ESLint.

**Spec:** `context/design.md` (part B2 for every type signature), `context/architecture.md` (5.1, 5.4, 5.7, 8.3, 9.3)

## Global Constraints

- TypeScript `strict: true`. Mandated by assessment line 21
- **No `any`. No `as unknown as X`.** `CLAUDE.md` non-negotiable 4
- File names are kebab-case with a role suffix: `*.type.ts`, `*.hook.ts`, `*.store.ts`, `*.adapter.ts`, `*.action.ts`, `*.component.tsx` (architecture 9.3)
- `core/` and `shared/` import nothing from React, Next or any adapter (architecture 5.1)
- **Test-driven**: no production code without a test watched failing first (architecture 8.3, D-28). Task 1 is the scaffolding exemption under condition (a)
- Type-level assertions verify red through `npm run typecheck`, not through Jest — a broken type assertion leaves Jest green (architecture 8)
- Jest `coverageThreshold` at 80% for branches, functions, lines and statements
- Every commit is green. Red lives inside the cycle and is never committed

---

## File Structure

```
frontend/
├── package.json                jest, typecheck, lint scripts
├── tsconfig.json               strict: true
├── jest.config.ts              next/jest, coverageThreshold 80
├── eslint.config.mjs
└── src/
    ├── shared/
    │   ├── types/
    │   │   ├── primitive.type.ts        Primitive — leaf scalars
    │   │   ├── depth.type.ts            Prev — the recursion bound
    │   │   ├── deep-readonly.type.ts    DeepReadonly<T, D>
    │   │   └── path-keys.type.ts        PathKeys<T, D>
    │   ├── events/
    │   │   └── typed-event-emitter.ts   createTypedEventEmitter
    │   └── query-builder/
    │       └── query-builder.ts         QueryBuilder<T, Selected, Query>
    └── core/
        ├── domain/
        │   ├── result.type.ts           Result<T, E>, CoreError, ok(), err()
        │   └── job/
        │       ├── job-status.type.ts   JobStatus
        │       ├── job-state.type.ts    JobState, JobAction, AllowedAction
        │       ├── job-summary.type.ts  JobSummary, JobDetail, Party
        │       ├── transition-job.ts    transitionJob
        │       ├── get-job-summary.ts   getJobSummary
        │       └── index.ts             barrel
        ├── application/
        │   ├── ports/jobs.port.ts       JobsPort and its query/input types
        │   └── use-cases/               one file per use case
        └── di/container.ts              resolves the adapter from configuration
```

One responsibility per file. `primitive.type.ts` and `depth.type.ts` are separate because both `DeepReadonly` and `PathKeys` need them and neither owns them.

---

### Task 1: Frontend scaffold and toolchain

**Files:**
- Create: `frontend/` (via `create-next-app`)
- Create: `frontend/jest.config.ts`
- Create: `frontend/jest.setup.ts`
- Modify: `frontend/package.json` (scripts)
- Test: `frontend/src/shared/types/__tests__/toolchain.test.ts`

**Interfaces:**
- Consumes: nothing
- Produces: `npm --prefix frontend test`, `npm --prefix frontend run typecheck`, `npm --prefix frontend run lint` — the three commands every later task runs

**This task has no red phase.** It is the scaffolding exemption of architecture 8.3 condition (a): a test lives inside the harness, so none can exist before the harness does. Every task after this one is test-first.

- [ ] **Step 1: Scaffold the Next.js app**

```bash
cd /Users/danielvelezortiz/technical_test/JobTracker
npx create-next-app@latest frontend \
  --typescript --app --tailwind --eslint --src-dir \
  --import-alias "@/*" --no-turbopack --use-npm --yes
```

- [ ] **Step 2: Confirm `strict` is on**

Read `frontend/tsconfig.json`. It must contain `"strict": true` under `compilerOptions`. `create-next-app` sets it; if it is absent, add it. Assessment line 21 mandates it and every type assertion in this plan depends on it.

- [ ] **Step 3: Install the test toolchain**

```bash
npm --prefix frontend install -D \
  jest@29 jest-environment-jsdom@29 @types/jest@29 \
  @testing-library/react@16 @testing-library/jest-dom@6 \
  expect-type@1 ts-node@10
```

`expect-type` rather than `vitest`: it exports the same `expectTypeOf` the assessment names on line 306, and the section heading names Jest as the runner (D-07).

- [ ] **Step 4: Write `frontend/jest.config.ts`**

```ts
import type { Config } from 'jest';
import nextJest from 'next/jest.js';

const createJestConfig = nextJest({ dir: './' });

const config: Config = {
  coverageProvider: 'v8',
  testEnvironment: 'jsdom',
  setupFilesAfterEnv: ['<rootDir>/jest.setup.ts'],
  moduleNameMapper: { '^@/(.*)$': '<rootDir>/src/$1' },
  collectCoverageFrom: [
    'src/**/*.{ts,tsx}',
    '!src/**/*.d.ts',
    '!src/**/index.ts',
    '!src/app/**',
  ],
  coverageThreshold: {
    global: { branches: 80, functions: 80, lines: 80, statements: 80 },
  },
};

export default createJestConfig(config);
```

`coverageThreshold` is a gate, not a report: the run fails below 80%, which is what the rubric asks for on line 439.

- [ ] **Step 5: Write `frontend/jest.setup.ts`**

```ts
import '@testing-library/jest-dom';
```

- [ ] **Step 6: Add the scripts to `frontend/package.json`**

Replace the `"scripts"` block with:

```json
"scripts": {
  "dev": "next dev",
  "build": "next build",
  "start": "next start",
  "lint": "eslint",
  "typecheck": "tsc --noEmit",
  "test": "jest",
  "test:coverage": "jest --coverage"
}
```

`typecheck` is separate from `test` on purpose: `expect-type` asserts at compile time, so a broken type assertion leaves Jest green while `tsc` fails. Architecture 8 makes it a required gate.

- [ ] **Step 7: Write the toolchain proof test**

Create `frontend/src/shared/types/__tests__/toolchain.test.ts`:

```ts
import { expectTypeOf } from 'expect-type';

describe('toolchain', () => {
  it('runs Jest assertions', () => {
    expect(1 + 1).toBe(2);
  });

  it('runs expect-type assertions', () => {
    expectTypeOf<string>().toEqualTypeOf<string>();
    expectTypeOf<string>().not.toEqualTypeOf<number>();
  });

  it('has strictNullChecks on', () => {
    // @ts-expect-error null is not assignable to string under strict
    const value: string = null;
    expect(value).toBeNull();
  });
});
```

The third case is the one that matters. `@ts-expect-error` fails the build when the error it expects stops occurring, so this test breaks if anyone turns `strict` off.

- [ ] **Step 8: Verify all three commands are green**

```bash
npm --prefix frontend test
npm --prefix frontend run typecheck
npm --prefix frontend run lint
```

Expected: three passing tests, no type errors, no lint errors.

- [ ] **Step 9: Commit**

```bash
git add frontend
git commit -m "chore: scaffold the frontend and its test toolchain

create-next-app with TypeScript strict, App Router and src/, plus Jest
via next/jest, expect-type for compile-time assertions, and a
coverageThreshold of 80% that fails the run rather than reporting.

typecheck is a separate script from test because expect-type asserts at
compile time: a broken type assertion leaves Jest green while tsc fails,
so architecture 8 makes tsc --noEmit its own gate.

The toolchain test asserts strictNullChecks with @ts-expect-error, which
breaks the build if strict is ever turned off."
```

---

### Task 2: `DeepReadonly<T>`

**Files:**
- Create: `frontend/src/shared/types/primitive.type.ts`
- Create: `frontend/src/shared/types/depth.type.ts`
- Create: `frontend/src/shared/types/deep-readonly.type.ts`
- Test: `frontend/src/shared/types/__tests__/deep-readonly.type-test.ts`

**Interfaces:**
- Consumes: nothing
- Produces: `Primitive`, `Prev`, `DeepReadonly<T, D extends Prev[number] = 9>`. Task 3 imports `Primitive` and `Prev`; the plan-2 store types its state as `DeepReadonly<JobsUiState>`

- [ ] **Step 1: Write the failing type test**

Create `frontend/src/shared/types/__tests__/deep-readonly.type-test.ts`:

```ts
import { expectTypeOf } from 'expect-type';
import type { DeepReadonly } from '../deep-readonly.type';

describe('DeepReadonly', () => {
  it('leaves primitives unchanged', () => {
    expectTypeOf<DeepReadonly<string>>().toEqualTypeOf<string>();
    expectTypeOf<DeepReadonly<number>>().toEqualTypeOf<number>();
    expectTypeOf<DeepReadonly<null>>().toEqualTypeOf<null>();
  });

  it('makes nested object properties readonly', () => {
    expectTypeOf<DeepReadonly<{ a: { b: string } }>>().toEqualTypeOf<{
      readonly a: { readonly b: string };
    }>();
  });

  it('turns arrays into ReadonlyArray of DeepReadonly items', () => {
    expectTypeOf<DeepReadonly<{ a: string }[]>>().toEqualTypeOf<
      ReadonlyArray<{ readonly a: string }>
    >();
  });

  it('turns Maps into ReadonlyMap with a deep value', () => {
    expectTypeOf<DeepReadonly<Map<string, { a: number }>>>().toEqualTypeOf<
      ReadonlyMap<string, { readonly a: number }>
    >();
  });

  it('turns Sets into ReadonlySet with a deep value', () => {
    expectTypeOf<DeepReadonly<Set<{ a: number }>>>().toEqualTypeOf<
      ReadonlySet<{ readonly a: number }>
    >();
  });

  it('keeps tuple positions instead of widening to an array', () => {
    expectTypeOf<DeepReadonly<[string, { a: number }]>>().toEqualTypeOf<
      readonly [string, { readonly a: number }]
    >();
  });

  it('treats Date as a leaf', () => {
    expectTypeOf<DeepReadonly<{ at: Date }>>().toEqualTypeOf<{
      readonly at: Date;
    }>();
  });

  it('bounds recursion so a self-referential type still resolves', () => {
    interface Node {
      value: string;
      child: Node;
    }
    expectTypeOf<DeepReadonly<Node>>().not.toBeNever();
  });

  it('is a type-only module', () => {
    expect(true).toBe(true);
  });
});
```

The last case exists so Jest does not report the file as containing no tests; the assertions above it are compile-time.

- [ ] **Step 2: Run typecheck to verify it fails**

Run: `npm --prefix frontend run typecheck`
Expected: FAIL with `Cannot find module '../deep-readonly.type'`.

- [ ] **Step 3: Write `primitive.type.ts`**

```ts
export type Primitive =
  | string
  | number
  | boolean
  | bigint
  | symbol
  | null
  | undefined;
```

- [ ] **Step 4: Write `depth.type.ts`**

```ts
/**
 * A decrementing counter for recursive conditional types. Indexing
 * `Prev[D]` yields `D - 1`, and `Prev[0]` is `never`, which is the stop
 * condition. The rubric asks for bounded recursive depth explicitly.
 */
export type Prev = [never, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9];
```

- [ ] **Step 5: Write `deep-readonly.type.ts`**

```ts
import type { Prev } from './depth.type';
import type { Primitive } from './primitive.type';

export type DeepReadonly<T, D extends Prev[number] = 9> = [D] extends [never]
  ? T
  : T extends Primitive
    ? T
    : T extends (...args: never[]) => unknown
      ? T
      : T extends Date
        ? T
        : T extends Map<infer K, infer V>
          ? ReadonlyMap<K, DeepReadonly<V, Prev[D]>>
          : T extends Set<infer V>
            ? ReadonlySet<DeepReadonly<V, Prev[D]>>
            : T extends readonly [unknown, ...unknown[]]
              ? { readonly [I in keyof T]: DeepReadonly<T[I], Prev[D]> }
              : T extends ReadonlyArray<infer I>
                ? ReadonlyArray<DeepReadonly<I, Prev[D]>>
                : { readonly [K in keyof T]: DeepReadonly<T[K], Prev[D]> };
```

Order matters twice. The tuple case precedes the array case because a tuple also matches `ReadonlyArray` and would otherwise lose its positional types. `Date` and functions are leaves because freezing their members means nothing.

- [ ] **Step 6: Run typecheck and tests to verify green**

```bash
npm --prefix frontend run typecheck
npm --prefix frontend test -- deep-readonly
```

Expected: no type errors; one passing test.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/shared/types
git commit -m "feat: add DeepReadonly with bounded recursion

Handles nested objects, arrays, Maps, Sets and tuples, with Date and
functions as leaves because freezing their members means nothing. The
tuple case precedes the array case: a tuple also matches ReadonlyArray
and would otherwise lose its positional types.

Depth is bounded by a Prev tuple, which the rubric asks for explicitly
and which keeps a self-referential type from exhausting the compiler.
Assertions are compile-time, so the gate is tsc --noEmit rather than
Jest."
```

---

### Task 3: `PathKeys<T>`

**Files:**
- Create: `frontend/src/shared/types/path-keys.type.ts`
- Test: `frontend/src/shared/types/__tests__/path-keys.type-test.ts`

**Interfaces:**
- Consumes: `Primitive` from `primitive.type.ts`, `Prev` from `depth.type.ts` (task 2)
- Produces: `PathKeys<T, D extends Prev[number] = 9>`. Plan 2 uses it to type every field address in the create-job reducer

- [ ] **Step 1: Write the failing type test**

Create `frontend/src/shared/types/__tests__/path-keys.type-test.ts`:

```ts
import { expectTypeOf } from 'expect-type';
import type { PathKeys } from '../path-keys.type';

describe('PathKeys', () => {
  it('produces dot-notation paths to leaves only', () => {
    expectTypeOf<PathKeys<{ a: { b: string; c: { d: number } } }>>().toEqualTypeOf<
      'a.b' | 'a.c.d'
    >();
  });

  it('does not emit the intermediate path', () => {
    expectTypeOf<PathKeys<{ a: { b: string } }>>().not.toEqualTypeOf<'a' | 'a.b'>();
  });

  it('treats an array property as a leaf', () => {
    expectTypeOf<PathKeys<{ tags: string[]; name: string }>>().toEqualTypeOf<
      'tags' | 'name'
    >();
  });

  it('treats a Date property as a leaf', () => {
    expectTypeOf<PathKeys<{ at: Date }>>().toEqualTypeOf<'at'>();
  });

  it('handles the shape the create-job form uses', () => {
    type Values = {
      title: string;
      address: { street: string; zipCode: string };
    };
    expectTypeOf<PathKeys<Values>>().toEqualTypeOf<
      'title' | 'address.street' | 'address.zipCode'
    >();
  });

  it('is a type-only module', () => {
    expect(true).toBe(true);
  });
});
```

The last real case is the one plan 2 depends on: `address.zipCode` must be a valid path and `address.zip` must not compile.

- [ ] **Step 2: Run typecheck to verify it fails**

Run: `npm --prefix frontend run typecheck`
Expected: FAIL with `Cannot find module '../path-keys.type'`.

- [ ] **Step 3: Write `path-keys.type.ts`**

```ts
import type { Prev } from './depth.type';
import type { Primitive } from './primitive.type';

type Leaf =
  | Primitive
  | Date
  | ReadonlyArray<unknown>
  | Map<unknown, unknown>
  | Set<unknown>
  | ((...args: never[]) => unknown);

export type PathKeys<T, D extends Prev[number] = 9> = [D] extends [never]
  ? never
  : {
      [K in keyof T & string]: T[K] extends Leaf
        ? K
        : `${K}.${PathKeys<T[K], Prev[D]>}`;
    }[keyof T & string];
```

`D` bounds the recursion for the same reason `DeepReadonly` does, reusing the same `Prev`. Without it a self-referential type produces *"Type instantiation is excessively deep"* instead of a union.

- [ ] **Step 4: Run typecheck and tests to verify green**

```bash
npm --prefix frontend run typecheck
npm --prefix frontend test -- path-keys
```

Expected: no type errors; one passing test.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/shared/types
git commit -m "feat: add PathKeys producing leaf paths only

PathKeys<{a:{b:string;c:{d:number}}}> yields 'a.b' | 'a.c.d' and not the
intermediate 'a', which is what assessment line 33 specifies. Arrays,
Dates, Maps, Sets and functions count as leaves.

Depth is bounded with the same Prev tuple DeepReadonly uses. Two
recursive utilities sitting side by side with only one of them bounded
reads as an oversight."
```

---

### Task 4: `createTypedEventEmitter`

**Files:**
- Create: `frontend/src/shared/events/typed-event-emitter.ts`
- Test: `frontend/src/shared/events/__tests__/typed-event-emitter.test.ts`

**Interfaces:**
- Consumes: nothing
- Produces: `EventMap`, `TypedEventEmitter<E>`, `createTypedEventEmitter<E>()`. Plan 2 instantiates it with `JobsEventMap` as the cross-slice bus, which is what lets slices stay mutually unaware (architecture 5.6 rule 2)

- [ ] **Step 1: Write the failing test**

Create `frontend/src/shared/events/__tests__/typed-event-emitter.test.ts`:

```ts
import { expectTypeOf } from 'expect-type';
import { createTypedEventEmitter } from '../typed-event-emitter';

type TestEvents = {
  'job:status-changed': { jobId: string; status: string };
  'jobs:invalidate': void;
};

describe('createTypedEventEmitter', () => {
  it('delivers a payload to a subscribed handler', () => {
    const emitter = createTypedEventEmitter<TestEvents>();
    const received: { jobId: string; status: string }[] = [];

    emitter.on('job:status-changed', (payload) => received.push(payload));
    emitter.emit('job:status-changed', { jobId: 'j1', status: 'Completed' });

    expect(received).toEqual([{ jobId: 'j1', status: 'Completed' }]);
  });

  it('delivers to every subscriber of the same event', () => {
    const emitter = createTypedEventEmitter<TestEvents>();
    const calls: string[] = [];

    emitter.on('job:status-changed', () => calls.push('first'));
    emitter.on('job:status-changed', () => calls.push('second'));
    emitter.emit('job:status-changed', { jobId: 'j1', status: 'Completed' });

    expect(calls).toEqual(['first', 'second']);
  });

  it('stops delivering after off with the same handler reference', () => {
    const emitter = createTypedEventEmitter<TestEvents>();
    const calls: string[] = [];
    const handler = () => calls.push('called');

    emitter.on('job:status-changed', handler);
    emitter.off('job:status-changed', handler);
    emitter.emit('job:status-changed', { jobId: 'j1', status: 'Completed' });

    expect(calls).toEqual([]);
  });

  it('leaves other handlers subscribed when one is removed', () => {
    const emitter = createTypedEventEmitter<TestEvents>();
    const calls: string[] = [];
    const removed = () => calls.push('removed');

    emitter.on('job:status-changed', removed);
    emitter.on('job:status-changed', () => calls.push('kept'));
    emitter.off('job:status-changed', removed);
    emitter.emit('job:status-changed', { jobId: 'j1', status: 'Completed' });

    expect(calls).toEqual(['kept']);
  });

  it('does nothing when emitting an event with no subscribers', () => {
    const emitter = createTypedEventEmitter<TestEvents>();
    expect(() => emitter.emit('jobs:invalidate', undefined)).not.toThrow();
  });

  it('types the handler payload from the event name', () => {
    const emitter = createTypedEventEmitter<TestEvents>();
    emitter.on('job:status-changed', (payload) => {
      expectTypeOf(payload).toEqualTypeOf<{ jobId: string; status: string }>();
    });
    expect(emitter).toBeDefined();
  });

  it('rejects a payload that does not match the event', () => {
    const emitter = createTypedEventEmitter<TestEvents>();
    // @ts-expect-error the payload for job:status-changed is not a string
    emitter.emit('job:status-changed', 'wrong');
    expect(emitter).toBeDefined();
  });

  it('rejects an unknown event name', () => {
    const emitter = createTypedEventEmitter<TestEvents>();
    // @ts-expect-error 'job:exploded' is not in the event map
    emitter.on('job:exploded', () => undefined);
    expect(emitter).toBeDefined();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `npm --prefix frontend test -- typed-event-emitter`
Expected: FAIL with `Cannot find module '../typed-event-emitter'`.

- [ ] **Step 3: Write `typed-event-emitter.ts`**

```ts
export type EventMap = Record<string, unknown>;

export interface TypedEventEmitter<E extends EventMap> {
  on<K extends keyof E>(event: K, handler: (payload: E[K]) => void): void;
  off<K extends keyof E>(event: K, handler: (payload: E[K]) => void): void;
  emit<K extends keyof E>(event: K, payload: E[K]): void;
}

/**
 * Handlers are stored in a mapped type rather than a flat Map. That is what
 * removes the need for a cast: indexing a mapped type with a generic
 * `K extends keyof E` preserves the link between the key and its payload,
 * whereas `Map<keyof E, Set<(p: E[keyof E]) => void>>` collapses the payload
 * to a union and forces an assertion on retrieval. Assessment line 42
 * forbids the cast, so the storage shape is the whole answer.
 */
type HandlerStore<E extends EventMap> = {
  [K in keyof E]?: Set<(payload: E[K]) => void>;
};

export function createTypedEventEmitter<E extends EventMap>(): TypedEventEmitter<E> {
  const handlers: HandlerStore<E> = {};

  return {
    on(event, handler) {
      const existing = handlers[event];
      if (existing === undefined) {
        handlers[event] = new Set([handler]);
        return;
      }
      existing.add(handler);
    },

    off(event, handler) {
      handlers[event]?.delete(handler);
    },

    emit(event, payload) {
      handlers[event]?.forEach((handler) => handler(payload));
    },
  };
}
```

- [ ] **Step 4: Run tests and typecheck to verify green**

```bash
npm --prefix frontend test -- typed-event-emitter
npm --prefix frontend run typecheck
```

Expected: eight passing tests; no type errors.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/shared/events
git commit -m "feat: add createTypedEventEmitter with no casts

Handlers live in a mapped type rather than a flat Map, which is the
whole reason no assertion is needed: indexing a mapped type with a
generic K extends keyof E keeps the key bound to its payload, while
Map<keyof E, ...> collapses the payload to a union and forces a cast on
retrieval. Assessment line 42 forbids the cast, so the storage shape is
the answer.

Negative cases use @ts-expect-error, so the build breaks if a mismatched
payload or an unknown event name ever starts compiling."
```

---

### Task 5: `QueryBuilder<T>`

**Files:**
- Create: `frontend/src/shared/query-builder/query-builder.ts`
- Test: `frontend/src/shared/query-builder/__tests__/query-builder.test.ts`

**Interfaces:**
- Consumes: nothing
- Produces: `ComparisonOperator`, `QueryBuilder<T, Selected, Query>` with `select`, `where`, `orderBy`, `limit`, `build`. Plan 3 mirrors its rendered SQL in `database/queries.sql`; the browser never sends it

- [ ] **Step 1: Write the failing test**

Create `frontend/src/shared/query-builder/__tests__/query-builder.test.ts`:

```ts
import { expectTypeOf } from 'expect-type';
import { QueryBuilder } from '../query-builder';

type Job = {
  id: string;
  title: string;
  status: 'Scheduled' | 'Completed';
  photoCount: number;
};

describe('QueryBuilder', () => {
  it('renders the chained query', () => {
    const result = new QueryBuilder<Job>()
      .select('id', 'title', 'status')
      .where('status', 'eq', 'Completed')
      .orderBy('title', 'asc')
      .limit(10)
      .build();

    expect(result.query).toBe(
      'SELECT id, title, status WHERE status eq ORDER BY title ASC LIMIT 10',
    );
  });

  it('collects the bound values as params in order', () => {
    const result = new QueryBuilder<Job>()
      .select('id', 'status', 'photoCount')
      .where('status', 'eq', 'Scheduled')
      .where('photoCount', 'gt', 3)
      .build();

    expect(result.params).toEqual(['Scheduled', 3]);
  });

  it('accumulates the query as a template literal type', () => {
    const result = new QueryBuilder<Job>()
      .select('id', 'title')
      .where('title', 'like', 'roof')
      .orderBy('title', 'desc')
      .limit(5)
      .build();

    // Backticks, not quotes: a template literal *type* needs them. In single
    // quotes this is a plain string literal containing the characters
    // "${string}", and the assertion would compare against the wrong type.
    expectTypeOf(result.query).toEqualTypeOf<
      `SELECT ${string} WHERE title like ORDER BY title DESC LIMIT 5`
    >();
  });

  it('rejects a where on a field that was not selected', () => {
    const builder = new QueryBuilder<Job>().select('id', 'title');
    // @ts-expect-error photoCount was not selected
    builder.where('photoCount', 'gt', 1);
    expect(builder).toBeDefined();
  });

  it('rejects an orderBy on a field that was not selected', () => {
    const builder = new QueryBuilder<Job>().select('id', 'title');
    // @ts-expect-error status was not selected
    builder.orderBy('status', 'asc');
    expect(builder).toBeDefined();
  });

  it('rejects a value that does not match the field type', () => {
    const builder = new QueryBuilder<Job>().select('id', 'photoCount');
    // @ts-expect-error photoCount is a number
    builder.where('photoCount', 'eq', 'three');
    expect(builder).toBeDefined();
  });

  it('rejects a value outside a literal union field type', () => {
    const builder = new QueryBuilder<Job>().select('id', 'status');
    // @ts-expect-error 'Draft' is not a member of the status union
    builder.where('status', 'eq', 'Draft');
    expect(builder).toBeDefined();
  });
});
```

Note the third case: the template literal must carry the **specific** operator and direction. Without generic `Op` and `Dir` parameters the type would splice in the whole eight-member union and the assertion would fail.

- [ ] **Step 2: Run tests to verify they fail**

Run: `npm --prefix frontend test -- query-builder`
Expected: FAIL with `Cannot find module '../query-builder'`.

- [ ] **Step 3: Write `query-builder.ts`**

```ts
export type ComparisonOperator =
  | 'eq'
  | 'neq'
  | 'gt'
  | 'gte'
  | 'lt'
  | 'lte'
  | 'like'
  | 'in';

type Direction = 'asc' | 'desc';

export class QueryBuilder<
  T,
  Selected extends keyof T = keyof T,
  Query extends string = '',
> {
  readonly #query: string;
  readonly #params: readonly unknown[];

  constructor(query = '', params: readonly unknown[] = []) {
    this.#query = query;
    this.#params = params;
  }

  select<K extends keyof T & string>(
    ...fields: readonly [K, ...K[]]
  ): QueryBuilder<T, K, `SELECT ${string}`> {
    return new QueryBuilder<T, K, `SELECT ${string}`>(
      `SELECT ${fields.join(', ')}`,
      this.#params,
    );
  }

  where<K extends Selected & string, Op extends ComparisonOperator>(
    field: K,
    operator: Op,
    value: T[K],
  ): QueryBuilder<T, Selected, `${Query} WHERE ${K} ${Op}`> {
    return new QueryBuilder<T, Selected, `${Query} WHERE ${K} ${Op}`>(
      `${this.#query} WHERE ${field} ${operator}`,
      [...this.#params, value],
    );
  }

  orderBy<K extends Selected & string, Dir extends Direction>(
    field: K,
    direction: Dir,
  ): QueryBuilder<T, Selected, `${Query} ORDER BY ${K} ${Uppercase<Dir>}`> {
    return new QueryBuilder<T, Selected, `${Query} ORDER BY ${K} ${Uppercase<Dir>}`>(
      `${this.#query} ORDER BY ${field} ${direction.toUpperCase()}`,
      this.#params,
    );
  }

  limit<N extends number>(
    count: N,
  ): QueryBuilder<T, Selected, `${Query} LIMIT ${N}`> {
    return new QueryBuilder<T, Selected, `${Query} LIMIT ${N}`>(
      `${this.#query} LIMIT ${count}`,
      this.#params,
    );
  }

  build(): { query: Query; params: readonly unknown[] } {
    // The single assertion in this file. The runtime string is built by the
    // same steps that build `Query`, so the two agree by construction, but
    // the compiler cannot verify a string it did not compute. This is a
    // narrowing assertion, not the `as unknown as X` that CLAUDE.md forbids,
    // and the type test above is what keeps the two in step.
    return { query: this.#query as Query, params: this.#params };
  }
}
```

`Op`, `Dir` and `N` are generic parameters rather than plain ones. Written as `operator: ComparisonOperator`, the accumulated type would splice the whole union into the literal and produce eight strings, then sixteen, then thirty-two down the chain.

- [ ] **Step 4: Run tests and typecheck to verify green**

```bash
npm --prefix frontend test -- query-builder
npm --prefix frontend run typecheck
```

Expected: seven passing tests; no type errors.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/shared/query-builder
git commit -m "feat: add a chainable QueryBuilder that narrows at each step

select narrows Selected, so where and orderBy accept only selected
fields; value is typed T[K], so the third argument must match the
field's own type; and Query accumulates as a template literal.

Op, Dir and N are generic parameters rather than plain ones. Written as
operator: ComparisonOperator the accumulated type would splice the whole
eight-member union into the literal and fan out combinatorially down the
chain; capturing the argument's literal type is what makes the rendered
type assertable.

One narrowing assertion in build(), commented: the runtime string and
the type are built by the same steps and agree by construction, but the
compiler cannot verify a string it did not compute."
```

---

### Task 6: The job state machine

**Files:**
- Create: `frontend/src/core/domain/job/job-status.type.ts`
- Create: `frontend/src/core/domain/job/job-state.type.ts`
- Create: `frontend/src/core/domain/job/transition-job.ts`
- Create: `frontend/src/core/domain/job/get-job-summary.ts`
- Create: `frontend/src/core/domain/job/index.ts`
- Test: `frontend/src/core/domain/job/__tests__/transition-job.test.ts`
- Test: `frontend/src/core/domain/job/__tests__/get-job-summary.test.ts`

**Interfaces:**
- Consumes: nothing
- Produces: `JobStatus`, `JobState`, `JobAction`, `AllowedAction`, `transitionJob`, `getJobSummary`. Plan 2 asks `transitionJob` which actions a row may offer, so an invalid action cannot be rendered

- [ ] **Step 1: Write the failing transition test**

Create `frontend/src/core/domain/job/__tests__/transition-job.test.ts`:

```ts
import { transitionJob } from '../transition-job';
import type { JobState } from '../job-state.type';

const scheduledAt = new Date('2026-03-14T09:00:00Z');
const startedAt = new Date('2026-03-14T10:00:00Z');
const completedAt = new Date('2026-03-14T16:00:00Z');
const cancelledAt = new Date('2026-03-14T11:00:00Z');

const draft = { status: 'Draft' } as const satisfies JobState;
const scheduled = {
  status: 'Scheduled',
  scheduledDate: scheduledAt,
  assigneeId: 'a1',
} as const satisfies JobState;
const inProgress = {
  status: 'InProgress',
  startedAt,
  assigneeId: 'a1',
  photos: [],
} as const satisfies JobState;

describe('transitionJob', () => {
  it('moves Draft to Scheduled', () => {
    const next = transitionJob(draft, {
      type: 'SCHEDULE',
      scheduledDate: scheduledAt,
      assigneeId: 'a1',
    });

    expect(next).toEqual({
      status: 'Scheduled',
      scheduledDate: scheduledAt,
      assigneeId: 'a1',
    });
  });

  it('moves Scheduled to InProgress and keeps the assignee', () => {
    const next = transitionJob(scheduled, { type: 'START', startedAt });

    expect(next).toEqual({
      status: 'InProgress',
      startedAt,
      assigneeId: 'a1',
      photos: [],
    });
  });

  it('moves Scheduled to Cancelled with the reason', () => {
    const next = transitionJob(scheduled, {
      type: 'CANCEL',
      cancelledAt,
      reason: 'Weather',
    });

    expect(next).toEqual({ status: 'Cancelled', cancelledAt, reason: 'Weather' });
  });

  it('moves InProgress to Completed carrying photos and signature', () => {
    const withPhoto = { ...inProgress, photos: ['p1.jpg'] } as const;
    const next = transitionJob(withPhoto, {
      type: 'COMPLETE',
      completedAt,
      signatureUrl: 'sig.png',
    });

    expect(next).toEqual({
      status: 'Completed',
      startedAt,
      completedAt,
      assigneeId: 'a1',
      photos: ['p1.jpg'],
      signatureUrl: 'sig.png',
    });
  });

  it('moves InProgress to Cancelled', () => {
    const next = transitionJob(inProgress, {
      type: 'CANCEL',
      cancelledAt,
      reason: 'Customer withdrew',
    });

    expect(next.status).toBe('Cancelled');
  });

  it('refuses at compile time to start a Draft', () => {
    // @ts-expect-error Draft only allows SCHEDULE
    transitionJob(draft, { type: 'START', startedAt });
    expect(true).toBe(true);
  });

  it('refuses at compile time to complete a Scheduled job', () => {
    // @ts-expect-error BR-3: only a Scheduled job can start, and only an
    // InProgress job can complete
    transitionJob(scheduled, { type: 'COMPLETE', completedAt, signatureUrl: 's' });
    expect(true).toBe(true);
  });

  it('refuses at compile time to transition out of Completed', () => {
    const completed = {
      status: 'Completed',
      startedAt,
      completedAt,
      assigneeId: 'a1',
      photos: [],
      signatureUrl: 'sig.png',
    } as const satisfies JobState;

    // @ts-expect-error Completed is terminal: ActionFor<Completed> is never
    transitionJob(completed, { type: 'CANCEL', cancelledAt, reason: 'no' });
    expect(true).toBe(true);
  });

  it('refuses at compile time to transition out of Cancelled', () => {
    const cancelled = {
      status: 'Cancelled',
      cancelledAt,
      reason: 'Weather',
    } as const satisfies JobState;

    // @ts-expect-error Cancelled is terminal
    transitionJob(cancelled, {
      type: 'SCHEDULE',
      scheduledDate: scheduledAt,
      assigneeId: 'a1',
    });
    expect(true).toBe(true);
  });
});
```

The four `@ts-expect-error` cases are the point of the exercise: assessment line 87 requires invalid transitions to be **compile-time** errors, and `@ts-expect-error` fails the build if any of them ever starts compiling.

- [ ] **Step 2: Run typecheck to verify it fails**

Run: `npm --prefix frontend run typecheck`
Expected: FAIL with `Cannot find module '../transition-job'`.

- [ ] **Step 3: Write `job-status.type.ts`**

```ts
export type JobStatus =
  | 'Draft'
  | 'Scheduled'
  | 'InProgress'
  | 'Completed'
  | 'Cancelled';
```

- [ ] **Step 4: Write `job-state.type.ts`**

```ts
export type JobState =
  | { readonly status: 'Draft'; readonly notes?: string }
  | {
      readonly status: 'Scheduled';
      readonly scheduledDate: Date;
      readonly assigneeId: string;
    }
  | {
      readonly status: 'InProgress';
      readonly startedAt: Date;
      readonly assigneeId: string;
      readonly photos: readonly string[];
    }
  | {
      readonly status: 'Completed';
      readonly startedAt: Date;
      readonly completedAt: Date;
      readonly assigneeId: string;
      readonly photos: readonly string[];
      readonly signatureUrl: string;
    }
  | {
      readonly status: 'Cancelled';
      readonly cancelledAt: Date;
      readonly reason: string;
    };

export type JobAction =
  | { readonly type: 'SCHEDULE'; readonly scheduledDate: Date; readonly assigneeId: string }
  | { readonly type: 'START'; readonly startedAt: Date }
  | { readonly type: 'COMPLETE'; readonly completedAt: Date; readonly signatureUrl: string }
  | { readonly type: 'CANCEL'; readonly cancelledAt: Date; readonly reason: string };

/** The transition table, in the type system rather than in a switch. */
export type AllowedAction = {
  Draft: 'SCHEDULE';
  Scheduled: 'START' | 'CANCEL';
  InProgress: 'COMPLETE' | 'CANCEL';
  Completed: never;
  Cancelled: never;
};

type TransitionTarget = {
  SCHEDULE: 'Scheduled';
  START: 'InProgress';
  COMPLETE: 'Completed';
  CANCEL: 'Cancelled';
};

export type ActionFor<S extends JobState> = Extract<
  JobAction,
  { type: AllowedAction[S['status']] }
>;

export type ResultOf<A extends JobAction> = Extract<
  JobState,
  { status: TransitionTarget[A['type']] }
>;
```

- [ ] **Step 5: Write `transition-job.ts`**

```ts
import type { ActionFor, JobAction, JobState, ResultOf } from './job-state.type';

/**
 * The signature on assessment line 81 — `transitionJob(current: JobState, …)` —
 * accepts the entire union, so every state is assignable and no call can ever
 * be a compile error. It cannot satisfy line 87. Constraining `A` by the
 * *narrowed* type of `current` is what makes an invalid pair unrepresentable:
 * for a terminal state `AllowedAction[S['status']]` is `never`, so `ActionFor<S>`
 * is `never` and no argument can satisfy `A`.
 *
 * The consequence, stated plainly: the caller must narrow before calling.
 */
export function transitionJob<S extends JobState, A extends ActionFor<S>>(
  current: S,
  action: A,
): ResultOf<A> {
  switch (action.type) {
    case 'SCHEDULE':
      return {
        status: 'Scheduled',
        scheduledDate: action.scheduledDate,
        assigneeId: action.assigneeId,
      } as ResultOf<A>;

    case 'START': {
      // `ActionFor<S>` guarantees START only reaches a Scheduled state, and
      // narrowing `current` here is what lets the compiler see its assignee
      // rather than a helper inventing a fallback for a case that cannot occur.
      const from = narrow(current, 'Scheduled');
      return {
        status: 'InProgress',
        startedAt: action.startedAt,
        assigneeId: from.assigneeId,
        photos: [],
      } as ResultOf<A>;
    }

    case 'COMPLETE': {
      const from = narrow(current, 'InProgress');
      return {
        status: 'Completed',
        startedAt: from.startedAt,
        completedAt: action.completedAt,
        assigneeId: from.assigneeId,
        photos: from.photos,
        signatureUrl: action.signatureUrl,
      } as ResultOf<A>;
    }

    case 'CANCEL':
      return {
        status: 'Cancelled',
        cancelledAt: action.cancelledAt,
        reason: action.reason,
      } as ResultOf<A>;
  }
}

/**
 * Re-narrows `current` to the state the action's precondition already
 * guarantees. It throws rather than substituting a default, because reaching
 * the throw would mean the type-level transition table and this switch had
 * drifted apart — a defect, not an expected failure.
 */
function narrow<K extends JobState['status']>(
  state: JobState,
  status: K,
): Extract<JobState, { status: K }> {
  if (state.status !== status) {
    throw new Error(`transitionJob: expected ${status}, received ${state.status}`);
  }
  return state as Extract<JobState, { status: K }>;
}

export type { JobAction };
```

The `as ResultOf<A>` assertions are unavoidable and safe: `ResultOf<A>` is a conditional type over the *caller's* `A`, which the compiler cannot resolve inside a generic function body. Each branch returns exactly the state `TransitionTarget` maps that action to; the tests are what hold the two in agreement. They are narrowing assertions, not `as unknown as X`.

`narrow` replaces three `'x' in state ? state.x : fallback` helpers. Those had a
branch that no valid call could reach — `START` only arrives at a `Scheduled`
state — which is both a smell and a coverage problem, since an unreachable
branch can never be exercised and drags branch coverage below the 80% gate.
Throwing on the impossible case is the honest version: `CLAUDE.md` reserves
exceptions for defects, and a drift between the transition table and this switch
is exactly that.

- [ ] **Step 6: Run typecheck and tests to verify green**

```bash
npm --prefix frontend run typecheck
npm --prefix frontend test -- transition-job
```

Expected: no type errors; nine passing tests.

- [ ] **Step 7: Write the failing summary test**

Create `frontend/src/core/domain/job/__tests__/get-job-summary.test.ts`:

```ts
import { getJobSummary } from '../get-job-summary';
import type { JobState } from '../job-state.type';

describe('getJobSummary', () => {
  it.each<[JobState, string]>([
    [{ status: 'Draft' }, 'Draft, not yet scheduled'],
    [
      {
        status: 'Scheduled',
        scheduledDate: new Date('2026-03-14T00:00:00Z'),
        assigneeId: 'a1',
      },
      'Scheduled for 2026-03-14, assigned to a1',
    ],
    [
      {
        status: 'InProgress',
        startedAt: new Date('2026-03-14T10:00:00Z'),
        assigneeId: 'a1',
        photos: ['p1', 'p2'],
      },
      'In progress since 2026-03-14, 2 photos',
    ],
    [
      {
        status: 'Completed',
        startedAt: new Date('2026-03-14T10:00:00Z'),
        completedAt: new Date('2026-03-14T16:00:00Z'),
        assigneeId: 'a1',
        photos: [],
        signatureUrl: 'sig.png',
      },
      'Completed on 2026-03-14, signed',
    ],
    [
      {
        status: 'Cancelled',
        cancelledAt: new Date('2026-03-14T11:00:00Z'),
        reason: 'Weather',
      },
      'Cancelled on 2026-03-14: Weather',
    ],
  ])('summarises %o', (state, expected) => {
    expect(getJobSummary(state)).toBe(expected);
  });

  it('includes the optional Draft note when present', () => {
    expect(getJobSummary({ status: 'Draft', notes: 'call first' })).toBe(
      'Draft, not yet scheduled: call first',
    );
  });
});
```

- [ ] **Step 8: Run tests to verify they fail**

Run: `npm --prefix frontend test -- get-job-summary`
Expected: FAIL with `Cannot find module '../get-job-summary'`.

- [ ] **Step 9: Write `get-job-summary.ts`**

```ts
import type { JobState } from './job-state.type';

const asDay = (value: Date): string => value.toISOString().slice(0, 10);

export function getJobSummary(state: JobState): string {
  switch (state.status) {
    case 'Draft':
      return state.notes === undefined
        ? 'Draft, not yet scheduled'
        : `Draft, not yet scheduled: ${state.notes}`;

    case 'Scheduled':
      return `Scheduled for ${asDay(state.scheduledDate)}, assigned to ${state.assigneeId}`;

    case 'InProgress':
      return `In progress since ${asDay(state.startedAt)}, ${state.photos.length} photos`;

    case 'Completed':
      return `Completed on ${asDay(state.completedAt)}, signed`;

    case 'Cancelled':
      return `Cancelled on ${asDay(state.cancelledAt)}: ${state.reason}`;

    default: {
      // The never trick assessment line 89 asks for: adding a state to
      // JobState breaks the build at this line rather than at runtime.
      const exhaustive: never = state;
      return exhaustive;
    }
  }
}
```

- [ ] **Step 10: Write the barrel `index.ts`**

```ts
export type { JobStatus } from './job-status.type';
export type {
  ActionFor,
  AllowedAction,
  JobAction,
  JobState,
  ResultOf,
} from './job-state.type';
export { transitionJob } from './transition-job';
export { getJobSummary } from './get-job-summary';
```

- [ ] **Step 11: Run the full suite to verify green**

```bash
npm --prefix frontend test
npm --prefix frontend run typecheck
npm --prefix frontend run lint
```

Expected: every test passing, no type errors, no lint errors.

- [ ] **Step 12: Commit**

```bash
git add frontend/src/core/domain/job
git commit -m "feat: add the job state machine with compile-time transitions

JobState is a discriminated union carrying exactly the data each state
holds, and the transition table lives in the type system rather than in
a switch.

transitionJob takes a generic S constrained to the narrowed current
state, not the union. The signature on line 81 accepts every state, so
no call could ever be a compile error and it cannot satisfy line 87;
constraining A by ActionFor<S> makes an invalid pair unrepresentable,
and for a terminal state ActionFor<S> is never so no argument fits. Four
@ts-expect-error cases hold that guarantee: the build breaks if starting
a Draft or cancelling a Completed job ever starts compiling.

getJobSummary closes with a never assignment, so adding a state breaks
the build at that line."
```

---

### Task 7: Result and the transfer shapes

**Files:**
- Create: `frontend/src/core/domain/result.type.ts`
- Create: `frontend/src/core/domain/job/job-summary.type.ts`
- Test: `frontend/src/core/domain/__tests__/result.test.ts`

**Interfaces:**
- Consumes: `JobStatus` from task 6
- Produces: `Result<T, E>`, `ok<T>()`, `err<E>()`, `isOk`, `CoreError`, `Party`, `JobSummary`, `JobDetail`. Every port method and use case in tasks 8 and 9 returns `Result<_, CoreError>`

- [ ] **Step 1: Write the failing test**

Create `frontend/src/core/domain/__tests__/result.test.ts`:

```ts
import { err, isOk, ok } from '../result.type';
import type { CoreError } from '../result.type';

const failure: CoreError = {
  code: 'job.not-found',
  message: 'No such job',
  kind: 'not-found',
};

describe('Result', () => {
  it('carries a value on success', () => {
    const result = ok(42);
    expect(result.ok).toBe(true);
    expect(isOk(result)).toBe(true);
    if (isOk(result)) {
      expect(result.value).toBe(42);
    }
  });

  it('carries an error on failure', () => {
    const result = err(failure);
    expect(result.ok).toBe(false);
    expect(isOk(result)).toBe(false);
    if (!isOk(result)) {
      expect(result.error.kind).toBe('not-found');
    }
  });

  it('narrows the union through the guard', () => {
    const result = Math.random() > 2 ? ok('yes') : err(failure);
    const rendered = isOk(result) ? result.value : result.error.message;
    expect(typeof rendered).toBe('string');
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `npm --prefix frontend test -- result`
Expected: FAIL with `Cannot find module '../result.type'`.

- [ ] **Step 3: Write `result.type.ts`**

```ts
export type CoreErrorKind =
  | 'validation'
  | 'not-found'
  | 'conflict'
  | 'unauthorized'
  | 'failure';

export type CoreError = {
  readonly code: string;
  readonly message: string;
  readonly kind: CoreErrorKind;
  readonly fieldErrors?: Readonly<Record<string, string>>;
};

export type Result<T, E = CoreError> =
  | { readonly ok: true; readonly value: T }
  | { readonly ok: false; readonly error: E };

export const ok = <T>(value: T): Result<T, never> => ({ ok: true, value });

export const err = <E>(error: E): Result<never, E> => ({ ok: false, error });

export const isOk = <T, E>(
  result: Result<T, E>,
): result is { readonly ok: true; readonly value: T } => result.ok;
```

A discriminated union mirroring the backend's `Result`, so both sides speak the same failure vocabulary. `kind` is what `HttpJobsAdapter` maps `ProblemDetails` onto in plan 3.

- [ ] **Step 4: Write `job-summary.type.ts`**

```ts
import type { JobStatus } from './job-status.type';

export type Party = { readonly id: string; readonly name: string };

export type JobSummary = {
  readonly id: string;
  readonly title: string;
  readonly status: JobStatus;
  readonly scheduledDate: string;
  readonly assigneeId: string;
  readonly assigneeName: string;
  readonly address: {
    readonly street: string;
    readonly city: string;
    readonly state: string;
  };
  readonly photoCount: number;
};

export type JobPhoto = {
  readonly id: string;
  readonly url: string;
  readonly capturedAt: string;
  readonly caption: string | null;
};

export type JobDetail = JobSummary & {
  readonly description: string | null;
  readonly address: {
    readonly street: string;
    readonly city: string;
    readonly state: string;
    readonly zipCode: string;
    readonly latitude: number;
    readonly longitude: number;
  };
  readonly startedAt: string | null;
  readonly completedAt: string | null;
  readonly signatureUrl: string | null;
  readonly photos: readonly JobPhoto[];
};
```

Dates cross the boundary as ISO strings, not `Date`: they arrive from JSON and a `Date` in a Server Component prop would not survive serialisation.

- [ ] **Step 5: Run tests and typecheck to verify green**

```bash
npm --prefix frontend test -- result
npm --prefix frontend run typecheck
```

Expected: three passing tests; no type errors.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/core/domain
git commit -m "feat: add Result and the transfer shapes

Result is a discriminated union mirroring the backend's, so both sides
speak one failure vocabulary; kind is what HttpJobsAdapter will map
ProblemDetails onto.

Dates cross the boundary as ISO strings rather than Date, because they
arrive from JSON and a Date passed as a Server Component prop would not
survive serialisation."
```

---

### Task 8: `JobsPort` and the in-memory adapter

**Files:**
- Create: `frontend/src/core/application/ports/jobs.port.ts`
- Create: `frontend/src/infrastructure/adapters/in-memory-jobs.adapter.ts`
- Create: `frontend/src/infrastructure/adapters/seed.ts`
- Test: `frontend/src/infrastructure/adapters/__tests__/in-memory-jobs.adapter.test.ts`

**Interfaces:**
- Consumes: `Result`, `CoreError`, `JobSummary`, `JobDetail`, `Party`, `JobStatus` (tasks 6-7)
- Produces: `JobsPort`, `JobSearchQuery`, `PagedJobs`, `CreateJobInput`, `CompleteJobInput`, `createInMemoryJobsAdapter()`. Task 9's use cases depend on `JobsPort`; plan 2's Playwright suite runs against this adapter

- [ ] **Step 1: Write the failing test**

Create `frontend/src/infrastructure/adapters/__tests__/in-memory-jobs.adapter.test.ts`:

```ts
import { isOk } from '@/core/domain/result.type';
import { createInMemoryJobsAdapter } from '../in-memory-jobs.adapter';

import type { CoreError, Result } from '@/core/domain/result.type';

// No cast: isOk is a type guard, so narrowing is what produces the value.
const unwrap = <T>(result: Result<T, CoreError>): T => {
  if (!isOk(result)) {
    throw new Error(`expected ok, received ${JSON.stringify(result.error)}`);
  }
  return result.value;
};

const validInput = {
  title: 'Roof repair',
  description: 'Replace ridge tiles',
  address: {
    street: '12 Elm St',
    city: 'Springfield',
    state: 'IL',
    zipCode: '62701',
    latitude: 39.78,
    longitude: -89.65,
  },
  scheduledDate: '2099-03-14',
  assigneeId: 'assignee-1',
  customerId: 'customer-1',
};

describe('InMemoryJobsAdapter', () => {
  it('returns seeded jobs newest first', async () => {
    const adapter = createInMemoryJobsAdapter();
    const result = await adapter.search({ limit: 10 });

    expect(isOk(result)).toBe(true);
    const page = unwrap(result);
    const dates = page.items.map((job) => job.scheduledDate);
    expect([...dates]).toEqual([...dates].sort().reverse());
  });

  it('creates a job and returns it in state Scheduled', async () => {
    const adapter = createInMemoryJobsAdapter();
    const created = await adapter.create(validInput);
    const id = unwrap(created);

    const page = unwrap(
      await adapter.search({ limit: 50 }),
    );
    expect(page.items.find((job) => job.id === id)?.status).toBe('Scheduled');
  });

  it('filters by status', async () => {
    const adapter = createInMemoryJobsAdapter();
    const page = unwrap(
      await adapter.search({ limit: 50, statuses: ['Completed'] }),
    );

    expect(page.items.length).toBeGreaterThan(0);
    expect(page.items.every((job) => job.status === 'Completed')).toBe(true);
  });

  it('filters by free text over title and description', async () => {
    const adapter = createInMemoryJobsAdapter();
    unwrap(await adapter.create(validInput));

    const page = unwrap(
      await adapter.search({ limit: 50, text: 'ridge' }),
    );
    expect(page.items.map((job) => job.title)).toContain('Roof repair');
  });

  it('filters by assignee', async () => {
    const adapter = createInMemoryJobsAdapter();
    unwrap(await adapter.create(validInput));

    const page = unwrap(
      await adapter.search({ limit: 50, assigneeId: 'assignee-1' }),
    );
    expect(page.items.every((job) => job.assigneeId === 'assignee-1')).toBe(true);
  });

  it('pages with a cursor without repeating rows', async () => {
    const adapter = createInMemoryJobsAdapter();
    const first = unwrap(
      await adapter.search({ limit: 2 }),
    );
    expect(first.nextCursor).not.toBeNull();

    const second = unwrap(
      await adapter.search({ limit: 2, cursor: first.nextCursor }),
    );

    const firstIds = first.items.map((job) => job.id);
    const secondIds = second.items.map((job) => job.id);
    expect(firstIds.some((id) => secondIds.includes(id))).toBe(false);
  });

  it('starts a Scheduled job', async () => {
    const adapter = createInMemoryJobsAdapter();
    const id = unwrap(await adapter.create(validInput));

    expect(isOk(await adapter.start(id))).toBe(true);
    const detail = unwrap(await adapter.getById(id));
    expect(detail.status).toBe('InProgress');
  });

  it('refuses to start a job that is not Scheduled (BR-3)', async () => {
    const adapter = createInMemoryJobsAdapter();
    const id = unwrap(await adapter.create(validInput));
    await adapter.start(id);

    const again = await adapter.start(id);
    expect(isOk(again)).toBe(false);
    if (!isOk(again)) expect(again.error.kind).toBe('conflict');
  });

  it('completes an InProgress job with a signature', async () => {
    const adapter = createInMemoryJobsAdapter();
    const id = unwrap(await adapter.create(validInput));
    await adapter.start(id);

    const done = await adapter.complete(id, {
      signatureUrl: 'data:image/png;base64,AAA',
      photos: [{ url: 'p1.jpg', caption: 'ridge' }],
    });

    expect(isOk(done)).toBe(true);
    const detail = unwrap(
      await adapter.getById(id),
    );
    expect(detail.status).toBe('Completed');
    expect(detail.photos).toHaveLength(1);
  });

  it('refuses to complete without a signature (BR-4)', async () => {
    const adapter = createInMemoryJobsAdapter();
    const id = unwrap(await adapter.create(validInput));
    await adapter.start(id);

    const done = await adapter.complete(id, { signatureUrl: '', photos: [] });
    expect(isOk(done)).toBe(false);
    if (!isOk(done)) expect(done.error.kind).toBe('validation');
  });

  it('refuses to cancel without a reason (BR-5)', async () => {
    const adapter = createInMemoryJobsAdapter();
    const id = unwrap(await adapter.create(validInput));

    const cancelled = await adapter.cancel(id, '   ');
    expect(isOk(cancelled)).toBe(false);
    if (!isOk(cancelled)) expect(cancelled.error.kind).toBe('validation');
  });

  it('refuses to change a terminal job (BR-2)', async () => {
    const adapter = createInMemoryJobsAdapter();
    const id = unwrap(await adapter.create(validInput));
    await adapter.cancel(id, 'Weather');

    const started = await adapter.start(id);
    expect(isOk(started)).toBe(false);
    if (!isOk(started)) expect(started.error.kind).toBe('conflict');
  });

  it('refuses a scheduled date in the past (BR-1)', async () => {
    const adapter = createInMemoryJobsAdapter();
    const created = await adapter.create({ ...validInput, scheduledDate: '2000-01-01' });

    expect(isOk(created)).toBe(false);
    if (!isOk(created)) {
      expect(created.error.kind).toBe('validation');
      expect(created.error.fieldErrors?.scheduledDate).toBeDefined();
    }
  });

  it('reports not-found for an unknown id', async () => {
    const adapter = createInMemoryJobsAdapter();
    const result = await adapter.getById('missing');

    expect(isOk(result)).toBe(false);
    if (!isOk(result)) expect(result.error.kind).toBe('not-found');
  });

  it('lists the assignee and customer rosters', async () => {
    const adapter = createInMemoryJobsAdapter();
    const assignees = unwrap(await adapter.assignees());
    const customers = unwrap(await adapter.customers());

    expect(assignees.length).toBeGreaterThan(0);
    expect(customers.length).toBeGreaterThan(0);
  });
});
```

The adapter reproduces the backend's refusals rather than being a happy-path stub. That is what makes it substitutable for `HttpJobsAdapter` in the Liskov sense: a caller written against one cannot be surprised by the other.

- [ ] **Step 2: Run tests to verify they fail**

Run: `npm --prefix frontend test -- in-memory-jobs`
Expected: FAIL with `Cannot find module '../in-memory-jobs.adapter'`.

- [ ] **Step 3: Write `jobs.port.ts`**

```ts
import type { CoreError, Result } from '@/core/domain/result.type';
import type { JobStatus } from '@/core/domain/job/job-status.type';
import type {
  JobDetail,
  JobSummary,
  Party,
} from '@/core/domain/job/job-summary.type';

export type JobSortField = 'scheduledDate' | 'title';

export type JobSearchQuery = {
  readonly text?: string;
  readonly statuses?: readonly JobStatus[];
  readonly scheduledFrom?: string;
  readonly scheduledTo?: string;
  readonly assigneeId?: string;
  readonly sort?: JobSortField;
  readonly cursor?: string | null;
  readonly limit: number;
};

export type PagedJobs = {
  readonly items: readonly JobSummary[];
  readonly nextCursor: string | null;
};

export type CreateJobInput = {
  readonly title: string;
  readonly description: string;
  readonly address: {
    readonly street: string;
    readonly city: string;
    readonly state: string;
    readonly zipCode: string;
    readonly latitude: number;
    readonly longitude: number;
  };
  readonly scheduledDate: string;
  readonly assigneeId: string;
  readonly customerId: string;
};

export type NewPhoto = { readonly url: string; readonly caption: string | null };

export type CompleteJobInput = {
  readonly signatureUrl: string;
  readonly photos: readonly NewPhoto[];
};

export interface JobsPort {
  search(query: JobSearchQuery): Promise<Result<PagedJobs, CoreError>>;
  getById(id: string): Promise<Result<JobDetail, CoreError>>;
  create(input: CreateJobInput): Promise<Result<string, CoreError>>;
  start(id: string): Promise<Result<void, CoreError>>;
  complete(id: string, input: CompleteJobInput): Promise<Result<void, CoreError>>;
  cancel(id: string, reason: string): Promise<Result<void, CoreError>>;
  assignees(): Promise<Result<readonly Party[], CoreError>>;
  customers(): Promise<Result<readonly Party[], CoreError>>;
}
```

- [ ] **Step 4: Write `seed.ts`**

```ts
import type { Party } from '@/core/domain/job/job-summary.type';
import type { JobStatus } from '@/core/domain/job/job-status.type';

export const SEED_ASSIGNEES: readonly Party[] = [
  { id: 'assignee-1', name: 'J. Ortiz' },
  { id: 'assignee-2', name: 'M. Ruiz' },
];

export const SEED_CUSTOMERS: readonly Party[] = [
  { id: 'customer-1', name: 'Acme Holdings' },
  { id: 'customer-2', name: 'Birch Property' },
];

export type SeedJob = {
  id: string;
  title: string;
  description: string;
  status: JobStatus;
  scheduledDate: string;
  assigneeId: string;
  customerId: string;
  address: {
    street: string;
    city: string;
    state: string;
    zipCode: string;
    latitude: number;
    longitude: number;
  };
  startedAt: string | null;
  completedAt: string | null;
  cancelledAt: string | null;
  cancellationReason: string | null;
  signatureUrl: string | null;
  photos: { id: string; url: string; capturedAt: string; caption: string | null }[];
};

const address = (street: string) => ({
  street,
  city: 'Springfield',
  state: 'IL',
  zipCode: '62701',
  latitude: 39.78,
  longitude: -89.65,
});

export const seedJobs = (): SeedJob[] => [
  {
    id: 'job-1',
    title: 'Ridge tile replacement',
    description: 'Replace cracked ridge tiles on the north slope',
    status: 'Scheduled',
    scheduledDate: '2099-03-14',
    assigneeId: 'assignee-1',
    customerId: 'customer-1',
    address: address('12 Elm St'),
    startedAt: null,
    completedAt: null,
    cancelledAt: null,
    cancellationReason: null,
    signatureUrl: null,
    photos: [],
  },
  {
    id: 'job-2',
    title: 'Gutter reline',
    description: 'Reline the rear gutter run',
    status: 'InProgress',
    scheduledDate: '2099-03-13',
    assigneeId: 'assignee-2',
    customerId: 'customer-2',
    address: address('8 Oak Ave'),
    startedAt: '2099-03-13T09:00:00.000Z',
    completedAt: null,
    cancelledAt: null,
    cancellationReason: null,
    signatureUrl: null,
    photos: [],
  },
  {
    id: 'job-3',
    title: 'Shingle swap',
    description: 'Swap storm-damaged shingles',
    status: 'Completed',
    scheduledDate: '2099-03-09',
    assigneeId: 'assignee-1',
    customerId: 'customer-1',
    address: address('44 Pine Rd'),
    startedAt: '2099-03-09T08:00:00.000Z',
    completedAt: '2099-03-09T15:30:00.000Z',
    cancelledAt: null,
    cancellationReason: null,
    signatureUrl: 'data:image/png;base64,seed',
    photos: [
      {
        id: 'photo-1',
        url: 'https://example.invalid/photo-1.jpg',
        capturedAt: '2099-03-09T14:00:00.000Z',
        caption: 'After',
      },
    ],
  },
  {
    id: 'job-4',
    title: 'Flashing inspection',
    description: 'Inspect chimney flashing after leak report',
    status: 'Cancelled',
    scheduledDate: '2099-03-08',
    assigneeId: 'assignee-2',
    customerId: 'customer-2',
    address: address('3 Cedar Ln'),
    startedAt: null,
    completedAt: null,
    cancelledAt: '2099-03-07T12:00:00.000Z',
    cancellationReason: 'Customer withdrew',
    signatureUrl: null,
    photos: [],
  },
];
```

Seed dates are in 2099 so `BR-1` never rejects a seeded job as the real clock advances. The roster ids match what plan 3 seeds into `jobs.assignees` and `jobs.customers`, so the two adapters agree.

- [ ] **Step 5: Write `in-memory-jobs.adapter.ts`**

```ts
import { err, ok } from '@/core/domain/result.type';
import type { CoreError, Result } from '@/core/domain/result.type';
import type { JobDetail, JobSummary, Party } from '@/core/domain/job/job-summary.type';
import type {
  CompleteJobInput,
  CreateJobInput,
  JobSearchQuery,
  JobsPort,
  PagedJobs,
} from '@/core/application/ports/jobs.port';
import { SEED_ASSIGNEES, SEED_CUSTOMERS, seedJobs } from './seed';
import type { SeedJob } from './seed';

const conflict = (message: string): CoreError => ({
  code: 'job.conflict',
  message,
  kind: 'conflict',
});

const notFound = (): CoreError => ({
  code: 'job.not-found',
  message: 'No job with that identifier',
  kind: 'not-found',
});

const invalid = (message: string, field: string): CoreError => ({
  code: 'job.validation',
  message,
  kind: 'validation',
  fieldErrors: { [field]: message },
});

const nameOf = (roster: readonly Party[], id: string): string =>
  roster.find((party) => party.id === id)?.name ?? 'Unassigned';

export function createInMemoryJobsAdapter(): JobsPort {
  const jobs: SeedJob[] = seedJobs();
  let sequence = 0;

  const toSummary = (job: SeedJob): JobSummary => ({
    id: job.id,
    title: job.title,
    status: job.status,
    scheduledDate: job.scheduledDate,
    assigneeId: job.assigneeId,
    assigneeName: nameOf(SEED_ASSIGNEES, job.assigneeId),
    address: { street: job.address.street, city: job.address.city, state: job.address.state },
    photoCount: job.photos.length,
  });

  const find = (id: string): SeedJob | undefined => jobs.find((job) => job.id === id);

  return {
    async search(query: JobSearchQuery): Promise<Result<PagedJobs, CoreError>> {
      const text = query.text?.trim().toLowerCase();

      const matched = jobs
        .filter((job) => (query.statuses ? query.statuses.includes(job.status) : true))
        .filter((job) => (query.assigneeId ? job.assigneeId === query.assigneeId : true))
        .filter((job) => (query.scheduledFrom ? job.scheduledDate >= query.scheduledFrom : true))
        .filter((job) => (query.scheduledTo ? job.scheduledDate <= query.scheduledTo : true))
        .filter((job) =>
          text === undefined || text === ''
            ? true
            : `${job.title} ${job.description}`.toLowerCase().includes(text),
        )
        .sort((left, right) =>
          query.sort === 'title'
            ? left.title.localeCompare(right.title) || left.id.localeCompare(right.id)
            : right.scheduledDate.localeCompare(left.scheduledDate) ||
              right.id.localeCompare(left.id),
        );

      const start = query.cursor === null || query.cursor === undefined
        ? 0
        : matched.findIndex((job) => job.id === query.cursor) + 1;

      const page = matched.slice(start, start + query.limit);
      const consumed = start + page.length;

      return ok({
        items: page.map(toSummary),
        nextCursor: consumed < matched.length ? page[page.length - 1].id : null,
      });
    },

    async getById(id: string): Promise<Result<JobDetail, CoreError>> {
      const job = find(id);
      if (job === undefined) return err(notFound());

      return ok({
        ...toSummary(job),
        description: job.description === '' ? null : job.description,
        address: job.address,
        startedAt: job.startedAt,
        completedAt: job.completedAt,
        signatureUrl: job.signatureUrl,
        photos: job.photos,
      });
    },

    async create(input: CreateJobInput): Promise<Result<string, CoreError>> {
      const today = new Date().toISOString().slice(0, 10);
      if (input.scheduledDate < today) {
        return err(invalid('A job cannot be scheduled in the past', 'scheduledDate'));
      }
      if (input.title.trim() === '') {
        return err(invalid('A title is required', 'title'));
      }

      sequence += 1;
      const id = `job-new-${sequence}`;
      jobs.push({
        id,
        title: input.title,
        description: input.description,
        status: 'Scheduled',
        scheduledDate: input.scheduledDate,
        assigneeId: input.assigneeId,
        customerId: input.customerId,
        address: input.address,
        startedAt: null,
        completedAt: null,
        cancelledAt: null,
        cancellationReason: null,
        signatureUrl: null,
        photos: [],
      });

      return ok(id);
    },

    async start(id: string): Promise<Result<void, CoreError>> {
      const job = find(id);
      if (job === undefined) return err(notFound());
      if (job.status !== 'Scheduled') {
        return err(conflict('Only a Scheduled job can start'));
      }

      job.status = 'InProgress';
      job.startedAt = new Date().toISOString();
      return ok(undefined);
    },

    async complete(id: string, input: CompleteJobInput): Promise<Result<void, CoreError>> {
      const job = find(id);
      if (job === undefined) return err(notFound());
      if (input.signatureUrl.trim() === '') {
        return err(invalid('A customer signature is required', 'signatureUrl'));
      }
      if (job.status !== 'InProgress') {
        return err(conflict('Only a job in progress can be completed'));
      }

      job.status = 'Completed';
      job.completedAt = new Date().toISOString();
      job.signatureUrl = input.signatureUrl;
      job.photos = input.photos.map((photo, index) => ({
        id: `${id}-photo-${index + 1}`,
        url: photo.url,
        capturedAt: new Date().toISOString(),
        caption: photo.caption,
      }));
      return ok(undefined);
    },

    async cancel(id: string, reason: string): Promise<Result<void, CoreError>> {
      const job = find(id);
      if (job === undefined) return err(notFound());
      if (reason.trim() === '') {
        return err(invalid('A cancellation reason is required', 'reason'));
      }
      if (job.status === 'Completed' || job.status === 'Cancelled') {
        return err(conflict('A job in a terminal state cannot change state'));
      }

      job.status = 'Cancelled';
      job.cancelledAt = new Date().toISOString();
      job.cancellationReason = reason;
      return ok(undefined);
    },

    async assignees(): Promise<Result<readonly Party[], CoreError>> {
      return ok(SEED_ASSIGNEES);
    },

    async customers(): Promise<Result<readonly Party[], CoreError>> {
      return ok(SEED_CUSTOMERS);
    },
  };
}
```

Validation precedes the state check in `complete` and `cancel` on purpose: a missing signature is a validation failure whatever the job's state, and the tests assert that ordering.

- [ ] **Step 6: Run tests, typecheck and lint to verify green**

```bash
npm --prefix frontend test -- in-memory-jobs
npm --prefix frontend run typecheck
npm --prefix frontend run lint
```

Expected: fifteen passing tests; no type errors; no lint errors.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/core/application frontend/src/infrastructure
git commit -m "feat: add JobsPort and the in-memory adapter

The port declares the eight operations the use cases need, in domain
terms. The in-memory adapter implements them over a seeded array and
reproduces the backend's refusals rather than being a happy-path stub:
BR-1 to BR-5 each return the CoreError kind the HTTP adapter will map
from ProblemDetails.

That behavioural agreement is what makes the two substitutable in the
Liskov sense, and it is what lets the Playwright suite in plan 2 run
with no backend and no database.

Seed dates sit in 2099 so BR-1 never rejects a seeded job as the clock
advances, and the roster ids match what plan 3 seeds into jobs.assignees
and jobs.customers."
```

---

### Task 9: Use cases and the DI container

**Files:**
- Create: `frontend/src/core/application/use-cases/search-jobs.use-case.ts`
- Create: `frontend/src/core/application/use-cases/get-job.use-case.ts`
- Create: `frontend/src/core/application/use-cases/create-job.use-case.ts`
- Create: `frontend/src/core/application/use-cases/start-job.use-case.ts`
- Create: `frontend/src/core/application/use-cases/complete-job.use-case.ts`
- Create: `frontend/src/core/application/use-cases/cancel-job.use-case.ts`
- Create: `frontend/src/core/application/use-cases/list-parties.use-case.ts`
- Create: `frontend/src/core/di/container.ts`
- Test: `frontend/src/core/application/__tests__/use-cases.test.ts`
- Test: `frontend/src/core/di/__tests__/container.test.ts`

**Interfaces:**
- Consumes: `JobsPort` and its input types (task 8)
- Produces: `searchJobs`, `getJob`, `createJob`, `startJob`, `completeJob`, `cancelJob`, `listAssignees`, `listCustomers`, each taking a `JobsPort` as its first argument; and `getContainer()` returning `{ jobs: JobsPort }`. Plan 2's `page.tsx` calls `container.jobs` through `searchJobs`

- [ ] **Step 1: Write the failing use-case test**

Create `frontend/src/core/application/__tests__/use-cases.test.ts`:

```ts
import { isOk, ok } from '@/core/domain/result.type';
import type { JobsPort } from '../ports/jobs.port';
import { createJob } from '../use-cases/create-job.use-case';
import { searchJobs } from '../use-cases/search-jobs.use-case';

const port = (overrides: Partial<JobsPort>): JobsPort =>
  ({
    search: jest.fn(async () => ok({ items: [], nextCursor: null })),
    getById: jest.fn(),
    create: jest.fn(async () => ok('job-1')),
    start: jest.fn(),
    complete: jest.fn(),
    cancel: jest.fn(),
    assignees: jest.fn(async () => ok([])),
    customers: jest.fn(async () => ok([])),
    ...overrides,
  }) as JobsPort;

describe('use cases', () => {
  it('searchJobs applies a default limit when none is given', async () => {
    const search = jest.fn(async () => ok({ items: [], nextCursor: null }));
    const result = await searchJobs(port({ search }), {});

    expect(isOk(result)).toBe(true);
    expect(search).toHaveBeenCalledWith(expect.objectContaining({ limit: 20 }));
  });

  it('searchJobs honours an explicit limit', async () => {
    const search = jest.fn(async () => ok({ items: [], nextCursor: null }));
    await searchJobs(port({ search }), { limit: 5 });

    expect(search).toHaveBeenCalledWith(expect.objectContaining({ limit: 5 }));
  });

  it('createJob passes the input straight through and returns the id', async () => {
    const create = jest.fn(async () => ok('job-9'));
    const input = {
      title: 'Roof repair',
      description: '',
      address: {
        street: '12 Elm St',
        city: 'Springfield',
        state: 'IL',
        zipCode: '62701',
        latitude: 39.78,
        longitude: -89.65,
      },
      scheduledDate: '2099-03-14',
      assigneeId: 'assignee-1',
      customerId: 'customer-1',
    };

    const result = await createJob(port({ create }), input);

    expect(create).toHaveBeenCalledWith(input);
    expect(isOk(result) && result.value).toBe('job-9');
  });
});
```

The use cases are thin on purpose: they exist so the page, the Server Action and the test all reach the port the same way. `searchJobs` owning the default page size is the one policy that belongs here rather than in the adapter.

- [ ] **Step 2: Run tests to verify they fail**

Run: `npm --prefix frontend test -- use-cases`
Expected: FAIL with `Cannot find module '../use-cases/create-job.use-case'`.

- [ ] **Step 3: Write the seven use cases**

`search-jobs.use-case.ts`:

```ts
import type { CoreError, Result } from '@/core/domain/result.type';
import type { JobSearchQuery, JobsPort, PagedJobs } from '../ports/jobs.port';

export const DEFAULT_PAGE_SIZE = 20;

export function searchJobs(
  jobs: JobsPort,
  query: Partial<JobSearchQuery>,
): Promise<Result<PagedJobs, CoreError>> {
  return jobs.search({ ...query, limit: query.limit ?? DEFAULT_PAGE_SIZE });
}
```

`get-job.use-case.ts`:

```ts
import type { CoreError, Result } from '@/core/domain/result.type';
import type { JobDetail } from '@/core/domain/job/job-summary.type';
import type { JobsPort } from '../ports/jobs.port';

export function getJob(
  jobs: JobsPort,
  id: string,
): Promise<Result<JobDetail, CoreError>> {
  return jobs.getById(id);
}
```

`create-job.use-case.ts`:

```ts
import type { CoreError, Result } from '@/core/domain/result.type';
import type { CreateJobInput, JobsPort } from '../ports/jobs.port';

export function createJob(
  jobs: JobsPort,
  input: CreateJobInput,
): Promise<Result<string, CoreError>> {
  return jobs.create(input);
}
```

`start-job.use-case.ts`:

```ts
import type { CoreError, Result } from '@/core/domain/result.type';
import type { JobsPort } from '../ports/jobs.port';

export function startJob(jobs: JobsPort, id: string): Promise<Result<void, CoreError>> {
  return jobs.start(id);
}
```

`complete-job.use-case.ts`:

```ts
import type { CoreError, Result } from '@/core/domain/result.type';
import type { CompleteJobInput, JobsPort } from '../ports/jobs.port';

export function completeJob(
  jobs: JobsPort,
  id: string,
  input: CompleteJobInput,
): Promise<Result<void, CoreError>> {
  return jobs.complete(id, input);
}
```

`cancel-job.use-case.ts`:

```ts
import type { CoreError, Result } from '@/core/domain/result.type';
import type { JobsPort } from '../ports/jobs.port';

export function cancelJob(
  jobs: JobsPort,
  id: string,
  reason: string,
): Promise<Result<void, CoreError>> {
  return jobs.cancel(id, reason);
}
```

`list-parties.use-case.ts`:

```ts
import type { CoreError, Result } from '@/core/domain/result.type';
import type { Party } from '@/core/domain/job/job-summary.type';
import type { JobsPort } from '../ports/jobs.port';

export function listAssignees(jobs: JobsPort): Promise<Result<readonly Party[], CoreError>> {
  return jobs.assignees();
}

export function listCustomers(jobs: JobsPort): Promise<Result<readonly Party[], CoreError>> {
  return jobs.customers();
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `npm --prefix frontend test -- use-cases`
Expected: three passing tests.

- [ ] **Step 5: Write the failing container test**

Create `frontend/src/core/di/__tests__/container.test.ts`:

```ts
import { getContainer, resetContainer } from '../container';

describe('container', () => {
  afterEach(() => {
    resetContainer();
  });

  it('resolves the in-memory adapter by default', async () => {
    const container = getContainer();
    const result = await container.jobs.search({ limit: 1 });

    expect(result.ok).toBe(true);
  });

  it('returns the same instance on repeated calls', () => {
    expect(getContainer()).toBe(getContainer());
  });

  it('resets between tests so state does not leak', async () => {
    const before = getContainer();
    resetContainer();

    expect(getContainer()).not.toBe(before);
  });
});
```

`resetContainer` exists for the tests, and that is a legitimate reason: without it a job created in one test is visible in the next, and the suite passes or fails on execution order.

- [ ] **Step 6: Run tests to verify they fail**

Run: `npm --prefix frontend test -- container`
Expected: FAIL with `Cannot find module '../container'`.

- [ ] **Step 7: Write `container.ts`**

```ts
import { createInMemoryJobsAdapter } from '@/infrastructure/adapters/in-memory-jobs.adapter';
import type { JobsPort } from '@/core/application/ports/jobs.port';

export type Container = { readonly jobs: JobsPort };

let instance: Container | null = null;

/**
 * `http` is wired in plan 3, when HttpJobsAdapter exists. Until then the
 * in-memory adapter is the only implementation and is also the default in
 * development and CI (D-01), which is what lets the Playwright suite run
 * with no backend.
 */
function build(): Container {
  return { jobs: createInMemoryJobsAdapter() };
}

export function getContainer(): Container {
  instance ??= build();
  return instance;
}

export function resetContainer(): void {
  instance = null;
}
```

- [ ] **Step 8: Run the whole suite with coverage to verify green**

```bash
npm --prefix frontend run test:coverage
npm --prefix frontend run typecheck
npm --prefix frontend run lint
```

Expected: every test passing, coverage at or above 80% on all four metrics, no type errors, no lint errors.

- [ ] **Step 9: Commit**

```bash
git add frontend/src/core
git commit -m "feat: add the use cases and the DI container

Seven use cases, each taking a JobsPort as its first argument so the
page, the Server Action and a test all reach the port the same way.
They are deliberately thin; searchJobs owning the default page size is
the one policy that belongs at this layer rather than in an adapter.

The container resolves the in-memory adapter, which D-01 makes the
default in development and CI so Playwright needs neither backend nor
database. resetContainer exists for the tests: without it a job created
in one test is visible in the next and the suite passes on execution
order."
```

---

## Self-Review

**Spec coverage.** Assessment part 1 is fully covered: 1.1 A by task 2, 1.1 B by task 3, 1.1 C by task 4, 1.2 by task 5, 1.3 A/B/C by task 6. Architecture 5.1 (`core/` imports nothing from React or Next) holds — no file in tasks 6-9 imports either. Architecture 5.4 (two adapters behind one port) is half-built: `InMemoryJobsAdapter` here, `HttpJobsAdapter` in plan 3. D-06 (utilities integrated, not parked) is deferred to plan 2, which is where the consuming positions exist; the `Produces` block of each task names where.

**Not in this plan, by design.** Anything under `src/app/` or `src/presentation/` — that is plan 2. The `JobsEventMap` instance likewise: the emitter is generic here, and the application's event map belongs with the slices that emit on it.

**Placeholder scan.** Every code step carries the actual code. No "add error handling", no "similar to task N".

**Type consistency.** `JobStatus` is defined once in task 6 and imported by tasks 7 and 8. `CoreError.kind` uses the same five members in `result.type.ts` and in every adapter error helper. `JobSearchQuery.limit` is required on the port and optional at the use case, which is deliberate and is what the first use-case test asserts. `Party` is defined in task 7 and consumed by tasks 8 and 9. `JobSortField` lives on the port rather than in the store, so plan 2's `sortConfig` imports it from there.

**One gap this review found, recorded rather than silently fixed.** `getJobSummary` (task 6) has no consumer yet. Architecture 5.7 says the state machine drives which actions a row offers, which is `transitionJob`'s job; `getJobSummary` exists because assessment line 88 asks for it. Plan 2 should use it as the row's `aria-label`, which turns it from a required artefact into a used one. Noted for plan 2 rather than invented here.
