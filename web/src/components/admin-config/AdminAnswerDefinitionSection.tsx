import { useAdminAnswerDefinitionManagement } from "../../hooks/useAdminAnswerDefinitionManagement";
import SectionHeader from "../ui/SectionHeader";
import SelectionListItem from "../ui/SelectionListItem";

type AdminAnswerDefinitionSectionProps = {
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
};

const INPUT_TYPE_OPTIONS = [
  { value: "boolean", label: "boolean" },
  { value: "text", label: "text" },
  { value: "select", label: "select" },
  { value: "multi_select", label: "multi_select" },
] as const;

export function AdminAnswerDefinitionSection({
  onNotice,
  onError,
}: AdminAnswerDefinitionSectionProps) {
  const {
    workflowDefinitions,
    selectedWorkflowDefinitionId,
    definitions,
    selectedDefinition,
    draft,
    isCreatingNew,
    isLoadingProcessTypes,
    isLoadingDefinitions,
    isSaving,
    isDeleting,
    selectWorkflowDefinition,
    selectDefinition,
    startCreatingDefinition,
    updateDraft,
    createDefinition,
    saveDefinition,
    removeDefinition,
  } = useAdminAnswerDefinitionManagement({
    onNotice,
    onError,
  });

  const panelTitle = isCreatingNew
    ? "Neues Feld"
    : selectedDefinition
      ? `Feld bearbeiten: ${selectedDefinition.title}`
      : "Feld auswählen";

  return (
    <div className="content-stack">
      <div className="master-detail-layout">
        <section className="panel admin-detail-sidebar master-detail-sidebar">
          <SectionHeader title="Felder definieren" />

          <div className="toolbar-row admin-detail-toolbar">
            <label className="field admin-detail-process-field">
              <span>Workflow-Definition</span>
              <select
                value={selectedWorkflowDefinitionId ?? ""}
                onChange={(event) => selectWorkflowDefinition(event.target.value)}
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
              disabled={!selectedWorkflowDefinitionId || isLoadingDefinitions || isSaving || isDeleting}
              onClick={startCreatingDefinition}
            >
              Neues Feld
            </button>
          </div>

          {!selectedWorkflowDefinitionId ? <p className="panel-note">Bitte zuerst eine Workflow-Definition auswählen.</p> : null}
          {isLoadingDefinitions ? <p className="panel-note">Felder werden geladen...</p> : null}

          {!isLoadingDefinitions && selectedWorkflowDefinitionId && definitions.length === 0 ? (
            <p className="panel-note">Für diese Workflow-Definition sind noch keine Felder vorhanden.</p>
          ) : null}

          {!isLoadingDefinitions && definitions.length > 0 ? (
            <div className="selection-list" aria-label="Felder">
              {definitions.map((definition) => {
                const isSelected = !isCreatingNew && selectedDefinition?.id === definition.id;

                return (
                  <SelectionListItem
                    key={definition.id}
                    active={isSelected}
                    title={definition.title}
                    meta={`Key ${definition.answerKey} | ${definition.inputType}`}
                    secondaryMeta={`${definition.category} | Sortierung ${definition.sortOrder} | ${definition.isRequired ? "Pflicht" : "Optional"}`}
                    onClick={() => selectDefinition(definition)}
                  />
                );
              })}
            </div>
          ) : null}
        </section>

        <div className="content-stack admin-detail-main master-detail-main">
          <section className="panel">
            <SectionHeader title={panelTitle} />

            {!selectedWorkflowDefinitionId ? (
              <p className="panel-note">Bitte zuerst eine Workflow-Definition auswählen.</p>
            ) : !isCreatingNew && !selectedDefinition ? (
              <p className="panel-note">Bitte links ein Feld auswählen oder ein neues anlegen.</p>
            ) : (
              <div className="content-stack">
                <div
                  style={{
                    display: "grid",
                    gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))",
                    gap: "1rem",
                  }}
                >
                  <label>
                    <span className="form-label">Antwort-Key</span>
                    <input
                      className="form-input"
                      value={draft.answerKey}
                      onChange={(event) => updateDraft("answerKey", event.target.value)}
                    />
                  </label>

                  <label>
                    <span className="form-label">Titel</span>
                    <input
                      className="form-input"
                      value={draft.title}
                      onChange={(event) => updateDraft("title", event.target.value)}
                    />
                  </label>

                  <label>
                    <span className="form-label">Kategorie</span>
                    <input
                      className="form-input"
                      value={draft.category}
                      onChange={(event) => updateDraft("category", event.target.value)}
                    />
                  </label>

                  <label>
                    <span className="form-label">Icon-Key</span>
                    <input
                      className="form-input"
                      value={draft.iconKey}
                      onChange={(event) => updateDraft("iconKey", event.target.value)}
                    />
                  </label>

                  <label>
                    <span className="form-label">Input-Typ</span>
                    <select
                      className="form-select"
                      value={draft.inputType}
                      onChange={(event) =>
                        updateDraft("inputType", event.target.value as "boolean" | "text" | "select" | "multi_select")
                      }
                    >
                      {INPUT_TYPE_OPTIONS.map((option) => (
                        <option key={option.value} value={option.value}>
                          {option.label}
                        </option>
                      ))}
                    </select>
                  </label>

                  <label>
                    <span className="form-label">Sortierung</span>
                    <input
                      className="form-input"
                      type="number"
                      value={draft.sortOrder}
                      onChange={(event) => updateDraft("sortOrder", event.target.value)}
                    />
                  </label>
                </div>

                <label>
                  <span className="form-label">Beschreibung</span>
                  <textarea
                    className="form-input"
                    value={draft.description}
                    onChange={(event) => updateDraft("description", event.target.value)}
                    rows={5}
                  />
                </label>

                <div style={{ display: "flex", gap: "1.5rem", flexWrap: "wrap" }}>
                  <label style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
                    <input
                      type="checkbox"
                      checked={draft.isRequired}
                      onChange={(event) => updateDraft("isRequired", event.target.checked)}
                    />
                    <span>Pflichtfeld</span>
                  </label>

                  <label style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
                    <input
                      type="checkbox"
                      checked={draft.isActive}
                      onChange={(event) => updateDraft("isActive", event.target.checked)}
                    />
                    <span>Aktiv</span>
                  </label>
                </div>

                <div style={{ display: "flex", gap: "0.75rem", flexWrap: "wrap" }}>
                  {isCreatingNew ? (
                    <button
                      type="button"
                      className="btn btn-primary"
                      disabled={isSaving || isDeleting}
                      onClick={() => void createDefinition()}
                    >
                      {isSaving ? "Wird angelegt..." : "Feld anlegen"}
                    </button>
                  ) : (
                    <button
                      type="button"
                      className="btn btn-primary"
                      disabled={!selectedDefinition || isSaving || isDeleting}
                      onClick={() => void saveDefinition()}
                    >
                      {isSaving ? "Wird gespeichert..." : "Änderungen speichern"}
                    </button>
                  )}

                  <button
                    type="button"
                    className="btn btn-outline"
                    disabled={!selectedDefinition || isCreatingNew || isSaving || isDeleting}
                    onClick={() => void removeDefinition()}
                  >
                    {isDeleting ? "Wird gelöscht..." : "Feld löschen"}
                  </button>
                </div>
              </div>
            )}
          </section>
        </div>
      </div>
    </div>
  );
}
