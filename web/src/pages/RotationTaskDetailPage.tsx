import { useMemo } from "react";
import { Link, useParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import EmptyState from "../components/feedback/EmptyState";
import LoadingState from "../components/feedback/LoadingState";
import PageHeader from "../components/layout/PageHeader";
import RotationAuditLog from "../components/rotation/RotationAuditLog";
import RotationNotificationsPanel from "../components/rotation/RotationNotificationsPanel";
import TaskCommentsSection from "../components/workflows/TaskCommentsSection";
import TaskSlaPill from "../components/workflows/TaskSlaPill";
import TaskStatusPill from "../components/workflows/TaskStatusPill";
import { useTaskInteraction } from "../hooks/useTaskInteraction";
import { queryKeys } from "../services/queryKeys";
import {
  useRotationAuditLog,
  useRotationGeneratedTasks,
  useRotationNotifications,
  useRotationPlanDetail,
} from "../services/queries/rotationQueries";
import { getTaskByRef } from "../services/taskApi";
import type { RotationAuditEntry, RotationNotification, RotationStation } from "../types/rotation";
import { formatDate, formatDateTime } from "../utils/dateFormat";
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

function getStationPair(stations: RotationStation[], stationId: number | null, triggerType: "enter" | "exit" | null) {
  const orderedStations = [...stations].sort((left, right) => left.orderIndex - right.orderIndex);
  const stationIndex = orderedStations.findIndex((station) => station.id === stationId);
  if (stationIndex < 0) {
    return { currentStation: null, nextStation: null };
  }

  if (triggerType === "enter") {
    return {
      currentStation: orderedStations[stationIndex - 1] ?? null,
      nextStation: orderedStations[stationIndex] ?? null,
    };
  }

  return {
    currentStation: orderedStations[stationIndex] ?? null,
    nextStation: orderedStations[stationIndex + 1] ?? null,
  };
}

export default function RotationTaskDetailPage() {
  const { taskRef = "" } = useParams<{ taskRef: string }>();
  const {
    savingTaskIds,
    commentDrafts,
    savingCommentTaskIds,
    handleStatusChange,
    handleCommentDraftChange,
    handleTaskCommentSubmit,
  } = useTaskInteraction();

  const taskQuery = useQuery({
    queryKey: queryKeys.tasks.byRef(taskRef),
    queryFn: () => getTaskByRef(taskRef),
    enabled: Boolean(taskRef),
    staleTime: 0,
  });

  const rotationPlanId = taskQuery.data?.rotation?.rotationPlanId ?? null;
  const planDetailQuery = useRotationPlanDetail(rotationPlanId, rotationPlanId !== null);
  const generatedTasksQuery = useRotationGeneratedTasks(rotationPlanId, rotationPlanId !== null);
  const auditLogQuery = useRotationAuditLog(rotationPlanId, 100, 0, rotationPlanId !== null);
  const notificationsQuery = useRotationNotifications(rotationPlanId, 100, 0, rotationPlanId !== null);

  const taskRow = taskQuery.data;
  const rotationContext = taskRow?.rotation ?? null;
  const stationPair = useMemo(() => {
    if (!rotationContext || !planDetailQuery.data) {
      return { currentStation: null, nextStation: null };
    }

    return getStationPair(
      planDetailQuery.data.stations,
      rotationContext.rotationStationId,
      rotationContext.triggerType
    );
  }, [planDetailQuery.data, rotationContext]);

  const relatedTasks = useMemo(
    () =>
      (generatedTasksQuery.data ?? []).filter(
        (generatedTask) => generatedTask.rotationStationId === rotationContext?.rotationStationId
      ),
    [generatedTasksQuery.data, rotationContext?.rotationStationId]
  );
  const relevantAuditEntries = useMemo<RotationAuditEntry[]>(() => {
    if (!rotationContext) {
      return [];
    }

    return (auditLogQuery.data ?? []).filter((entry) =>
      entry.generatedTaskId === taskRow?.task.id
      || (
        rotationContext.rotationStationId !== null
        && entry.rotationStationId === rotationContext.rotationStationId
      )
    );
  }, [auditLogQuery.data, rotationContext, taskRow?.task.id]);
  const relevantNotifications = useMemo<RotationNotification[]>(() => {
    if (!rotationContext) {
      return [];
    }

    return (notificationsQuery.data ?? []).filter((notification) =>
      notification.generatedTaskId === taskRow?.task.id
      || (
        rotationContext.rotationStationId !== null
        && notification.rotationStationId === rotationContext.rotationStationId
      )
    );
  }, [notificationsQuery.data, rotationContext, taskRow?.task.id]);

  if (!taskRef) {
    return (
      <main className="app-shell">
        <div className="page-container">
          <EmptyState title="Ungültiger Aufgabenpfad" description="Die gewünschte Detailansicht konnte nicht geöffnet werden." />
        </div>
      </main>
    );
  }

  return (
    <main className="app-shell">
      <div className="page-container">
        <div className="page-back-row">
          <Link to="/rotation/operations" className="btn btn-ghost">
            ← Zurück zu Wechsel & Durchlaufaufgaben
          </Link>
        </div>

        {(taskQuery.isLoading || planDetailQuery.isLoading || generatedTasksQuery.isLoading) ? (
          <LoadingState title="Rotationsdetail wird geladen..." />
        ) : null}

        {!taskQuery.isLoading && taskQuery.error ? (
          <EmptyState
            title="Aufgabe konnte nicht geladen werden."
            description={taskQuery.error instanceof Error ? taskQuery.error.message : "Die Detailansicht ist fehlgeschlagen."}
            actionLabel="Erneut versuchen"
            onAction={() => void taskQuery.refetch()}
          />
        ) : null}

        {!taskQuery.isLoading && !taskQuery.error && (!taskRow || taskRow.taskFamily !== "rotation" || !rotationContext) ? (
          <EmptyState
            title="Keine Rotationsaufgabe gefunden"
            description="Die angeforderte Aufgabe ist entweder nicht mehr vorhanden oder gehört nicht zum Durchlauf-Slice."
          />
        ) : null}

        {!taskQuery.isLoading && !taskQuery.error && taskRow && taskRow.taskFamily === "rotation" && rotationContext ? (
          <>
            <PageHeader
              variant="detail"
              eyebrow="Rotationsaufgabe"
              title={taskRow.task.title}
              description={`${rotationContext.displayName} · ${rotationContext.planTitle}`}
            />

            <section className="panel">
              <div className="panel-head">
                <h2>Wechselkontext</h2>
              </div>
              <dl className="workflow-meta">
                <div>
                  <dt>Person</dt>
                  <dd>{rotationContext.displayName}</dd>
                </div>
                <div>
                  <dt>Aktuelle Station</dt>
                  <dd>{stationPair.currentStation?.departmentName ?? "-"}</dd>
                </div>
                <div>
                  <dt>Nächste Station</dt>
                  <dd>{stationPair.nextStation?.departmentName ?? "-"}</dd>
                </div>
                <div>
                  <dt>Wechseltermin</dt>
                  <dd>{rotationContext.anchorDate ? formatDate(rotationContext.anchorDate) : "-"}</dd>
                </div>
                <div>
                  <dt>Trigger</dt>
                  <dd>{rotationContext.triggerType === "exit" ? "Austritt" : "Eintritt"}</dd>
                </div>
                <div>
                  <dt>Quell-Onboarding</dt>
                  <dd>{rotationContext.sourceWorkflowUid}</dd>
                </div>
              </dl>
            </section>

            <section className="panel">
              <div className="panel-head">
                <h2>Maßnahme</h2>
              </div>
              <div className="task-card">
                <div className="task-card-top">
                  <div>
                    <h3>{taskRow.task.title}</h3>
                    <p className="panel-text">{taskRow.task.description}</p>
                  </div>
                  <div className="task-card-pill-group">
                    <TaskSlaPill status={taskRow.task.slaStatus} />
                    <TaskStatusPill status={taskRow.task.status} />
                  </div>
                </div>
                <div className="task-card-layout">
                  <div className="task-card-content">
                    <dl className="task-meta">
                      <div>
                        <dt>Zuständiger Bereich</dt>
                        <dd>{getResponsibleResponsibilityLabel(taskRow.task)}</dd>
                      </div>
                      <div>
                        <dt>Verantwortliche Person</dt>
                        <dd>{getResponsibleUserLabel(taskRow.task)}</dd>
                      </div>
                      <div>
                        <dt>Erstellt</dt>
                        <dd>{formatDateTime(taskRow.task.createdAt)}</dd>
                      </div>
                      <div>
                        <dt>Fällig</dt>
                        <dd>{taskRow.task.dueAt ? formatDateTime(taskRow.task.dueAt) : "Keine Frist"}</dd>
                      </div>
                    </dl>

                    {getAvailableVisibleTaskStatuses(taskRow.task.status, taskRow.taskFamily).length > 1 ? (
                      <div className="toolbar-row task-actions-row">
                        <label className="field compact">
                          <span>Status</span>
                          <select
                            value={getVisibleTaskStatus(taskRow.task.status)}
                            disabled={savingTaskIds[taskRow.task.id] === true}
                            onChange={(event) =>
                              void handleStatusChange({
                                taskId: taskRow.task.id,
                                taskRef: taskRow.taskRef,
                                taskFamily: taskRow.taskFamily,
                                rotationPlanId: rotationContext.rotationPlanId,
                                status: event.target.value as VisibleTaskStatus,
                                currentStatus: taskRow.task.status,
                              })
                            }
                          >
                            {getAvailableVisibleTaskStatuses(taskRow.task.status, taskRow.taskFamily).map((status) => (
                              <option key={status} value={status}>
                                {getVisibleTaskStatusLabel(status)}
                              </option>
                            ))}
                          </select>
                        </label>
                      </div>
                    ) : null}

                    <TaskCommentsSection
                      task={taskRow.task}
                      draftValue={commentDrafts[taskRow.task.id] ?? ""}
                      isSaving={savingCommentTaskIds[taskRow.task.id] === true}
                      onDraftChange={handleCommentDraftChange}
                      onSubmit={(taskId) =>
                        handleTaskCommentSubmit({
                          taskId,
                          taskRef: taskRow.taskRef,
                          taskFamily: taskRow.taskFamily,
                          rotationPlanId: rotationContext.rotationPlanId,
                        })
                      }
                    />
                  </div>
                </div>
              </div>
            </section>

            <section className="panel">
              <div className="panel-head">
                <h2>Generierte Maßnahmen derselben Station</h2>
              </div>
              {relatedTasks.length === 0 ? (
                <p className="panel-note">Für diese Station wurden keine weiteren Maßnahmen gefunden.</p>
              ) : (
                <div className="workflow-grid" aria-label="Generierte Maßnahmen derselben Station">
                  {relatedTasks.map((generatedTask) => (
                    <article key={generatedTask.taskRef} className="workflow-card card-list">
                      <div className="workflow-card-top">
                        <h3>{generatedTask.title}</h3>
                        <span className="status-pill running">{generatedTask.status}</span>
                      </div>
                      <dl className="workflow-meta">
                        <div>
                          <dt>Abteilung</dt>
                          <dd>{generatedTask.departmentName ?? "-"}</dd>
                        </div>
                        <div>
                          <dt>Wechselbezug</dt>
                          <dd>{formatDate(generatedTask.anchorDate)}</dd>
                        </div>
                        <div>
                          <dt>Fällig</dt>
                          <dd>{generatedTask.dueDate ? formatDate(generatedTask.dueDate) : "-"}</dd>
                        </div>
                      </dl>
                    </article>
                  ))}
                </div>
              )}
            </section>

            <RotationAuditLog
              title="Historie"
              description="Task- und stationsbezogene Historie aus Audit- und Synchronisierungseinträgen."
              entries={relevantAuditEntries}
              isLoading={auditLogQuery.isLoading}
              error={
                auditLogQuery.error instanceof Error
                  ? auditLogQuery.error.message
                  : auditLogQuery.error
                    ? "Historie konnte nicht geladen werden."
                    : null
              }
            />

            <RotationNotificationsPanel
              title="Benachrichtigungshistorie"
              description="Versandhistorie der zugehörigen Wechsel- und Aufgabenbenachrichtigungen."
              notifications={relevantNotifications}
              isLoading={notificationsQuery.isLoading}
              error={
                notificationsQuery.error instanceof Error
                  ? notificationsQuery.error.message
                  : notificationsQuery.error
                    ? "Benachrichtigungen konnten nicht geladen werden."
                    : null
              }
            />
          </>
        ) : null}
      </div>
    </main>
  );
}
