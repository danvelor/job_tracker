import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useJobsUiStore } from '@/presentation/stores/jobs-ui.store';
import { JobFilterBar } from '../components/molecules/job-filter-bar.component';

const ASSIGNEES = [
  { id: 'assignee-1', name: 'J. Ortiz' },
  { id: 'assignee-2', name: 'M. Ruiz' },
];

describe('JobFilterBar', () => {
  beforeEach(() => useJobsUiStore.getState().reset());

  it('renders only the children it is given', () => {
    render(
      <JobFilterBar assignees={ASSIGNEES}>
        <JobFilterBar.Status />
        <JobFilterBar.Search />
      </JobFilterBar>,
    );

    expect(screen.getByTestId('filter-status-select')).toBeInTheDocument();
    expect(screen.getByTestId('filter-search-input')).toBeInTheDocument();
    // The root does not know the set: nothing renders that was not asked for.
    expect(screen.queryByTestId('filter-date-from')).not.toBeInTheDocument();
    expect(screen.queryByTestId('filter-assignee-select')).not.toBeInTheDocument();
  });

  it('a child writes through the context to the store', async () => {
    render(
      <JobFilterBar assignees={ASSIGNEES}>
        <JobFilterBar.Status />
      </JobFilterBar>,
    );

    await userEvent.click(screen.getByTestId('filter-status-option-Completed'));

    expect(useJobsUiStore.getState().filters.statuses).toEqual(['Completed']);
  });

  it('the assignee child offers the roster it was handed', () => {
    render(
      <JobFilterBar assignees={ASSIGNEES}>
        <JobFilterBar.Assignee />
      </JobFilterBar>,
    );

    expect(screen.getByRole('option', { name: 'J. Ortiz' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'M. Ruiz' })).toBeInTheDocument();
  });

  it('Clear resets every filter', async () => {
    render(
      <JobFilterBar assignees={ASSIGNEES}>
        <JobFilterBar.Status />
        <JobFilterBar.Clear />
      </JobFilterBar>,
    );

    await userEvent.click(screen.getByTestId('filter-status-option-Completed'));
    await userEvent.click(screen.getByTestId('filter-clear-button'));

    expect(useJobsUiStore.getState().filters.statuses).toEqual([]);
  });

  it('a child used outside the root fails loudly rather than silently', () => {
    const consoleError = jest.spyOn(console, 'error').mockImplementation(() => undefined);

    expect(() => render(<JobFilterBar.Status />)).toThrow(
      'JobFilterBar.Status must be rendered inside JobFilterBar',
    );

    consoleError.mockRestore();
  });
});
