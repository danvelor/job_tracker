import type { ReactNode } from 'react';
import { JobRow } from '@/presentation/components/molecules/job-row.component';
import type { VisibleJob } from '@/presentation/stores/jobs-ui.store';

const HEADINGS = ['', 'TITLE', 'ADDRESS', 'SCHEDULED', 'CREW', 'STATUS'];

/**
 * A thin shell: props in, markup out. It declares no state and no handler
 * body, and the actions each row offers arrive through a render prop rather
 * than through an import, which is what keeps the module graph acyclic
 * (architecture 9.4).
 */
export function JobsTable({
  jobs,
  hasActiveFilter,
  onToggleSelect,
  renderActions,
}: {
  readonly jobs: readonly VisibleJob[];
  readonly hasActiveFilter: boolean;
  readonly onToggleSelect: (id: string) => void;
  readonly renderActions: (job: VisibleJob) => ReactNode;
}) {
  if (jobs.length === 0) {
    return hasActiveFilter ? (
      <div data-testid="jobs-empty-no-matches" className="p-6 text-sm text-slate-600">
        No jobs match these filters.
      </div>
    ) : (
      <div data-testid="jobs-empty-no-jobs" className="p-6 text-sm text-slate-600">
        No jobs yet.
      </div>
    );
  }

  return (
    <table data-testid="jobs-table" className="w-full border-collapse text-left">
      <thead>
        <tr className="border-b border-slate-200">
          {HEADINGS.map((heading, index) => (
            <th
              key={heading === '' ? `col-${index}` : heading}
              scope="col"
              className="px-3 py-2 text-xs font-medium text-slate-500"
            >
              {heading}
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        {jobs.map((job) => (
          <JobRow
            key={job.id}
            job={job}
            onToggleSelect={onToggleSelect}
            actions={renderActions(job)}
          />
        ))}
      </tbody>
    </table>
  );
}
