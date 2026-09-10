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

export type Result<T, E = CoreError> =
  | { readonly ok: true; readonly value: T }
  | { readonly ok: false; readonly error: E };
