import type { ReactNode } from 'react';
import { getJobSummary } from '@/core/domain/job';
import type { JobState } from '@/core/domain/job';
import type { VisibleJob } from '@/presentation/stores/jobs-ui.store';
import { Checkbox } from '../atoms/checkbox.component';
import { StatusBadge } from '../atoms/status-badge.component';

/**
 * A summary carries less than a JobState does, so the timestamps the label
 * does not read are filled from scheduledDate. The label is exact for
 * Scheduled — which is what the test asserts — and approximate elsewhere.
 * Plan 2B reconsiders whether a summary-shaped variant in core/domain is
 * worth it once the mutation slices show which states matter here.
 */
function toState(job: VisibleJob): JobState {
  const at = new Date(job.scheduledDate);

  switch (job.status) {
    case 'Scheduled':
      return { status: 'Scheduled', scheduledDate: at, assigneeId: job.assigneeId };
    case 'InProgress':
      return {
        status: 'InProgress',
        startedAt: at,
        assigneeId: job.assigneeId,
        photos: [],
      };
    case 'Completed':
      return {
        status: 'Completed',
        startedAt: at,
        completedAt: at,
        assigneeId: job.assigneeId,
        photos: [],
        signatureUrl: '',
      };
    case 'Cancelled':
      return { status: 'Cancelled', cancelledAt: at, reason: '' };
    case 'Draft':
      return { status: 'Draft' };
  }
}

/**
 * A shared molecule, so it sits below the slices. The actions it renders
 * belong to slices *above* it, and importing one would close the loop
 * components/ -> views/jobs/features/ -> components/. They arrive as a node
 * instead (design A3, architecture 9.4).
 */
export function JobRow({
  job,
  onToggleSelect,
  actions,
}: {
  readonly job: VisibleJob;
  readonly onToggleSelect: (id: string) => void;
  readonly actions: ReactNode;
}) {
  return (
    <tr
      data-testid={`job-row-${job.id}`}
      aria-label={getJobSummary(toState(job))}
      aria-busy={job.isPending}
      className={job.isPending ? 'opacity-60' : undefined}
    >
      <td className="px-3 py-2">
        <Checkbox
          testId={`job-row-${job.id}-select`}
          checked={job.isSelected}
          onChange={() => onToggleSelect(job.id)}
          label={`Select ${job.title}`}
        />
      </td>
      <td data-testid={`job-row-${job.id}-title`} className="px-3 py-2 text-sm">
        {job.title}
      </td>
      <td className="px-3 py-2 text-sm text-slate-600">
        {job.address.street}, {job.address.city}
      </td>
      <td className="px-3 py-2 text-sm">{job.scheduledDate}</td>
      <td className="px-3 py-2 text-sm">{job.assigneeName}</td>
      <td className="px-3 py-2">
        <StatusBadge testId={`job-row-${job.id}-status`} status={job.status} />
        {actions}
      </td>
    </tr>
  );
}
