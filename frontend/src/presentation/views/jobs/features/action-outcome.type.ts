import type { CoreError } from '@/core/domain/result.type';

export type ActionOutcome =
  | { readonly ok: true }
  | { readonly ok: false; readonly error: CoreError };

export type CreateOutcome =
  | { readonly ok: true; readonly id: string }
  | { readonly ok: false; readonly error: CoreError };
