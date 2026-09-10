'use client';

import { useCallback, useEffect, useMemo } from 'react';
import useSWR from 'swr';
import { useShallow } from 'zustand/react/shallow';
import type { PagedJobs } from '@/core/application/ports/jobs.port';
import type { Party } from '@/core/domain/job/job-summary.type';
import { makeVisibleJobsSelector, useJobsUiStore } from '@/presentation/stores/jobs-ui.store';
import type { VisibleJob } from '@/presentation/stores/jobs-ui.store';
import { jobsEventBus } from '@/shared/events/jobs-event-bus';
import { useCancelJob } from '../features/cancel-job';
import { useCompleteJob } from '../features/complete-job';
import { useCreateJob } from '../features/create-job';
import { useFilterJobs } from '../features/filter-jobs';
import { useStartJob } from '../features/start-job';

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
  const { sortConfig, cursor, selectedJobIds, toggleSelection } = useJobsUiStore(
    useShallow((state) => ({
      sortConfig: state.sortConfig,
      cursor: state.cursor,
      selectedJobIds: state.selectedJobIds,
      toggleSelection: state.toggleSelection,
    })),
  );

  const key = useMemo(() => {
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
    if (cursor !== null) params.set('cursor', cursor);
    return `/api/jobs?${params.toString()}`;
  }, [filter.debouncedText, filter.filters, sortConfig.field, cursor]);

  const { data, error, isLoading, mutate } = useSWR<PagedJobs>(key, fetchPage, {
    fallbackData: initial,
    revalidateOnFocus: false,
  });

  const rows = data?.items ?? initial.items;

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
    selectedCount: selectedJobIds.length,
    hasActiveFilter: filter.hasActiveFilter,
    isLoading,
    error: error instanceof Error ? error : null,
    onToggleSelect,
  };
}
