'use client';

import type { NewPhoto } from '@/core/application/ports/jobs.port';
import { Button } from '@/presentation/components/atoms/button.component';
import { SignaturePad } from '@/presentation/components/molecules/signature-pad.component';

export function CompleteJobModal({
  signature,
  photos,
  error,
  isPending,
  onSignatureChange,
  onAddPhoto,
  onRemovePhoto,
  onSubmit,
  onCancel,
}: {
  readonly signature: string;
  readonly photos: readonly NewPhoto[];
  readonly error: string | null;
  readonly isPending: boolean;
  readonly onSignatureChange: (value: string) => void;
  readonly onAddPhoto: () => void;
  readonly onRemovePhoto: (index: number) => void;
  readonly onSubmit: () => void;
  readonly onCancel: () => void;
}) {
  return (
    <div
      data-testid="complete-job-modal"
      role="dialog"
      aria-modal="true"
      aria-label="Complete job"
      className="fixed inset-0 z-10 flex items-start justify-center bg-black/30 p-8"
    >
      <div className="w-full max-w-md rounded bg-white p-6">
        <h2 className="mb-4 text-lg font-semibold">Complete job</h2>

        <SignaturePad
          testId="complete-job-signature"
          value={signature}
          onChange={onSignatureChange}
        />

        <div className="mt-4">
          <Button testId="complete-job-photos" variant="secondary" onClick={onAddPhoto}>
            Attach site photo
          </Button>
          <ul className="mt-1 space-y-1">
            {photos.map((photo, index) => (
              <li
                key={photo.url}
                data-testid={`complete-job-photo-${index}`}
                className="flex items-center gap-2 text-xs text-slate-600"
              >
                {photo.url}
                <Button
                  testId={`complete-job-photo-${index}-remove`}
                  variant="ghost"
                  onClick={() => onRemovePhoto(index)}
                >
                  Remove
                </Button>
              </li>
            ))}
          </ul>
        </div>

        {error === null ? null : (
          <p data-testid="complete-job-error" role="alert" className="mt-3 text-sm text-red-700">
            {error}
          </p>
        )}

        <div className="mt-4 flex gap-2">
          <Button testId="complete-job-submit" onClick={onSubmit} pending={isPending}>
            Complete
          </Button>
          <Button testId="complete-job-cancel" variant="secondary" onClick={onCancel}>
            Cancel
          </Button>
        </div>
      </div>
    </div>
  );
}
