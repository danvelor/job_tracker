import type { CoreError, Result } from '@/core/domain/result.type';
import type { Party } from '@/core/domain/job/job-summary.type';
import type { JobsPort } from '../ports/jobs.port';

export function listAssignees(
  jobs: JobsPort,
): Promise<Result<readonly Party[], CoreError>> {
  return jobs.assignees();
}

export function listCustomers(
  jobs: JobsPort,
): Promise<Result<readonly Party[], CoreError>> {
  return jobs.customers();
}
