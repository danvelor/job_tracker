import { act, renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { SWRConfig } from 'swr';
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

const row = (id: string, title: string) => ({
  id,
  title,
  status: 'Scheduled' as const,
  scheduledDate: '2099-03-14',
  assigneeId: 'assignee-1',
  assigneeName: 'J. Ortiz',
  address: { street: '12 Elm St', city: 'Springfield', state: 'IL' },
  photoCount: 0,
});

const firstOfTwo: PagedJobs = {
  items: [row('job-1', 'Ridge tile replacement'), row('job-2', 'Gutter reline')],
  nextCursor: 'job-2',
};

const secondOfTwo: PagedJobs = { items: [row('job-3', 'Skylight seal')], nextCursor: null };

const fetchMock = jest.fn();

const wrapper = ({ children }: { children: ReactNode }) => (
  <SWRConfig value={{ provider: () => new Map() }}>{children}</SWRConfig>
);

beforeEach(() => {
  useJobsUiStore.getState().reset();
  fetchMock.mockReset();
  fetchMock.mockResolvedValue({ ok: true, json: async () => page });
  global.fetch = fetchMock as unknown as typeof fetch;
});

describe('useJobsPage', () => {
  it('renders the rows the server component resolved', async () => {
    const { result } = renderHook(() => useJobsPage(page, [], []), { wrapper });

    await waitFor(() => expect(result.current.jobs).toHaveLength(1));
    expect(result.current.jobs[0].title).toBe('Ridge tile replacement');
  });

  it('overlays the optimistic status onto the rows', async () => {
    const { result } = renderHook(() => useJobsPage(page, [], []), { wrapper });
    await waitFor(() => expect(result.current.jobs).toHaveLength(1));

    act(() => useJobsUiStore.getState().beginOptimistic('job-1', 'InProgress', 'Scheduled'));

    expect(result.current.jobs[0].status).toBe('InProgress');
    expect(result.current.jobs[0].isPending).toBe(true);
  });

  it('keeps the visible rows referentially stable across a re-render', async () => {
    const { result, rerender } = renderHook(() => useJobsPage(page, [], []), { wrapper });
    await waitFor(() => expect(result.current.jobs).toHaveLength(1));

    const before = result.current.jobs;
    rerender();

    expect(result.current.jobs).toBe(before);
  });

  it('says there is nothing more to load when the page carries no cursor', async () => {
    const { result } = renderHook(() => useJobsPage(page, [], []), { wrapper });

    await waitFor(() => expect(result.current.jobs).toHaveLength(1));
    expect(result.current.hasMore).toBe(false);
  });

  it('says there is more to load while the page carries a cursor', async () => {
    fetchMock.mockResolvedValue({ ok: true, json: async () => firstOfTwo });
    const { result } = renderHook(() => useJobsPage(firstOfTwo, [], []), { wrapper });

    await waitFor(() => expect(result.current.jobs).toHaveLength(2));
    expect(result.current.hasMore).toBe(true);
  });

  it('appends the next page rather than replacing the rows', async () => {
    fetchMock.mockResolvedValueOnce({ ok: true, json: async () => firstOfTwo });
    fetchMock.mockResolvedValueOnce({ ok: true, json: async () => secondOfTwo });
    const { result } = renderHook(() => useJobsPage(firstOfTwo, [], []), { wrapper });
    await waitFor(() => expect(result.current.jobs).toHaveLength(2));

    await act(async () => {
      await result.current.loadMore();
    });

    await waitFor(() => expect(result.current.jobs).toHaveLength(3));
    expect(result.current.jobs.map((job) => job.id)).toEqual(['job-1', 'job-2', 'job-3']);
    expect(result.current.hasMore).toBe(false);
  });

  it('asks the server for the next page with the cursor the last one returned', async () => {
    fetchMock.mockResolvedValueOnce({ ok: true, json: async () => firstOfTwo });
    fetchMock.mockResolvedValueOnce({ ok: true, json: async () => secondOfTwo });
    const { result } = renderHook(() => useJobsPage(firstOfTwo, [], []), { wrapper });
    await waitFor(() => expect(result.current.jobs).toHaveLength(2));

    await act(async () => {
      await result.current.loadMore();
    });

    const urls = fetchMock.mock.calls.map((call) => String(call[0]));
    expect(urls.some((url) => url.includes('cursor=job-2'))).toBe(true);
  });

  it('refetches when a slice emits jobs:invalidate', async () => {
    const { result } = renderHook(() => useJobsPage(page, [], []), { wrapper });
    await waitFor(() => expect(result.current.jobs).toHaveLength(1));
    const before = fetchMock.mock.calls.length;

    act(() => jobsEventBus.emit('jobs:invalidate', undefined));

    await waitFor(() => expect(fetchMock.mock.calls.length).toBeGreaterThan(before));
  });

  it('unsubscribes from the bus when it unmounts', async () => {
    const { result, unmount } = renderHook(() => useJobsPage(page, [], []), { wrapper });
    await waitFor(() => expect(result.current.jobs).toHaveLength(1));

    unmount();
    const before = fetchMock.mock.calls.length;
    act(() => jobsEventBus.emit('jobs:invalidate', undefined));

    expect(fetchMock.mock.calls.length).toBe(before);
  });
});
