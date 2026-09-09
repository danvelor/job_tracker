import 'server-only';

/**
 * Architecture 7.1: the Next server obtains a development token and attaches it
 * server-side. It never reaches the browser, which is why this module is
 * `server-only` — importing it from a client component is a build error rather
 * than a leak nobody notices.
 *
 * A real deployment replaces this one function with an identity provider, and
 * nothing else in the design moves.
 */
export type TokenSource = () => Promise<string>;

export function createDevTokenSource(baseUrl: string, organizationId: string): TokenSource {
  // Cached as a promise rather than a string, so two concurrent requests on a
  // cold server share one round trip instead of racing for two.
  let pending: Promise<string> | null = null;

  const fetchToken = async (): Promise<string> => {
    const response = await fetch(`${baseUrl}/auth/dev-token`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ organizationId }),
      cache: 'no-store',
    });

    if (!response.ok) {
      // Cleared so the next request tries again. A cached rejection would
      // turn one bad moment into a permanently broken server.
      pending = null;
      throw new Error(`The token endpoint answered ${response.status}`);
    }

    const body: unknown = await response.json();

    if (typeof body !== 'object' || body === null || typeof (body as { token?: unknown }).token !== 'string') {
      pending = null;
      throw new Error('The token endpoint returned no token');
    }

    return (body as { token: string }).token;
  };

  return () => {
    pending ??= fetchToken().catch((cause: unknown) => {
      pending = null;
      throw cause;
    });
    return pending;
  };
}
