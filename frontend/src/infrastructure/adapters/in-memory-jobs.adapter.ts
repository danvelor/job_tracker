import { err, ok } from '@/core/domain/result';
import type { CoreError, Result } from '@/core/domain/result.type';
import type { JobDetail, JobSummary, Party } from '@/core/domain/job/job-summary.type';
import type {
  CompleteJobInput,
  CreateJobInput,
  JobSearchQuery,
  JobsPort,
  PagedJobs,
} from '@/core/application/ports/jobs.port';
import { SEED_ASSIGNEES, SEED_CUSTOMERS, seedJobs } from './seed';
import type { SeedJob } from './seed';

const conflict = (message: string): CoreError => ({
  code: 'job.conflict',
  message,
  kind: 'conflict',
});

const notFound = (): CoreError => ({
  code: 'job.not-found',
  message: 'No job with that identifier',
  kind: 'not-found',
});

const invalid = (message: string, field: string): CoreError => ({
  code: 'job.validation',
  message,
  kind: 'validation',
  fieldErrors: { [field]: message },
});

const nameOf = (roster: readonly Party[], id: string): string =>
  roster.find((party) => party.id === id)?.name ?? 'Unassigned';

export function createInMemoryJobsAdapter(): JobsPort {
  const jobs: SeedJob[] = seedJobs();
  let sequence = 0;

  const toSummary = (job: SeedJob): JobSummary => ({
    id: job.id,
    title: job.title,
    status: job.status,
    scheduledDate: job.scheduledDate,
    assigneeId: job.assigneeId,
    assigneeName: nameOf(SEED_ASSIGNEES, job.assigneeId),
    address: {
      street: job.address.street,
      city: job.address.city,
      state: job.address.state,
    },
    photoCount: job.photos.length,
  });

  const find = (id: string): SeedJob | undefined => jobs.find((job) => job.id === id);

  const isTerminal = (job: SeedJob): boolean =>
    job.status === 'Completed' || job.status === 'Cancelled';

  return {
    async search(query: JobSearchQuery): Promise<Result<PagedJobs, CoreError>> {
      const text = query.text?.trim().toLowerCase();

      const matched = jobs
        .filter((job) => query.statuses?.includes(job.status) ?? true)
        .filter((job) => (query.assigneeId ? job.assigneeId === query.assigneeId : true))
        .filter((job) =>
          query.scheduledFrom ? job.scheduledDate >= query.scheduledFrom : true,
        )
        .filter((job) => (query.scheduledTo ? job.scheduledDate <= query.scheduledTo : true))
        .filter((job) =>
          text === undefined || text === ''
            ? true
            : `${job.title} ${job.description}`.toLowerCase().includes(text),
        )
        .sort((left, right) =>
          query.sort === 'title'
            ? left.title.localeCompare(right.title) || left.id.localeCompare(right.id)
            : right.scheduledDate.localeCompare(left.scheduledDate) ||
              right.id.localeCompare(left.id),
        );

      const start =
        query.cursor === null || query.cursor === undefined
          ? 0
          : matched.findIndex((job) => job.id === query.cursor) + 1;

      const page = matched.slice(start, start + query.limit);
      const consumed = start + page.length;
      const last = page.at(-1);

      return ok({
        items: page.map(toSummary),
        nextCursor: last !== undefined && consumed < matched.length ? last.id : null,
      });
    },

    async getById(id: string): Promise<Result<JobDetail, CoreError>> {
      const job = find(id);
      if (job === undefined) return err(notFound());

      return ok({
        ...toSummary(job),
        description: job.description === '' ? null : job.description,
        address: job.address,
        startedAt: job.startedAt,
        completedAt: job.completedAt,
        signatureUrl: job.signatureUrl,
        cancelledAt: job.cancelledAt,
        cancellationReason: job.cancellationReason,
        photos: job.photos,
      });
    },

    async create(input: CreateJobInput): Promise<Result<string, CoreError>> {
      if (input.title.trim() === '') {
        return err(invalid('A title is required', 'title'));
      }

      const today = new Date().toISOString().slice(0, 10);
      if (input.scheduledDate < today) {
        return err(invalid('A job cannot be scheduled in the past', 'scheduledDate'));
      }

      sequence += 1;
      const id = `job-new-${sequence}`;
      jobs.push({
        id,
        title: input.title,
        description: input.description,
        status: 'Scheduled',
        scheduledDate: input.scheduledDate,
        assigneeId: input.assigneeId,
        customerId: input.customerId,
        address: { ...input.address },
        startedAt: null,
        completedAt: null,
        cancelledAt: null,
        cancellationReason: null,
        signatureUrl: null,
        photos: [],
      });

      return ok(id);
    },

    async start(id: string): Promise<Result<void, CoreError>> {
      const job = find(id);
      if (job === undefined) return err(notFound());
      if (job.status !== 'Scheduled') {
        return err(
          conflict(
            isTerminal(job)
              ? 'A job in a terminal state cannot change state'
              : 'Only a Scheduled job can start',
          ),
        );
      }

      job.status = 'InProgress';
      job.startedAt = new Date().toISOString();
      return ok(undefined);
    },

    async complete(id: string, input: CompleteJobInput): Promise<Result<void, CoreError>> {
      const job = find(id);
      if (job === undefined) return err(notFound());

      if (input.signatureUrl.trim() === '') {
        return err(invalid('A customer signature is required', 'signatureUrl'));
      }
      if (job.status !== 'InProgress') {
        return err(conflict('Only a job in progress can be completed'));
      }

      const capturedAt = new Date().toISOString();
      job.status = 'Completed';
      job.completedAt = capturedAt;
      job.signatureUrl = input.signatureUrl;
      job.photos = input.photos.map((photo, index) => ({
        id: `${id}-photo-${index + 1}`,
        url: photo.url,
        capturedAt,
        caption: photo.caption,
      }));
      return ok(undefined);
    },

    async cancel(id: string, reason: string): Promise<Result<void, CoreError>> {
      const job = find(id);
      if (job === undefined) return err(notFound());
      if (reason.trim() === '') {
        return err(invalid('A cancellation reason is required', 'reason'));
      }
      if (isTerminal(job)) {
        return err(conflict('A job in a terminal state cannot change state'));
      }

      job.status = 'Cancelled';
      job.cancelledAt = new Date().toISOString();
      job.cancellationReason = reason;
      return ok(undefined);
    },

    async assignees(): Promise<Result<readonly Party[], CoreError>> {
      return ok(SEED_ASSIGNEES);
    },

    async customers(): Promise<Result<readonly Party[], CoreError>> {
      return ok(SEED_CUSTOMERS);
    },
  };
}
