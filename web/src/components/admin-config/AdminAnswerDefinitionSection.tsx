import { useAdminAnswerDefinitionManagement } from "../../hooks/useAdminAnswerDefinitionManagement";

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
      <section className="panel panel-muted">
        <div className="panel-head">
          <h2>Antwortfelder</h2>
          <p>Antwortfelder pro Prozesstyp verwalten.</p>
        </div>
      </section>

      <section className="panel">
        <div className="panel-head">
          <h2>Prozesstyp & Übersicht</h2>
          <p>Prozesstyp wählen und Antwortfelder bearbeiten.</p>
        </div>
        <div className="panel-body">
          <div style={{ display: "flex", gap: "1rem", alignItems: "end", flexWrap: "wrap", marginBottom: "1rem" }}>
            <label style={{ minWidth: "18rem" }}>
              <span className="form-label">Prozesstyp</span>
              <select
                className="form-select"
                value={selectedProcessTypeId ?? ""}
                onChange={(event) => selectProcessType(event.target.value)}
                disabled={isLoadingProcessTypes || isSaving || isDeleting}
              >
                <option value="">-- Prozesstyp wählen --</option>
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

          {isLoadingDefinitions ? <p className="panel-note">Antwortfelder werden geladen...</p> : null}

          {!isLoadingDefinitions && selectedProcessTypeId && definitions.length === 0 ? (
            <p className="panel-note">Für diesen Prozesstyp sind noch keine Antwortfelder vorhanden.</p>
          ) : null}

          {!isLoadingDefinitions && definitions.length > 0 ? (
            <table className="table">
              <thead>
                <tr>
                  <th>Key</th>
                  <th>Titel</th>
                  <th>Input-Typ</th>
                  <th>Kategorie</th>
                  <th>Pflicht</th>
                  <th>Aktiv</th>
                  <th>Sortierung</th>
                </tr>
              </thead>
              <tbody>
                {definitions.map((definition) => {
                  const isSelected = !isCreatingNew && selectedDefinition?.id === definition.id;

                  return (
                    <tr
                      key={definition.id}
                      onClick={() => selectDefinition(definition)}
                      style={{ cursor: "pointer", backgroundColor: isSelected ? "rgba(15, 118, 110, 0.08)" : undefined }}
                      aria-selected={isSelected}
                    >
                      <td className="text-muted">{definition.answerKey}</td>
                      <td><strong>{definition.title}</strong></td>
                      <td>{definition.inputType}</td>
                      <td>{definition.category}</td>
                      <td>{definition.isRequired ? "Ja" : "Nein"}</td>
                      <td>
                        <span className={`badge badge--${definition.isActive ? "success" : "default"}`}>
                          {definition.isActive ? "Aktiv" : "Inaktiv"}
                        </span>
                      </td>
                      <td>{definition.sortOrder}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          ) : null}
        </div>
      </section>

      <section className="panel">
        <div className="panel-head">
          <h2>{panelTitle}</h2>
          <p>Felder direkt am Antwortfeld pflegen.</p>
        </div>

        {!selectedProcessTypeId ? (
          <p className="panel-note">Bitte zuerst einen Prozesstyp auswählen.</p>
        ) : !isCreatingNew && !selectedDefinition ? (
          <p className="panel-note">Bitte links ein Antwortfeld auswählen oder ein neues anlegen.</p>
        ) : (
          <div className="panel-body">
            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "1rem" }}>
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

            <label style={{ display: "block", marginTop: "1rem" }}>
              <span className="form-label">Beschreibung</span>
              <textarea
                className="form-input"
                value={draft.description}
                onChange={(event) => updateDraft("description", event.target.value)}
                rows={5}
              />
            </label>

            <div style={{ display: "flex", gap: "1.5rem", flexWrap: "wrap", marginTop: "1rem" }}>
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

            <div style={{ display: "flex", gap: "0.75rem", marginTop: "1.5rem", flexWrap: "wrap" }}>
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
  );
}
