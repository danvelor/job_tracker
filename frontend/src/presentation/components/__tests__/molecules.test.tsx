import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { VisibleJob } from '@/presentation/stores/jobs-ui.store';
import { FormField } from '../molecules/form-field.component';
import { JobRow } from '../molecules/job-row.component';
import { SelectionSummary } from '../molecules/selection-summary.component';

const job: VisibleJob = {
  id: 'job-1',
  title: 'Ridge tile replacement',
  status: 'Scheduled',
  scheduledDate: '2099-03-14',
  assigneeId: 'assignee-1',
  assigneeName: 'J. Ortiz',
  address: { street: '12 Elm St', city: 'Springfield', state: 'IL' },
  photoCount: 0,
  isSelected: false,
  isPending: false,
};

const noop = () => undefined;

describe('molecules', () => {
  it('FormField wires the label and the error to the control', () => {
    render(
      <FormField id="title" label="Title" error="Required">
        <input id="title" data-testid="title" aria-describedby="title-error" />
      </FormField>,
    );

    expect(screen.getByLabelText('Title')).toBeInTheDocument();
    expect(screen.getByTestId('field-error-title')).toHaveTextContent('Required');
  });

  it('SelectionSummary counts loaded rows and the selection', () => {
    render(<SelectionSummary loaded={24} selected={2} />);

    expect(screen.getByTestId('jobs-selection-summary')).toHaveTextContent(
      'Showing 24 jobs · 2 selected',
    );
  });

  it('SelectionSummary omits the selection clause when nothing is selected', () => {
    render(<SelectionSummary loaded={24} selected={0} />);

    expect(screen.getByTestId('jobs-selection-summary')).toHaveTextContent('Showing 24 jobs');
    expect(screen.getByTestId('jobs-selection-summary')).not.toHaveTextContent('selected');
  });

  it('JobRow renders the identifiers the e2e contract names', () => {
    render(
      <table>
        <tbody>
          <JobRow job={job} onToggleSelect={noop} actions={null} />
        </tbody>
      </table>,
    );

    expect(screen.getByTestId('job-row-job-1')).toBeInTheDocument();
    expect(screen.getByTestId('job-row-job-1-title')).toHaveTextContent(
      'Ridge tile replacement',
    );
    expect(screen.getByTestId('job-row-job-1-status')).toHaveTextContent('Scheduled');
  });

  it('JobRow labels itself from data it actually holds', () => {
    render(
      <table>
        <tbody>
          <JobRow job={job} onToggleSelect={noop} actions={null} />
        </tbody>
      </table>,
    );

    expect(screen.getByTestId('job-row-job-1')).toHaveAttribute(
      'aria-label',
      'Ridge tile replacement, Scheduled, 2099-03-14, J. Ortiz',
    );
  });

  it('JobRow marks a pending row busy', () => {
    render(
      <table>
        <tbody>
          <JobRow job={{ ...job, isPending: true }} onToggleSelect={noop} actions={null} />
        </tbody>
      </table>,
    );

    expect(screen.getByTestId('job-row-job-1')).toHaveAttribute('aria-busy', 'true');
  });

  it('JobRow reports a selection toggle by identifier', async () => {
    const onToggleSelect = jest.fn();
    render(
      <table>
        <tbody>
          <JobRow job={job} onToggleSelect={onToggleSelect} actions={null} />
        </tbody>
      </table>,
    );

    await userEvent.click(screen.getByTestId('job-row-job-1-select'));

    expect(onToggleSelect).toHaveBeenCalledWith('job-1');
  });

  it('JobRow renders the actions it is handed and owns none itself', () => {
    render(
      <table>
        <tbody>
          <JobRow
            job={job}
            onToggleSelect={noop}
            actions={<button data-testid="job-row-job-1-start">Start</button>}
          />
        </tbody>
      </table>,
    );

    expect(screen.getByTestId('job-row-job-1-start')).toBeInTheDocument();
  });
});
