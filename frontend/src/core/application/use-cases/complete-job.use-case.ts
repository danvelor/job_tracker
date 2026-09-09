import type { CoreError, Result } from '@/core/domain/result.type';
import type { CompleteJobInput, JobsPort } from '../ports/jobs.port';

export function completeJob(
  jobs: JobsPort,
  id: string,
  input: CompleteJobInput,
): Promise<Result<void, CoreError>> {
  return jobs.complete(id, input);
}
