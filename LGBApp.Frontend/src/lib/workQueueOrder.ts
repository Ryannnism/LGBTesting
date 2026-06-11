export type WorkQueueScope = 'attention' | 'tracker' | 'job-requests';

function storageKey(userId: number, scope: WorkQueueScope): string {
  return `lgb-work-queue:${userId}:${scope}`;
}

export function loadWorkQueueOrder(userId: number, scope: WorkQueueScope): string[] {
  try {
    const raw = localStorage.getItem(storageKey(userId, scope));
    if (!raw) return [];
    const parsed = JSON.parse(raw) as unknown;
    return Array.isArray(parsed) ? parsed.filter((k): k is string => typeof k === 'string') : [];
  } catch {
    return [];
  }
}

export function saveWorkQueueOrder(userId: number, scope: WorkQueueScope, order: string[]): void {
  localStorage.setItem(storageKey(userId, scope), JSON.stringify(order));
}

export function clearWorkQueueOrder(userId: number, scope: WorkQueueScope): void {
  localStorage.removeItem(storageKey(userId, scope));
}

/** Parse yyyy-MM-dd (or ISO) for sorting; missing dates sort last. */
export function parseQueueSortDate(...dates: (string | undefined | null)[]): number {
  for (const d of dates) {
    if (!d?.trim()) continue;
    const t = Date.parse(d);
    if (!Number.isNaN(t)) return t;
  }
  return Number.MAX_SAFE_INTEGER;
}

export function formatQueueDate(...dates: (string | undefined | null)[]): string {
  for (const d of dates) {
    if (d?.trim()) return d;
  }
  return '—';
}

export function applyWorkQueueOrder<T>(
  items: T[],
  getKey: (item: T) => string,
  getSortDate: (item: T) => number,
  savedOrder: string[],
): T[] {
  if (savedOrder.length === 0) {
    return [...items].sort((a, b) => getSortDate(a) - getSortDate(b));
  }

  const byKey = new Map(items.map((item) => [getKey(item), item]));
  const result: T[] = [];
  const used = new Set<string>();

  for (const key of savedOrder) {
    const item = byKey.get(key);
    if (item) {
      result.push(item);
      used.add(key);
    }
  }

  const remaining = items
    .filter((item) => !used.has(getKey(item)))
    .sort((a, b) => getSortDate(a) - getSortDate(b));

  return [...result, ...remaining];
}

export function reorderWorkQueueKeys(order: string[], fromKey: string, toKey: string): string[] {
  if (fromKey === toKey) return order;
  const next = order.filter((k) => k !== fromKey);
  const toIndex = next.indexOf(toKey);
  if (toIndex < 0) return [...next, fromKey];
  next.splice(toIndex, 0, fromKey);
  return next;
}

export function buildOrderFromKeys(keys: string[], fromKey: string, toKey: string): string[] {
  const merged = keys.includes(fromKey) ? keys : [...keys, fromKey];
  const base = merged.filter((k) => k !== fromKey);
  const toIndex = base.indexOf(toKey);
  if (toIndex < 0) return [...base, fromKey];
  base.splice(toIndex, 0, fromKey);
  return base;
}
