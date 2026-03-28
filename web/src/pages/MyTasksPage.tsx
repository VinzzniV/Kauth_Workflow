// Arbeitsliste fuer Fachbereiche. Hier werden persoenliche oder verantwortungsbezogene Aufgaben gepflegt.
import { useCallback, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import EmptyState from "../components/feedback/EmptyState";
import SkeletonCard from "../components/feedback/SkeletonCard";
import PageHeader from "../components/layout/PageHeader";
import RequirementIcon from "../components/workflows/RequirementIcon";
import TaskCommentsSection from "../components/workflows/TaskCommentsSection";
import TaskSlaPill from "../components/workflows/TaskSlaPill";
import TaskStatusPill from "../components/workflows/TaskStatusPill";
import { useMyTasks } from "../services/queries/workflowQueries";
import type { TaskWithWorkflow } from "../types/workflow";
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
  TASK_STATUS_ORDER,
  VISIBLE_TASK_STATUS_ORDER,
  type VisibleTaskStatus,
} from "../utils/taskStatus";
import { formatDateTime } from "../utils/dateFormat";
import { useTaskInteraction } from "../hooks/useTaskInteraction";

function toTaskStateKey(workflowUid: string, taskId: number): string {
  return `${workflowUid}:${taskId}`;
}

export default function MyTasksPage() {
  const myTasksQuery = useMyTasks();
  const {
    savingTaskIds,
    commentDrafts,
    savingCommentTaskIds,
    handleStatusChange,
    handleCommentDraftChange,
    handleTaskCommentSubmit,
  } = useTaskInteraction();
  const [search, setSearch] = useState<string>("");
  const [departmentFilter, setDepartmentFilter] = useState<string>("all");
  const [statusFilter, setStatusFilter] = useState<"all" | VisibleTaskStatus>("all");
  const [responsibilityFilter, setResponsibilityFilter] = useState<string>("all");
  const rows: TaskWithWorkflow[] = myTasksQuery.data ?? [];
  const isLoading = myTasksQuery.isLoading;
  const isRefreshing = myTasksQuery.isFetching;
  const error = myTasksQuery.error instanceof Error
    ? myTasksQuery.error.message
    : myTasksQuery.error
      ? "Aufgaben konnten nicht geladen werden."
      : null;

  const reload = useCallback(async () => {
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
  const visibleGroups = useMemo(() => groupedRows.filter((group) => group.items.length > 0), [groupedRows]);

  return (
    <main className="app-shell">
      <div className="page-container">
        <PageHeader title="Meine Aufgaben" />

        <section className="panel panel-muted">
          <div className="panel-head">
            <h2>Filter</h2>
          </div>

          <div className="toolbar-row toolbar-row-filters">
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
        </section>

        {isLoading ? (
          <section className="skeleton-stack-list" aria-label="Aufgaben werden geladen">
            {Array.from({ length: 5 }, (_, index) => (
              <SkeletonCard key={`task-skeleton-${index}`} variant="task" />
            ))}
          </section>
        ) : null}

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
            description="Aktuell sind keine Aufgaben zugeordnet."
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
                              {canChangeStatus ? (
                                <label className="field compact">
                                  <span>Status</span>
                                  <select
                                    value={visibleStatus}
                                    disabled={isSavingTask}
                                    onChange={(event) =>
                                      void handleStatusChange({
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
                              <p className="panel-note">
                                Aufgabe bleibt offen, bis ihre Abhängigkeiten erfüllt sind.
                              </p>
                            ) : null}

                            <TaskCommentsSection
                              task={row.task}
                              draftValue={commentDrafts[row.task.id] ?? ""}
                              isSaving={savingCommentTaskIds[row.task.id] === true}
                              onDraftChange={handleCommentDraftChange}
                              onSubmit={(taskId) => handleTaskCommentSubmit({ taskId, workflowUid })}
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
        ) : null}
      </div>
    </main>
  );
}
