import type { JobStatus } from '@/core/domain/job/job-status.type';
import { createTypedEventEmitter } from './typed-event-emitter';

/**
 * The cross-slice bus. It is what lets rule 2 of architecture 5.6 hold — no
 * slice imports another — and it is one-way by architecture 9.3: slices emit,
 * the view and the store react, and no subscriber emits in response to what it
 * received.
 *
 * Plan 2B is where the mutation slices emit on it. The view subscribes here,
 * so the wiring is proven before a publisher exists.
 */
export type JobsEventMap = {
  'job:status-changed': { jobId: string; status: JobStatus };
  'job:rollback': { jobId: string; previous: JobStatus; error: string };
  'jobs:invalidate': undefined;
};

export const jobsEventBus = createTypedEventEmitter<JobsEventMap>();
