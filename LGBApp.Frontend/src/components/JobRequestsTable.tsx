import { Briefcase, Edit } from 'lucide-react';
import { useCallback, useEffect, useState } from 'react';
import { WorkQueueDragHandle } from './WorkQueueDragHandle';
import { WorkQueueOrderHint } from './WorkQueueOrderHint';
import { useWorkQueueOrder } from '@/hooks/useWorkQueueOrder';
import { ApiError, getJobRequests, updateJobRequest, type JobRequestResponse } from '@/lib/api';
import { formatQueueDate, parseQueueSortDate } from '@/lib/workQueueOrder';

type JobRequest = JobRequestResponse;

interface JobRequestsTableProps {
  onViewJob: (job: JobRequest) => void;
  onOpenTask?: (job: JobRequest) => void;
  refreshKey?: number;
  onActionError?: (message: string) => void;
  onActionSuccess?: () => void;
  isAdmin?: boolean;
  userId?: number;
}

function jobKey(job: JobRequest): string {
  return `job-${job.id}`;
}

function jobSortDate(job: JobRequest): number {
  return parseQueueSortDate(job.scheduledDate, job.dateRequested);
}

function jobDisplayDate(job: JobRequest): string {
  return formatQueueDate(job.scheduledDate, job.dateRequested);
}

export function JobRequestsTable({
  onViewJob,
  onOpenTask,
  refreshKey = 0,
  onActionError,
  onActionSuccess,
  isAdmin = false,
  userId,
}: JobRequestsTableProps) {
  const [jobRequests, setJobRequests] = useState<JobRequest[]>([]);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [draggingKey, setDraggingKey] = useState<string | null>(null);
  const [dropTargetKey, setDropTargetKey] = useState<string | null>(null);

  useEffect(() => {
    setLoading(true);
    setError('');
    getJobRequests()
      .then((data) => {
        setJobRequests(data);
      })
      .catch((err) => {
        setJobRequests([]);
        setError(err instanceof ApiError ? err.message : 'Failed to load job requests.');
      })
      .finally(() => setLoading(false));
  }, [refreshKey]);

  const getKey = useCallback((job: JobRequest) => jobKey(job), []);

  const {
    sortedItems: orderedJobs,
    moveItem,
    resetOrder,
    hasCustomOrder,
  } = useWorkQueueOrder(userId, 'job-requests', jobRequests, getKey, jobSortDate);

  const handleStatusChange = async (job: JobRequest, newStatus: 'Completed' | 'Canceled') => {
    try {
      await updateJobRequest(job.id, { ...job, status: newStatus });
      setJobRequests((prev) => prev.filter((j) => j.id !== job.id));
      setEditingId(null);
      onActionSuccess?.();
    } catch (err) {
      onActionError?.(err instanceof ApiError ? err.message : 'Failed to update job status.');
    }
  };

  const formTaskTypes = ['MOI', 'MOI Approval', 'MOA'];

  const canOpenFormTask = (job: JobRequest) => {
    if (!onOpenTask || !formTaskTypes.includes(job.taskType)) return false;
    if (isAdmin) return job.status === 'Pending' || job.status === 'In Progress';
    return job.status === 'In Progress';
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'Pending': return 'bg-yellow-100 text-yellow-800';
      case 'In Progress': return 'bg-blue-100 text-blue-800';
      case 'Completed': return 'bg-green-100 text-green-800';
      case 'Canceled': return 'bg-red-100 text-red-800';
      default: return 'bg-gray-100 text-gray-800';
    }
  };

  const handleDrop = (targetKey: string) => {
    if (draggingKey) moveItem(draggingKey, targetKey);
    setDraggingKey(null);
    setDropTargetKey(null);
  };

  return (
    <div className="bg-card rounded-lg border border-border overflow-hidden">
      <div className="p-4 border-b border-border">
        <div className="flex items-center gap-2">
          <Briefcase className="w-5 h-5 text-muted-foreground" />
          <h2>Current Job Requests</h2>
        </div>
        {orderedJobs.length > 0 && (
          <div className="mt-2">
            <WorkQueueOrderHint hasCustomOrder={hasCustomOrder} onReset={resetOrder} />
          </div>
        )}
        {error && (
          <p className="mt-2 text-sm text-destructive">{error}</p>
        )}
      </div>

      <div className="overflow-auto">
        <table className="w-full">
          <thead className="bg-muted/50 sticky top-0">
            <tr>
              <th className="px-2 py-3 w-8" aria-label="Reorder" />
              <th className="px-4 py-3 text-left">Date</th>
              <th className="px-4 py-3 text-left">Customer</th>
              <th className="px-4 py-3 text-left">Task</th>
              <th className="px-4 py-3 text-center">Usage</th>
              <th className="px-4 py-3 text-left">Send To (signer)</th>
              <th className="px-4 py-3 text-left">User</th>
              <th className="px-4 py-3 text-center">Status</th>
              {isAdmin && <th className="px-4 py-3 text-center">Action</th>}
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={isAdmin ? 9 : 8} className="px-4 py-8 text-center text-muted-foreground">
                  Loading job requests...
                </td>
              </tr>
            ) : orderedJobs.length === 0 ? (
              <tr>
                <td colSpan={isAdmin ? 9 : 8} className="px-4 py-8 text-center text-muted-foreground">
                  {isAdmin
                    ? 'No active work yet — items appear here once clients start their sessions and release work to LGB.'
                    : 'No job requests assigned to you yet — items appear here once clients release work you are assigned to.'}
                </td>
              </tr>
            ) : (
              orderedJobs.map((job) => {
                const key = jobKey(job);
                const taskLabel = job.taskType || job.service;
                const isDragging = draggingKey === key;
                const isDropTarget = dropTargetKey === key && draggingKey !== key;
                return (
                <tr
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
                  className={`border-t border-border hover:bg-muted/30 transition-colors ${
                    isDragging ? 'opacity-50 bg-muted/40' : ''
                  } ${isDropTarget ? 'bg-primary/5 ring-1 ring-inset ring-primary/30' : ''}`}
                >
                  <td className="px-2 py-3">
                    <WorkQueueDragHandle />
                  </td>
                  <td className="px-4 py-3 tabular-nums text-muted-foreground whitespace-nowrap">
                    {jobDisplayDate(job)}
                  </td>
                  <td
                    className="px-4 py-3 font-medium cursor-pointer"
                    onClick={() => onViewJob(job)}
                  >
                    {job.customer}
                  </td>
                  <td
                    className={`px-4 py-3 ${canOpenFormTask(job) ? 'cursor-pointer text-primary hover:underline' : ''}`}
                    onClick={(e) => {
                      if (canOpenFormTask(job)) {
                        e.stopPropagation();
                        onOpenTask!(job);
                      }
                    }}
                  >
                    {taskLabel}
                  </td>
                  <td className="px-4 py-3 text-center">{job.usedQty}/{job.totalQty}</td>
                  <td className="px-4 py-3">
                    <div>{job.accountHolder || '—'}</div>
                    {(job.accountHolderEmail || job.accountHolderPhone) && (
                      <div className="text-xs text-muted-foreground mt-0.5">
                        {[job.accountHolderEmail, job.accountHolderPhone].filter(Boolean).join(' · ')}
                      </div>
                    )}
                  </td>
                  <td className="px-4 py-3">{job.jobAssignedTo || '—'}</td>
                  <td className="px-4 py-3 text-center">
                    <span className={`px-2 py-1 rounded-full text-xs ${getStatusColor(job.status)}`}>
                      {job.status}
                    </span>
                  </td>
                  {isAdmin && (
                    <td className="px-4 py-3 text-center">
                      {job.status === 'Pending' ? (
                        <button
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation();
                            onViewJob(job);
                          }}
                          className="px-3 py-1 text-xs bg-primary text-primary-foreground rounded hover:bg-primary/90 transition-colors"
                        >
                          Assign
                        </button>
                      ) : editingId === job.id ? (
                        <div className="flex items-center justify-center gap-2">
                          <button
                            type="button"
                            onClick={() => handleStatusChange(job, 'Completed')}
                            className="px-3 py-1 text-xs bg-green-600 text-white rounded hover:bg-green-700 transition-colors"
                          >
                            Completed
                          </button>
                          <button
                            type="button"
                            onClick={() => handleStatusChange(job, 'Canceled')}
                            className="px-3 py-1 text-xs bg-red-600 text-white rounded hover:bg-red-700 transition-colors"
                          >
                            Canceled
                          </button>
                          <button
                            type="button"
                            onClick={() => setEditingId(null)}
                            className="px-3 py-1 text-xs bg-gray-600 text-white rounded hover:bg-gray-700 transition-colors"
                          >
                            Cancel
                          </button>
                        </div>
                      ) : (
                        <button
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation();
                            setEditingId(job.id);
                          }}
                          className="p-1 hover:bg-muted rounded transition-colors"
                          disabled={job.status === 'Completed' || job.status === 'Canceled'}
                        >
                          <Edit className={`w-4 h-4 ${job.status === 'Completed' || job.status === 'Canceled' ? 'text-muted-foreground opacity-50' : ''}`} />
                        </button>
                      )}
                    </td>
                  )}
                </tr>
              );
              })
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export type { JobRequest };
