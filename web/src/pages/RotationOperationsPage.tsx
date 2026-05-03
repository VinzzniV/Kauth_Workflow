import { Link } from "react-router-dom";
import EmptyState from "../components/feedback/EmptyState";
import SkeletonCard from "../components/feedback/SkeletonCard";
import PageHeader from "../components/layout/PageHeader";
import TaskSlaPill from "../components/workflows/TaskSlaPill";
import TaskStatusPill from "../components/workflows/TaskStatusPill";
import { useTaskInteraction } from "../hooks/useTaskInteraction";
import { useRotationOperationsView } from "../hooks/useRotationOperationsView";
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

export default function RotationOperationsPage() {
  const { savingTaskIds, handleStatusChange } = useTaskInteraction();
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
