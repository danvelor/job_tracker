'use client';

import { Button } from '@/presentation/components/atoms/button.component';
import { TextInput } from '@/presentation/components/atoms/text-input.component';

export function CancelReasonField({
  jobId,
  value,
  onChange,
  onSubmit,
  onDismiss,
  error,
  pending,
}: {
  readonly jobId: string;
  readonly value: string;
  readonly onChange: (value: string) => void;
  readonly onSubmit: () => void;
  readonly onDismiss: () => void;
  readonly error?: string;
  readonly pending: boolean;
}) {
  return (
    <span className="mt-1 flex items-center gap-1">
      <TextInput
        testId={`job-row-${jobId}-cancel-reason`}
        id={`job-row-${jobId}-cancel-reason`}
        value={value}
        onChange={onChange}
        placeholder="Reason"
        error={error}
      />
      <Button testId={`job-row-${jobId}-cancel-confirm`} onClick={onSubmit} pending={pending}>
        Confirm
      </Button>
      <Button testId={`job-row-${jobId}-cancel-dismiss`} variant="ghost" onClick={onDismiss}>
        Keep
      </Button>
    </span>
  );
}
