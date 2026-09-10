import { expect, test } from '@playwright/test';
import { JobsPage } from './pages/jobs.page';

test.describe('route boundaries', () => {
  test('an unknown job renders the custom 404', async ({ page }) => {
    const jobs = new JobsPage(page);

    const response = await jobs.gotoJob('no-such-job');

    await expect(jobs.notFound).toBeVisible();

    expect(response?.status()).toBe(200);
  });

  test('the 404 leads back to the job list', async ({ page }) => {
    const jobs = new JobsPage(page);
    await jobs.gotoJob('no-such-job');

    await jobs.backToList.click();
    await jobs.waitForList();

    await expect(jobs.root).toBeVisible();
  });

  test('the root lands on the job list', async ({ page }) => {
    await page.goto('/');

    await expect(page).toHaveURL(/\/jobs$/);
    await expect(page.getByTestId('jobs-page')).toBeVisible();
  });

  test('a real job id renders the job, not the 404', async ({ page }) => {
    const jobs = new JobsPage(page);

    await jobs.gotoJob('job-1');

    await expect(page.getByTestId('job-detail')).toBeVisible();
    await expect(jobs.notFound).toHaveCount(0);
  });
});
