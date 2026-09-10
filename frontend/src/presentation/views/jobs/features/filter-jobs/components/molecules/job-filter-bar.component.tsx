'use client';

import { createContext, useContext, useMemo } from 'react';
import type { ReactNode } from 'react';
import type { JobStatus } from '@/core/domain/job/job-status.type';
import type { Party } from '@/core/domain/job/job-summary.type';
import { Button } from '@/presentation/components/atoms/button.component';
import { Checkbox } from '@/presentation/components/atoms/checkbox.component';
import { DateInput } from '@/presentation/components/atoms/date-input.component';
import { Select } from '@/presentation/components/atoms/select.component';
import { SearchField } from '@/presentation/components/molecules/search-field.component';
import { useFilterJobs } from '../../hooks/use-filter-jobs.hook';
import type { FilterJobsApi } from '../../hooks/use-filter-jobs.hook';

const STATUSES: readonly JobStatus[] = [
  'Draft',
  'Scheduled',
  'InProgress',
  'Completed',
  'Cancelled',
];

type BarContext = FilterJobsApi & { readonly assignees: readonly Party[] };

const Context = createContext<BarContext | null>(null);

function useBar(child: string): BarContext {
  const value = useContext(Context);
  if (value === null) {
    throw new Error(`JobFilterBar.${child} must be rendered inside JobFilterBar`);
  }
  return value;
}

function JobFilterBarRoot({
  assignees,
  children,
}: {
  readonly assignees: readonly Party[];
  readonly children: ReactNode;
}) {
  const api = useFilterJobs();
  const value = useMemo(() => ({ ...api, assignees }), [api, assignees]);

  return (
    <Context.Provider value={value}>
      <div
        data-testid="jobs-filter-bar"
        role="search"
        className="mb-3 flex flex-wrap items-start gap-3 rounded border border-slate-200 p-3"
      >
        {children}
      </div>
    </Context.Provider>
  );
}

function Search() {
  const { filters, setText } = useBar('Search');

  return (
    <div className="min-w-56 flex-1">
      <SearchField value={filters.text} onChange={setText} />
    </div>
  );
}

function Status() {
  const { filters, toggleStatus } = useBar('Status');

  return (
    <fieldset data-testid="filter-status-select" className="flex flex-wrap gap-2">
      <legend className="sr-only">Status</legend>
      {STATUSES.map((status) => (
        <label key={status} className="flex items-center gap-1 text-xs">
          <Checkbox
            testId={`filter-status-option-${status}`}
            checked={filters.statuses.includes(status)}
            onChange={() => toggleStatus(status)}
            label={status}
          />
          {status}
        </label>
      ))}
    </fieldset>
  );
}

function DateRange() {
  const { filters, setDateRange } = useBar('DateRange');

  return (
    <div className="flex items-center gap-2">
      <DateInput
        testId="filter-date-from"
        value={filters.scheduledFrom ?? ''}
        onChange={(value) => setDateRange(value === '' ? null : value, filters.scheduledTo)}
      />
      <span className="text-xs text-slate-500">to</span>
      <DateInput
        testId="filter-date-to"
        value={filters.scheduledTo ?? ''}
        onChange={(value) => setDateRange(filters.scheduledFrom, value === '' ? null : value)}
      />
    </div>
  );
}

function Assignee() {
  const { filters, setAssignee, assignees } = useBar('Assignee');

  return (
    <Select
      testId="filter-assignee-select"
      value={filters.assigneeId ?? ''}
      placeholder="Any crew"
      options={assignees.map((party) => ({ value: party.id, label: party.name }))}
      onChange={(value) => setAssignee(value === '' ? null : value)}
    />
  );
}

function Clear() {
  const { clear } = useBar('Clear');

  return (
    <Button testId="filter-clear-button" variant="ghost" onClick={clear}>
      Clear
    </Button>
  );
}

export const JobFilterBar = Object.assign(JobFilterBarRoot, {
  Search,
  Status,
  DateRange,
  Assignee,
  Clear,
});
