import { SkeletonBlock } from '@/presentation/components/atoms/skeleton-block.component';

const ROWS = [0, 1, 2, 3, 4];

/** The Suspense fallback. Five rows, matching design A4. */
export function JobsTableSkeleton() {
  return (
    <div data-testid="jobs-table-skeleton" className="space-y-2 p-3">
      {ROWS.map((row) => (
        <SkeletonBlock key={row} className="h-8 w-full" />
      ))}
    </div>
  );
}
