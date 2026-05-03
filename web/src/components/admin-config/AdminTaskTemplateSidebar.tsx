import type { AdminTaskSpec, AdminWorkflowDefinitionSummary } from "../../types/auth";
import SectionHeader from "../ui/SectionHeader";
import SelectionListItem from "../ui/SelectionListItem";

type AdminTaskTemplateSidebarProps = {
  workflowDefinitions: AdminWorkflowDefinitionSummary[];
  selectedWorkflowDefinitionId: number | null;
  templates: AdminTaskSpec[];
  selectedTemplateId: number | null;
  isCreatingNew: boolean;
  isLoadingProcessTypes: boolean;
  isLoadingTemplates: boolean;
  isSaving: boolean;
  isDeleting: boolean;
  onSelectWorkflowDefinition: (value: string) => void;
  onStartCreatingTemplate: () => void;
  onSelectTemplate: (template: AdminTaskSpec) => void;
};

export function AdminTaskTemplateSidebar({
  workflowDefinitions,
  selectedWorkflowDefinitionId,
  templates,
  selectedTemplateId,
  isCreatingNew,
  isLoadingProcessTypes,
  isLoadingTemplates,
  isSaving,
  isDeleting,
  onSelectWorkflowDefinition,
  onStartCreatingTemplate,
  onSelectTemplate,
}: AdminTaskTemplateSidebarProps) {
  return (
    <section className="panel admin-detail-sidebar master-detail-sidebar">
      <SectionHeader title="Aufgabenvorlagen" />

      <div className="toolbar-row admin-detail-toolbar">
        <label className="field admin-detail-process-field">
          <span>Workflow-Definition</span>
          <select
            value={selectedWorkflowDefinitionId ?? ""}
            onChange={(event) => onSelectWorkflowDefinition(event.target.value)}
            disabled={isLoadingProcessTypes || isSaving || isDeleting}
          >
            <option value="">Workflow-Definition wählen</option>
            {workflowDefinitions.map((definition) => (
              <option key={definition.id} value={definition.id}>
                {definition.name}
              </option>
            ))}
          </select>
        </label>

        <button
          type="button"
          className="btn btn-primary"
          disabled={!selectedWorkflowDefinitionId || isLoadingTemplates || isSaving || isDeleting}
          onClick={onStartCreatingTemplate}
        >
          Neue Aufgabenvorlage
        </button>
      </div>

      {!selectedWorkflowDefinitionId ? <p className="panel-note">Bitte zuerst eine Workflow-Definition auswählen.</p> : null}
      {isLoadingTemplates ? <p className="panel-note">Aufgabenvorlagen werden geladen...</p> : null}

      {!isLoadingTemplates && selectedWorkflowDefinitionId && templates.length === 0 ? (
        <p className="panel-note">Für diese Workflow-Definition sind noch keine Aufgabenvorlagen vorhanden.</p>
      ) : null}

      {!isLoadingTemplates && templates.length > 0 ? (
        <div className="selection-list" aria-label="Aufgabenvorlagen">
          {templates.map((template) => {
            const isSelected = !isCreatingNew && selectedTemplateId === template.id;

            return (
              <SelectionListItem
                key={template.id}
                active={isSelected}
                title={template.title}
                meta={`${template.category} | Key ${template.specKey}`}
                secondaryMeta={`Sortierung ${template.sortOrder} | Bedingungen ${template.conditionCount} | Abhängigkeiten ${template.dependencyCount}`}
                onClick={() => onSelectTemplate(template)}
              />
            );
          })}
        </div>
      ) : null}
    </section>
  );
}
