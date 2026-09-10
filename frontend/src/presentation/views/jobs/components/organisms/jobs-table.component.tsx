import type { ReactNode } from 'react';
import type { JobSortField } from '@/core/application/ports/jobs.port';
import { JobRow } from '@/presentation/components/molecules/job-row.component';
import type { VisibleJob } from '@/presentation/stores/jobs-ui.store';

type Column = { readonly label: string; readonly sortBy?: JobSortField };

const COLUMNS: readonly Column[] = [
  { label: '' },
  { label: 'TITLE', sortBy: 'title' },
  { label: 'ADDRESS' },
  { label: 'SCHEDULED', sortBy: 'scheduledDate' },
  { label: 'CREW' },
  { label: 'STATUS' },
];

const ORDER_OF: Record<JobSortField, 'ascending' | 'descending'> = {
  title: 'ascending',
  scheduledDate: 'descending',
};

export function JobsTable({
  jobs,
  hasActiveFilter,
  sortField,
  onSort,
  onToggleSelect,
  renderActions,
}: {
  readonly jobs: readonly VisibleJob[];
  readonly hasActiveFilter: boolean;
  readonly sortField: JobSortField;
  readonly onSort: (field: JobSortField) => void;
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
          {COLUMNS.map(({ label, sortBy }, index) => (
            <th
              key={label === '' ? `col-${index}` : label}
              scope="col"
              aria-sort={
                sortBy === undefined
                  ? undefined
                  : sortBy === sortField
                    ? ORDER_OF[sortBy]
                    : 'none'
              }
              className="px-3 py-2 text-xs font-medium text-slate-500"
            >
              {sortBy === undefined ? (
                label
              ) : (
                <button
                  type="button"
                  data-testid={`jobs-sort-${sortBy}`}
                  onClick={() => onSort(sortBy)}
                  className="flex items-center gap-1 font-medium text-slate-500 hover:text-slate-900"
                >
                  {label}
                  {sortBy === sortField ? (
                    <span aria-hidden="true">
                      {ORDER_OF[sortBy] === 'ascending' ? '↑' : '↓'}
                    </span>
                  ) : null}
                </button>
              )}
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
