'use client';

import { use } from 'react';
import type { PagedJobs } from '@/core/application/ports/jobs.port';
import type { Party } from '@/core/domain/job/job-summary.type';
import { Button } from '@/presentation/components/atoms/button.component';
import { SelectionSummary } from '@/presentation/components/molecules/selection-summary.component';
import { CompleteJobModal } from '../../features/complete-job';
import { CreateJobModal } from '../../features/create-job';
import { JobFilterBar } from '../../features/filter-jobs';
import { useJobsPage } from '../../hooks/use-jobs-page.hook';
import { JobsLoadMore } from '../molecules/jobs-load-more.component';
import { JobRowActions } from './job-row-actions.component';
import { JobsErrorBoundary } from './jobs-error-boundary.component';
import { JobsTable } from './jobs-table.component';

export function JobsClient({
  jobsPromise,
  assignees,
  customers,
}: {
  readonly jobsPromise: Promise<PagedJobs>;
  readonly assignees: readonly Party[];
  readonly customers: readonly Party[];
}) {
  const initial = use(jobsPromise);
  const page = useJobsPage(initial, assignees, customers);

  return (
    <section data-testid="jobs-page" className="p-6">
      <div className="mb-4 flex items-center justify-between">
        <h1 className="text-xl font-semibold">Jobs</h1>
        <Button testId="jobs-new-button" onClick={page.create.open}>
          + New job
        </Button>
      </div>

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
          renderActions={(job) => (
            <JobRowActions
              job={job}
              onStart={(row) => page.start.run(row.id, row.status)}
              onComplete={(row) => page.complete.open(row.id)}
              cancel={page.cancel}
              startError={page.start.errorFor(job.id)}
            />
          )}
        />
      </JobsErrorBoundary>

      {page.hasMore ? (
        <JobsLoadMore isLoading={page.isLoading} onLoadMore={page.loadMore} />
      ) : null}

      {page.create.isOpen ? (
        <CreateJobModal
          form={page.create.form}
          assignees={page.assignees}
          customers={page.customers}
          isPending={page.create.isPending}
          onChange={page.create.change}
          onBlur={page.create.blur}
          onSubmit={page.create.submit}
          onCancel={page.create.close}
        />
      ) : null}

      {page.complete.openFor === null ? null : (
        <CompleteJobModal
          signature={page.complete.signature}
          photos={page.complete.photos}
          error={page.complete.error}
          isPending={page.complete.isPending}
          onSignatureChange={page.complete.setSignature}
          onAddPhoto={() =>
            page.complete.addPhoto({
              url: `site-photo-${page.complete.photos.length + 1}.jpg`,
              caption: null,
            })
          }
          onRemovePhoto={page.complete.removePhoto}
          onSubmit={() => page.complete.submit('InProgress')}
          onCancel={page.complete.close}
        />
      )}
    </section>
  );
}
