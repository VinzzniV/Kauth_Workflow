import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import TaskStatusPill from "../workflows/TaskStatusPill";
import TaskCommentsSection from "../workflows/TaskCommentsSection";
import TaskSlaPill from "../workflows/TaskSlaPill";
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
  compareAreaGroupsForDisplay,
  compareTasksForDisplay,
  toAreaStatus,
  toAreaStatusLabel,
  toAreaStatusNote,
  toTaskDisplayTitle,
  type ProcessAreaGroup,
} from "./workflowDetailModel";

type WorkflowTaskAreasSectionProps = {
  tasksByArea: ProcessAreaGroup[];
  savingTaskIds: Record<number, boolean>;
  commentDrafts: Record<number, string>;
  savingCommentTaskIds: Record<number, boolean>;
  usesAdminOverride: boolean;
  canManageAdminConfiguration: boolean;
  isReaderOnlyView: boolean;
  onTaskStatusChange: (taskId: number, status: VisibleTaskStatus, currentStatus: WorkflowTask["status"]) => Promise<void>;
  onCommentDraftChange: (taskId: number, value: string) => void;
  onTaskCommentSubmit: (taskId: number) => Promise<void>;
};

function getAreaStatusClass(status: ReturnType<typeof toAreaStatus>): string {
  return status === "done" ? "status-pill completed" : status === "none" ? "chip" : "status-pill running";
}

export default function WorkflowTaskAreasSection({
  tasksByArea,
  savingTaskIds,
  commentDrafts,
  savingCommentTaskIds,
  usesAdminOverride,
  canManageAdminConfiguration,
  isReaderOnlyView,
  onTaskStatusChange,
  onCommentDraftChange,
  onTaskCommentSubmit,
}: WorkflowTaskAreasSectionProps) {
  const groupRefs = useRef<Record<string, HTMLElement | null>>({});

  const orderedGroups = useMemo(
    () =>
      tasksByArea
        .map((group) => ({
          ...group,
          tasks: group.tasks.slice().sort(compareTasksForDisplay),
        }))
        .sort(compareAreaGroupsForDisplay),
    [tasksByArea]
  );

  const initialExpanded = useMemo(() => {
    const map: Record<string, boolean> = {};
    for (const group of orderedGroups) {
      map[group.name] = group.isCurrentArea || toAreaStatus(group) !== "done";
    }
    return map;
  }, [orderedGroups]);

  const [expandedAreas, setExpandedAreas] = useState<Record<string, boolean>>(initialExpanded);

  useEffect(() => {
    setExpandedAreas((current) => {
      const next: Record<string, boolean> = {};
      for (const group of orderedGroups) {
        next[group.name] = current[group.name] ?? initialExpanded[group.name] ?? false;
        if (group.isCurrentArea) {
          next[group.name] = true;
        }
      }
      return next;
    });
  }, [initialExpanded, orderedGroups]);

  const toggleArea = useCallback((name: string) => {
    setExpandedAreas((prev) => ({ ...prev, [name]: !prev[name] }));
  }, []);

  const scrollToArea = useCallback((name: string) => {
    setExpandedAreas((prev) => ({ ...prev, [name]: true }));
    requestAnimationFrame(() => {
      groupRefs.current[name]?.scrollIntoView({ behavior: "smooth", block: "start" });
    });
  }, []);

  const expandAll = useCallback(() => {
    setExpandedAreas(Object.fromEntries(orderedGroups.map((g) => [g.name, true])));
  }, [orderedGroups]);

  const collapseAll = useCallback(() => {
    setExpandedAreas(Object.fromEntries(orderedGroups.map((g) => [g.name, false])));
  }, [orderedGroups]);

  return (
    <section className="content-stack">
      <section className="panel">
        <div className="panel-head">
          <h2>Aufgaben nach Bereich</h2>
        </div>

        <div className="workflow-area-grid" aria-label="Status nach Bereich">
          {orderedGroups.map((group) => {
            const status = toAreaStatus(group);

            return (
              <button
                key={`overview-${group.name}`}
                type="button"
                className={`workflow-area-card ${group.isCurrentArea ? "current" : ""}`}
                onClick={() => scrollToArea(group.name)}
              >
                {group.isCurrentArea ? <p className="workflow-area-card-eyebrow">Jetzt relevant</p> : null}
                <div className="workflow-area-card-head">
                  <h3>{group.name}</h3>
                  <span className={getAreaStatusClass(status)}>{toAreaStatusLabel(status)}</span>
                </div>
                <p className="workflow-area-card-count">
                  {group.totalCount} Aufgabe{group.totalCount === 1 ? "" : "n"}
                </p>
                <p className="workflow-area-card-note">{toAreaStatusNote(group)}</p>
              </button>
            );
          })}
        </div>

        <div className="task-groups-controls">
          <button type="button" className="btn-text" onClick={expandAll}>Alle aufklappen</button>
          <button type="button" className="btn-text" onClick={collapseAll}>Alle zuklappen</button>
        </div>

        <div className="task-groups" aria-label="Aufgaben nach Bereich">
          {orderedGroups.map((group, index) => {
            const status = toAreaStatus(group);
            const isExpanded = expandedAreas[group.name] ?? false;
            const isDone = status === "done";
            const contentId = `workflow-task-group-${index}`;

            return (
              <section
                key={group.name}
                className={`task-group${isDone ? " task-group--done" : ""}${isDone && !isExpanded ? " task-group--compact" : ""}${group.isCurrentArea ? " task-group--current" : ""}`}
                ref={(el) => { groupRefs.current[group.name] = el; }}
              >
                <div className="task-group-head">
                  <button
                    type="button"
                    className="task-group-head task-group-head--collapsible"
                    aria-controls={contentId}
                    aria-label={`${group.name} ${isExpanded ? "zuklappen" : "aufklappen"}`}
                    aria-expanded={isExpanded}
                    onClick={() => toggleArea(group.name)}
                  >
                    <svg className={`task-group-chevron${isExpanded ? " task-group-chevron--open" : ""}`} viewBox="0 0 20 20" fill="currentColor" width="16" height="16" aria-hidden="true">
                      <path fillRule="evenodd" d="M5.23 7.21a.75.75 0 0 1 1.06.02L10 11.168l3.71-3.938a.75.75 0 1 1 1.08 1.04l-4.25 4.5a.75.75 0 0 1-1.08 0l-4.25-4.5a.75.75 0 0 1 .02-1.06Z" clipRule="evenodd" />
                    </svg>
                    <div className="task-group-head-copy">
                      <h3>{group.name}</h3>
                      {group.isCurrentArea ? <p>Jetzt relevant</p> : null}
                    </div>
                    <span className={getAreaStatusClass(status)}>{toAreaStatusLabel(status)}</span>
                    <span className="task-group-count">
                      {group.openCount > 0 ? `${group.openCount} offen` : `${group.completedCount}/${group.totalCount} erledigt`}
                    </span>
                  </button>
                </div>

                {isExpanded ? (
                  <div id={contentId}>
                    {group.tasks.length === 0 ? (
                      <p className="panel-note">Für diesen Bereich sind aktuell keine Aufgaben vorhanden.</p>
                    ) : (
                      <ul className="task-list">
                        {group.tasks.map((task) => {
                          const isTaskDone = task.status === "done";
                          const visibleStatus = getVisibleTaskStatus(task.status);
                          const availableStatuses = getAvailableVisibleTaskStatuses(task.status);
                          const isSavingTask = savingTaskIds[task.id] === true;
                          const canChangeTaskStatus =
                            canManageAdminConfiguration && task.canUpdateStatus && availableStatuses.length > 1;
                          return (
                            <li key={task.id} className={`task-card${isTaskDone ? " task-card--done" : ""}${!isTaskDone ? " task-card--active" : ""}`}>
                              <div className="task-card-top">
                                <div>
                                  <h3>{toTaskDisplayTitle(task)}</h3>
                                  {!isTaskDone ? <p className="panel-text">{task.description}</p> : null}
                                  {isTaskDone ? <p className="task-card-done-note">Erledigte Aufgabe</p> : null}
                                </div>
                                <div className="task-card-pill-group">
                                  <TaskSlaPill status={task.slaStatus} />
                                  <TaskStatusPill status={task.status} />
                                </div>
                              </div>

                              {!isTaskDone ? (
                                <dl className="task-meta">
                                  <div>
                                    <dt>Zuständiger Bereich</dt>
                                    <dd>{getResponsibleResponsibilityLabel(task)}</dd>
                                  </div>
                                  <div>
                                    <dt>Verantwortliche Person</dt>
                                    <dd>{getResponsibleUserLabel(task)}</dd>
                                  </div>
                                  <div>
                                    <dt>Erstellt</dt>
                                    <dd>{formatDateTime(task.createdAt)}</dd>
                                  </div>
                                  <div>
                                    <dt>Fällig</dt>
                                    <dd>{task.dueAt ? formatDateTime(task.dueAt) : "Keine Frist"}</dd>
                                  </div>
                                </dl>
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

                              {!isReaderOnlyView && !isTaskDone ? (
                                <TaskCommentsSection
                                  task={task}
                                  draftValue={commentDrafts[task.id] ?? ""}
                                  isSaving={savingCommentTaskIds[task.id] === true}
                                  onDraftChange={onCommentDraftChange}
                                  onSubmit={onTaskCommentSubmit}
                                />
                              ) : null}
                            </li>
                          );
                        })}
                      </ul>
                    )}
                  </div>
                ) : null}
              </section>
            );
          })}
        </div>
      </section>
    </section>
  );
}
