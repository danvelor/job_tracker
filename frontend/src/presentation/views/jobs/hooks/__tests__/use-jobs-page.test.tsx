import { act, renderHook, waitFor } from '@testing-library/react';
import type { PagedJobs } from '@/core/application/ports/jobs.port';
import { useJobsUiStore } from '@/presentation/stores/jobs-ui.store';
import { jobsEventBus } from '@/shared/events/jobs-event-bus';
import { useJobsPage } from '../use-jobs-page.hook';

const page: PagedJobs = {
  items: [
    {
      id: 'job-1',
      title: 'Ridge tile replacement',
      status: 'Scheduled',
      scheduledDate: '2099-03-14',
      assigneeId: 'assignee-1',
      assigneeName: 'J. Ortiz',
      address: { street: '12 Elm St', city: 'Springfield', state: 'IL' },
      photoCount: 0,
    },
  ],
  nextCursor: null,
};

const fetchMock = jest.fn();

beforeEach(() => {
  useJobsUiStore.getState().reset();
  fetchMock.mockReset();
  // jsdom exposes no Response global, and the hook only reads `ok` and
  // `json()` — so the double is the contract rather than a shim of one.
  fetchMock.mockResolvedValue({ ok: true, json: async () => page });
  global.fetch = fetchMock as unknown as typeof fetch;
});

/**
 * The hook calls use() on the promise, so it suspends until that promise
 * resolves — which is the whole point of D-11. Without a boundary here,
 * renderHook leaves it suspended forever and result.current stays null. The
 * page provides this boundary in production; the test has to as well.
 */
describe('useJobsPage', () => {
  it('renders the rows the server component resolved', async () => {
    const { result } = renderHook(() => useJobsPage(page, [], []));

    await waitFor(() => expect(result.current.jobs).toHaveLength(1));
    expect(result.current.jobs[0].title).toBe('Ridge tile replacement');
  });

  it('overlays the optimistic status onto the rows', async () => {
    const { result } = renderHook(() => useJobsPage(page, [], []));
    await waitFor(() => expect(result.current.jobs).toHaveLength(1));

    act(() => useJobsUiStore.getState().beginOptimistic('job-1', 'InProgress', 'Scheduled'));

    expect(result.current.jobs[0].status).toBe('InProgress');
    expect(result.current.jobs[0].isPending).toBe(true);
  });

  it('keeps the visible rows referentially stable across unrelated changes', async () => {
    const { result } = renderHook(() => useJobsPage(page, [], []));
    await waitFor(() => expect(result.current.jobs).toHaveLength(1));

    const before = result.current.jobs;
    act(() => useJobsUiStore.getState().setCursor('job-1'));

    expect(result.current.jobs).toBe(before);
  });

  it('refetches when a slice emits jobs:invalidate', async () => {
    const { result } = renderHook(() => useJobsPage(page, [], []));
    await waitFor(() => expect(result.current.jobs).toHaveLength(1));
    const before = fetchMock.mock.calls.length;

    act(() => jobsEventBus.emit('jobs:invalidate', undefined));

    await waitFor(() => expect(fetchMock.mock.calls.length).toBeGreaterThan(before));
  });

  it('unsubscribes from the bus when it unmounts', async () => {
    const { result, unmount } = renderHook(() => useJobsPage(page, [], []));
    await waitFor(() => expect(result.current.jobs).toHaveLength(1));

    unmount();
    const before = fetchMock.mock.calls.length;
    act(() => jobsEventBus.emit('jobs:invalidate', undefined));

    expect(fetchMock.mock.calls.length).toBe(before);
  });
});
