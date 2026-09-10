import type { AllowedAction, JobAction } from './job-state.type';
import type { JobStatus } from './job-status.type';

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
