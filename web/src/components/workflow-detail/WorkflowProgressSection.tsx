import type { ProcessStep } from "./workflowDetailModel";
import { toProcessStepClass } from "./workflowDetailModel";

type WorkflowProgressSectionProps = {
  processSteps: ProcessStep[];
  processTypeName: string;
};

export default function WorkflowProgressSection({ processSteps, processTypeName }: WorkflowProgressSectionProps) {
  return (
    <section className="panel panel-muted">
      <div className="panel-head">
        <h2>Prozessfortschritt</h2>
        <p>So weit ist der {processTypeName}-Vorgang aktuell.</p>
      </div>

      <ol className="process-step-list" aria-label={`Fortschritt ${processTypeName}`}>
        {processSteps.map((step) => (
          <li key={step.key} className="process-step-item">
            <span className={`process-step-state ${toProcessStepClass(step.state)}`} aria-hidden="true" />
            <div className="process-step-content">
              <p className="process-step-title">{step.title}</p>
              <p className="process-step-detail">{step.detail}</p>
            </div>
          </li>
        ))}
      </ol>
    </section>
  );
}
