import type { JobsPort } from '@/core/application/ports/jobs.port';
import { createInMemoryJobsAdapter } from '@/infrastructure/adapters/in-memory-jobs.adapter';

export type Container = { readonly jobs: JobsPort };

let instance: Container | null = null;

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
  instance ??= build();
  return instance;
}

/**
 * Exists for the tests, and that is a legitimate reason: the in-memory adapter
 * holds state, so without a reset a job created in one test is visible in the
 * next and the suite passes or fails on execution order.
 */
export function resetContainer(): void {
  instance = null;
}
