import { Link } from "react-router-dom";
import type { WorkflowSummary } from "../../types/workflow";
import {
  getWorkflowRuntimeStatusLabel,
  getWorkflowRuntimeStatusPillClass,
} from "../../utils/workflowStatus";
import { formatDate } from "../../utils/dateFormat";
import Card from "../ui/Card";

type Props = {
  workflow: WorkflowSummary;
};

export default function WorkflowCard({ workflow }: Props) {
  const fullName = `${workflow.firstName} ${workflow.lastName}`.trim();

  return (
    <Card variant="list" className="workflow-card workflow-card--summary">
      <div className="workflow-card-top">
        <div>
          <h3>{fullName || "Unbekannter Name"}</h3>
          <div className="chips-row" aria-label="Prozesstyp">
            <span className="chip">{workflow.workflowDefinition.name}</span>
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
        <div>
          <dt>Fortschritt</dt>
          <dd>{workflow.taskMetrics.overall.doneCount}/{workflow.taskMetrics.overall.totalCount}</dd>
        </div>
        <div>
          <dt>Pflicht</dt>
          <dd>{workflow.taskMetrics.required.doneCount}/{workflow.taskMetrics.required.totalCount}</dd>
        </div>
      </dl>

      <Link className="btn btn-secondary" to={`/workflows/${workflow.uid}`}>
        Öffnen
      </Link>
    </Card>
  );
}
