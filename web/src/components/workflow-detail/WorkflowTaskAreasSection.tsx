import TaskStatusPill from "../workflows/TaskStatusPill";
import type { WorkflowTask } from "../../types/workflow";
import { getResponsibleResponsibilityLabel, getResponsibleUserLabel } from "../../utils/taskAssignment";
import {
  getAvailableVisibleTaskStatuses,
  getVisibleTaskStatus,
  getVisibleTaskStatusLabel,
  type VisibleTaskStatus,
} from "../../utils/taskStatus";
import {
  formatDateTime,
  toAreaStatus,
  toAreaStatusLabel,
  toAreaStatusNote,
  toTaskDisplayTitle,
  type ProcessAreaGroup,
} from "./workflowDetailModel";

type WorkflowTaskAreasSectionProps = {
  tasksByArea: ProcessAreaGroup[];
  taskError: string | null;
  taskNotice: string | null;
  savingTaskIds: Record<number, boolean>;
  usesAdminOverride: boolean;
  canManageAdminConfiguration: boolean;
  isReaderOnlyView: boolean;
  onTaskStatusChange: (taskId: number, status: VisibleTaskStatus, currentStatus: WorkflowTask["status"]) => Promise<void>;
};

function getAreaStatusClass(status: ReturnType<typeof toAreaStatus>): string {
  return status === "done" ? "status-pill completed" : status === "none" ? "chip" : "status-pill running";
}

export default function WorkflowTaskAreasSection({
  tasksByArea,
  taskError,
  taskNotice,
  savingTaskIds,
  usesAdminOverride,
  canManageAdminConfiguration,
  isReaderOnlyView,
  onTaskStatusChange,
}: WorkflowTaskAreasSectionProps) {
  return (
    <>
      <section className="panel panel-muted">
        <div className="panel-head">
          <h2>Bereiche im Überblick</h2>
          <p>So ist der aktuelle Stand in den beteiligten Bereichen.</p>
        </div>

        <div className="workflow-area-grid" aria-label="Status nach Bereich">
          {tasksByArea.map((group) => {
            const status = toAreaStatus(group);

            return (
              <article key={`overview-${group.name}`} className={`workflow-area-card ${group.isCurrentArea ? "current" : ""}`}>
                <div className="workflow-area-card-head">
                  <h3>{group.name}</h3>
                  <span className={getAreaStatusClass(status)}>{toAreaStatusLabel(status)}</span>
                </div>
                <p className="workflow-area-card-count">
                  {group.totalCount} Aufgabe{group.totalCount === 1 ? "" : "n"}
                </p>
                <p className="workflow-area-card-note">{toAreaStatusNote(group)}</p>
              </article>
            );
          })}
        </div>
      </section>

      <section className="panel">
        <div className="panel-head">
          <h2>Aufgaben nach Bereich</h2>
          <p>Alle Aufgaben sind nach dem jeweils zuständigen Bereich gruppiert.</p>
        </div>

        {taskError ? <p className="panel-note">{taskError}</p> : null}
        {taskNotice ? <p className="panel-note">{taskNotice}</p> : null}

        <div className="task-groups" aria-label="Aufgaben nach Bereich">
          {tasksByArea.map((group) => {
            const status = toAreaStatus(group);

            return (
              <section key={group.name} className="task-group">
                <header className="task-group-head">
                  <h3>{group.name}{group.isCurrentArea ? " · aktuell dran" : ""}</h3>
                  <p>
                    {group.totalCount} Aufgabe{group.totalCount === 1 ? "" : "n"} | Offen: {group.openCount} | In
                    Bearbeitung: {group.inProgressCount} | Erledigt: {group.completedCount}
                  </p>
                </header>

                <div className="action-row">
                  <span className={getAreaStatusClass(status)}>{toAreaStatusLabel(status)}</span>
                </div>

                {group.tasks.length === 0 ? (
                  <p className="panel-note">Für diesen Bereich sind aktuell keine Aufgaben vorhanden.</p>
                ) : (
                  <ul className="task-list">
                    {group.tasks.map((task) => {
                      const visibleStatus = getVisibleTaskStatus(task.status);
                      const availableStatuses = getAvailableVisibleTaskStatuses(task.status);
                      const isSavingTask = savingTaskIds[task.id] === true;
                      const canChangeTaskStatus =
                        canManageAdminConfiguration && task.canUpdateStatus && availableStatuses.length > 1;

                      return (
                        <li key={task.id} className="task-card">
                          <div className="task-card-top">
                            <div>
                              <h3>{toTaskDisplayTitle(task)}</h3>
                              <p className="panel-text">{task.description}</p>
                            </div>
                            <TaskStatusPill status={task.status} />
                          </div>

                          <dl className="task-meta">
                            <div>
                              <dt>Zuständiger Bereich</dt>
                              <dd>{getResponsibleResponsibilityLabel(task)}</dd>
                            </div>
                            {!isReaderOnlyView ? (
                              <div>
                                <dt>Verantwortliche Person</dt>
                                <dd>{getResponsibleUserLabel(task)}</dd>
                              </div>
                            ) : null}
                            <div>
                              <dt>Erstellt</dt>
                              <dd>{formatDateTime(task.createdAt)}</dd>
                            </div>
                          </dl>

                          {isReaderOnlyView ? (
                            <p className="panel-note">Interne Zuweisungsdetails sind für Leser ausgeblendet.</p>
                          ) : null}

                          {canChangeTaskStatus ? (
                            <div className="toolbar-row task-actions-row">
                              <label className="field compact">
                                <span>{usesAdminOverride ? "Status (Admin-Override)" : "Status"}</span>
                                <select
                                  value={visibleStatus}
                                  disabled={isSavingTask}
                                  onChange={(event) =>
                                    void onTaskStatusChange(
                                      task.id,
                                      event.target.value as VisibleTaskStatus,
                                      task.status
                                    )
                                  }
                                >
                                  {availableStatuses.map((taskStatus) => (
                                    <option key={taskStatus} value={taskStatus}>
                                      {getVisibleTaskStatusLabel(taskStatus)}
                                    </option>
                                  ))}
                                </select>
                              </label>
                            </div>
                          ) : null}


                        </li>
                      );
                    })}
                  </ul>
                )}
              </section>
            );
          })}
        </div>
      </section>
    </>
  );
}
