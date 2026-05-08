// 360-Grad-Personenakte: zieht Vorgaenge, offene Aufgaben, Benachrichtigungen und Directory-Kontext
// in einer zusammenhaengenden Arbeitsflaeche zusammen. Nutzt nur bestehende Backend-APIs
// (`PersonWorkflowHistory` + `WorkflowDetail`); Aggregation passiert clientseitig.
import { useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useCurrentUser } from "../auth/useCurrentUser";
import EmptyState from "../components/feedback/EmptyState";
import LoadingState from "../components/feedback/LoadingState";
import PageHeader from "../components/layout/PageHeader";
import ViewModeToggle, { type ViewMode } from "../components/layout/ViewModeToggle";
import {
  usePersonWorkflowAggregates,
  type AggregatedNotification,
  type AggregatedTask,
} from "../hooks/usePersonWorkflowAggregates";
import { usePersonWorkflowHistory } from "../services/queries/peopleQueries";
import { updatePerson } from "../services/peopleApi";
import { queryKeys } from "../services/queryKeys";
import type {
  PersonWorkflowHistory,
  PersonWorkflowSummary,
  WorkflowNotification,
} from "../types/workflow";
import { formatDate, formatDateTime } from "../utils/dateFormat";
import {
  formatDirectoryLinkStatus,
  formatEmploymentStatus,
  getEmploymentStatusClass,
} from "../utils/employmentStatus";
import {
  getTaskSlaClassName,
  getTaskSlaLabel,
  getTaskStatusClassName,
  getTaskStatusLabel,
} from "../utils/taskStatus";
import {
  getWorkflowRuntimeStatusLabel,
  getWorkflowRuntimeStatusPillClass,
} from "../utils/workflowStatus";

type WorkspaceTab = "overview" | "tasks" | "notifications" | "workflows";
type HistorySortKey = "created" | "completed" | "type" | "status" | "department";
type SortDirection = "asc" | "desc";

const TERMINAL_WORKFLOW_STATUSES = new Set<string>(["completed"]);

function getDirectoryChipClass(status: string | null): string {
  switch (status) {
    case "linked":
      return "status-pill running";
    case "user_only":
      return "status-pill open";
    case "unlinked":
      return "status-pill completed";
    default:
      return "status-pill";
  }
}

function isWorkflowActive(summary: PersonWorkflowSummary): boolean {
  if (summary.archivedAt) {
    return false;
  }
  return !TERMINAL_WORKFLOW_STATUSES.has(summary.workflowStatus);
}

function compareText(left: string, right: string): number {
  return left.localeCompare(right, "de", { sensitivity: "base" });
}

function compareNullableDate(left: string | null, right: string | null): number {
  const leftTime = left ? new Date(left).getTime() : Number.MAX_SAFE_INTEGER;
  const rightTime = right ? new Date(right).getTime() : Number.MAX_SAFE_INTEGER;
  return leftTime - rightTime;
}

function getNotificationTypeLabel(notificationType: string): string {
  switch (notificationType) {
    case "workflow_created":
      return "Workflow gestartet";
    case "task_ready":
      return "Aufgabe bereit";
    case "workflow_completed":
      return "Workflow abgeschlossen";
    default:
      return notificationType;
  }
}

function getNotificationStatusLabel(status: WorkflowNotification["status"]): string {
  switch (status) {
    case "pending":
      return "Ausstehend";
    case "sent":
      return "Gesendet";
    case "failed":
      return "Fehlgeschlagen";
    case "disabled":
      return "Deaktiviert";
    default:
      return status;
  }
}

function getNotificationStatusBadgeClass(status: WorkflowNotification["status"]): string {
  switch (status) {
    case "sent":
      return "badge badge--success";
    case "failed":
      return "badge badge--error";
    case "pending":
      return "badge badge--default";
    case "disabled":
      return "badge badge--default";
    default:
      return "badge badge--default";
  }
}

function WorkflowHistoryCard({
  workflow,
  displayName,
}: {
  workflow: PersonWorkflowSummary;
  displayName: string;
}) {
  const name = `${workflow.firstName} ${workflow.lastName}`.trim();
  return (
    <article key={workflow.uid} className="workflow-card card-list">
      <div className="workflow-card-top">
        <h3>{name || displayName}</h3>
        <span className={`status-pill ${getWorkflowRuntimeStatusPillClass(workflow.workflowStatus)}`}>
          {getWorkflowRuntimeStatusLabel(workflow.workflowStatus)}
        </span>
      </div>

      <dl className="workflow-meta">
        <div>
          <dt>Prozesstyp</dt>
          <dd>{workflow.processType.name}</dd>
        </div>
        <div>
          <dt>Stelle</dt>
          <dd>{workflow.roleName}</dd>
        </div>
        <div>
          <dt>Abteilung</dt>
          <dd>{workflow.departmentName}</dd>
        </div>
        <div>
          <dt>Erstellt</dt>
          <dd>{formatDateTime(workflow.createdAt)}</dd>
        </div>
        {workflow.completedAt ? (
          <div>
            <dt>Abgeschlossen</dt>
            <dd>{formatDate(workflow.completedAt)}</dd>
          </div>
        ) : null}
        {workflow.archivedAt ? (
          <div>
            <dt>Archiviert</dt>
            <dd>{formatDate(workflow.archivedAt)}</dd>
          </div>
        ) : null}
      </dl>

      <div className="action-row">
        <Link className="btn btn-secondary" to={`/workflows/${workflow.uid}`}>
          Öffnen
        </Link>
      </div>
    </article>
  );
}

function PersonOverviewSection({
  history,
  activeWorkflows,
  setActiveTab,
  personId,
  canEdit,
}: {
  history: PersonWorkflowHistory;
  activeWorkflows: PersonWorkflowSummary[];
  setActiveTab: (tab: WorkspaceTab) => void;
  personId: number;
  canEdit: boolean;
}) {
  const queryClient = useQueryClient();
  const [isEditing, setIsEditing] = useState(false);
  const [editEntryDate, setEditEntryDate] = useState<string>("");
  const [editBadgeNumber, setEditBadgeNumber] = useState<string>("");

  const startEdit = () => {
    setEditEntryDate(history.entryDate ? history.entryDate.substring(0, 10) : "");
    setEditBadgeNumber(history.badgeNumber != null ? String(history.badgeNumber) : "");
    setIsEditing(true);
  };

  const cancelEdit = () => setIsEditing(false);

  const mutation = useMutation({
    mutationFn: () =>
      updatePerson(personId, {
        entryDate: editEntryDate || null,
        badgeNumber: editBadgeNumber !== "" ? Number(editBadgeNumber) : null,
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.people.history(personId) });
      setIsEditing(false);
    },
  });

  const hasGaps = history.entryDate === null || history.badgeNumber === null;

  return (
    <div className="content-stack">
      <section className="panel">
        <div className="panel-head">
          <h2>Stammdaten</h2>
          <p>Personalstammdaten aus der internen Personenverwaltung.</p>
          {canEdit && !isEditing ? (
            <button type="button" className="btn btn-secondary" onClick={startEdit}>
              Bearbeiten
            </button>
          ) : null}
        </div>

        {hasGaps && !isEditing ? (
          <p className="panel-note" style={{ color: "var(--text-warning, #b45309)" }}>
            Eintrittsdatum und/oder Ausweisnummer fehlen noch. Bitte ergänzen.
          </p>
        ) : null}

        {isEditing ? (
          <div className="content-stack" style={{ gap: "0.75rem" }}>
            <dl className="workflow-meta">
              <div>
                <dt>Stamm-Abteilung</dt>
                <dd>{history.departmentName ?? "-"}</dd>
              </div>
              <div>
                <dt>Aktuelle Stelle</dt>
                <dd>{history.roleName ?? "-"}</dd>
              </div>
              <div>
                <dt>Beschäftigungsstatus</dt>
                <dd>{formatEmploymentStatus(history.employmentStatus)}</dd>
              </div>
              <div>
                <dt>Personalnummer</dt>
                <dd>{history.employeeNumber ?? "-"}</dd>
              </div>
              <div>
                <dt>
                  <label htmlFor="edit-badge-number">Ausweisnummer</label>
                </dt>
                <dd>
                  <input
                    id="edit-badge-number"
                    type="number"
                    className="form-input"
                    value={editBadgeNumber}
                    onChange={(e) => setEditBadgeNumber(e.target.value)}
                    placeholder="Ausweisnummer"
                    min={0}
                  />
                </dd>
              </div>
              <div>
                <dt>
                  <label htmlFor="edit-entry-date">Eintritt</label>
                </dt>
                <dd>
                  <input
                    id="edit-entry-date"
                    type="date"
                    className="form-input"
                    value={editEntryDate}
                    onChange={(e) => setEditEntryDate(e.target.value)}
                  />
                </dd>
              </div>
              <div>
                <dt>Austritt</dt>
                <dd>{history.exitDate ? formatDate(history.exitDate) : "-"}</dd>
              </div>
            </dl>
            <div className="action-row">
              <button
                type="button"
                className="btn btn-primary"
                onClick={() => mutation.mutate()}
                disabled={mutation.isPending}
              >
                {mutation.isPending ? "Wird gespeichert…" : "Speichern"}
              </button>
              <button
                type="button"
                className="btn btn-ghost"
                onClick={cancelEdit}
                disabled={mutation.isPending}
              >
                Abbrechen
              </button>
              {mutation.isError ? (
                <span style={{ color: "var(--text-error)", fontSize: "0.875rem" }}>
                  Speichern fehlgeschlagen.
                </span>
              ) : null}
            </div>
          </div>
        ) : (
          <dl className="workflow-meta">
            <div>
              <dt>Stamm-Abteilung</dt>
              <dd>{history.departmentName ?? "-"}</dd>
            </div>
            <div>
              <dt>Aktuelle Stelle</dt>
              <dd>{history.roleName ?? "-"}</dd>
            </div>
            <div>
              <dt>Beschäftigungsstatus</dt>
              <dd>{formatEmploymentStatus(history.employmentStatus)}</dd>
            </div>
            <div>
              <dt>Personalnummer</dt>
              <dd>{history.employeeNumber ?? "-"}</dd>
            </div>
            <div>
              <dt>Ausweisnummer</dt>
              <dd>{history.badgeNumber ?? "-"}</dd>
            </div>
            <div>
              <dt>Eintritt</dt>
              <dd>{history.entryDate ? formatDate(history.entryDate) : "-"}</dd>
            </div>
            <div>
              <dt>Austritt</dt>
              <dd>{history.exitDate ? formatDate(history.exitDate) : "-"}</dd>
            </div>
          </dl>
        )}
      </section>

      <section className="panel">
        <div className="panel-head">
          <h2>Verzeichnis-Kontext</h2>
          <p>Synchronisierter Status aus dem Verzeichnis (Entra/Active Directory).</p>
        </div>
        <dl className="workflow-meta">
          <div>
            <dt>Directory-Link</dt>
            <dd>{formatDirectoryLinkStatus(history.directoryLinkStatus)}</dd>
          </div>
          <div>
            <dt>Directory-Name</dt>
            <dd>{history.directoryDisplayName ?? "-"}</dd>
          </div>
          <div>
            <dt>UPN</dt>
            <dd>{history.directoryUserPrincipalName ?? "-"}</dd>
          </div>
          <div>
            <dt>Mail</dt>
            <dd>{history.directoryMail ?? "-"}</dd>
          </div>
          <div>
            <dt>Directory-Personalnr.</dt>
            <dd>{history.directoryEmployeeNumber ?? "-"}</dd>
          </div>
          <div>
            <dt>Letztes abgeschlossenes Onboarding</dt>
            <dd>
              {history.latestCompletedOnboardingWorkflowUid ? (
                <Link to={`/workflows/${history.latestCompletedOnboardingWorkflowUid}`}>
                  {history.latestCompletedOnboardingWorkflowUid}
                </Link>
              ) : (
                "-"
              )}
            </dd>
          </div>
          <div>
            <dt>Onboarding-Datum</dt>
            <dd>
              {history.latestCompletedOnboardingAt
                ? formatDate(history.latestCompletedOnboardingAt)
                : "-"}
            </dd>
          </div>
        </dl>
      </section>

      <section className="panel">
        <div className="panel-head">
          <h2>Aktive Vorgänge ({activeWorkflows.length})</h2>
          <p>Schnellzugriff auf laufende Vorgänge dieser Person.</p>
        </div>
        {activeWorkflows.length === 0 ? (
          <p className="panel-note">Aktuell laufen keine Vorgänge für diese Person.</p>
        ) : (
          <ul className="dashboard-work-list">
            {activeWorkflows.map((workflow) => (
              <li key={workflow.uid}>
                <Link className="dashboard-work-item" to={`/workflows/${workflow.uid}`}>
                  <div>
                    <div className="dashboard-work-item__title">{workflow.processType.name}</div>
                    <div className="dashboard-work-item__detail">
                      {workflow.roleName} · {workflow.departmentName} · seit{" "}
                      {formatDateTime(workflow.createdAt)}
                    </div>
                  </div>
                  <span
                    className={`status-pill ${getWorkflowRuntimeStatusPillClass(
                      workflow.workflowStatus
                    )}`}
                  >
                    {getWorkflowRuntimeStatusLabel(workflow.workflowStatus)}
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        )}
        {activeWorkflows.length > 0 ? (
          <div className="action-row">
            <button
              type="button"
              className="btn btn-ghost"
              onClick={() => setActiveTab("tasks")}
            >
              Offene Aufgaben dieser Vorgänge ansehen
            </button>
          </div>
        ) : null}
      </section>
    </div>
  );
}

function PersonOpenTasksSection({
  tasks,
  isLoading,
  errorCount,
  totalActiveQueries,
}: {
  tasks: AggregatedTask[];
  isLoading: boolean;
  errorCount: number;
  totalActiveQueries: number;
}) {
  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Offene Aufgaben ({tasks.length})</h2>
        <p>
          Aggregiert aus allen aktiven Vorgängen dieser Person, sortiert nach Frist und
          Status.
        </p>
      </div>

      {totalActiveQueries === 0 ? (
        <p className="panel-note">Keine aktiven Vorgänge — daher keine offenen Aufgaben.</p>
      ) : isLoading ? (
        <LoadingState title="Aufgaben werden geladen..." />
      ) : tasks.length === 0 ? (
        <p className="panel-note">
          Keine offenen Aufgaben in den aktiven Vorgängen dieser Person.
        </p>
      ) : (
        <ul className="dashboard-work-list">
          {tasks.map(({ task, context }) => (
            <li key={`${context.workflowUid}-${task.id}`}>
              <Link
                className="dashboard-work-item"
                to={`/workflows/${context.workflowUid}#task-${task.id}`}
              >
                <div>
                  <div className="dashboard-work-item__title">{task.title}</div>
                  <div className="dashboard-work-item__detail">
                    {context.processTypeName} · {context.departmentName} · {context.roleName}
                    {task.dueAt ? ` · fällig ${formatDate(task.dueAt)}` : ""}
                    {task.processArea ? ` · ${task.processArea}` : ""}
                  </div>
                </div>
                <div
                  style={{
                    display: "inline-flex",
                    alignItems: "center",
                    gap: "0.4rem",
                    flexWrap: "wrap",
                    justifyContent: "flex-end",
                  }}
                >
                  <span className={`task-pill ${getTaskStatusClassName(task.status)}`}>
                    {getTaskStatusLabel(task.status)}
                  </span>
                  {task.slaStatus !== "none" ? (
                    <span className={`task-sla-pill ${getTaskSlaClassName(task.slaStatus)}`}>
                      {getTaskSlaLabel(task.slaStatus)}
                    </span>
                  ) : null}
                </div>
              </Link>
            </li>
          ))}
        </ul>
      )}

      {errorCount > 0 ? (
        <p className="panel-note">
          {errorCount} aktive{errorCount > 1 ? "" : "r"} Vorgang konnte nicht geladen werden.
        </p>
      ) : null}
    </section>
  );
}

function PersonNotificationsSection({
  pending,
  failed,
  isLoading,
  errorCount,
  totalActiveQueries,
}: {
  pending: AggregatedNotification[];
  failed: AggregatedNotification[];
  isLoading: boolean;
  errorCount: number;
  totalActiveQueries: number;
}) {
  const renderRow = ({ notification, context }: AggregatedNotification) => (
    <tr key={`${context.workflowUid}-${notification.id}`}>
      <td>{getNotificationTypeLabel(notification.notificationType)}</td>
      <td>
        <span>{notification.targetName}</span>
        <br />
        <span className="text-muted" style={{ fontSize: "0.85em" }}>
          {notification.targetEmail}
        </span>
      </td>
      <td>
        <span className={getNotificationStatusBadgeClass(notification.status)}>
          {getNotificationStatusLabel(notification.status)}
        </span>
      </td>
      <td>{notification.attempts}</td>
      <td>
        <Link to={`/workflows/${context.workflowUid}`}>{context.processTypeName}</Link>
        <br />
        <span className="text-muted" style={{ fontSize: "0.85em" }}>
          {context.departmentName}
        </span>
      </td>
      <td>
        {notification.lastError ? (
          <span className="text-error" style={{ fontSize: "0.85em" }}>
            {notification.lastError}
          </span>
        ) : (
          "—"
        )}
      </td>
    </tr>
  );

  if (totalActiveQueries === 0) {
    return (
      <section className="panel">
        <div className="panel-head">
          <h2>Benachrichtigungen</h2>
          <p>Keine aktiven Vorgänge — daher kein Versand-Backlog für diese Person.</p>
        </div>
      </section>
    );
  }

  if (isLoading && pending.length === 0 && failed.length === 0) {
    return (
      <section className="panel">
        <div className="panel-head">
          <h2>Benachrichtigungen</h2>
        </div>
        <LoadingState title="Benachrichtigungen werden geladen..." />
      </section>
    );
  }

  const hasAny = pending.length > 0 || failed.length > 0;

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Benachrichtigungen</h2>
        <p>
          Ausstehende und fehlgeschlagene Benachrichtigungen aus den aktiven Vorgängen dieser
          Person.
        </p>
      </div>

      {!hasAny ? (
        <p className="panel-note">
          Keine ausstehenden oder fehlgeschlagenen Benachrichtigungen.
        </p>
      ) : (
        <div className="content-stack">
          {failed.length > 0 ? (
            <>
              <h3 style={{ margin: 0 }}>Fehlgeschlagen ({failed.length})</h3>
              <table className="table">
                <thead>
                  <tr>
                    <th>Typ</th>
                    <th>Empfänger</th>
                    <th>Status</th>
                    <th>Versuche</th>
                    <th>Vorgang</th>
                    <th>Fehler</th>
                  </tr>
                </thead>
                <tbody>{failed.map(renderRow)}</tbody>
              </table>
            </>
          ) : null}

          {pending.length > 0 ? (
            <>
              <h3 style={{ margin: 0 }}>Ausstehend ({pending.length})</h3>
              <table className="table">
                <thead>
                  <tr>
                    <th>Typ</th>
                    <th>Empfänger</th>
                    <th>Status</th>
                    <th>Versuche</th>
                    <th>Vorgang</th>
                    <th>Fehler</th>
                  </tr>
                </thead>
                <tbody>{pending.map(renderRow)}</tbody>
              </table>
            </>
          ) : null}
        </div>
      )}

      {errorCount > 0 ? (
        <p className="panel-note">
          {errorCount} aktive{errorCount > 1 ? "" : "r"} Vorgang konnte nicht geladen werden.
        </p>
      ) : null}
    </section>
  );
}

function PersonWorkflowsListSection({
  history,
  displayName,
  isRetroactivelyImported,
}: {
  history: PersonWorkflowHistory;
  displayName: string;
  isRetroactivelyImported: boolean;
}) {
  const [viewMode, setViewMode] = useState<ViewMode>("cards");
  const [sortKey, setSortKey] = useState<HistorySortKey>("created");
  const [sortDirection, setSortDirection] = useState<SortDirection>("desc");

  const sortedWorkflows = useMemo(() => {
    const next = [...history.workflows].sort((left, right) => {
      switch (sortKey) {
        case "completed":
          return compareNullableDate(left.completedAt, right.completedAt);
        case "type":
          return compareText(left.processType.name, right.processType.name);
        case "status":
          return compareText(
            getWorkflowRuntimeStatusLabel(left.workflowStatus),
            getWorkflowRuntimeStatusLabel(right.workflowStatus)
          );
        case "department":
          return compareText(left.departmentName, right.departmentName);
        default:
          return compareNullableDate(left.createdAt, right.createdAt);
      }
    });
    return sortDirection === "asc" ? next : next.reverse();
  }, [history.workflows, sortDirection, sortKey]);

  const updateSort = (nextKey: HistorySortKey) => {
    if (nextKey === sortKey) {
      setSortDirection((current) => (current === "asc" ? "desc" : "asc"));
      return;
    }

    setSortKey(nextKey);
    setSortDirection(nextKey === "created" || nextKey === "completed" ? "desc" : "asc");
  };
  const getSortValue = (key: HistorySortKey) =>
    sortKey === key ? (sortDirection === "asc" ? "ascending" : "descending") : "none";

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Vorgänge ({history.workflows.length})</h2>
        <p>Vollständige Prozesshistorie dieser Person inkl. archivierter Vorgänge.</p>
      </div>

      {history.workflows.length === 0 ? (
        isRetroactivelyImported ? (
          <div className="content-stack" style={{ gap: "0.5rem" }}>
            <p className="panel-note">
              Diese Person wurde nachträglich aus dem Entra-Verzeichnis importiert — es bestehen
              noch keine Vorgänge.
            </p>
            <p className="panel-note">
              Um einen Vorgang zu starten (z.&nbsp;B. Offboarding oder Stellenwechsel), nutzen
              Sie den Button <strong>„Neuen Vorgang anlegen"</strong> oben auf dieser Seite.
            </p>
          </div>
        ) : (
          <p className="panel-note">Noch keine Vorgänge für diese Person angelegt.</p>
        )
      ) : (
        <>
          <div className="list-view-toolbar">
            <ViewModeToggle value={viewMode} onChange={setViewMode} />
          </div>
          {viewMode === "table" ? (
            <>
              <div className="operational-table-wrap">
                <table
                  className="operational-table"
                  aria-label="Tabellenansicht Vorgänge dieser Person"
                >
                  <thead>
                    <tr>
                      <th scope="col">Vorgang</th>
                      <th scope="col" aria-sort={getSortValue("type")}>
                        <button
                          type="button"
                          className="operational-table-sort"
                          onClick={() => updateSort("type")}
                        >
                          Prozesstyp
                        </button>
                      </th>
                      <th scope="col" aria-sort={getSortValue("status")}>
                        <button
                          type="button"
                          className="operational-table-sort"
                          onClick={() => updateSort("status")}
                        >
                          Status
                        </button>
                      </th>
                      <th scope="col" aria-sort={getSortValue("department")}>
                        <button
                          type="button"
                          className="operational-table-sort"
                          onClick={() => updateSort("department")}
                        >
                          Abteilung
                        </button>
                      </th>
                      <th scope="col">Stelle</th>
                      <th scope="col" aria-sort={getSortValue("created")}>
                        <button
                          type="button"
                          className="operational-table-sort"
                          onClick={() => updateSort("created")}
                        >
                          Erstellt
                        </button>
                      </th>
                      <th scope="col" aria-sort={getSortValue("completed")}>
                        <button
                          type="button"
                          className="operational-table-sort"
                          onClick={() => updateSort("completed")}
                        >
                          Abgeschlossen
                        </button>
                      </th>
                      <th scope="col" aria-label="Aktionen" />
                    </tr>
                  </thead>
                  <tbody>
                    {sortedWorkflows.map((workflow) => (
                      <tr key={workflow.uid}>
                        <td>
                          <div className="operational-cell-primary">
                            <span className="operational-cell-title">
                              {`${workflow.firstName} ${workflow.lastName}`.trim() ||
                                displayName}
                            </span>
                            <span className="operational-cell-meta">{workflow.uid}</span>
                          </div>
                        </td>
                        <td>{workflow.processType.name}</td>
                        <td>
                          <span
                            className={`status-pill ${getWorkflowRuntimeStatusPillClass(
                              workflow.workflowStatus
                            )}`}
                          >
                            {getWorkflowRuntimeStatusLabel(workflow.workflowStatus)}
                          </span>
                        </td>
                        <td>{workflow.departmentName}</td>
                        <td>{workflow.roleName}</td>
                        <td className="operational-cell-number">
                          {formatDateTime(workflow.createdAt)}
                        </td>
                        <td className="operational-cell-number">
                          {workflow.completedAt ? formatDate(workflow.completedAt) : "-"}
                        </td>
                        <td>
                          <div className="operational-table-actions">
                            <Link
                              className="btn btn-secondary"
                              to={`/workflows/${workflow.uid}`}
                            >
                              Öffnen
                            </Link>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <div
                className="workflow-grid operational-card-fallback"
                aria-label="Vorgänge dieser Person"
              >
                {sortedWorkflows.map((workflow) => (
                  <WorkflowHistoryCard
                    key={workflow.uid}
                    workflow={workflow}
                    displayName={displayName}
                  />
                ))}
              </div>
            </>
          ) : (
            <div className="workflow-grid" aria-label="Vorgänge dieser Person">
              {sortedWorkflows.map((workflow) => (
                <WorkflowHistoryCard
                  key={workflow.uid}
                  workflow={workflow}
                  displayName={displayName}
                />
              ))}
            </div>
          )}
        </>
      )}
    </section>
  );
}

export default function PersonWorkflowHistoryPage() {
  const { personId } = useParams<{ personId: string }>();
  const { capabilities, canAccessFeature } = useCurrentUser();
  const [activeTab, setActiveTab] = useState<WorkspaceTab>("overview");
  const parsedPersonId = personId ? Number(personId) : null;
  const historyQuery = usePersonWorkflowHistory(parsedPersonId);
  const history = historyQuery.data ?? null;
  const displayName = history?.displayName ?? "Mitarbeiter";
  const createUrl = history ? `/create?targetPersonId=${history.personId}` : "/create";

  const activeWorkflows = useMemo(
    () => (history?.workflows ?? []).filter(isWorkflowActive),
    [history?.workflows]
  );
  const aggregates = usePersonWorkflowAggregates(history?.workflows);
  const isRetroactivelyImported = useMemo(
    () =>
      history !== null &&
      history.latestCompletedOnboardingWorkflowUid === null &&
      !history.workflows.some((w) => w.processType.key === "onboarding"),
    [history]
  );

  return (
    <main className="app-shell">
      <div className="page-container">
        <nav className="breadcrumb" aria-label="Breadcrumb">
          {canAccessFeature("peopleDirectory") ? (
            <Link to="/people">Mitarbeiter</Link>
          ) : (
            <Link to="/workflows">Vorgänge suchen</Link>
          )}
          <span className="breadcrumb-separator" aria-hidden="true">
            /
          </span>
          <span>Personenakte</span>
        </nav>

        <PageHeader
          variant="detail"
          eyebrow="Mitarbeiterakte"
          title={displayName}
          description="360°-Sicht auf laufende Vorgänge, offene Aufgaben, Benachrichtigungen und den Verzeichnis-Kontext dieser Person."
          actions={
            history && capabilities.canCreateWorkflow ? (
              <Link to={createUrl} className="btn btn-primary">
                Neuen Vorgang anlegen
              </Link>
            ) : undefined
          }
        />

        {historyQuery.isLoading ? (
          <LoadingState title="Mitarbeiterakte wird geladen..." />
        ) : null}

        {!historyQuery.isLoading && historyQuery.error ? (
          <EmptyState
            title="Akte konnte nicht geladen werden."
            description={
              historyQuery.error instanceof Error
                ? historyQuery.error.message
                : "Mitarbeiterakte konnte nicht geladen werden."
            }
            actionLabel="Erneut versuchen"
            onAction={() => void historyQuery.refetch()}
          />
        ) : null}

        {!historyQuery.isLoading && !historyQuery.error && history ? (
          <div className="content-stack">
            <section className="panel" aria-label="Personen-Status">
              <div
                style={{ display: "flex", flexWrap: "wrap", gap: "0.45rem" }}
              >
                <span className={getEmploymentStatusClass(history.employmentStatus)}>
                  {formatEmploymentStatus(history.employmentStatus)}
                </span>
                <span className={getDirectoryChipClass(history.directoryLinkStatus)}>
                  {formatDirectoryLinkStatus(history.directoryLinkStatus)}
                </span>
                {history.departmentName ? (
                  <span className="status-pill">{history.departmentName}</span>
                ) : null}
                {history.roleName ? (
                  <span className="status-pill">{history.roleName}</span>
                ) : null}
                {isRetroactivelyImported ? (
                  <span className="badge badge--default">Retroaktiv importiert</span>
                ) : null}
              </div>

              <div className="dashboard-metrics" style={{ marginTop: "0.85rem" }}>
                <div className="dashboard-metric dashboard-metric--progress">
                  <span className="dashboard-metric__value">
                    {aggregates.activeWorkflowCount}
                  </span>
                  <span className="dashboard-metric__label">Aktive Vorgänge</span>
                </div>
                <div className="dashboard-metric dashboard-metric--attention">
                  <span className="dashboard-metric__value">
                    {aggregates.openTasks.length}
                  </span>
                  <span className="dashboard-metric__label">Offene Aufgaben</span>
                </div>
                <div className="dashboard-metric dashboard-metric--neutral">
                  <span className="dashboard-metric__value">
                    {aggregates.pendingNotifications.length}
                  </span>
                  <span className="dashboard-metric__label">Benachrichtigungen ausstehend</span>
                </div>
                <div
                  className={`dashboard-metric ${
                    aggregates.failedNotifications.length > 0
                      ? "dashboard-metric--attention"
                      : "dashboard-metric--success"
                  }`}
                >
                  <span className="dashboard-metric__value">
                    {aggregates.failedNotifications.length}
                  </span>
                  <span className="dashboard-metric__label">
                    Benachrichtigungen fehlgeschlagen
                  </span>
                </div>
              </div>
            </section>

            <div
              className="admin-tab-strip"
              role="tablist"
              aria-label="Personenakte-Bereiche"
            >
              <button
                type="button"
                role="tab"
                aria-selected={activeTab === "overview"}
                className={`admin-tab ${activeTab === "overview" ? "active" : ""}`}
                onClick={() => setActiveTab("overview")}
              >
                Übersicht
              </button>
              <button
                type="button"
                role="tab"
                aria-selected={activeTab === "tasks"}
                className={`admin-tab ${activeTab === "tasks" ? "active" : ""}`}
                onClick={() => setActiveTab("tasks")}
              >
                Offene Aufgaben
                <span className="admin-tab-count">{aggregates.openTasks.length}</span>
              </button>
              <button
                type="button"
                role="tab"
                aria-selected={activeTab === "notifications"}
                className={`admin-tab ${activeTab === "notifications" ? "active" : ""}`}
                onClick={() => setActiveTab("notifications")}
              >
                Benachrichtigungen
                <span className="admin-tab-count">
                  {aggregates.pendingNotifications.length +
                    aggregates.failedNotifications.length}
                </span>
              </button>
              <button
                type="button"
                role="tab"
                aria-selected={activeTab === "workflows"}
                className={`admin-tab ${activeTab === "workflows" ? "active" : ""}`}
                onClick={() => setActiveTab("workflows")}
              >
                Vorgänge
                <span className="admin-tab-count">{history.workflows.length}</span>
              </button>
            </div>

            {activeTab === "overview" ? (
              <PersonOverviewSection
                history={history}
                activeWorkflows={activeWorkflows}
                setActiveTab={setActiveTab}
                personId={history.personId}
                canEdit={capabilities.hasAdminRole}
              />
            ) : null}

            {activeTab === "tasks" ? (
              <PersonOpenTasksSection
                tasks={aggregates.openTasks}
                isLoading={aggregates.isLoading}
                errorCount={aggregates.errorCount}
                totalActiveQueries={aggregates.totalActiveQueries}
              />
            ) : null}

            {activeTab === "notifications" ? (
              <PersonNotificationsSection
                pending={aggregates.pendingNotifications}
                failed={aggregates.failedNotifications}
                isLoading={aggregates.isLoading}
                errorCount={aggregates.errorCount}
                totalActiveQueries={aggregates.totalActiveQueries}
              />
            ) : null}

            {activeTab === "workflows" ? (
              <PersonWorkflowsListSection
                history={history}
                displayName={displayName}
                isRetroactivelyImported={isRetroactivelyImported}
              />
            ) : null}
          </div>
        ) : null}
      </div>
    </main>
  );
}
