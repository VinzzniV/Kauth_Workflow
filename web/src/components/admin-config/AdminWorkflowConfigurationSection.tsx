import type { WorkflowConfig } from "../../types/workflow";

type AdminWorkflowConfigurationSectionProps = {
  workflowConfig: WorkflowConfig | null;
  isLoading: boolean;
};

export function AdminWorkflowConfigurationSection({
  workflowConfig,
  isLoading,
}: AdminWorkflowConfigurationSectionProps) {
  const requirementCount = workflowConfig?.requirements.length ?? 0;
  const requiredRequirementCount = workflowConfig?.requirements.filter((requirement) => requirement.isRequired).length ?? 0;
  const defaultRequirementCount = workflowConfig?.roleRecommendations.recommendedRequirementIds.length ?? 0;
  const requirementPreview = workflowConfig?.requirements.slice(0, 8) ?? [];

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Workflow-Konfiguration</h2>
      </div>

      {isLoading ? (
        <p className="panel-note">Workflow-Konfiguration wird geladen...</p>
      ) : null}

      {!isLoading && !workflowConfig ? (
        <p className="panel-note">Workflow-Konfiguration konnte nicht geladen werden.</p>
      ) : null}

      {!isLoading && workflowConfig ? (
        <>
          <div className="dashboard-grid" aria-label="Workflow-Konfiguration">
            <article className="dashboard-stat-card card-stat">
              <div>
                <h2>Anforderungen</h2>
                <p>{requirementCount}</p>
              </div>
              <p className="panel-note">Davon Pflichtfelder: {requiredRequirementCount}</p>
            </article>

            <article className="dashboard-stat-card card-stat">
              <div>
                <h2>Defaults</h2>
                <p>{defaultRequirementCount}</p>
              </div>
            </article>

            <article className="dashboard-stat-card card-stat">
              <div>
                <h2>Status</h2>
                <p>Nur Ansicht</p>
              </div>
              <p className="panel-note">Keine Bearbeitung in diesem Bereich.</p>
            </article>
          </div>

          <div className="dashboard-card card-primary">
            <div>
              <h2>Aktive Anforderungen</h2>
              <p>Auszug der geladenen Anforderungen.</p>
            </div>

            {requirementPreview.length === 0 ? (
              <p className="panel-note">Keine Anforderungen gefunden.</p>
            ) : (
              <div className="task-list">
                {requirementPreview.map((requirement) => (
                  <article key={requirement.id} className="task-card">
                    <div className="task-card-top">
                      <div>
                        <h3>{requirement.title}</h3>
                        <p className="panel-text">{requirement.description}</p>
                      </div>
                      <span className="chip">{requirement.inputType}</span>
                    </div>

                    <p className="panel-note">
                      {requirement.category} | {requirement.isRequired ? "Pflicht" : "Optional"} | {requirement.options.length} Optionen
                    </p>
                  </article>
                ))}
              </div>
            )}

            {workflowConfig.requirements.length > requirementPreview.length ? (
              <p className="panel-note">
                Weitere Anforderungen vorhanden: {workflowConfig.requirements.length - requirementPreview.length}
              </p>
            ) : null}
          </div>
        </>
      ) : null}
    </section>
  );
}
