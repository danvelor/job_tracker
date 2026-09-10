import type { JobSummary } from '@/core/domain/job/job-summary.type';
import {
  DEFAULT_PAGE_SIZE,
  makeVisibleJobsSelector,
  selectFilters,
  selectIsSelected,
  selectOptimisticStatus,
  selectPageSize,
  useJobsUiStore,
} from '../jobs-ui.store';

const row = (id: string, title: string, status: JobSummary['status']): JobSummary => ({
  id,
  title,
  status,
  scheduledDate: '2099-03-14',
  assigneeId: 'assignee-1',
  assigneeName: 'J. Ortiz',
  address: { street: '12 Elm St', city: 'Springfield', state: 'IL' },
  photoCount: 0,
});

const rows: readonly JobSummary[] = [
  row('job-1', 'Ridge tile replacement', 'Scheduled'),
  row('job-2', 'Gutter reline', 'InProgress'),
];

describe('useJobsUiStore', () => {
  beforeEach(() => {
    useJobsUiStore.getState().reset();
  });

  it('holds no jobs array', () => {
    expect(Object.keys(useJobsUiStore.getState())).not.toContain('jobs');
  });

  it('merges a filter patch', () => {
    useJobsUiStore.getState().setFilter({ text: 'ridge' });

    expect(selectFilters(useJobsUiStore.getState()).text).toBe('ridge');
  });

  it('holds the page size, which is how much a load-more step asks for', () => {
    expect(selectPageSize(useJobsUiStore.getState())).toBe(DEFAULT_PAGE_SIZE);

    useJobsUiStore.getState().setPageSize(25);

    expect(selectPageSize(useJobsUiStore.getState())).toBe(25);
  });

  it('holds no cursor, because which pages are loaded belongs to SWR', () => {
    expect(Object.keys(useJobsUiStore.getState())).not.toContain('cursor');
  });

  it('keeps filter fields the patch did not mention', () => {
    useJobsUiStore.getState().setFilter({ text: 'ridge' });
    useJobsUiStore.getState().setFilter({ assigneeId: 'assignee-2' });

    const filters = selectFilters(useJobsUiStore.getState());
    expect(filters.text).toBe('ridge');
    expect(filters.assigneeId).toBe('assignee-2');
  });

  it('clearFilters restores the defaults', () => {
    useJobsUiStore.getState().setFilter({ text: 'ridge', statuses: ['Completed'] });
    useJobsUiStore.getState().clearFilters();

    const state = useJobsUiStore.getState();
    expect(selectFilters(state)).toEqual({
      text: '',
      statuses: [],
      scheduledFrom: null,
      scheduledTo: null,
      assigneeId: null,
    });
  });

  it('setSort replaces the config', () => {
    useJobsUiStore.getState().setSort('title', 'asc');

    expect(useJobsUiStore.getState().sortConfig).toEqual({
      field: 'title',
      direction: 'asc',
    });
  });

  it('toggles selection on and off', () => {
    useJobsUiStore.getState().toggleSelection('job-1');
    expect(selectIsSelected('job-1')(useJobsUiStore.getState())).toBe(true);

    useJobsUiStore.getState().toggleSelection('job-1');
    expect(selectIsSelected('job-1')(useJobsUiStore.getState())).toBe(false);
  });

  it('clears the whole selection', () => {
    useJobsUiStore.getState().toggleSelection('job-1');
    useJobsUiStore.getState().toggleSelection('job-2');
    useJobsUiStore.getState().clearSelection();

    expect(useJobsUiStore.getState().selectedJobIds).toEqual([]);
  });

  it('beginOptimistic records the target and the previous status', () => {
    useJobsUiStore.getState().beginOptimistic('job-1', 'InProgress', 'Scheduled');

    const state = useJobsUiStore.getState();
    expect(selectOptimisticStatus('job-1')(state)).toBe('InProgress');
    expect(state.rollbackSnapshot['job-1']).toBe('Scheduled');
  });

  it('commitOptimistic drops both entries so the row returns to server ownership', () => {
    useJobsUiStore.getState().beginOptimistic('job-1', 'InProgress', 'Scheduled');
    useJobsUiStore.getState().commitOptimistic('job-1');

    const state = useJobsUiStore.getState();
    expect(selectOptimisticStatus('job-1')(state)).toBeUndefined();
    expect(state.rollbackSnapshot['job-1']).toBeUndefined();
  });

  it('rollbackOptimistic restores the previous status and drops both entries', () => {
    useJobsUiStore.getState().beginOptimistic('job-1', 'InProgress', 'Scheduled');
    useJobsUiStore.getState().rollbackOptimistic('job-1');

    const state = useJobsUiStore.getState();
    expect(selectOptimisticStatus('job-1')(state)).toBeUndefined();
    expect(state.rollbackSnapshot['job-1']).toBeUndefined();
  });

  it('leaves other rows untouched when one rolls back', () => {
    useJobsUiStore.getState().beginOptimistic('job-1', 'InProgress', 'Scheduled');
    useJobsUiStore.getState().beginOptimistic('job-2', 'Completed', 'InProgress');
    useJobsUiStore.getState().rollbackOptimistic('job-1');

    expect(selectOptimisticStatus('job-2')(useJobsUiStore.getState())).toBe('Completed');
  });

  it('the visible selector overlays the optimistic status onto SWR rows', () => {
    useJobsUiStore.getState().beginOptimistic('job-1', 'InProgress', 'Scheduled');

    const visible = makeVisibleJobsSelector(rows)(useJobsUiStore.getState());

    expect(visible[0].status).toBe('InProgress');
    expect(visible[0].isPending).toBe(true);
    expect(visible[1].status).toBe('InProgress');
    expect(visible[1].isPending).toBe(false);
  });

  it('the visible selector marks selection', () => {
    useJobsUiStore.getState().toggleSelection('job-2');

    const visible = makeVisibleJobsSelector(rows)(useJobsUiStore.getState());

    expect(visible[0].isSelected).toBe(false);
    expect(visible[1].isSelected).toBe(true);
  });

  it('the visible selector does not reorder: sorting is server-side (D-18)', () => {
    const visible = makeVisibleJobsSelector(rows)(useJobsUiStore.getState());

    expect(visible.map((job) => job.id)).toEqual(['job-1', 'job-2']);
  });

  it('the visible selector returns a stable reference for the same inputs', () => {
    const select = makeVisibleJobsSelector(rows);
    const state = useJobsUiStore.getState();

    expect(select(state)).toBe(select(state));
  });

  it('the visible selector recomputes when the overlay changes', () => {
    const select = makeVisibleJobsSelector(rows);
    const before = select(useJobsUiStore.getState());

    useJobsUiStore.getState().beginOptimistic('job-1', 'Completed', 'Scheduled');

    expect(select(useJobsUiStore.getState())).not.toBe(before);
  });
});
