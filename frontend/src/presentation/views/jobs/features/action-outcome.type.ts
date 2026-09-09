import type { CoreError } from '@/core/domain/result.type';

/**
 * What every mutation Server Action in this view returns.
 *
 * Deliberately a plain object rather than the core `Result`: a Server Action's
 * return value crosses a serialisation boundary, so it may carry only data.
 * `CoreError` already qualifies — code, message, kind and an optional record of
 * field errors, no methods.
 *
 * It sits beside the slices rather than inside one, because all four return it
 * and no slice may import another.
 */
export type ActionOutcome =
  | { readonly ok: true }
  | { readonly ok: false; readonly error: CoreError };

export type CreateOutcome =
  | { readonly ok: true; readonly id: string }
  | { readonly ok: false; readonly error: CoreError };
