import { render, screen } from '@testing-library/react';
import JobNotFound from '../not-found';

/**
 * Rendered by the `notFound()` call in app/jobs/[id]/page.tsx when a job does
 * not exist or belongs to another organization. The way back to the list is
 * the only interactive thing on the page, so it is the thing worth asserting.
 */
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

    // A job hidden by the tenant boundary and a job that never existed look
    // the same from outside, which is what stops the 404 from confirming that
    // another organization's job id is real.
    expect(screen.getByText(/does not exist, or it belongs to another organization/)).toBeInTheDocument();
  });
});
