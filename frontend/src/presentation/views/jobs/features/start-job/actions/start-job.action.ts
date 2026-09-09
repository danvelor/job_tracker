'use server';

import { startJob } from '@/core/application/use-cases/start-job.use-case';
import { getContainer } from '@/core/di/container';
import { isOk } from '@/core/domain/result';
import type { ActionOutcome } from '../../action-outcome.type';

/**
 * A mutation, which is the only thing a Server Action is for (assessment
 * line 138). Reads go through the Route Handler of D-29.
 */
export async function startJobAction(id: string): Promise<ActionOutcome> {
  const result = await startJob(getContainer().jobs, id);
  return isOk(result) ? { ok: true } : { ok: false, error: result.error };
}
