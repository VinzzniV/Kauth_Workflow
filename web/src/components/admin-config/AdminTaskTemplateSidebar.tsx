import { AlertTriangle } from "lucide-react";
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

      {selectedWorkflowDefinitionId ? (
        <VersionContextBanner
          workflowDefinitions={workflowDefinitions}
          selectedWorkflowDefinitionId={selectedWorkflowDefinitionId}
        />
      ) : (
        <p className="panel-note">Bitte zuerst eine Workflow-Definition auswählen.</p>
      )}
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

function VersionContextBanner({
  workflowDefinitions,
  selectedWorkflowDefinitionId,
}: {
  workflowDefinitions: AdminWorkflowDefinitionSummary[];
  selectedWorkflowDefinitionId: number;
}) {
  const definition = workflowDefinitions.find((d) => d.id === selectedWorkflowDefinitionId);
  if (!definition) return null;

  const publishedVersions = definition.versions.filter((v) => v.status === "published");
  const draftVersions = definition.versions.filter((v) => v.status !== "published" && v.status !== "archived");

  const hasPublished = publishedVersions.length > 0;
  const hasDraft = draftVersions.length > 0;
  const latestDraft = draftVersions[draftVersions.length - 1];
  const latestPublished = publishedVersions[publishedVersions.length - 1];

  if (hasPublished && hasDraft) {
    return (
      <div className="panel-note panel-note--warning" role="alert" style={{ display: "flex", gap: "0.5rem", alignItems: "flex-start" }}>
        <AlertTriangle size={14} style={{ flexShrink: 0, marginTop: "0.1rem" }} aria-hidden="true" />
        <span>
          <strong>Entwurf vorhanden (Version {latestDraft.versionNumber}).</strong>{" "}
          Änderungen hier gelten auf Definitions-Ebene — sie beziehen sich auf die veröffentlichte Version{" "}
          {latestPublished.versionNumber}. Wenn der Entwurf später veröffentlicht wird, können Specs aus dem
          Builder diese Änderungen überschreiben. Versions-sichere Pflege ist erst mit Backend-Unterstützung
          für versionierte Template-Endpunkte möglich.
        </span>
      </div>
    );
  }

  if (!hasPublished && hasDraft) {
    return (
      <p className="panel-note">
        Nur Entwurf (Version {latestDraft.versionNumber}) — noch keine veröffentlichte Version.
      </p>
    );
  }

  if (hasPublished && !hasDraft) {
    return (
      <p className="panel-note" style={{ color: "var(--color-text-secondary)" }}>
        Aktive Version: {latestPublished.versionNumber} (veröffentlicht) — kein offener Entwurf.
      </p>
    );
  }

  return null;
}
