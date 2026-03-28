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
    processTypes,
    selectedProcessTypeId,
    definitions,
    selectedDefinition,
    draft,
    isCreatingNew,
    isLoadingProcessTypes,
    isLoadingDefinitions,
    isSaving,
    isDeleting,
    selectProcessType,
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
    ? "Neues Antwortfeld"
    : selectedDefinition
      ? `Antwortfeld bearbeiten: ${selectedDefinition.title}`
      : "Antwortfeld auswählen";

  return (
    <div className="content-stack">
      <div className="master-detail-layout">
        <section className="panel admin-detail-sidebar master-detail-sidebar">
          <SectionHeader title="Antwortfelder" />

          <div className="toolbar-row admin-detail-toolbar">
            <label className="field admin-detail-process-field">
              <span>Prozesstyp</span>
              <select
                value={selectedProcessTypeId ?? ""}
                onChange={(event) => selectProcessType(event.target.value)}
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
              disabled={!selectedProcessTypeId || isLoadingDefinitions || isSaving || isDeleting}
              onClick={startCreatingDefinition}
            >
              Neues Antwortfeld
            </button>
          </div>

          {!selectedProcessTypeId ? <p className="panel-note">Bitte zuerst einen Prozesstyp auswählen.</p> : null}
          {isLoadingDefinitions ? <p className="panel-note">Antwortfelder werden geladen...</p> : null}

          {!isLoadingDefinitions && selectedProcessTypeId && definitions.length === 0 ? (
            <p className="panel-note">Für diesen Prozesstyp sind noch keine Antwortfelder vorhanden.</p>
          ) : null}

          {!isLoadingDefinitions && definitions.length > 0 ? (
            <div className="selection-list" aria-label="Antwortfelder">
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

            {!selectedProcessTypeId ? (
              <p className="panel-note">Bitte zuerst einen Prozesstyp auswählen.</p>
            ) : !isCreatingNew && !selectedDefinition ? (
              <p className="panel-note">Bitte links ein Antwortfeld auswählen oder ein neues anlegen.</p>
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
                      {isSaving ? "Wird angelegt..." : "Antwortfeld anlegen"}
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
                    {isDeleting ? "Wird gelöscht..." : "Antwortfeld löschen"}
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
