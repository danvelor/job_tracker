import type { CoreError, Result } from '@/core/domain/result.type';
import type { JobStatus } from '@/core/domain/job/job-status.type';
import type { JobDetail, JobSummary, Party } from '@/core/domain/job/job-summary.type';

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
