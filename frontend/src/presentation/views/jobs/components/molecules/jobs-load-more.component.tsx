import { Button } from '@/presentation/components/atoms/button.component';

export function JobsLoadMore({
  isLoading,
  onLoadMore,
}: {
  readonly isLoading: boolean;
  readonly onLoadMore: () => void;
}) {
  return (
    <div className="mt-4 flex justify-center">
      <Button
        testId="jobs-load-more"
        variant="secondary"
        pending={isLoading}
        onClick={onLoadMore}
      >
        Load more
      </Button>
    </div>
  );
}
