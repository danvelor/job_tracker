import { render, screen } from '@testing-library/react';
import type { JobDetail } from '@/core/domain/job/job-summary.type';
import { JobDetailView } from '../job-detail.component';

const detail: JobDetail = {
  id: 'job-3',
  title: 'Shingle swap',
  status: 'Completed',
  scheduledDate: '2099-03-09',
  assigneeId: 'assignee-1',
  assigneeName: 'J. Ortiz',
  description: 'Swap storm-damaged shingles',
  address: {
    street: '44 Pine Rd',
    city: 'Springfield',
    state: 'IL',
    zipCode: '62701',
    latitude: 39.78,
    longitude: -89.65,
  },
  startedAt: '2099-03-09T08:00:00.000Z',
  completedAt: '2099-03-09T15:30:00.000Z',
  signatureUrl: 'data:image/png;base64,seed',
  photos: [
    {
      id: 'photo-1',
      url: 'https://example.invalid/photo-1.jpg',
      capturedAt: '2099-03-09T14:00:00.000Z',
      caption: 'After',
    },
  ],
  cancelledAt: null,
  cancellationReason: null,
  photoCount: 1,
};

describe('JobDetailView', () => {
  it('renders the job title and status', () => {
    render(<JobDetailView job={detail} />);

    expect(screen.getByTestId('job-detail-title')).toHaveTextContent('Shingle swap');
    expect(screen.getByTestId('job-detail-status')).toHaveTextContent('Completed');
  });

  it('renders the full address the summary does not carry', () => {
    render(<JobDetailView job={detail} />);

    expect(screen.getByTestId('job-detail-address')).toHaveTextContent('62701');
  });

  it('lists the photos with their captions', () => {
    render(<JobDetailView job={detail} />);

    expect(screen.getByTestId('job-detail-photo-photo-1')).toHaveTextContent('After');
  });

  it('falls back to the url when a photo has no caption', () => {
    render(
      <JobDetailView
        job={{ ...detail, photos: [{ ...detail.photos[0], caption: null }] }}
      />,
    );

    expect(screen.getByTestId('job-detail-photo-photo-1')).toHaveTextContent(
      'https://example.invalid/photo-1.jpg',
    );
  });

  it('says so when there are no photos rather than rendering an empty list', () => {
    render(<JobDetailView job={{ ...detail, photos: [], photoCount: 0 }} />);

    expect(screen.getByTestId('job-detail-no-photos')).toBeInTheDocument();
  });

  it('summarises the job through the domain state machine', () => {
    render(<JobDetailView job={detail} />);

    // /jobs/[id] holds startedAt, completedAt, the signature and the photos,
    // so it can build a faithful JobState — which is what makes it the honest
    // home for getJobSummary. A list row cannot: a summary carries none of
    // those.
    expect(screen.getByTestId('job-detail-summary')).toHaveTextContent(
      'Completed on 2099-03-09, signed',
    );
  });

  it('summarises a cancelled job with its reason', () => {
    render(
      <JobDetailView
        job={{
          ...detail,
          status: 'Cancelled',
          completedAt: null,
          signatureUrl: null,
          cancelledAt: '2099-03-08T12:00:00.000Z',
          cancellationReason: 'Customer withdrew',
        }}
      />,
    );

    expect(screen.getByTestId('job-detail-summary')).toHaveTextContent(
      'Cancelled on 2099-03-08: Customer withdrew',
    );
  });

  it('says so when there is no description', () => {
    render(<JobDetailView job={{ ...detail, description: null }} />);

    expect(screen.getByTestId('job-detail')).toHaveTextContent('No description');
  });
});
