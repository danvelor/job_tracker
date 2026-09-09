export function SkeletonBlock({
  testId,
  className = 'h-4 w-full',
}: {
  readonly testId?: string;
  readonly className?: string;
}) {
  return (
    <span
      data-testid={testId}
      aria-hidden="true"
      className={`block animate-pulse rounded bg-slate-200 ${className}`}
    />
  );
}
