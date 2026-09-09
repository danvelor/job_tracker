import type { JobsPort } from '@/core/application/ports/jobs.port';
import { createInMemoryJobsAdapter } from '@/infrastructure/adapters/in-memory-jobs.adapter';

export type Container = { readonly jobs: JobsPort };

declare global {
  /**
   * The container lives on globalThis, not in a module-level binding.
   *
   * Next bundles page code, Route Handlers and Server Actions into separate
   * server chunks, and a module-level `let` is per-chunk rather than
   * per-process. A job created by the create-job Server Action was invisible
   * to both `GET /api/jobs` and the page: three entry points, three
   * containers, three copies of the seeded array. globalThis is the one scope
   * they share.
   *
   * This only matters because the in-memory adapter holds state. Once
   * HttpJobsAdapter arrives in plan 3 the state lives in PostgreSQL and the
   * container becomes stateless — but the seam would still be here, so the
   * pattern stays.
   */
  var __jobTrackerContainer: Container | undefined;
}

/**
 * The in-memory adapter is the only implementation for now, and D-01 makes it
 * the default in development and CI regardless — which is what lets the
 * end-to-end suite run with neither backend nor database. `HttpJobsAdapter`
 * arrives in plan 3, and selecting between them from configuration is the only
 * change this function needs then.
 */
function build(): Container {
  return { jobs: createInMemoryJobsAdapter() };
}

export function getContainer(): Container {
  globalThis.__jobTrackerContainer ??= build();
  return globalThis.__jobTrackerContainer;
}

/**
 * Exists for the tests, and that is a legitimate reason: the in-memory adapter
 * holds state, so without a reset a job created in one test is visible in the
 * next and the suite passes or fails on execution order.
 */
export function resetContainer(): void {
  globalThis.__jobTrackerContainer = undefined;
}
