import type { CoreError, Result } from '@/core/domain/result.type';
import type { JobSearchQuery, JobsPort, PagedJobs } from '../ports/jobs.port';

export const DEFAULT_PAGE_SIZE = 20;

/**
 * Owning the default page size is the one policy that belongs at this layer:
 * an adapter should not invent how many rows a caller wanted, and a caller
 * should not have to remember.
 */
export function searchJobs(
  jobs: JobsPort,
  query: Partial<JobSearchQuery>,
): Promise<Result<PagedJobs, CoreError>> {
  return jobs.search({ ...query, limit: query.limit ?? DEFAULT_PAGE_SIZE });
}
