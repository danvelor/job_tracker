import type { JobsPort } from '@/core/application/ports/jobs.port';
import { createHttpJobsAdapter } from '@/infrastructure/adapters/http-jobs.adapter';
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
 * The one place the application chooses an implementation.
 *
 * A configured API url means the real backend; no url means the in-memory
 * adapter, which D-01 keeps as the default in development and CI — that is what
 * lets the end-to-end suite run with neither backend nor database. The switch
 * is a URL being present rather than a flag someone has to remember to set.
 *
 * The variable is read here rather than at module load, so a bundle built in CI
 * does not carry CI's answer into the Compose stack.
 */
function build(): Container {
  const apiUrl = process.env.JOBTRACKER_API_URL;

  return {
    jobs:
      apiUrl === undefined || apiUrl === ''
        ? createInMemoryJobsAdapter()
        : createHttpJobsAdapter({
            baseUrl: apiUrl,
            ...(process.env.JOBTRACKER_ORGANIZATION_ID === undefined
              ? {}
              : { organizationId: process.env.JOBTRACKER_ORGANIZATION_ID }),
          }),
  };
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
