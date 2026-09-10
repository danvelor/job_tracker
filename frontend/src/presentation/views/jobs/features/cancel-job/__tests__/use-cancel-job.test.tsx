import { act, renderHook, waitFor } from '@testing-library/react';
import { useJobsUiStore } from '@/presentation/stores/jobs-ui.store';
import { cancelJobAction } from '../actions/cancel-job.action';
import { useCancelJob } from '../hooks/use-cancel-job.hook';

jest.mock('../actions/cancel-job.action', () => ({ cancelJobAction: jest.fn() }));

const action = jest.mocked(cancelJobAction);

beforeEach(() => {
  useJobsUiStore.getState().reset();
  action.mockReset();
  action.mockResolvedValue({ ok: true });
});

describe('useCancelJob', () => {
  it('opens the reason field for one row at a time', () => {
    const { result } = renderHook(() => useCancelJob());

    act(() => result.current.open('job-1'));
    expect(result.current.openFor).toBe('job-1');

    act(() => result.current.open('job-2'));
    expect(result.current.openFor).toBe('job-2');
  });

  it('closes the field and forgets the typed reason', () => {
    const { result } = renderHook(() => useCancelJob());

    act(() => result.current.open('job-1'));
    act(() => result.current.setReason('Weather'));
    act(() => result.current.close());

    expect(result.current.openFor).toBeNull();
    expect(result.current.reason).toBe('');
  });

  it('refuses an empty reason before calling the action (BR-5)', async () => {
    const { result } = renderHook(() => useCancelJob());

    act(() => result.current.open('job-1'));
    await act(async () => result.current.submit('Scheduled'));

    expect(action).not.toHaveBeenCalled();
    expect(result.current.errorFor('job-1')).toBe('A cancellation reason is required');
  });

  it('refuses a reason that is only whitespace', async () => {
    const { result } = renderHook(() => useCancelJob());

    act(() => result.current.open('job-1'));
    act(() => result.current.setReason('   '));
    await act(async () => result.current.submit('Scheduled'));

    expect(action).not.toHaveBeenCalled();
  });

  it('cancels optimistically and closes on success', async () => {
    const { result } = renderHook(() => useCancelJob());

    act(() => result.current.open('job-1'));
    act(() => result.current.setReason('Weather'));
    await act(async () => result.current.submit('Scheduled'));

    expect(action).toHaveBeenCalledWith('job-1', 'Weather');
    await waitFor(() => expect(result.current.openFor).toBeNull());
    await waitFor(() =>
      expect(useJobsUiStore.getState().optimisticStatus['job-1']).toBeUndefined(),
    );
  });

  it('rolls back and keeps the field open on failure', async () => {
    action.mockResolvedValue({
      ok: false,
      error: {
        code: 'job.conflict',
        message: 'A job in a terminal state cannot change state',
        kind: 'conflict',
      },
    });

    const { result } = renderHook(() => useCancelJob());
    act(() => result.current.open('job-1'));
    act(() => result.current.setReason('Weather'));
    await act(async () => result.current.submit('Completed'));

    await waitFor(() =>
      expect(result.current.errorFor('job-1')).toBe(
        'A job in a terminal state cannot change state',
      ),
    );
    expect(result.current.openFor).toBe('job-1');
    expect(result.current.reason).toBe('Weather');
  });
});
