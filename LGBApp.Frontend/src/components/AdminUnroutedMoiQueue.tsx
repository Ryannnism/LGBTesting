import { useCallback, useEffect, useState } from 'react';
import {
  ApiError,
  assignMoiApprover,
  getMoiFormsNeedingApprover,
  type FormResponse,
} from '@/lib/api';

interface AdminUnroutedMoiQueueProps {
  refreshKey?: number;
  onAssigned?: () => void;
}

interface Draft {
  name: string;
  email: string;
}

/**
 * MOIs that submit could not route: no Approval Matrix row for the submitter and no MOI
 * approver on the company record. They stay here until an Admin names an approver.
 */
export function AdminUnroutedMoiQueue({ refreshKey = 0, onAssigned }: AdminUnroutedMoiQueueProps) {
  const [forms, setForms] = useState<FormResponse[]>([]);
  const [drafts, setDrafts] = useState<Record<number, Draft>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      setForms(await getMoiFormsNeedingApprover());
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to load unrouted MOIs.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load, refreshKey]);

  const draftFor = (id: number) => drafts[id] ?? { name: '', email: '' };

  const patchDraft = (id: number, patch: Partial<Draft>) => {
    setDrafts((prev) => ({ ...prev, [id]: { ...draftFor(id), ...patch } }));
  };

  const assign = async (form: FormResponse) => {
    const draft = draftFor(form.id);
    if (!draft.name.trim() && !draft.email.trim()) {
      setError('Enter an approver name or email.');
      return;
    }
    setSaving(true);
    setError('');
    setMessage('');
    try {
      await assignMoiApprover(form.id, draft.name.trim(), draft.email.trim());
      setMessage(`Approver assigned for ${form.company}. The MOI is now awaiting their signature.`);
      setDrafts((prev) => {
        const next = { ...prev };
        delete next[form.id];
        return next;
      });
      await load();
      onAssigned?.();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to assign approver.');
    } finally {
      setSaving(false);
    }
  };

  if (loading) return <p className="text-sm text-muted-foreground p-4">Loading unrouted MOIs…</p>;

  return (
    <div className="bg-card border border-border rounded-lg p-6 space-y-4">
      <div>
        <h3 className="text-lg font-medium">MOIs awaiting an approver</h3>
        <p className="text-sm text-muted-foreground mt-1">
          These MOIs could not be routed: the submitter is not on the Approval Matrix and the company
          record names no MOI approver. Assign someone to release them for client signature.
        </p>
      </div>

      {error && <p className="text-sm text-destructive">{error}</p>}
      {message && <p className="text-sm text-green-600">{message}</p>}

      {forms.length === 0 ? (
        <p className="text-sm text-muted-foreground">Nothing waiting — every submitted MOI has an approver.</p>
      ) : (
        <div className="space-y-3">
          {forms.map((form) => (
            <div key={form.id} className="border border-border rounded-lg p-3 space-y-2">
              <div className="flex flex-wrap gap-2 items-baseline">
                <span className="font-medium">{form.company}</span>
                <span className="text-xs text-muted-foreground">
                  MOI #{form.id}
                  {form.jobId ? ` · job #${form.jobId}` : ''} · submitted{' '}
                  {new Date(form.updatedAt).toLocaleDateString()}
                </span>
              </div>
              <div className="flex flex-wrap gap-2 items-center">
                <input
                  className="flex-1 min-w-[10rem] text-sm px-2 py-1 border border-border rounded bg-input-background"
                  placeholder="Approver name"
                  value={draftFor(form.id).name}
                  onChange={(e) => patchDraft(form.id, { name: e.target.value })}
                />
                <input
                  className="flex-1 min-w-[12rem] text-sm px-2 py-1 border border-border rounded bg-input-background"
                  placeholder="Approver email"
                  value={draftFor(form.id).email}
                  onChange={(e) => patchDraft(form.id, { email: e.target.value })}
                />
                <button
                  type="button"
                  disabled={saving}
                  onClick={() => void assign(form)}
                  className="text-sm px-3 py-1.5 bg-primary text-primary-foreground rounded disabled:opacity-50"
                >
                  Assign
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
