import { Link } from "react-router-dom";
import EmptyState from "../components/feedback/EmptyState";
import SkeletonCard from "../components/feedback/SkeletonCard";
import type { WorkflowSummary } from "../types/workflow";
import { formatDate } from "../utils/dateFormat";
import { getWorkflowRuntimeStatusLabel, getWorkflowRuntimeStatusPillClass } from "../utils/workflowStatus";

type WorkflowListResultsProps = {
  rows: WorkflowSummary[];
  isLoading: boolean;
  error: string | null;
  hasActiveFilters: boolean;
  onRetry: () => void;
};

export function WorkflowListResults({
  rows,
  isLoading,
  error,
  hasActiveFilters,
  onRetry,
}: WorkflowListResultsProps) {
  if (isLoading) {
    return (
      <section className="workflow-grid" aria-label="Vorgänge werden geladen">
        {Array.from({ length: 8 }, (_, index) => (
          <SkeletonCard key={`workflow-skeleton-${index}`} variant="workflow" />
        ))}
      </section>
    );
  }

  if (error) {
    return (
      <EmptyState
        title="Vorgänge konnten nicht geladen werden."
        description={error}
        actionLabel="Erneut versuchen"
        onAction={onRetry}
      />
    );
  }

  if (rows.length === 0 && !hasActiveFilters) {
    return (
      <EmptyState
        title="Keine laufenden Vorgänge vorhanden"
        description="Aktuell sind keine laufenden Vorgänge vorhanden."
      />
    );
  }

  if (rows.length === 0 && hasActiveFilters) {
    return (
      <EmptyState
        title="Keine Treffer"
        description="Die aktuelle Filterkombination liefert keine passenden Vorgänge."
      />
    );
  }

  return (
    <section className="workflow-grid" aria-label="Liste Mitarbeiterprozesse">
      {rows.map((workflow) => {
        const workflowDisplayName = `${workflow.firstName} ${workflow.lastName}`.trim();

        return (
          <article key={workflow.uid} className="workflow-card workflow-card-extended card-list">
            <div className="workflow-card-top">
              <div>
                <h3>{workflowDisplayName || "Unbekannter Name"}</h3>
                <div className="chips-row" aria-label="Prozesstyp">
                  <span className="chip">{workflow.processType.name}</span>
                </div>
              </div>
              <span className={`status-pill ${getWorkflowRuntimeStatusPillClass(workflow.workflowStatus)}`}>
                {getWorkflowRuntimeStatusLabel(workflow.workflowStatus)}
              </span>
            </div>

            <dl className="workflow-meta">
              <div>
                <dt>Stelle</dt>
                <dd>{workflow.roleName}</dd>
              </div>
              <div>
                <dt>Abteilung</dt>
                <dd>{workflow.departmentName}</dd>
              </div>
              <div>
                <dt>Personalnummer</dt>
                <dd>{workflow.employeeNumber}</dd>
              </div>
              <div>
                <dt>Deadline</dt>
                <dd>{formatDate(workflow.deadlineDate)}</dd>
              </div>
            </dl>

            <div className="action-row">
              <Link className="btn btn-secondary" to={`/workflows/${workflow.uid}`}>
                Öffnen
              </Link>
            </div>
          </article>
        );
      })}
    </section>
  );
}
