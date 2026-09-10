import 'server-only';
import { notFound } from 'next/navigation';
import { getJob } from '@/core/application/use-cases/get-job.use-case';
import { getContainer } from '@/core/di/container';
import { isOk } from '@/core/domain/result';
import { JobDetailView } from '@/presentation/views/job-detail/components/organisms/job-detail.component';

export const dynamic = 'force-dynamic';

export default async function JobDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  const result = await getJob(getContainer().jobs, id);

  if (!isOk(result)) {
    notFound();
  }

  return <JobDetailView job={result.value} />;
}
