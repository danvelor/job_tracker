import { act, renderHook, waitFor } from '@testing-library/react';
import { useJobsUiStore } from '@/presentation/stores/jobs-ui.store';
import { completeJobAction } from '../actions/complete-job.action';
import { useCompleteJob } from '../hooks/use-complete-job.hook';

jest.mock('../actions/complete-job.action', () => ({ completeJobAction: jest.fn() }));

const action = jest.mocked(completeJobAction);

beforeEach(() => {
  useJobsUiStore.getState().reset();
  action.mockReset();
  action.mockResolvedValue({ ok: true });
});

describe('useCompleteJob', () => {
  it('opens for one job and closes', () => {
    const { result } = renderHook(() => useCompleteJob());

    act(() => result.current.open('job-2'));
    expect(result.current.openFor).toBe('job-2');

    act(() => result.current.close());
    expect(result.current.openFor).toBeNull();
  });

  it('forgets the signature and the photos when it closes', () => {
    const { result } = renderHook(() => useCompleteJob());

    act(() => result.current.open('job-2'));
    act(() => result.current.setSignature('data:image/png;base64,AAA'));
    act(() => result.current.addPhoto({ url: 'p1.jpg', caption: 'ridge' }));
    act(() => result.current.close());

    expect(result.current.signature).toBe('');
    expect(result.current.photos).toEqual([]);
  });

  it('refuses to submit without a signature before calling the action (BR-4)', async () => {
    const { result } = renderHook(() => useCompleteJob());
    act(() => result.current.open('job-2'));

    await act(async () => result.current.submit('InProgress'));

    expect(action).not.toHaveBeenCalled();
    expect(result.current.error).toBe('A customer signature is required');
  });

  it('refuses a signature that is only whitespace', async () => {
    const { result } = renderHook(() => useCompleteJob());
    act(() => result.current.open('job-2'));
    act(() => result.current.setSignature('   '));

    await act(async () => result.current.submit('InProgress'));

    expect(action).not.toHaveBeenCalled();
  });

  it('removes a photo by index', () => {
    const { result } = renderHook(() => useCompleteJob());
    act(() => result.current.open('job-2'));
    act(() => result.current.addPhoto({ url: 'p1.jpg', caption: null }));
    act(() => result.current.addPhoto({ url: 'p2.jpg', caption: null }));

    act(() => result.current.removePhoto(0));

    expect(result.current.photos.map((photo) => photo.url)).toEqual(['p2.jpg']);
  });

  it('sends the signature and the photos it collected', async () => {
    const { result } = renderHook(() => useCompleteJob());
    act(() => result.current.open('job-2'));
    act(() => result.current.setSignature('data:image/png;base64,AAA'));
    act(() => result.current.addPhoto({ url: 'p1.jpg', caption: 'ridge' }));

    await act(async () => result.current.submit('InProgress'));

    expect(action).toHaveBeenCalledWith('job-2', {
      signatureUrl: 'data:image/png;base64,AAA',
      photos: [{ url: 'p1.jpg', caption: 'ridge' }],
    });
    await waitFor(() => expect(result.current.openFor).toBeNull());
  });

  it('rolls back and keeps the modal open on failure', async () => {
    action.mockResolvedValue({
      ok: false,
      error: {
        code: 'job.conflict',
        message: 'Only a job in progress can be completed',
        kind: 'conflict',
      },
    });

    const { result } = renderHook(() => useCompleteJob());
    act(() => result.current.open('job-2'));
    act(() => result.current.setSignature('data:image/png;base64,AAA'));
    await act(async () => result.current.submit('InProgress'));

    await waitFor(() =>
      expect(result.current.error).toBe('Only a job in progress can be completed'),
    );
    expect(result.current.openFor).toBe('job-2');
    expect(useJobsUiStore.getState().optimisticStatus['job-2']).toBeUndefined();
  });
});
