import type { WorkflowDetail } from "../../types/workflow";
import { getWorkflowRuntimeStatusPillClass } from "../../utils/workflowStatus";
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
      <div className="panel-head">
        <h2>
          {workflow.firstName} {workflow.lastName}
        </h2>
        <p>
          Neue Person in {workflow.departmentName} | {workflow.roleName}
        </p>
      </div>

      <div className="action-row">
        <span className={`status-pill ${getWorkflowRuntimeStatusPillClass(workflow.workflowStatus)}`}>
          Status: {toRuntimeStatusLabel(workflow.workflowStatus)}
        </span>
        <span className="chip">Prozessstand: {toRuntimeStatusLabel(workflow.workflowStatus)}</span>
      </div>

      <p className="panel-note">
        Bearbeitungsphase: {regularEditingText} | Sichtbarkeit und Aktionen werden im Backend je Rolle geprüft.
        {canManageAdminConfiguration ? " | Admin kann bei Bedarf eingreifen." : ""}
      </p>

      <div className="next-action-callout" role="status" aria-live="polite">
        <p className="next-action-label">Nächste nötige Aktion</p>
        <p className="next-action-text">{nextActionText}</p>
      </div>

      <div className="workflow-detail-summary-grid">
        <article className="workflow-detail-kpi">
          <p className="workflow-detail-kpi-label">Abteilung</p>
          <p className="workflow-detail-kpi-value">{workflow.departmentName}</p>
          <p className="workflow-detail-kpi-note">Geplanter Einsatzbereich der neuen Person.</p>
        </article>

        <article className="workflow-detail-kpi">
          <p className="workflow-detail-kpi-label">Rolle / Position</p>
          <p className="workflow-detail-kpi-value">{workflow.roleName}</p>
          <p className="workflow-detail-kpi-note">Hinterlegte Zielposition für das Onboarding.</p>
        </article>

        <article className="workflow-detail-kpi">
          <p className="workflow-detail-kpi-label">Startdatum</p>
          <p className="workflow-detail-kpi-value">{formatDate(workflow.createdAt)}</p>
          <p className="workflow-detail-kpi-note">Onboarding angelegt durch HR.</p>
        </article>

        <article className="workflow-detail-kpi">
          <p className="workflow-detail-kpi-label">Deadline</p>
          <p className="workflow-detail-kpi-value">{formatDate(workflow.deadlineDate)}</p>
          <p className="workflow-detail-kpi-note">Optionales Ziel-Datum für den gesamten Vorgang.</p>
        </article>

        <article className="workflow-detail-kpi">
          <p className="workflow-detail-kpi-label">Aktueller Status</p>
          <p className="workflow-detail-kpi-value">{toRuntimeStatusLabel(workflow.workflowStatus)}</p>
          <p className="workflow-detail-kpi-note">Gesamtstand des Vorgangs.</p>
        </article>

        <article className="workflow-detail-kpi">
          <p className="workflow-detail-kpi-label">Aktuell zuständiger Bereich</p>
          <p className="workflow-detail-kpi-value">{currentArea}</p>
          <p className="workflow-detail-kpi-note">Wer diese Workflow-Phase regulär bearbeitet.</p>
        </article>

        <article className="workflow-detail-kpi">
          <p className="workflow-detail-kpi-label">Aktuell dran</p>
          <p className="workflow-detail-kpi-value">{currentOwnerText}</p>
          <p className="workflow-detail-kpi-note">Konkrete Zuständigkeit für die aktuell offenen Aufgaben.</p>
        </article>

        <article className="workflow-detail-kpi">
          <p className="workflow-detail-kpi-label">Offene Aufgaben</p>
          <p className="workflow-detail-kpi-value">{workflow.taskMetrics.overall.activeCount}</p>
          <p className="workflow-detail-kpi-note">
            Offen: {workflow.taskMetrics.overall.openCount} | In Bearbeitung: {workflow.taskMetrics.overall.inProgressCount} | Erledigt: {workflow.taskMetrics.overall.completedCount}
          </p>
        </article>
      </div>
    </section>
  );
}
