/**
 * The failure vocabulary. It mirrors the backend's error types so both sides
 * speak the same language: `HttpJobsAdapter` maps `ProblemDetails` onto these
 * kinds, and `InMemoryJobsAdapter` produces the same ones for the same
 * refusals — which is what makes the two substitutable.
 */
export type CoreErrorKind =
  | 'validation'
  | 'not-found'
  | 'conflict'
  | 'unauthorized'
  | 'failure';

export type CoreError = {
  readonly code: string;
  readonly message: string;
  readonly kind: CoreErrorKind;
  readonly fieldErrors?: Readonly<Record<string, string>>;
};

/**
 * Success or failure as a value. Expected failures are returned, never thrown;
 * exceptions are reserved for defects, exactly as on the backend.
 *
 * The constructors and the guard live in `result.ts`. A `*.type.ts` module
 * carries no runtime code, which is what lets coverage exclude the suffix
 * without excluding anything a test could exercise.
 */
export type Result<T, E = CoreError> =
  | { readonly ok: true; readonly value: T }
  | { readonly ok: false; readonly error: E };
