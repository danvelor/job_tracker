import type { ReactNode } from 'react';
import type { VisibleJob } from '@/presentation/stores/jobs-ui.store';
import { Checkbox } from '../atoms/checkbox.component';
import { StatusBadge } from '../atoms/status-badge.component';

/**
 * A shared molecule, so it sits below the slices. The actions it renders
 * belong to slices *above* it, and importing one would close the loop
 * components/ -> views/jobs/features/ -> components/. They arrive as a node
 * instead (design A3, architecture 9.4).
 *
 * The accessible name is built from what a JobSummary holds. It used to
 * reconstruct a JobState so it could call getJobSummary, which meant inventing
 * a startedAt and a signatureUrl the summary does not carry — a label that was
 * right for Scheduled by accident. getJobSummary lives on /jobs/[id] now,
 * where the timestamps are real.
 */
/**
 * An em dash rather than an empty cell: a blank reads as a rendering fault,
 * and the accessible name would end in a bare comma.
 */
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
