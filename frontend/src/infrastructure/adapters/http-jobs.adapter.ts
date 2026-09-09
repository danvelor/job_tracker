import { err, ok } from '@/core/domain/result';
import type { CoreError, Result } from '@/core/domain/result.type';
import type { JobDetail, JobSummary, Party } from '@/core/domain/job/job-summary.type';
import type { JobStatus } from '@/core/domain/job/job-status.type';
import type {
  CompleteJobInput,
  CreateJobInput,
  JobSearchQuery,
  JobsPort,
  PagedJobs,
} from '@/core/application/ports/jobs.port';
import { toCoreError, toTransportError } from '@/infrastructure/api/problem';
import { createDevTokenSource, type TokenSource } from '@/infrastructure/api/token';

export type HttpJobsAdapterConfig = {
  readonly baseUrl: string;
  readonly organizationId?: string;
  readonly token?: TokenSource;
};

/** The development organization the seed migration creates (RosterSeed). */
const DEVELOPMENT_ORGANIZATION = '11111111-1111-1111-1111-111111111111';

/** What `GET /api/jobs` returns per row: the address is flat on the wire. */
type JobRowResponse = {
  readonly id: string;
  readonly title: string;
  readonly status: JobStatus;
  readonly scheduledDate: string | null;
  readonly assigneeId: string | null;
  readonly assigneeName: string | null;
  readonly street: string;
  readonly city: string;
  readonly state: string;
  readonly photoCount: number;
};

type JobDetailResponse = Omit<JobRowResponse, 'photoCount'> & {
  readonly description: string | null;
  readonly startedAt: string | null;
  readonly completedAt: string | null;
  readonly cancelledAt: string | null;
  readonly cancellationReason: string | null;
  readonly signatureUrl: string | null;
  readonly zipCode: string;
  readonly latitude: number;
  readonly longitude: number;
  readonly photos: readonly {
    readonly id: string;
    readonly url: string;
    readonly capturedAt: string;
    readonly caption: string | null;
  }[];
};

/**
 * The same word the in-memory adapter uses. The two must agree on what the user
 * reads, not only on what the types say.
 */
const UNASSIGNED = 'Unassigned';

const toSummary = (row: JobRowResponse): JobSummary => ({
  id: row.id,
  title: row.title,
  status: row.status,
  scheduledDate: row.scheduledDate,
  assigneeId: row.assigneeId ?? '',
  assigneeName: row.assigneeName ?? UNASSIGNED,
  address: { street: row.street, city: row.city, state: row.state },
  photoCount: row.photoCount,
});

const toDetail = (body: JobDetailResponse): JobDetail => ({
  id: body.id,
  title: body.title,
  description: body.description,
  status: body.status,
  scheduledDate: body.scheduledDate,
  assigneeId: body.assigneeId ?? '',
  assigneeName: body.assigneeName ?? UNASSIGNED,
  address: {
    street: body.street,
    city: body.city,
    state: body.state,
    zipCode: body.zipCode,
    latitude: body.latitude,
    longitude: body.longitude,
  },
  startedAt: body.startedAt,
  completedAt: body.completedAt,
  cancelledAt: body.cancelledAt,
  cancellationReason: body.cancellationReason,
  signatureUrl: body.signatureUrl,
  photoCount: body.photos.length,
  photos: body.photos,
});

function searchParams(query: JobSearchQuery): string {
  const params = new URLSearchParams();

  // Set, never assigned blank. `?text=` is a different request from no text at
  // all, and the API would be within its rights to read it as a search for the
  // empty string.
  const set = (key: string, value: string | undefined | null): void => {
    if (value !== undefined && value !== null && value !== '') {
      params.set(key, value);
    }
  };

  set('text', query.text);
  set('scheduledFrom', query.scheduledFrom);
  set('scheduledTo', query.scheduledTo);
  set('assigneeId', query.assigneeId);
  set('sort', query.sort);
  set('cursor', query.cursor);
  params.set('limit', String(query.limit));

  for (const status of query.statuses ?? []) {
    params.append('statuses', status);
  }

  return params.toString();
}

/**
 * The other implementation of `JobsPort`. It agrees with the in-memory adapter
 * behaviourally — the same refusal produces the same `CoreError.kind` — which
 * is what lets the end-to-end suite run against one and production against the
 * other (D-01).
 *
 * Nothing here throws. A refusal, a server fault and an unreachable host are
 * all `Result` failures, because a thrown error escapes into an error boundary
 * and loses the code the user would quote.
 */
export function createHttpJobsAdapter(config: HttpJobsAdapterConfig): JobsPort {
  const baseUrl = config.baseUrl.replace(/\/+$/, '');
  const token =
    config.token ??
    createDevTokenSource(baseUrl, config.organizationId ?? DEVELOPMENT_ORGANIZATION);

  async function request<T>(
    path: string,
    init: RequestInit,
    read: (response: Response) => Promise<T>,
  ): Promise<Result<T, CoreError>> {
    try {
      const response = await fetch(`${baseUrl}${path}`, {
        ...init,
        headers: {
          ...(init.body === undefined ? {} : { 'content-type': 'application/json' }),
          authorization: `Bearer ${await token()}`,
        },
        cache: 'no-store',
      });

      return response.ok ? ok(await read(response)) : err(await toCoreError(response));
    } catch (cause: unknown) {
      // A refused connection, a DNS failure, a token endpoint that is down.
      // None of them has a status, and all of them are still Results.
      return err(toTransportError(cause));
    }
  }

  const nothing = (): Promise<void> => Promise.resolve();

  const post = (path: string, body?: unknown): Promise<Result<void, CoreError>> =>
    request(
      path,
      { method: 'POST', ...(body === undefined ? {} : { body: JSON.stringify(body) }) },
      nothing,
    );

  return {
    async search(query: JobSearchQuery): Promise<Result<PagedJobs, CoreError>> {
      return request(`/api/jobs?${searchParams(query)}`, { method: 'GET' }, async (response) => {
        const body = (await response.json()) as {
          readonly items: readonly JobRowResponse[];
          readonly nextCursor: string | null;
        };

        return { items: body.items.map(toSummary), nextCursor: body.nextCursor };
      });
    },

    getById(id: string): Promise<Result<JobDetail, CoreError>> {
      return request(`/api/jobs/${id}`, { method: 'GET' }, async (response) =>
        toDetail((await response.json()) as JobDetailResponse),
      );
    },

    create(input: CreateJobInput): Promise<Result<string, CoreError>> {
      // The port nests the address; the API takes it flat. Mapping here is
      // what keeps the wire shape out of the slices.
      return request(
        '/api/jobs',
        {
          method: 'POST',
          body: JSON.stringify({
            title: input.title,
            description: input.description === '' ? null : input.description,
            street: input.address.street,
            city: input.address.city,
            state: input.address.state,
            zipCode: input.address.zipCode,
            latitude: input.address.latitude,
            longitude: input.address.longitude,
            scheduledDate: input.scheduledDate,
            assigneeId: input.assigneeId,
            customerId: input.customerId,
          }),
        },
        async (response) => ((await response.json()) as { readonly id: string }).id,
      );
    },

    start(id: string): Promise<Result<void, CoreError>> {
      return post(`/api/jobs/${id}/start`);
    },

    complete(id: string, input: CompleteJobInput): Promise<Result<void, CoreError>> {
      return post(`/api/jobs/${id}/complete`, {
        signatureUrl: input.signatureUrl,
        photos: input.photos.map((photo) => ({ url: photo.url, caption: photo.caption })),
      });
    },

    cancel(id: string, reason: string): Promise<Result<void, CoreError>> {
      return post(`/api/jobs/${id}/cancel`, { reason });
    },

    assignees(): Promise<Result<readonly Party[], CoreError>> {
      return request('/api/assignees', { method: 'GET' }, async (response) =>
        (await response.json()) as readonly Party[],
      );
    },

    customers(): Promise<Result<readonly Party[], CoreError>> {
      return request('/api/customers', { method: 'GET' }, async (response) =>
        (await response.json()) as readonly Party[],
      );
    },
  };
}
