'use client';

import { useCallback, useEffect, useMemo, useRef } from 'react';
import useSWRInfinite from 'swr/infinite';
import { useShallow } from 'zustand/react/shallow';
import type { PagedJobs } from '@/core/application/ports/jobs.port';
import type { JobSummary, Party } from '@/core/domain/job/job-summary.type';
import { makeVisibleJobsSelector, useJobsUiStore } from '@/presentation/stores/jobs-ui.store';
import type { VisibleJob } from '@/presentation/stores/jobs-ui.store';
import { jobsEventBus } from '@/shared/events/jobs-event-bus';
import { useCancelJob } from '../features/cancel-job';
import { useCompleteJob } from '../features/complete-job';
import { useCreateJob } from '../features/create-job';
import { useFilterJobs } from '../features/filter-jobs';
import { useStartJob } from '../features/start-job';

function useStableRows(rows: readonly JobSummary[]): readonly JobSummary[] {
  const previous = useRef(rows);
  const unchanged =
    previous.current.length === rows.length &&
    previous.current.every((row, index) => row === rows[index]);

  if (!unchanged) {
    previous.current = rows;
  }

  return previous.current;
}

const fetchPage = async (key: string): Promise<PagedJobs> => {
  const response = await fetch(key);
  if (!response.ok) {
    throw new Error(`The job list could not be loaded (${response.status})`);
  }
  return (await response.json()) as PagedJobs;
};

export function useJobsPage(
  initial: PagedJobs,
  assignees: readonly Party[],
  customers: readonly Party[],
) {
  const filter = useFilterJobs();

  const create = useCreateJob();
  const start = useStartJob();
  const cancel = useCancelJob();
  const complete = useCompleteJob();
  const { sortConfig, pageSize, selectedJobIds, toggleSelection } = useJobsUiStore(
    useShallow((state) => ({
      sortConfig: state.sortConfig,
      pageSize: state.pageSize,
      selectedJobIds: state.selectedJobIds,
      toggleSelection: state.toggleSelection,
    })),
  );

  const keyFor = useCallback(
    (cursor: string | null) => {
      const params = new URLSearchParams();
      if (filter.debouncedText !== '') params.set('text', filter.debouncedText);
      filter.filters.statuses.forEach((status) => params.append('statuses', status));
      if (filter.filters.scheduledFrom !== null) {
        params.set('scheduledFrom', filter.filters.scheduledFrom);
      }
      if (filter.filters.scheduledTo !== null) {
        params.set('scheduledTo', filter.filters.scheduledTo);
      }
      if (filter.filters.assigneeId !== null) {
        params.set('assigneeId', filter.filters.assigneeId);
      }
      params.set('sort', sortConfig.field);
      params.set('limit', String(pageSize));
      if (cursor !== null) params.set('cursor', cursor);
      return `/api/jobs?${params.toString()}`;
    },
    [filter.debouncedText, filter.filters, sortConfig.field, pageSize],
  );

  const getKey = useCallback(
    (index: number, previous: PagedJobs | null): string | null => {
      if (index === 0) return keyFor(null);
      if (previous === null || previous.nextCursor === null) return null;
      return keyFor(previous.nextCursor);
    },
    [keyFor],
  );

  const { data, error, isLoading, size, setSize, mutate } = useSWRInfinite<PagedJobs>(
    getKey,
    fetchPage,
    { fallbackData: [initial], revalidateFirstPage: false, revalidateOnFocus: false },
  );

  const pages = data ?? [initial];
  const rows = useStableRows(pages.flatMap((page) => page.items));
  const hasMore = (pages[pages.length - 1]?.nextCursor ?? null) !== null;

  const loadMore = useCallback(async (): Promise<void> => {
    if (!hasMore) return;
    await setSize(size + 1);
  }, [hasMore, setSize, size]);

  const selectVisible = useMemo(() => makeVisibleJobsSelector(rows), [rows]);
  const jobs: readonly VisibleJob[] = useJobsUiStore(selectVisible);

  useEffect(() => {
    const revalidate = () => {
      void mutate();
    };
    jobsEventBus.on('jobs:invalidate', revalidate);
    return () => jobsEventBus.off('jobs:invalidate', revalidate);
  }, [mutate]);

  const onToggleSelect = useCallback((id: string) => toggleSelection(id), [toggleSelection]);

  return {
    jobs,
    assignees,
    customers,
    create,
    start,
    cancel,
    complete,
    hasMore,
    loadMore,
    selectedCount: selectedJobIds.length,
    hasActiveFilter: filter.hasActiveFilter,
    isLoading,
    error: error instanceof Error ? error : null,
    onToggleSelect,
  };
}
