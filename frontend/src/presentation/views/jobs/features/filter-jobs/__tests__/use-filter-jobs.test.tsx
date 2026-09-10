import { act, renderHook } from '@testing-library/react';
import { useJobsUiStore } from '@/presentation/stores/jobs-ui.store';
import { useFilterJobs } from '../hooks/use-filter-jobs.hook';

describe('useFilterJobs', () => {
  beforeEach(() => {
    jest.useFakeTimers();
    useJobsUiStore.getState().reset();
  });

  afterEach(() => {
    act(() => {
      jest.runOnlyPendingTimers();
    });
    jest.useRealTimers();
  });

  it('writes text to the store immediately', () => {
    const { result } = renderHook(() => useFilterJobs());

    act(() => result.current.setText('ridge'));

    expect(result.current.filters.text).toBe('ridge');
  });

  it('debounces the text that reaches the query by 300ms', () => {
    const { result } = renderHook(() => useFilterJobs());

    act(() => result.current.setText('ridge'));
    expect(result.current.debouncedText).toBe('');

    act(() => {
      jest.advanceTimersByTime(300);
    });
    expect(result.current.debouncedText).toBe('ridge');
  });

  it('applies status immediately, without debouncing', () => {
    const { result } = renderHook(() => useFilterJobs());

    act(() => result.current.toggleStatus('Completed'));

    expect(result.current.filters.statuses).toEqual(['Completed']);
  });

  it('toggles a status off when it is already on', () => {
    const { result } = renderHook(() => useFilterJobs());

    act(() => result.current.toggleStatus('Completed'));
    act(() => result.current.toggleStatus('Completed'));

    expect(result.current.filters.statuses).toEqual([]);
  });

  it('sets a date range', () => {
    const { result } = renderHook(() => useFilterJobs());

    act(() => result.current.setDateRange('2099-03-01', '2099-03-31'));

    expect(result.current.filters.scheduledFrom).toBe('2099-03-01');
    expect(result.current.filters.scheduledTo).toBe('2099-03-31');
  });

  it('clamps a from date later than the to date (design A3)', () => {
    const { result } = renderHook(() => useFilterJobs());

    act(() => result.current.setDateRange('2099-03-01', '2099-03-31'));
    act(() => result.current.setDateRange('2099-04-15', '2099-03-31'));

    expect(result.current.filters.scheduledFrom).toBe('2099-04-15');
    expect(result.current.filters.scheduledTo).toBe('2099-04-15');
  });

  it('clamps a to date earlier than the from date', () => {
    const { result } = renderHook(() => useFilterJobs());

    act(() => result.current.setDateRange('2099-03-10', '2099-03-31'));
    act(() => result.current.setDateRange('2099-03-10', '2099-03-01'));

    expect(result.current.filters.scheduledFrom).toBe('2099-03-01');
    expect(result.current.filters.scheduledTo).toBe('2099-03-01');
  });

  it('reports whether any filter is active', () => {
    const { result } = renderHook(() => useFilterJobs());
    expect(result.current.hasActiveFilter).toBe(false);

    act(() => result.current.setAssignee('assignee-1'));
    expect(result.current.hasActiveFilter).toBe(true);
  });

  it('clear restores every default in one action', () => {
    const { result } = renderHook(() => useFilterJobs());

    act(() => result.current.setText('ridge'));
    act(() => result.current.toggleStatus('Completed'));
    act(() => result.current.clear());
    act(() => {
      jest.advanceTimersByTime(300);
    });

    expect(result.current.filters.text).toBe('');
    expect(result.current.filters.statuses).toEqual([]);
    expect(result.current.debouncedText).toBe('');
    expect(result.current.hasActiveFilter).toBe(false);
  });
});
