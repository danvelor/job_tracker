import 'server-only';
import { notFound } from 'next/navigation';
import { getJob } from '@/core/application/use-cases/get-job.use-case';
import { getContainer } from '@/core/di/container';
import { isOk } from '@/core/domain/result';
import { JobDetailView } from '@/presentation/views/job-detail/components/organisms/job-detail.component';

export const dynamic = 'force-dynamic';

// Next 15: params is a Promise and must be awaited. Verified against 15.5.25
// through next build, which is what validates route signatures — tsc does not.
export default async function JobDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  const result = await getJob(getContainer().jobs, id);

  if (!isOk(result)) {
    // Renders app/jobs/not-found.tsx. Without a route that can genuinely 404,
    // that file is decorative — which is why design A1 put the detail screen
    // in scope at all.
    notFound();
  }

  return <JobDetailView job={result.value} />;
}
