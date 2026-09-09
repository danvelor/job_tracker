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

/**
 * The orchestrator of assessment line 132. It seeds SWR with the page the
 * Server Component resolved, composes the slice hooks, and returns exactly
 * what JobsClient renders. It holds no state of its own — it wires.
 *
 * It takes the **resolved** page rather than the promise. `use()` lives one
 * level up, in JobsClient, which is where D-11 puts it: the client component
 * that receives the unresolved promise as a prop is the thing that unwraps it.
 * Keeping the call there rather than here makes this hook a pure function of
 * data, and it is also the only shape testable under Jest — `use()` suspends
 * and never resumes in jsdom, so a hook that called it could only ever be
 * exercised through Playwright.
 */
export function useJobsPage(
  initial: PagedJobs,
  assignees: readonly Party[],
  customers: readonly Party[],
) {
  const filter = useFilterJobs();

  // The orchestrator composes the slices; none of them knows the others
  // exist. This is the only place they meet.
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

  // The key is the query. A filter change changes the key, which is what makes
  // SWR refetch — rather than an effect doing it by hand.
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

  // Line 157's useMemo on derived state. Rebuilding the factory on every
  // render would defeat its memo and re-render every row on any store change.
  const selectVisible = useMemo(() => makeVisibleJobsSelector(rows), [rows]);
  const jobs: readonly VisibleJob[] = useJobsUiStore(selectVisible);

  // The view reacts to the bus; slices emit on it. One direction only
  // (architecture 9.3), which is what stops a feedback loop.
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
