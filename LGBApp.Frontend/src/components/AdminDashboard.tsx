import { useCallback, useEffect, useMemo, useState } from 'react';
import { AlertCircle } from 'lucide-react';
import { AdminPackageOverview } from './AdminPackageOverview';
import { CompletedServicesTable } from './CompletedServicesTable';
import { MyWorkTracker } from './MyWorkTracker';
import { StatsCards } from './StatsCards';
import { WorkQueueDragHandle } from './WorkQueueDragHandle';
import { WorkQueueOrderHint } from './WorkQueueOrderHint';
import { useWorkQueueOrder } from '@/hooks/useWorkQueueOrder';
import {
  ApiError,
  getJobRequests,
  type CustomerPackageDto,
  type CustomerResponse,
  type JobRequestResponse,
  type UserResponse,
} from '@/lib/api';
import { canAssignSecretarialTeam } from '@/lib/packageItemStatus';
import { formatQueueDate, parseQueueSortDate } from '@/lib/workQueueOrder';

interface AdminDashboardProps {
  refreshKey?: number;
  currentUser: UserResponse;
  onManagePackage: (customer: CustomerResponse, pkg: CustomerPackageDto) => void;
  onOpenTask: (jobId: number) => void;
  onViewHistory: () => void;
  onError: (message: string) => void;
  onSuccess: () => void;
}

function attentionLabel(job: JobRequestResponse): string {
  if (job.awaitingIntakeApproval || job.units?.some((u) => u.awaitingIntakeApproval))
    return 'MOI submitted — review intake';
  if (job.internalHandoffStatus === 'AdminReview')
    return 'MOA ready for head secretary review';
  if (job.internalHandoffStatus === 'MoaSharonApproved')
    return 'MOA approved — send to client';
  if (canAssignSecretarialTeam(job, []))
    return 'Assign secretarial team';
  return 'Needs attention';
}

function attentionDate(job: JobRequestResponse): string {
  const awaitingUnit = job.units?.find((u) => u.awaitingIntakeApproval);
  return formatQueueDate(
    awaitingUnit?.scheduledDate,
    job.scheduledDate,
    job.dateRequested,
  );
}

function attentionSortDate(job: JobRequestResponse): number {
  const awaitingUnit = job.units?.find((u) => u.awaitingIntakeApproval);
  return parseQueueSortDate(
    awaitingUnit?.scheduledDate,
    job.scheduledDate,
    job.dateRequested,
  );
}

export function AdminDashboard({
  refreshKey = 0,
  currentUser,
  onManagePackage,
  onOpenTask,
  onViewHistory,
  onError,
  onSuccess,
}: AdminDashboardProps) {
  const [attentionJobs, setAttentionJobs] = useState<JobRequestResponse[]>([]);
  const [loadingAttention, setLoadingAttention] = useState(true);
  const [draggingKey, setDraggingKey] = useState<string | null>(null);
  const [dropTargetKey, setDropTargetKey] = useState<string | null>(null);

  const loadAttention = useCallback(async () => {
    setLoadingAttention(true);
    try {
      const jobs = await getJobRequests();
      const filtered = jobs.filter((job) => {
        const unitAwaitingIntake = job.units?.some((u) => u.awaitingIntakeApproval) ?? false;
        if ((job.awaitingIntakeApproval || unitAwaitingIntake) && currentUser.canApproveMoiIntake)
          return true;
        if (job.internalHandoffStatus === 'AdminReview' && currentUser.canApproveMoa)
          return true;
        if (job.internalHandoffStatus === 'MoaSharonApproved'
          && (currentUser.canApproveMoa || currentUser.role === 'Admin'))
          return true;
        if (canAssignSecretarialTeam(job, jobs) && currentUser.role === 'Admin')
          return true;
        return false;
      });
      setAttentionJobs(filtered);
    } catch (err) {
      onError(err instanceof ApiError ? err.message : 'Failed to load action queue.');
      setAttentionJobs([]);
    } finally {
      setLoadingAttention(false);
    }
  }, [currentUser, onError]);

  useEffect(() => {
    void loadAttention();
  }, [loadAttention, refreshKey]);

  const getAttentionKey = useCallback((job: JobRequestResponse) => `job-${job.id}`, []);

  const {
    sortedItems: orderedAttentionJobs,
    moveItem,
    resetOrder,
    hasCustomOrder,
  } = useWorkQueueOrder(
    currentUser.userId,
    'attention',
    attentionJobs,
    getAttentionKey,
    attentionSortDate,
  );

  const attentionSummary = useMemo(() => {
    if (loadingAttention) return 'Loading…';
    if (orderedAttentionJobs.length === 0) return 'No items waiting on you right now.';
    return `${orderedAttentionJobs.length} item${orderedAttentionJobs.length === 1 ? '' : 's'} need your action`;
  }, [orderedAttentionJobs.length, loadingAttention]);

  const handleDrop = (targetKey: string) => {
    if (draggingKey) moveItem(draggingKey, targetKey);
    setDraggingKey(null);
    setDropTargetKey(null);
  };

  return (
    <div className="space-y-8">
      <div>
        <h2 className="text-xl font-semibold">Operations</h2>
        <p className="text-sm text-muted-foreground mt-1">
          Your work queue, packages needing action, and company-wide package overview.
        </p>
      </div>

      <section className="bg-card border border-border rounded-lg overflow-hidden">
        <div className="p-4 border-b border-border flex items-center gap-2">
          <AlertCircle className="w-5 h-5 text-amber-600" />
          <div className="flex-1">
            <h3 className="font-medium">Needs your attention</h3>
            <p className="text-xs text-muted-foreground">{attentionSummary}</p>
            {orderedAttentionJobs.length > 0 && (
              <WorkQueueOrderHint hasCustomOrder={hasCustomOrder} onReset={resetOrder} />
            )}
          </div>
        </div>
        {orderedAttentionJobs.length === 0 ? (
          <p className="p-6 text-sm text-muted-foreground">
            {loadingAttention ? 'Loading action queue…' : 'You are all caught up on intake, MOA review, and assignments.'}
          </p>
        ) : (
          <ul className="divide-y divide-border">
            {orderedAttentionJobs.map((job) => {
              const key = getAttentionKey(job);
              const isDragging = draggingKey === key;
              const isDropTarget = dropTargetKey === key && draggingKey !== key;
              return (
                <li
                  key={key}
                  draggable
                  onDragStart={() => setDraggingKey(key)}
                  onDragEnd={() => {
                    setDraggingKey(null);
                    setDropTargetKey(null);
                  }}
                  onDragOver={(e) => {
                    e.preventDefault();
                    setDropTargetKey(key);
                  }}
                  onDragLeave={() => {
                    if (dropTargetKey === key) setDropTargetKey(null);
                  }}
                  onDrop={(e) => {
                    e.preventDefault();
                    handleDrop(key);
                  }}
                  className={`flex items-center gap-3 px-4 py-3 text-sm transition-colors ${
                    isDragging ? 'opacity-50 bg-muted/40' : ''
                  } ${isDropTarget ? 'bg-primary/5 ring-1 ring-inset ring-primary/30' : ''}`}
                >
                  <WorkQueueDragHandle />
                  <div className="w-24 shrink-0">
                    <p className="text-xs text-muted-foreground">Date</p>
                    <p className="font-medium tabular-nums">{attentionDate(job)}</p>
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="font-medium truncate">
                      {job.customer} — {job.taskType === 'Service' ? job.service : job.taskType}
                    </p>
                    <p className="text-xs text-muted-foreground mt-0.5">{attentionLabel(job)}</p>
                  </div>
                  <button
                    type="button"
                    onClick={() => onOpenTask(job.id)}
                    className="shrink-0 px-3 py-1.5 text-xs border border-border rounded-lg hover:bg-muted"
                  >
                    Open
                  </button>
                </li>
              );
            })}
          </ul>
        )}
      </section>

      <MyWorkTracker
        refreshKey={refreshKey}
        userId={currentUser.userId}
        onOpenTask={onOpenTask}
        onError={onError}
        onSuccess={onSuccess}
      />

      <AdminPackageOverview refreshKey={refreshKey} onManagePackage={onManagePackage} />

      <StatsCards refreshKey={refreshKey} />

      <CompletedServicesTable refreshKey={refreshKey} onViewHistory={onViewHistory} />
    </div>
  );
}
