'use client';

import { useRouter } from 'next/navigation';
import { Button } from '@/presentation/components/atoms/button.component';

export default function JobsError({
  error,
  reset,
}: {
  readonly error: Error & { digest?: string };
  readonly reset: () => void;
}) {
  const router = useRouter();

  const retry = (): void => {
    router.refresh();
    reset();
  };

  return (
    <section data-testid="jobs-error" role="alert" className="p-6">
      <h1 className="mb-2 text-lg font-semibold">The job list could not be loaded</h1>
      <p className="mb-4 text-sm text-slate-600">{error.message}</p>
      <Button testId="jobs-error-retry" onClick={retry}>
        Try again
      </Button>
    </section>
  );
}
