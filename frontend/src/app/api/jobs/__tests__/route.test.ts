/**
 * @jest-environment node
 */

import { resetContainer } from '@/core/di/container';
import { GET } from '../route';

describe('GET /api/jobs', () => {
  afterEach(() => resetContainer());

  it('returns the seeded page', async () => {
    const response = await GET(new Request('http://localhost/api/jobs'));
    const body = (await response.json()) as { items: unknown[] };

    expect(response.status).toBe(200);
    expect(Array.isArray(body.items)).toBe(true);
  });

  it('passes the status filter through to the use case', async () => {
    const response = await GET(new Request('http://localhost/api/jobs?statuses=Completed'));
    const body = (await response.json()) as { items: { status: string }[] };

    expect(body.items.length).toBeGreaterThan(0);
    expect(body.items.every((job) => job.status === 'Completed')).toBe(true);
  });

  it('accepts repeated status parameters', async () => {
    const response = await GET(
      new Request('http://localhost/api/jobs?statuses=Completed&statuses=Cancelled'),
    );
    const body = (await response.json()) as { items: { status: string }[] };

    expect(new Set(body.items.map((job) => job.status))).toEqual(
      new Set(['Completed', 'Cancelled']),
    );
  });

  it('ignores a status that is not a member of the union', async () => {
    const response = await GET(new Request('http://localhost/api/jobs?statuses=Nonsense'));
    const body = (await response.json()) as { items: unknown[] };

    expect(body.items.length).toBeGreaterThan(0);
  });

  it('passes free text through', async () => {
    const response = await GET(new Request('http://localhost/api/jobs?text=gutter'));
    const body = (await response.json()) as { items: { title: string }[] };

    expect(body.items.map((job) => job.title)).toEqual(['Gutter reline']);
  });

  it('rejects a non-numeric limit with a problem response', async () => {
    const response = await GET(new Request('http://localhost/api/jobs?limit=nonsense'));

    expect(response.status).toBe(400);
    expect(response.headers.get('content-type')).toContain('application/problem+json');
  });
});
