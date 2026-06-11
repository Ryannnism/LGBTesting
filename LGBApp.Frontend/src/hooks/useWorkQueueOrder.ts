import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  applyWorkQueueOrder,
  buildOrderFromKeys,
  clearWorkQueueOrder,
  loadWorkQueueOrder,
  saveWorkQueueOrder,
  type WorkQueueScope,
} from '@/lib/workQueueOrder';

export function useWorkQueueOrder<T>(
  userId: number | undefined,
  scope: WorkQueueScope,
  items: T[],
  getKey: (item: T) => string,
  getSortDate: (item: T) => number,
) {
  const [savedOrder, setSavedOrder] = useState<string[]>(() =>
    userId ? loadWorkQueueOrder(userId, scope) : [],
  );

  useEffect(() => {
    setSavedOrder(userId ? loadWorkQueueOrder(userId, scope) : []);
  }, [userId, scope]);

  const sortedItems = useMemo(
    () => applyWorkQueueOrder(items, getKey, getSortDate, savedOrder),
    [items, getKey, getSortDate, savedOrder],
  );

  const moveItem = useCallback((fromKey: string, toKey: string) => {
    if (!userId || fromKey === toKey) return;
    const currentKeys = sortedItems.map(getKey);
    const next = buildOrderFromKeys(
      savedOrder.length > 0 ? savedOrder : currentKeys,
      fromKey,
      toKey,
    );
    setSavedOrder(next);
    saveWorkQueueOrder(userId, scope, next);
  }, [userId, scope, sortedItems, getKey, savedOrder]);

  const resetOrder = useCallback(() => {
    if (!userId) return;
    clearWorkQueueOrder(userId, scope);
    setSavedOrder([]);
  }, [userId, scope]);

  const hasCustomOrder = savedOrder.length > 0;

  return { sortedItems, moveItem, resetOrder, hasCustomOrder };
}
