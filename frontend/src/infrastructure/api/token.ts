import 'server-only';

export type TokenSource = (options?: { readonly refresh?: boolean }) => Promise<string>;

export function createDevTokenSource(baseUrl: string, organizationId: string): TokenSource {
  let pending: Promise<string> | null = null;

  const fetchToken = async (): Promise<string> => {
    const response = await fetch(`${baseUrl}/auth/dev-token`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ organizationId }),
      cache: 'no-store',
    });

    if (!response.ok) {
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

  return (options) => {
    if (options?.refresh === true) {
      pending = null;
    }

    pending ??= fetchToken().catch((cause: unknown) => {
      pending = null;
      throw cause;
    });
    return pending;
  };
}
