import { FieldError } from './field-error.component';

export type TextInputProps = {
  readonly testId: string;
  readonly value: string;
  readonly onChange: (value: string) => void;
  readonly id?: string;
  readonly name?: string;
  readonly placeholder?: string;
  readonly error?: string;
  readonly disabled?: boolean;
  readonly onBlur?: () => void;
};

/**
 * The controlled contract of design A7: `value` in, `onChange` out carrying
 * the value rather than the DOM event, and `error` displayed rather than
 * decided. The component holds nothing.
 */
export function TextInput({
  testId,
  value,
  onChange,
  id,
  name,
  placeholder,
  error,
  disabled = false,
  onBlur,
}: TextInputProps) {
  const errorId = id === undefined ? undefined : `${id}-error`;

  return (
    <>
      <input
        data-testid={testId}
        id={id}
        name={name}
        value={value}
        placeholder={placeholder}
        disabled={disabled}
        onBlur={onBlur}
        onChange={(event) => onChange(event.target.value)}
        aria-invalid={error !== undefined}
        aria-describedby={error !== undefined ? errorId : undefined}
        className="w-full rounded border border-slate-300 px-2 py-1.5 text-sm"
      />
      {error !== undefined ? (
        <FieldError testId={`${testId}-error`} id={errorId} message={error} />
      ) : null}
    </>
  );
}
