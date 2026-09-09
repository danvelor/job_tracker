import { getContainer, resetContainer } from '../container';

describe('container', () => {
  afterEach(() => {
    resetContainer();
  });

  it('resolves an adapter that answers a search', async () => {
    const container = getContainer();
    const result = await container.jobs.search({ limit: 1 });

    expect(result.ok).toBe(true);
  });

  it('returns the same instance on repeated calls', () => {
    expect(getContainer()).toBe(getContainer());
  });

  it('builds a fresh instance after a reset', () => {
    const before = getContainer();
    resetContainer();

    expect(getContainer()).not.toBe(before);
  });

  it('does not carry writes across a reset', async () => {
    const created = await getContainer().jobs.create({
      title: 'Roof repair',
      description: '',
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
    });
    expect(created.ok).toBe(true);

    const afterCreate = await getContainer().jobs.search({ limit: 100 });
    resetContainer();
    const afterReset = await getContainer().jobs.search({ limit: 100 });

    if (!afterCreate.ok || !afterReset.ok) throw new Error('expected both searches to succeed');
    expect(afterReset.value.items.length).toBeLessThan(afterCreate.value.items.length);
  });
});
