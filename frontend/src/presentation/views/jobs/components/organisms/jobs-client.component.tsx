'use client';

import { use } from 'react';
import type { PagedJobs } from '@/core/application/ports/jobs.port';
import type { Party } from '@/core/domain/job/job-summary.type';
import { SelectionSummary } from '@/presentation/components/molecules/selection-summary.component';
import { JobFilterBar } from '../../features/filter-jobs';
import { useJobsPage } from '../../hooks/use-jobs-page.hook';
import { JobsErrorBoundary } from './jobs-error-boundary.component';
import { JobsTable } from './jobs-table.component';

/**
 * The thin shell of assessment line 117, and the client boundary D-11 names:
 * the page hands it an UNRESOLVED promise and this component unwraps it with
 * use(), which is what makes the <Suspense> above actually suspend.
 *
 * Everything else comes from the orchestrator hook. This component declares no
 * state and no handler body.
 */
export function JobsClient({
  jobsPromise,
  assignees,
}: {
  readonly jobsPromise: Promise<PagedJobs>;
  readonly assignees: readonly Party[];
}) {
  const initial = use(jobsPromise);
  const page = useJobsPage(initial, assignees);

  return (
    <section data-testid="jobs-page" className="p-6">
      <h1 className="mb-4 text-xl font-semibold">Jobs</h1>

      <JobFilterBar assignees={page.assignees}>
        <JobFilterBar.Search />
        <JobFilterBar.Status />
        <JobFilterBar.DateRange />
        <JobFilterBar.Assignee />
        <JobFilterBar.Clear />
      </JobFilterBar>

      <SelectionSummary loaded={page.jobs.length} selected={page.selectedCount} />

      <JobsErrorBoundary>
        <JobsTable
          jobs={page.jobs}
          hasActiveFilter={page.hasActiveFilter}
          onToggleSelect={page.onToggleSelect}
          renderActions={() => null}
        />
      </JobsErrorBoundary>
    </section>
  );
}
