import type { AllowedAction, JobAction } from './job-state.type';
import type { JobStatus } from './job-status.type';

/**
 * The runtime mirror of the `AllowedAction` table. The type-level version
 * cannot be read at runtime, and the UI needs to know which buttons a row may
 * show — architecture 5.7: the UI asks the state machine which actions a row
 * offers, so an invalid action cannot be rendered.
 *
 * The mapped type on the value is what keeps the two statements of the rule in
 * step: a missing status fails to compile, and an action a state does not
 * permit is not assignable.
 */
const ALLOWED: {
  [S in JobStatus]: readonly Extract<JobAction['type'], AllowedAction[S]>[];
} = {
  Draft: ['SCHEDULE'],
  Scheduled: ['START', 'CANCEL'],
  InProgress: ['COMPLETE', 'CANCEL'],
  Completed: [],
  Cancelled: [],
};

export function allowedActionsFor(status: JobStatus): readonly JobAction['type'][] {
  return ALLOWED[status];
}
