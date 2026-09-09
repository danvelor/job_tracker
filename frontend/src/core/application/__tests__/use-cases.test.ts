import { isOk, ok } from '@/core/domain/result';
import type { CreateJobInput, JobsPort } from '../ports/jobs.port';
import { cancelJob } from '../use-cases/cancel-job.use-case';
import { completeJob } from '../use-cases/complete-job.use-case';
import { createJob } from '../use-cases/create-job.use-case';
import { getJob } from '../use-cases/get-job.use-case';
import { listAssignees, listCustomers } from '../use-cases/list-parties.use-case';
import { searchJobs } from '../use-cases/search-jobs.use-case';
import { startJob } from '../use-cases/start-job.use-case';

const stubPort = (overrides: Partial<JobsPort> = {}): JobsPort => ({
  search: async () => ok({ items: [], nextCursor: null }),
  getById: async () => ok({} as never),
  create: async () => ok('job-1'),
  start: async () => ok(undefined),
  complete: async () => ok(undefined),
  cancel: async () => ok(undefined),
  assignees: async () => ok([]),
  customers: async () => ok([]),
  ...overrides,
});

const input: CreateJobInput = {
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
};

describe('use cases', () => {
  it('searchJobs applies a default limit when none is given', async () => {
    const search = jest.fn(async () => ok({ items: [], nextCursor: null }));
    const result = await searchJobs(stubPort({ search }), {});

    expect(isOk(result)).toBe(true);
    expect(search).toHaveBeenCalledWith(expect.objectContaining({ limit: 20 }));
  });

  it('searchJobs honours an explicit limit', async () => {
    const search = jest.fn(async () => ok({ items: [], nextCursor: null }));
    await searchJobs(stubPort({ search }), { limit: 5 });

    expect(search).toHaveBeenCalledWith(expect.objectContaining({ limit: 5 }));
  });

  it('searchJobs passes the filters through untouched', async () => {
    const search = jest.fn(async () => ok({ items: [], nextCursor: null }));
    await searchJobs(stubPort({ search }), { text: 'ridge', statuses: ['Scheduled'] });

    expect(search).toHaveBeenCalledWith({
      text: 'ridge',
      statuses: ['Scheduled'],
      limit: 20,
    });
  });

  it('createJob passes the input straight through and returns the id', async () => {
    const create = jest.fn(async () => ok('job-9'));
    const result = await createJob(stubPort({ create }), input);

    expect(create).toHaveBeenCalledWith(input);
    expect(isOk(result) && result.value).toBe('job-9');
  });

  it('getJob asks the port for the identifier it was given', async () => {
    const getById = jest.fn(async () => ok({} as never));
    await getJob(stubPort({ getById }), 'job-7');

    expect(getById).toHaveBeenCalledWith('job-7');
  });

  it('startJob asks the port for the identifier it was given', async () => {
    const start = jest.fn(async () => ok(undefined));
    await startJob(stubPort({ start }), 'job-7');

    expect(start).toHaveBeenCalledWith('job-7');
  });

  it('completeJob forwards the identifier and the input', async () => {
    const complete = jest.fn(async () => ok(undefined));
    const payload = { signatureUrl: 'sig', photos: [] };
    await completeJob(stubPort({ complete }), 'job-7', payload);

    expect(complete).toHaveBeenCalledWith('job-7', payload);
  });

  it('cancelJob forwards the identifier and the reason', async () => {
    const cancel = jest.fn(async () => ok(undefined));
    await cancelJob(stubPort({ cancel }), 'job-7', 'Weather');

    expect(cancel).toHaveBeenCalledWith('job-7', 'Weather');
  });

  it('listAssignees and listCustomers reach their own port methods', async () => {
    const assignees = jest.fn(async () => ok([]));
    const customers = jest.fn(async () => ok([]));
    const port = stubPort({ assignees, customers });

    await listAssignees(port);
    await listCustomers(port);

    expect(assignees).toHaveBeenCalledTimes(1);
    expect(customers).toHaveBeenCalledTimes(1);
  });
});
