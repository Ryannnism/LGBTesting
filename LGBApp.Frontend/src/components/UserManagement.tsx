import { Users, Plus, Edit, Trash2, X, ChevronDown } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import {
  ApiError,
  addClientSignatory,
  deleteUser,
  getCustomers,
  getUsers,
  updateUser,
  type CustomerResponse,
  type UserResponse,
} from '@/lib/api';
import { ROLES, roleLabel } from '@/lib/roles';

interface UserManagementProps {
  onCreateUser: () => void;
  refreshKey?: number;
  mode?: 'internal' | 'clientTeam';
  title?: string;
  description?: string;
}

const EXTERNAL_ROLES = [ROLES.ClientAdmin, ROLES.ClientSignatory];

type UserViewFilter = 'all' | 'internal-admin' | 'internal-sec' | 'external';

interface EditFormState {
  name: string;
  email: string;
  mobile: string;
  role: string;
  jobTitle: string;
  canRecommendMoi: boolean;
  canApproveMoiIntake: boolean;
  canApproveMoi: boolean;
  canApproveMoa: boolean;
  isInternalSignatory: boolean;
  customerId?: number;
}

function isInternalAdmin(user: UserResponse) {
  return user.role === ROLES.Admin;
}

function isInternalSecretary(user: UserResponse) {
  return user.role === ROLES.User;
}

function isExternalUser(user: UserResponse) {
  return EXTERNAL_ROLES.includes(user.role as (typeof EXTERNAL_ROLES)[number]);
}

function externalSortRank(user: UserResponse) {
  if (user.role === ROLES.ClientAdmin) return 0;
  if (user.role === ROLES.ClientSignatory) return 1;
  return 2;
}

export function UserManagement({
  onCreateUser,
  refreshKey = 0,
  mode = 'internal',
  title = 'User Management',
  description,
}: UserManagementProps) {
  const isClientTeamMode = mode === 'clientTeam';
  const [users, setUsers] = useState<UserResponse[]>([]);
  const [customers, setCustomers] = useState<CustomerResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [viewFilter, setViewFilter] = useState<UserViewFilter>('all');
  const [customerFilter, setCustomerFilter] = useState<number | 'all'>('all');
  const [editingUser, setEditingUser] = useState<UserResponse | null>(null);
  const [editForm, setEditForm] = useState<EditFormState>({
    name: '',
    email: '',
    mobile: '',
    role: ROLES.User,
    jobTitle: '',
    canRecommendMoi: false,
    canApproveMoiIntake: false,
    canApproveMoi: false,
    canApproveMoa: false,
    isInternalSignatory: false,
    customerId: undefined,
  });
  const [saving, setSaving] = useState(false);
  const [showAddSignatory, setShowAddSignatory] = useState(false);
  const [signatoryForm, setSignatoryForm] = useState({
    name: '',
    email: '',
    phone: '',
    moi: true,
    moiApproval: false,
    moa: false,
  });
  const [addingSignatory, setAddingSignatory] = useState(false);

  const loadUsers = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const userData = await getUsers();
      setUsers(userData);
      if (!isClientTeamMode) {
        const customerData = await getCustomers();
        setCustomers(customerData);
      }
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        setError('Sign in as Admin to manage users.');
      } else if (err instanceof ApiError && err.status === 403) {
        setError('Admin role required to view users.');
      } else {
        setError('Could not load users. Make sure the backend is running.');
      }
    } finally {
      setLoading(false);
    }
  }, [isClientTeamMode]);

  useEffect(() => {
    void loadUsers();
  }, [loadUsers, refreshKey]);

  const externalCustomerIds = useMemo(() => {
    const ids = new Set<number>();
    users.filter(isExternalUser).forEach((u) => {
      if (u.customerId) ids.add(u.customerId);
    });
    return [...ids].sort((a, b) => {
      const nameA = customers.find((c) => c.id === a)?.company ?? '';
      const nameB = customers.find((c) => c.id === b)?.company ?? '';
      return nameA.localeCompare(nameB);
    });
  }, [users, customers]);

  const filteredUsers = useMemo(() => {
    if (isClientTeamMode) return users;

    let list = users;
    if (viewFilter === 'internal-admin') {
      list = users.filter(isInternalAdmin);
    } else if (viewFilter === 'internal-sec') {
      list = users.filter(isInternalSecretary);
    } else if (viewFilter === 'external') {
      list = users.filter(isExternalUser);
      if (customerFilter !== 'all') {
        list = list.filter((u) => u.customerId === customerFilter);
      }
    }

    return [...list].sort((a, b) => {
      if (viewFilter === 'external' || (viewFilter === 'all' && isExternalUser(a) && isExternalUser(b))) {
        const rank = externalSortRank(a) - externalSortRank(b);
        if (rank !== 0) return rank;
      }
      return a.name.localeCompare(b.name);
    });
  }, [users, viewFilter, customerFilter, isClientTeamMode]);

  const groupedSections = useMemo(() => {
    if (isClientTeamMode || viewFilter !== 'all') {
      return [{ key: 'list', label: '', users: filteredUsers }];
    }

    const admins = users.filter(isInternalAdmin).sort((a, b) => a.name.localeCompare(b.name));
    const secretaries = users.filter(isInternalSecretary).sort((a, b) => a.name.localeCompare(b.name));
    const externalByCustomer = externalCustomerIds.map((customerId) => {
      const company = customers.find((c) => c.id === customerId)?.company
        ?? users.find((u) => u.customerId === customerId)?.customerName
        ?? `Customer #${customerId}`;
      const members = users
        .filter((u) => u.customerId === customerId && isExternalUser(u))
        .sort((a, b) => {
          const rank = externalSortRank(a) - externalSortRank(b);
          return rank !== 0 ? rank : a.name.localeCompare(b.name);
        });
      return { key: `customer-${customerId}`, label: company, users: members };
    });

    const sections: { key: string; label: string; users: UserResponse[] }[] = [];
    if (admins.length) sections.push({ key: 'admins', label: 'Internal — Admin', users: admins });
    if (secretaries.length) sections.push({ key: 'secretaries', label: 'Internal — Secretarial', users: secretaries });
    externalByCustomer.forEach((g) => {
      if (g.users.length) sections.push({ key: g.key, label: `External — ${g.label}`, users: g.users });
    });
    return sections;
  }, [users, customers, externalCustomerIds, filteredUsers, viewFilter, isClientTeamMode]);

  const handleEdit = (user: UserResponse) => {
    setEditingUser(user);
    setEditForm({
      name: user.name,
      email: user.email,
      mobile: user.mobile,
      role: user.role,
      jobTitle: user.jobTitle ?? '',
      canRecommendMoi: Boolean(user.canRecommendMoi),
      canApproveMoiIntake: Boolean(user.canApproveMoiIntake),
      canApproveMoi: Boolean(user.canApproveMoi),
      canApproveMoa: Boolean(user.canApproveMoa),
      isInternalSignatory: Boolean(user.isInternalSignatory),
      customerId: user.customerId,
    });
  };

  const isExternalRole = EXTERNAL_ROLES.includes(editForm.role as (typeof EXTERNAL_ROLES)[number]);

  const handleSaveEdit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingUser) return;
    if (isExternalRole && !editForm.customerId) {
      setError('Select a customer for external users.');
      return;
    }
    setSaving(true);
    try {
      await updateUser(editingUser.userId, {
        email: editForm.email,
        name: editForm.name,
        mobile: editForm.mobile,
        role: editForm.role,
        jobTitle: isExternalRole || isClientTeamMode ? undefined : editForm.jobTitle || undefined,
        canRecommendMoi: isExternalRole || isClientTeamMode ? undefined : editForm.canRecommendMoi,
        canApproveMoiIntake: isExternalRole || isClientTeamMode ? undefined : editForm.canApproveMoiIntake,
        canApproveMoi: isExternalRole || isClientTeamMode ? undefined : editForm.canApproveMoi,
        canApproveMoa: isExternalRole || isClientTeamMode ? undefined : editForm.canApproveMoa,
        isInternalSignatory: isExternalRole || isClientTeamMode ? undefined : editForm.isInternalSignatory,
        customerId: isExternalRole || isClientTeamMode ? editForm.customerId : undefined,
      });
      setEditingUser(null);
      setError('');
      await loadUsers();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to update user.');
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (user: UserResponse) => {
    if (!window.confirm(`Delete user ${user.name}?`)) return;
    try {
      await deleteUser(user.userId);
      setError('');
      await loadUsers();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to delete user.');
    }
  };

  const customerName = (customerId?: number) =>
    customers.find((c) => c.id === customerId)?.company ?? (customerId ? `Customer #${customerId}` : '—');

  const renderUserRow = (user: UserResponse) => (
    <tr key={user.userId} className="border-t border-border hover:bg-muted/30 transition-colors">
      <td className="px-4 py-3 font-medium">{user.name}</td>
      <td className="px-4 py-3">{user.email}</td>
      <td className="px-4 py-3">{user.mobile || '—'}</td>
      <td className="px-4 py-3">{roleLabel(user.role)}</td>
      {!isClientTeamMode && (
        <td className="px-4 py-3">{user.customerName ?? customerName(user.customerId)}</td>
      )}
      <td className="px-4 py-3">
        <div className="flex items-center justify-center gap-2">
          <button type="button" onClick={() => handleEdit(user)} className="p-1 hover:bg-muted rounded transition-colors">
            <Edit className="w-4 h-4" />
          </button>
          <button
            type="button"
            onClick={() => void handleDelete(user)}
            className="p-1 hover:bg-destructive/10 text-destructive rounded transition-colors"
          >
            <Trash2 className="w-4 h-4" />
          </button>
        </div>
      </td>
    </tr>
  );

  const colSpan = isClientTeamMode ? 5 : 6;

  return (
    <>
      <div className="bg-card rounded-lg border border-border overflow-hidden">
        <div className="p-4 border-b border-border flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-2">
            <Users className="w-5 h-5 text-muted-foreground" />
            <div>
              <h2>{title}</h2>
              {description && <p className="text-xs text-muted-foreground mt-0.5">{description}</p>}
            </div>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            {!isClientTeamMode && (
              <>
                <div className="relative">
                  <select
                    value={viewFilter}
                    onChange={(e) => {
                      setViewFilter(e.target.value as UserViewFilter);
                      if (e.target.value !== 'external') setCustomerFilter('all');
                    }}
                    className="appearance-none pl-3 pr-8 py-2 text-sm border border-border rounded-lg bg-input-background"
                  >
                    <option value="all">All users (grouped)</option>
                    <option value="internal-admin">Internal — Admin</option>
                    <option value="internal-sec">Internal — Secretarial</option>
                    <option value="external">External — all customers</option>
                  </select>
                  <ChevronDown className="w-4 h-4 absolute right-2 top-1/2 -translate-y-1/2 pointer-events-none text-muted-foreground" />
                </div>
                {viewFilter === 'external' && (
                  <div className="relative">
                    <select
                      value={customerFilter === 'all' ? '' : String(customerFilter)}
                      onChange={(e) => setCustomerFilter(e.target.value ? Number(e.target.value) : 'all')}
                      className="appearance-none pl-3 pr-8 py-2 text-sm border border-border rounded-lg bg-input-background max-w-[200px]"
                    >
                      <option value="">All customers</option>
                      {externalCustomerIds.map((id) => (
                        <option key={id} value={id}>{customerName(id)}</option>
                      ))}
                    </select>
                    <ChevronDown className="w-4 h-4 absolute right-2 top-1/2 -translate-y-1/2 pointer-events-none text-muted-foreground" />
                  </div>
                )}
              </>
            )}
            {isClientTeamMode && (
              <button
                type="button"
                onClick={() => setShowAddSignatory(true)}
                className="flex items-center gap-2 px-4 py-2 border border-border rounded-lg hover:bg-muted transition-colors"
              >
                <Plus className="w-4 h-4" />
                Add signatory
              </button>
            )}
            <button
              onClick={onCreateUser}
              className="flex items-center gap-2 px-4 py-2 bg-primary text-primary-foreground rounded-lg hover:bg-primary/90 transition-colors"
            >
              <Plus className="w-4 h-4" />
              {isClientTeamMode ? 'Invite client admin' : 'Add User'}
            </button>
          </div>
        </div>

        {error && (
          <div className="px-4 py-3 text-sm text-destructive bg-destructive/10 border-b border-border">
            {error}
          </div>
        )}

        <div className="overflow-auto">
          <table className="w-full">
            <thead className="bg-muted/50">
              <tr>
                <th className="px-4 py-3 text-left">Name</th>
                <th className="px-4 py-3 text-left">Email</th>
                <th className="px-4 py-3 text-left">Mobile</th>
                <th className="px-4 py-3 text-left">Role</th>
                {!isClientTeamMode && <th className="px-4 py-3 text-left">Customer</th>}
                <th className="px-4 py-3 text-center">Actions</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={colSpan} className="px-4 py-8 text-center text-muted-foreground">
                    Loading users...
                  </td>
                </tr>
              ) : filteredUsers.length === 0 ? (
                <tr>
                  <td colSpan={colSpan} className="px-4 py-8 text-center text-muted-foreground">
                    {isClientTeamMode ? 'No team members yet. Invite your first user.' : 'No users in this view.'}
                  </td>
                </tr>
              ) : (
                groupedSections.map((section) => (
                  section.users.length === 0 ? null : (
                    <SectionRows
                      key={section.key}
                      label={section.label}
                      users={section.users}
                      colSpan={colSpan}
                      renderRow={renderUserRow}
                    />
                  )
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {showAddSignatory && isClientTeamMode && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
          <div className="bg-card rounded-lg border border-border w-full max-w-md">
            <div className="p-4 border-b border-border flex items-center justify-between">
              <h3>Add signatory</h3>
              <button type="button" onClick={() => setShowAddSignatory(false)} className="p-1 hover:bg-muted rounded">
                <X className="w-5 h-5" />
              </button>
            </div>
            <form
              className="p-4 space-y-3"
              onSubmit={(e) => {
                e.preventDefault();
                setAddingSignatory(true);
                void addClientSignatory(signatoryForm)
                  .then(() => {
                    setShowAddSignatory(false);
                    setSignatoryForm({ name: '', email: '', phone: '', moi: true, moiApproval: false, moa: false });
                    return loadUsers();
                  })
                  .catch((err) => setError(err instanceof ApiError ? err.message : 'Failed to add signatory.'))
                  .finally(() => setAddingSignatory(false));
              }}
            >
              <p className="text-xs text-muted-foreground">
                Creates a signatory login scoped to your company. LGB admin will see this as client-added.
              </p>
              <input
                required
                className="w-full px-3 py-2 border border-border rounded-lg"
                placeholder="Full name"
                value={signatoryForm.name}
                onChange={(e) => setSignatoryForm({ ...signatoryForm, name: e.target.value })}
              />
              <input
                required
                type="email"
                className="w-full px-3 py-2 border border-border rounded-lg"
                placeholder="Email"
                value={signatoryForm.email}
                onChange={(e) => setSignatoryForm({ ...signatoryForm, email: e.target.value })}
              />
              <input
                className="w-full px-3 py-2 border border-border rounded-lg"
                placeholder="Phone"
                value={signatoryForm.phone}
                onChange={(e) => setSignatoryForm({ ...signatoryForm, phone: e.target.value })}
              />
              <div className="flex flex-wrap gap-4 text-sm">
                <label className="flex items-center gap-2">
                  <input type="checkbox" checked={signatoryForm.moi} onChange={(e) => setSignatoryForm({ ...signatoryForm, moi: e.target.checked })} />
                  MOI
                </label>
                <label className="flex items-center gap-2">
                  <input type="checkbox" checked={signatoryForm.moiApproval} onChange={(e) => setSignatoryForm({ ...signatoryForm, moiApproval: e.target.checked })} />
                  MOI Approval
                </label>
                <label className="flex items-center gap-2">
                  <input type="checkbox" checked={signatoryForm.moa} onChange={(e) => setSignatoryForm({ ...signatoryForm, moa: e.target.checked })} />
                  MOA
                </label>
              </div>
              <div className="flex justify-end gap-2 pt-2">
                <button type="button" onClick={() => setShowAddSignatory(false)} className="px-4 py-2 border rounded-lg">Cancel</button>
                <button type="submit" disabled={addingSignatory} className="px-4 py-2 bg-primary text-primary-foreground rounded-lg">
                  {addingSignatory ? 'Adding…' : 'Add signatory'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {editingUser && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
          <div className="bg-card rounded-lg border border-border w-full max-w-md max-h-[90vh] overflow-y-auto">
            <div className="p-4 border-b border-border flex items-center justify-between">
              <h3>Edit User</h3>
              <button type="button" onClick={() => setEditingUser(null)} className="p-1 hover:bg-muted rounded">
                <X className="w-5 h-5" />
              </button>
            </div>
            <form onSubmit={(e) => void handleSaveEdit(e)} className="p-4 space-y-3">
              <input
                type="text"
                required
                value={editForm.name}
                onChange={(e) => setEditForm({ ...editForm, name: e.target.value })}
                className="w-full px-3 py-2 border border-border rounded-lg"
                placeholder="Name"
              />
              <input
                type="email"
                required
                value={editForm.email}
                onChange={(e) => setEditForm({ ...editForm, email: e.target.value })}
                className="w-full px-3 py-2 border border-border rounded-lg"
                placeholder="Email"
              />
              <input
                type="tel"
                value={editForm.mobile}
                onChange={(e) => setEditForm({ ...editForm, mobile: e.target.value })}
                className="w-full px-3 py-2 border border-border rounded-lg"
                placeholder="Mobile"
              />
              {!isClientTeamMode ? (
                <select
                  value={editForm.role}
                  onChange={(e) =>
                    setEditForm({
                      ...editForm,
                      role: e.target.value,
                      customerId: EXTERNAL_ROLES.includes(e.target.value as (typeof EXTERNAL_ROLES)[number])
                        ? editForm.customerId
                        : undefined,
                    })
                  }
                  className="w-full px-3 py-2 border border-border rounded-lg"
                >
                  <option value={ROLES.Admin}>{roleLabel(ROLES.Admin)}</option>
                  <option value={ROLES.User}>{roleLabel(ROLES.User)}</option>
                  <option value={ROLES.ClientAdmin}>{roleLabel(ROLES.ClientAdmin)}</option>
                  <option value={ROLES.ClientSignatory}>{roleLabel(ROLES.ClientSignatory)}</option>
                </select>
              ) : (
                <input type="text" disabled value={roleLabel(editForm.role)} className="w-full px-3 py-2 border border-border rounded-lg bg-muted/30" />
              )}

              {isExternalRole && !isClientTeamMode ? (
                <select
                  required
                  value={editForm.customerId ?? ''}
                  onChange={(e) =>
                    setEditForm({
                      ...editForm,
                      customerId: e.target.value ? Number(e.target.value) : undefined,
                    })
                  }
                  className="w-full px-3 py-2 border border-border rounded-lg"
                >
                  <option value="">Select customer…</option>
                  {customers.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.company}
                    </option>
                  ))}
                </select>
              ) : (
                <>
                  <input
                    type="text"
                    value={editForm.jobTitle}
                    onChange={(e) => setEditForm({ ...editForm, jobTitle: e.target.value })}
                    className="w-full px-3 py-2 border border-border rounded-lg"
                    placeholder="Job title"
                  />
                  <label className="flex items-center gap-2 text-sm">
                    <input
                      type="checkbox"
                      checked={editForm.canRecommendMoi}
                      onChange={(e) => setEditForm({ ...editForm, canRecommendMoi: e.target.checked })}
                    />
                    Can recommend MOI
                  </label>
                  <label className="flex items-center gap-2 text-sm">
                    <input
                      type="checkbox"
                      checked={editForm.canApproveMoiIntake}
                      onChange={(e) => setEditForm({ ...editForm, canApproveMoiIntake: e.target.checked })}
                    />
                    Can approve MOI intake
                  </label>
                  <label className="flex items-center gap-2 text-sm">
                    <input
                      type="checkbox"
                      checked={editForm.canApproveMoi}
                      onChange={(e) => setEditForm({ ...editForm, canApproveMoi: e.target.checked })}
                    />
                    Can sign off MOI (sees MOI before assignment)
                  </label>
                  <label className="flex items-center gap-2 text-sm">
                    <input
                      type="checkbox"
                      checked={editForm.canApproveMoa}
                      onChange={(e) => setEditForm({ ...editForm, canApproveMoa: e.target.checked })}
                    />
                    Can sign off MOA (sees MOA before assignment)
                  </label>
                  <label className="flex items-center gap-2 text-sm">
                    <input
                      type="checkbox"
                      checked={editForm.isInternalSignatory}
                      onChange={(e) => setEditForm({ ...editForm, isInternalSignatory: e.target.checked })}
                    />
                    MOA workflow signatory (internal named approver)
                  </label>
                </>
              )}

              <div className="flex justify-end gap-2 pt-2">
                <button type="button" onClick={() => setEditingUser(null)} className="px-4 py-2 border rounded-lg">
                  Cancel
                </button>
                <button type="submit" disabled={saving} className="px-4 py-2 bg-primary text-primary-foreground rounded-lg">
                  {saving ? 'Saving...' : 'Save'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
  );
}

function SectionRows({
  label,
  users,
  colSpan,
  renderRow,
}: {
  label: string;
  users: UserResponse[];
  colSpan: number;
  renderRow: (user: UserResponse) => ReactNode;
}) {
  if (!label) {
    return <>{users.map(renderRow)}</>;
  }
  return (
    <>
      <tr className="bg-muted/40">
        <td colSpan={colSpan} className="px-4 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          {label}
        </td>
      </tr>
      {users.map(renderRow)}
    </>
  );
}
