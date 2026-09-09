'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { useShallow } from 'zustand/react/shallow';
import type { JobStatus } from '@/core/domain/job/job-status.type';
import { useJobsUiStore } from '@/presentation/stores/jobs-ui.store';

export const SEARCH_DEBOUNCE_MS = 300;

export function useFilterJobs() {
  const { filters, setFilter, clearFilters } = useJobsUiStore(
    useShallow((state) => ({
      filters: state.filters,
      setFilter: state.setFilter,
      clearFilters: state.clearFilters,
    })),
  );

  // Text is debounced before it reaches the query; every other control applies
  // immediately (design A5). The store holds the typed value so the input
  // stays responsive, and this is the value the SWR key uses — so a keystroke
  // does not become a request.
  const [debouncedText, setDebouncedText] = useState('');

  useEffect(() => {
    const timer = setTimeout(() => setDebouncedText(filters.text), SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [filters.text]);

  const setText = useCallback((text: string) => setFilter({ text }), [setFilter]);

  const toggleStatus = useCallback(
    (status: JobStatus) =>
      setFilter({
        statuses: filters.statuses.includes(status)
          ? filters.statuses.filter((current) => current !== status)
          : [...filters.statuses, status],
      }),
    [filters.statuses, setFilter],
  );

  const setDateRange = useCallback(
    (from: string | null, to: string | null) =>
      setFilter({ scheduledFrom: from, scheduledTo: to }),
    [setFilter],
  );

  const setAssignee = useCallback(
    (assigneeId: string | null) => setFilter({ assigneeId }),
    [setFilter],
  );

  const hasActiveFilter = useMemo(
    () =>
      filters.text !== '' ||
      filters.statuses.length > 0 ||
      filters.scheduledFrom !== null ||
      filters.scheduledTo !== null ||
      filters.assigneeId !== null,
    [filters],
  );

  return {
    filters,
    debouncedText,
    setText,
    toggleStatus,
    setDateRange,
    setAssignee,
    clear: clearFilters,
    hasActiveFilter,
  };
}

export type FilterJobsApi = ReturnType<typeof useFilterJobs>;
