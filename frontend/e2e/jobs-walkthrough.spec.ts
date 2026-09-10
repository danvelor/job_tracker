import { expect, test } from '@playwright/test';
import { JobsPage } from './pages/jobs.page';

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

    await expect(jobs.emptyNoMatches).toBeVisible();
  });

  test('the acceptance walkthrough, end to end', async ({ page }) => {
    const jobs = new JobsPage(page);
    await jobs.goto();
    await jobs.waitForList();

    await jobs.createJob('Chimney reflash');

    await expect(jobs.titleCell('Chimney reflash')).toBeVisible();
    const id = await jobs.idOf('Chimney reflash');
    await expect(jobs.rowStatus(id)).toHaveText('Scheduled');

    await jobs.statusOption('Scheduled').check();
    await expect(jobs.row(id)).toBeVisible();
    await jobs.clearFilters.click();

    await jobs.rowStart(id).click();
    await expect(jobs.rowStatus(id)).toHaveText('InProgress');

    await jobs.rowComplete(id).click();
    await jobs.completeModal.waitFor({ state: 'visible' });
    await jobs.completeSignature.fill('data:image/png;base64,AAA');
    await jobs.completeSubmit.click();
    await jobs.completeModal.waitFor({ state: 'detached' });

    await expect(jobs.rowStatus(id)).toHaveText('Completed');
    await expect(jobs.rowStart(id)).toHaveCount(0);
    await expect(jobs.rowComplete(id)).toHaveCount(0);
  });

  test('completing without a signature is refused (BR-4)', async ({ page }) => {
    const jobs = new JobsPage(page);
    await jobs.goto();
    await jobs.waitForList();

    await jobs.rowComplete('job-2').click();
    await jobs.completeModal.waitFor({ state: 'visible' });
    await jobs.completeSubmit.click();

    await expect(jobs.completeError).toHaveText('A customer signature is required');
    await expect(jobs.completeModal).toBeVisible();
  });
});
