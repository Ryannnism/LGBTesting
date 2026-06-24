import type { WorkflowTemplateDto } from '@/lib/api';

/** MOA workflow template codes (match backend WorkflowTemplate.Code). */
export const MOA_WORKFLOW_TEMPLATES = [
  { code: '', label: 'Division default' },
  { code: 'MOA_NO_LOA', label: 'MOA — No LOA' },
  { code: 'MOA_WITH_LOA', label: 'MOA — With LOA' },
  { code: 'MOA_SWM', label: 'MOA — SWM Group' },
] as const;

const SKIPPED_MOA_STEP_ASSIGNEE_TYPES = new Set([
  'ProjectInitiator',
  'DivisionRecommender',
  'BoardMembers',
  'LoaHolders',
]);

function parseLoaHolderNames(loaHolders: string | undefined | null): string[] {
  return (loaHolders ?? '')
    .split(',')
    .map((name) => name.trim())
    .filter(Boolean);
}

function resolveMoaStepHolderName(step: WorkflowTemplateDto['steps'][number]): string {
  if (SKIPPED_MOA_STEP_ASSIGNEE_TYPES.has(step.assigneeType)) return '';

  const configured = (step.assigneeDisplayName || step.assigneeRole || '').trim();
  if (configured) return configured;

  if (step.assigneeType === 'JobTitle' || step.assigneeType === 'ExternalName') {
    return step.displayName.trim();
  }

  return '';
}

/** Names from Admin → Workflow config for the selected MOA template. */
export function getMoaWorkflowTemplateHolderNames(
  templateCode: string | undefined | null,
  workflowTemplates: WorkflowTemplateDto[],
  options?: { hasLoa?: boolean; loaHolders?: string },
): string[] {
  const code = (templateCode ?? '').trim();
  if (!code) return [];

  const template = workflowTemplates.find(
    (entry) => entry.code.toUpperCase() === code.toUpperCase(),
  );
  if (!template) return [];

  const names: string[] = [];
  const seen = new Set<string>();

  for (const step of template.steps) {
    const name = resolveMoaStepHolderName(step);
    if (!name) continue;
    const key = name.toLowerCase();
    if (seen.has(key)) continue;
    seen.add(key);
    names.push(name);
  }

  const hasLoaStep = template.steps.some((step) => step.assigneeType === 'LoaHolders');
  if (hasLoaStep && options?.hasLoa) {
    for (const loaName of parseLoaHolderNames(options.loaHolders)) {
      const key = loaName.toLowerCase();
      if (seen.has(key)) continue;
      seen.add(key);
      names.push(loaName);
    }
  }

  return names;
}

export interface MoaWorkflowAccountHolder {
  id: number;
  name: string;
  email: string;
  phone: string;
  moi: boolean;
  moiApproval: boolean;
  moa: boolean;
  fromMoaWorkflowTemplate?: boolean;
  fromDivisionRecommender?: boolean;
  /** MOI workflow slot auto-filled from Admin → Workflow config (e.g. project initiator). */
  fromMoiWorkflowSlot?: 'projectInitiator';
}

/** Manual placeholder row with no data — dropped once roster/template signers are synced. */
export function isBlankManualHolder(holder: MoaWorkflowAccountHolder): boolean {
  return !holder.fromMoaWorkflowTemplate
    && !holder.fromDivisionRecommender
    && !holder.fromMoiWorkflowSlot
    && !holder.name.trim()
    && !holder.email.trim()
    && !holder.phone.trim();
}

export function withoutBlankManualHolders(holders: MoaWorkflowAccountHolder[]): MoaWorkflowAccountHolder[] {
  return holders.filter((holder) => !isBlankManualHolder(holder));
}

const MOI_WORKFLOW_TEMPLATE_CODE = 'MOI_RECOMMEND';

const SKIPPED_MOI_CUSTOMER_STEP_TYPES = new Set([
  'DivisionRecommender',
  'MoiApprovalHolder',
]);

function resolveMoiClientStepHolder(
  step: WorkflowTemplateDto['steps'][number],
): { slot: 'projectInitiator'; name: string; moi: boolean; moiApproval: boolean } | null {
  if (SKIPPED_MOI_CUSTOMER_STEP_TYPES.has(step.assigneeType)) return null;

  if (step.assigneeType === 'ProjectInitiator') {
    const name = (step.assigneeDisplayName || 'Project Initiator').trim();
    if (!name) return null;
    return { slot: 'projectInitiator', name, moi: true, moiApproval: false };
  }

  return null;
}

/** MOI workflow steps → client account holders (project initiator = Needs MOI). */
export function syncMoiWorkflowAccountHolders(
  holders: MoaWorkflowAccountHolder[],
  workflowTemplates: WorkflowTemplateDto[],
  templateCode: string = MOI_WORKFLOW_TEMPLATE_CODE,
): MoaWorkflowAccountHolder[] {
  const template = workflowTemplates.find(
    (entry) => entry.code.toUpperCase() === templateCode.toUpperCase(),
  ) ?? workflowTemplates.find((entry) => entry.workflowType === 'MOI');

  const kept = holders.filter((holder) => !holder.fromMoiWorkflowSlot);
  if (!template) return kept;

  const slots = template.steps
    .map(resolveMoiClientStepHolder)
    .filter((slot): slot is NonNullable<typeof slot> => slot != null);

  const existingNames = new Set(kept.map((holder) => holder.name.trim().toLowerCase()).filter(Boolean));
  let nextId = Math.max(0, ...holders.map((holder) => holder.id)) + 1;

  const added = slots
    .filter((slot) => !existingNames.has(slot.name.toLowerCase()))
    .map((slot) => ({
      id: nextId++,
      name: slot.name,
      email: '',
      phone: '',
      moi: slot.moi,
      moiApproval: slot.moiApproval,
      moa: false,
      fromMoiWorkflowSlot: slot.slot,
    }));

  return [...kept, ...added];
}

/** MOI client signers: division roster first, then MOI workflow project-initiator slot if needed. */
export function syncMoiClientWorkflowHolders(
  holders: MoaWorkflowAccountHolder[],
  moiWorkflowTemplates: WorkflowTemplateDto[],
  divisionRecommenders: {
    displayName: string;
    email?: string;
    phone?: string;
    needsMoi?: boolean;
    needsMoiApproval?: boolean;
    needsMoa?: boolean;
  }[] | undefined,
): MoaWorkflowAccountHolder[] {
  let next = syncDivisionRecommenderAccountHolders(holders, divisionRecommenders);
  if (!next.some((holder) => holder.moi)) {
    next = syncMoiWorkflowAccountHolders(next, moiWorkflowTemplates);
  }
  return withoutBlankManualHolders(next);
}

/** Division group roster → client account holders (saved on customer; login created in Admin). */
export function syncDivisionRecommenderAccountHolders(
  holders: MoaWorkflowAccountHolder[],
  recommenders: {
    displayName: string;
    email?: string;
    phone?: string;
    needsMoi?: boolean;
    needsMoiApproval?: boolean;
    needsMoa?: boolean;
  }[] | undefined,
): MoaWorkflowAccountHolder[] {
  const roster = recommenders ?? [];
  const rosterKeys = new Set(
    roster.map((entry) => entry.displayName.trim().toLowerCase()).filter(Boolean),
  );

  const kept = holders
    .filter((holder) => !holder.fromDivisionRecommender)
    .map((holder) => {
      const match = roster.find(
        (entry) => entry.displayName.trim().toLowerCase() === holder.name.trim().toLowerCase(),
      );
      if (!match) return holder;
      return {
        ...holder,
        email: holder.email || match.email?.trim() || '',
        phone: holder.phone || match.phone?.trim() || '',
        moi: holder.moi || Boolean(match.needsMoi),
        moiApproval: holder.moiApproval || Boolean(match.needsMoiApproval),
        moa: holder.moa || Boolean(match.needsMoa),
      };
    });

  const existingNames = new Set(kept.map((holder) => holder.name.trim().toLowerCase()).filter(Boolean));

  let nextId = Math.max(0, ...holders.map((holder) => holder.id)) + 1;
  const added = roster
    .filter((entry) => {
      const name = entry.displayName.trim();
      return name && !existingNames.has(name.toLowerCase());
    })
    .map((entry) => ({
      id: nextId++,
      name: entry.displayName.trim(),
      email: entry.email?.trim() || '',
      phone: entry.phone?.trim() || '',
      moi: Boolean(entry.needsMoi),
      moiApproval: entry.needsMoiApproval !== false,
      moa: Boolean(entry.needsMoa),
      fromDivisionRecommender: true,
    }));

  const merged = [...kept, ...added];
  if (rosterKeys.size === 0) return merged;
  return merged;
}

/** Replace workflow-generated MOA rows; keep client signers Sharon or the user already entered. */
export function syncMoaWorkflowAccountHolders(
  holders: MoaWorkflowAccountHolder[],
  templateCode: string | undefined | null,
  workflowTemplates: WorkflowTemplateDto[],
  options?: { hasLoa?: boolean; loaHolders?: string },
): MoaWorkflowAccountHolder[] {
  const templateNames = getMoaWorkflowTemplateHolderNames(templateCode, workflowTemplates, options);
  const kept = holders.filter((holder) => !holder.fromMoaWorkflowTemplate);
  const existingNames = new Set(kept.map((holder) => holder.name.trim().toLowerCase()).filter(Boolean));

  let nextId = Math.max(0, ...holders.map((holder) => holder.id)) + 1;
  const added = templateNames
    .filter((name) => !existingNames.has(name.toLowerCase()))
    .map((name) => ({
      id: nextId++,
      name,
      email: '',
      phone: '',
      moi: false,
      moiApproval: false,
      moa: true,
      fromMoaWorkflowTemplate: true,
    }));

  const merged = [...kept, ...added];
  return withoutBlankManualHolders(merged);
}

export interface MoaTemplateSectionFlags {
  seniorManagerCoSec: boolean;
  managerRegulatory: boolean;
  headOfFinanceCfo: boolean;
  ceoCooGm: boolean;
  msTeh: boolean;
  boardMembers: boolean;
  loaHolders: boolean;
  dlcm: boolean;
}

const NO_LOA: MoaTemplateSectionFlags = {
  seniorManagerCoSec: true,
  managerRegulatory: false,
  headOfFinanceCfo: true,
  ceoCooGm: true,
  msTeh: true,
  boardMembers: true,
  dlcm: true,
};

const WITH_LOA: MoaTemplateSectionFlags = {
  seniorManagerCoSec: true,
  managerRegulatory: false,
  headOfFinanceCfo: true,
  ceoCooGm: false,
  msTeh: true,
  boardMembers: false,
  loaHolders: false,
  dlcm: true,
};

const SWM: MoaTemplateSectionFlags = {
  seniorManagerCoSec: true,
  managerRegulatory: true,
  headOfFinanceCfo: true,
  ceoCooGm: false,
  msTeh: true,
  boardMembers: false,
  loaHolders: true,
  dlcm: false,
};

function baseForTemplate(templateCode: string | undefined | null): MoaTemplateSectionFlags {
  const code = (templateCode ?? '').trim().toUpperCase();
  if (code === 'MOA_WITH_LOA') return { ...WITH_LOA };
  if (code === 'MOA_SWM') return { ...SWM };
  if (code === 'MOA_NO_LOA' || code === '') return { ...NO_LOA };
  return { ...NO_LOA };
}

/** Option C: template + MOA form flags determine which approval blocks appear. */
export function resolveMoaTemplateSections(
  templateCode: string | undefined | null,
  options: {
    financeRelated?: boolean;
    bankSignatoryMatter?: boolean;
    shareMovement?: boolean;
    hasLoa?: boolean;
  },
): MoaTemplateSectionFlags {
  const base = baseForTemplate(templateCode);
  return {
    seniorManagerCoSec: base.seniorManagerCoSec,
    managerRegulatory: base.managerRegulatory && (templateCode === 'MOA_SWM'),
    headOfFinanceCfo: base.headOfFinanceCfo && Boolean(options.financeRelated),
    ceoCooGm: base.ceoCooGm && Boolean(options.financeRelated || options.shareMovement),
    msTeh: base.msTeh && Boolean(options.bankSignatoryMatter),
    boardMembers: base.boardMembers && !options.hasLoa,
    loaHolders: base.loaHolders && Boolean(options.hasLoa),
    dlcm: base.dlcm,
  };
}
