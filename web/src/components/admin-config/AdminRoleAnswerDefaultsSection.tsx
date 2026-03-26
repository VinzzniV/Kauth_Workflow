import { useAdminRoleAnswerDefaults } from "../../hooks/useAdminRoleAnswerDefaults";

type AdminRoleAnswerDefaultsSectionProps = {
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
};

export function AdminRoleAnswerDefaultsSection({
  onNotice,
  onError,
}: AdminRoleAnswerDefaultsSectionProps) {
  const {
    processTypes,
    selectedProcessTypeId,
    sortedRoles,
    sortedDefinitions,
    isLoadingProcessTypes,
    isLoadingMatrix,
    isSaving,
    hasChanges,
    selectProcessType,
    getCellDraft,
    updateTextDraft,
    updateBooleanDraft,
    saveDefaults,
  } = useAdminRoleAnswerDefaults({
    onNotice,
    onError,
  });

  return (
    <div className="content-stack">
      <section className="panel panel-muted">
        <div className="panel-head">
          <h2>Role Answer Defaults</h2>
          <p>
            Pflegen Sie Standardwerte pro Prozesstyp, Answer Definition und Rolle in einer Matrix.
            Gespeichert wird gesammelt per Bulk-Update.
          </p>
        </div>
      </section>

      <section className="panel">
        <div className="panel-head">
          <h2>Prozesstyp & Matrix</h2>
          <p>Wählen Sie zuerst einen Prozesstyp. Danach erscheinen die aktiven Rollen als Spalten.</p>
        </div>
        <div className="panel-body">
          <div style={{ display: "flex", gap: "1rem", alignItems: "end", flexWrap: "wrap", marginBottom: "1rem" }}>
            <label style={{ minWidth: "18rem" }}>
              <span className="form-label">Prozesstyp</span>
              <select
                className="form-select"
                value={selectedProcessTypeId ?? ""}
                onChange={(event) => selectProcessType(event.target.value)}
                disabled={isLoadingProcessTypes || isSaving}
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
              disabled={!selectedProcessTypeId || !hasChanges || isSaving}
              onClick={() => void saveDefaults()}
            >
              {isSaving ? "Wird gespeichert..." : "Alle Änderungen speichern"}
            </button>
          </div>

          {isLoadingMatrix ? <p className="panel-note">Role Answer Defaults werden geladen...</p> : null}

          {!isLoadingMatrix && selectedProcessTypeId && sortedDefinitions.length === 0 ? (
            <p className="panel-note">Für diesen Prozesstyp sind noch keine Answer Definitions vorhanden.</p>
          ) : null}

          {!isLoadingMatrix && selectedProcessTypeId && sortedRoles.length === 0 ? (
            <p className="panel-note">Es sind keine Admin-Rollen vorhanden.</p>
          ) : null}

          {!isLoadingMatrix && selectedProcessTypeId && sortedDefinitions.length > 0 && sortedRoles.length > 0 ? (
            <div style={{ overflowX: "auto" }}>
              <table className="table">
                <thead>
                  <tr>
                    <th>Answer</th>
                    <th>Input-Typ</th>
                    {sortedRoles.map((role) => (
                      <th key={role.roleId}>{role.departmentName ? `${role.departmentName} / ${role.roleName}` : role.roleName}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {sortedDefinitions.map((definition) => (
                    <tr key={definition.id}>
                      <td>
                        <strong>{definition.title}</strong>
                        <div className="text-muted" style={{ fontSize: "0.85rem" }}>{definition.answerKey}</div>
                      </td>
                      <td>{definition.inputType}</td>
                      {sortedRoles.map((role) => {
                        const draft = getCellDraft(role.roleId, definition.answerKey);

                        return (
                          <td key={`${definition.id}-${role.roleId}`}>
                            {definition.inputType === "boolean" ? (
                              <select
                                className="form-select"
                                value={draft.boolean === null ? "" : draft.boolean ? "true" : "false"}
                                onChange={(event) =>
                                  updateBooleanDraft(
                                    role.roleId,
                                    definition.answerKey,
                                    event.target.value === ""
                                      ? null
                                      : event.target.value === "true"
                                  )
                                }
                              >
                                <option value="">-- leer --</option>
                                <option value="true">true</option>
                                <option value="false">false</option>
                              </select>
                            ) : (
                              <input
                                className="form-input"
                                value={draft.text}
                                onChange={(event) =>
                                  updateTextDraft(role.roleId, definition.answerKey, event.target.value)
                                }
                                placeholder={definition.inputType === "text" ? "Textwert" : "Optionswert"}
                              />
                            )}
                          </td>
                        );
                      })}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : null}
        </div>
      </section>
    </div>
  );
}
