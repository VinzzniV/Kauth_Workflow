// Zeigt alle Vorgänge einer Person in chronologischer Reihenfolge (Mitarbeiter-Lifecycle-Ansicht).
import { useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useCurrentUser } from "../auth/useCurrentUser";
import EmptyState from "../components/feedback/EmptyState";
import LoadingState from "../components/feedback/LoadingState";
import PageHeader from "../components/layout/PageHeader";
import ViewModeToggle, { type ViewMode } from "../components/layout/ViewModeToggle";
import { usePersonWorkflowHistory } from "../services/queries/peopleQueries";
import type { PersonWorkflowSummary } from "../types/workflow";
import { formatDate, formatDateTime } from "../utils/dateFormat";
import {
  getWorkflowRuntimeStatusLabel,
  getWorkflowRuntimeStatusPillClass,
} from "../utils/workflowStatus";

function formatEmploymentStatus(status: string | null): string {
  switch (status) {
    case "planned":
      return "Geplant";
    case "active":
      return "Aktiv";
    case "inactive":
      return "Inaktiv";
    case "exited":
      return "Ausgetreten";
    default:
      return "-";
  }
}

function formatDirectoryLinkStatus(status: string | null): string {
  switch (status) {
    case "linked":
      return "Mit Verzeichnis verknüpft";
    case "user_only":
      return "Nur App-Benutzer verknüpft";
    case "unlinked":
      return "Noch nicht verknüpft";
    default:
      return "-";
  }
}

type HistorySortKey = "created" | "completed" | "type" | "status" | "department";
type SortDirection = "asc" | "desc";

function compareText(left: string, right: string): number {
  return left.localeCompare(right, "de", { sensitivity: "base" });
}

function compareNullableDate(left: string | null, right: string | null): number {
  const leftTime = left ? new Date(left).getTime() : Number.MAX_SAFE_INTEGER;
  const rightTime = right ? new Date(right).getTime() : Number.MAX_SAFE_INTEGER;
  return leftTime - rightTime;
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

export default function PersonWorkflowHistoryPage() {
  const { personId } = useParams<{ personId: string }>();
  const { capabilities } = useCurrentUser();
  const [viewMode, setViewMode] = useState<ViewMode>("cards");
  const [sortKey, setSortKey] = useState<HistorySortKey>("created");
  const [sortDirection, setSortDirection] = useState<SortDirection>("desc");
  const parsedPersonId = personId ? Number(personId) : null;
  const historyQuery = usePersonWorkflowHistory(parsedPersonId);
  const history = historyQuery.data ?? null;
  const displayName = history?.displayName ?? "Mitarbeiter";
  const createUrl = history
    ? `/create?targetPersonId=${history.personId}`
    : "/create";
  const sortedWorkflows = useMemo(() => {
    const next = [...(history?.workflows ?? [])].sort((left, right) => {
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
  }, [history?.workflows, sortDirection, sortKey]);
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
    <main className="app-shell">
      <div className="page-container">
        <nav className="breadcrumb" aria-label="Breadcrumb">
          <Link to="/search">Vorgänge suchen</Link>
          <span className="breadcrumb-separator" aria-hidden="true">/</span>
          <span>Personenverlauf</span>
        </nav>

        <PageHeader
          variant="detail"
          eyebrow="Mitarbeiterakte"
          title={displayName}
          description="Bisherige Vorgänge dieser Person in chronologischer Reihenfolge."
          actions={
            history ? (
              <a href={createUrl} className="btn btn-primary">
                Neuen Vorgang anlegen
              </a>
            ) : undefined
          }
        />

        {historyQuery.isLoading ? <LoadingState title="Mitarbeiterakte wird geladen..." /> : null}

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
          <>
            <section className="panel">
              <div className="panel-head">
                <h2>Person</h2>
              </div>
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
                  <dt>Letztes abgeschlossenes Onboarding</dt>
                  <dd>{history.latestCompletedOnboardingWorkflowUid ?? "-"}</dd>
                </div>
              </dl>

              {capabilities.canCreateWorkflow ? (
                <div className="action-row">
                  <Link className="btn btn-primary" to={createUrl}>
                    Neuen Prozess starten
                  </Link>
                </div>
              ) : null}
            </section>

            <section className="panel">
              <div className="panel-head">
                <h2>Vorgänge ({history.workflows.length})</h2>
                <p>Vollständige Prozesshistorie dieser Person inkl. archivierter Vorgänge.</p>
              </div>

              {history.workflows.length === 0 ? (
                <p className="panel-note">Noch keine Vorgänge für diese Person angelegt.</p>
              ) : (
                <>
                  <div className="list-view-toolbar">
                    <ViewModeToggle value={viewMode} onChange={setViewMode} />
                  </div>
                  {viewMode === "table" ? (
                    <>
                      <div className="operational-table-wrap">
                        <table className="operational-table" aria-label="Tabellenansicht Vorgänge dieser Person">
                          <thead>
                            <tr>
                              <th scope="col">Vorgang</th>
                              <th scope="col" aria-sort={getSortValue("type")}>
                                <button type="button" className="operational-table-sort" onClick={() => updateSort("type")}>
                                  Prozesstyp
                                </button>
                              </th>
                              <th scope="col" aria-sort={getSortValue("status")}>
                                <button type="button" className="operational-table-sort" onClick={() => updateSort("status")}>
                                  Status
                                </button>
                              </th>
                              <th scope="col" aria-sort={getSortValue("department")}>
                                <button type="button" className="operational-table-sort" onClick={() => updateSort("department")}>
                                  Abteilung
                                </button>
                              </th>
                              <th scope="col">Stelle</th>
                              <th scope="col" aria-sort={getSortValue("created")}>
                                <button type="button" className="operational-table-sort" onClick={() => updateSort("created")}>
                                  Erstellt
                                </button>
                              </th>
                              <th scope="col" aria-sort={getSortValue("completed")}>
                                <button type="button" className="operational-table-sort" onClick={() => updateSort("completed")}>
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
                                      {`${workflow.firstName} ${workflow.lastName}`.trim() || displayName}
                                    </span>
                                    <span className="operational-cell-meta">{workflow.uid}</span>
                                  </div>
                                </td>
                                <td>{workflow.processType.name}</td>
                                <td>
                                  <span className={`status-pill ${getWorkflowRuntimeStatusPillClass(workflow.workflowStatus)}`}>
                                    {getWorkflowRuntimeStatusLabel(workflow.workflowStatus)}
                                  </span>
                                </td>
                                <td>{workflow.departmentName}</td>
                                <td>{workflow.roleName}</td>
                                <td className="operational-cell-number">{formatDateTime(workflow.createdAt)}</td>
                                <td className="operational-cell-number">{workflow.completedAt ? formatDate(workflow.completedAt) : "-"}</td>
                                <td>
                                  <div className="operational-table-actions">
                                    <Link className="btn btn-secondary" to={`/workflows/${workflow.uid}`}>
                                      Öffnen
                                    </Link>
                                  </div>
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                      <div className="workflow-grid operational-card-fallback" aria-label="Vorgänge dieser Person">
                        {sortedWorkflows.map((workflow) => (
                          <WorkflowHistoryCard key={workflow.uid} workflow={workflow} displayName={displayName} />
                        ))}
                      </div>
                    </>
                  ) : (
                    <div className="workflow-grid" aria-label="Vorgänge dieser Person">
                      {sortedWorkflows.map((workflow) => (
                        <WorkflowHistoryCard key={workflow.uid} workflow={workflow} displayName={displayName} />
                      ))}
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
