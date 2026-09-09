export type DateInputProps = {
  readonly testId: string;
  readonly value: string;
  readonly onChange: (value: string) => void;
  readonly id?: string;
  readonly disabled?: boolean;
};

export function DateInput({ testId, value, onChange, id, disabled = false }: DateInputProps) {
  return (
    <input
      type="date"
      data-testid={testId}
      id={id}
      value={value}
      disabled={disabled}
      onChange={(event) => onChange(event.target.value)}
      className="rounded border border-slate-300 px-2 py-1.5 text-sm"
    />
  );
}
