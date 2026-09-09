export function FieldError({
  testId,
  id,
  message,
}: {
  readonly testId: string;
  readonly id?: string;
  readonly message: string;
}) {
  return (
    <p data-testid={testId} id={id} role="alert" className="mt-1 text-xs text-red-700">
      {message}
    </p>
  );
}
