'use server';

import { startJob } from '@/core/application/use-cases/start-job.use-case';
import { getContainer } from '@/core/di/container';
import { isOk } from '@/core/domain/result';
import type { ActionOutcome } from '../../action-outcome.type';

export async function startJobAction(id: string): Promise<ActionOutcome> {
  const result = await startJob(getContainer().jobs, id);
  return isOk(result) ? { ok: true } : { ok: false, error: result.error };
}
