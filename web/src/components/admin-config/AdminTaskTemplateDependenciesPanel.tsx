import type {
  AdminDependencyGraph,
  AdminTaskSpec,
  AdminTaskSpecDependency,
} from "../../types/auth";
import type { DependencyStatus } from "../../hooks/adminTaskTemplateManagementModel";
import { DependencyGraphEditor } from "./DependencyGraphEditor";
import SectionHeader from "../ui/SectionHeader";

type AdminTaskTemplateDependenciesPanelProps = {
  selectedTemplate: AdminTaskSpec;
  templates: AdminTaskSpec[];
  dependencyGraph: AdminDependencyGraph;
  dependencies: AdminTaskSpecDependency[];
  isLoadingDependencyGraph: boolean;
  isLoadingTemplates: boolean;
  isLoadingDependencies: boolean;
  isSavingDependency: boolean;
  deletingDependencyId: number | null;
  onSelectTemplate: (template: AdminTaskSpec) => void;
  onCreateDependencyFromGraph: (
    sourceSpecId: number,
    targetSpecId: number,
    requiredStatus: DependencyStatus
  ) => Promise<void>;
  onRemoveDependencyFromGraph: (dependencyId: number) => Promise<void>;
  onRemoveDependency: (dependencyId: number) => Promise<void>;
};

export function AdminTaskTemplateDependenciesPanel({
  selectedTemplate,
  templates,
  dependencyGraph,
  dependencies,
  isLoadingDependencyGraph,
  isLoadingTemplates,
  isLoadingDependencies,
  isSavingDependency,
  deletingDependencyId,
  onSelectTemplate,
  onCreateDependencyFromGraph,
  onRemoveDependencyFromGraph,
  onRemoveDependency,
}: AdminTaskTemplateDependenciesPanelProps) {
  return (
    <section className="panel">
      <SectionHeader title="Abhängigkeiten" />

      <div className="content-stack">
        <DependencyGraphEditor
          graph={dependencyGraph}
          templates={templates}
          selectedTemplateId={selectedTemplate.id}
          isLoading={isLoadingDependencyGraph || isLoadingTemplates}
          isCreatingDependency={isSavingDependency}
          isDeletingDependency={deletingDependencyId !== null}
          onSelectTemplate={(templateId) => {
            const template = templates.find((entry) => entry.id === templateId);
            if (template) {
              onSelectTemplate(template);
            }
          }}
          onCreateDependency={onCreateDependencyFromGraph}
          onDeleteDependency={onRemoveDependencyFromGraph}
        />

        {isLoadingDependencies ? <p className="panel-note">Abhängigkeiten werden geladen...</p> : null}

        {!isLoadingDependencies && dependencies.length === 0 ? (
          <p className="panel-note">Für diese Aufgabenvorlage sind noch keine Abhängigkeiten definiert.</p>
        ) : null}

        {!isLoadingDependencies && dependencies.length > 0 ? (
          <table className="table">
            <thead>
              <tr>
                <th>Abhängige Aufgabenvorlage</th>
                <th>Required Status</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {dependencies.map((dependency) => (
                <tr key={dependency.id}>
                  <td>{dependency.dependsOnSpecTitle}</td>
                  <td>{dependency.requiredStatus}</td>
                  <td>
                    <button
                      type="button"
                      className="btn btn-sm btn-outline"
                      disabled={deletingDependencyId === dependency.id || isSavingDependency}
                      onClick={() => void onRemoveDependency(dependency.id)}
                    >
                      {deletingDependencyId === dependency.id ? "Löscht..." : "Entfernen"}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : null}
      </div>
    </section>
  );
}
