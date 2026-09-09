import { isOk } from '@/core/domain/result';
import type { CoreError, Result } from '@/core/domain/result.type';
import type { CreateJobInput } from '@/core/application/ports/jobs.port';
import { createInMemoryJobsAdapter } from '../in-memory-jobs.adapter';

// No cast: isOk is a type guard, so narrowing is what produces the value.
const unwrap = <T>(result: Result<T, CoreError>): T => {
  if (!isOk(result)) {
    throw new Error(`expected ok, received ${JSON.stringify(result.error)}`);
  }
  return result.value;
};

const validInput: CreateJobInput = {
  title: 'Roof repair',
  description: 'Replace ridge tiles',
  address: {
    street: '12 Elm St',
    city: 'Springfield',
    state: 'IL',
    zipCode: '62701',
    latitude: 39.78,
    longitude: -89.65,
  },
  scheduledDate: '2099-03-14',
  assigneeId: 'assignee-1',
  customerId: 'customer-1',
};

describe('InMemoryJobsAdapter', () => {
  it('returns seeded jobs newest first', async () => {
    const adapter = createInMemoryJobsAdapter();
    const page = unwrap(await adapter.search({ limit: 10 }));

    const dates = page.items.map((job) => job.scheduledDate);
    expect([...dates]).toEqual([...dates].sort().reverse());
  });

  it('creates a job and returns it in state Scheduled', async () => {
    const adapter = createInMemoryJobsAdapter();
    const id = unwrap(await adapter.create(validInput));

    const page = unwrap(await adapter.search({ limit: 50 }));
    expect(page.items.find((job) => job.id === id)?.status).toBe('Scheduled');
  });

  it('filters by status', async () => {
    const adapter = createInMemoryJobsAdapter();
    const page = unwrap(await adapter.search({ limit: 50, statuses: ['Completed'] }));

    expect(page.items.length).toBeGreaterThan(0);
    expect(page.items.every((job) => job.status === 'Completed')).toBe(true);
  });

  it('filters by free text over title and description', async () => {
    const adapter = createInMemoryJobsAdapter();
    unwrap(await adapter.create(validInput));

    const page = unwrap(await adapter.search({ limit: 50, text: 'ridge' }));
    expect(page.items.map((job) => job.title)).toContain('Roof repair');
  });

  it('filters by assignee', async () => {
    const adapter = createInMemoryJobsAdapter();
    unwrap(await adapter.create(validInput));

    const page = unwrap(await adapter.search({ limit: 50, assigneeId: 'assignee-1' }));
    expect(page.items.length).toBeGreaterThan(0);
    expect(page.items.every((job) => job.assigneeId === 'assignee-1')).toBe(true);
  });

  it('filters by scheduled date range', async () => {
    const adapter = createInMemoryJobsAdapter();
    const page = unwrap(
      await adapter.search({
        limit: 50,
        scheduledFrom: '2099-03-13',
        scheduledTo: '2099-03-14',
      }),
    );

    expect(page.items.length).toBeGreaterThan(0);
    expect(
      page.items.every(
        (job) => job.scheduledDate >= '2099-03-13' && job.scheduledDate <= '2099-03-14',
      ),
    ).toBe(true);
  });

  it('pages with a cursor without repeating rows', async () => {
    const adapter = createInMemoryJobsAdapter();
    const first = unwrap(await adapter.search({ limit: 2 }));
    expect(first.nextCursor).not.toBeNull();

    const second = unwrap(await adapter.search({ limit: 2, cursor: first.nextCursor }));

    const firstIds = first.items.map((job) => job.id);
    const secondIds = second.items.map((job) => job.id);
    expect(firstIds.some((id) => secondIds.includes(id))).toBe(false);
  });

  it('reports no next cursor on the last page', async () => {
    const adapter = createInMemoryJobsAdapter();
    const page = unwrap(await adapter.search({ limit: 100 }));

    expect(page.nextCursor).toBeNull();
  });

  it('sorts by title when asked', async () => {
    const adapter = createInMemoryJobsAdapter();
    const page = unwrap(await adapter.search({ limit: 50, sort: 'title' }));

    const titles = page.items.map((job) => job.title);
    expect([...titles]).toEqual([...titles].sort());
  });

  it('resolves the assignee name from the roster', async () => {
    const adapter = createInMemoryJobsAdapter();
    const id = unwrap(await adapter.create(validInput));

    const page = unwrap(await adapter.search({ limit: 50 }));
    expect(page.items.find((job) => job.id === id)?.assigneeName).toBe('J. Ortiz');
  });

  it('starts a Scheduled job', async () => {
    const adapter = createInMemoryJobsAdapter();
    const id = unwrap(await adapter.create(validInput));

    expect(isOk(await adapter.start(id))).toBe(true);
    const detail = unwrap(await adapter.getById(id));
    expect(detail.status).toBe('InProgress');
  });

  it('refuses to start a job that is not Scheduled (BR-3)', async () => {
    const adapter = createInMemoryJobsAdapter();
    const id = unwrap(await adapter.create(validInput));
    await adapter.start(id);

    const again = await adapter.start(id);
    expect(isOk(again)).toBe(false);
    if (!isOk(again)) expect(again.error.kind).toBe('conflict');
  });

  it('completes an InProgress job with a signature', async () => {
    const adapter = createInMemoryJobsAdapter();
    const id = unwrap(await adapter.create(validInput));
    await adapter.start(id);

    const done = await adapter.complete(id, {
      signatureUrl: 'data:image/png;base64,AAA',
      photos: [{ url: 'p1.jpg', caption: 'ridge' }],
    });

    expect(isOk(done)).toBe(true);
    const detail = unwrap(await adapter.getById(id));
    expect(detail.status).toBe('Completed');
    expect(detail.photos).toHaveLength(1);
  });

  it('refuses to complete without a signature (BR-4)', async () => {
    const adapter = createInMemoryJobsAdapter();
    const id = unwrap(await adapter.create(validInput));
    await adapter.start(id);

    const done = await adapter.complete(id, { signatureUrl: '', photos: [] });
    expect(isOk(done)).toBe(false);
    if (!isOk(done)) expect(done.error.kind).toBe('validation');
  });

  it('refuses to complete a job that never started (BR-3)', async () => {
    const adapter = createInMemoryJobsAdapter();
    const id = unwrap(await adapter.create(validInput));

    const done = await adapter.complete(id, { signatureUrl: 'sig', photos: [] });
    expect(isOk(done)).toBe(false);
    if (!isOk(done)) expect(done.error.kind).toBe('conflict');
  });

  it('cancels with a reason', async () => {
    const adapter = createInMemoryJobsAdapter();
    const id = unwrap(await adapter.create(validInput));

    expect(isOk(await adapter.cancel(id, 'Weather'))).toBe(true);
    const detail = unwrap(await adapter.getById(id));
    expect(detail.status).toBe('Cancelled');
  });

  it('refuses to cancel without a reason (BR-5)', async () => {
    const adapter = createInMemoryJobsAdapter();
    const id = unwrap(await adapter.create(validInput));

    const cancelled = await adapter.cancel(id, '   ');
    expect(isOk(cancelled)).toBe(false);
    if (!isOk(cancelled)) expect(cancelled.error.kind).toBe('validation');
  });

  it('refuses to change a terminal job (BR-2)', async () => {
    const adapter = createInMemoryJobsAdapter();
    const id = unwrap(await adapter.create(validInput));
    await adapter.cancel(id, 'Weather');

    const started = await adapter.start(id);
    expect(isOk(started)).toBe(false);
    if (!isOk(started)) expect(started.error.kind).toBe('conflict');
  });

  it('refuses to cancel a job that is already terminal (BR-2)', async () => {
    const adapter = createInMemoryJobsAdapter();
    const id = unwrap(await adapter.create(validInput));
    await adapter.cancel(id, 'Weather');

    // The other BR-2 case goes through start(); this one goes through
    // cancel(), and each has its own terminal guard to exercise.
    const again = await adapter.cancel(id, 'Changed our mind');
    expect(isOk(again)).toBe(false);
    if (!isOk(again)) expect(again.error.kind).toBe('conflict');
  });

  it('refuses a scheduled date in the past (BR-1)', async () => {
    const adapter = createInMemoryJobsAdapter();
    const created = await adapter.create({ ...validInput, scheduledDate: '2000-01-01' });

    expect(isOk(created)).toBe(false);
    if (!isOk(created)) {
      expect(created.error.kind).toBe('validation');
      expect(created.error.fieldErrors?.scheduledDate).toBeDefined();
    }
  });

  it('refuses an empty title', async () => {
    const adapter = createInMemoryJobsAdapter();
    const created = await adapter.create({ ...validInput, title: '  ' });

    expect(isOk(created)).toBe(false);
    if (!isOk(created)) expect(created.error.fieldErrors?.title).toBeDefined();
  });

  it('reports not-found for an unknown id', async () => {
    const adapter = createInMemoryJobsAdapter();

    // Widened to a common element type: the four calls return Results over
    // different values, and a heterogeneous array would give isOk a union it
    // cannot narrow. The value is irrelevant here — only the failure is.
    const results: Result<unknown, CoreError>[] = [
      await adapter.getById('missing'),
      await adapter.start('missing'),
      await adapter.cancel('missing', 'Weather'),
      await adapter.complete('missing', { signatureUrl: 'sig', photos: [] }),
    ];

    for (const result of results) {
      expect(isOk(result)).toBe(false);
      if (!isOk(result)) expect(result.error.kind).toBe('not-found');
    }
  });

  it('lists the assignee and customer rosters', async () => {
    const adapter = createInMemoryJobsAdapter();
    const assignees = unwrap(await adapter.assignees());
    const customers = unwrap(await adapter.customers());

    expect(assignees.length).toBeGreaterThan(0);
    expect(customers.length).toBeGreaterThan(0);
  });

  it('keeps each adapter instance independent', async () => {
    const first = createInMemoryJobsAdapter();
    const second = createInMemoryJobsAdapter();

    const before = unwrap(await second.search({ limit: 100 })).items.length;
    unwrap(await first.create(validInput));
    const after = unwrap(await second.search({ limit: 100 })).items.length;

    expect(after).toBe(before);
  });
});
