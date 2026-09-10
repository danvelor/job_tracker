import type { JobStatus } from './job-status.type';

export type Party = { readonly id: string; readonly name: string };

export type JobSummary = {
  readonly id: string;
  readonly title: string;
  readonly status: JobStatus;
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
