import type { ActionFor, JobAction, JobState, ResultOf } from './job-state.type';

function narrow<K extends JobState['status']>(
  state: JobState,
  status: K,
): Extract<JobState, { status: K }> {
  if (state.status !== status) {
    throw new Error(`transitionJob: expected ${status}, received ${state.status}`);
  }
  return state as Extract<JobState, { status: K }>;
}

export function transitionJob<S extends JobState, A extends ActionFor<S>>(
  current: S,
  action: A,
): ResultOf<A> {
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
