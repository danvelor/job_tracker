import 'server-only';
import type { JobSearchQuery, JobSortField } from '@/core/application/ports/jobs.port';
import { searchJobs } from '@/core/application/use-cases/search-jobs.use-case';
import { getContainer } from '@/core/di/container';
import type { JobStatus } from '@/core/domain/job/job-status.type';
import { isOk } from '@/core/domain/result';

const STATUSES: readonly string[] = [
  'Draft',
  'Scheduled',
  'InProgress',
  'Completed',
  'Cancelled',
];

const isStatus = (value: string): value is JobStatus => STATUSES.includes(value);

const optional = (value: string | null): string | undefined =>
  value === null || value === '' ? undefined : value;

const problem = (title: string, status: number, errorCode: string): Response =>
  Response.json(
    { title, status, errorCode },
    { status, headers: { 'content-type': 'application/problem+json' } },
  );

/**
 * The read endpoint SWR fetches (D-29).
 *
 * Reads cannot be Server Actions (assessment line 138) and the browser cannot
 * hold the API token (architecture 7.1), so this handler is the only place a
 * client-side query can go. In plan 3 it is also the only place
 * HttpJobsAdapter's bearer token can live.
 */
export async function GET(request: Request): Promise<Response> {
  const params = new URL(request.url).searchParams;

  const rawLimit = params.get('limit');
  const limit = rawLimit === null || rawLimit === '' ? undefined : Number(rawLimit);
  if (limit !== undefined && !Number.isInteger(limit)) {
    return problem('limit must be an integer', 400, 'query.limit');
  }

  // An unknown status filters nothing rather than everything: a typo in a
  // query string should not silently empty the list.
  const statuses = params.getAll('statuses').filter(isStatus);
  const sort = params.get('sort');

  const query: Partial<JobSearchQuery> = {
    text: optional(params.get('text')),
    statuses: statuses.length === 0 ? undefined : statuses,
    scheduledFrom: optional(params.get('scheduledFrom')),
    scheduledTo: optional(params.get('scheduledTo')),
    assigneeId: optional(params.get('assigneeId')),
    sort: sort === 'title' || sort === 'scheduledDate' ? (sort satisfies JobSortField) : undefined,
    cursor: optional(params.get('cursor')) ?? null,
    limit,
  };

  const result = await searchJobs(getContainer().jobs, query);

  return isOk(result)
    ? Response.json(result.value)
    : problem(result.error.message, 500, result.error.code);
}
