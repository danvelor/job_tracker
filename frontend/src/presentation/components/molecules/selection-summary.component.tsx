import { useMemo } from 'react';

export function SelectionSummary({
  loaded,
  selected,
}: {
  readonly loaded: number;
  readonly selected: number;
}) {
  const text = useMemo(
    () =>
      selected === 0
        ? `Showing ${loaded} jobs`
        : `Showing ${loaded} jobs · ${selected} selected`,
    [loaded, selected],
  );

  return (
    <p data-testid="jobs-selection-summary" className="mb-2 text-xs text-slate-600">
      {text}
    </p>
  );
}
