'use server';

import type { CreateJobInput } from '@/core/application/ports/jobs.port';
import { createJob } from '@/core/application/use-cases/create-job.use-case';
import { getContainer } from '@/core/di/container';
import { isOk } from '@/core/domain/result';
import type { CreateOutcome } from '../../action-outcome.type';

export async function createJobAction(input: CreateJobInput): Promise<CreateOutcome> {
  const result = await createJob(getContainer().jobs, input);
  return isOk(result) ? { ok: true, id: result.value } : { ok: false, error: result.error };
}
