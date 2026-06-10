import { useCallback, useEffect, useMemo, useState } from 'react';
import { Check, FileText, Plus, Undo2, X } from 'lucide-react';
import { DateInput } from './DateInput';
import { parseDateToIso } from '@/lib/dates';
import {
  ApiError,
  assignClientJob,
  getClientJobs,
  getClientPortalSummary,
  getMOIForms,
  getUsers,
  issueMoiJob,
  recordClientJobProgress,
  type ClientPortalSummaryDto,
  type JobRequestResponse,
  type JobRequestUnitDto,
  type UserResponse,
} from '@/lib/api';
import { JOB_CATEGORIES, jobCategory } from '@/lib/roles';
import { handoffLabel } from '@/lib/handoff';

interface ClientPortalProps {
  currentUser: UserResponse;
  onOpenForm: (job: JobRequestResponse) => void;
  refreshKey?: number;
}

function jobUnits(job: JobRequestResponse): JobRequestUnitDto[] {
  if (job.units?.length) return job.units;
  return [{
    id: 0,
    unitNumber: 1,
    assignedUserName: job.jobAssignedTo,
    status: job.status as JobRequestUnitDto['status'],
    scheduledDate: job.scheduledDate,
    assignees: [],
  }];
}

function CategoryCounter({ label, pending, inProgress, completed, total }: {
  label: string;
  pending: number;
  inProgress: number;
  completed: number;
  total: number;
}) {
  if (total === 0) return null;
  const open = pending + inProgress;
  return (
    <div className="bg-card border border-border rounded-lg p-4">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="text-2xl font-semibold mt-1">{completed}<span className="text-base font-normal text-muted-foreground"> / {total}</span></p>
      <p className="text-xs mt-2 text-muted-foreground">
        {open} open · {completed} done
      </p>
      <div className="mt-2 h-1.5 bg-muted rounded-full overflow-hidden">
        <div
          className="h-full bg-green-600 rounded-full"
          style={{ width: total ? `${(completed / total) * 100}%` : '0%' }}
        />
      </div>
    </div>
  );
}

export function ClientPortal({ currentUser, onOpenForm, refreshKey = 0 }: ClientPortalProps) {
  const [jobs, setJobs] = useState<JobRequestResponse[]>([]);
  const [summary, setSummary] = useState<ClientPortalSummaryDto | null>(null);
  const [team, setTeam] = useState<UserResponse[]>([]);
  const [teamHint, setTeamHint] = useState('');
  const [activeCategory, setActiveCategory] = useState<string>('All');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [issuing, setIssuing] = useState(false);
  const [showIssue, setShowIssue] = useState(false);
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
      const [data, portalSummary, members] = await Promise.all([
        getClientJobs(true),
        getClientPortalSummary(),
        getUsers(),
      ]);
      setJobs(data);
      setSummary(portalSummary);
      setTeam(members.filter((m) => m.userId !== currentUser.userId));
      if (portalSummary.teamMembers === 0) {
        setTeamHint('Invite additional client admins under the Team tab.');
      } else {
        setTeamHint('');
      }
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to load jobs.');
    } finally {
      setLoading(false);
    }
  }, [currentUser.userId]);

  useEffect(() => {
    void load();
  }, [load, refreshKey]);

  const filteredJobs = useMemo(() => {
    if (activeCategory === 'All') return jobs;
    return jobs.filter((j) => jobCategory(j.taskType) === activeCategory);
  }, [jobs, activeCategory]);

  const categoryTabs = useMemo(() => {
    const present = new Set(jobs.map((j) => jobCategory(j.taskType)));
    return ['All', ...JOB_CATEGORIES.filter((c) => present.has(c))];
  }, [jobs]);

  const handleAssign = async (job: JobRequestResponse, userId: number, unitNumber = 1) => {
    setError('');
    try {
      await assignClientJob(job.id, { userId, unitNumber });
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to assign task.');
    }
  };

  const handleRemove = async (job: JobRequestResponse, userId: number, unitNumber: number) => {
    setError('');
    try {
      await assignClientJob(job.id, { userId, unitNumber, remove: true });
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to remove assignee.');
    }
  };

  const handleSchedule = async (job: JobRequestResponse, unitNumber: number, isoValue: string) => {
    const unit = jobUnits(job).find((u) => u.unitNumber === unitNumber);
    const savedIso = unit?.scheduledDate ? parseDateToIso(unit.scheduledDate) ?? '' : '';
    if (isoValue === savedIso) return;

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
      if (job.linkedFormId) onOpenForm(job);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to issue MOI.');
    } finally {
      setIssuing(false);
    }
  };

  const openJobForm = async (job: JobRequestResponse) => {
    if (job.linkedFormId) {
      onOpenForm(job);
      return;
    }
    if (job.taskType === 'MOI' || job.taskType === 'MOI Approval') {
      const forms = await getMOIForms(job.id);
      if (forms[0]) {
        onOpenForm({ ...job, linkedFormId: forms[0].id, linkedFormKind: 'MOI' });
      }
    }
  };

  const renderAssignees = (job: JobRequestResponse, unit: JobRequestUnitDto) => {
    const assignees = unit.assignees ?? [];
    if (assignees.length === 0 && unit.assignedUserName) {
      return <span className="text-xs text-muted-foreground">{unit.assignedUserName}</span>;
    }
    if (assignees.length === 0) {
      return <span className="text-xs text-muted-foreground">Unassigned</span>;
    }
    return (
      <div className="flex flex-wrap gap-1">
        {assignees.map((a) => (
          <span key={a.userId} className="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded bg-muted">
            {a.userName}
            <button
              type="button"
              title="Remove from task"
              className="hover:text-destructive"
              onClick={() => void handleRemove(job, a.userId, unit.unitNumber)}
            >
              <X className="w-3 h-3" />
            </button>
          </span>
        ))}
      </div>
    );
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h2 className="text-xl font-semibold">Client portal</h2>
          <p className="text-sm text-muted-foreground mt-1">
            {currentUser.customerName ?? 'Your company'} — issue MOI, set target dates, and track progress by category.
          </p>
        </div>
        <button
          type="button"
          onClick={() => setShowIssue(true)}
          className="flex items-center gap-2 px-4 py-2 bg-primary text-primary-foreground rounded-lg text-sm"
        >
          <Plus className="w-4 h-4" />
          Issue MOI
        </button>
      </div>

      {summary?.categoryProgress && summary.categoryProgress.length > 0 && (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          {summary.categoryProgress.map((cat) => (
            <CategoryCounter
              key={cat.category}
              label={cat.category}
              pending={cat.pending}
              inProgress={cat.inProgress}
              completed={cat.completed}
              total={cat.total}
            />
          ))}
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

      <div className="flex flex-wrap gap-2">
        {categoryTabs.map((tab) => (
          <button
            key={tab}
            type="button"
            onClick={() => setActiveCategory(tab)}
            className={`px-3 py-1.5 text-sm rounded-lg border transition-colors ${
              activeCategory === tab
                ? 'bg-primary text-primary-foreground border-primary'
                : 'border-border hover:bg-muted'
            }`}
          >
            {tab}
          </button>
        ))}
      </div>

      <div className="bg-card border border-border rounded-lg overflow-hidden">
        {loading ? (
          <p className="p-6 text-sm text-muted-foreground">Loading jobs…</p>
        ) : filteredJobs.length === 0 ? (
          <p className="p-6 text-sm text-muted-foreground">No jobs in this category yet.</p>
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-muted/50">
              <tr>
                <th className="px-4 py-3 text-left">Task</th>
                <th className="px-4 py-3 text-left">Service</th>
                <th className="px-4 py-3 text-left">Target date</th>
                <th className="px-4 py-3 text-left">Status</th>
                <th className="px-4 py-3 text-left">Handoff</th>
                <th className="px-4 py-3 text-left">Assigned</th>
                <th className="px-4 py-3 text-left">Form</th>
                <th className="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {filteredJobs.map((job) =>
                jobUnits(job).map((unit) => (
                  <tr key={`${job.id}-${unit.unitNumber}`} className="border-t border-border">
                    <td className="px-4 py-3">
                      {job.taskType}
                      {(job.totalQty ?? 1) > 1 && (
                        <span className="ml-1 text-xs text-muted-foreground">#{unit.unitNumber}</span>
                      )}
                    </td>
                    <td className="px-4 py-3">{job.service}</td>
                    <td className="px-4 py-3">
                      <DateInput
                        value={unit.scheduledDate}
                        onChange={(iso) => void handleSchedule(job, unit.unitNumber, iso)}
                      />
                    </td>
                    <td className="px-4 py-3">
                      <span className={`text-xs px-2 py-0.5 rounded-full ${
                        unit.status === 'Completed' ? 'bg-green-100 text-green-800'
                          : unit.status === 'In Progress' ? 'bg-blue-100 text-blue-800'
                          : 'bg-yellow-100 text-yellow-800'
                      }`}>
                        {unit.status}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-xs">{handoffLabel(job.internalHandoffStatus)}</td>
                    <td className="px-4 py-3">
                      {renderAssignees(job, unit)}
                      <select
                        className="mt-1 text-xs border border-border rounded px-2 py-1 max-w-full"
                        defaultValue=""
                        onChange={(e) => {
                          const id = Number(e.target.value);
                          if (id) void handleAssign(job, id, unit.unitNumber);
                          e.target.value = '';
                        }}
                      >
                        <option value="">+ Assign admin…</option>
                        {team.map((m) => (
                          <option key={m.userId} value={m.userId}>{m.name}</option>
                        ))}
                      </select>
                    </td>
                    <td className="px-4 py-3">
                      {job.linkedFormId ? (
                        <span className="text-xs px-2 py-0.5 rounded bg-primary/10">{job.linkedFormKind} #{job.linkedFormId}</span>
                      ) : (
                        <span className="text-muted-foreground">—</span>
                      )}
                    </td>
                    <td className="px-4 py-3 text-right">
                      <div className="flex items-center justify-end gap-2 flex-wrap">
                        {unit.status === 'Completed' ? (
                          <button
                            type="button"
                            title="Undo completion"
                            onClick={() => void handleUndo(job, unit.unitNumber)}
                            className="inline-flex items-center gap-1 px-2 py-1 text-xs border border-border rounded hover:bg-muted"
                          >
                            <Undo2 className="w-3 h-3" />
                            Undo
                          </button>
                        ) : (
                          <button
                            type="button"
                            title="Mark done"
                            onClick={() => void handleMarkDone(job, unit.unitNumber)}
                            className="inline-flex items-center gap-1 px-2 py-1 text-xs bg-green-600 text-white rounded hover:bg-green-700"
                          >
                            <Check className="w-3 h-3" />
                            Done
                          </button>
                        )}
                        {['MOI', 'MOI Approval', 'MOA'].includes(job.taskType) && (
                          <button
                            type="button"
                            onClick={() => void openJobForm(job)}
                            className="inline-flex items-center gap-1 text-sm text-primary hover:underline"
                          >
                            <FileText className="w-4 h-4" />
                            Form
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                )),
              )}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
