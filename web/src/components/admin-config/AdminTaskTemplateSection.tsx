import type { AdminDepartmentAssignment, AdminResponsibilityOwner } from "../../types/auth";
import { formatTimestamp, responsibilityAreaLabel } from "./adminConfigHelpers";
import { useAdminTaskTemplateManagement } from "../../hooks/useAdminTaskTemplateManagement";
import { DependencyGraphEditor } from "./DependencyGraphEditor";

type AdminTaskTemplateSectionProps = {
  departments: AdminDepartmentAssignment[];
  responsibilities: AdminResponsibilityOwner[];
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
};

export function AdminTaskTemplateSection({
  departments,
  responsibilities,
  onNotice,
  onError,
}: AdminTaskTemplateSectionProps) {
  const {
    processTypes,
    selectedProcessTypeId,
    templates,
    dependencyGraph,
    selectedTemplate,
    answerDefinitions,
    draft,
    groupedConditions,
    conditionDraft,
    dependencies,
    isCreatingNew,
    isLoadingProcessTypes,
    isLoadingTemplates,
    isLoadingDependencyGraph,
    isLoadingConditions,
    isLoadingDependencies,
    isSaving,
    isDeleting,
    isSavingCondition,
    deletingConditionId,
    isSavingDependency,
    deletingDependencyId,
    selectProcessType,
    selectTemplate,
    startCreatingTemplate,
    updateDraft,
    updateConditionDraft,
    createTemplate,
    saveTemplate,
    removeTemplate,
    addCondition,
    removeCondition,
    addConditionGroup,
    createDependencyFromGraph,
    removeDependency,
    removeDependencyFromGraph,
  } = useAdminTaskTemplateManagement({
    onNotice,
    onError,
  });

  const panelTitle = isCreatingNew
    ? "Neues Task-Template"
    : selectedTemplate
      ? `Task-Template bearbeiten: ${selectedTemplate.title}`
      : "Task-Template auswählen";

  return (
    <div className="content-stack">
      <section className="panel panel-muted">
        <div className="panel-head">
          <h2>Task-Templates</h2>
          <p>
            Verwalten Sie Vorlagen pro Prozesstyp. Änderungen betreffen die Konfiguration für neue
            Workflows, nicht bereits erzeugte Workflow-Tasks.
          </p>
        </div>
      </section>

      <section className="panel">
        <div className="panel-head">
          <h2>Prozesstyp & Übersicht</h2>
          <p>Zuerst Prozesstyp wählen, dann Vorlagen filtern, anlegen oder bearbeiten.</p>
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
              disabled={!selectedProcessTypeId || isLoadingTemplates || isSaving || isDeleting}
              onClick={startCreatingTemplate}
            >
              Neues Template
            </button>
          </div>

          {isLoadingTemplates ? <p className="panel-note">Task-Templates werden geladen...</p> : null}

          {!isLoadingTemplates && selectedProcessTypeId && templates.length === 0 ? (
            <p className="panel-note">Für diesen Prozesstyp sind noch keine Task-Templates vorhanden.</p>
          ) : null}

          {!isLoadingTemplates && templates.length > 0 ? (
            <table className="table">
              <thead>
                <tr>
                  <th>Titel</th>
                  <th>Kategorie</th>
                  <th>Key</th>
                  <th>Sortierung</th>
                  <th>Aktiv</th>
                  <th>Bedingungen</th>
                  <th>Abhängigkeiten</th>
                </tr>
              </thead>
              <tbody>
                {templates.map((template) => {
                  const isSelected = !isCreatingNew && selectedTemplate?.id === template.id;

                  return (
                    <tr
                      key={template.id}
                      onClick={() => selectTemplate(template)}
                      style={{ cursor: "pointer", backgroundColor: isSelected ? "rgba(15, 118, 110, 0.08)" : undefined }}
                      aria-selected={isSelected}
                    >
                      <td><strong>{template.title}</strong></td>
                      <td>{template.category}</td>
                      <td className="text-muted">{template.templateKey}</td>
                      <td>{template.sortOrder}</td>
                      <td>
                        <span className={`badge badge--${template.isActive ? "success" : "default"}`}>
                          {template.isActive ? "Aktiv" : "Inaktiv"}
                        </span>
                      </td>
                      <td>{template.conditionCount}</td>
                      <td>{template.dependencyCount}</td>
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
          <h2>Dependency-Graph</h2>
          <p>
            Interaktive Übersicht aller Template-Abhängigkeiten des ausgewählten Prozesstyps.
            Verbindung erstellen per Drag von Quelle zu Ziel, löschen per Klick auf die Kante.
            Ein Klick auf einen Knoten öffnet das Detailpanel.
          </p>
        </div>
        <div className="panel-body">
          {!selectedProcessTypeId ? (
            <p className="panel-note">Bitte zuerst einen Prozesstyp auswählen.</p>
          ) : (
            <DependencyGraphEditor
              graph={dependencyGraph}
              templates={templates}
              selectedTemplateId={selectedTemplate?.id ?? null}
              isLoading={isLoadingDependencyGraph || isLoadingTemplates}
              isCreatingDependency={isSavingDependency}
              isDeletingDependency={deletingDependencyId !== null}
              onSelectTemplate={(templateId) => {
                const template = templates.find((entry) => entry.id === templateId);
                if (template) {
                  selectTemplate(template);
                }
              }}
              onCreateDependency={async (sourceTemplateId, targetTemplateId, requiredStatus) => {
                await createDependencyFromGraph(sourceTemplateId, targetTemplateId, requiredStatus);
              }}
              onDeleteDependency={async (dependencyId) => {
                await removeDependencyFromGraph(dependencyId);
              }}
            />
          )}
        </div>
      </section>

      <section className="panel">
        <div className="panel-head">
          <h2>{panelTitle}</h2>
          <p>
            Alle Felder werden direkt auf der Task-Vorlage gepflegt. Die Zuordnung bleibt strikt am
            ausgewählten Prozesstyp.
          </p>
        </div>

        {!selectedProcessTypeId ? (
          <p className="panel-note">Bitte zuerst einen Prozesstyp auswählen.</p>
        ) : !isCreatingNew && !selectedTemplate ? (
          <p className="panel-note">Bitte links ein Task-Template auswählen oder ein neues anlegen.</p>
        ) : (
          <div className="panel-body">
            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "1rem" }}>
              <label>
                <span className="form-label">Template-Key</span>
                <input
                  className="form-input"
                  value={draft.templateKey}
                  onChange={(event) => updateDraft("templateKey", event.target.value)}
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
                <span className="form-label">Owning Department</span>
                <select
                  className="form-select"
                  value={draft.owningDepartmentId}
                  onChange={(event) => updateDraft("owningDepartmentId", event.target.value)}
                >
                  <option value="">-- Keine feste Abteilung --</option>
                  {departments.map((department) => (
                    <option key={department.departmentId} value={department.departmentId}>
                      {department.departmentName}
                    </option>
                  ))}
                </select>
              </label>

              <label>
                <span className="form-label">Default Responsibility</span>
                <select
                  className="form-select"
                  value={draft.defaultResponsibilityId}
                  onChange={(event) => updateDraft("defaultResponsibilityId", event.target.value)}
                >
                  <option value="">-- Keine feste Zuständigkeit --</option>
                  {responsibilities.map((responsibility) => (
                    <option key={responsibility.responsibilityId} value={responsibility.responsibilityId}>
                      {responsibility.responsibilityName} ({responsibilityAreaLabel(responsibility)})
                    </option>
                  ))}
                </select>
              </label>

              <label>
                <span className="form-label">Prozessbereich</span>
                <input
                  className="form-input"
                  value={draft.processAreaLabel}
                  onChange={(event) => updateDraft("processAreaLabel", event.target.value)}
                />
              </label>

              <label>
                <span className="form-label">Fällig in Tagen</span>
                <input
                  className="form-input"
                  type="number"
                  min="0"
                  value={draft.dueInDays}
                  onChange={(event) => updateDraft("dueInDays", event.target.value)}
                />
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
                  checked={draft.isDepartmentPhaseTask}
                  onChange={(event) => updateDraft("isDepartmentPhaseTask", event.target.checked)}
                />
                <span>Department-Phase-Task</span>
              </label>

              <label style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
                <input
                  type="checkbox"
                  checked={draft.isRequired}
                  onChange={(event) => updateDraft("isRequired", event.target.checked)}
                />
                <span>Pflichtaufgabe</span>
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

            {selectedTemplate ? (
              <div style={{ marginTop: "1rem" }} className="panel-note">
                Erstellt am {formatTimestamp(selectedTemplate.createdAt)}. Bedingungen: {selectedTemplate.conditionCount},
                Abhängigkeiten: {selectedTemplate.dependencyCount}.
              </div>
            ) : null}

            <div style={{ display: "flex", gap: "0.75rem", marginTop: "1.5rem", flexWrap: "wrap" }}>
              {isCreatingNew ? (
                <button
                  type="button"
                  className="btn btn-primary"
                  disabled={isSaving || isDeleting}
                  onClick={() => void createTemplate()}
                >
                  {isSaving ? "Wird angelegt..." : "Template anlegen"}
                </button>
              ) : (
                <button
                  type="button"
                  className="btn btn-primary"
                  disabled={!selectedTemplate || isSaving || isDeleting}
                  onClick={() => void saveTemplate()}
                >
                  {isSaving ? "Wird gespeichert..." : "Änderungen speichern"}
                </button>
              )}

              <button
                type="button"
                className="btn btn-outline"
                disabled={!selectedTemplate || isCreatingNew || isSaving || isDeleting}
                onClick={() => void removeTemplate()}
              >
                {isDeleting ? "Wird gelöscht..." : "Template löschen"}
              </button>
            </div>
          </div>
        )}
      </section>

      {selectedTemplate && !isCreatingNew ? (
        <section className="panel">
          <div className="panel-head">
            <h2>Bedingungen</h2>
            <p>
              Bedingungen innerhalb derselben Gruppe werden per AND kombiniert. Unterschiedliche
              Gruppen werden per OR ausgewertet.
            </p>
          </div>

          <div className="panel-body">
            {isLoadingConditions ? <p className="panel-note">Bedingungen werden geladen...</p> : null}

            {!isLoadingConditions && groupedConditions.length === 0 ? (
              <p className="panel-note">Für dieses Template sind noch keine Bedingungen definiert.</p>
            ) : null}

            {!isLoadingConditions && groupedConditions.length > 0 ? (
              <div className="content-stack">
                {groupedConditions.map((group) => (
                  <section key={group.group} className="panel panel-muted">
                    <div className="panel-head">
                      <h2>Gruppe {group.group}</h2>
                      <p>Alle Regeln in dieser Gruppe müssen gleichzeitig erfüllt sein.</p>
                    </div>

                    <table className="table">
                      <thead>
                        <tr>
                          <th>Answer Key</th>
                          <th>Operator</th>
                          <th>Text</th>
                          <th>Bool</th>
                          <th>Zahl</th>
                          <th />
                        </tr>
                      </thead>
                      <tbody>
                        {group.items.map((condition) => (
                          <tr key={condition.id}>
                            <td className="text-muted">{condition.answerKey}</td>
                            <td>{condition.operator}</td>
                            <td>{condition.expectedValueText ?? "—"}</td>
                            <td>
                              {condition.expectedValueBoolean === null
                                ? "—"
                                : condition.expectedValueBoolean
                                  ? "true"
                                  : "false"}
                            </td>
                            <td>{condition.expectedValueNumber ?? "—"}</td>
                            <td>
                              <button
                                type="button"
                                className="btn btn-sm btn-outline"
                                disabled={deletingConditionId === condition.id || isSavingCondition}
                                onClick={() => void removeCondition(condition.id)}
                              >
                                {deletingConditionId === condition.id ? "Löscht..." : "Entfernen"}
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </section>
                ))}
              </div>
            ) : null}

            <div style={{ marginTop: "1.5rem", display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "1rem" }}>
              <label>
                <span className="form-label">Condition Group</span>
                <input
                  className="form-input"
                  type="number"
                  min="1"
                  value={conditionDraft.conditionGroup}
                  onChange={(event) => updateConditionDraft("conditionGroup", event.target.value)}
                />
              </label>

              <label>
                <span className="form-label">Answer Key</span>
                <select
                  className="form-select"
                  value={conditionDraft.answerKey}
                  onChange={(event) => updateConditionDraft("answerKey", event.target.value)}
                >
                  <option value="">-- Answer Definition wählen --</option>
                  {answerDefinitions.map((definition) => (
                    <option key={definition.id} value={definition.answerKey}>
                      {definition.answerKey} ({definition.title})
                    </option>
                  ))}
                </select>
              </label>

              <label>
                <span className="form-label">Operator</span>
                <select
                  className="form-select"
                  value={conditionDraft.operator}
                  onChange={(event) =>
                    updateConditionDraft(
                      "operator",
                      event.target.value as "eq" | "neq" | "is_true" | "is_false" | "is_null" | "is_not_null"
                    )
                  }
                >
                  <option value="eq">eq</option>
                  <option value="neq">neq</option>
                  <option value="is_true">is_true</option>
                  <option value="is_false">is_false</option>
                  <option value="is_null">is_null</option>
                  <option value="is_not_null">is_not_null</option>
                </select>
              </label>

              <label>
                <span className="form-label">Expected Text</span>
                <input
                  className="form-input"
                  value={conditionDraft.expectedValueText}
                  onChange={(event) => updateConditionDraft("expectedValueText", event.target.value)}
                />
              </label>

              <label>
                <span className="form-label">Expected Boolean</span>
                <select
                  className="form-select"
                  value={conditionDraft.expectedValueBoolean}
                  onChange={(event) => updateConditionDraft("expectedValueBoolean", event.target.value as "true" | "false" | "")}
                >
                  <option value="">-- leer --</option>
                  <option value="true">true</option>
                  <option value="false">false</option>
                </select>
              </label>

              <label>
                <span className="form-label">Expected Number</span>
                <input
                  className="form-input"
                  type="number"
                  value={conditionDraft.expectedValueNumber}
                  onChange={(event) => updateConditionDraft("expectedValueNumber", event.target.value)}
                />
              </label>
            </div>

            <div style={{ display: "flex", gap: "0.75rem", marginTop: "1.25rem", flexWrap: "wrap" }}>
              <button
                type="button"
                className="btn btn-secondary"
                disabled={isSavingCondition}
                onClick={addConditionGroup}
              >
                Neue Gruppe vorbereiten
              </button>

              <button
                type="button"
                className="btn btn-primary"
                disabled={isSavingCondition}
                onClick={() => void addCondition()}
              >
                {isSavingCondition ? "Wird angelegt..." : "Bedingung hinzufügen"}
              </button>
            </div>
          </div>
        </section>
      ) : null}

      {selectedTemplate && !isCreatingNew ? (
        <section className="panel">
          <div className="panel-head">
            <h2>Abhängigkeits-Details</h2>
            <p>
              Detailansicht für das aktuell ausgewählte Template. Neue Verbindungen werden im
              Dependency-Graph erstellt, bestehende können hier oder direkt über Kantenklick entfernt werden.
            </p>
          </div>

          <div className="panel-body">
            {isLoadingDependencies ? <p className="panel-note">Abhängigkeiten werden geladen...</p> : null}

            {!isLoadingDependencies && dependencies.length === 0 ? (
              <p className="panel-note">Für dieses Template sind noch keine Abhängigkeiten definiert.</p>
            ) : null}

            {!isLoadingDependencies && dependencies.length > 0 ? (
              <table className="table">
                <thead>
                  <tr>
                    <th>Abhängiges Template</th>
                    <th>Required Status</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {dependencies.map((dependency) => (
                    <tr key={dependency.id}>
                      <td>{dependency.dependsOnTemplateTitle}</td>
                      <td>{dependency.requiredStatus}</td>
                      <td>
                        <button
                          type="button"
                          className="btn btn-sm btn-outline"
                          disabled={deletingDependencyId === dependency.id || isSavingDependency}
                          onClick={() => void removeDependency(dependency.id)}
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
      ) : null}
    </div>
  );
}
