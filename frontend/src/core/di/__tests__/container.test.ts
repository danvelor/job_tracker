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

    // D-01 keeps in-memory the default in development and CI, which is what
    // lets the end-to-end suite run with neither backend nor database. The
    // switch is a URL being present, not a flag someone has to remember.
    return expect(getContainer().jobs.search({ limit: 1 })).resolves.toMatchObject({ ok: true });
  });

  it('uses the HTTP adapter when an API url is configured', async () => {
    // Pointed at a port nothing is listening on: an in-memory adapter would
    // answer happily, and the HTTP one cannot reach anything. The failure is
    // the evidence of which adapter was built.
    process.env.JOBTRACKER_API_URL = 'http://127.0.0.1:1';
    resetContainer();

    const result = await getContainer().jobs.search({ limit: 1 });

    expect(result.ok).toBe(false);
  });

  it('reads the API url on every build rather than once at import', async () => {
    // Reading it at module load would bake the CI value into the bundle, and
    // the Compose stack would silently keep serving the seeded array.
    delete process.env.JOBTRACKER_API_URL;
    resetContainer();
    expect((await getContainer().jobs.search({ limit: 1 })).ok).toBe(true);

    process.env.JOBTRACKER_API_URL = 'http://127.0.0.1:1';
    resetContainer();
    expect((await getContainer().jobs.search({ limit: 1 })).ok).toBe(false);
  });

  it('returns the same container across calls', () => {
    resetContainer();

    // The in-memory adapter holds state, so a container rebuilt per call would
    // lose every job the moment a second entry point asked for one.
    expect(getContainer()).toBe(getContainer());
  });
});
