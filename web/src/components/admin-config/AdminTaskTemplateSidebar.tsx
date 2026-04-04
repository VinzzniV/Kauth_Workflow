import type { AdminProcessType, AdminTaskTemplate } from "../../types/auth";
import SectionHeader from "../ui/SectionHeader";
import SelectionListItem from "../ui/SelectionListItem";

type AdminTaskTemplateSidebarProps = {
  processTypes: AdminProcessType[];
  selectedProcessTypeId: number | null;
  templates: AdminTaskTemplate[];
  selectedTemplateId: number | null;
  isCreatingNew: boolean;
  isLoadingProcessTypes: boolean;
  isLoadingTemplates: boolean;
  isSaving: boolean;
  isDeleting: boolean;
  onSelectProcessType: (value: string) => void;
  onStartCreatingTemplate: () => void;
  onSelectTemplate: (template: AdminTaskTemplate) => void;
};

export function AdminTaskTemplateSidebar({
  processTypes,
  selectedProcessTypeId,
  templates,
  selectedTemplateId,
  isCreatingNew,
  isLoadingProcessTypes,
  isLoadingTemplates,
  isSaving,
  isDeleting,
  onSelectProcessType,
  onStartCreatingTemplate,
  onSelectTemplate,
}: AdminTaskTemplateSidebarProps) {
  return (
    <section className="panel admin-detail-sidebar master-detail-sidebar">
      <SectionHeader title="Aufgabenvorlagen" />

      <div className="toolbar-row admin-detail-toolbar">
        <label className="field admin-detail-process-field">
          <span>Prozesstyp</span>
          <select
            value={selectedProcessTypeId ?? ""}
            onChange={(event) => onSelectProcessType(event.target.value)}
            disabled={isLoadingProcessTypes || isSaving || isDeleting}
          >
            <option value="">Prozesstyp wählen</option>
            {processTypes.map((processType) => (
              <option key={processType.id} value={processType.id}>
                {processType.name}
              </option>
            ))}
          </select>
        </label>

        <button
          type="button"
          className="btn btn-primary"
          disabled={!selectedProcessTypeId || isLoadingTemplates || isSaving || isDeleting}
          onClick={onStartCreatingTemplate}
        >
          Neue Aufgabenvorlage
        </button>
      </div>

      {!selectedProcessTypeId ? <p className="panel-note">Bitte zuerst einen Prozesstyp auswählen.</p> : null}
      {isLoadingTemplates ? <p className="panel-note">Aufgabenvorlagen werden geladen...</p> : null}

      {!isLoadingTemplates && selectedProcessTypeId && templates.length === 0 ? (
        <p className="panel-note">Für diesen Prozesstyp sind noch keine Aufgabenvorlagen vorhanden.</p>
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
                meta={`${template.category} | Key ${template.templateKey}`}
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
