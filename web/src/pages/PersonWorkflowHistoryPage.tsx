// Zeigt alle Vorgänge einer Person in chronologischer Reihenfolge (Mitarbeiter-Lifecycle-Ansicht).
import { Link, useParams } from "react-router-dom";
import { useCurrentUser } from "../auth/useCurrentUser";
import EmptyState from "../components/feedback/EmptyState";
import LoadingState from "../components/feedback/LoadingState";
import PageHeader from "../components/layout/PageHeader";
import { usePersonWorkflowHistory } from "../services/queries/peopleQueries";
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

export default function PersonWorkflowHistoryPage() {
  const { personId } = useParams<{ personId: string }>();
  const { capabilities } = useCurrentUser();
  const parsedPersonId = personId ? Number(personId) : null;
  const historyQuery = usePersonWorkflowHistory(parsedPersonId);
  const history = historyQuery.data ?? null;
  const displayName = history?.displayName ?? "Mitarbeiter";
  const createUrl = history
    ? `/create?targetPersonId=${history.personId}`
    : "/create";

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
                <div className="workflow-grid" aria-label="Vorgänge dieser Person">
                  {history.workflows.map((workflow) => {
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
                  })}
                </div>
              )}
            </section>
          </>
        ) : null}
      </div>
    </main>
  );
}
