'use server';

import type { CompleteJobInput } from '@/core/application/ports/jobs.port';
import { completeJob } from '@/core/application/use-cases/complete-job.use-case';
import { getContainer } from '@/core/di/container';
import { isOk } from '@/core/domain/result';
import type { ActionOutcome } from '../../action-outcome.type';

export async function completeJobAction(
  id: string,
  input: CompleteJobInput,
): Promise<ActionOutcome> {
  const result = await completeJob(getContainer().jobs, id, input);
  return isOk(result) ? { ok: true } : { ok: false, error: result.error };
}
