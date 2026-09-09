import { expect, test } from '@playwright/test';
import { Client } from 'pg';
import { JobsPage } from './pages/jobs.page';

/**
 * The definition of done (architecture 8.1): all nine steps of the acceptance
 * walkthrough in `context/prd.md` section 8, against the Compose stack.
 *
 * Run it with the stack already up:
 *
 *   docker compose up --build --wait
 *   npm run test:smoke
 *
 * Steps 4 and 9 query Postgres, because neither consequence appears in the
 * interface — design A5 step 6 says completion does not wait for them and does
 * not claim they happened. Reading the database the system just wrote is the
 * normal shape of an integration test; the alternative, a diagnostics endpoint,
 * would add public surface D-04 declined to give Billing.
 *
 * The two backend suites already prove steps 4 and 9 over HTTP, so this should
 * confirm rather than discover. When it does not, the difference is
 * infrastructure — networking, environment, startup order — which is precisely
 * what a stack-level run exists to catch, and did: see D-36.
 */
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

/**
 * Polls with a bound rather than sleeping. NFR-4 promises the consequences
 * arrive *within seconds*, not immediately: asserting on a bound is asserting
 * on eventual consistency, while a fixed sleep pretends the system is
 * synchronous and fails on a slow machine for a reason that is not a defect.
 */
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
  // One test, because the nine steps are one story and a reviewer reads them
  // as one. Splitting them would need each to recreate the state the last one
  // left, which is more machinery than the story is worth.
  test('the nine steps of the acceptance walkthrough', async ({ page }) => {
    const jobs = new JobsPage(page);
    const title = `Ridge tile replacement ${Date.now()}`;

    // --- 1. Office staff open the job list ------------------------------
    await jobs.goto();
    await jobs.waitForList();
    await expect(jobs.root).toBeVisible();

    // --- 2. Create a job through the form -------------------------------
    await jobs.createJob(title);

    // --- 3. It appears as Scheduled -------------------------------------
    await expect(jobs.titleCell(title)).toBeVisible();
    const id = await jobs.idOf(title);
    await expect(jobs.rowStatus(id)).toHaveText('Scheduled');

    // --- 4. The assignee is notified (FR-8) -----------------------------
    // Nothing in the interface says so, so the assertion goes to the data.
    const afterCreate = await eventually(
      () => notificationsFor(title),
      (rows) => rows.length === 1 && rows[0].status === 'Sent',
      'the assignee notification',
    );
    expect(afterCreate[0].recipient).toBe('J. Ortiz');

    // --- 5. Narrow by status and find it --------------------------------
    await jobs.statusOption('Scheduled').check();
    await expect(jobs.row(id)).toBeVisible();
    await jobs.clearFilters.click();

    // --- 6. Record that the crew started --------------------------------
    // BR-3 requires this, and the assessment's own flow omits it (D-14).
    await jobs.rowStart(id).click();
    await expect(jobs.rowStatus(id)).toHaveText('InProgress');

    // --- 7. Complete with a signature -----------------------------------
    await jobs.rowComplete(id).click();
    await jobs.completeModal.waitFor({ state: 'visible' });
    await jobs.completeSignature.fill('data:image/png;base64,AAA');
    await jobs.completeSubmit.click();
    await jobs.completeModal.waitFor({ state: 'detached' });

    // --- 8. It shows Completed ------------------------------------------
    await expect(jobs.rowStatus(id)).toHaveText('Completed');
    // And offers nothing further: the type-level state machine showing through
    // the interface, because a terminal state permits no action.
    await expect(jobs.rowStart(id)).toHaveCount(0);
    await expect(jobs.rowComplete(id)).toHaveCount(0);

    // --- 9. An invoice exists and the customer was notified -------------
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
    // BR-4 travels the whole way: the aggregate refuses, the API answers 400
    // naming the field, the adapter maps it to a validation CoreError and the
    // modal stays open with the message. Every layer is a real one here.
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
    // NFR-1 at the level of the stack. The frontend holds one organization, so
    // this asks the database what the other one can see — which is the only
    // place the answer is not already filtered.
    const rows = await query<{ count: string }>(
      `select count(*)::text as count from jobs.jobs
       where organization_id = '22222222-2222-2222-2222-222222222222'`,
    );

    expect(rows[0].count).toBe('0');
  });
});
