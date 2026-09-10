import { expect, test } from '@playwright/test';
import { JobsPage } from './pages/jobs.page';

/**
 * Runs against the `backend-unreachable` project: a second Next server whose
 * JOBTRACKER_API_URL points at a port nothing listens on. `HttpJobsAdapter`
 * cannot connect, `searchJobs` returns a failure, `app/jobs/page.tsx` throws,
 * and `app/jobs/error.tsx` takes over.
 *
 * A backend that is down is the failure this boundary exists for, so it is the
 * one worth reproducing. Request interception could not do it: the fetch that
 * fails is made by the Next server during the Server Component render, not by
 * the browser.
 */
test.describe('the job list when the backend cannot be reached', () => {
  test('the route error boundary takes over', async ({ page }) => {
    const jobs = new JobsPage(page);
    await jobs.goto();

    await expect(jobs.routeErrorRetry).toBeVisible();
    await expect(jobs.errorRegion).toBeVisible();
  });

  test('the page itself never renders', async ({ page }) => {
    const jobs = new JobsPage(page);
    await jobs.goto();

    await expect(jobs.routeErrorRetry).toBeVisible();
    // No half-rendered table behind the error, and no skeleton left spinning.
    await expect(jobs.root).toHaveCount(0);
    await expect(jobs.skeleton).toHaveCount(0);
  });

  test('the error is announced to a screen reader', async ({ page }) => {
    const jobs = new JobsPage(page);
    await jobs.goto();

    await expect(page.getByRole('alert')).toBeVisible();
  });

  test('retry asks the server again', async ({ page }) => {
    const jobs = new JobsPage(page);
    await jobs.goto();
    await expect(jobs.routeErrorRetry).toBeVisible();

    // What "retry" has to mean: a fresh attempt at the route reaches the
    // server. Re-rendering the failure that is already in hand would look
    // identical on screen and recover nothing.
    const retried = page.waitForRequest(
      (request) => request.url().includes('/jobs') && !request.url().includes('/_next/static'),
      { timeout: 10_000 },
    );
    await jobs.routeErrorRetry.click();

    await expect(retried).resolves.toBeTruthy();
    // The backend is still down, so the boundary is still the right answer.
    await expect(jobs.routeErrorRetry).toBeVisible();
  });
});
