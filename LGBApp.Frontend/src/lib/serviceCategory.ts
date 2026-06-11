/** Mirrors PackageServiceCategoryResolver on the backend. */
export const ALL_SERVICES = 'All services';

export const SERVICE_CATEGORY_ORDER = [
  ALL_SERVICES,
  'Board meetings',
  'Resolutions',
  'Annual compliance',
  'Secretarial & audit',
  'Support services',
  'Lodgement fees',
  'Other services',
] as const;

export type ServiceCategory = (typeof SERVICE_CATEGORY_ORDER)[number];

export function resolveServiceCategory(serviceName: string): ServiceCategory {
  const s = (serviceName ?? '').trim();
  if (!s) return 'Other services';

  const lower = s.toLowerCase();
  if (lower.includes('board meeting')) return 'Board meetings';
  if (lower.includes('resolution') || lower.includes('reso')) return 'Resolutions';
  if (
    lower.includes('annual')
    || lower.includes('mbrs')
    || lower.includes('bo declaration')
    || lower.includes('audited')
  ) {
    return 'Annual compliance';
  }
  if (lower.includes('support')) return 'Support services';
  if (
    lower.includes('secretarial')
    || lower.includes('register office')
    || lower.includes('auditor')
  ) {
    return 'Secretarial & audit';
  }
  if (lower.includes('lodgement')) return 'Lodgement fees';
  return 'Other services';
}
