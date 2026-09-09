import type { ActionFor, JobAction, JobState, ResultOf } from './job-state.type';

/**
 * Re-narrows `current` to the state the action's precondition already
 * guarantees.
 *
 * It throws rather than substituting a default, because reaching the throw
 * would mean the type-level transition table and the switch below had drifted
 * apart — a defect, not an expected failure, and CLAUDE.md reserves exceptions
 * for exactly that. A `'x' in state ? state.x : fallback` helper would instead
 * carry a branch no valid call can reach, which is both a smell and an
 * unreachable branch that drags coverage down.
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

/**
 * The signature on assessment line 81 — `transitionJob(current: JobState, …)` —
 * accepts the entire union, so every state is assignable and no call can ever
 * be a compile error. It cannot satisfy line 87.
 *
 * Constraining `A` by the *narrowed* type of `current` is what makes an invalid
 * pair unrepresentable: for a terminal state `AllowedAction[S['status']]` is
 * `never`, so `ActionFor<S>` is `never` and no argument can satisfy `A`.
 *
 * The consequence, stated plainly: the caller must narrow before calling. Given
 * a value of type `JobState`, discriminate on `status` first. That is correct
 * behaviour, not a limitation.
 */
export function transitionJob<S extends JobState, A extends ActionFor<S>>(
  current: S,
  action: A,
): ResultOf<A> {
  // `A` is constrained by a conditional type over `S`, which TypeScript cannot
  // resolve inside this body — so switching on `action.type` does not narrow
  // `A` and every field access fails. Widening to the union `A` is a subset of
  // restores discriminated narrowing, and costs no assertion: `ActionFor<S>` is
  // an `Extract` from `JobAction`, so the assignment is plainly safe.
  const step: JobAction = action;

  switch (step.type) {
    case 'SCHEDULE':
      return {
        status: 'Scheduled',
        scheduledDate: step.scheduledDate,
        assigneeId: step.assigneeId,
      } as ResultOf<A>;

    case 'START': {
      const from = narrow(current, 'Scheduled');
      return {
        status: 'InProgress',
        startedAt: step.startedAt,
        assigneeId: from.assigneeId,
        photos: [],
      } as ResultOf<A>;
    }

    case 'COMPLETE': {
      const from = narrow(current, 'InProgress');
      return {
        status: 'Completed',
        startedAt: from.startedAt,
        completedAt: step.completedAt,
        assigneeId: from.assigneeId,
        photos: from.photos,
        signatureUrl: step.signatureUrl,
      } as ResultOf<A>;
    }

    case 'CANCEL':
      return {
        status: 'Cancelled',
        cancelledAt: step.cancelledAt,
        reason: step.reason,
      } as ResultOf<A>;
  }
}

export type { JobAction };
