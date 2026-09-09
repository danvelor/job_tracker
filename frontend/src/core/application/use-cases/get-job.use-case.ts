import type { CoreError, Result } from '@/core/domain/result.type';
import type { JobDetail } from '@/core/domain/job/job-summary.type';
import type { JobsPort } from '../ports/jobs.port';

export function getJob(
  jobs: JobsPort,
  id: string,
): Promise<Result<JobDetail, CoreError>> {
  return jobs.getById(id);
}
