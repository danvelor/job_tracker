import type { JobStatus } from '@/core/domain/job/job-status.type';

const TONE: Record<JobStatus, string> = {
  Draft: 'bg-slate-100 text-slate-700',
  Scheduled: 'bg-blue-100 text-blue-800',
  InProgress: 'bg-amber-100 text-amber-900',
  Completed: 'bg-green-100 text-green-800',
  Cancelled: 'bg-red-100 text-red-800',
};

export function StatusBadge({
  testId,
  status,
}: {
  readonly testId: string;
  readonly status: JobStatus;
}) {
  return (
    <span
      data-testid={testId}
      className={`inline-block rounded px-2 py-0.5 text-xs font-medium ${TONE[status]}`}
    >
      {status}
    </span>
  );
}
