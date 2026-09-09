import { JobsTableSkeleton } from '@/presentation/views/jobs';

/**
 * The route-level skeleton. It covers the navigation transition, which is a
 * different event from the list resolving — that one is the Suspense fallback
 * inside the page (architecture 5.3).
 *
 * Its own testid, because they are different events and design A8 assigns
 * `jobs-table-skeleton` to the Suspense fallback. Sharing one made both match
 * a single locator during the streaming handoff, which a fast in-memory adapter
 * hid and the Compose stack surfaced within a second.
 */
export default function JobsLoading() {
  return (
    <section data-testid="jobs-route-skeleton" className="p-6">
      <div className="mb-4 h-6 w-24 animate-pulse rounded bg-slate-200" />
      <div className="mb-3 h-16 w-full animate-pulse rounded bg-slate-100" />
      <JobsTableSkeleton />
    </section>
  );
}
