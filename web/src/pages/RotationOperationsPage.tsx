import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import EmptyState from "../components/feedback/EmptyState";
import SkeletonCard from "../components/feedback/SkeletonCard";
import PageHeader from "../components/layout/PageHeader";
import TaskSlaPill from "../components/workflows/TaskSlaPill";
import TaskStatusPill from "../components/workflows/TaskStatusPill";
import { useCurrentUser } from "../auth/useCurrentUser";
import { useTaskInteraction } from "../hooks/useTaskInteraction";
import { useMyTasks } from "../services/queries/workflowQueries";
import type { TaskWithWorkflow } from "../types/workflow";
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

type RotationTaskRow = TaskWithWorkflow & {
  taskFamily: "rotation";
  rotation: NonNullable<TaskWithWorkflow["rotation"]>;
};

type UpcomingChangeSummary = {
  key: string;
  rotationPlanId: number;
  personName: string;
  departmentName: string;
  triggerType: "enter" | "exit" | null;
  anchorDate: string | null;
  taskCount: number;
  taskRefs: string[];
};

function isOperationallyOpen(row: RotationTaskRow): boolean {
  return row.task.status === "open" || row.task.status === "in_progress";
}

function isUpcomingAnchorDate(anchorDate: string | null, maxDays: number): boolean {
  if (!anchorDate) {
    return false;
  }

  const today = new Date();
  const midnightToday = new Date(today.getFullYear(), today.getMonth(), today.getDate()).getTime();
  const anchor = new Date(anchorDate).getTime();
  if (Number.isNaN(anchor)) {
    return false;
  }

  const daysUntil = Math.floor((anchor - midnightToday) / (24 * 60 * 60 * 1000));
  return daysUntil >= 0 && daysUntil <= maxDays;
}

export default function RotationOperationsPage() {
  const { currentUser } = useCurrentUser();
  const myTasksQuery = useMyTasks();
  const { savingTaskIds, handleStatusChange } = useTaskInteraction();
  const [search, setSearch] = useState("");
  const [departmentFilter, setDepartmentFilter] = useState("all");
  const [statusFilter, setStatusFilter] = useState<"all" | VisibleTaskStatus>("all");
  const [changeWindowDays, setChangeWindowDays] = useState("14");
  const [onlyOpen, setOnlyOpen] = useState(true);
  const [onlyItTasks, setOnlyItTasks] = useState(false);
  const [onlyOwnDepartment, setOnlyOwnDepartment] = useState(false);

  const rotationRows = useMemo<RotationTaskRow[]>(
    () =>
      (myTasksQuery.data ?? []).filter(
        (row): row is RotationTaskRow => row.taskFamily === "rotation" && row.rotation !== null
      ),
    [myTasksQuery.data]
  );

  const departmentOptions = useMemo(
    () =>
      Array.from(
        new Map(rotationRows.map((row) => [row.rotation.departmentId, row.rotation.departmentName] as const)).entries()
      ).sort((left, right) => left[1].localeCompare(right[1], "de")),
    [rotationRows]
  );

  const inferredOwnDepartmentId = useMemo(() => {
    const scopedDepartmentId = currentUser?.permissionScopes.find(
      (scope) => typeof scope.scopeDepartmentId === "number"
    )?.scopeDepartmentId;
    return scopedDepartmentId ?? null;
  }, [currentUser]);

  const filteredRows = useMemo(() => {
    const normalizedSearch = search.trim().toLowerCase();

    return rotationRows
      .filter((row) => {
        const visibleStatus = getVisibleTaskStatus(row.task.status);
        const matchesDepartment =
          departmentFilter === "all" || String(row.rotation.departmentId) === departmentFilter;
        const matchesStatus = statusFilter === "all" || visibleStatus === statusFilter;
        const matchesOpen = !onlyOpen || isOperationallyOpen(row);
        const matchesIt =
          !onlyItTasks || getResponsibleResponsibilityLabel(row.task).toLowerCase().includes("it");
        const matchesOwnDepartment =
          !onlyOwnDepartment
          || inferredOwnDepartmentId === null
          || row.rotation.departmentId === inferredOwnDepartmentId;
        const matchesSearch =
          !normalizedSearch
          || row.rotation.displayName.toLowerCase().includes(normalizedSearch)
          || row.rotation.planTitle.toLowerCase().includes(normalizedSearch)
          || row.task.title.toLowerCase().includes(normalizedSearch)
          || row.taskRef.toLowerCase().includes(normalizedSearch)
          || row.rotation.departmentName.toLowerCase().includes(normalizedSearch);

        return (
          matchesDepartment
          && matchesStatus
          && matchesOpen
          && matchesIt
          && matchesOwnDepartment
          && matchesSearch
        );
      })
      .sort((left, right) => {
        const leftAnchor = left.rotation.anchorDate ? new Date(left.rotation.anchorDate).getTime() : Number.MAX_SAFE_INTEGER;
        const rightAnchor = right.rotation.anchorDate ? new Date(right.rotation.anchorDate).getTime() : Number.MAX_SAFE_INTEGER;
        if (leftAnchor !== rightAnchor) {
          return leftAnchor - rightAnchor;
        }

        return left.rotation.displayName.localeCompare(right.rotation.displayName, "de");
      });
  }, [
    departmentFilter,
    inferredOwnDepartmentId,
    onlyItTasks,
    onlyOpen,
    onlyOwnDepartment,
    rotationRows,
    search,
    statusFilter,
  ]);

  const upcomingChanges = useMemo<UpcomingChangeSummary[]>(() => {
    const grouped = new Map<string, UpcomingChangeSummary>();
    const daysWindow = Math.max(Number(changeWindowDays), 0);

    for (const row of filteredRows) {
      if (!isOperationallyOpen(row) || !isUpcomingAnchorDate(row.rotation.anchorDate, daysWindow)) {
        continue;
      }

      const key = `${row.rotation.rotationPlanId}:${row.rotation.anchorDate ?? "none"}:${row.rotation.triggerType ?? "none"}`;
      const existing = grouped.get(key);
      if (existing) {
        existing.taskCount += 1;
        existing.taskRefs.push(row.taskRef);
        continue;
      }

      grouped.set(key, {
        key,
        rotationPlanId: row.rotation.rotationPlanId,
        personName: row.rotation.displayName,
        departmentName: row.rotation.departmentName,
        triggerType: row.rotation.triggerType,
        anchorDate: row.rotation.anchorDate,
        taskCount: 1,
        taskRefs: [row.taskRef],
      });
    }

    return Array.from(grouped.values()).sort((left, right) => {
      const leftAnchor = left.anchorDate ? new Date(left.anchorDate).getTime() : Number.MAX_SAFE_INTEGER;
      const rightAnchor = right.anchorDate ? new Date(right.anchorDate).getTime() : Number.MAX_SAFE_INTEGER;
      return leftAnchor - rightAnchor;
    });
  }, [changeWindowDays, filteredRows]);

  const departmentSummaries = useMemo(() => {
    const grouped = new Map<string, { departmentName: string; count: number; openCount: number }>();
    for (const row of filteredRows) {
      const key = `${row.rotation.departmentId}`;
      const existing = grouped.get(key) ?? {
        departmentName: row.rotation.departmentName,
        count: 0,
        openCount: 0,
      };
      existing.count += 1;
      if (isOperationallyOpen(row)) {
        existing.openCount += 1;
      }
      grouped.set(key, existing);
    }

    return Array.from(grouped.values()).sort((left, right) => left.departmentName.localeCompare(right.departmentName, "de"));
  }, [filteredRows]);

  const personSummaries = useMemo(() => {
    const grouped = new Map<
      string,
      {
        planId: number;
        personName: string;
        departmentName: string;
        planTitle: string;
        taskCount: number;
        openCount: number;
        nextAnchorDate: string | null;
        firstTaskRef: string;
      }
    >();

    for (const row of filteredRows) {
      const key = `${row.rotation.rotationPlanId}`;
      const existing = grouped.get(key) ?? {
        planId: row.rotation.rotationPlanId,
        personName: row.rotation.displayName,
        departmentName: row.rotation.departmentName,
        planTitle: row.rotation.planTitle,
        taskCount: 0,
        openCount: 0,
        nextAnchorDate: row.rotation.anchorDate,
        firstTaskRef: row.taskRef,
      };
      existing.taskCount += 1;
      if (isOperationallyOpen(row)) {
        existing.openCount += 1;
      }
      if (
        row.rotation.anchorDate
        && (!existing.nextAnchorDate || new Date(row.rotation.anchorDate).getTime() < new Date(existing.nextAnchorDate).getTime())
      ) {
        existing.nextAnchorDate = row.rotation.anchorDate;
      }
      grouped.set(key, existing);
    }

    return Array.from(grouped.values()).sort((left, right) => {
      if (left.openCount !== right.openCount) {
        return right.openCount - left.openCount;
      }

      return left.personName.localeCompare(right.personName, "de");
    });
  }, [filteredRows]);

  return (
    <main className="app-shell">
      <div className="page-container">
        <PageHeader
          variant="workspace"
          title="Wechsel & Durchlaufaufgaben"
          description="IT und Fachbereiche sehen hier anstehende Wechsel, offene Maßnahmen und den Bearbeitungsstand je Person."
        />

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
                placeholder="Person, Plan, Aufgabe oder TaskRef"
              />
            </label>

            <label className="field compact">
              <span>Ziel-Abteilung</span>
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
              <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value as "all" | VisibleTaskStatus)}>
                <option value="all">Alle</option>
                <option value="open">Offen</option>
                <option value="in_progress">In Bearbeitung</option>
                <option value="done">Erledigt</option>
                <option value="failed">Fehlgeschlagen</option>
                <option value="cancelled">Storniert</option>
              </select>
            </label>

            <label className="field compact">
              <span>Kommende Wechsel in</span>
              <select value={changeWindowDays} onChange={(event) => setChangeWindowDays(event.target.value)}>
                <option value="7">7 Tagen</option>
                <option value="14">14 Tagen</option>
                <option value="30">30 Tagen</option>
                <option value="0">ohne Grenze</option>
              </select>
            </label>
          </div>

          <div className="toolbar-row toolbar-row-filters">
            <label className="checkbox-row">
              <input type="checkbox" checked={onlyOpen} onChange={(event) => setOnlyOpen(event.target.checked)} />
              <span>Nur offene Aufgaben</span>
            </label>
            <label className="checkbox-row">
              <input type="checkbox" checked={onlyItTasks} onChange={(event) => setOnlyItTasks(event.target.checked)} />
              <span>Nur IT-Aufgaben</span>
            </label>
            <label className="checkbox-row">
              <input
                type="checkbox"
                checked={onlyOwnDepartment}
                onChange={(event) => setOnlyOwnDepartment(event.target.checked)}
                disabled={inferredOwnDepartmentId === null}
              />
              <span>Nur eigene Abteilung</span>
            </label>
          </div>
        </section>

        {myTasksQuery.isLoading ? (
          <section className="skeleton-stack-list" aria-label="Rotationsaufgaben werden geladen">
            {Array.from({ length: 4 }, (_, index) => (
              <SkeletonCard key={`rotation-task-skeleton-${index}`} variant="task" />
            ))}
          </section>
        ) : null}

        {!myTasksQuery.isLoading && myTasksQuery.error ? (
          <EmptyState
            title="Wechselansicht konnte nicht geladen werden."
            description={
              myTasksQuery.error instanceof Error
                ? myTasksQuery.error.message
                : "Die Aufgabenansicht ist fehlgeschlagen."
            }
            actionLabel="Erneut versuchen"
            onAction={() => void myTasksQuery.refetch()}
          />
        ) : null}

        {!myTasksQuery.isLoading && !myTasksQuery.error && rotationRows.length === 0 ? (
          <EmptyState
            title="Keine Durchlaufaufgaben vorhanden"
            description="Aktuell sind keine rotierenden Maßnahmen für IT oder Fachbereiche zugewiesen."
          />
        ) : null}

        {!myTasksQuery.isLoading && !myTasksQuery.error && rotationRows.length > 0 ? (
          <>
            <section className="panel">
              <div className="panel-head">
                <h2>Kommende Wechsel</h2>
                <p>Wechseltermine im gewählten Zeitfenster, zusammengefasst pro Person und Stationswechsel.</p>
              </div>

              {upcomingChanges.length === 0 ? (
                <p className="panel-note">Im aktuellen Filterfenster stehen keine Wechsel an.</p>
              ) : (
                <div className="workflow-grid" aria-label="Kommende Wechsel">
                  {upcomingChanges.map((change) => (
                    <article key={change.key} className="workflow-card card-list">
                      <div className="workflow-card-top">
                        <h3>{change.personName}</h3>
                        <span className="status-pill running">
                          {change.triggerType === "exit" ? "Austritt" : "Eintritt"}
                        </span>
                      </div>
                      <dl className="workflow-meta">
                        <div>
                          <dt>Zielbereich</dt>
                          <dd>{change.departmentName}</dd>
                        </div>
                        <div>
                          <dt>Wechseltermin</dt>
                          <dd>{change.anchorDate ? formatDate(change.anchorDate) : "-"}</dd>
                        </div>
                        <div>
                          <dt>Offene Maßnahmen</dt>
                          <dd>{change.taskCount}</dd>
                        </div>
                      </dl>
                      <div className="action-row">
                        <Link className="btn btn-primary" to={`/rotation/tasks/${encodeURIComponent(change.taskRefs[0]!)}`}>
                          Detail öffnen
                        </Link>
                      </div>
                    </article>
                  ))}
                </div>
              )}
            </section>

            <section className="panel">
              <div className="panel-head">
                <h2>Aufgaben nach Abteilung</h2>
              </div>
              <div className="workflow-grid" aria-label="Aufgaben nach Abteilung">
                {departmentSummaries.map((summary) => (
                  <article key={summary.departmentName} className="workflow-card card-list">
                    <div className="workflow-card-top">
                      <h3>{summary.departmentName}</h3>
                    </div>
                    <dl className="workflow-meta">
                      <div>
                        <dt>Gesamt</dt>
                        <dd>{summary.count}</dd>
                      </div>
                      <div>
                        <dt>Offen</dt>
                        <dd>{summary.openCount}</dd>
                      </div>
                    </dl>
                  </article>
                ))}
              </div>
            </section>

            <section className="panel">
              <div className="panel-head">
                <h2>Aufgaben nach Person</h2>
              </div>
              <div className="workflow-grid" aria-label="Aufgaben nach Person">
                {personSummaries.map((summary) => (
                  <article key={summary.planId} className="workflow-card card-list">
                    <div className="workflow-card-top">
                      <h3>{summary.personName}</h3>
                      <span className="status-pill open">{summary.openCount} offen</span>
                    </div>
                    <dl className="workflow-meta">
                      <div>
                        <dt>Plan</dt>
                        <dd>{summary.planTitle}</dd>
                      </div>
                      <div>
                        <dt>Bereich</dt>
                        <dd>{summary.departmentName}</dd>
                      </div>
                      <div>
                        <dt>Nächster Wechsel</dt>
                        <dd>{summary.nextAnchorDate ? formatDate(summary.nextAnchorDate) : "-"}</dd>
                      </div>
                    </dl>
                    <div className="action-row">
                      <Link className="btn btn-secondary" to={`/rotation/tasks/${encodeURIComponent(summary.firstTaskRef)}`}>
                        Detail öffnen
                      </Link>
                    </div>
                  </article>
                ))}
              </div>
            </section>

            <section className="panel">
              <div className="panel-head">
                <h2>Offene Maßnahmen</h2>
              </div>

              {filteredRows.length === 0 ? (
                <p className="panel-note">Die aktuelle Filterkombination liefert keine Maßnahmen.</p>
              ) : (
                <div className="task-groups-content" aria-label="Rotationsaufgabenliste">
                  <ul className="task-list">
                    {filteredRows.map((row) => {
                      const availableStatuses = getAvailableVisibleTaskStatuses(row.task.status, row.taskFamily);
                      return (
                        <li key={row.taskRef} className="task-card">
                          <div className="task-card-top">
                            <div>
                              <h3>{row.task.title}</h3>
                              <p className="panel-text">{row.rotation.displayName} · {row.rotation.planTitle}</p>
                            </div>
                            <div className="task-card-pill-group">
                              <TaskSlaPill status={row.task.slaStatus} />
                              <TaskStatusPill status={row.task.status} />
                            </div>
                          </div>

                          <div className="task-card-layout">
                            <div className="task-card-content">
                              <dl className="task-meta">
                                <div>
                                  <dt>Bereich</dt>
                                  <dd>{row.rotation.departmentName}</dd>
                                </div>
                                <div>
                                  <dt>Wechselbezug</dt>
                                  <dd>{row.rotation.anchorDate ? formatDate(row.rotation.anchorDate) : "-"}</dd>
                                </div>
                                <div>
                                  <dt>Verantwortung</dt>
                                  <dd>{getResponsibleResponsibilityLabel(row.task)}</dd>
                                </div>
                                <div>
                                  <dt>Person</dt>
                                  <dd>{getResponsibleUserLabel(row.task)}</dd>
                                </div>
                                <div>
                                  <dt>Fällig</dt>
                                  <dd>{row.task.dueAt ? formatDateTime(row.task.dueAt) : "Keine Frist"}</dd>
                                </div>
                                <div>
                                  <dt>Detail</dt>
                                  <dd>
                                    <Link className="task-meta-link" to={`/rotation/tasks/${encodeURIComponent(row.taskRef)}`}>
                                      Detailansicht öffnen
                                    </Link>
                                  </dd>
                                </div>
                              </dl>

                              {availableStatuses.length > 1 ? (
                                <div className="toolbar-row task-actions-row">
                                  <label className="field compact">
                                    <span>Status</span>
                                    <select
                                      value={getVisibleTaskStatus(row.task.status)}
                                      disabled={savingTaskIds[row.task.id] === true}
                                      onChange={(event) =>
                                        void handleStatusChange({
                                          taskId: row.task.id,
                                          taskRef: row.taskRef,
                                          taskFamily: row.taskFamily,
                                          rotationPlanId: row.rotation.rotationPlanId,
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
                                </div>
                              ) : null}
                            </div>
                          </div>
                        </li>
                      );
                    })}
                  </ul>
                </div>
              )}
            </section>
          </>
        ) : null}
      </div>
    </main>
  );
}
