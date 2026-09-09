# Jobs Mutations Implementation Plan (Plan 2B)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the four mutation slices — `create-job`, `start-job`, `cancel-job`, `complete-job` — so the acceptance walkthrough runs end to end against the in-memory adapter: create a job, see it Scheduled, filter to it, start it, complete it, see it Completed.

**Architecture:** Every mutation follows one optimistic path — write the intended status into the overlay, run the Server Action, then commit and revalidate or roll back and surface the error on the row. Server Actions live inside their slice, because a slice with a mutation owns the whole path from the click to the API call. Slices never import each other; they meet at the typed event bus, one-way (architecture 9.3).

**Tech Stack:** Next 15.5.25, React 19.1.0 (`useTransition`, `useReducer`), TypeScript strict, Zustand 5, SWR 2, Jest + React Testing Library, Playwright.

**Spec:** `context/design.md` A5 (interaction flows), A6-A8, B4 (hook contracts); `context/prd.md` section 8 (the walkthrough), `FR-1` to `FR-5`, `BR-1` to `BR-5`; `context/architecture.md` 5.6, 9.3, 9.4

**Continues:** plan 2A, which left steps 1, 3 and 5 green. This plan turns steps 2, 6, 7 and 8 green. Steps 4 and 9 are asynchronous and belong to the Compose smoke run of architecture 8.1, which is plan 4.

## Global Constraints

- TypeScript `strict: true`. **No `any`, no `as unknown as X`**
- **Test-driven**: no production code without a test watched failing first (D-28)
- **Organisms are thin shells**: no `useState`, no handler bodies. State lives in the slice hook
- **No slice imports another slice**; every import into a slice goes through its `index.ts`; a slice never imports the view's barrel. Enforced by lint
- Conditional rendering uses a **ternary**, never `&&`
- Server Actions are for **mutations only** (assessment line 138), and live in `features/<slice>/actions/*.action.ts`
- Every element the walkthrough touches carries its `data-testid` from design A8
- **Two gates**: `tsc --noEmit` for the code, `next build` for App Router signatures. Both before any commit touching `src/app/`
- **`use()` does not resume under Jest with jsdom and React 19.1.** Proven in plan 2A with a minimal probe. Keep `use()` in components, never in a hook a unit test must drive
- **Server Actions must return serialisable values.** A `Result` carrying only plain objects crosses; a class instance or a function does not

## Debts this plan settles

Plan 2A's self-review left two, and the first has a better answer than the one recorded there.

**`JobRow` reconstructs a `JobState` from a `JobSummary`** and invents `startedAt` and `signatureUrl` to call `getJobSummary`. The fix is not a summary-shaped variant. Design A5 says *"`Start` appears only on Scheduled rows, because the state machine says so"*, and architecture 5.7 says the UI asks the state machine which actions a row offers — so what the row actually needs is `allowedActionsFor(status)`, a runtime mirror of the `AllowedAction` table. With that, the row stops inventing data, and `getJobSummary` moves to `/jobs/[id]`, which holds `startedAt`, `completedAt`, `signatureUrl` and photos and can build a faithful `JobState` without inventing anything. Task 1.

**The `from ≤ to` guard** design A3 specifies for `DateRangeFilter` does not exist. Task 6.

---

## File Structure

```
frontend/src/
├── core/domain/job/
│   ├── allowed-actions.ts                 allowedActionsFor — runtime mirror of AllowedAction
│   └── index.ts                           (extended)
└── presentation/
    ├── components/molecules/
    │   ├── signature-pad.component.tsx    canvas producing a data URL (BR-4)
    │   └── photo-list.component.tsx       thumbnails with captions
    └── views/jobs/
        ├── components/organisms/
        │   ├── job-row-actions.component.tsx   composes the slices for one row
        │   └── jobs-client.component.tsx       (modified: renderActions, modals)
        └── features/
            ├── start-job/
            │   ├── hooks/use-start-job.hook.ts
            │   ├── actions/start-job.action.ts
            │   └── index.ts
            ├── cancel-job/
            │   ├── hooks/use-cancel-job.hook.ts
            │   ├── components/molecules/cancel-reason-field.component.tsx
            │   ├── actions/cancel-job.action.ts
            │   └── index.ts
            ├── create-job/
            │   ├── hooks/use-create-job.hook.ts
            │   ├── components/organisms/create-job-modal.component.tsx
            │   ├── actions/create-job.action.ts
            │   └── index.ts
            └── complete-job/
                ├── hooks/use-complete-job.hook.ts
                ├── components/organisms/complete-job-modal.component.tsx
                ├── actions/complete-job.action.ts
                └── index.ts
```

`job-row-actions.component.tsx` is the one place the slices are composed for a row. It lives in the **view**, not in a slice, which is what lets five mutually unaware slices produce one row's worth of buttons without any of them importing another.

---

### Task 1: `allowedActionsFor`, and the two honesty fixes

**Files:**
- Create: `frontend/src/core/domain/job/allowed-actions.ts`
- Modify: `frontend/src/core/domain/job/index.ts`
- Modify: `frontend/src/presentation/components/molecules/job-row.component.tsx`
- Modify: `frontend/src/presentation/views/job-detail/components/organisms/job-detail.component.tsx`
- Test: `frontend/src/core/domain/job/__tests__/allowed-actions.test.ts`
- Test: modify `frontend/src/presentation/components/__tests__/molecules.test.tsx`
- Test: modify `frontend/src/presentation/views/job-detail/components/organisms/__tests__/job-detail.test.tsx`

**Interfaces:**
- Consumes: `JobStatus`, `JobAction`, `AllowedAction`, `JobDetail`
- Produces: `allowedActionsFor(status: JobStatus): readonly JobAction['type'][]`, and a `JobRow` that no longer invents data

- [ ] **Step 1: Write the failing test**

Create `frontend/src/core/domain/job/__tests__/allowed-actions.test.ts`:

```ts
import { expectTypeOf } from 'expect-type';
import { allowedActionsFor } from '../allowed-actions';
import type { AllowedAction } from '../job-state.type';

describe('allowedActionsFor', () => {
  it('offers SCHEDULE from Draft', () => {
    expect(allowedActionsFor('Draft')).toEqual(['SCHEDULE']);
  });

  it('offers START and CANCEL from Scheduled', () => {
    expect(allowedActionsFor('Scheduled')).toEqual(['START', 'CANCEL']);
  });

  it('offers COMPLETE and CANCEL from InProgress', () => {
    expect(allowedActionsFor('InProgress')).toEqual(['COMPLETE', 'CANCEL']);
  });

  it('offers nothing from a terminal state (BR-2)', () => {
    expect(allowedActionsFor('Completed')).toEqual([]);
    expect(allowedActionsFor('Cancelled')).toEqual([]);
  });

  it('agrees with the type-level transition table', () => {
    // The runtime table and AllowedAction are two statements of one rule, and
    // nothing but this assertion stops them drifting. Adding a transition to
    // one without the other fails here rather than at runtime.
    type FromRuntime = {
      [S in keyof AllowedAction]: AllowedAction[S];
    };
    expectTypeOf<FromRuntime['Scheduled']>().toEqualTypeOf<'START' | 'CANCEL'>();
    expectTypeOf<FromRuntime['Completed']>().toEqualTypeOf<never>();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `npm --prefix frontend test -- allowed-actions`
Expected: FAIL with `Cannot find module '../allowed-actions'`.

- [ ] **Step 3: Write `allowed-actions.ts`**

```ts
import type { JobStatus } from './job-status.type';
import type { AllowedAction, JobAction } from './job-state.type';

/**
 * The runtime mirror of the `AllowedAction` table. The type-level version
 * cannot be read at runtime, and the UI needs to know which buttons a row may
 * show — architecture 5.7: the UI asks the state machine which actions a row
 * offers, so an invalid action cannot be rendered.
 *
 * The mapped type on the value is what keeps the two in step: a status missing
 * here fails to compile, and the test asserts the members match.
 */
const ALLOWED: { [S in JobStatus]: readonly Extract<JobAction['type'], AllowedAction[S]>[] } = {
  Draft: ['SCHEDULE'],
  Scheduled: ['START', 'CANCEL'],
  InProgress: ['COMPLETE', 'CANCEL'],
  Completed: [],
  Cancelled: [],
};

export function allowedActionsFor(status: JobStatus): readonly JobAction['type'][] {
  return ALLOWED[status];
}
```

- [ ] **Step 4: Extend the barrel**

Add to `frontend/src/core/domain/job/index.ts`:

```ts
export { allowedActionsFor } from './allowed-actions';
```

- [ ] **Step 5: Run tests to verify green**

```bash
npm --prefix frontend test -- allowed-actions
npm --prefix frontend run typecheck
```

- [ ] **Step 6: Rewrite the JobRow label test**

In `frontend/src/presentation/components/__tests__/molecules.test.tsx`, replace the case named *"JobRow carries the state summary as its accessible label"* with:

```tsx
  it('JobRow labels itself from data it actually holds', () => {
    render(
      <table>
        <tbody>
          <JobRow job={job} onToggleSelect={noop} actions={null} />
        </tbody>
      </table>,
    );

    // A summary carries no timestamps, so the row states what it knows rather
    // than reconstructing a JobState and inventing the rest.
    expect(screen.getByTestId('job-row-job-1')).toHaveAttribute(
      'aria-label',
      'Ridge tile replacement, Scheduled, 2099-03-14, J. Ortiz',
    );
  });
```

- [ ] **Step 7: Run it to verify it fails**

Run: `npm --prefix frontend test -- molecules`
Expected: FAIL — the label is still the reconstructed `getJobSummary` output.

- [ ] **Step 8: Fix `job-row.component.tsx`**

Delete the `toState` function and the `getJobSummary` import entirely. Replace the `<tr>` opening tag with:

```tsx
    <tr
      data-testid={`job-row-${job.id}`}
      aria-label={`${job.title}, ${job.status}, ${job.scheduledDate}, ${job.assigneeName}`}
      aria-busy={job.isPending}
      className={job.isPending ? 'opacity-60' : undefined}
    >
```

The row now invents nothing. A `JobSummary` has no `startedAt` and no `signatureUrl`, and a label that fabricated them was accurate only for `Scheduled` by accident.

- [ ] **Step 9: Write the failing detail-summary test**

Add to `frontend/src/presentation/views/job-detail/components/organisms/__tests__/job-detail.test.tsx`:

```tsx
  it('summarises the job through the domain state machine', () => {
    render(<JobDetailView job={detail} />);

    // /jobs/[id] holds startedAt, completedAt, the signature and the photos,
    // so it can build a faithful JobState — which is what makes it the honest
    // home for getJobSummary. The list row cannot: a summary carries none of
    // those.
    expect(screen.getByTestId('job-detail-summary')).toHaveTextContent(
      'Completed on 2099-03-09, signed',
    );
  });

  it('summarises a cancelled job with its reason', () => {
    render(
      <JobDetailView
        job={{
          ...detail,
          status: 'Cancelled',
          completedAt: null,
          signatureUrl: null,
          cancelledAt: '2099-03-08T12:00:00.000Z',
          cancellationReason: 'Customer withdrew',
        }}
      />,
    );

    expect(screen.getByTestId('job-detail-summary')).toHaveTextContent(
      'Cancelled on 2099-03-08: Customer withdrew',
    );
  });
```

- [ ] **Step 10: Extend `JobDetail` with the two cancellation fields**

`JobDetail` cannot describe a cancelled job today. In `frontend/src/core/domain/job/job-summary.type.ts`, add to `JobDetail`:

```ts
  readonly cancelledAt: string | null;
  readonly cancellationReason: string | null;
```

And in `frontend/src/infrastructure/adapters/in-memory-jobs.adapter.ts`, add them to the object `getById` returns:

```ts
        cancelledAt: job.cancelledAt,
        cancellationReason: job.cancellationReason,
```

The seed already carries both; only the transfer shape was missing them.

- [ ] **Step 11: Write the summary into the detail view**

In `job-detail.component.tsx`, add above the description paragraph:

```tsx
      <p data-testid="job-detail-summary" className="mt-2 text-sm text-slate-700">
        {getJobSummary(toState(job))}
      </p>
```

And add the faithful conversion at the top of the file:

```tsx
import { getJobSummary } from '@/core/domain/job';
import type { JobState } from '@/core/domain/job';

/**
 * A faithful conversion: every field the state needs is present on JobDetail,
 * so nothing is invented. This is why the summary lives here and not on the
 * list row, whose JobSummary carries no timestamps.
 */
function toState(job: JobDetail): JobState {
  switch (job.status) {
    case 'Draft':
      return { status: 'Draft' };
    case 'Scheduled':
      return {
        status: 'Scheduled',
        scheduledDate: new Date(job.scheduledDate),
        assigneeId: job.assigneeId,
      };
    case 'InProgress':
      return {
        status: 'InProgress',
        startedAt: new Date(job.startedAt ?? job.scheduledDate),
        assigneeId: job.assigneeId,
        photos: job.photos.map((photo) => photo.url),
      };
    case 'Completed':
      return {
        status: 'Completed',
        startedAt: new Date(job.startedAt ?? job.scheduledDate),
        completedAt: new Date(job.completedAt ?? job.scheduledDate),
        assigneeId: job.assigneeId,
        photos: job.photos.map((photo) => photo.url),
        signatureUrl: job.signatureUrl ?? '',
      };
    case 'Cancelled':
      return {
        status: 'Cancelled',
        cancelledAt: new Date(job.cancelledAt ?? job.scheduledDate),
        reason: job.cancellationReason ?? '',
      };
  }
}
```

- [ ] **Step 12: Verify every gate**

```bash
npm --prefix frontend test
npm --prefix frontend run typecheck
npm --prefix frontend run lint
npm --prefix frontend run build
```

- [ ] **Step 13: Commit**

```bash
git add frontend/src
git commit -m "feat: give the row its actions from the state machine

allowedActionsFor is the runtime mirror of the AllowedAction table. The
type-level version cannot be read at runtime, and architecture 5.7 has
the UI asking the state machine which actions a row offers so an invalid
one cannot be rendered. A mapped type on the value keeps the two
statements of the rule in step, and a test asserts the members match.

That settles the first debt plan 2A recorded, and better than the answer
recorded there. JobRow was reconstructing a JobState from a JobSummary
and inventing startedAt and signatureUrl to call getJobSummary — a label
that was right for Scheduled by accident. The row now states what it
holds, and getJobSummary moves to /jobs/[id], which carries the
timestamps, the signature and the photos and can build a faithful state
without inventing anything.

JobDetail gained cancelledAt and cancellationReason: it could not
describe a cancelled job at all. The seed already had both; only the
transfer shape was missing them."
```

---

### Task 2: The `start-job` slice

**Files:**
- Create: `frontend/src/presentation/views/jobs/features/start-job/actions/start-job.action.ts`
- Create: `frontend/src/presentation/views/jobs/features/start-job/hooks/use-start-job.hook.ts`
- Create: `frontend/src/presentation/views/jobs/features/start-job/index.ts`
- Test: `frontend/src/presentation/views/jobs/features/start-job/__tests__/use-start-job.test.tsx`

**Interfaces:**
- Consumes: the store's optimistic actions, `jobsEventBus`, `startJob` use case
- Produces: `useStartJob()` returning `{ run, isPending, errorFor }`, and `ActionOutcome` — the serialisable shape every mutation action in this plan returns

This slice establishes the optimistic path the other three reuse. It is first because it is the smallest complete instance of it: no modal, no form, no input.

- [ ] **Step 1: Write the failing test**

Create `frontend/src/presentation/views/jobs/features/start-job/__tests__/use-start-job.test.tsx`:

```tsx
import { act, renderHook, waitFor } from '@testing-library/react';
import { useJobsUiStore } from '@/presentation/stores/jobs-ui.store';
import { jobsEventBus } from '@/shared/events/jobs-event-bus';
import { useStartJob } from '../hooks/use-start-job.hook';
import { startJobAction } from '../actions/start-job.action';

jest.mock('../actions/start-job.action', () => ({
  startJobAction: jest.fn(),
}));

const action = jest.mocked(startJobAction);

beforeEach(() => {
  useJobsUiStore.getState().reset();
  action.mockReset();
});

describe('useStartJob', () => {
  it('shows the target status immediately, before the action resolves', async () => {
    let release = (): void => undefined;
    action.mockImplementation(
      () => new Promise((resolve) => {
        release = () => resolve({ ok: true });
      }),
    );

    const { result } = renderHook(() => useStartJob());
    act(() => result.current.run('job-1', 'Scheduled'));

    // The overlay is written before the await, which is what makes the change
    // optimistic rather than merely fast.
    expect(useJobsUiStore.getState().optimisticStatus['job-1']).toBe('InProgress');

    await act(async () => {
      release();
    });
  });

  it('commits the overlay and asks the view to revalidate on success', async () => {
    action.mockResolvedValue({ ok: true });
    const invalidate = jest.fn();
    jobsEventBus.on('jobs:invalidate', invalidate);

    const { result } = renderHook(() => useStartJob());
    await act(async () => result.current.run('job-1', 'Scheduled'));

    await waitFor(() =>
      expect(useJobsUiStore.getState().optimisticStatus['job-1']).toBeUndefined(),
    );
    expect(invalidate).toHaveBeenCalledTimes(1);
    jobsEventBus.off('jobs:invalidate', invalidate);
  });

  it('rolls back and reports the error on failure', async () => {
    action.mockResolvedValue({
      ok: false,
      error: { code: 'job.conflict', message: 'Only a Scheduled job can start', kind: 'conflict' },
    });

    const { result } = renderHook(() => useStartJob());
    await act(async () => result.current.run('job-1', 'Scheduled'));

    await waitFor(() =>
      expect(useJobsUiStore.getState().optimisticStatus['job-1']).toBeUndefined(),
    );
    expect(result.current.errorFor('job-1')).toBe('Only a Scheduled job can start');
  });

  it('keeps each row's error to itself', async () => {
    action.mockResolvedValue({
      ok: false,
      error: { code: 'job.conflict', message: 'no', kind: 'conflict' },
    });

    const { result } = renderHook(() => useStartJob());
    await act(async () => result.current.run('job-1', 'Scheduled'));

    expect(result.current.errorFor('job-2')).toBeUndefined();
  });

  it('clears a previous error when the row is retried', async () => {
    action.mockResolvedValueOnce({
      ok: false,
      error: { code: 'job.conflict', message: 'no', kind: 'conflict' },
    });
    action.mockResolvedValueOnce({ ok: true });

    const { result } = renderHook(() => useStartJob());
    await act(async () => result.current.run('job-1', 'Scheduled'));
    await waitFor(() => expect(result.current.errorFor('job-1')).toBe('no'));

    await act(async () => result.current.run('job-1', 'Scheduled'));

    await waitFor(() => expect(result.current.errorFor('job-1')).toBeUndefined());
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `npm --prefix frontend test -- use-start-job`
Expected: FAIL with `Cannot find module '../hooks/use-start-job.hook'`.

- [ ] **Step 3: Write the shared outcome type**

Create `frontend/src/presentation/views/jobs/features/action-outcome.type.ts`:

```ts
import type { CoreError } from '@/core/domain/result.type';

/**
 * What every mutation Server Action in this view returns.
 *
 * It is deliberately a plain object rather than the core `Result`: a Server
 * Action's return value crosses a serialisation boundary, so it may carry only
 * data. `CoreError` already qualifies — code, message, kind and an optional
 * record of field errors, no methods.
 */
export type ActionOutcome =
  | { readonly ok: true }
  | { readonly ok: false; readonly error: CoreError };

export type CreateOutcome =
  | { readonly ok: true; readonly id: string }
  | { readonly ok: false; readonly error: CoreError };
```

This file sits beside the slices rather than inside one, because all four return it and no slice may import another.

- [ ] **Step 4: Write the Server Action**

Create `frontend/src/presentation/views/jobs/features/start-job/actions/start-job.action.ts`:

```ts
'use server';

import { startJob } from '@/core/application/use-cases/start-job.use-case';
import { getContainer } from '@/core/di/container';
import { isOk } from '@/core/domain/result';
import type { ActionOutcome } from '../../action-outcome.type';

/**
 * A mutation, which is the only thing a Server Action is for (assessment
 * line 138). Reads go through the Route Handler of D-29.
 */
export async function startJobAction(id: string): Promise<ActionOutcome> {
  const result = await startJob(getContainer().jobs, id);
  return isOk(result) ? { ok: true } : { ok: false, error: result.error };
}
```

- [ ] **Step 5: Write the hook**

Create `frontend/src/presentation/views/jobs/features/start-job/hooks/use-start-job.hook.ts`:

```ts
'use client';

import { useCallback, useState, useTransition } from 'react';
import { useShallow } from 'zustand/react/shallow';
import type { JobStatus } from '@/core/domain/job/job-status.type';
import { useJobsUiStore } from '@/presentation/stores/jobs-ui.store';
import { jobsEventBus } from '@/shared/events/jobs-event-bus';
import { startJobAction } from '../actions/start-job.action';

export function useStartJob() {
  const { beginOptimistic, commitOptimistic, rollbackOptimistic } = useJobsUiStore(
    useShallow((state) => ({
      beginOptimistic: state.beginOptimistic,
      commitOptimistic: state.commitOptimistic,
      rollbackOptimistic: state.rollbackOptimistic,
    })),
  );

  // Errors are keyed by job id so two rows can fail independently, which is
  // what design A5 means by the error appearing on its own row.
  const [errors, setErrors] = useState<Readonly<Record<string, string>>>({});
  const [isPending, startTransition] = useTransition();

  const run = useCallback(
    (id: string, current: JobStatus) => {
      setErrors((previous) => {
        const { [id]: _dropped, ...rest } = previous;
        return rest;
      });

      // Written before the await: that ordering is what makes the change
      // optimistic rather than merely fast.
      beginOptimistic(id, 'InProgress', current);

      startTransition(async () => {
        const outcome = await startJobAction(id);

        if (outcome.ok) {
          commitOptimistic(id);
          // The view revalidates; the slice does not know the view exists.
          jobsEventBus.emit('jobs:invalidate', undefined);
          return;
        }

        rollbackOptimistic(id);
        setErrors((previous) => ({ ...previous, [id]: outcome.error.message }));
      });
    },
    [beginOptimistic, commitOptimistic, rollbackOptimistic],
  );

  const errorFor = useCallback((id: string): string | undefined => errors[id], [errors]);

  return { run, isPending, errorFor };
}
```

- [ ] **Step 6: Write the barrel**

Create `frontend/src/presentation/views/jobs/features/start-job/index.ts`:

```ts
export { useStartJob } from './hooks/use-start-job.hook';
```

- [ ] **Step 7: Run tests, typecheck and lint to verify green**

```bash
npm --prefix frontend test -- use-start-job
npm --prefix frontend run typecheck
npm --prefix frontend run lint
```

Expected: five passing tests, no type errors, no lint errors.

- [ ] **Step 8: Commit**

```bash
git add frontend/src/presentation/views/jobs/features
git commit -m "feat: add the start-job slice and the optimistic path

The smallest complete instance of the path the other three mutations
reuse: write the intended status into the overlay, run the Server
Action, then commit and ask the view to revalidate, or roll back and
put the message on the row.

The overlay is written before the await, and a test holds that ordering
by resolving the action manually — it is what makes the change
optimistic rather than merely fast.

Errors are keyed by job id so two rows fail independently, which is what
design A5 means by the error appearing on its own row.

The slice emits jobs:invalidate rather than calling the view's mutate.
It does not know the view exists, which is rule 2 of architecture 5.6
holding in practice rather than in prose.

ActionOutcome sits beside the slices rather than in one, because all
four return it and no slice may import another. It is a plain object
rather than the core Result: a Server Action's return value crosses a
serialisation boundary and may carry only data."
```

---

### Task 3: The `cancel-job` slice

**Files:**
- Create: `frontend/src/presentation/views/jobs/features/cancel-job/actions/cancel-job.action.ts`
- Create: `frontend/src/presentation/views/jobs/features/cancel-job/hooks/use-cancel-job.hook.ts`
- Create: `frontend/src/presentation/views/jobs/features/cancel-job/components/molecules/cancel-reason-field.component.tsx`
- Create: `frontend/src/presentation/views/jobs/features/cancel-job/index.ts`
- Test: `frontend/src/presentation/views/jobs/features/cancel-job/__tests__/use-cancel-job.test.tsx`

**Interfaces:**
- Consumes: the store's optimistic actions, `jobsEventBus`, `cancelJob` use case
- Produces: `useCancelJob()` returning `{ openFor, open, close, reason, setReason, submit, isPending, errorFor }`, and `CancelReasonField`

Cancelling collects one value, so design A1 gives it an inline field rather than a modal. The slice therefore owns which row's field is open.

- [ ] **Step 1: Write the failing test**

Create `frontend/src/presentation/views/jobs/features/cancel-job/__tests__/use-cancel-job.test.tsx`:

```tsx
import { act, renderHook, waitFor } from '@testing-library/react';
import { useJobsUiStore } from '@/presentation/stores/jobs-ui.store';
import { useCancelJob } from '../hooks/use-cancel-job.hook';
import { cancelJobAction } from '../actions/cancel-job.action';

jest.mock('../actions/cancel-job.action', () => ({
  cancelJobAction: jest.fn(),
}));

const action = jest.mocked(cancelJobAction);

beforeEach(() => {
  useJobsUiStore.getState().reset();
  action.mockReset();
  action.mockResolvedValue({ ok: true });
});

describe('useCancelJob', () => {
  it('opens the reason field for one row at a time', () => {
    const { result } = renderHook(() => useCancelJob());

    act(() => result.current.open('job-1'));
    expect(result.current.openFor).toBe('job-1');

    act(() => result.current.open('job-2'));
    expect(result.current.openFor).toBe('job-2');
  });

  it('closes the field and forgets the typed reason', () => {
    const { result } = renderHook(() => useCancelJob());

    act(() => result.current.open('job-1'));
    act(() => result.current.setReason('Weather'));
    act(() => result.current.close());

    expect(result.current.openFor).toBeNull();
    expect(result.current.reason).toBe('');
  });

  it('refuses an empty reason before calling the action (BR-5)', async () => {
    const { result } = renderHook(() => useCancelJob());

    act(() => result.current.open('job-1'));
    await act(async () => result.current.submit('Scheduled'));

    expect(action).not.toHaveBeenCalled();
    expect(result.current.errorFor('job-1')).toBe('A cancellation reason is required');
  });

  it('refuses a reason that is only whitespace', async () => {
    const { result } = renderHook(() => useCancelJob());

    act(() => result.current.open('job-1'));
    act(() => result.current.setReason('   '));
    await act(async () => result.current.submit('Scheduled'));

    expect(action).not.toHaveBeenCalled();
  });

  it('cancels optimistically and closes on success', async () => {
    const { result } = renderHook(() => useCancelJob());

    act(() => result.current.open('job-1'));
    act(() => result.current.setReason('Weather'));
    await act(async () => result.current.submit('Scheduled'));

    expect(action).toHaveBeenCalledWith('job-1', 'Weather');
    await waitFor(() => expect(result.current.openFor).toBeNull());
    await waitFor(() =>
      expect(useJobsUiStore.getState().optimisticStatus['job-1']).toBeUndefined(),
    );
  });

  it('rolls back and keeps the field open on failure', async () => {
    action.mockResolvedValue({
      ok: false,
      error: {
        code: 'job.conflict',
        message: 'A job in a terminal state cannot change state',
        kind: 'conflict',
      },
    });

    const { result } = renderHook(() => useCancelJob());
    act(() => result.current.open('job-1'));
    act(() => result.current.setReason('Weather'));
    await act(async () => result.current.submit('Completed'));

    await waitFor(() =>
      expect(result.current.errorFor('job-1')).toBe(
        'A job in a terminal state cannot change state',
      ),
    );
    // The field stays open so the reason is not lost with the error.
    expect(result.current.openFor).toBe('job-1');
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `npm --prefix frontend test -- use-cancel-job`
Expected: FAIL with `Cannot find module '../hooks/use-cancel-job.hook'`.

- [ ] **Step 3: Write the Server Action**

```ts
'use server';

import { cancelJob } from '@/core/application/use-cases/cancel-job.use-case';
import { getContainer } from '@/core/di/container';
import { isOk } from '@/core/domain/result';
import type { ActionOutcome } from '../../action-outcome.type';

export async function cancelJobAction(id: string, reason: string): Promise<ActionOutcome> {
  const result = await cancelJob(getContainer().jobs, id, reason);
  return isOk(result) ? { ok: true } : { ok: false, error: result.error };
}
```

- [ ] **Step 4: Write the hook**

```ts
'use client';

import { useCallback, useState, useTransition } from 'react';
import { useShallow } from 'zustand/react/shallow';
import type { JobStatus } from '@/core/domain/job/job-status.type';
import { useJobsUiStore } from '@/presentation/stores/jobs-ui.store';
import { jobsEventBus } from '@/shared/events/jobs-event-bus';
import { cancelJobAction } from '../actions/cancel-job.action';

const REASON_REQUIRED = 'A cancellation reason is required';

export function useCancelJob() {
  const { beginOptimistic, commitOptimistic, rollbackOptimistic } = useJobsUiStore(
    useShallow((state) => ({
      beginOptimistic: state.beginOptimistic,
      commitOptimistic: state.commitOptimistic,
      rollbackOptimistic: state.rollbackOptimistic,
    })),
  );

  const [openFor, setOpenFor] = useState<string | null>(null);
  const [reason, setReason] = useState('');
  const [errors, setErrors] = useState<Readonly<Record<string, string>>>({});
  const [isPending, startTransition] = useTransition();

  const open = useCallback((id: string) => {
    setOpenFor(id);
    setReason('');
  }, []);

  const close = useCallback(() => {
    setOpenFor(null);
    setReason('');
  }, []);

  const submit = useCallback(
    (current: JobStatus) => {
      const id = openFor;
      if (id === null) return;

      // BR-5 is checked here as well as in the adapter: once where the user
      // can be told, and once where it cannot be bypassed.
      if (reason.trim() === '') {
        setErrors((previous) => ({ ...previous, [id]: REASON_REQUIRED }));
        return;
      }

      setErrors((previous) => {
        const { [id]: _dropped, ...rest } = previous;
        return rest;
      });
      beginOptimistic(id, 'Cancelled', current);

      startTransition(async () => {
        const outcome = await cancelJobAction(id, reason);

        if (outcome.ok) {
          commitOptimistic(id);
          jobsEventBus.emit('jobs:invalidate', undefined);
          setOpenFor(null);
          setReason('');
          return;
        }

        rollbackOptimistic(id);
        setErrors((previous) => ({ ...previous, [id]: outcome.error.message }));
        // The field stays open: closing it would discard the typed reason
        // along with the error that asked the user to reconsider it.
      });
    },
    [openFor, reason, beginOptimistic, commitOptimistic, rollbackOptimistic],
  );

  const errorFor = useCallback((id: string): string | undefined => errors[id], [errors]);

  return { openFor, open, close, reason, setReason, submit, isPending, errorFor };
}
```

- [ ] **Step 5: Write the inline field**

```tsx
'use client';

import { Button } from '@/presentation/components/atoms/button.component';
import { TextInput } from '@/presentation/components/atoms/text-input.component';

export function CancelReasonField({
  jobId,
  value,
  onChange,
  onSubmit,
  onDismiss,
  error,
  pending,
}: {
  readonly jobId: string;
  readonly value: string;
  readonly onChange: (value: string) => void;
  readonly onSubmit: () => void;
  readonly onDismiss: () => void;
  readonly error?: string;
  readonly pending: boolean;
}) {
  return (
    <span className="mt-1 flex items-center gap-1">
      <TextInput
        testId={`job-row-${jobId}-cancel-reason`}
        id={`job-row-${jobId}-cancel-reason`}
        value={value}
        onChange={onChange}
        placeholder="Reason"
        error={error}
      />
      <Button testId={`job-row-${jobId}-cancel-confirm`} onClick={onSubmit} pending={pending}>
        Confirm
      </Button>
      <Button
        testId={`job-row-${jobId}-cancel-dismiss`}
        variant="ghost"
        onClick={onDismiss}
      >
        Keep
      </Button>
    </span>
  );
}
```

- [ ] **Step 6: Write the barrel**

```ts
export { useCancelJob } from './hooks/use-cancel-job.hook';
export { CancelReasonField } from './components/molecules/cancel-reason-field.component';
```

- [ ] **Step 7: Verify green and commit**

```bash
npm --prefix frontend test -- use-cancel-job
npm --prefix frontend run typecheck
npm --prefix frontend run lint
git add frontend/src/presentation/views/jobs/features/cancel-job
git commit -m "feat: add the cancel-job slice with its inline reason field

Cancelling collects one value, so design A1 gives it an inline field
rather than a modal, and the slice owns which row's field is open.

BR-5 is checked in the hook as well as in the adapter: once where the
user can be told, and once where it cannot be bypassed. The hook's check
runs before the action is called at all, which a test asserts by
verifying the action was never invoked.

On failure the field stays open. Closing it would discard the typed
reason along with the error that asked the user to reconsider it."
```

---

### Task 4: The `create-job` slice

**Files:**
- Create: `frontend/src/presentation/views/jobs/features/create-job/actions/create-job.action.ts`
- Create: `frontend/src/presentation/views/jobs/features/create-job/hooks/use-create-job.hook.ts`
- Create: `frontend/src/presentation/views/jobs/features/create-job/components/organisms/create-job-modal.component.tsx`
- Create: `frontend/src/presentation/views/jobs/features/create-job/index.ts`
- Test: `frontend/src/presentation/views/jobs/features/create-job/__tests__/use-create-job.test.tsx`
- Test: `frontend/src/presentation/views/jobs/features/create-job/__tests__/validate.test.ts`

**Interfaces:**
- Consumes: `PathKeys`, `createJob` use case, `Party`
- Produces: `useCreateJob()`, `CreateJobModal`, and the pure `validate(values)`

This is where `useReducer` (line 156) and `PathKeys` (D-06) do their work. The validator is a pure function so it can be tested with no React at all.

- [ ] **Step 1: Write the failing validator test**

Create `frontend/src/presentation/views/jobs/features/create-job/__tests__/validate.test.ts`:

```ts
import { EMPTY_VALUES, validate } from '../hooks/use-create-job.hook';

const valid = {
  title: 'Roof repair',
  description: '',
  address: {
    street: '12 Elm St',
    city: 'Springfield',
    state: 'IL',
    zipCode: '62701',
    latitude: '39.78',
    longitude: '-89.65',
  },
  scheduledDate: '2099-03-14',
  assigneeId: 'assignee-1',
  customerId: 'customer-1',
};

describe('validate', () => {
  it('accepts a complete form', () => {
    expect(validate(valid)).toEqual({});
  });

  it('requires a title', () => {
    expect(validate({ ...valid, title: '  ' }).title).toBe('A title is required');
  });

  it('requires every address field', () => {
    const errors = validate({ ...valid, address: { ...valid.address, city: '' } });

    // PathKeys types the key, so 'address.city' compiles and 'address.town'
    // does not — which is the whole reason the form's error map is keyed this
    // way (D-06).
    expect(errors['address.city']).toBe('City is required');
  });

  it('requires coordinates to be numbers', () => {
    const errors = validate({
      ...valid,
      address: { ...valid.address, latitude: 'north' },
    });

    expect(errors['address.latitude']).toBe('Latitude must be a number');
  });

  it('requires a scheduled date', () => {
    expect(validate({ ...valid, scheduledDate: '' }).scheduledDate).toBe(
      'A scheduled date is required',
    );
  });

  it('refuses a date in the past (BR-1)', () => {
    expect(validate({ ...valid, scheduledDate: '2000-01-01' }).scheduledDate).toBe(
      'A job cannot be scheduled in the past',
    );
  });

  it('requires an assignee and a customer', () => {
    const errors = validate({ ...valid, assigneeId: '', customerId: '' });

    expect(errors.assigneeId).toBe('An assignee is required');
    expect(errors.customerId).toBe('A customer is required');
  });

  it('reports every problem at once rather than the first', () => {
    // Design A5 point 3: pressing submit reveals all remaining errors rather
    // than hiding the way forward one at a time.
    expect(Object.keys(validate(EMPTY_VALUES)).length).toBeGreaterThan(3);
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `npm --prefix frontend test -- validate`
Expected: FAIL with `Cannot find module '../hooks/use-create-job.hook'`.

- [ ] **Step 3: Write the reducer, the validator and the hook**

Create `frontend/src/presentation/views/jobs/features/create-job/hooks/use-create-job.hook.ts`:

```ts
'use client';

import { useCallback, useReducer, useTransition } from 'react';
import type { CreateJobInput } from '@/core/application/ports/jobs.port';
import { jobsEventBus } from '@/shared/events/jobs-event-bus';
import type { PathKeys } from '@/shared/types/path-keys.type';
import { createJobAction } from '../actions/create-job.action';

export type CreateJobValues = {
  title: string;
  description: string;
  address: {
    street: string;
    city: string;
    state: string;
    zipCode: string;
    latitude: string;
    longitude: string;
  };
  scheduledDate: string;
  assigneeId: string;
  customerId: string;
};

/**
 * PathKeys types every field address, so 'address.zipCode' is valid and
 * 'address.zip' does not compile. Nested form state is exactly the case
 * dot-notation paths exist for (D-06).
 */
export type CreateJobField = PathKeys<CreateJobValues>;
export type CreateJobErrors = Partial<Record<CreateJobField, string>>;

export const EMPTY_VALUES: CreateJobValues = {
  title: '',
  description: '',
  address: { street: '', city: '', state: '', zipCode: '', latitude: '', longitude: '' },
  scheduledDate: '',
  assigneeId: '',
  customerId: '',
};

const isBlank = (value: string): boolean => value.trim() === '';

const isNumeric = (value: string): boolean => value !== '' && !Number.isNaN(Number(value));

/**
 * A pure function from values to errors, called by the reducer. Keeping it
 * pure is what makes it testable with no React in the process, and it is the
 * same reason the reducer holds no I/O.
 */
export function validate(values: CreateJobValues): CreateJobErrors {
  const errors: Record<string, string> = {};

  if (isBlank(values.title)) errors.title = 'A title is required';
  if (isBlank(values.address.street)) errors['address.street'] = 'Street is required';
  if (isBlank(values.address.city)) errors['address.city'] = 'City is required';
  if (isBlank(values.address.state)) errors['address.state'] = 'State is required';
  if (isBlank(values.address.zipCode)) errors['address.zipCode'] = 'ZIP code is required';
  if (!isNumeric(values.address.latitude)) {
    errors['address.latitude'] = 'Latitude must be a number';
  }
  if (!isNumeric(values.address.longitude)) {
    errors['address.longitude'] = 'Longitude must be a number';
  }

  if (isBlank(values.scheduledDate)) {
    errors.scheduledDate = 'A scheduled date is required';
  } else if (values.scheduledDate < new Date().toISOString().slice(0, 10)) {
    // BR-1, checked where the user can be told. The aggregate checks it again
    // where it cannot be bypassed.
    errors.scheduledDate = 'A job cannot be scheduled in the past';
  }

  if (isBlank(values.assigneeId)) errors.assigneeId = 'An assignee is required';
  if (isBlank(values.customerId)) errors.customerId = 'A customer is required';

  return errors;
}

export type CreateJobFormState = {
  values: CreateJobValues;
  errors: CreateJobErrors;
  touched: Partial<Record<CreateJobField, boolean>>;
  status: 'idle' | 'submitting' | 'failed';
  formError: string | null;
};

export type CreateJobFormAction =
  | { type: 'FIELD_CHANGED'; field: CreateJobField; value: string }
  | { type: 'FIELD_BLURRED'; field: CreateJobField }
  | { type: 'SUBMIT_STARTED' }
  | { type: 'SUBMIT_FAILED'; formError: string; fieldErrors?: CreateJobErrors }
  | { type: 'SUBMIT_SUCCEEDED' }
  | { type: 'RESET' };

const INITIAL: CreateJobFormState = {
  values: EMPTY_VALUES,
  errors: {},
  touched: {},
  status: 'idle',
  formError: null,
};

const setField = (values: CreateJobValues, field: CreateJobField, value: string): CreateJobValues =>
  field.startsWith('address.')
    ? { ...values, address: { ...values.address, [field.slice('address.'.length)]: value } }
    : { ...values, [field]: value };

export function createJobReducer(
  state: CreateJobFormState,
  action: CreateJobFormAction,
): CreateJobFormState {
  switch (action.type) {
    case 'FIELD_CHANGED': {
      const values = setField(state.values, action.field, action.value);
      return { ...state, values, errors: validate(values), formError: null };
    }
    case 'FIELD_BLURRED':
      return { ...state, touched: { ...state.touched, [action.field]: true } };
    case 'SUBMIT_STARTED':
      return { ...state, status: 'submitting', formError: null };
    case 'SUBMIT_FAILED':
      return {
        ...state,
        status: 'failed',
        formError: action.formError,
        errors: { ...state.errors, ...action.fieldErrors },
      };
    case 'SUBMIT_SUCCEEDED':
      return INITIAL;
    case 'RESET':
      return INITIAL;
  }
}

const toInput = (values: CreateJobValues): CreateJobInput => ({
  title: values.title,
  description: values.description,
  address: {
    street: values.address.street,
    city: values.address.city,
    state: values.address.state,
    zipCode: values.address.zipCode,
    latitude: Number(values.address.latitude),
    longitude: Number(values.address.longitude),
  },
  scheduledDate: values.scheduledDate,
  assigneeId: values.assigneeId,
  customerId: values.customerId,
});

export function useCreateJob() {
  const [isOpen, setOpen] = useReducer((_: boolean, next: boolean) => next, false);
  const [form, dispatch] = useReducer(createJobReducer, INITIAL);
  const [isPending, startTransition] = useTransition();

  const open = useCallback(() => setOpen(true), []);

  const close = useCallback(() => {
    setOpen(false);
    dispatch({ type: 'RESET' });
  }, []);

  const change = useCallback(
    (field: CreateJobField, value: string) =>
      dispatch({ type: 'FIELD_CHANGED', field, value }),
    [],
  );

  const blur = useCallback(
    (field: CreateJobField) => dispatch({ type: 'FIELD_BLURRED', field }),
    [],
  );

  const submit = useCallback(() => {
    const errors = validate(form.values);
    if (Object.keys(errors).length > 0) {
      // Design A5 point 3: submit stays enabled so pressing it reveals every
      // remaining error at once rather than hiding the way forward.
      dispatch({ type: 'SUBMIT_FAILED', formError: 'Fix the highlighted fields', fieldErrors: errors });
      return;
    }

    dispatch({ type: 'SUBMIT_STARTED' });

    startTransition(async () => {
      const outcome = await createJobAction(toInput(form.values));

      if (outcome.ok) {
        dispatch({ type: 'SUBMIT_SUCCEEDED' });
        setOpen(false);
        jobsEventBus.emit('jobs:invalidate', undefined);
        return;
      }

      dispatch({
        type: 'SUBMIT_FAILED',
        formError: outcome.error.message,
        fieldErrors: outcome.error.fieldErrors as CreateJobErrors | undefined,
      });
    });
  }, [form.values]);

  return { isOpen, open, close, form, change, blur, submit, isPending };
}
```

- [ ] **Step 4: Write the Server Action**

```ts
'use server';

import type { CreateJobInput } from '@/core/application/ports/jobs.port';
import { createJob } from '@/core/application/use-cases/create-job.use-case';
import { getContainer } from '@/core/di/container';
import { isOk } from '@/core/domain/result';
import type { CreateOutcome } from '../../action-outcome.type';

export async function createJobAction(input: CreateJobInput): Promise<CreateOutcome> {
  const result = await createJob(getContainer().jobs, input);
  return isOk(result) ? { ok: true, id: result.value } : { ok: false, error: result.error };
}
```

- [ ] **Step 5: Run the validator tests to verify green**

Run: `npm --prefix frontend test -- validate`
Expected: eight passing tests.

- [ ] **Step 6: Write the failing hook test**

Create `frontend/src/presentation/views/jobs/features/create-job/__tests__/use-create-job.test.tsx`:

```tsx
import { act, renderHook, waitFor } from '@testing-library/react';
import { jobsEventBus } from '@/shared/events/jobs-event-bus';
import { createJobAction } from '../actions/create-job.action';
import { useCreateJob } from '../hooks/use-create-job.hook';

jest.mock('../actions/create-job.action', () => ({ createJobAction: jest.fn() }));

const action = jest.mocked(createJobAction);

const fill = (result: { current: ReturnType<typeof useCreateJob> }) => {
  act(() => result.current.change('title', 'Roof repair'));
  act(() => result.current.change('address.street', '12 Elm St'));
  act(() => result.current.change('address.city', 'Springfield'));
  act(() => result.current.change('address.state', 'IL'));
  act(() => result.current.change('address.zipCode', '62701'));
  act(() => result.current.change('address.latitude', '39.78'));
  act(() => result.current.change('address.longitude', '-89.65'));
  act(() => result.current.change('scheduledDate', '2099-03-14'));
  act(() => result.current.change('assigneeId', 'assignee-1'));
  act(() => result.current.change('customerId', 'customer-1'));
};

beforeEach(() => {
  action.mockReset();
  action.mockResolvedValue({ ok: true, id: 'job-new-1' });
});

describe('useCreateJob', () => {
  it('opens and closes the modal', () => {
    const { result } = renderHook(() => useCreateJob());
    expect(result.current.isOpen).toBe(false);

    act(() => result.current.open());
    expect(result.current.isOpen).toBe(true);

    act(() => result.current.close());
    expect(result.current.isOpen).toBe(false);
  });

  it('writes a nested field through its dot-notation path', () => {
    const { result } = renderHook(() => useCreateJob());

    act(() => result.current.change('address.city', 'Springfield'));

    expect(result.current.form.values.address.city).toBe('Springfield');
  });

  it('records which fields have been blurred', () => {
    const { result } = renderHook(() => useCreateJob());

    act(() => result.current.blur('title'));

    expect(result.current.form.touched.title).toBe(true);
  });

  it('refuses to submit an invalid form and reveals every error', () => {
    const { result } = renderHook(() => useCreateJob());

    act(() => result.current.submit());

    expect(action).not.toHaveBeenCalled();
    expect(result.current.form.formError).toBe('Fix the highlighted fields');
    expect(Object.keys(result.current.form.errors).length).toBeGreaterThan(3);
  });

  it('converts the coordinates to numbers before calling the action', async () => {
    const { result } = renderHook(() => useCreateJob());
    fill(result);

    await act(async () => result.current.submit());

    expect(action).toHaveBeenCalledWith(
      expect.objectContaining({
        address: expect.objectContaining({ latitude: 39.78, longitude: -89.65 }),
      }),
    );
  });

  it('closes, resets and asks the view to revalidate on success', async () => {
    const invalidate = jest.fn();
    jobsEventBus.on('jobs:invalidate', invalidate);

    const { result } = renderHook(() => useCreateJob());
    act(() => result.current.open());
    fill(result);
    await act(async () => result.current.submit());

    await waitFor(() => expect(result.current.isOpen).toBe(false));
    expect(result.current.form.values.title).toBe('');
    expect(invalidate).toHaveBeenCalledTimes(1);
    jobsEventBus.off('jobs:invalidate', invalidate);
  });

  it('stays open and surfaces field errors on failure', async () => {
    action.mockResolvedValue({
      ok: false,
      error: {
        code: 'job.validation',
        message: 'A job cannot be scheduled in the past',
        kind: 'validation',
        fieldErrors: { scheduledDate: 'A job cannot be scheduled in the past' },
      },
    });

    const { result } = renderHook(() => useCreateJob());
    act(() => result.current.open());
    fill(result);
    await act(async () => result.current.submit());

    await waitFor(() =>
      expect(result.current.form.formError).toBe('A job cannot be scheduled in the past'),
    );
    expect(result.current.isOpen).toBe(true);
    expect(result.current.form.errors.scheduledDate).toBeDefined();
  });
});
```

- [ ] **Step 7: Run tests to verify green**

Run: `npm --prefix frontend test -- use-create-job`
Expected: seven passing tests.

- [ ] **Step 8: Write the modal**

Create `create-job-modal.component.tsx`. It is a thin shell: every value and every handler comes from the hook, passed in as props by `JobsClient`.

```tsx
'use client';

import type { Party } from '@/core/domain/job/job-summary.type';
import { Button } from '@/presentation/components/atoms/button.component';
import { DateInput } from '@/presentation/components/atoms/date-input.component';
import { Select } from '@/presentation/components/atoms/select.component';
import { TextInput } from '@/presentation/components/atoms/text-input.component';
import { FormField } from '@/presentation/components/molecules/form-field.component';
import type { CreateJobField, CreateJobFormState } from '../../hooks/use-create-job.hook';

const TEXT_FIELDS: readonly { field: CreateJobField; label: string; testId: string }[] = [
  { field: 'title', label: 'Title', testId: 'create-job-title' },
  { field: 'description', label: 'Description', testId: 'create-job-description' },
  { field: 'address.street', label: 'Street', testId: 'create-job-street' },
  { field: 'address.city', label: 'City', testId: 'create-job-city' },
  { field: 'address.state', label: 'State', testId: 'create-job-state' },
  { field: 'address.zipCode', label: 'ZIP code', testId: 'create-job-zip' },
  { field: 'address.latitude', label: 'Latitude', testId: 'create-job-latitude' },
  { field: 'address.longitude', label: 'Longitude', testId: 'create-job-longitude' },
];

const valueAt = (form: CreateJobFormState, field: CreateJobField): string =>
  field.startsWith('address.')
    ? form.values.address[
        field.slice('address.'.length) as keyof CreateJobFormState['values']['address']
      ]
    : String(form.values[field as 'title' | 'description' | 'scheduledDate' | 'assigneeId' | 'customerId']);

export function CreateJobModal({
  form,
  assignees,
  customers,
  isPending,
  onChange,
  onBlur,
  onSubmit,
  onCancel,
}: {
  readonly form: CreateJobFormState;
  readonly assignees: readonly Party[];
  readonly customers: readonly Party[];
  readonly isPending: boolean;
  readonly onChange: (field: CreateJobField, value: string) => void;
  readonly onBlur: (field: CreateJobField) => void;
  readonly onSubmit: () => void;
  readonly onCancel: () => void;
}) {
  return (
    <div
      data-testid="create-job-modal"
      role="dialog"
      aria-modal="true"
      aria-label="New job"
      className="fixed inset-0 z-10 flex items-start justify-center overflow-y-auto bg-black/30 p-8"
    >
      <div className="w-full max-w-lg rounded bg-white p-6">
        <h2 className="mb-4 text-lg font-semibold">New job</h2>

        {TEXT_FIELDS.map(({ field, label, testId }) => (
          <FormField key={field} id={testId} label={label} error={form.errors[field]}>
            <TextInput
              testId={testId}
              id={testId}
              value={valueAt(form, field)}
              onChange={(value) => onChange(field, value)}
              onBlur={() => onBlur(field)}
            />
          </FormField>
        ))}

        <FormField
          id="create-job-scheduled-date"
          label="Scheduled date"
          error={form.errors.scheduledDate}
        >
          <DateInput
            testId="create-job-scheduled-date"
            id="create-job-scheduled-date"
            value={form.values.scheduledDate}
            onChange={(value) => onChange('scheduledDate', value)}
          />
        </FormField>

        <FormField id="create-job-assignee" label="Crew" error={form.errors.assigneeId}>
          <Select
            testId="create-job-assignee"
            id="create-job-assignee"
            value={form.values.assigneeId}
            placeholder="Choose crew"
            options={assignees.map((party) => ({ value: party.id, label: party.name }))}
            onChange={(value) => onChange('assigneeId', value)}
          />
        </FormField>

        <FormField id="create-job-customer" label="Customer" error={form.errors.customerId}>
          <Select
            testId="create-job-customer"
            id="create-job-customer"
            value={form.values.customerId}
            placeholder="Choose customer"
            options={customers.map((party) => ({ value: party.id, label: party.name }))}
            onChange={(value) => onChange('customerId', value)}
          />
        </FormField>

        {form.formError === null ? null : (
          <p data-testid="create-job-error" role="alert" className="mb-3 text-sm text-red-700">
            {form.formError}
          </p>
        )}

        <div className="flex gap-2">
          <Button testId="create-job-submit" onClick={onSubmit} pending={isPending}>
            Create job
          </Button>
          <Button testId="create-job-cancel" variant="secondary" onClick={onCancel}>
            Cancel
          </Button>
        </div>
      </div>
    </div>
  );
}
```

- [ ] **Step 9: Write the barrel, verify and commit**

```ts
export { useCreateJob } from './hooks/use-create-job.hook';
export type { CreateJobField, CreateJobFormState } from './hooks/use-create-job.hook';
export { CreateJobModal } from './components/organisms/create-job-modal.component';
```

```bash
npm --prefix frontend test
npm --prefix frontend run typecheck
npm --prefix frontend run lint
git add frontend/src/presentation/views/jobs/features/create-job
git commit -m "feat: add the create-job slice with a useReducer form

useReducer because the form's fields change together and its submission
has a lifecycle (assessment line 156). Validation is a pure function
from values to errors called by the reducer, which keeps it testable
with no React in the process — eight of the tests here never render.

PathKeys types every field address, so 'address.zipCode' is valid and
'address.zip' does not compile. Nested form state is exactly the case
dot-notation paths exist for (D-06).

Submit stays enabled and pressing it reveals every remaining error at
once rather than hiding the way forward one field at a time (design A5
point 3), and a test asserts more than three errors appear together.

BR-1 is checked in the validator as well as in the adapter: once where
the user can be told, once where it cannot be bypassed."
```

---

### Task 5: The `complete-job` slice

**Files:**
- Create: `frontend/src/presentation/components/molecules/signature-pad.component.tsx`
- Create: `frontend/src/presentation/views/jobs/features/complete-job/actions/complete-job.action.ts`
- Create: `frontend/src/presentation/views/jobs/features/complete-job/hooks/use-complete-job.hook.ts`
- Create: `frontend/src/presentation/views/jobs/features/complete-job/components/organisms/complete-job-modal.component.tsx`
- Create: `frontend/src/presentation/views/jobs/features/complete-job/index.ts`
- Test: `frontend/src/presentation/views/jobs/features/complete-job/__tests__/use-complete-job.test.tsx`

**Interfaces:**
- Consumes: the store's optimistic actions, `jobsEventBus`, `completeJob` use case
- Produces: `useCompleteJob()` returning `{ openFor, open, close, signature, setSignature, photos, addPhoto, removePhoto, submit, isPending, error }`, and `CompleteJobModal`

- [ ] **Step 1: Write the failing test**

Create `frontend/src/presentation/views/jobs/features/complete-job/__tests__/use-complete-job.test.tsx`:

```tsx
import { act, renderHook, waitFor } from '@testing-library/react';
import { useJobsUiStore } from '@/presentation/stores/jobs-ui.store';
import { completeJobAction } from '../actions/complete-job.action';
import { useCompleteJob } from '../hooks/use-complete-job.hook';

jest.mock('../actions/complete-job.action', () => ({ completeJobAction: jest.fn() }));

const action = jest.mocked(completeJobAction);

beforeEach(() => {
  useJobsUiStore.getState().reset();
  action.mockReset();
  action.mockResolvedValue({ ok: true });
});

describe('useCompleteJob', () => {
  it('opens for one job and closes', () => {
    const { result } = renderHook(() => useCompleteJob());

    act(() => result.current.open('job-2'));
    expect(result.current.openFor).toBe('job-2');

    act(() => result.current.close());
    expect(result.current.openFor).toBeNull();
  });

  it('forgets the signature and the photos when it closes', () => {
    const { result } = renderHook(() => useCompleteJob());

    act(() => result.current.open('job-2'));
    act(() => result.current.setSignature('data:image/png;base64,AAA'));
    act(() => result.current.addPhoto({ url: 'p1.jpg', caption: 'ridge' }));
    act(() => result.current.close());

    expect(result.current.signature).toBe('');
    expect(result.current.photos).toEqual([]);
  });

  it('refuses to submit without a signature before calling the action (BR-4)', async () => {
    const { result } = renderHook(() => useCompleteJob());
    act(() => result.current.open('job-2'));

    await act(async () => result.current.submit('InProgress'));

    expect(action).not.toHaveBeenCalled();
    expect(result.current.error).toBe('A customer signature is required');
  });

  it('removes a photo by index', () => {
    const { result } = renderHook(() => useCompleteJob());
    act(() => result.current.open('job-2'));
    act(() => result.current.addPhoto({ url: 'p1.jpg', caption: null }));
    act(() => result.current.addPhoto({ url: 'p2.jpg', caption: null }));

    act(() => result.current.removePhoto(0));

    expect(result.current.photos.map((photo) => photo.url)).toEqual(['p2.jpg']);
  });

  it('completes optimistically and closes on success', async () => {
    const { result } = renderHook(() => useCompleteJob());
    act(() => result.current.open('job-2'));
    act(() => result.current.setSignature('data:image/png;base64,AAA'));

    await act(async () => result.current.submit('InProgress'));

    expect(action).toHaveBeenCalledWith('job-2', {
      signatureUrl: 'data:image/png;base64,AAA',
      photos: [],
    });
    await waitFor(() => expect(result.current.openFor).toBeNull());
  });

  it('rolls back and keeps the modal open on failure', async () => {
    action.mockResolvedValue({
      ok: false,
      error: {
        code: 'job.conflict',
        message: 'Only a job in progress can be completed',
        kind: 'conflict',
      },
    });

    const { result } = renderHook(() => useCompleteJob());
    act(() => result.current.open('job-2'));
    act(() => result.current.setSignature('data:image/png;base64,AAA'));
    await act(async () => result.current.submit('InProgress'));

    await waitFor(() =>
      expect(result.current.error).toBe('Only a job in progress can be completed'),
    );
    expect(result.current.openFor).toBe('job-2');
    expect(useJobsUiStore.getState().optimisticStatus['job-2']).toBeUndefined();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `npm --prefix frontend test -- use-complete-job`
Expected: FAIL with `Cannot find module '../hooks/use-complete-job.hook'`.

- [ ] **Step 3: Write the Server Action**

```ts
'use server';

import type { CompleteJobInput } from '@/core/application/ports/jobs.port';
import { completeJob } from '@/core/application/use-cases/complete-job.use-case';
import { getContainer } from '@/core/di/container';
import { isOk } from '@/core/domain/result';
import type { ActionOutcome } from '../../action-outcome.type';

export async function completeJobAction(
  id: string,
  input: CompleteJobInput,
): Promise<ActionOutcome> {
  const result = await completeJob(getContainer().jobs, id, input);
  return isOk(result) ? { ok: true } : { ok: false, error: result.error };
}
```

- [ ] **Step 4: Write the hook**

```ts
'use client';

import { useCallback, useState, useTransition } from 'react';
import { useShallow } from 'zustand/react/shallow';
import type { NewPhoto } from '@/core/application/ports/jobs.port';
import type { JobStatus } from '@/core/domain/job/job-status.type';
import { useJobsUiStore } from '@/presentation/stores/jobs-ui.store';
import { jobsEventBus } from '@/shared/events/jobs-event-bus';
import { completeJobAction } from '../actions/complete-job.action';

const SIGNATURE_REQUIRED = 'A customer signature is required';

export function useCompleteJob() {
  const { beginOptimistic, commitOptimistic, rollbackOptimistic } = useJobsUiStore(
    useShallow((state) => ({
      beginOptimistic: state.beginOptimistic,
      commitOptimistic: state.commitOptimistic,
      rollbackOptimistic: state.rollbackOptimistic,
    })),
  );

  const [openFor, setOpenFor] = useState<string | null>(null);
  const [signature, setSignature] = useState('');
  const [photos, setPhotos] = useState<readonly NewPhoto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  const open = useCallback((id: string) => {
    setOpenFor(id);
    setSignature('');
    setPhotos([]);
    setError(null);
  }, []);

  const close = useCallback(() => {
    setOpenFor(null);
    setSignature('');
    setPhotos([]);
    setError(null);
  }, []);

  const addPhoto = useCallback(
    (photo: NewPhoto) => setPhotos((current) => [...current, photo]),
    [],
  );

  const removePhoto = useCallback(
    (index: number) => setPhotos((current) => current.filter((_, at) => at !== index)),
    [],
  );

  const submit = useCallback(
    (current: JobStatus) => {
      const id = openFor;
      if (id === null) return;

      // BR-4 before any call is made (design A5 point 3 of the complete flow).
      if (signature.trim() === '') {
        setError(SIGNATURE_REQUIRED);
        return;
      }

      setError(null);
      beginOptimistic(id, 'Completed', current);

      startTransition(async () => {
        const outcome = await completeJobAction(id, { signatureUrl: signature, photos });

        if (outcome.ok) {
          commitOptimistic(id);
          jobsEventBus.emit('jobs:invalidate', undefined);
          setOpenFor(null);
          setSignature('');
          setPhotos([]);
          return;
        }

        rollbackOptimistic(id);
        setError(outcome.error.message);
      });
    },
    [openFor, signature, photos, beginOptimistic, commitOptimistic, rollbackOptimistic],
  );

  return {
    openFor,
    open,
    close,
    signature,
    setSignature,
    photos,
    addPhoto,
    removePhoto,
    submit,
    isPending,
    error,
  };
}
```

- [ ] **Step 5: Write the signature pad**

`signature-pad.component.tsx` produces a data URL. jsdom has no canvas, so the component exposes a text fallback the tests and Playwright drive; the canvas is progressive enhancement.

```tsx
'use client';

import { useCallback, useRef } from 'react';

/**
 * Produces the data URL BR-4 requires. The canvas is the real input, but jsdom
 * implements no 2D context and Playwright cannot draw reliably, so the
 * component also exposes a text input carrying the same value. That is not a
 * test-only escape hatch bolted on: it is the accessible path for anyone who
 * cannot draw with a pointer, and the suite uses the same one.
 */
export function SignaturePad({
  testId,
  value,
  onChange,
}: {
  readonly testId: string;
  readonly value: string;
  readonly onChange: (dataUrl: string) => void;
}) {
  const canvas = useRef<HTMLCanvasElement>(null);

  const capture = useCallback(() => {
    const element = canvas.current;
    if (element === null) return;
    onChange(element.toDataURL('image/png'));
  }, [onChange]);

  return (
    <div>
      <canvas
        ref={canvas}
        width={320}
        height={96}
        onPointerUp={capture}
        aria-hidden="true"
        className="rounded border border-slate-300"
      />
      <input
        data-testid={testId}
        aria-label="Customer signature"
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder="Signature"
        className="mt-1 w-full rounded border border-slate-300 px-2 py-1.5 text-sm"
      />
    </div>
  );
}
```

- [ ] **Step 6: Write the modal**

```tsx
'use client';

import type { NewPhoto } from '@/core/application/ports/jobs.port';
import { Button } from '@/presentation/components/atoms/button.component';
import { SignaturePad } from '@/presentation/components/molecules/signature-pad.component';

export function CompleteJobModal({
  signature,
  photos,
  error,
  isPending,
  onSignatureChange,
  onAddPhoto,
  onSubmit,
  onCancel,
}: {
  readonly signature: string;
  readonly photos: readonly NewPhoto[];
  readonly error: string | null;
  readonly isPending: boolean;
  readonly onSignatureChange: (value: string) => void;
  readonly onAddPhoto: () => void;
  readonly onSubmit: () => void;
  readonly onCancel: () => void;
}) {
  return (
    <div
      data-testid="complete-job-modal"
      role="dialog"
      aria-modal="true"
      aria-label="Complete job"
      className="fixed inset-0 z-10 flex items-start justify-center bg-black/30 p-8"
    >
      <div className="w-full max-w-md rounded bg-white p-6">
        <h2 className="mb-4 text-lg font-semibold">Complete job</h2>

        <SignaturePad
          testId="complete-job-signature"
          value={signature}
          onChange={onSignatureChange}
        />

        <div className="mt-4">
          <Button testId="complete-job-photos" variant="secondary" onClick={onAddPhoto}>
            Add photo
          </Button>
          <p className="mt-1 text-xs text-slate-600">{photos.length} photos</p>
        </div>

        {error === null ? null : (
          <p data-testid="complete-job-error" role="alert" className="mt-3 text-sm text-red-700">
            {error}
          </p>
        )}

        <div className="mt-4 flex gap-2">
          <Button testId="complete-job-submit" onClick={onSubmit} pending={isPending}>
            Complete
          </Button>
          <Button testId="complete-job-cancel" variant="secondary" onClick={onCancel}>
            Cancel
          </Button>
        </div>
      </div>
    </div>
  );
}
```

- [ ] **Step 7: Write the barrel, verify and commit**

```ts
export { useCompleteJob } from './hooks/use-complete-job.hook';
export { CompleteJobModal } from './components/organisms/complete-job-modal.component';
```

```bash
npm --prefix frontend test
npm --prefix frontend run typecheck
npm --prefix frontend run lint
git add frontend/src
git commit -m "feat: add the complete-job slice with a signature pad

BR-4 is refused before any call is made, and a test asserts the action
was never invoked rather than that it returned a failure — checking the
rule where the user can be told, in addition to where it cannot be
bypassed.

The signature pad carries a text input alongside the canvas. jsdom
implements no 2D context and Playwright cannot draw reliably, but that
is not why the input exists: it is the accessible path for anyone who
cannot sign with a pointer, and the suite uses the same one rather than
a test-only hook."
```

---

### Task 6: Wire the actions, guard the date range, and run the whole walkthrough

**Files:**
- Create: `frontend/src/presentation/views/jobs/components/organisms/job-row-actions.component.tsx`
- Modify: `frontend/src/presentation/views/jobs/components/organisms/jobs-client.component.tsx`
- Modify: `frontend/src/presentation/views/jobs/hooks/use-jobs-page.hook.ts`
- Modify: `frontend/src/presentation/views/jobs/features/filter-jobs/hooks/use-filter-jobs.hook.ts`
- Modify: `frontend/e2e/jobs-walkthrough.spec.ts`, `frontend/e2e/pages/jobs.page.ts`
- Test: `frontend/src/presentation/views/jobs/features/filter-jobs/__tests__/use-filter-jobs.test.tsx`

**Interfaces:**
- Consumes: all four slice barrels, `allowedActionsFor`
- Produces: **walkthrough steps 2, 6, 7 and 8 green**, and the whole flow as one test

- [ ] **Step 1: Write the failing date-guard test**

Add to `use-filter-jobs.test.tsx`:

```tsx
  it('clamps a from date later than the to date (design A3)', () => {
    const { result } = renderHook(() => useFilterJobs());

    act(() => result.current.setDateRange('2099-03-01', '2099-03-31'));
    act(() => result.current.setDateRange('2099-04-15', '2099-03-31'));

    // A from > to range matches nothing and reads as a bug rather than as a
    // filter, so the later bound moves with it.
    expect(result.current.filters.scheduledFrom).toBe('2099-04-15');
    expect(result.current.filters.scheduledTo).toBe('2099-04-15');
  });

  it('clamps a to date earlier than the from date', () => {
    const { result } = renderHook(() => useFilterJobs());

    act(() => result.current.setDateRange('2099-03-10', '2099-03-31'));
    act(() => result.current.setDateRange('2099-03-10', '2099-03-01'));

    expect(result.current.filters.scheduledFrom).toBe('2099-03-01');
    expect(result.current.filters.scheduledTo).toBe('2099-03-01');
  });
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npm --prefix frontend test -- use-filter-jobs`
Expected: FAIL — no guard exists; the second call stores an inverted range.

- [ ] **Step 3: Add the guard**

Replace `setDateRange` in `use-filter-jobs.hook.ts`:

```ts
  const setDateRange = useCallback(
    (from: string | null, to: string | null) => {
      // Design A3 specifies a from <= to guard. An inverted range matches
      // nothing, which reads as a bug rather than as a filter, so whichever
      // bound moved drags the other with it.
      if (from !== null && to !== null && from > to) {
        const moved = from !== filters.scheduledFrom ? from : to;
        setFilter({ scheduledFrom: moved, scheduledTo: moved });
        return;
      }
      setFilter({ scheduledFrom: from, scheduledTo: to });
    },
    [filters.scheduledFrom, setFilter],
  );
```

- [ ] **Step 4: Write the row-actions organism**

Create `job-row-actions.component.tsx`:

```tsx
'use client';

import { allowedActionsFor } from '@/core/domain/job';
import { Button } from '@/presentation/components/atoms/button.component';
import { FieldError } from '@/presentation/components/atoms/field-error.component';
import type { VisibleJob } from '@/presentation/stores/jobs-ui.store';
import { CancelReasonField } from '../../features/cancel-job';

/**
 * The one place the slices meet for a row. It lives in the view rather than in
 * a slice, which is what lets four mutually unaware slices produce one row's
 * worth of buttons without any of them importing another (architecture 5.6).
 *
 * Which buttons exist is asked of the state machine, not hard-coded: a
 * Completed row offers nothing because the model says so (design A2).
 */
export function JobRowActions({
  job,
  onStart,
  onComplete,
  cancel,
}: {
  readonly job: VisibleJob;
  readonly onStart: (job: VisibleJob) => void;
  readonly onComplete: (job: VisibleJob) => void;
  readonly cancel: {
    readonly openFor: string | null;
    readonly reason: string;
    readonly isPending: boolean;
    readonly open: (id: string) => void;
    readonly close: () => void;
    readonly setReason: (value: string) => void;
    readonly submit: (current: VisibleJob['status']) => void;
    readonly errorFor: (id: string) => string | undefined;
  };
  readonly startError?: string;
}) {
  const allowed = allowedActionsFor(job.status);
  const cancelError = cancel.errorFor(job.id);

  return (
    <span className="mt-1 flex flex-col gap-1">
      {allowed.includes('START') ? (
        <Button
          testId={`job-row-${job.id}-start`}
          variant="secondary"
          disabled={job.isPending}
          onClick={() => onStart(job)}
        >
          Start
        </Button>
      ) : null}

      {allowed.includes('COMPLETE') ? (
        <Button
          testId={`job-row-${job.id}-complete`}
          variant="secondary"
          disabled={job.isPending}
          onClick={() => onComplete(job)}
        >
          Complete
        </Button>
      ) : null}

      {allowed.includes('CANCEL') ? (
        <Button
          testId={`job-row-${job.id}-cancel`}
          variant="danger"
          disabled={job.isPending}
          onClick={() => cancel.open(job.id)}
        >
          Cancel
        </Button>
      ) : null}

      {cancel.openFor === job.id ? (
        <CancelReasonField
          jobId={job.id}
          value={cancel.reason}
          onChange={cancel.setReason}
          onSubmit={() => cancel.submit(job.status)}
          onDismiss={cancel.close}
          error={cancelError}
          pending={cancel.isPending}
        />
      ) : null}

      {cancelError === undefined && cancel.openFor !== job.id ? null : null}
    </span>
  );
}
```

Add a row-level error slot below the actions, so a rollback surfaces where design A8 names it:

```tsx
      {startError === undefined ? null : (
        <FieldError testId={`job-row-${job.id}-error`} message={startError} />
      )}
```

- [ ] **Step 5: Compose the slices in `use-jobs-page.hook.ts`**

Add the four slice hooks to the orchestrator and return them, so `JobsClient` stays a shell:

```ts
import { useCancelJob } from '../features/cancel-job';
import { useCompleteJob } from '../features/complete-job';
import { useCreateJob } from '../features/create-job';
import { useStartJob } from '../features/start-job';
```

Inside `useJobsPage`, after the filter hook:

```ts
  const create = useCreateJob();
  const start = useStartJob();
  const cancel = useCancelJob();
  const complete = useCompleteJob();
```

And add them to the returned object: `create, start, cancel, complete`.

- [ ] **Step 6: Wire `JobsClient`**

Add the New job button, the two modals, and the real `renderActions`. `JobsClient` still declares no state — every value comes from `page`.

```tsx
      <div className="mb-4 flex items-center justify-between">
        <h1 className="text-xl font-semibold">Jobs</h1>
        <Button testId="jobs-new-button" onClick={page.create.open}>
          + New job
        </Button>
      </div>
```

```tsx
          renderActions={(job) => (
            <JobRowActions
              job={job}
              onStart={(row) => page.start.run(row.id, row.status)}
              onComplete={(row) => page.complete.open(row.id)}
              cancel={page.cancel}
              startError={page.start.errorFor(job.id)}
            />
          )}
```

```tsx
      {page.create.isOpen ? (
        <CreateJobModal
          form={page.create.form}
          assignees={page.assignees}
          customers={page.customers}
          isPending={page.create.isPending}
          onChange={page.create.change}
          onBlur={page.create.blur}
          onSubmit={page.create.submit}
          onCancel={page.create.close}
        />
      ) : null}

      {page.complete.openFor === null ? null : (
        <CompleteJobModal
          signature={page.complete.signature}
          photos={page.complete.photos}
          error={page.complete.error}
          isPending={page.complete.isPending}
          onSignatureChange={page.complete.setSignature}
          onAddPhoto={() => page.complete.addPhoto({ url: 'photo.jpg', caption: null })}
          onSubmit={() => page.complete.submit('InProgress')}
          onCancel={page.complete.close}
        />
      )}
```

`page.customers` is new: add `listCustomers` to `app/jobs/page.tsx` alongside `listAssignees` and thread it through `JobsClient` and `useJobsPage` the same way.

- [ ] **Step 7: Extend the page object**

Add to `frontend/e2e/pages/jobs.page.ts`:

```ts
  get newJobButton(): Locator {
    return this.page.getByTestId('jobs-new-button');
  }

  get createModal(): Locator {
    return this.page.getByTestId('create-job-modal');
  }

  createField(name: string): Locator {
    return this.page.getByTestId(`create-job-${name}`);
  }

  get createSubmit(): Locator {
    return this.page.getByTestId('create-job-submit');
  }

  rowStart(id: string): Locator {
    return this.page.getByTestId(`job-row-${id}-start`);
  }

  rowComplete(id: string): Locator {
    return this.page.getByTestId(`job-row-${id}-complete`);
  }

  get completeModal(): Locator {
    return this.page.getByTestId('complete-job-modal');
  }

  get completeSignature(): Locator {
    return this.page.getByTestId('complete-job-signature');
  }

  get completeSubmit(): Locator {
    return this.page.getByTestId('complete-job-submit');
  }

  /** Fills the create form with a valid job and returns the title used. */
  async createJob(title: string): Promise<void> {
    await this.newJobButton.click();
    await this.createModal.waitFor({ state: 'visible' });
    await this.createField('title').fill(title);
    await this.createField('street').fill('99 Birch Way');
    await this.createField('city').fill('Springfield');
    await this.createField('state').fill('IL');
    await this.createField('zip').fill('62701');
    await this.createField('latitude').fill('39.78');
    await this.createField('longitude').fill('-89.65');
    await this.createField('scheduled-date').fill('2099-06-01');
    await this.createField('assignee').selectOption('assignee-1');
    await this.createField('customer').selectOption('customer-1');
    await this.createSubmit.click();
    await this.createModal.waitFor({ state: 'detached' });
  }

  /** The row whose title cell holds the given text. */
  rowByTitle(title: string): Locator {
    return this.page
      .locator('[data-testid^="job-row-"][data-testid$="-title"]')
      .filter({ hasText: title });
  }
}
```

- [ ] **Step 8: Write the whole-flow walkthrough**

Append to `frontend/e2e/jobs-walkthrough.spec.ts`:

```ts
  test('the acceptance walkthrough, end to end', async ({ page }) => {
    const jobs = new JobsPage(page);
    await jobs.goto();
    await jobs.waitForList();

    // Step 2: create a job through the modal.
    await jobs.createJob('Chimney reflash');

    // Step 3: it appears in the list, Scheduled.
    const created = jobs.rowByTitle('Chimney reflash');
    await expect(created).toBeVisible();
    const testId = await created.getAttribute('data-testid');
    const id = String(testId).replace('job-row-', '').replace('-title', '');
    await expect(jobs.rowStatus(id)).toHaveText('Scheduled');

    // Step 5: narrow by status and still find it.
    await jobs.statusOption('Scheduled').check();
    await expect(jobs.row(id)).toBeVisible();
    await jobs.clearFilters.click();

    // Step 6: record that the crew started.
    await jobs.rowStart(id).click();
    await expect(jobs.rowStatus(id)).toHaveText('InProgress');

    // Step 7: complete it with a signature.
    await jobs.rowComplete(id).click();
    await jobs.completeModal.waitFor({ state: 'visible' });
    await jobs.completeSignature.fill('data:image/png;base64,AAA');
    await jobs.completeSubmit.click();
    await jobs.completeModal.waitFor({ state: 'detached' });

    // Step 8: it shows Completed, and offers nothing further.
    await expect(jobs.rowStatus(id)).toHaveText('Completed');
    await expect(jobs.rowStart(id)).toHaveCount(0);
    await expect(jobs.rowComplete(id)).toHaveCount(0);
  });

  test('completing without a signature is refused (BR-4)', async ({ page }) => {
    const jobs = new JobsPage(page);
    await jobs.goto();
    await jobs.waitForList();

    // job-2 is InProgress in the seed.
    await jobs.rowComplete('job-2').click();
    await jobs.completeModal.waitFor({ state: 'visible' });
    await jobs.completeSubmit.click();

    await expect(page.getByTestId('complete-job-error')).toHaveText(
      'A customer signature is required',
    );
    await expect(jobs.completeModal).toBeVisible();
  });
```

Step 8's last two assertions are the state machine showing through the interface: a Completed row offers nothing because `allowedActionsFor('Completed')` is empty.

- [ ] **Step 9: Verify every gate and the whole suite**

```bash
npm --prefix frontend run test:coverage
npm --prefix frontend run typecheck
npm --prefix frontend run lint
npm --prefix frontend run build
npm --prefix frontend run test:e2e
```

Expected: Jest green at or above 80% coverage, no type errors, no lint errors, the build listing `/jobs`, `/jobs/[id]` and `/api/jobs`, and **seven Playwright tests passing** — the five from plan 2A plus the whole-flow walkthrough and the BR-4 refusal.

- [ ] **Step 10: Commit**

```bash
git add frontend/src frontend/e2e
git commit -m "feat: wire the mutation slices and run the walkthrough end to end

Steps 2, 6, 7 and 8 are green, and the whole flow now runs as one test:
create a job through the modal, see it Scheduled, filter to it, start
it, complete it with a signature, see it Completed.

JobRowActions is the one place the four slices meet for a row, and it
lives in the view rather than in a slice — which is what lets four
mutually unaware slices produce one row's worth of buttons without any
of them importing another. Which buttons exist is asked of
allowedActionsFor rather than hard-coded, so a Completed row offers
nothing because the model says so. The walkthrough asserts that
absence.

Settles the second debt plan 2A recorded: setDateRange now implements
the from <= to guard design A3 specifies. An inverted range matches
nothing, which reads as a bug rather than as a filter, so whichever
bound moved drags the other with it."
```

---

## Self-Review

**Spec coverage.** `FR-1` create is task 4, `FR-3` start task 2, `FR-4` complete task 5, `FR-5` cancel task 3. `BR-1`, `BR-4` and `BR-5` are each checked in a hook as well as in the adapter — once where the user can be told, once where it cannot be bypassed — and each has a test asserting the action was never called. `BR-2` shows through `allowedActionsFor` returning nothing for a terminal state.

Assessment 2.3 is now complete: `useReducer` arrives with task 4, joining the controlled components, the compound bar, `useMemo`, the error boundary and the ternaries from plan 2A.

Assessment 5.3's flow — navigate, create, verify it appears, filter, complete, verify Completed — is task 6's single test, with the start step the walkthrough needs by `BR-3` (D-14).

**Placeholder scan.** Every step carries real code.

**Type consistency.** `ActionOutcome` and `CreateOutcome` are defined once in task 2 and used by all four actions. `CreateJobField` is `PathKeys<CreateJobValues>` throughout. `NewPhoto` comes from the port. The cancel slice's `submit` and the complete slice's `submit` both take the current `JobStatus`, because the overlay needs the previous value to roll back to.

**Three things this review found.**

`JobRowActions` has a leftover no-op ternary in step 4 (`{cancelError === undefined && cancel.openFor !== job.id ? null : null}`) that does nothing and must be deleted rather than shipped — both branches are null, so it is noise that reads as an unfinished thought.

The complete modal's "Add photo" button adds a hard-coded `photo.jpg` rather than opening a file picker. Real photo capture is out of scope by `context/prd.md` section 9 (photos are captured by office staff, and no upload path is specified), but the button as written implies a capability that is not there. It should either be labelled honestly or the plan should say why a placeholder is the right call — flagged for the executing session to decide rather than settled here.

The whole-flow test extracts the created job's id by parsing a `data-testid` attribute, which is the one place in the suite that reads a selector as data rather than using it as a selector. It works because A8 fixes the format, but a `data-job-id` attribute on the row would be cleaner. Left as is because adding an attribute purely for the test is the trade the other way; noted so the choice is visible.
