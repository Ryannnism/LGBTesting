import { GripVertical } from 'lucide-react';

interface WorkQueueDragHandleProps {
  label?: string;
}

export function WorkQueueDragHandle({ label = 'Drag to reprioritize' }: WorkQueueDragHandleProps) {
  return (
    <span
      className="inline-flex items-center text-muted-foreground cursor-grab active:cursor-grabbing"
      title={label}
      aria-hidden
    >
      <GripVertical className="w-4 h-4" />
    </span>
  );
}
