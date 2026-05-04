import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import EmptyState from "../components/feedback/EmptyState";
import SkeletonCard from "../components/feedback/SkeletonCard";
import PageHeader from "../components/layout/PageHeader";
import ViewModeToggle, { type ViewMode } from "../components/layout/ViewModeToggle";
import TaskSlaPill from "../components/workflows/TaskSlaPill";
import TaskStatusPill from "../components/workflows/TaskStatusPill";
import { useTaskInteraction } from "../hooks/useTaskInteraction";
import { useRotationOperationsView, type RotationTaskRow } from "../hooks/useRotationOperationsView";
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

function FilterChip({ label, value }: { label: string; value: string }) {
  return (
    <span className="filter-chip">
      <strong>{label}</strong>
      <span>{value}</span>
    </span>
  );
}

type RotationSortKey = "person" | "task" | "department" | "status" | "anchor" | "due";
type SortDirection = "asc" | "desc";

function compareText(left: string, right: string): number {
  return left.localeCompare(right, "de", { sensitivity: "base" });
}

function compareNullableDate(left: string | null, right: string | null): number {
  const leftTime = left ? new Date(left).getTime() : Number.MAX_SAFE_INTEGER;
  const rightTime = right ? new Date(right).getTime() : Number.MAX_SAFE_INTEGER;
  return leftTime - rightTime;
}

export default function RotationOperationsPage() {
  const { savingTaskIds, handleStatusChange } = useTaskInteraction();
  const [taskViewMode, setTaskViewMode] = useState<ViewMode>("cards");
  const [sortKey, setSortKey] = useState<RotationSortKey>("anchor");
  const [sortDirection, setSortDirection] = useState<SortDirection>("asc");
  const {
    myTasksQuery,
    rotationRows,
    departmentOptions,
    inferredOwnDepartmentId,
    filteredRows,
    upcomingChanges,
    departmentSummaries,
    personSummaries,
    filters,
    setters,
  } = useRotationOperationsView();
  const selectedDepartmentLabel =
    departmentOptions.find(([departmentId]) => String(departmentId) === filters.departmentFilter)?.[1] ?? null;
  const statusLabelMap: Record<VisibleTaskStatus | "all", string> = {
    all: "Alle",
    open: "Offen",
    in_progress: "In Bearbeitung",
    blocked: "Blockiert",
    done: "Erledigt",
    failed: "Fehlgeschlagen",
    cancelled: "Storniert",
  };
  const hasActiveFilters =
    filters.search.trim().length > 0 ||
    filters.departmentFilter !== "all" ||
    filters.statusFilter !== "all" ||
    filters.changeWindowDays !== "14" ||
    !filters.onlyOpen ||
    filters.onlyItTasks ||
    filters.onlyOwnDepartment;
  const changeWindowLabelMap: Record<string, string> = {
    "0": "Ohne Grenze",
    "7": "7 Tage",
    "14": "14 Tage",
    "30": "30 Tage",
  };
  const resetFilters = () => {
    setters.setSearch("");
    setters.setDepartmentFilter("all");
    setters.setStatusFilter("all");
    setters.setChangeWindowDays("14");
    setters.setOnlyOpen(true);
    setters.setOnlyItTasks(false);
    setters.setOnlyOwnDepartment(false);
  };
  const sortedFilteredRows = useMemo(() => {
    const next = [...filteredRows].sort((left, right) => {
      switch (sortKey) {
        case "person":
          return compareText(left.rotation.displayName, right.rotation.displayName);
        case "task":
          return compareText(left.task.title, right.task.title);
        case "department":
          return compareText(left.rotation.departmentName, right.rotation.departmentName);
        case "status":
          return compareText(
            getVisibleTaskStatusLabel(getVisibleTaskStatus(left.task.status)),
            getVisibleTaskStatusLabel(getVisibleTaskStatus(right.task.status))
          );
        case "due":
          return compareNullableDate(left.task.dueAt, right.task.dueAt);
        default:
          return compareNullableDate(left.rotation.anchorDate, right.rotation.anchorDate);
      }
    });
    return sortDirection === "asc" ? next : next.reverse();
  }, [filteredRows, sortDirection, sortKey]);
  const updateSort = (nextKey: RotationSortKey) => {
    if (nextKey === sortKey) {
      setSortDirection((current) => (current === "asc" ? "desc" : "asc"));
      return;
    }

    setSortKey(nextKey);
    setSortDirection(nextKey === "due" || nextKey === "anchor" ? "asc" : "asc");
  };
  const getSortValue = (key: RotationSortKey) =>
    sortKey === key ? (sortDirection === "asc" ? "ascending" : "descending") : "none";
  const renderTaskCard = (row: RotationTaskRow) => {
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
  };

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
                value={filters.search}
                onChange={(event) => setters.setSearch(event.target.value)}
                placeholder="Person, Plan, Aufgabe oder TaskRef"
              />
            </label>

            <label className="field compact">
              <span>Ziel-Abteilung</span>
              <select
                value={filters.departmentFilter}
                onChange={(event) => setters.setDepartmentFilter(event.target.value)}
              >
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
                value={filters.statusFilter}
                onChange={(event) =>
                  setters.setStatusFilter(event.target.value as "all" | VisibleTaskStatus)
                }
              >
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
              <select
                value={filters.changeWindowDays}
                onChange={(event) => setters.setChangeWindowDays(event.target.value)}
              >
                <option value="7">7 Tagen</option>
                <option value="14">14 Tagen</option>
                <option value="30">30 Tagen</option>
                <option value="0">ohne Grenze</option>
              </select>
            </label>
          </div>

          <div className="toolbar-row toolbar-row-filters">
            <label className="checkbox-row">
              <input
                type="checkbox"
                checked={filters.onlyOpen}
                onChange={(event) => setters.setOnlyOpen(event.target.checked)}
              />
              <span>Nur offene Aufgaben</span>
            </label>
            <label className="checkbox-row">
              <input
                type="checkbox"
                checked={filters.onlyItTasks}
                onChange={(event) => setters.setOnlyItTasks(event.target.checked)}
              />
              <span>Nur IT-Aufgaben</span>
            </label>
            <label className="checkbox-row">
              <input
                type="checkbox"
                checked={filters.onlyOwnDepartment}
                onChange={(event) => setters.setOnlyOwnDepartment(event.target.checked)}
                disabled={inferredOwnDepartmentId === null}
              />
              <span>Nur eigene Abteilung</span>
            </label>
          </div>

          <div className="filter-panel-actions" aria-live="polite">
            <div className="filter-chip-row" aria-label="Aktive Filter">
              {filters.search.trim().length > 0 ? <FilterChip label="Suche" value={filters.search.trim()} /> : null}
              {selectedDepartmentLabel ? <FilterChip label="Ziel-Abteilung" value={selectedDepartmentLabel} /> : null}
              {filters.statusFilter !== "all" ? <FilterChip label="Status" value={statusLabelMap[filters.statusFilter]} /> : null}
              {filters.changeWindowDays !== "14" ? <FilterChip label="Wechsel" value={changeWindowLabelMap[filters.changeWindowDays] ?? `${filters.changeWindowDays} Tage`} /> : null}
              {!filters.onlyOpen ? <FilterChip label="Offene Aufgaben" value="Auch erledigte" /> : null}
              {filters.onlyItTasks ? <FilterChip label="Bereich" value="Nur IT-Aufgaben" /> : null}
              {filters.onlyOwnDepartment ? <FilterChip label="Scope" value="Nur eigene Abteilung" /> : null}
              {!hasActiveFilters ? <span className="filter-chip-empty">Keine aktiven Filter.</span> : null}
            </div>
            <button type="button" className="btn btn-ghost" onClick={resetFilters} disabled={!hasActiveFilters}>
              Filter zurücksetzen
            </button>
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
                <>
                  <div className="list-view-toolbar">
                    <ViewModeToggle value={taskViewMode} onChange={setTaskViewMode} />
                  </div>
                  {taskViewMode === "table" ? (
                    <>
                      <div className="operational-table-wrap">
                        <table className="operational-table" aria-label="Tabellenansicht Rotationsaufgaben">
                          <thead>
                            <tr>
                              <th scope="col" aria-sort={getSortValue("task")}>
                                <button type="button" className="operational-table-sort" onClick={() => updateSort("task")}>
                                  Aufgabe
                                </button>
                              </th>
                              <th scope="col" aria-sort={getSortValue("person")}>
                                <button type="button" className="operational-table-sort" onClick={() => updateSort("person")}>
                                  Person
                                </button>
                              </th>
                              <th scope="col" aria-sort={getSortValue("department")}>
                                <button type="button" className="operational-table-sort" onClick={() => updateSort("department")}>
                                  Bereich
                                </button>
                              </th>
                              <th scope="col" aria-sort={getSortValue("anchor")}>
                                <button type="button" className="operational-table-sort" onClick={() => updateSort("anchor")}>
                                  Wechselbezug
                                </button>
                              </th>
                              <th scope="col" aria-sort={getSortValue("due")}>
                                <button type="button" className="operational-table-sort" onClick={() => updateSort("due")}>
                                  Fällig
                                </button>
                              </th>
                              <th scope="col">Verantwortung</th>
                              <th scope="col" aria-sort={getSortValue("status")}>
                                <button type="button" className="operational-table-sort" onClick={() => updateSort("status")}>
                                  Status
                                </button>
                              </th>
                              <th scope="col" aria-label="Aktionen" />
                            </tr>
                          </thead>
                          <tbody>
                            {sortedFilteredRows.map((row) => {
                              const availableStatuses = getAvailableVisibleTaskStatuses(row.task.status, row.taskFamily);
                              return (
                                <tr key={row.taskRef}>
                                  <td>
                                    <div className="operational-cell-primary">
                                      <span className="operational-cell-title">{row.task.title}</span>
                                      <span className="operational-cell-meta">{row.rotation.planTitle}</span>
                                    </div>
                                  </td>
                                  <td>{row.rotation.displayName}</td>
                                  <td>{row.rotation.departmentName}</td>
                                  <td className="operational-cell-number">
                                    {row.rotation.anchorDate ? formatDate(row.rotation.anchorDate) : "-"}
                                  </td>
                                  <td className="operational-cell-number">
                                    {row.task.dueAt ? formatDateTime(row.task.dueAt) : "Keine Frist"}
                                  </td>
                                  <td>{getResponsibleResponsibilityLabel(row.task)}</td>
                                  <td>
                                    {availableStatuses.length > 1 ? (
                                      <label className="field compact">
                                        <span className="sr-only">Status</span>
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
                                    ) : (
                                      <TaskStatusPill status={row.task.status} />
                                    )}
                                  </td>
                                  <td>
                                    <div className="operational-table-actions">
                                      <Link className="btn btn-secondary" to={`/rotation/tasks/${encodeURIComponent(row.taskRef)}`}>
                                        Detail
                                      </Link>
                                    </div>
                                  </td>
                                </tr>
                              );
                            })}
                          </tbody>
                        </table>
                      </div>
                      <div className="task-groups-content operational-card-fallback" aria-label="Rotationsaufgabenliste">
                        <ul className="task-list">
                          {sortedFilteredRows.map(renderTaskCard)}
                        </ul>
                      </div>
                    </>
                  ) : (
                    <div className="task-groups-content" aria-label="Rotationsaufgabenliste">
                      <ul className="task-list">
                        {sortedFilteredRows.map(renderTaskCard)}
                      </ul>
                    </div>
                  )}
                </>
              )}
            </section>
          </>
        ) : null}
      </div>
    </main>
  );
}
