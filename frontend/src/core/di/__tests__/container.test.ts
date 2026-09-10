/**
 * @jest-environment node
 */

import { getContainer, resetContainer } from '../container';

describe('the container', () => {
  const original = process.env.JOBTRACKER_API_URL;

  afterEach(() => {
    if (original === undefined) {
      delete process.env.JOBTRACKER_API_URL;
    } else {
      process.env.JOBTRACKER_API_URL = original;
    }
    resetContainer();
  });

  it('uses the in-memory adapter when no API url is configured', () => {
    delete process.env.JOBTRACKER_API_URL;
    resetContainer();

    return expect(getContainer().jobs.search({ limit: 1 })).resolves.toMatchObject({ ok: true });
  });

  it('uses the HTTP adapter when an API url is configured', async () => {
    process.env.JOBTRACKER_API_URL = 'http://127.0.0.1:1';
    resetContainer();

    const result = await getContainer().jobs.search({ limit: 1 });

    expect(result.ok).toBe(false);
  });

  it('reads the API url on every build rather than once at import', async () => {
    delete process.env.JOBTRACKER_API_URL;
    resetContainer();
    expect((await getContainer().jobs.search({ limit: 1 })).ok).toBe(true);

    process.env.JOBTRACKER_API_URL = 'http://127.0.0.1:1';
    resetContainer();
    expect((await getContainer().jobs.search({ limit: 1 })).ok).toBe(false);
  });

  it('returns the same container across calls', () => {
    resetContainer();

    expect(getContainer()).toBe(getContainer());
  });
});
