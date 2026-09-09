import { useMemo } from 'react';

/**
 * The count is of loaded rows, not of matches (design A2): a total would need
 * a second aggregate query per keystroke, which buys a number nobody acts on.
 *
 * useMemo here is line 157's "computing totals": without it the text is
 * rebuilt on every keystroke of a debounced search field.
 */
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
