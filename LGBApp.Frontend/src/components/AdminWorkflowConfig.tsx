import { Plus, Trash2 } from 'lucide-react';
import { useCallback, useEffect, useState } from 'react';
import {
  ApiError,
  getDivisionGroups,
  getUsers,
  getWorkflowTemplates,
  updateDivisionGroup,
  updateWorkflowTemplate,
  type DivisionGroupDto,
  type UserResponse,
  type WorkflowTemplateDto,
} from '@/lib/api';
import { isInternalStaff } from '@/lib/roles';

interface AdminWorkflowConfigProps {
  refreshKey?: number;
}

export function AdminWorkflowConfig({ refreshKey = 0 }: AdminWorkflowConfigProps) {
  const [groups, setGroups] = useState<DivisionGroupDto[]>([]);
  const [templates, setTemplates] = useState<WorkflowTemplateDto[]>([]);
  const [internalUsers, setInternalUsers] = useState<UserResponse[]>([]);
  const [selectedTemplate, setSelectedTemplate] = useState<WorkflowTemplateDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const [g, t, users] = await Promise.all([
        getDivisionGroups(),
        getWorkflowTemplates('MOA'),
        getUsers(),
      ]);
      setGroups(g);
      setTemplates(t);
      setInternalUsers(users.filter((u) => isInternalStaff(u) && u.role !== 'ClientSignatory'));
      setSelectedTemplate((prev) => prev ?? t[0] ?? null);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to load workflow config.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load, refreshKey]);

  const saveTemplate = async () => {
    if (!selectedTemplate) return;
    setSaving(true);
    setMessage('');
    try {
      await updateWorkflowTemplate(selectedTemplate.id, selectedTemplate);
      setMessage('Workflow template saved.');
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to save template.');
    } finally {
      setSaving(false);
    }
  };

  const patchGroup = (groupId: number, patch: Partial<DivisionGroupDto>) => {
    setGroups((prev) => prev.map((g) => (g.id === groupId ? { ...g, ...patch } : g)));
  };

  const addRecommender = (groupId: number) => {
    const group = groups.find((g) => g.id === groupId);
    if (!group) return;
    patchGroup(groupId, {
      recommenders: [...group.recommenders, { id: 0, displayName: '', userId: undefined }],
    });
  };

  const updateRecommender = (
    groupId: number,
    index: number,
    patch: Partial<DivisionGroupDto['recommenders'][number]>,
  ) => {
    const group = groups.find((g) => g.id === groupId);
    if (!group) return;
    const recommenders = group.recommenders.map((r, i) => (i === index ? { ...r, ...patch } : r));
    patchGroup(groupId, { recommenders });
  };

  const removeRecommender = (groupId: number, index: number) => {
    const group = groups.find((g) => g.id === groupId);
    if (!group) return;
    patchGroup(groupId, { recommenders: group.recommenders.filter((_, i) => i !== index) });
  };

  const saveGroup = async (group: DivisionGroupDto) => {
    const payload: DivisionGroupDto = {
      ...group,
      recommenders: group.recommenders
        .map((r) => ({
          ...r,
          displayName: r.displayName.trim(),
        }))
        .filter((r) => r.displayName.length > 0),
    };
    setSaving(true);
    setMessage('');
    try {
      await updateDivisionGroup(payload.id, payload);
      setMessage(`Division group "${group.name}" saved.`);
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to save division group.');
    } finally {
      setSaving(false);
    }
  };

  if (loading) return <p className="text-sm text-muted-foreground p-4">Loading workflow config…</p>;

  return (
    <div className="bg-card border border-border rounded-lg p-6 space-y-6">
      <div>
        <h3 className="text-lg font-medium">MOA Workflow Templates</h3>
        <p className="text-sm text-muted-foreground mt-1">
          Conditional approval chains (No LOA / With LOA / SWM). Link internal signatory accounts to named steps — these are LGB staff, not client signatories.
        </p>
      </div>

      {error && <p className="text-sm text-destructive">{error}</p>}
      {message && <p className="text-sm text-green-600">{message}</p>}

      <div className="flex flex-wrap gap-2">
        {templates.map((t) => (
          <button
            key={t.id}
            type="button"
            onClick={() => setSelectedTemplate(t)}
            className={`px-3 py-1.5 rounded-lg text-sm border ${
              selectedTemplate?.id === t.id ? 'bg-primary text-primary-foreground border-primary' : 'border-border'
            }`}
          >
            {t.name}
          </button>
        ))}
      </div>

      {selectedTemplate && (
        <div className="space-y-3">
          <p className="text-sm text-muted-foreground">{selectedTemplate.description}</p>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-border text-left">
                  <th className="p-2">#</th>
                  <th className="p-2">Step</th>
                  <th className="p-2">Condition</th>
                  <th className="p-2">Assignee type</th>
                  <th className="p-2">Role / Name</th>
                </tr>
              </thead>
              <tbody>
                {selectedTemplate.steps.map((step, idx) => (
                  <tr key={step.id || idx} className="border-b border-border/50">
                    <td className="p-2">{step.stepOrder}</td>
                    <td className="p-2">
                      <input
                        className="w-full px-2 py-1 border border-border rounded bg-input-background"
                        value={step.displayName}
                        onChange={(e) => {
                          const steps = [...selectedTemplate.steps];
                          steps[idx] = { ...step, displayName: e.target.value };
                          setSelectedTemplate({ ...selectedTemplate, steps });
                        }}
                      />
                    </td>
                    <td className="p-2">
                      <select
                        className="w-full px-2 py-1 border border-border rounded bg-input-background"
                        value={step.conditionType}
                        onChange={(e) => {
                          const steps = [...selectedTemplate.steps];
                          steps[idx] = { ...step, conditionType: e.target.value };
                          setSelectedTemplate({ ...selectedTemplate, steps });
                        }}
                      >
                        <option value="Always">Always</option>
                        <option value="FinanceRelated">Finance related</option>
                        <option value="BankSignatory">Bank signatory</option>
                        <option value="Applicable">If applicable</option>
                        <option value="LoaHolders">LOA holders</option>
                        <option value="BoardApproval">Board approval</option>
                      </select>
                    </td>
                    <td className="p-2">
                      <select
                        className="w-full px-2 py-1 border border-border rounded bg-input-background"
                        value={step.assigneeType}
                        onChange={(e) => {
                          const steps = [...selectedTemplate.steps];
                          steps[idx] = { ...step, assigneeType: e.target.value };
                          setSelectedTemplate({ ...selectedTemplate, steps });
                        }}
                      >
                        <option value="JobTitle">Job title</option>
                        <option value="NamedUser">Internal signatory (named)</option>
                        <option value="InternalSignatory">Internal signatory (account)</option>
                        <option value="DivisionRecommender">Division recommender</option>
                        <option value="ProjectInitiator">Project initiator</option>
                        <option value="LoaHolders">LOA holders</option>
                        <option value="BoardMembers">Board members</option>
                        <option value="ExternalName">External name</option>
                      </select>
                    </td>
                    <td className="p-2">
                      {step.assigneeType === 'DivisionRecommender' ? (
                        <span className="text-xs text-muted-foreground italic">
                          Uses division group recommenders below
                        </span>
                      ) : step.assigneeType === 'NamedUser' || step.assigneeType === 'InternalSignatory' ? (
                        <select
                          className="w-full px-2 py-1 border border-border rounded bg-input-background"
                          value={step.assigneeUserId ?? ''}
                          onChange={(e) => {
                            const raw = e.target.value;
                            const steps = [...selectedTemplate.steps];
                            if (!raw) {
                              steps[idx] = { ...step, assigneeUserId: undefined };
                            } else {
                              const userId = Number(raw);
                              const user = internalUsers.find((u) => u.userId === userId);
                              steps[idx] = {
                                ...step,
                                assigneeUserId: userId,
                                assigneeDisplayName: user?.name ?? step.assigneeDisplayName,
                              };
                            }
                            setSelectedTemplate({ ...selectedTemplate, steps });
                          }}
                        >
                          <option value="">Select internal signatory…</option>
                          {internalUsers
                            .filter((u) => u.isInternalSignatory || step.assigneeType === 'NamedUser')
                            .map((u) => (
                              <option key={u.userId} value={u.userId}>
                                {u.name} ({u.email})
                              </option>
                            ))}
                        </select>
                      ) : (
                        <input
                          className="w-full px-2 py-1 border border-border rounded bg-input-background"
                          value={step.assigneeDisplayName || step.assigneeRole || ''}
                          onChange={(e) => {
                            const steps = [...selectedTemplate.steps];
                            steps[idx] = {
                              ...step,
                              assigneeDisplayName: e.target.value,
                              assigneeRole: step.assigneeType === 'JobTitle' ? e.target.value : step.assigneeRole,
                            };
                            setSelectedTemplate({ ...selectedTemplate, steps });
                          }}
                        />
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <button
            type="button"
            disabled={saving}
            onClick={() => void saveTemplate()}
            className="px-4 py-2 bg-primary text-primary-foreground rounded-lg text-sm disabled:opacity-50"
          >
            Save workflow template
          </button>
        </div>
      )}

      <div className="border-t border-border pt-6">
        <h4 className="font-medium mb-1">Division groups &amp; recommenders</h4>
        <p className="text-sm text-muted-foreground mb-3">
          Recommenders approve MOI/MOA steps when a workflow step uses assignee type &quot;Division recommender&quot;.
          Link to an internal user account when possible, or enter a display name only.
        </p>
        <div className="space-y-4 max-h-[32rem] overflow-y-auto">
          {groups.map((group) => (
            <div key={group.id} className="border border-border rounded-lg p-3 space-y-3">
              <div className="flex flex-wrap gap-2 items-center">
                <span className="font-medium">{group.name}</span>
                <span className="text-xs text-muted-foreground">({group.code})</span>
                <select
                  className="ml-auto text-sm px-2 py-1 border border-border rounded"
                  value={group.moaWorkflowTemplateCode}
                  onChange={(e) => patchGroup(group.id, { moaWorkflowTemplateCode: e.target.value })}
                >
                  {templates.map((t) => (
                    <option key={t.code} value={t.code}>{t.name}</option>
                  ))}
                </select>
                <button
                  type="button"
                  disabled={saving}
                  onClick={() => void saveGroup(group)}
                  className="text-sm px-2 py-1 border border-border rounded hover:bg-muted"
                >
                  Save group
                </button>
              </div>

              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <span className="text-sm font-medium">Recommenders</span>
                  <button
                    type="button"
                    onClick={() => addRecommender(group.id)}
                    className="flex items-center gap-1 text-xs px-2 py-1 border border-border rounded hover:bg-muted"
                  >
                    <Plus className="w-3 h-3" />
                    Add
                  </button>
                </div>

                {group.recommenders.length === 0 ? (
                  <p className="text-xs text-muted-foreground">No recommenders — add at least one for this division.</p>
                ) : (
                  <div className="space-y-2">
                    {group.recommenders.map((rec, idx) => (
                      <div key={`${group.id}-${rec.id}-${idx}`} className="flex flex-wrap gap-2 items-center">
                        <input
                          className="flex-1 min-w-[8rem] text-sm px-2 py-1 border border-border rounded bg-input-background"
                          placeholder="Display name"
                          value={rec.displayName}
                          onChange={(e) => updateRecommender(group.id, idx, { displayName: e.target.value })}
                        />
                        <select
                          className="flex-1 min-w-[10rem] text-sm px-2 py-1 border border-border rounded bg-input-background"
                          value={rec.userId ?? ''}
                          onChange={(e) => {
                            const raw = e.target.value;
                            if (!raw) {
                              updateRecommender(group.id, idx, { userId: undefined });
                              return;
                            }
                            const userId = Number(raw);
                            const user = internalUsers.find((u) => u.userId === userId);
                            updateRecommender(group.id, idx, {
                              userId,
                              displayName: user?.name || rec.displayName,
                            });
                          }}
                        >
                          <option value="">No linked user</option>
                          {internalUsers.map((u) => (
                            <option key={u.userId} value={u.userId}>
                              {u.name} ({u.email})
                            </option>
                          ))}
                        </select>
                        <button
                          type="button"
                          onClick={() => removeRecommender(group.id, idx)}
                          className="p-1.5 text-destructive hover:bg-destructive/10 rounded"
                          title="Remove recommender"
                        >
                          <Trash2 className="w-4 h-4" />
                        </button>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
