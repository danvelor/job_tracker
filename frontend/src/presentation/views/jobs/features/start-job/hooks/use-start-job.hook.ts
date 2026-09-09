'use client';

import { useCallback, useState, useTransition } from 'react';
import { useShallow } from 'zustand/react/shallow';
import type { JobStatus } from '@/core/domain/job/job-status.type';
import { useJobsUiStore } from '@/presentation/stores/jobs-ui.store';
import { jobsEventBus } from '@/shared/events/jobs-event-bus';
import { startJobAction } from '../actions/start-job.action';

/**
 * The optimistic path, in its smallest complete form: write the intended
 * status into the overlay, run the Server Action, then commit and ask the view
 * to revalidate, or roll back and put the message on the row.
 */
export function useStartJob() {
  const { beginOptimistic, commitOptimistic, rollbackOptimistic } = useJobsUiStore(
    useShallow((state) => ({
      beginOptimistic: state.beginOptimistic,
      commitOptimistic: state.commitOptimistic,
      rollbackOptimistic: state.rollbackOptimistic,
    })),
  );

  // Keyed by job id so two rows fail independently, which is what design A5
  // means by the error appearing on its own row.
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

      // Written before the await: that ordering is what makes the change
      // optimistic rather than merely fast.
      beginOptimistic(id, 'InProgress', current);

      startTransition(async () => {
        const outcome = await startJobAction(id);

        if (outcome.ok) {
          commitOptimistic(id);
          // The view revalidates. The slice does not know the view exists,
          // which is rule 2 of architecture 5.6 holding in practice.
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
