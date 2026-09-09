import type { CoreError, CoreErrorKind } from '@/core/domain/result.type';

/**
 * The mirror of the backend's `ResultExtensions`. These two files are the error
 * contract, and they are why `InMemoryJobsAdapter` and `HttpJobsAdapter` are
 * substitutable: the same refusal arrives as the same kind on both sides.
 */
const KIND_BY_STATUS: Readonly<Record<number, CoreErrorKind>> = {
  400: 'validation',
  401: 'unauthorized',
  403: 'unauthorized',
  404: 'not-found',
  409: 'conflict',
};

type ProblemDocument = {
  readonly detail?: unknown;
  readonly title?: unknown;
  readonly errorCode?: unknown;
  readonly errors?: unknown;
};

const asString = (value: unknown, fallback: string): string =>
  typeof value === 'string' && value !== '' ? value : fallback;

/**
 * `errors` arrives as `{ field: string[] }` and the form wants one message per
 * field. The first is the one to show: FluentValidation orders rules as they
 * were declared, so the first is the most basic thing wrong with the value.
 */
function toFieldErrors(errors: unknown): Readonly<Record<string, string>> | undefined {
  if (typeof errors !== 'object' || errors === null) {
    return undefined;
  }

  const entries = Object.entries(errors as Record<string, unknown>)
    .map(([field, messages]): readonly [string, string] | null =>
      Array.isArray(messages) && typeof messages[0] === 'string'
        ? ([field, messages[0]] as const)
        : null,
    )
    .filter((entry): entry is readonly [string, string] => entry !== null);

  return entries.length === 0 ? undefined : Object.fromEntries(entries);
}

export async function toCoreError(response: Response): Promise<CoreError> {
  const kind = KIND_BY_STATUS[response.status] ?? 'failure';

  // A proxy answering 502 with an HTML page is a real Tuesday, so parsing is
  // allowed to fail without turning into an exception of its own.
  const document = await response
    .json()
    .then((body: unknown): ProblemDocument => (typeof body === 'object' && body !== null ? body : {}))
    .catch((): ProblemDocument => ({}));

  return {
    code: asString(document.errorCode, `http.${response.status}`),
    message: asString(document.detail, asString(document.title, response.statusText)),
    kind,
    ...(toFieldErrors(document.errors) === undefined
      ? {}
      : { fieldErrors: toFieldErrors(document.errors) }),
  };
}

/** A request that never reached the server: no status, no document, still a Result. */
export function toTransportError(cause: unknown): CoreError {
  return {
    code: 'http.unreachable',
    message: cause instanceof Error ? cause.message : 'The API could not be reached',
    kind: 'failure',
  };
}
