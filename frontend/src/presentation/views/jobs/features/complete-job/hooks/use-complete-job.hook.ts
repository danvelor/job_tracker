'use client';

import { useCallback, useState, useTransition } from 'react';
import { useShallow } from 'zustand/react/shallow';
import type { NewPhoto } from '@/core/application/ports/jobs.port';
import type { JobStatus } from '@/core/domain/job/job-status.type';
import { useJobsUiStore } from '@/presentation/stores/jobs-ui.store';
import { jobsEventBus } from '@/shared/events/jobs-event-bus';
import { completeJobAction } from '../actions/complete-job.action';

const SIGNATURE_REQUIRED = 'A customer signature is required';

export function useCompleteJob() {
  const { beginOptimistic, commitOptimistic, rollbackOptimistic } = useJobsUiStore(
    useShallow((state) => ({
      beginOptimistic: state.beginOptimistic,
      commitOptimistic: state.commitOptimistic,
      rollbackOptimistic: state.rollbackOptimistic,
    })),
  );

  const [openFor, setOpenFor] = useState<string | null>(null);
  const [signature, setSignature] = useState('');
  const [photos, setPhotos] = useState<readonly NewPhoto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  const open = useCallback((id: string) => {
    setOpenFor(id);
    setSignature('');
    setPhotos([]);
    setError(null);
  }, []);

  const close = useCallback(() => {
    setOpenFor(null);
    setSignature('');
    setPhotos([]);
    setError(null);
  }, []);

  const addPhoto = useCallback(
    (photo: NewPhoto) => setPhotos((current) => [...current, photo]),
    [],
  );

  const removePhoto = useCallback(
    (index: number) => setPhotos((current) => current.filter((_, at) => at !== index)),
    [],
  );

  const submit = useCallback(
    (current: JobStatus) => {
      const id = openFor;
      if (id === null) return;

      if (signature.trim() === '') {
        setError(SIGNATURE_REQUIRED);
        return;
      }

      setError(null);
      beginOptimistic(id, 'Completed', current);

      startTransition(async () => {
        const outcome = await completeJobAction(id, { signatureUrl: signature, photos });

        if (outcome.ok) {
          commitOptimistic(id);
          jobsEventBus.emit('jobs:invalidate', undefined);
          setOpenFor(null);
          setSignature('');
          setPhotos([]);
          return;
        }

        rollbackOptimistic(id);
        setError(outcome.error.message);
      });
    },
    [openFor, signature, photos, beginOptimistic, commitOptimistic, rollbackOptimistic],
  );

  return {
    openFor,
    open,
    close,
    signature,
    setSignature,
    photos,
    addPhoto,
    removePhoto,
    submit,
    isPending,
    error,
  };
}
