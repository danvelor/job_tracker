import { getJobSummary } from '@/core/domain/job';
import type { JobState } from '@/core/domain/job';
import type { JobDetail } from '@/core/domain/job/job-summary.type';
import { StatusBadge } from '@/presentation/components/atoms/status-badge.component';

const stamped = (value: string | null): Date => new Date(value ?? 0);

function toState(job: JobDetail): JobState {
  switch (job.status) {
    case 'Draft':
      return { status: 'Draft' };

    case 'Scheduled':
      return job.scheduledDate === null
        ? { status: 'Draft', notes: 'no scheduled date recorded' }
        : {
            status: 'Scheduled',
            scheduledDate: new Date(job.scheduledDate),
            assigneeId: job.assigneeId,
          };

    case 'InProgress':
      return {
        status: 'InProgress',
        startedAt: stamped(job.startedAt),
        assigneeId: job.assigneeId,
        photos: job.photos.map((photo) => photo.url),
      };

    case 'Completed':
      return {
        status: 'Completed',
        startedAt: stamped(job.startedAt),
        completedAt: stamped(job.completedAt),
        assigneeId: job.assigneeId,
        photos: job.photos.map((photo) => photo.url),
        signatureUrl: job.signatureUrl ?? '',
      };

    case 'Cancelled':
      return {
        status: 'Cancelled',
        cancelledAt: stamped(job.cancelledAt),
        reason: job.cancellationReason ?? '',
      };
  }
}

export function JobDetailView({ job }: { readonly job: JobDetail }) {
  return (
    <article data-testid="job-detail" className="p-6">
      <h1 data-testid="job-detail-title" className="mb-1 text-xl font-semibold">
        {job.title}
      </h1>
      <StatusBadge testId="job-detail-status" status={job.status} />

      <p data-testid="job-detail-summary" className="mt-2 text-sm text-slate-700">
        {getJobSummary(toState(job))}
      </p>

      <p className="mt-3 text-sm text-slate-600">{job.description ?? 'No description'}</p>

      <p data-testid="job-detail-address" className="mt-3 text-sm">
        {job.address.street}, {job.address.city}, {job.address.state} {job.address.zipCode}
      </p>

      <h2 className="mt-6 mb-2 text-sm font-medium">Photos</h2>
      {job.photos.length === 0 ? (
        <p data-testid="job-detail-no-photos" className="text-sm text-slate-600">
          No photos were captured for this job.
        </p>
      ) : (
        <ul className="space-y-1">
          {job.photos.map((photo) => (
            <li key={photo.id} data-testid={`job-detail-photo-${photo.id}`} className="text-sm">
              {photo.caption ?? photo.url}
            </li>
          ))}
        </ul>
      )}
    </article>
  );
}
