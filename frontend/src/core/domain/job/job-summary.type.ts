import type { JobStatus } from './job-status.type';

/**
 * An assignee or a customer, as the pickers and the job row need them.
 * Backed by a read-only roster the `jobs` schema owns (D-26), not by a name
 * copied onto the job.
 */
export type Party = { readonly id: string; readonly name: string };

/**
 * Dates cross this boundary as ISO strings rather than `Date`: they arrive
 * from JSON, and a `Date` passed as a Server Component prop would not survive
 * serialisation.
 */
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

export type JobDetail = Omit<JobSummary, 'address'> & {
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
  readonly cancelledAt: string | null;
  readonly cancellationReason: string | null;
  readonly photos: readonly JobPhoto[];
};
