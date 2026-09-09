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

  test('step 5: staff narrow the list by status and find the job', async ({ page }) => {
    const jobs = new JobsPage(page);
    await jobs.goto();
    await jobs.waitForList();

    await jobs.statusOption('Scheduled').check();

    // Waits on the filtered result rather than on a timeout: the Scheduled row
    // survives and an InProgress row does not.
    await expect(jobs.row('job-1')).toBeVisible();
    await expect(jobs.row('job-2')).toHaveCount(0);
  });

  test('step 5: clearing the filter brings the other rows back', async ({ page }) => {
    const jobs = new JobsPage(page);
    await jobs.goto();
    await jobs.waitForList();

    await jobs.statusOption('Scheduled').check();
    await expect(jobs.row('job-2')).toHaveCount(0);

    await jobs.clearFilters.click();

    await expect(jobs.row('job-2')).toBeVisible();
  });

  test('a filter that matches nothing says so, and says the right thing', async ({
    page,
  }) => {
    const jobs = new JobsPage(page);
    await jobs.goto();
    await jobs.waitForList();

    await jobs.searchInput.fill('nothing matches this');

    // The no-matches message, not the no-jobs one: the remedy differs, and
    // telling someone with an active filter that they have no jobs yet sends
    // them to create a duplicate (design A4).
    await expect(jobs.emptyNoMatches).toBeVisible();
  });
});
