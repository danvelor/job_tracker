'use client';

import { allowedActionsFor } from '@/core/domain/job';
import { Button } from '@/presentation/components/atoms/button.component';
import { FieldError } from '@/presentation/components/atoms/field-error.component';
import type { VisibleJob } from '@/presentation/stores/jobs-ui.store';
import { CancelReasonField } from '../../features/cancel-job';

export type CancelApi = {
  readonly openFor: string | null;
  readonly reason: string;
  readonly isPending: boolean;
  readonly open: (id: string) => void;
  readonly close: () => void;
  readonly setReason: (value: string) => void;
  readonly submit: (current: VisibleJob['status']) => void;
  readonly errorFor: (id: string) => string | undefined;
};

/**
 * The one place the slices meet for a row, and it lives in the view rather
 * than in a slice — which is what lets four mutually unaware slices produce
 * one row's worth of buttons without any of them importing another
 * (architecture 5.6 rule 2).
 *
 * Which buttons exist is asked of the state machine, not hard-coded: a
 * Completed row offers nothing because the model says so (design A2), and the
 * walkthrough asserts that absence.
 */
export function JobRowActions({
  job,
  onStart,
  onComplete,
  cancel,
  startError,
}: {
  readonly job: VisibleJob;
  readonly onStart: (job: VisibleJob) => void;
  readonly onComplete: (job: VisibleJob) => void;
  readonly cancel: CancelApi;
  readonly startError?: string;
}) {
  const allowed = allowedActionsFor(job.status);
  const cancelError = cancel.errorFor(job.id);

  return (
    <span className="mt-1 flex flex-col items-start gap-1">
      {allowed.includes('START') ? (
        <Button
          testId={`job-row-${job.id}-start`}
          variant="secondary"
          disabled={job.isPending}
          onClick={() => onStart(job)}
        >
          Start
        </Button>
      ) : null}

      {allowed.includes('COMPLETE') ? (
        <Button
          testId={`job-row-${job.id}-complete`}
          variant="secondary"
          disabled={job.isPending}
          onClick={() => onComplete(job)}
        >
          Complete
        </Button>
      ) : null}

      {allowed.includes('CANCEL') ? (
        <Button
          testId={`job-row-${job.id}-cancel`}
          variant="danger"
          disabled={job.isPending}
          onClick={() => cancel.open(job.id)}
        >
          Cancel
        </Button>
      ) : null}

      {cancel.openFor === job.id ? (
        <CancelReasonField
          jobId={job.id}
          value={cancel.reason}
          onChange={cancel.setReason}
          onSubmit={() => cancel.submit(job.status)}
          onDismiss={cancel.close}
          error={cancelError}
          pending={cancel.isPending}
        />
      ) : null}

      {startError === undefined ? null : (
        <FieldError testId={`job-row-${job.id}-error`} message={startError} />
      )}
    </span>
  );
}
