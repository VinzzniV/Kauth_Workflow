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
    workflowDefinitions,
    selectedWorkflowDefinitionId,
    sortedRoles,
    sortedDefinitions,
    isLoadingProcessTypes,
    isLoadingMatrix,
    isSaving,
    hasChanges,
    selectWorkflowDefinition,
    getCellDraft,
    updateTextDraft,
    updateBooleanDraft,
    saveDefaults,
  } = useAdminRoleAnswerDefaults({
    onNotice,
    onError,
  });

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Vorgaben je Rolle</h2>
        <p>Wählen Sie einen Prozesstyp und pflegen Sie die Vorauswahlen, die neue Vorgänge für einzelne Rollen mitbringen sollen.</p>
      </div>
      <div className="panel-body">
        <div style={{ display: "flex", gap: "1rem", alignItems: "end", flexWrap: "wrap", marginBottom: "1rem" }}>
          <label style={{ minWidth: "18rem" }}>
            <span className="form-label">Workflow-Definition</span>
            <select
              className="form-select"
              value={selectedWorkflowDefinitionId ?? ""}
              onChange={(event) => selectWorkflowDefinition(event.target.value)}
              disabled={isLoadingProcessTypes || isSaving}
            >
              <option value="">-- Workflow-Definition wählen --</option>
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
            disabled={!selectedWorkflowDefinitionId || !hasChanges || isSaving}
            onClick={() => void saveDefaults()}
          >
            {isSaving ? "Wird gespeichert..." : "Vorgaben speichern"}
          </button>
        </div>

        {isLoadingMatrix ? <p className="panel-note">Vorgaben werden geladen...</p> : null}

        {!isLoadingMatrix && selectedWorkflowDefinitionId && sortedDefinitions.length === 0 ? (
          <p className="panel-note">Für diese Workflow-Definition sind noch keine Felder vorhanden.</p>
        ) : null}

        {!isLoadingMatrix && selectedWorkflowDefinitionId && sortedRoles.length === 0 ? (
          <p className="panel-note">Es sind keine Admin-Rollen vorhanden.</p>
        ) : null}

        {!isLoadingMatrix && selectedWorkflowDefinitionId && sortedDefinitions.length > 0 && sortedRoles.length > 0 ? (
          <div style={{ overflowX: "auto" }}>
            <table className="table">
              <thead>
                <tr>
                  <th>Feld</th>
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
  );
}
