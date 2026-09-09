import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { JobStatus } from '@/core/domain/job/job-status.type';
import type { VisibleJob } from '@/presentation/stores/jobs-ui.store';
import { JobRowActions } from '../job-row-actions.component';
import type { CancelApi } from '../job-row-actions.component';

const jobWith = (status: JobStatus, isPending = false): VisibleJob => ({
  id: 'job-1',
  title: 'Ridge tile replacement',
  status,
  scheduledDate: '2099-03-14',
  assigneeId: 'assignee-1',
  assigneeName: 'J. Ortiz',
  address: { street: '12 Elm St', city: 'Springfield', state: 'IL' },
  photoCount: 0,
  isSelected: false,
  isPending,
});

const cancelApi = (overrides: Partial<CancelApi> = {}): CancelApi => ({
  openFor: null,
  reason: '',
  isPending: false,
  open: jest.fn(),
  close: jest.fn(),
  setReason: jest.fn(),
  submit: jest.fn(),
  errorFor: () => undefined,
  ...overrides,
});

const noop = () => undefined;

const renderActions = (job: VisibleJob, cancel = cancelApi(), startError?: string) =>
  render(
    <JobRowActions
      job={job}
      onStart={noop}
      onComplete={noop}
      cancel={cancel}
      startError={startError}
    />,
  );

describe('JobRowActions', () => {
  it('offers Start and Cancel on a Scheduled row', () => {
    renderActions(jobWith('Scheduled'));

    expect(screen.getByTestId('job-row-job-1-start')).toBeInTheDocument();
    expect(screen.getByTestId('job-row-job-1-cancel')).toBeInTheDocument();
    expect(screen.queryByTestId('job-row-job-1-complete')).not.toBeInTheDocument();
  });

  it('offers Complete and Cancel on an InProgress row', () => {
    renderActions(jobWith('InProgress'));

    expect(screen.getByTestId('job-row-job-1-complete')).toBeInTheDocument();
    expect(screen.getByTestId('job-row-job-1-cancel')).toBeInTheDocument();
    expect(screen.queryByTestId('job-row-job-1-start')).not.toBeInTheDocument();
  });

  it.each<JobStatus>(['Completed', 'Cancelled'])(
    'offers nothing on a %s row, because the model says so',
    (status) => {
      renderActions(jobWith(status));

      expect(screen.queryByTestId('job-row-job-1-start')).not.toBeInTheDocument();
      expect(screen.queryByTestId('job-row-job-1-complete')).not.toBeInTheDocument();
      expect(screen.queryByTestId('job-row-job-1-cancel')).not.toBeInTheDocument();
    },
  );

  it('disables every action while a change is in flight', () => {
    renderActions(jobWith('Scheduled', true));

    expect(screen.getByTestId('job-row-job-1-start')).toBeDisabled();
    expect(screen.getByTestId('job-row-job-1-cancel')).toBeDisabled();
  });

  it('opens the cancel field for its own row', async () => {
    const cancel = cancelApi();
    renderActions(jobWith('Scheduled'), cancel);

    await userEvent.click(screen.getByTestId('job-row-job-1-cancel'));

    expect(cancel.open).toHaveBeenCalledWith('job-1');
  });

  it('shows the reason field only for the row it was opened on', () => {
    renderActions(jobWith('Scheduled'), cancelApi({ openFor: 'job-2' }));

    expect(screen.queryByTestId('job-row-job-1-cancel-reason')).not.toBeInTheDocument();
  });

  it('shows the reason field when it is this row that is open', () => {
    renderActions(jobWith('Scheduled'), cancelApi({ openFor: 'job-1' }));

    expect(screen.getByTestId('job-row-job-1-cancel-reason')).toBeInTheDocument();
  });

  it('surfaces a rollback error on the row', () => {
    renderActions(jobWith('Scheduled'), cancelApi(), 'Only a Scheduled job can start');

    expect(screen.getByTestId('job-row-job-1-error')).toHaveTextContent(
      'Only a Scheduled job can start',
    );
  });

  it('shows no error slot when nothing failed', () => {
    renderActions(jobWith('Scheduled'));

    expect(screen.queryByTestId('job-row-job-1-error')).not.toBeInTheDocument();
  });
});
