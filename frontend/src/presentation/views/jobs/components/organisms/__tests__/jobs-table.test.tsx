import { render, screen } from '@testing-library/react';
import type { VisibleJob } from '@/presentation/stores/jobs-ui.store';
import { JobsErrorBoundary } from '../jobs-error-boundary.component';
import { JobsTable } from '../jobs-table.component';
import { JobsTableSkeleton } from '../jobs-table-skeleton.component';

const job: VisibleJob = {
  id: 'job-1',
  title: 'Ridge tile replacement',
  status: 'Scheduled',
  scheduledDate: '2099-03-14',
  assigneeId: 'assignee-1',
  assigneeName: 'J. Ortiz',
  address: { street: '12 Elm St', city: 'Springfield', state: 'IL' },
  photoCount: 0,
  isSelected: false,
  isPending: false,
};

const noop = () => undefined;

describe('JobsTable', () => {
  it('renders a row per job', () => {
    render(
      <JobsTable
        jobs={[job]}
        hasActiveFilter={false}
        onToggleSelect={noop}
        renderActions={() => null}
      />,
    );

    expect(screen.getByTestId('jobs-table')).toBeInTheDocument();
    expect(screen.getByTestId('job-row-job-1')).toBeInTheDocument();
  });

  it('shows the no-jobs empty state when nothing matches and no filter is active', () => {
    render(
      <JobsTable
        jobs={[]}
        hasActiveFilter={false}
        onToggleSelect={noop}
        renderActions={() => null}
      />,
    );

    expect(screen.getByTestId('jobs-empty-no-jobs')).toBeInTheDocument();
    expect(screen.queryByTestId('jobs-empty-no-matches')).not.toBeInTheDocument();
  });

  it('shows the no-matches empty state when a filter is active', () => {
    render(
      <JobsTable jobs={[]} hasActiveFilter onToggleSelect={noop} renderActions={() => null} />,
    );

    expect(screen.getByTestId('jobs-empty-no-matches')).toBeInTheDocument();
    expect(screen.queryByTestId('jobs-empty-no-jobs')).not.toBeInTheDocument();
  });

  it('delegates the row actions to the render prop', () => {
    render(
      <JobsTable
        jobs={[job]}
        hasActiveFilter={false}
        onToggleSelect={noop}
        renderActions={(row) => <button data-testid={`job-row-${row.id}-start`}>Start</button>}
      />,
    );

    expect(screen.getByTestId('job-row-job-1-start')).toBeInTheDocument();
  });
});

describe('JobsTableSkeleton', () => {
  it('renders the identifier the suspense fallback waits on', () => {
    render(<JobsTableSkeleton />);

    expect(screen.getByTestId('jobs-table-skeleton')).toBeInTheDocument();
  });
});

describe('JobsErrorBoundary', () => {
  it('renders its children while nothing throws', () => {
    render(
      <JobsErrorBoundary>
        <p data-testid="child">fine</p>
      </JobsErrorBoundary>,
    );

    expect(screen.getByTestId('child')).toBeInTheDocument();
  });

  it('replaces only the table when a child throws', () => {
    const Boom = (): never => {
      throw new Error('render failed');
    };
    const consoleError = jest.spyOn(console, 'error').mockImplementation(() => undefined);

    render(
      <JobsErrorBoundary>
        <Boom />
      </JobsErrorBoundary>,
    );

    expect(screen.getByTestId('jobs-error')).toBeInTheDocument();
    consoleError.mockRestore();
  });
});
