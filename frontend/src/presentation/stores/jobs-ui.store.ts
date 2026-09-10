import { create } from 'zustand';
import type { JobSortField } from '@/core/application/ports/jobs.port';
import type { JobStatus } from '@/core/domain/job/job-status.type';
import type { JobSummary } from '@/core/domain/job/job-summary.type';
import type { DeepReadonly } from '@/shared/types/deep-readonly.type';

export type JobFilters = {
  text: string;
  statuses: JobStatus[];
  scheduledFrom: string | null;
  scheduledTo: string | null;
  assigneeId: string | null;
};

export type SortConfig = { field: JobSortField; direction: 'asc' | 'desc' };

export type JobsUiState = DeepReadonly<{
  filters: JobFilters;
  pageSize: number;
  sortConfig: SortConfig;
  selectedJobIds: string[];
  optimisticStatus: Record<string, JobStatus>;
  rollbackSnapshot: Record<string, JobStatus>;
}>;

export type JobsUiActions = {
  setFilter: (patch: Partial<JobFilters>) => void;
  clearFilters: () => void;
  setPageSize: (pageSize: number) => void;
  setSort: (field: JobSortField, direction: 'asc' | 'desc') => void;
  toggleSelection: (id: string) => void;
  clearSelection: () => void;
  beginOptimistic: (id: string, target: JobStatus, previous: JobStatus) => void;
  commitOptimistic: (id: string) => void;
  rollbackOptimistic: (id: string) => void;
  reset: () => void;
};

const DEFAULT_FILTERS: JobFilters = {
  text: '',
  statuses: [],
  scheduledFrom: null,
  scheduledTo: null,
  assigneeId: null,
};

export const DEFAULT_PAGE_SIZE = 3;

const initialState: JobsUiState = {
  filters: DEFAULT_FILTERS,
  pageSize: DEFAULT_PAGE_SIZE,
  sortConfig: { field: 'scheduledDate', direction: 'desc' },
  selectedJobIds: [],
  optimisticStatus: {},
  rollbackSnapshot: {},
};

const without = (
  source: Readonly<Record<string, JobStatus>>,
  id: string,
): Record<string, JobStatus> =>
  Object.fromEntries(Object.entries(source).filter(([key]) => key !== id));

export const useJobsUiStore = create<JobsUiState & JobsUiActions>()((set) => ({
  ...initialState,

  setFilter: (patch) =>
    set((state) => ({ filters: { ...state.filters, ...patch } })),

  clearFilters: () => set({ filters: DEFAULT_FILTERS }),

  setPageSize: (pageSize) => set({ pageSize }),

  setSort: (field, direction) => set({ sortConfig: { field, direction } }),

  toggleSelection: (id) =>
    set((state) => ({
      selectedJobIds: state.selectedJobIds.includes(id)
        ? state.selectedJobIds.filter((selected) => selected !== id)
        : [...state.selectedJobIds, id],
    })),

  clearSelection: () => set({ selectedJobIds: [] }),

  beginOptimistic: (id, target, previous) =>
    set((state) => ({
      optimisticStatus: { ...state.optimisticStatus, [id]: target },
      rollbackSnapshot: { ...state.rollbackSnapshot, [id]: previous },
    })),

  commitOptimistic: (id) =>
    set((state) => ({
      optimisticStatus: without(state.optimisticStatus, id),
      rollbackSnapshot: without(state.rollbackSnapshot, id),
    })),

  rollbackOptimistic: (id) =>
    set((state) => ({
      optimisticStatus: without(state.optimisticStatus, id),
      rollbackSnapshot: without(state.rollbackSnapshot, id),
    })),

  reset: () => set(initialState),
}));

export type VisibleJob = JobSummary & {
  readonly isSelected: boolean;
  readonly isPending: boolean;
};

export const selectFilters = (state: JobsUiState): JobsUiState['filters'] => state.filters;

export const selectSortConfig = (state: JobsUiState): JobsUiState['sortConfig'] =>
  state.sortConfig;

export const selectPageSize = (state: JobsUiState): number => state.pageSize;

export const selectSelectedIds = (state: JobsUiState): readonly string[] =>
  state.selectedJobIds;

export const selectIsSelected =
  (id: string) =>
  (state: JobsUiState): boolean =>
    state.selectedJobIds.includes(id);

export const selectOptimisticStatus =
  (id: string) =>
  (state: JobsUiState): JobStatus | undefined =>
    state.optimisticStatus[id];

export const makeVisibleJobsSelector = (rows: readonly JobSummary[]) => {
  let lastOverlay: JobsUiState['optimisticStatus'] | null = null;
  let lastSelection: JobsUiState['selectedJobIds'] | null = null;
  let lastResult: readonly VisibleJob[] = [];
  let computed = false;

  return (state: JobsUiState): readonly VisibleJob[] => {
    if (
      computed &&
      state.optimisticStatus === lastOverlay &&
      state.selectedJobIds === lastSelection
    ) {
      return lastResult;
    }

    lastOverlay = state.optimisticStatus;
    lastSelection = state.selectedJobIds;
    lastResult = rows.map((job) => {
      const pending = state.optimisticStatus[job.id];
      return {
        ...job,
        status: pending ?? job.status,
        isPending: pending !== undefined,
        isSelected: state.selectedJobIds.includes(job.id),
      };
    });
    computed = true;

    return lastResult;
  };
};
