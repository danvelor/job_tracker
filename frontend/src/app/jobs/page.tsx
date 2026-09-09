import 'server-only';
import { Suspense } from 'react';
import type { PagedJobs } from '@/core/application/ports/jobs.port';
import {
  listAssignees,
  listCustomers,
} from '@/core/application/use-cases/list-parties.use-case';
import { searchJobs } from '@/core/application/use-cases/search-jobs.use-case';
import { getContainer } from '@/core/di/container';
import type { Party } from '@/core/domain/job/job-summary.type';
import { isOk } from '@/core/domain/result';
import { JobsClient, JobsTableSkeleton } from '@/presentation/views/jobs';

export const dynamic = 'force-dynamic';

async function resolvePage(): Promise<PagedJobs> {
  const result = await searchJobs(getContainer().jobs, {});
  if (!isOk(result)) throw new Error(result.error.message);
  return result.value;
}

async function resolveAssignees(): Promise<readonly Party[]> {
  const result = await listAssignees(getContainer().jobs);
  return isOk(result) ? result.value : [];
}

async function resolveCustomers(): Promise<readonly Party[]> {
  const result = await listCustomers(getContainer().jobs);
  return isOk(result) ? result.value : [];
}

export default async function JobsPage() {
  // The rosters ARE awaited: they are short lists the filter bar and the
  // create form need before they can render at all.
  //
  // The job page is NOT awaited. The unresolved promise crosses into the
  // boundary so <Suspense> actually suspends and the skeleton is real (D-11).
  // Awaiting here would resolve the data before React renders and the fallback
  // would never appear, which is the trap architecture 5.3 describes.
  const [assignees, customers] = await Promise.all([
    resolveAssignees(),
    resolveCustomers(),
  ]);
  const jobsPromise = resolvePage();

  return (
    <Suspense fallback={<JobsTableSkeleton />}>
      <JobsClient
        jobsPromise={jobsPromise}
        assignees={assignees}
        customers={customers}
      />
    </Suspense>
  );
}
