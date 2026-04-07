import { Link } from "react-router-dom";
import RequirementIcon from "../components/workflows/RequirementIcon";
import TaskCommentsSection from "../components/workflows/TaskCommentsSection";
import TaskSlaPill from "../components/workflows/TaskSlaPill";
import TaskStatusPill from "../components/workflows/TaskStatusPill";
import type { TaskWithWorkflow } from "../types/workflow";
import { formatDateTime } from "../utils/dateFormat";
import {
  getResponsibleResponsibilityLabel,
  getResponsibleUserLabel,
} from "../utils/taskAssignment";
import {
  getAvailableVisibleTaskStatuses,
  getVisibleTaskStatus,
  getVisibleTaskStatusLabel,
  type VisibleTaskStatus,
} from "../utils/taskStatus";
import { toTaskStateKey } from "./myTasksPageModel";

type TaskGroup = {
  status: VisibleTaskStatus;
  items: TaskWithWorkflow[];
};

type MyTaskGroupsProps = {
  visibleGroups: TaskGroup[];
  savingTaskIds: Record<number, boolean>;
  savingApprovalTaskIds: Record<number, boolean>;
  commentDrafts: Record<number, string>;
  savingCommentTaskIds: Record<number, boolean>;
  onStatusChange: (args: {
    taskId: number;
    workflowUid: string;
    status: VisibleTaskStatus;
    currentStatus: TaskWithWorkflow["task"]["status"];
  }) => Promise<void>;
  onApprovalDecision: (args: { taskId: number; workflowUid: string; approved: boolean }) => Promise<void>;
  onCommentDraftChange: (taskId: number, value: string) => void;
  onCommentSubmit: (args: { taskId: number; workflowUid: string }) => Promise<void>;
};

export function MyTaskGroups({
  visibleGroups,
  savingTaskIds,
  savingApprovalTaskIds,
  commentDrafts,
  savingCommentTaskIds,
  onStatusChange,
  onApprovalDecision,
  onCommentDraftChange,
  onCommentSubmit,
}: MyTaskGroupsProps) {
  return (
    <div className="task-groups" aria-label="Aufgaben nach Status">
      {visibleGroups.map((group) => (
        <section key={group.status} className="task-group">
          <header className="task-group-head">
            <h2>{getVisibleTaskStatusLabel(group.status)}</h2>
            <p>
              {group.items.length} Aufgabe{group.items.length === 1 ? "" : "n"}
            </p>
          </header>
          <ul className="task-list">
            {group.items.map((row) => {
              const workflowUid = row.workflow.workflowUid;
              const statusKey = toTaskStateKey(workflowUid, row.task.id);
              const workflowDisplayName =
                `${row.workflow.firstName} ${row.workflow.lastName}`.trim() || "Unbekannter Mitarbeitender";
              const effectiveStatus = row.task.status;
              const visibleStatus = getVisibleTaskStatus(effectiveStatus);
              const availableStatuses = getAvailableVisibleTaskStatuses(effectiveStatus);
              const isSavingTask = savingTaskIds[row.task.id] === true;
              const isSavingApproval = savingApprovalTaskIds[row.task.id] === true;
              const canChangeStatus = row.task.canUpdateStatus && availableStatuses.length > 1;
              const canDecideApproval = row.task.isApprovalTask && row.task.canDecideApproval;

              return (
                <li key={statusKey} className="task-card">
                  <div className="task-card-top">
                    <div>
                      <h3>{row.task.title}</h3>
                      <p className="panel-text">{row.task.description}</p>
                    </div>
                    <div className="task-card-pill-group">
                      <TaskSlaPill status={row.task.slaStatus} />
                      <TaskStatusPill status={effectiveStatus} />
                    </div>
                  </div>

                  <div className="task-card-layout">
                    <div className="task-card-content">
                      <dl className="task-meta">
                        <div>
                          <dt>Vorgang</dt>
                          <dd>
                            {workflowDisplayName} ({row.workflow.employeeNumber})
                          </dd>
                        </div>
                        <div>
                          <dt>Abteilung</dt>
                          <dd>{row.workflow.departmentName}</dd>
                        </div>
                        <div>
                          <dt>Stelle</dt>
                          <dd>{row.workflow.roleName}</dd>
                        </div>
                        <div>
                          <dt>Zuständiger Bereich</dt>
                          <dd>{getResponsibleResponsibilityLabel(row.task)}</dd>
                        </div>
                        <div>
                          <dt>Verantwortliche Person</dt>
                          <dd>{getResponsibleUserLabel(row.task)}</dd>
                        </div>
                        <div>
                          <dt>Erstellt</dt>
                          <dd>{formatDateTime(row.task.createdAt)}</dd>
                        </div>
                        <div>
                          <dt>Fällig</dt>
                          <dd>{row.task.dueAt ? formatDateTime(row.task.dueAt) : "Keine Frist"}</dd>
                        </div>
                        <div>
                          <dt>Vorgangsdetails</dt>
                          <dd>
                            <Link className="task-meta-link" to={`/workflows/${workflowUid}`}>
                              Zum Vorgang
                            </Link>
                          </dd>
                        </div>
                      </dl>

                      <div className="toolbar-row task-actions-row">
                        {canDecideApproval ? (
                          <div className="task-approval-actions">
                            <button
                              type="button"
                              className="btn btn-primary"
                              disabled={isSavingApproval}
                              onClick={() => void onApprovalDecision({ taskId: row.task.id, workflowUid, approved: true })}
                            >
                              {isSavingApproval ? "Speichere..." : "Freigeben"}
                            </button>
                            <button
                              type="button"
                              className="btn btn-secondary"
                              disabled={isSavingApproval}
                              onClick={() => void onApprovalDecision({ taskId: row.task.id, workflowUid, approved: false })}
                            >
                              {isSavingApproval ? "Speichere..." : "Ablehnen"}
                            </button>
                          </div>
                        ) : canChangeStatus ? (
                          <label className="field compact">
                            <span>Status</span>
                            <select
                              value={visibleStatus}
                              disabled={isSavingTask}
                              onChange={(event) =>
                                void onStatusChange({
                                  taskId: row.task.id,
                                  workflowUid,
                                  status: event.target.value as VisibleTaskStatus,
                                  currentStatus: row.task.status,
                                })
                              }
                            >
                              {availableStatuses.map((status) => (
                                <option key={status} value={status}>
                                  {getVisibleTaskStatusLabel(status)}
                                </option>
                              ))}
                            </select>
                          </label>
                        ) : (
                          <p className="panel-note">Für diese Aufgabe ist aktuell kein Statuswechsel möglich.</p>
                        )}
                      </div>

                      {effectiveStatus === "blocked" ? (
                        <p className="panel-note">Aufgabe bleibt offen, bis ihre Abhängigkeiten erfüllt sind.</p>
                      ) : null}

                      <TaskCommentsSection
                        task={row.task}
                        draftValue={commentDrafts[row.task.id] ?? ""}
                        isSaving={savingCommentTaskIds[row.task.id] === true}
                        onDraftChange={onCommentDraftChange}
                        onSubmit={(taskId) => onCommentSubmit({ taskId, workflowUid })}
                      />
                    </div>

                    <div className="task-icon-side">
                      <RequirementIcon iconKey={row.task.iconKey} title={row.task.title} size="md" />
                    </div>
                  </div>
                </li>
              );
            })}
          </ul>
        </section>
      ))}
    </div>
  );
}
