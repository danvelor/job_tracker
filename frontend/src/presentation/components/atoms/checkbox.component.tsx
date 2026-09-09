export type CheckboxProps = {
  readonly testId: string;
  readonly checked: boolean;
  readonly onChange: (checked: boolean) => void;
  readonly label: string;
  readonly disabled?: boolean;
};

export function Checkbox({ testId, checked, onChange, label, disabled = false }: CheckboxProps) {
  return (
    <input
      type="checkbox"
      data-testid={testId}
      checked={checked}
      disabled={disabled}
      aria-label={label}
      onChange={(event) => onChange(event.target.checked)}
      className="h-4 w-4 rounded border-slate-300"
    />
  );
}
