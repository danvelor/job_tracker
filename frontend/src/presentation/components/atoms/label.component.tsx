export function Label({
  htmlFor,
  children,
}: {
  readonly htmlFor: string;
  readonly children: string;
}) {
  return (
    <label htmlFor={htmlFor} className="mb-1 block text-xs font-medium text-slate-700">
      {children}
    </label>
  );
}
