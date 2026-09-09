import type { CoreError, Result } from '@/core/domain/result.type';
import type { JobsPort } from '../ports/jobs.port';

export function cancelJob(
  jobs: JobsPort,
  id: string,
  reason: string,
): Promise<Result<void, CoreError>> {
  return jobs.cancel(id, reason);
}
