import type { JobRequestResponse, JobRequestUnitDto } from '@/lib/api';

/**
 * Visible work-item name for all parties.
 * Prefer curated MOI documentTitle — it replaces type-of-document / service in the UI.
 * Category buckets still use job.service via resolveServiceCategory; never group by this title.
 */
export function jobDisplayTitle(
  job: JobRequestResponse,
  unit?: JobRequestUnitDto | null,
): string {
  const title = (unit?.documentTitle || job.documentTitle || '').trim();
  if (title) return title;
  // No curated title yet — fall back to package service / type of document
  if (job.taskType === 'Service' || !job.taskType) return job.service || 'Work item';
  if (job.service && job.taskType !== job.service) return job.service;
  return job.taskType;
}

/** True when a curated MOI title is present (type of document is no longer the visible name). */
export function hasCuratedDocumentTitle(
  job: JobRequestResponse,
  unit?: JobRequestUnitDto | null,
): boolean {
  return Boolean((unit?.documentTitle || job.documentTitle || '').trim());
}
