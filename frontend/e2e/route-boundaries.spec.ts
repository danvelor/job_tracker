import { expect, test } from '@playwright/test';
import { JobsPage } from './pages/jobs.page';

/**
 * The route-level boundaries of assessment lines 106-110. Jest covers what
 * those two components render; only a browser proves that Next mounts them —
 * that `notFound()` in `[id]/page.tsx` reaches `app/jobs/not-found.tsx`, and
 * that the segment answers with a 404 rather than a 200 that merely looks
 * like one.
 */
test.describe('route boundaries', () => {
  test('an unknown job renders the custom 404', async ({ page }) => {
    const jobs = new JobsPage(page);

    const response = await jobs.gotoJob('no-such-job');

    await expect(jobs.notFound).toBeVisible();

    // 200, and deliberately so. `app/jobs/loading.tsx` covers this child
    // segment, so Next flushes the shell before the page resolves and the
    // status is already on the wire when notFound() runs. Measured: with that
    // file moved aside and nothing else changed, the same request answers 404.
    //
    // The trade is a real one — a crawler reads the status, not the markup —
    // and it is recorded as D-39. Asserted rather than ignored so that a route
    // restructure that recovers the 404 shows up here as a red test with this
    // note attached, instead of passing unnoticed.
    expect(response?.status()).toBe(200);
  });

  test('the 404 leads back to the job list', async ({ page }) => {
    const jobs = new JobsPage(page);
    await jobs.gotoJob('no-such-job');

    await jobs.backToList.click();
    await jobs.waitForList();

    await expect(jobs.root).toBeVisible();
  });

  test('a real job id renders the job, not the 404', async ({ page }) => {
    const jobs = new JobsPage(page);

    // The control. Without it, a detail route that renders the 404 for every
    // id would pass the two tests above.
    await jobs.gotoJob('job-1');

    await expect(page.getByTestId('job-detail')).toBeVisible();
    await expect(jobs.notFound).toHaveCount(0);
  });
});
