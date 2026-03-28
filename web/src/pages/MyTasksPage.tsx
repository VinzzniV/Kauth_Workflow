// Arbeitsliste fuer Fachbereiche. Hier werden persoenliche oder verantwortungsbezogene Aufgaben gepflegt.
import { useCallback, useMemo, useState } from "react";
import EmptyState from "../components/feedback/EmptyState";
import LoadingState from "../components/feedback/LoadingState";
import PageHeader from "../components/layout/PageHeader";
import RequirementIcon from "../components/workflows/RequirementIcon";
import TaskCommentsSection from "../components/workflows/TaskCommentsSection";
import TaskSlaPill from "../components/workflows/TaskSlaPill";
import TaskStatusPill from "../components/workflows/TaskStatusPill";
import { addTaskComment as addTaskCommentApi, updateTaskStatus as updateTaskStatusApi } from "../services/taskApi";
import { useMyTasks } from "../services/queries/workflowQueries";
import type { TaskWithWorkflow, WorkflowTaskStatus } from "../types/workflow";
import {
  getResponsibleResponsibilityFilterOption,
  getResponsibleResponsibilityFilterValue,
  getResponsibleResponsibilityLabel,
  getResponsibleUserLabel,
} from "../utils/taskAssignment";
import {
  getAvailableVisibleTaskStatuses,
  getVisibleTaskStatus,
  getVisibleTaskStatusLabel,
  mapVisibleTaskStatusToWorkflowStatus,
  TASK_STATUS_ORDER,
  VISIBLE_TASK_STATUS_ORDER,
  type VisibleTaskStatus,
} from "../utils/taskStatus";
import { formatDateTime } from "../utils/dateFormat";

function toTaskStateKey(workflowUid: string, taskId: number): string {
  return `${workflowUid}:${taskId}`;
}

export default function MyTasksPage() {
  const myTasksQuery = useMyTasks();
  const [notice, setNotice] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const [search, setSearch] = useState<string>("");
  const [departmentFilter, setDepartmentFilter] = useState<string>("all");
  const [statusFilter, setStatusFilter] = useState<"all" | VisibleTaskStatus>("all");
  const [responsibilityFilter, setResponsibilityFilter] = useState<string>("all");
  const [savingTaskIds, setSavingTaskIds] = useState<Record<number, boolean>>({});
  const [commentDrafts, setCommentDrafts] = useState<Record<number, string>>({});
  const [savingCommentTaskIds, setSavingCommentTaskIds] = useState<Record<number, boolean>>({});
  const [commentFeedbackTaskId, setCommentFeedbackTaskId] = useState<number | null>(null);
  const [commentFeedbackMessage, setCommentFeedbackMessage] = useState<string | null>(null);
  const rows: TaskWithWorkflow[] = myTasksQuery.data ?? [];
  const isLoading = myTasksQuery.isLoading;
  const isRefreshing = myTasksQuery.isFetching;
  const error =
    actionError ??
    (myTasksQuery.error instanceof Error
      ? myTasksQuery.error.message
      : myTasksQuery.error
        ? "Aufgaben konnten nicht geladen werden."
        : null);

  const reload = useCallback(async () => {
    setNotice(null);
    setActionError(null);
    await myTasksQuery.refetch();
  }, [myTasksQuery]);

  const departmentOptions = useMemo(() => {
    const entries = Array.from(
      new Map(rows.map((row) => [row.workflow.departmentId, row.workflow.departmentName])).entries()
    );
    return entries.sort((left, right) => left[1].localeCompare(right[1], "de"));
  }, [rows]);

  const responsibilityOptions = useMemo(() => {
    const optionsByValue = new Map(
      rows.map((row) => {
        const option = getResponsibleResponsibilityFilterOption(row.task);
        return [option.value, option] as const;
      })
    );

    return Array.from(optionsByValue.values()).sort((left, right) => left.label.localeCompare(right.label, "de"));
  }, [rows]);

  // Suche und Filter arbeiten ausschliesslich auf der bereits geladenen Aufgabenliste.
  const filteredRows = useMemo(() => {
    const normalizedSearch = search.trim().toLowerCase();

    return rows
      .filter((row) => {
        const workflowUid = row.workflow.workflowUid;
        const workflowDisplayName = `${row.workflow.firstName} ${row.workflow.lastName}`.trim() || "Unbekannter Mitarbeitender";
        const effectiveStatus = getVisibleTaskStatus(row.task.status);

        const matchesDepartment =
          departmentFilter === "all" || String(row.workflow.departmentId) === departmentFilter;
        const matchesStatus = statusFilter === "all" || effectiveStatus === statusFilter;

        const responsibilityFilterValue = getResponsibleResponsibilityFilterValue(row.task);
        const matchesResponsibility =
          responsibilityFilter === "all" || responsibilityFilter === responsibilityFilterValue;

        if (!matchesDepartment || !matchesStatus || !matchesResponsibility) {
          return false;
        }

        if (!normalizedSearch) {
          return true;
        }

        return (
          workflowDisplayName.toLowerCase().includes(normalizedSearch) ||
          String(row.workflow.employeeNumber).includes(normalizedSearch) ||
          workflowUid.toLowerCase().includes(normalizedSearch) ||
          row.task.title.toLowerCase().includes(normalizedSearch) ||
          row.task.taskKey.toLowerCase().includes(normalizedSearch)
        );
      })
      .sort((left, right) => {
        const leftStatus = left.task.status;
        const rightStatus = right.task.status;
        const leftName = `${left.workflow.firstName} ${left.workflow.lastName}`.trim() || "Unbekannter Mitarbeitender";
        const rightName = `${right.workflow.firstName} ${right.workflow.lastName}`.trim() || "Unbekannter Mitarbeitender";

        const statusDelta = TASK_STATUS_ORDER.indexOf(leftStatus) - TASK_STATUS_ORDER.indexOf(rightStatus);
        if (statusDelta !== 0) {
          return statusDelta;
        }

        return (
          left.task.sortOrder - right.task.sortOrder ||
          leftName.localeCompare(rightName, "de") ||
          left.task.id - right.task.id
        );
      });
  }, [rows, search, departmentFilter, statusFilter, responsibilityFilter]);

  const groupedRows = useMemo(() => {
    const groups = VISIBLE_TASK_STATUS_ORDER.map((status) => ({
      status,
      items: [] as TaskWithWorkflow[],
    }));

    const itemsByStatus = new Map(groups.map((group) => [group.status, group.items]));

    for (const row of filteredRows) {
      itemsByStatus.get(getVisibleTaskStatus(row.task.status))?.push(row);
    }

    return groups;
  }, [filteredRows]);

  const handleStatusChange = useCallback(
    async (taskId: number, status: VisibleTaskStatus, currentStatus: WorkflowTaskStatus) => {
      const nextStatus = mapVisibleTaskStatusToWorkflowStatus(status, currentStatus);
      if (nextStatus === currentStatus) {
        return;
      }

      setSavingTaskIds((current) => ({ ...current, [taskId]: true }));
      setNotice(null);
      setActionError(null);

      try {
        await updateTaskStatusApi(taskId, nextStatus);
        await myTasksQuery.refetch();
        setNotice("Aufgabenstatus wurde gespeichert.");
      } catch (err) {
        const message = err instanceof Error ? err.message : "Aufgabenstatus konnte nicht aktualisiert werden.";
        setActionError(message);
      } finally {
        setSavingTaskIds((current) => ({ ...current, [taskId]: false }));
      }
    },
    [myTasksQuery]
  );

  const handleCommentDraftChange = useCallback((taskId: number, value: string) => {
    setCommentDrafts((current) => ({ ...current, [taskId]: value }));
  }, []);

  const handleTaskCommentSubmit = useCallback(
    async (taskId: number) => {
      const draft = (commentDrafts[taskId] ?? "").trim();
      if (!draft) {
        return;
      }

      setSavingCommentTaskIds((current) => ({ ...current, [taskId]: true }));
      setCommentFeedbackTaskId(taskId);
      setCommentFeedbackMessage(null);
      setActionError(null);
      setNotice(null);

      try {
        await addTaskCommentApi(taskId, draft);
        await myTasksQuery.refetch();
        setCommentDrafts((current) => ({ ...current, [taskId]: "" }));
        setCommentFeedbackMessage("Kommentar wurde gespeichert.");
      } catch (err) {
        const message = err instanceof Error ? err.message : "Kommentar konnte nicht gespeichert werden.";
        setCommentFeedbackMessage(message);
      } finally {
        setSavingCommentTaskIds((current) => ({ ...current, [taskId]: false }));
      }
    },
    [commentDrafts, myTasksQuery]
  );

  return (
    <main className="app-shell">
      <div className="page-container">
        <PageHeader
          title="Meine Aufgaben"
          description="Hier sehen Sie nur die offenen und laufenden Aufgaben Ihrer fachlichen Zuständigkeiten."
        />

        <section className="panel">
          <div className="panel-head">
            <h2>Aufgabenfilter</h2>
            <p>Filtern Sie nach Abteilung, Status und zuständigem Bereich.</p>
          </div>
          <div className="next-action-callout">
            <p className="next-action-label">Nächste nötige Aktion</p>
            <p className="next-action-text">Bearbeiten Sie zuerst blockierte und bereits laufende Aufgaben.</p>
          </div>

          <div className="toolbar-row">
            <label className="field compact grow">
              <span>Suche</span>
              <input
                type="text"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="z. B. Name, ID, Aufgabe"
              />
            </label>

            <label className="field compact">
              <span>Abteilung</span>
              <select value={departmentFilter} onChange={(event) => setDepartmentFilter(event.target.value)}>
                <option value="all">Alle</option>
                {departmentOptions.map(([departmentId, departmentName]) => (
                  <option key={departmentId} value={departmentId}>
                    {departmentName}
                  </option>
                ))}
              </select>
            </label>

            <label className="field compact">
              <span>Status</span>
              <select
                value={statusFilter}
                onChange={(event) => setStatusFilter(event.target.value as "all" | VisibleTaskStatus)}
              >
                <option value="all">Alle</option>
                {VISIBLE_TASK_STATUS_ORDER.map((status) => (
                  <option key={status} value={status}>
                    {getVisibleTaskStatusLabel(status)}
                  </option>
                ))}
              </select>
            </label>

            <label className="field compact">
              <span>Zuständiger Bereich</span>
              <select value={responsibilityFilter} onChange={(event) => setResponsibilityFilter(event.target.value)}>
                <option value="all">Alle</option>
                {responsibilityOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </label>

            <button type="button" className="btn btn-secondary" onClick={() => void reload()} disabled={isRefreshing}>
              {isRefreshing ? "Aktualisiere..." : "Aktualisieren"}
            </button>
          </div>

          <p className="panel-note">Es werden nur Aufgaben angezeigt, die Ihrer fachlichen Zuständigkeit zugeordnet sind.</p>
          {notice ? <p className="panel-note">{notice}</p> : null}
        </section>

        {isLoading ? <LoadingState title="Aufgaben werden geladen..." /> : null}

        {!isLoading && error ? (
          <EmptyState
            title="Aufgaben konnten nicht geladen werden."
            description={error}
            actionLabel="Erneut versuchen"
            onAction={reload}
          />
        ) : null}

        {!isLoading && !error && rows.length === 0 ? (
          <EmptyState
            title="Keine Aufgaben vorhanden"
            description="Aktuell liegen keine zugewiesenen Aufgaben für Ihre fachlichen Zuständigkeiten vor."
          />
        ) : null}

        {!isLoading && !error && rows.length > 0 && filteredRows.length === 0 ? (
          <EmptyState
            title="Keine Treffer"
            description="Die aktuelle Filterkombination liefert keine Aufgaben."
          />
        ) : null}

        {!isLoading && !error && filteredRows.length > 0 ? (
          <div className="task-groups" aria-label="Aufgaben nach Status">
            {groupedRows.map((group) => (
              <section key={group.status} className="task-group">
                <header className="task-group-head">
                  <h2>{getVisibleTaskStatusLabel(group.status)}</h2>
                  <p>
                    {group.items.length} Aufgabe{group.items.length === 1 ? "" : "n"}
                  </p>
                </header>

                {group.items.length === 0 ? (
                  <p className="panel-note">Keine Aufgaben in dieser Statusgruppe.</p>
                ) : (
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
                      const canChangeStatus = row.task.canUpdateStatus && availableStatuses.length > 1;

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
                              </dl>

                              <p className="panel-note">
                                Workflow-ID: {workflowUid} | Abhängigkeiten: {row.task.dependencies.length}
                              </p>

                              <div className="toolbar-row task-actions-row">
                                {canChangeStatus ? (
                                  <label className="field compact">
                                    <span>Status</span>
                                    <select
                                      value={visibleStatus}
                                      disabled={isSavingTask}
                                      onChange={(event) =>
                                        void handleStatusChange(
                                          row.task.id,
                                          event.target.value as VisibleTaskStatus,
                                          row.task.status
                                        )
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
                                <p className="panel-note">
                                  Diese Aufgabe bleibt offen, bis ihre Abhängigkeiten erfüllt sind.
                                </p>
                              ) : null}

                              <TaskCommentsSection
                                task={row.task}
                                draftValue={commentDrafts[row.task.id] ?? ""}
                                isSaving={savingCommentTaskIds[row.task.id] === true}
                                feedbackMessage={commentFeedbackTaskId === row.task.id ? commentFeedbackMessage : null}
                                onDraftChange={handleCommentDraftChange}
                                onSubmit={handleTaskCommentSubmit}
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
                )}
              </section>
            ))}
          </div>
        ) : null}
      </div>
    </main>
  );
}
