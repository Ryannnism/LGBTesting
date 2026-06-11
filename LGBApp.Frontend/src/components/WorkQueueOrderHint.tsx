interface WorkQueueOrderHintProps {
  hasCustomOrder: boolean;
  onReset: () => void;
}

export function WorkQueueOrderHint({ hasCustomOrder, onReset }: WorkQueueOrderHintProps) {
  return (
    <p className="text-xs text-muted-foreground">
      Default order: earliest date first.
      {hasCustomOrder && (
        <>
          {' '}
          <button type="button" onClick={onReset} className="text-primary hover:underline">
            Reset to date order
          </button>
        </>
      )}
      {' · '}
      Drag rows to reprioritize.
    </p>
  );
}
