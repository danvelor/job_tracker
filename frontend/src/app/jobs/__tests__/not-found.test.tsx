import { render, screen } from '@testing-library/react';
import JobNotFound from '../not-found';

describe('JobNotFound', () => {
  it('says the job does not exist', () => {
    render(<JobNotFound />);

    expect(screen.getByTestId('jobs-not-found')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'No such job' })).toBeInTheDocument();
  });

  it('offers a way back to the job list', () => {
    render(<JobNotFound />);

    expect(screen.getByRole('link', { name: 'Back to the job list' })).toHaveAttribute(
      'href',
      '/jobs',
    );
  });

  it('does not leak the reason a job was withheld', () => {
    render(<JobNotFound />);

    expect(screen.getByText(/does not exist, or it belongs to another organization/)).toBeInTheDocument();
  });
});
