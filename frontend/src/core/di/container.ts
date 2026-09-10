import type { JobsPort } from '@/core/application/ports/jobs.port';
import { createHttpJobsAdapter } from '@/infrastructure/adapters/http-jobs.adapter';
import { createInMemoryJobsAdapter } from '@/infrastructure/adapters/in-memory-jobs.adapter';

export type Container = { readonly jobs: JobsPort };

declare global {
  var __jobTrackerContainer: Container | undefined;
}

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

export function resetContainer(): void {
  globalThis.__jobTrackerContainer = undefined;
}
