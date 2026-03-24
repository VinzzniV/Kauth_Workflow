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
  const recommendedRequirementCount = workflowConfig?.roleRecommendations.recommendedRequirementIds.length ?? 0;
  const requirementPreview = workflowConfig?.requirements.slice(0, 8) ?? [];

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Workflow-Konfiguration</h2>
        <p>
          Diese Ansicht zeigt den aktuell wirksamen Konfigurationsstand für Anforderungen und Rollenempfehlungen. Aufgabenvorlagen,
          Abhängigkeiten, Generierungsbedingungen und Prozessbereichslogik sind weiterhin code- bzw. SQL-gesteuert und hier nicht editierbar.
        </p>
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
            <article className="dashboard-card">
              <div>
                <h2>Anforderungen</h2>
                <p>{requirementCount}</p>
              </div>
              <p className="panel-note">Davon Pflichtfelder: {requiredRequirementCount}</p>
            </article>

            <article className="dashboard-card">
              <div>
                <h2>Rollenempfehlungen</h2>
                <p>{recommendedRequirementCount}</p>
              </div>
              <p className="panel-note">Empfohlene Anforderungen mit hinterlegten Rollen-Defaults.</p>
            </article>

            <article className="dashboard-card">
              <div>
                <h2>Konfigurierbarkeit</h2>
                <p>Read-only</p>
              </div>
              <p className="panel-note">Transparenz im Admin, aber keine Pflege von Templates und Ablaufregeln.</p>
            </article>
          </div>

          <div className="dashboard-card">
            <div>
              <h2>Aktive Anforderungen</h2>
              <p>Auszug der aktuell geladenen Anforderungen aus der Workflow-Konfiguration.</p>
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
                      Kategorie: {requirement.category} | Pflicht: {requirement.isRequired ? "Ja" : "Nein"} | Optionen: {requirement.options.length}
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
