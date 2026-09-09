export type SelectOption = { readonly value: string; readonly label: string };

export type SelectProps = {
  readonly testId: string;
  readonly value: string;
  readonly onChange: (value: string) => void;
  readonly options: readonly SelectOption[];
  readonly id?: string;
  readonly placeholder?: string;
  readonly disabled?: boolean;
};

export function Select({
  testId,
  value,
  onChange,
  options,
  id,
  placeholder,
  disabled = false,
}: SelectProps) {
  return (
    <select
      data-testid={testId}
      id={id}
      value={value}
      disabled={disabled}
      onChange={(event) => onChange(event.target.value)}
      className="rounded border border-slate-300 px-2 py-1.5 text-sm"
    >
      {placeholder === undefined ? null : <option value="">{placeholder}</option>}
      {options.map((option) => (
        <option key={option.value} value={option.value}>
          {option.label}
        </option>
      ))}
    </select>
  );
}
