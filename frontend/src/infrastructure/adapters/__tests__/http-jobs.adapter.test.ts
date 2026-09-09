/**
 * @jest-environment node
 */
import { createServer, type IncomingMessage, type Server, type ServerResponse } from 'node:http';
import type { AddressInfo } from 'node:net';
import { createHttpJobsAdapter } from '../http-jobs.adapter';

type Recorded = {
  readonly method: string;
  readonly url: string;
  readonly authorization: string | undefined;
  readonly body: string;
};

type Route = (request: IncomingMessage, response: ServerResponse) => void;

const json = (response: ServerResponse, status: number, body: unknown): void => {
  response.writeHead(status, { 'content-type': 'application/json' });
  response.end(JSON.stringify(body));
};

const problem = (
  response: ServerResponse,
  status: number,
  errorCode: string,
  extra: Record<string, unknown> = {},
): void => {
  response.writeHead(status, { 'content-type': 'application/problem+json' });
  response.end(JSON.stringify({ title: 'Refused', status, detail: 'why', errorCode, ...extra }));
};

/**
 * A real server on an ephemeral port, not a mocked fetch.
 *
 * A mock would assert on the mock. This asserts on what crossed the wire, and
 * it catches what a mock never produces: a wrong path, a missing header, a body
 * serialised the wrong way, and — in one test below — a refused connection.
 */
async function serving(route: Route): Promise<{
  origin: string;
  seen: Recorded[];
  close: () => Promise<void>;
}> {
  const seen: Recorded[] = [];

  const server: Server = createServer((request, response) => {
    const chunks: Buffer[] = [];
    request.on('data', (chunk: Buffer) => chunks.push(chunk));
    request.on('end', () => {
      seen.push({
        method: request.method ?? '',
        url: request.url ?? '',
        authorization: request.headers.authorization,
        body: Buffer.concat(chunks).toString(),
      });
      route(request, response);
    });
  });

  await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve));
  const { port } = server.address() as AddressInfo;

  return {
    origin: `http://127.0.0.1:${port}`,
    seen,
    close: () => new Promise<void>((resolve) => server.close(() => resolve())),
  };
}

/** Answers the token endpoint, then delegates everything else. */
const withToken = (route: Route): Route => (request, response) => {
  if (request.url === '/auth/dev-token') {
    json(response, 200, { token: 'a-token' });
    return;
  }
  route(request, response);
};

const A_JOB = {
  id: '11111111-1111-1111-1111-111111111111',
  title: 'Ridge tile replacement',
  status: 'Scheduled',
  scheduledDate: '2099-03-14',
  assigneeId: '22222222-2222-2222-2222-222222222222',
  assigneeName: 'J. Ortiz',
  street: '12 Elm St',
  city: 'Springfield',
  state: 'IL',
  photoCount: 2,
};

describe('HttpJobsAdapter', () => {
  it('sends a bearer token on every request', async () => {
    const server = await serving(withToken((_, response) =>
      json(response, 200, { items: [A_JOB], nextCursor: null }),
    ));

    const adapter = createHttpJobsAdapter({ baseUrl: server.origin });
    await adapter.search({ limit: 10 });
    await server.close();

    // Architecture 7.1: the token is attached here, server-side, and never
    // reaches the browser. Without it the user sees an empty list, not a 401.
    const search = server.seen.find((request) => request.url.startsWith('/api/jobs'));
    expect(search?.authorization).toBe('Bearer a-token');
  });

  it('asks for a token once and reuses it', async () => {
    const server = await serving(withToken((_, response) =>
      json(response, 200, { items: [], nextCursor: null }),
    ));

    const adapter = createHttpJobsAdapter({ baseUrl: server.origin });
    await adapter.search({ limit: 10 });
    await adapter.search({ limit: 10 });
    await server.close();

    // Otherwise every page load costs an extra round trip and the token
    // endpoint becomes the hot path.
    expect(server.seen.filter((request) => request.url === '/auth/dev-token')).toHaveLength(1);
  });

  it('nests the flat address the API returns', async () => {
    const server = await serving(withToken((_, response) =>
      json(response, 200, { items: [A_JOB], nextCursor: null }),
    ));

    const adapter = createHttpJobsAdapter({ baseUrl: server.origin });
    const result = await adapter.search({ limit: 10 });
    await server.close();

    // JobResponse is flat; JobSummary nests. Mapping here is what stops the
    // wire shape leaking into components.
    expect(result.ok && result.value.items[0].address).toEqual({
      street: '12 Elm St',
      city: 'Springfield',
      state: 'IL',
    });
  });

  it('keeps a missing scheduled date null rather than inventing one', async () => {
    const server = await serving(withToken((_, response) =>
      json(response, 200, { items: [{ ...A_JOB, scheduledDate: null }], nextCursor: null }),
    ));

    const adapter = createHttpJobsAdapter({ baseUrl: server.origin });
    const result = await adapter.search({ limit: 10 });
    await server.close();

    expect(result.ok && result.value.items[0].scheduledDate).toBeNull();
  });

  it('labels a job with no assignee rather than showing an empty column', async () => {
    const server = await serving(withToken((_, response) =>
      json(response, 200, {
        items: [{ ...A_JOB, assigneeId: null, assigneeName: null }],
        nextCursor: null,
      }),
    ));

    const adapter = createHttpJobsAdapter({ baseUrl: server.origin });
    const result = await adapter.search({ limit: 10 });
    await server.close();

    // The same word the in-memory adapter uses, because the two must be
    // substitutable down to what the user reads.
    expect(result.ok && result.value.items[0].assigneeName).toBe('Unassigned');
  });

  it('puts every filter on the query string', async () => {
    const server = await serving(withToken((_, response) =>
      json(response, 200, { items: [], nextCursor: null }),
    ));

    const adapter = createHttpJobsAdapter({ baseUrl: server.origin });
    await adapter.search({
      limit: 5,
      text: 'ridge',
      statuses: ['Scheduled', 'Completed'],
      assigneeId: 'a-1',
      cursor: 'c-1',
    });
    await server.close();

    const url = server.seen.find((request) => request.url.startsWith('/api/jobs'))!.url;
    expect(url).toContain('text=ridge');
    expect(url).toContain('statuses=Scheduled');
    expect(url).toContain('statuses=Completed');
    expect(url).toContain('cursor=c-1');
    expect(url).toContain('limit=5');
  });

  it('omits a filter that was not set rather than sending an empty one', async () => {
    const server = await serving(withToken((_, response) =>
      json(response, 200, { items: [], nextCursor: null }),
    ));

    const adapter = createHttpJobsAdapter({ baseUrl: server.origin });
    await adapter.search({ limit: 5 });
    await server.close();

    // `?text=` is not the same request as no text at all, and the API would
    // be within its rights to treat it as a search for the empty string.
    const url = server.seen.find((request) => request.url.startsWith('/api/jobs'))!.url;
    expect(url).not.toContain('text=');
    expect(url).not.toContain('cursor=');
  });

  it('returns the created identifier from the response body', async () => {
    const server = await serving(withToken((_, response) =>
      json(response, 201, { id: '33333333-3333-3333-3333-333333333333' }),
    ));

    const adapter = createHttpJobsAdapter({ baseUrl: server.origin });
    const result = await adapter.create({
      title: 'New',
      description: '',
      address: { street: 's', city: 'c', state: 'st', zipCode: 'z', latitude: 1, longitude: 2 },
      scheduledDate: '2099-01-01',
      assigneeId: 'a',
      customerId: 'c',
    });
    await server.close();

    expect(result).toEqual({ ok: true, value: '33333333-3333-3333-3333-333333333333' });
  });

  it('flattens the nested address the port takes into the body the API expects', async () => {
    const server = await serving(withToken((_, response) => json(response, 201, { id: 'x' })));

    const adapter = createHttpJobsAdapter({ baseUrl: server.origin });
    await adapter.create({
      title: 'New',
      description: '',
      address: { street: '12 Elm St', city: 'Springfield', state: 'IL', zipCode: '62701', latitude: 39.78, longitude: -89.65 },
      scheduledDate: '2099-01-01',
      assigneeId: 'a',
      customerId: 'c',
    });
    await server.close();

    const body = JSON.parse(server.seen.find((r) => r.method === 'POST' && r.url === '/api/jobs')!.body);
    expect(body.street).toBe('12 Elm St');
    expect(body.address).toBeUndefined();
  });

  // ---- the error contract ------------------------------------------------

  it('maps a 409 onto a conflict', async () => {
    const server = await serving(withToken((_, response) =>
      problem(response, 409, 'job.terminal'),
    ));

    const adapter = createHttpJobsAdapter({ baseUrl: server.origin });
    const result = await adapter.start('an-id');
    await server.close();

    // The agreement that makes the two adapters substitutable: the same
    // refusal produces the same kind on both sides (jobs.port.ts).
    expect(result.ok).toBe(false);
    expect(!result.ok && result.error.kind).toBe('conflict');
    expect(!result.ok && result.error.code).toBe('job.terminal');
  });

  it('maps a 404 onto not-found', async () => {
    const server = await serving(withToken((_, response) =>
      problem(response, 404, 'job.not-found'),
    ));

    const adapter = createHttpJobsAdapter({ baseUrl: server.origin });
    const result = await adapter.start('an-id');
    await server.close();

    expect(!result.ok && result.error.kind).toBe('not-found');
  });

  it('carries the field errors from a 400 so the form can highlight them', async () => {
    const server = await serving(withToken((_, response) =>
      problem(response, 400, 'job.scheduled-in-the-past', {
        errors: { ScheduledDate: ['A job cannot be scheduled in the past'] },
      }),
    ));

    const adapter = createHttpJobsAdapter({ baseUrl: server.origin });
    const result = await adapter.cancel('an-id', '');
    await server.close();

    expect(!result.ok && result.error.kind).toBe('validation');
    expect(!result.ok && result.error.fieldErrors?.ScheduledDate)
      .toBe('A job cannot be scheduled in the past');
  });

  it('turns a 500 into a failure rather than throwing', async () => {
    const server = await serving(withToken((_, response) => {
      response.writeHead(500);
      response.end('the server fell over');
    }));

    const adapter = createHttpJobsAdapter({ baseUrl: server.origin });
    const result = await adapter.start('an-id');
    await server.close();

    // A thrown error escapes the Result discipline and lands in an error
    // boundary, losing the code the user would quote in a report. The body is
    // not even JSON here, which is what a proxy returns on a bad day.
    expect(!result.ok && result.error.kind).toBe('failure');
  });

  it('turns a refused connection into a failure rather than throwing', async () => {
    const server = await serving(withToken((_, response) => json(response, 200, {})));
    const origin = server.origin;
    await server.close();

    const adapter = createHttpJobsAdapter({ baseUrl: origin });
    const result = await adapter.search({ limit: 10 });

    // The case a mocked fetch never produces and production produces on its
    // first deploy.
    expect(result.ok).toBe(false);
    expect(!result.ok && result.error.kind).toBe('failure');
  });

  it('surfaces a 401 as unauthorized rather than as an empty list', async () => {
    const server = await serving(withToken((_, response) =>
      problem(response, 401, 'auth.required'),
    ));

    const adapter = createHttpJobsAdapter({ baseUrl: server.origin });
    const result = await adapter.search({ limit: 10 });
    await server.close();

    expect(!result.ok && result.error.kind).toBe('unauthorized');
  });

  it('reports a failure when the token endpoint itself is unreachable', async () => {
    const server = await serving((_, response) => {
      response.writeHead(500);
      response.end();
    });

    const adapter = createHttpJobsAdapter({ baseUrl: server.origin });
    const result = await adapter.search({ limit: 10 });
    await server.close();

    // Without this the adapter would send `Bearer undefined` and the user
    // would see an authorization error for an availability problem.
    expect(!result.ok && result.error.kind).toBe('failure');
  });

  it('reads the rosters the pickers need', async () => {
    const server = await serving(withToken((_, response) =>
      json(response, 200, [{ id: 'a-1', name: 'J. Ortiz' }]),
    ));

    const adapter = createHttpJobsAdapter({ baseUrl: server.origin });
    const assignees = await adapter.assignees();
    await server.close();

    expect(assignees).toEqual({ ok: true, value: [{ id: 'a-1', name: 'J. Ortiz' }] });
  });
});
