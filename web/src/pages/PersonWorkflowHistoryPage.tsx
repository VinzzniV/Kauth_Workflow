// Zeigt alle Vorgänge einer Person in chronologischer Reihenfolge (Mitarbeiter-Lifecycle-Ansicht).
import { useCallback, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useCurrentUser } from "../auth/useCurrentUser";
import EmptyState from "../components/feedback/EmptyState";
import LoadingState from "../components/feedback/LoadingState";
import PageHeader from "../components/layout/PageHeader";
import { getPersonWorkflowHistory } from "../services/peopleApi";
import type { PersonWorkflowHistory } from "../types/workflow";
import { formatDate, formatDateTime } from "../utils/dateFormat";
import {
  getWorkflowRuntimeStatusLabel,
  getWorkflowRuntimeStatusPillClass,
} from "../utils/workflowStatus";

export default function PersonWorkflowHistoryPage() {
  const { personId } = useParams<{ personId: string }>();
  const { capabilities } = useCurrentUser();
  const [history, setHistory] = useState<PersonWorkflowHistory | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!personId) {
      return;
    }

    setIsLoading(true);
    setError(null);

    try {
      const data = await getPersonWorkflowHistory(Number(personId));
      setHistory(data);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Mitarbeiterakte konnte nicht geladen werden.";
      setError(message);
    } finally {
      setIsLoading(false);
    }
  }, [personId]);

  useEffect(() => {
    void load();
  }, [load]);

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

        {isLoading ? <LoadingState title="Mitarbeiterakte wird geladen..." /> : null}

        {!isLoading && error ? (
          <EmptyState
            title="Akte konnte nicht geladen werden."
            description={error}
            actionLabel="Erneut versuchen"
            onAction={load}
          />
        ) : null}

        {!isLoading && !error && history ? (
          <>
            <section className="panel">
              <div className="panel-head">
                <h2>Person</h2>
              </div>
              <dl className="workflow-meta">
                {history.departmentName ? (
                  <div>
                    <dt>Abteilung</dt>
                    <dd>{history.departmentName}</dd>
                  </div>
                ) : null}
                {history.employeeNumber ? (
                  <div>
                    <dt>Personalnummer</dt>
                    <dd>{history.employeeNumber}</dd>
                  </div>
                ) : null}
                {history.badgeNumber ? (
                  <div>
                    <dt>Ausweisnummer</dt>
                    <dd>{history.badgeNumber}</dd>
                  </div>
                ) : null}
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
