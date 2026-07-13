import { useCallback, useEffect, useState } from 'react';
import { FileText, Plus } from 'lucide-react';
import { ClientCompanyWorkbench } from './ClientCompanyWorkbench';
import {
  ApiError,
  getClientJobs,
  getClientPortalSummary,
  getMOIForms,
  getMyCompany,
  issueMoiForJob,
  issueMoiJob,
  recordClientJobProgress,
  updateMoiApprovalMode,
  type JobRequestResponse,
  type JobRequestUnitDto,
  type UserResponse,
} from '@/lib/api';
import {
  canClientStartMoi,
  isMoiRejected,
  canSignatoryStartMoi,
  signatoryCanSignMoi,
  canClientViewMoa,
  canClientViewMoi,
  canOpenMoiForm,
  isMoaClientSignoffPhase,
  signatoryCanSignMoa,
  unitHasMoaForm,
  unitHasMoiForm,
} from '@/lib/packageItemStatus';

interface ClientPortalProps {
  currentUser: UserResponse;
  onOpenMoiForm: (job: JobRequestResponse) => void;
  onOpenMoaForm: (job: JobRequestResponse) => void;
  refreshKey?: number;
  mode?: 'admin' | 'signatory';
}

function jobForUnit(job: JobRequestResponse, unit: JobRequestUnitDto): JobRequestResponse {
  if ((job.totalQty ?? 1) <= 1) return job;
  return {
    ...job,
    linkedFormId: unit.linkedFormId,
    linkedFormKind: unit.linkedFormKind,
    hasMoiForm: unit.hasMoiForm,
    hasMoaForm: unit.hasMoaForm,
    moiWorkflowState: unit.moiWorkflowState,
    activeUnitNumber: unit.unitNumber,
  };
}

function canOpenJobForm(
  job: JobRequestResponse,
  unit: JobRequestUnitDto,
  isSignatoryView: boolean,
  currentUser?: UserResponse,
): boolean {
  if (isSignatoryView) {
    if (currentUser && signatoryCanSignMoi(job, currentUser, unit)) return true;
    if (canClientViewMoi(job, unit)) return true;
    if (currentUser && canSignatoryStartMoi(job, currentUser, unit) && canClientStartMoi(job, unit)) return true;
    if (signatoryCanSignMoa(job, currentUser ?? { name: '' }, unit)) return true;
    if (canClientViewMoa(job, unit)) return true;
    return false;
  }
  if (canClientStartMoi(job, unit)) return true;
  if (canClientViewMoi(job, unit) || canClientViewMoa(job, unit)) return true;
  if (unitHasMoiForm(job, unit) || unitHasMoaForm(job, unit)) return true;
  return canOpenMoiForm(job);
}

export function ClientPortal({ currentUser, onOpenMoiForm, onOpenMoaForm, refreshKey = 0, mode = 'admin' }: ClientPortalProps) {
  const isSignatoryView = mode === 'signatory';
  const [jobs, setJobs] = useState<JobRequestResponse[]>([]);
  const [teamHint, setTeamHint] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [issuing, setIssuing] = useState(false);
  const [showIssue, setShowIssue] = useState(false);
  const [moiApprovalMode, setMoiApprovalMode] = useState<'AllRequired' | 'AnyOne'>('AllRequired');
  const [savingMode, setSavingMode] = useState(false);
  const [issueForm, setIssueForm] = useState({
    service: '',
    typeOfDocument: '',
    documentTitle: '',
    adHoc: false,
  });

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const [data, portalSummary, company] = await Promise.all([
        getClientJobs(true),
        getClientPortalSummary(),
        isSignatoryView ? Promise.resolve(null) : getMyCompany(),
      ]);
      setJobs(data);
      if (company?.moiApprovalMode) {
        setMoiApprovalMode(company.moiApprovalMode);
      }
      if (isSignatoryView) {
        setTeamHint('');
      } else if (portalSummary.teamMembers === 0) {
        setTeamHint('Invite additional client admins under the Team tab.');
      } else {
        setTeamHint('');
      }
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to load jobs.');
    } finally {
      setLoading(false);
    }
  }, [currentUser.userId, isSignatoryView]);

  useEffect(() => {
    void load();
  }, [load, refreshKey]);

  const moiActionLabel = (job: JobRequestResponse, unit: JobRequestUnitDto) => {
    if (isMoiRejected(job, unit)) return 'Revise MOI';
    if (isSignatoryView && signatoryCanSignMoi(job, currentUser, unit)) return 'Sign MOI';
    const moiState = unit.moiWorkflowState ?? job.moiWorkflowState ?? '';
    if (unitHasMoiForm(job, unit) && moiState === 'Draft') return 'Continue MOI';
    if (canClientStartMoi(job, unit) && (!isSignatoryView || canSignatoryStartMoi(job, currentUser, unit))) return 'Start MOI';
    return 'View MOI';
  };

  const moaActionLabel = (job: JobRequestResponse, unit: JobRequestUnitDto) => {
    if (signatoryCanSignMoa(job, currentUser, unit) || (!isSignatoryView && currentUser.needsMoa && isMoaClientSignoffPhase(job, unit))) {
      return 'Sign MOA';
    }
    return 'View MOA';
  };

  const showMoiAction = (job: JobRequestResponse, unit: JobRequestUnitDto) =>
    canClientViewMoi(job, unit)
    || canClientStartMoi(job, unit)
    || (isSignatoryView && signatoryCanSignMoi(job, currentUser, unit))
    || (isSignatoryView && canSignatoryStartMoi(job, currentUser, unit) && canClientStartMoi(job, unit));

  const showMoaAction = (job: JobRequestResponse, unit: JobRequestUnitDto) =>
    unitHasMoaForm(job, unit)
    && (signatoryCanSignMoa(job, currentUser, unit)
      || canClientViewMoa(job, unit)
      || (!isSignatoryView && currentUser.needsMoa && isMoaClientSignoffPhase(job, unit)));

  const renderFormActions = (job: JobRequestResponse, unit: JobRequestUnitDto) => {
    const ctx = jobForUnit(job, unit);
    const actions: { key: string; label: string; onClick: () => void }[] = [];

    if (showMoiAction(job, unit)) {
      actions.push({
        key: 'moi',
        label: moiActionLabel(job, unit),
        onClick: () => void openMoiForm(job, unit),
      });
    }
    if (showMoaAction(job, unit)) {
      actions.push({
        key: 'moa',
        label: moaActionLabel(job, unit),
        onClick: () => onOpenMoaForm(ctx),
      });
    }

    if (actions.length === 0) return <span className="text-sm text-muted-foreground">—</span>;

    return (
      <div className="flex flex-col items-start gap-1">
        {actions.map((action) => (
          <button
            key={action.key}
            type="button"
            onClick={action.onClick}
            className="inline-flex items-center gap-1 text-sm text-primary hover:underline"
          >
            <FileText className="w-3.5 h-3.5" />
            {action.label}
          </button>
        ))}
      </div>
    );
  };

  const handleSchedule = async (job: JobRequestResponse, unitNumber: number, isoValue: string) => {
    setError('');
    try {
      await recordClientJobProgress(job.id, { unitNumber, scheduledDate: isoValue });
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to save date.');
    }
  };

  const handleMarkDone = async (job: JobRequestResponse, unitNumber: number) => {
    setError('');
    try {
      await recordClientJobProgress(job.id, { unitNumber, markUnitComplete: true });
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to mark complete.');
    }
  };

  const handleUndo = async (job: JobRequestResponse, unitNumber: number) => {
    setError('');
    try {
      await recordClientJobProgress(job.id, { unitNumber, markUnitIncomplete: true });
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to undo completion.');
    }
  };

  const handleIssue = async (e: React.FormEvent) => {
    e.preventDefault();
    setIssuing(true);
    setError('');
    try {
      const job = await issueMoiJob({
        service: issueForm.service || issueForm.typeOfDocument || 'MOI',
        typeOfDocument: issueForm.typeOfDocument,
        documentTitle: issueForm.documentTitle,
        adHoc: issueForm.adHoc,
        initiationDate: new Date().toISOString().split('T')[0],
        requestedBy: currentUser.name,
      });
      setShowIssue(false);
      setIssueForm({ service: '', typeOfDocument: '', documentTitle: '', adHoc: false });
      await load();
      if (job.linkedFormId) onOpenMoiForm(job);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to issue MOI.');
    } finally {
      setIssuing(false);
    }
  };

  const handleMoiApprovalModeChange = async (mode: 'AllRequired' | 'AnyOne') => {
    if (mode === moiApprovalMode) return;
    setSavingMode(true);
    setError('');
    try {
      const updated = await updateMoiApprovalMode(mode);
      setMoiApprovalMode(updated.moiApprovalMode ?? mode);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to update MOI signing policy.');
    } finally {
      setSavingMode(false);
    }
  };

  const openMoiForm = async (job: JobRequestResponse, unit: JobRequestUnitDto) => {
    if (!canOpenJobForm(job, unit, isSignatoryView, currentUser)) return;
    const ctx = jobForUnit(job, unit);
    setError('');
    try {
      if (ctx.linkedFormId || unit.moiFormId || unitHasMoiForm(job, unit) || isMoiRejected(job, unit)) {
        onOpenMoiForm({
          ...ctx,
          linkedFormId: unit.moiFormId ?? (ctx.linkedFormKind === 'MOI' ? ctx.linkedFormId : undefined),
          linkedFormKind: 'MOI',
          hasMoiForm: true,
        });
        return;
      }

      if (canClientStartMoi(job, unit)) {
        const updated = await issueMoiForJob(job.id, {
          service: job.service,
          typeOfDocument: job.service,
          requestedBy: currentUser.name,
          unitNumber: unit.unitNumber,
        });
        await load();
        const refreshedUnit = updated.units?.find((u) => u.unitNumber === unit.unitNumber) ?? unit;
        onOpenMoiForm(jobForUnit(updated, refreshedUnit));
        return;
      }

      const forms = await getMOIForms(job.id, unit.unitNumber);
      const linkedFormId = forms[0]?.id ?? unit.moiFormId ?? unit.linkedFormId;
      if (linkedFormId) {
        onOpenMoiForm({
          ...ctx,
          linkedFormId,
          linkedFormKind: 'MOI',
          hasMoiForm: true,
        });
      }
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to open MOI.');
    }
  };

  const openMoaForm = (job: JobRequestResponse, unit: JobRequestUnitDto) => {
    if (!canOpenJobForm(job, unit, isSignatoryView, currentUser)) return;
    onOpenMoaForm(jobForUnit(job, unit));
  };

  const openPrimaryForm = (job: JobRequestResponse, unit: JobRequestUnitDto) => {
    if (showMoaAction(job, unit) && signatoryCanSignMoa(job, currentUser, unit)) {
      openMoaForm(job, unit);
      return;
    }
    if (showMoiAction(job, unit)) {
      void openMoiForm(job, unit);
      return;
    }
    if (showMoaAction(job, unit)) {
      openMoaForm(job, unit);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h2 className="text-xl font-semibold">{isSignatoryView ? 'My documents' : 'Client portal'}</h2>
          <p className="text-sm text-muted-foreground mt-1">
            {isSignatoryView
              ? 'Pick a company, work one item at a time, and use History for completed work. Yellow borders mean something needs a signature.'
              : 'Open a company tile to work by category. Yellow borders mean a signature is pending. Green badge opens completed history.'}
          </p>
        </div>
        {!isSignatoryView && (
          <button
            type="button"
            onClick={() => setShowIssue(true)}
            className="flex items-center gap-2 px-4 py-2 bg-primary text-primary-foreground rounded-lg text-sm"
          >
            <Plus className="w-4 h-4" />
            Issue MOI
          </button>
        )}
      </div>

      {!isSignatoryView && (
        <div className="bg-card border border-border rounded-lg p-4 space-y-3">
          <div>
            <h3 className="text-sm font-medium">MOI signing policy</h3>
            <p className="text-xs text-muted-foreground mt-1">
              Choose how many client approvers must sign each MOI before it is released to LGB.
              MOA always requires every listed MOA signatory.
            </p>
          </div>
          <div className="flex flex-col sm:flex-row gap-3 text-sm">
            <label className={`flex items-start gap-2 border rounded-lg px-3 py-2 cursor-pointer ${moiApprovalMode === 'AllRequired' ? 'border-primary bg-primary/5' : 'border-border'}`}>
              <input
                type="radio"
                name="moiApprovalMode"
                className="mt-1"
                checked={moiApprovalMode === 'AllRequired'}
                disabled={savingMode}
                onChange={() => void handleMoiApprovalModeChange('AllRequired')}
              />
              <span>
                <span className="font-medium">All approvers must sign</span>
                <span className="block text-xs text-muted-foreground">Every MOI approver signs before LGB intake.</span>
              </span>
            </label>
            <label className={`flex items-start gap-2 border rounded-lg px-3 py-2 cursor-pointer ${moiApprovalMode === 'AnyOne' ? 'border-primary bg-primary/5' : 'border-border'}`}>
              <input
                type="radio"
                name="moiApprovalMode"
                className="mt-1"
                checked={moiApprovalMode === 'AnyOne'}
                disabled={savingMode}
                onChange={() => void handleMoiApprovalModeChange('AnyOne')}
              />
              <span>
                <span className="font-medium">Any one approver can sign</span>
                <span className="block text-xs text-muted-foreground">One MOI approver is enough to release to LGB.</span>
              </span>
            </label>
          </div>
        </div>
      )}

      {teamHint && (
        <p className="text-sm border border-amber-200 bg-amber-50 text-amber-900 rounded-lg px-4 py-3">{teamHint}</p>
      )}
      {error && <p className="text-sm text-destructive">{error}</p>}

      {showIssue && (
        <form onSubmit={(e) => void handleIssue(e)} className="bg-card border border-border rounded-lg p-6 space-y-4">
          <h3 className="font-medium">New MOI</h3>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label className="block text-sm mb-1">Service / document type *</label>
              <input
                required
                className="w-full px-3 py-2 border border-border rounded-lg"
                value={issueForm.typeOfDocument}
                onChange={(e) => setIssueForm({ ...issueForm, typeOfDocument: e.target.value, service: e.target.value })}
              />
            </div>
            <div>
              <label className="block text-sm mb-1">Document title</label>
              <input
                className="w-full px-3 py-2 border border-border rounded-lg"
                value={issueForm.documentTitle}
                onChange={(e) => setIssueForm({ ...issueForm, documentTitle: e.target.value })}
                placeholder="e.g. Resolution for new director appointment"
              />
            </div>
          </div>
          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={issueForm.adHoc}
              onChange={(e) => setIssueForm({ ...issueForm, adHoc: e.target.checked })}
            />
            Ad-hoc service (not tied to package)
          </label>
          <div className="flex gap-2">
            <button type="submit" disabled={issuing} className="px-4 py-2 bg-primary text-primary-foreground rounded-lg text-sm disabled:opacity-50">
              {issuing ? 'Issuing…' : 'Issue MOI'}
            </button>
            <button type="button" onClick={() => setShowIssue(false)} className="px-4 py-2 border border-border rounded-lg text-sm">
              Cancel
            </button>
          </div>
        </form>
      )}

      <ClientCompanyWorkbench
        jobs={jobs}
        currentUser={currentUser}
        isSignatoryView={isSignatoryView}
        loading={loading}
        onOpenPrimary={openPrimaryForm}
        renderFormActions={renderFormActions}
        onSchedule={isSignatoryView ? undefined : (job, unitNumber, iso) => void handleSchedule(job, unitNumber, iso)}
        onMarkDone={isSignatoryView ? undefined : (job, unitNumber) => void handleMarkDone(job, unitNumber)}
        onUndo={isSignatoryView ? undefined : (job, unitNumber) => void handleUndo(job, unitNumber)}
      />
    </div>
  );
}
