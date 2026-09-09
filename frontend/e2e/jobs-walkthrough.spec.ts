import { expect, test } from '@playwright/test';
import { JobsPage } from './pages/jobs.page';

/**
 * The acceptance walkthrough of prd.md section 8, grown one slice at a time
 * (architecture 8.3). Steps 2, 6, 7 and 8 arrive with plan 2B; steps 4 and 9
 * are asynchronous and belong to the Compose smoke run of architecture 8.1.
 */
test.describe('jobs walkthrough', () => {
  test('step 1: office staff open the job list', async ({ page }) => {
    const jobs = new JobsPage(page);
    await jobs.goto();
    await jobs.waitForList();

    await expect(jobs.root).toBeVisible();
    await expect(jobs.rows.first()).toBeVisible();
  });

  test('step 3: a scheduled job appears in the list', async ({ page }) => {
    const jobs = new JobsPage(page);
    await jobs.goto();
    await jobs.waitForList();

    await expect(jobs.rowTitle('job-1')).toHaveText('Ridge tile replacement');
    await expect(jobs.rowStatus('job-1')).toHaveText('Scheduled');
  });
});
