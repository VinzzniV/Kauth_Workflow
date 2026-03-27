import { Link } from "react-router-dom";
import type { WorkflowSummary } from "../../types/workflow";
import {
  getWorkflowRuntimeStatusLabel,
  getWorkflowRuntimeStatusPillClass,
} from "../../utils/workflowStatus";
import { formatDate, formatDateTime } from "../../utils/dateFormat";

type Props = {
  workflow: WorkflowSummary;
};

export default function WorkflowCard({ workflow }: Props) {
  const fullName = `${workflow.firstName} ${workflow.lastName}`.trim();

  return (
    <article className="workflow-card">
      <div className="workflow-card-top">
        <div>
          <h3>{fullName || "Unbekannter Name"}</h3>
          <div className="chips-row" aria-label="Prozesstyp">
            <span className="chip">{workflow.processType.name}</span>
          </div>
        </div>
        <div className="stacked-status">
          <span className={`status-pill ${getWorkflowRuntimeStatusPillClass(workflow.workflowStatus)}`}>
            {getWorkflowRuntimeStatusLabel(workflow.workflowStatus)}
          </span>
        </div>
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
          <dt>Workflow-ID</dt>
          <dd className="uid-value">{workflow.uid}</dd>
        </div>
        <div>
          <dt>Hinweise</dt>
          <dd>Offen: {workflow.pendingNotifications} / Fehler: {workflow.failedNotifications}</dd>
        </div>
        <div>
          <dt>Erstellt</dt>
          <dd>{formatDateTime(workflow.createdAt)}</dd>
        </div>
        <div>
          <dt>Deadline</dt>
          <dd>{formatDate(workflow.deadlineDate)}</dd>
        </div>
      </dl>

      <Link className="btn btn-secondary" to={`/workflows/${workflow.uid}`}>
        Öffnen
      </Link>
    </article>
  );
}
