import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { getRelatedWorkflows } from "../../services/workflowApi";
import type { RelatedWorkflowSummary } from "../../types/workflow";
import { formatDateTime } from "../../utils/dateFormat";
import {
  getWorkflowRuntimeStatusLabel,
  getWorkflowRuntimeStatusPillClass,
} from "../../utils/workflowStatus";

interface WorkflowLinksPanelProps {
  uid: string;
}

export default function WorkflowLinksPanel({ uid }: WorkflowLinksPanelProps) {
  const [relatedWorkflows, setRelatedWorkflows] = useState<RelatedWorkflowSummary[] | null>(null);

  useEffect(() => {
    let isActive = true;

    void getRelatedWorkflows(uid)
      .then((workflows) => {
        if (isActive) {
          setRelatedWorkflows(workflows);
        }
      })
      .catch(() => {
        if (isActive) {
          setRelatedWorkflows([]);
        }
      });

    return () => {
      isActive = false;
    };
  }, [uid]);

  if (!relatedWorkflows || relatedWorkflows.length === 0) {
    return null;
  }

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Verknüpfte Vorgänge</h2>
      </div>

      <div className="workflow-grid" aria-label="Verknüpfte Vorgänge">
        {relatedWorkflows.map((workflow) => (
          <article key={workflow.uid} className="workflow-card workflow-card--related card-list">
            <div className="workflow-card-top">
              <h3>{workflow.processType.name}</h3>
              <span className={`status-pill ${getWorkflowRuntimeStatusPillClass(workflow.workflowStatus)}`}>
                {getWorkflowRuntimeStatusLabel(workflow.workflowStatus)}
              </span>
            </div>

            <dl className="workflow-meta">
              <div>
                <dt>Angelegt</dt>
                <dd>{formatDateTime(workflow.createdAt)}</dd>
              </div>
            </dl>

            <div className="action-row">
              <Link className="btn btn-secondary" to={`/workflows/${workflow.uid}`}>
                Öffnen
              </Link>
            </div>
          </article>
        ))}
      </div>
    </section>
  );
}
