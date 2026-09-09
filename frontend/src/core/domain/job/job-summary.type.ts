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
  /**
   * Null for a job with no date. `Draft` is the state that has none, and D-14
   * makes it unreachable through the API — but the column is nullable and the
   * alternative is for the HTTP adapter to invent a date to satisfy this type.
   * A fabricated label is a placeholder a reader understands; a fabricated date
   * is something a scheduler acts on.
   */
  readonly scheduledDate: string | null;
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
