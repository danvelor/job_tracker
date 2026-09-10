import { expect, test } from '@playwright/test';
import { Client } from 'pg';
import { JobsPage } from './pages/jobs.page';

const CONNECTION =
  process.env.SMOKE_DATABASE_URL ??
  'postgres://jobtracker:jobtracker@127.0.0.1:5432/jobtracker';

async function query<T>(sql: string, params: readonly unknown[] = []): Promise<T[]> {
  const client = new Client({ connectionString: CONNECTION });
  await client.connect();

  try {
    const result = await client.query(sql, [...params]);
    return result.rows as T[];
  } finally {
    await client.end();
  }
}

async function eventually<T>(
  read: () => Promise<T>,
  satisfied: (value: T) => boolean,
  what: string,
  timeoutMs = 45_000,
): Promise<T> {
  const deadline = Date.now() + timeoutMs;
  let last: T = await read();

  while (Date.now() < deadline) {
    if (satisfied(last)) {
      return last;
    }

    await new Promise((resolve) => setTimeout(resolve, 500));
    last = await read();
  }

  throw new Error(`${what} did not happen within ${timeoutMs}ms. Last saw: ${JSON.stringify(last)}`);
}

const notificationsFor = (jobTitle: string) =>
  query<{ recipient: string; status: string }>(
    `select n.recipient, n.status
       from jobs.notifications n
       where n.body like $1
       order by n.recipient`,
    [`%${jobTitle}%`],
  );

const invoicesFor = (jobId: string) =>
  query<{ amount: string }>('select amount from billing.invoices where job_id = $1', [jobId]);

test.describe('the Compose stack', () => {
  test('the nine steps of the acceptance walkthrough', async ({ page }) => {
    const jobs = new JobsPage(page);
    const title = `Ridge tile replacement ${Date.now()}`;

    await jobs.goto();
    await jobs.waitForList();
    await expect(jobs.root).toBeVisible();

    await jobs.createJob(title);

    await expect(jobs.titleCell(title)).toBeVisible();
    const id = await jobs.idOf(title);
    await expect(jobs.rowStatus(id)).toHaveText('Scheduled');

    const afterCreate = await eventually(
      () => notificationsFor(title),
      (rows) => rows.length === 1 && rows[0].status === 'Sent',
      'the assignee notification',
    );
    expect(afterCreate[0].recipient).toBe('J. Ortiz');

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

    const invoices = await eventually(
      () => invoicesFor(id),
      (rows) => rows.length === 1,
      'the invoice',
    );
    expect(Number(invoices[0].amount)).toBeGreaterThan(0);

    const afterComplete = await eventually(
      () => notificationsFor(title),
      (rows) => rows.length === 2 && rows.every((row) => row.status === 'Sent'),
      'the customer notification',
    );
    expect(afterComplete.map((row) => row.recipient)).toEqual([
      'J. Ortiz',
      'ops@acme.test',
    ]);
  });

  test('completing without a signature is refused, against the real backend', async ({
    page,
  }) => {
    const jobs = new JobsPage(page);
    const title = `Gutter reline ${Date.now()}`;

    await jobs.goto();
    await jobs.waitForList();
    await jobs.createJob(title);

    const id = await jobs.idOf(title);
    await jobs.rowStart(id).click();
    await expect(jobs.rowStatus(id)).toHaveText('InProgress');

    await jobs.rowComplete(id).click();
    await jobs.completeModal.waitFor({ state: 'visible' });
    await jobs.completeSubmit.click();

    await expect(jobs.completeError).toBeVisible();
    await expect(jobs.completeModal).toBeVisible();
  });

  test('a job raised by one tenant is invisible to another', async () => {
    const rows = await query<{ count: string }>(
      `select count(*)::text as count from jobs.jobs
       where organization_id = '22222222-2222-2222-2222-222222222222'`,
    );

    expect(rows[0].count).toBe('0');
  });
});
