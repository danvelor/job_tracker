import type { JobStatus } from '@/core/domain/job/job-status.type';
import { createTypedEventEmitter } from './typed-event-emitter';

export type JobsEventMap = {
  'job:status-changed': { jobId: string; status: JobStatus };
  'job:rollback': { jobId: string; previous: JobStatus; error: string };
  'jobs:invalidate': undefined;
};

export const jobsEventBus = createTypedEventEmitter<JobsEventMap>();
