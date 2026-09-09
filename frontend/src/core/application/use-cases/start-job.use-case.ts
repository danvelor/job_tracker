import type { CoreError, Result } from '@/core/domain/result.type';
import type { JobsPort } from '../ports/jobs.port';

export function startJob(jobs: JobsPort, id: string): Promise<Result<void, CoreError>> {
  return jobs.start(id);
}
