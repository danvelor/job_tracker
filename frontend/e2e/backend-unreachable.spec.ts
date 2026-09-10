import { expect, test } from '@playwright/test';
import { JobsPage } from './pages/jobs.page';

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

    const retried = page.waitForRequest(
      (request) => request.url().includes('/jobs') && !request.url().includes('/_next/static'),
      { timeout: 10_000 },
    );
    await jobs.routeErrorRetry.click();

    await expect(retried).resolves.toBeTruthy();
    await expect(jobs.routeErrorRetry).toBeVisible();
  });
});
