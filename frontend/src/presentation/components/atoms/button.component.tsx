import type { ReactNode } from 'react';
import { Spinner } from './spinner.component';

export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger';

const STYLES: Record<ButtonVariant, string> = {
  primary: 'bg-slate-900 text-white hover:bg-slate-700',
  secondary: 'border border-slate-300 text-slate-900 hover:bg-slate-100',
  ghost: 'text-slate-700 hover:bg-slate-100',
  danger: 'border border-red-300 text-red-700 hover:bg-red-50',
};

export type ButtonProps = {
  readonly testId: string;
  readonly children: ReactNode;
  readonly onClick: () => void;
  readonly variant?: ButtonVariant;
  readonly pending?: boolean;
  readonly disabled?: boolean;
  readonly type?: 'button' | 'submit';
};

/** Owns its pending state visually, never logically (design A3). */
export function Button({
  testId,
  children,
  onClick,
  variant = 'primary',
  pending = false,
  disabled = false,
  type = 'button',
}: ButtonProps) {
  return (
    <button
      data-testid={testId}
      type={type}
      onClick={onClick}
      disabled={disabled || pending}
      aria-busy={pending}
      className={`inline-flex items-center gap-2 rounded px-3 py-1.5 text-sm disabled:opacity-50 ${STYLES[variant]}`}
    >
      {pending ? <Spinner testId={`${testId}-spinner`} /> : null}
      {children}
    </button>
  );
}
