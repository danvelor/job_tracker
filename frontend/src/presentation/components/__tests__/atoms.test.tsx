import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Button } from '../atoms/button.component';
import { Checkbox } from '../atoms/checkbox.component';
import { StatusBadge } from '../atoms/status-badge.component';
import { TextInput } from '../atoms/text-input.component';

describe('controlled atoms', () => {
  it('TextInput reports the value, not the event', async () => {
    const onChange = jest.fn();
    render(<TextInput testId="probe" value="" onChange={onChange} />);

    await userEvent.type(screen.getByTestId('probe'), 'a');

    expect(onChange).toHaveBeenCalledWith('a');
  });

  it('TextInput never holds its own value', async () => {
    render(<TextInput testId="probe" value="fixed" onChange={jest.fn()} />);
    const input = screen.getByTestId('probe');

    await userEvent.type(input, 'more');

    expect(input).toHaveValue('fixed');
  });

  it('TextInput shows the error it is given and links it for screen readers', () => {
    render(
      <TextInput testId="probe" value="" onChange={jest.fn()} error="Required" id="probe-id" />,
    );

    expect(screen.getByText('Required')).toBeInTheDocument();
    expect(screen.getByTestId('probe')).toHaveAttribute('aria-describedby', 'probe-id-error');
    expect(screen.getByTestId('probe')).toHaveAttribute('aria-invalid', 'true');
  });

  it('Checkbox reports the next checked state', async () => {
    const onChange = jest.fn();
    render(<Checkbox testId="probe" checked={false} onChange={onChange} label="Pick" />);

    await userEvent.click(screen.getByTestId('probe'));

    expect(onChange).toHaveBeenCalledWith(true);
  });

  it('Button renders a spinner and disables itself while pending', () => {
    render(
      <Button testId="probe" pending onClick={jest.fn()}>
        Save
      </Button>,
    );

    expect(screen.getByTestId('probe')).toBeDisabled();
    expect(screen.getByTestId('probe-spinner')).toBeInTheDocument();
  });

  it('StatusBadge renders the status as its text', () => {
    render(<StatusBadge testId="probe" status="InProgress" />);

    expect(screen.getByTestId('probe')).toHaveTextContent('InProgress');
  });
});
