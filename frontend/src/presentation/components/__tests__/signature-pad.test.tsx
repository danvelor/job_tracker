import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SignaturePad } from '../molecules/signature-pad.component';

describe('SignaturePad', () => {
  it('reports what is typed into the accessible field', async () => {
    const onChange = jest.fn();
    render(<SignaturePad testId="probe" value="" onChange={onChange} />);

    await userEvent.type(screen.getByTestId('probe'), 'x');

    expect(onChange).toHaveBeenCalledWith('x');
  });

  it('exposes the field to assistive technology by name', () => {
    render(<SignaturePad testId="probe" value="" onChange={jest.fn()} />);

    // The text field is the accessible path for anyone who cannot sign with a
    // pointer, so it carries a name rather than relying on the canvas.
    expect(screen.getByLabelText('Customer signature')).toBeInTheDocument();
  });

  it('shows the value it was given and holds none of its own', () => {
    render(<SignaturePad testId="probe" value="data:image/png;base64,AAA" onChange={jest.fn()} />);

    expect(screen.getByTestId('probe')).toHaveValue('data:image/png;base64,AAA');
  });
});
