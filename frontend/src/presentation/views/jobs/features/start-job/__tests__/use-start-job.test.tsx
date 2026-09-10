import { act, renderHook, waitFor } from '@testing-library/react';
import { useJobsUiStore } from '@/presentation/stores/jobs-ui.store';
import { jobsEventBus } from '@/shared/events/jobs-event-bus';
import { startJobAction } from '../actions/start-job.action';
import { useStartJob } from '../hooks/use-start-job.hook';

jest.mock('../actions/start-job.action', () => ({ startJobAction: jest.fn() }));

const action = jest.mocked(startJobAction);

beforeEach(() => {
  useJobsUiStore.getState().reset();
  action.mockReset();
});

describe('useStartJob', () => {
  it('shows the target status immediately, before the action resolves', async () => {
    let release = (): void => undefined;
    action.mockImplementation(
      () =>
        new Promise((resolve) => {
          release = () => resolve({ ok: true });
        }),
    );

    const { result } = renderHook(() => useStartJob());
    act(() => result.current.run('job-1', 'Scheduled'));

    expect(useJobsUiStore.getState().optimisticStatus['job-1']).toBe('InProgress');

    await act(async () => {
      release();
    });
  });

  it('commits the overlay and asks the view to revalidate on success', async () => {
    action.mockResolvedValue({ ok: true });
    const invalidate = jest.fn();
    jobsEventBus.on('jobs:invalidate', invalidate);

    const { result } = renderHook(() => useStartJob());
    await act(async () => result.current.run('job-1', 'Scheduled'));

    await waitFor(() =>
      expect(useJobsUiStore.getState().optimisticStatus['job-1']).toBeUndefined(),
    );
    expect(invalidate).toHaveBeenCalledTimes(1);
    jobsEventBus.off('jobs:invalidate', invalidate);
  });

  it('rolls back and reports the error on failure', async () => {
    action.mockResolvedValue({
      ok: false,
      error: {
        code: 'job.conflict',
        message: 'Only a Scheduled job can start',
        kind: 'conflict',
      },
    });

    const { result } = renderHook(() => useStartJob());
    await act(async () => result.current.run('job-1', 'Scheduled'));

    await waitFor(() =>
      expect(useJobsUiStore.getState().optimisticStatus['job-1']).toBeUndefined(),
    );
    expect(result.current.errorFor('job-1')).toBe('Only a Scheduled job can start');
  });

  it('keeps each row error to itself', async () => {
    action.mockResolvedValue({
      ok: false,
      error: { code: 'job.conflict', message: 'no', kind: 'conflict' },
    });

    const { result } = renderHook(() => useStartJob());
    await act(async () => result.current.run('job-1', 'Scheduled'));

    await waitFor(() => expect(result.current.errorFor('job-1')).toBe('no'));
    expect(result.current.errorFor('job-2')).toBeUndefined();
  });

  it('clears a previous error when the row is retried', async () => {
    action.mockResolvedValueOnce({
      ok: false,
      error: { code: 'job.conflict', message: 'no', kind: 'conflict' },
    });
    action.mockResolvedValueOnce({ ok: true });

    const { result } = renderHook(() => useStartJob());
    await act(async () => result.current.run('job-1', 'Scheduled'));
    await waitFor(() => expect(result.current.errorFor('job-1')).toBe('no'));

    await act(async () => result.current.run('job-1', 'Scheduled'));

    await waitFor(() => expect(result.current.errorFor('job-1')).toBeUndefined());
  });
});
