import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import JobsError from '../error';

const refresh = jest.fn();

jest.mock('next/navigation', () => ({ useRouter: () => ({ refresh: refresh }) }));

describe('JobsError', () => {
  const error = new Error('The upstream service is unavailable');

  beforeEach(() => {
    refresh.mockClear();
  });

  it('shows the message the boundary was handed', () => {
    render(<JobsError error={error} reset={jest.fn()} />);

    expect(screen.getByText('The upstream service is unavailable')).toBeInTheDocument();
  });

  it('announces itself to a screen reader', () => {
    render(<JobsError error={error} reset={jest.fn()} />);

    expect(screen.getByRole('alert')).toBeInTheDocument();
  });

  it('calls reset when the retry button is pressed', async () => {
    const reset = jest.fn();
    render(<JobsError error={error} reset={reset} />);

    await userEvent.click(screen.getByTestId('jobs-error-retry'));

    expect(reset).toHaveBeenCalledTimes(1);
  });

  it('discards the failed payload before resetting, so the retry reaches the server', async () => {
    render(<JobsError error={error} reset={jest.fn()} />);

    await userEvent.click(screen.getByTestId('jobs-error-retry'));

    expect(refresh).toHaveBeenCalledTimes(1);
  });

  it('renders an error carrying a digest, which is the production shape', () => {
    const digested: Error & { digest?: string } = Object.assign(
      new Error('Something went wrong'),
      { digest: 'a1b2c3' },
    );

    render(<JobsError error={digested} reset={jest.fn()} />);

    expect(screen.getByTestId('jobs-error')).toBeInTheDocument();
  });
});
