import type { ReactNode } from 'react';
import type { VisibleJob } from '@/presentation/stores/jobs-ui.store';
import { Checkbox } from '../atoms/checkbox.component';
import { StatusBadge } from '../atoms/status-badge.component';

const scheduledDate = (job: VisibleJob): string => job.scheduledDate ?? '—';

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
      aria-label={`${job.title}, ${job.status}, ${scheduledDate(job)}, ${job.assigneeName}`}
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
      <td className="px-3 py-2 text-sm">{scheduledDate(job)}</td>
      <td className="px-3 py-2 text-sm">{job.assigneeName}</td>
      <td className="px-3 py-2">
        <StatusBadge testId={`job-row-${job.id}-status`} status={job.status} />
        {actions}
      </td>
    </tr>
  );
}
