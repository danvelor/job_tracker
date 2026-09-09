import type { CoreError, Result } from '@/core/domain/result.type';
import type { CreateJobInput, JobsPort } from '../ports/jobs.port';

export function createJob(
  jobs: JobsPort,
  input: CreateJobInput,
): Promise<Result<string, CoreError>> {
  return jobs.create(input);
}
