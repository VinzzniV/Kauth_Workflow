import type {
  AdminDepartmentAssignment,
  AdminResponsibilityOwner,
  AdminTaskSpec,
} from "../../types/auth";
import type { TemplateDraft } from "../../hooks/adminTaskTemplateManagementModel";
import { formatTimestamp, responsibilityAreaLabel } from "./adminConfigHelpers";
import SectionHeader from "../ui/SectionHeader";

type AdminTaskTemplateEditorProps = {
  panelTitle: string;
  selectedWorkflowDefinitionId: number | null;
  selectedTemplate: AdminTaskSpec | null;
  isCreatingNew: boolean;
  draft: TemplateDraft;
  departments: AdminDepartmentAssignment[];
  responsibilities: AdminResponsibilityOwner[];
  isSaving: boolean;
  isDeleting: boolean;
  onUpdateDraft: <K extends keyof TemplateDraft>(key: K, value: TemplateDraft[K]) => void;
  onCreateTemplate: () => Promise<void>;
  onSaveTemplate: () => Promise<void>;
  onRemoveTemplate: () => Promise<void>;
};

export function AdminTaskTemplateEditor({
  panelTitle,
  selectedWorkflowDefinitionId,
  selectedTemplate,
  isCreatingNew,
  draft,
  departments,
  responsibilities,
  isSaving,
  isDeleting,
  onUpdateDraft,
  onCreateTemplate,
  onSaveTemplate,
  onRemoveTemplate,
}: AdminTaskTemplateEditorProps) {
  return (
    <section className="panel">
      <SectionHeader title={panelTitle} />

      {!selectedWorkflowDefinitionId ? (
        <p className="panel-note">Bitte zuerst eine Workflow-Definition auswählen.</p>
      ) : !isCreatingNew && !selectedTemplate ? (
        <p className="panel-note">Bitte links eine Aufgabenvorlage auswählen oder eine neue anlegen.</p>
      ) : (
        <div className="content-stack">
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "1rem" }}>
            <label>
              <span className="form-label">Vorlagen-Key</span>
              <input className="form-input" value={draft.specKey} onChange={(event) => onUpdateDraft("specKey", event.target.value)} />
            </label>
            <label>
              <span className="form-label">Titel</span>
              <input className="form-input" value={draft.title} onChange={(event) => onUpdateDraft("title", event.target.value)} />
            </label>
            <label>
              <span className="form-label">Kategorie</span>
              <input className="form-input" value={draft.category} onChange={(event) => onUpdateDraft("category", event.target.value)} />
            </label>
            <label>
              <span className="form-label">Icon-Key</span>
              <input className="form-input" value={draft.iconKey} onChange={(event) => onUpdateDraft("iconKey", event.target.value)} />
            </label>
            <label>
              <span className="form-label">Zuständige Abteilung</span>
              <select className="form-select" value={draft.owningDepartmentId} onChange={(event) => onUpdateDraft("owningDepartmentId", event.target.value)}>
                <option value="">Keine feste Abteilung</option>
                {departments.map((department) => (
                  <option key={department.departmentId} value={department.departmentId}>
                    {department.departmentName}
                  </option>
                ))}
              </select>
            </label>
            <label>
              <span className="form-label">Standard-Zuständigkeit</span>
              <select className="form-select" value={draft.defaultResponsibilityId} onChange={(event) => onUpdateDraft("defaultResponsibilityId", event.target.value)}>
                <option value="">Keine feste Zuständigkeit</option>
                {responsibilities.map((responsibility) => (
                  <option key={responsibility.responsibilityId} value={responsibility.responsibilityId}>
                    {responsibility.responsibilityName} ({responsibilityAreaLabel(responsibility)})
                  </option>
                ))}
              </select>
            </label>
            <label>
              <span className="form-label">Prozessbereich</span>
              <input className="form-input" value={draft.processAreaLabel} onChange={(event) => onUpdateDraft("processAreaLabel", event.target.value)} />
            </label>
            <label>
              <span className="form-label">Fällig in Tagen</span>
              <input className="form-input" type="number" min="0" value={draft.dueInDays} onChange={(event) => onUpdateDraft("dueInDays", event.target.value)} />
            </label>
            <label>
              <span className="form-label">Sortierung</span>
              <input className="form-input" type="number" value={draft.sortOrder} onChange={(event) => onUpdateDraft("sortOrder", event.target.value)} />
            </label>
          </div>

          <label>
            <span className="form-label">Beschreibung</span>
            <textarea className="form-input" value={draft.description} onChange={(event) => onUpdateDraft("description", event.target.value)} rows={5} />
          </label>

          <div style={{ display: "flex", gap: "1.5rem", flexWrap: "wrap" }}>
            <label style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
              <input type="checkbox" checked={draft.isDepartmentPhaseTask} onChange={(event) => onUpdateDraft("isDepartmentPhaseTask", event.target.checked)} />
              <span>Aufgabe der Fachbereichsphase</span>
            </label>
            <label style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
              <input type="checkbox" checked={draft.isRequired} onChange={(event) => onUpdateDraft("isRequired", event.target.checked)} />
              <span>Pflichtaufgabe</span>
            </label>
            <label style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
              <input type="checkbox" checked={draft.isActive} onChange={(event) => onUpdateDraft("isActive", event.target.checked)} />
              <span>Aktiv</span>
            </label>
          </div>

          {selectedTemplate ? (
            <p className="panel-note">
              Erstellt am {formatTimestamp(selectedTemplate.createdAt)} | Bedingungen {selectedTemplate.conditionCount} | Abhängigkeiten{" "}
              {selectedTemplate.dependencyCount}
            </p>
          ) : null}

          <div style={{ display: "flex", gap: "0.75rem", flexWrap: "wrap" }}>
            {isCreatingNew ? (
              <button type="button" className="btn btn-primary" disabled={isSaving || isDeleting} onClick={() => void onCreateTemplate()}>
                {isSaving ? "Wird angelegt..." : "Aufgabenvorlage anlegen"}
              </button>
            ) : (
              <button type="button" className="btn btn-primary" disabled={!selectedTemplate || isSaving || isDeleting} onClick={() => void onSaveTemplate()}>
                {isSaving ? "Wird gespeichert..." : "Änderungen speichern"}
              </button>
            )}

            <button
              type="button"
              className="btn btn-outline"
              disabled={!selectedTemplate || isCreatingNew || isSaving || isDeleting}
              onClick={() => void onRemoveTemplate()}
            >
              {isDeleting ? "Wird gelöscht..." : "Aufgabenvorlage löschen"}
            </button>
          </div>
        </div>
      )}
    </section>
  );
}
