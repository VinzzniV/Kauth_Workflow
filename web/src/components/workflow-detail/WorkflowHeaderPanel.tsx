import { Link } from "react-router-dom";
import type { WorkflowDetail } from "../../types/workflow";
import { formatDate, toRuntimeStatusLabel } from "./workflowDetailModel";

type WorkflowHeaderPanelProps = {
  workflow: WorkflowDetail;
  regularEditingText: string;
  nextActionText: string;
  currentArea: string;
  currentOwnerText: string;
  canManageAdminConfiguration: boolean;
};

export default function WorkflowHeaderPanel({
  workflow,
  regularEditingText,
  nextActionText,
  currentArea,
  currentOwnerText,
  canManageAdminConfiguration,
}: WorkflowHeaderPanelProps) {
  return (
    <section className="panel panel-intro">
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
            <span className="chip" aria-label={`Prozesstyp: ${workflow.processType.name}`}>{workflow.processType.name}</span>
            <span className="chip" aria-label={`Workflow-Status: ${toRuntimeStatusLabel(workflow.workflowStatus)}`}>Status: {toRuntimeStatusLabel(workflow.workflowStatus)}</span>
            <span className="chip" aria-label={`Aktuelle Phase: ${regularEditingText}`}>Phase: {regularEditingText}</span>
          </div>

          <div className="next-action-callout" role="status" aria-live="polite">
            <p className="next-action-label">Nächste nötige Aktion</p>
            <p className="next-action-text">{nextActionText}</p>
          </div>

          <p className="panel-note">
            Sichtbarkeit und Aktionen werden im Backend je Rolle geprüft.
            {canManageAdminConfiguration ? " Admin kann bei Bedarf eingreifen." : ""}
          </p>
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
            <span>Offene Aufgaben</span>
            <strong>{workflow.taskMetrics.overall.activeCount}</strong>
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
          <p className="workflow-detail-kpi-label">Prozesstyp</p>
          <p className="workflow-detail-kpi-value">{workflow.processType.name}</p>
          <p className="workflow-detail-kpi-note">Zu welchem Mitarbeiterprozess dieser Vorgang gehört.</p>
        </article>

        <article className="workflow-detail-kpi">
          <p className="workflow-detail-kpi-label">Abteilung</p>
          <p className="workflow-detail-kpi-value">{workflow.departmentName}</p>
          <p className="workflow-detail-kpi-note">Zugehörige Abteilung für diesen Vorgang.</p>
        </article>

        <article className="workflow-detail-kpi">
          <p className="workflow-detail-kpi-label">Rolle / Position</p>
          <p className="workflow-detail-kpi-value">{workflow.roleName}</p>
          <p className="workflow-detail-kpi-note">Hinterlegte Zielposition für diesen Vorgang.</p>
        </article>

        <article className="workflow-detail-kpi">
          <p className="workflow-detail-kpi-label">Angelegt am</p>
          <p className="workflow-detail-kpi-value">{formatDate(workflow.createdAt)}</p>
          <p className="workflow-detail-kpi-note">Vorgang wurde im System angelegt.</p>
        </article>

        <article className="workflow-detail-kpi">
          <p className="workflow-detail-kpi-label">Deadline</p>
          <p className="workflow-detail-kpi-value">{formatDate(workflow.deadlineDate)}</p>
          <p className="workflow-detail-kpi-note">Optionales Ziel-Datum für den gesamten Vorgang.</p>
        </article>

        <article className="workflow-detail-kpi">
          <p className="workflow-detail-kpi-label">Aufgabenstatus</p>
          <p className="workflow-detail-kpi-value">{workflow.taskMetrics.overall.activeCount} offen</p>
          <p className="workflow-detail-kpi-note">
            Offen: {workflow.taskMetrics.overall.openCount} | In Bearbeitung: {workflow.taskMetrics.overall.inProgressCount} | Erledigt: {workflow.taskMetrics.overall.completedCount}
          </p>
        </article>
      </div>
    </section>
  );
}
