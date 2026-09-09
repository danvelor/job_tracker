import { JobsTableSkeleton } from '@/presentation/views/jobs';

/**
 * The route-level skeleton. It covers the navigation transition, which is a
 * different event from the list resolving — that one is the Suspense fallback
 * inside the page (architecture 5.3).
 */
export default function JobsLoading() {
  return (
    <section className="p-6">
      <div className="mb-4 h-6 w-24 animate-pulse rounded bg-slate-200" />
      <div className="mb-3 h-16 w-full animate-pulse rounded bg-slate-100" />
      <JobsTableSkeleton />
    </section>
  );
}
