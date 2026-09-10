import { JobsTableSkeleton } from '@/presentation/views/jobs';

export default function JobsLoading() {
  return (
    <section data-testid="jobs-route-skeleton" className="p-6">
      <div className="mb-4 h-6 w-24 animate-pulse rounded bg-slate-200" />
      <div className="mb-3 h-16 w-full animate-pulse rounded bg-slate-100" />
      <JobsTableSkeleton />
    </section>
  );
}
