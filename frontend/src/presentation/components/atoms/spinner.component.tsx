export function Spinner({ testId }: { readonly testId: string }) {
  return (
    <span
      data-testid={testId}
      role="status"
      aria-label="Loading"
      className="inline-block h-3 w-3 animate-spin rounded-full border-2 border-current border-t-transparent"
    />
  );
}
