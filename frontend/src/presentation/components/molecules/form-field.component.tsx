import type { ReactNode } from 'react';
import { FieldError } from '../atoms/field-error.component';
import { Label } from '../atoms/label.component';

export function FormField({
  id,
  label,
  error,
  children,
}: {
  readonly id: string;
  readonly label: string;
  readonly error?: string;
  readonly children: ReactNode;
}) {
  return (
    <div className="mb-3">
      <Label htmlFor={id}>{label}</Label>
      {children}
      {error !== undefined ? (
        <FieldError testId={`field-error-${id}`} id={`${id}-error`} message={error} />
      ) : null}
    </div>
  );
}
