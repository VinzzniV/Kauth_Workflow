import { Link } from "react-router-dom";
import type { WorkflowSummary } from "../../types/workflow";
import {
  getWorkflowLegacyStatusLabel,
  getWorkflowLegacyStatusPillClass,
} from "../../utils/workflowStatus";

type Props = {
  workflow: WorkflowSummary;
};

function formatDate(value: string): string {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "-";
  }
  return date.toLocaleString("de-DE");
}

export default function WorkflowCard({ workflow }: Props) {
  const fullName = `${workflow.firstName} ${workflow.lastName}`.trim();

  return (
    <article className="workflow-card">
      <div className="workflow-card-top">
        <h3>{fullName || "Unbekannter Name"}</h3>
        <span className={`status-pill ${getWorkflowLegacyStatusPillClass(workflow.workflowStatus)}`}>
          {getWorkflowLegacyStatusLabel(workflow.workflowStatus)}
        </span>
      </div>

      <dl className="workflow-meta">
        <div>
          <dt>Personalnummer</dt>
          <dd>{workflow.employeeNumber}</dd>
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
          <dt>Onboarding-ID</dt>
          <dd className="uid-value">{workflow.uid}</dd>
        </div>
        <div>
          <dt>Hinweise</dt>
          <dd>Offen: {workflow.pendingNotifications} / Fehler: {workflow.failedNotifications}</dd>
        </div>
        <div>
          <dt>Erstellt</dt>
          <dd>{formatDate(workflow.createdAt)}</dd>
        </div>
      </dl>

      <Link className="btn btn-secondary" to={`/workflows/${workflow.uid}`}>
        Öffnen
      </Link>
    </article>
  );
}
