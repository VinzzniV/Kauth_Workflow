import { Link } from "react-router-dom";
import type { WorkflowDetail } from "../../types/workflow";
import { formatDate, toRuntimeStatusLabel } from "./workflowDetailModel";

type WorkflowHeaderPanelProps = {
  workflow: WorkflowDetail;
  regularEditingText: string;
  nextActionText: string;
  currentArea: string;
  currentOwnerText: string;
};

export default function WorkflowHeaderPanel({
  workflow,
  regularEditingText,
  nextActionText,
  currentArea,
  currentOwnerText,
}: WorkflowHeaderPanelProps) {
  return (
    <section className="panel">
      <div className="workflow-detail-hero">
        <div className="workflow-detail-hero-main">
          <div className="panel-head">
            <h2>
              {workflow.firstName} {workflow.lastName}
            </h2>
            <p>
              {workflow.departmentName} | {workflow.roleName}
            </p>
          </div>

          <div className="workflow-detail-chip-row">
            <span className="chip" aria-label={`Prozesstyp: ${workflow.workflowDefinition.name}`}>{workflow.workflowDefinition.name}</span>
            <span className="chip" aria-label={`Workflow-Status: ${toRuntimeStatusLabel(workflow.workflowStatus)}`}>Status: {toRuntimeStatusLabel(workflow.workflowStatus)}</span>
            <span className="chip" aria-label={`Aktuelle Phase: ${regularEditingText}`}>Phase: {regularEditingText}</span>
          </div>

          <div className="next-action-callout">
            <p className="next-action-label">Nächste nötige Aktion</p>
            <p className="next-action-text">{nextActionText}</p>
          </div>

        </div>

        <aside className="workflow-detail-focus-card">
          <p className="workflow-detail-focus-label">Aktuell wichtig</p>
          <div className="workflow-detail-focus-item">
            <span>Status</span>
            <strong>{toRuntimeStatusLabel(workflow.workflowStatus)}</strong>
          </div>
          <div className="workflow-detail-focus-item">
            <span>Aktueller Bereich</span>
            <strong>{currentArea}</strong>
          </div>
          <div className="workflow-detail-focus-item">
            <span>Aktuell dran</span>
            <strong>{currentOwnerText}</strong>
          </div>
          <div className="workflow-detail-focus-item">
            <span>Fortschritt</span>
            <strong>{workflow.taskMetrics.overall.doneCount}/{workflow.taskMetrics.overall.totalCount}</strong>
          </div>
          <div className="workflow-detail-focus-item">
            <span>Pflicht erledigt</span>
            <strong>{workflow.taskMetrics.required.doneCount}/{workflow.taskMetrics.required.totalCount}</strong>
          </div>
          {workflow.targetPersonId != null ? (
            <Link className="btn btn-secondary" to={`/people/${workflow.targetPersonId}`}>
              Mitarbeiterakte öffnen
            </Link>
          ) : null}
        </aside>
      </div>

      <div className="workflow-detail-context-grid">
        <article className="workflow-detail-kpi">
          <p className="workflow-detail-kpi-label">Angelegt am</p>
          <p className="workflow-detail-kpi-value">{formatDate(workflow.createdAt)}</p>
        </article>

        <article className="workflow-detail-kpi">
          <p className="workflow-detail-kpi-label">Deadline</p>
          <p className="workflow-detail-kpi-value">{formatDate(workflow.deadlineDate)}</p>
        </article>
      </div>
    </section>
  );
}
