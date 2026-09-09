'use client';

import { useCallback, useState, useTransition } from 'react';
import { useShallow } from 'zustand/react/shallow';
import type { JobStatus } from '@/core/domain/job/job-status.type';
import { useJobsUiStore } from '@/presentation/stores/jobs-ui.store';
import { jobsEventBus } from '@/shared/events/jobs-event-bus';
import { cancelJobAction } from '../actions/cancel-job.action';

const REASON_REQUIRED = 'A cancellation reason is required';

/**
 * Cancelling collects one value, so design A1 gives it an inline field rather
 * than a modal — and the slice owns which row's field is open.
 */
export function useCancelJob() {
  const { beginOptimistic, commitOptimistic, rollbackOptimistic } = useJobsUiStore(
    useShallow((state) => ({
      beginOptimistic: state.beginOptimistic,
      commitOptimistic: state.commitOptimistic,
      rollbackOptimistic: state.rollbackOptimistic,
    })),
  );

  const [openFor, setOpenFor] = useState<string | null>(null);
  const [reason, setReason] = useState('');
  const [errors, setErrors] = useState<Readonly<Record<string, string>>>({});
  const [isPending, startTransition] = useTransition();

  const open = useCallback((id: string) => {
    setOpenFor(id);
    setReason('');
  }, []);

  const close = useCallback(() => {
    setOpenFor(null);
    setReason('');
  }, []);

  const submit = useCallback(
    (current: JobStatus) => {
      const id = openFor;
      if (id === null) return;

      // BR-5 is checked here as well as in the adapter: once where the user
      // can be told, and once where it cannot be bypassed. This one runs
      // before the action is called at all.
      if (reason.trim() === '') {
        setErrors((previous) => ({ ...previous, [id]: REASON_REQUIRED }));
        return;
      }

      setErrors((previous) =>
        Object.fromEntries(Object.entries(previous).filter(([key]) => key !== id)),
      );
      beginOptimistic(id, 'Cancelled', current);

      startTransition(async () => {
        const outcome = await cancelJobAction(id, reason);

        if (outcome.ok) {
          commitOptimistic(id);
          jobsEventBus.emit('jobs:invalidate', undefined);
          setOpenFor(null);
          setReason('');
          return;
        }

        rollbackOptimistic(id);
        setErrors((previous) => ({ ...previous, [id]: outcome.error.message }));
        // The field stays open: closing it would discard the typed reason
        // along with the error that asked the user to reconsider it.
      });
    },
    [openFor, reason, beginOptimistic, commitOptimistic, rollbackOptimistic],
  );

  const errorFor = useCallback((id: string): string | undefined => errors[id], [errors]);

  return { openFor, open, close, reason, setReason, submit, isPending, errorFor };
}
