'use client';

import { useCallback, useState, useTransition } from 'react';
import { useShallow } from 'zustand/react/shallow';
import type { JobStatus } from '@/core/domain/job/job-status.type';
import { useJobsUiStore } from '@/presentation/stores/jobs-ui.store';
import { jobsEventBus } from '@/shared/events/jobs-event-bus';
import { startJobAction } from '../actions/start-job.action';

export function useStartJob() {
  const { beginOptimistic, commitOptimistic, rollbackOptimistic } = useJobsUiStore(
    useShallow((state) => ({
      beginOptimistic: state.beginOptimistic,
      commitOptimistic: state.commitOptimistic,
      rollbackOptimistic: state.rollbackOptimistic,
    })),
  );

  const [errors, setErrors] = useState<Readonly<Record<string, string>>>({});
  const [isPending, startTransition] = useTransition();

  const forget = useCallback(
    (id: string) =>
      setErrors((previous) =>
        Object.fromEntries(Object.entries(previous).filter(([key]) => key !== id)),
      ),
    [],
  );

  const run = useCallback(
    (id: string, current: JobStatus) => {
      forget(id);

      beginOptimistic(id, 'InProgress', current);

      startTransition(async () => {
        const outcome = await startJobAction(id);

        if (outcome.ok) {
          commitOptimistic(id);
          jobsEventBus.emit('jobs:invalidate', undefined);
          return;
        }

        rollbackOptimistic(id);
        setErrors((previous) => ({ ...previous, [id]: outcome.error.message }));
      });
    },
    [forget, beginOptimistic, commitOptimistic, rollbackOptimistic],
  );

  const errorFor = useCallback((id: string): string | undefined => errors[id], [errors]);

  return { run, isPending, errorFor };
}
