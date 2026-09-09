'use server';

import { cancelJob } from '@/core/application/use-cases/cancel-job.use-case';
import { getContainer } from '@/core/di/container';
import { isOk } from '@/core/domain/result';
import type { ActionOutcome } from '../../action-outcome.type';

export async function cancelJobAction(id: string, reason: string): Promise<ActionOutcome> {
  const result = await cancelJob(getContainer().jobs, id, reason);
  return isOk(result) ? { ok: true } : { ok: false, error: result.error };
}
