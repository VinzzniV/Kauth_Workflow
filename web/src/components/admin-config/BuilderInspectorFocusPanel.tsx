import { useEffect, useMemo, useState } from "react";
import { useAdminAnswerDefinitionManagement } from "../../hooks/useAdminAnswerDefinitionManagement";
import { useAdminTaskTemplateManagement } from "../../hooks/useAdminTaskTemplateManagement";
import type {
  BuilderInspectorFocusSection,
  BuilderInspectorFocusTarget,
} from "../../hooks/useAdminWorkflowBuilder";
import type {
  AdminDepartmentAssignment,
  AdminProcessType,
  AdminResponsibilityOwner,
} from "../../types/auth";
import {
  updateAdminProcessType,
} from "../../services/adminConfigApi";
import { getAdminDepartmentAssignments } from "../../services/adminApi";
import { AdminTaskTemplateConditionsPanel } from "./AdminTaskTemplateConditionsPanel";
import { AdminTaskTemplateDependenciesPanel } from "./AdminTaskTemplateDependenciesPanel";
import { AdminTaskTemplateEditor } from "./AdminTaskTemplateEditor";

type BuilderInspectorFocusPanelProps = {
  focus: BuilderInspectorFocusTarget;
  processTypes: AdminProcessType[];
  responsibilityOwners: AdminResponsibilityOwner[];
  onClose: () => void;
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
  onDataChanged: () => void | Promise<void>;
};

const INPUT_TYPE_OPTIONS = [
  { value: "boolean", label: "boolean" },
  { value: "text", label: "text" },
  { value: "select", label: "select" },
  { value: "multi_select", label: "multi_select" },
] as const;

export function BuilderInspectorFocusPanel({
  focus,
  processTypes,
  responsibilityOwners,
  onClose,
  onNotice,
  onError,
  onDataChanged,
}: BuilderInspectorFocusPanelProps) {
  const header = useMemo(() => {
    switch (focus.mode) {
      case "process_type":
        return {
          eyebrow: "Gezielt bearbeiten",
          title: "Prozesstyp",
          description: "Die fachliche Grundlage dieses Bausteins wird direkt im Builder-Kontext bearbeitet.",
        };
      case "answer_definition":
        return {
          eyebrow: "Gezielt bearbeiten",
          title: "Antwortfeld",
          description: "Das referenzierte Feld bleibt im Builder-Kontext bearbeitbar, ohne den Ablaufkontext zu verlieren.",
        };
      case "task_template_conditions":
        return {
          eyebrow: "Gezielt bearbeiten",
          title: "Template-Bedingungen",
          description: "Sie bearbeiten genau die Bedingungen der ausgewählten Maßnahme im selben Sidebar-Kontext.",
        };
      case "task_template_dependencies":
        return {
          eyebrow: "Gezielt bearbeiten",
          title: "Template-Abhängigkeiten",
          description: "Sie bearbeiten genau die Abhängigkeiten der ausgewählten Maßnahme im selben Sidebar-Kontext.",
        };
      case "task_template":
      default:
        return {
          eyebrow: "Gezielt bearbeiten",
          title: "Aufgabenvorlage",
          description: "Die referenzierte Vorlage bleibt im Builder sichtbar und wird hier ohne Bereichswechsel bearbeitet.",
        };
    }
  }, [focus.mode]);

  return (
    <section className="builder-sidebar-panel content-stack" aria-label="Gezielte Bearbeitung">
      <div className="builder-sidebar-panel__header">
        <span className="builder-sidebar-panel__eyebrow">{header.eyebrow}</span>
        <h3>{header.title}</h3>
        <p className="text-muted">{header.description}</p>
      </div>

      <button
        type="button"
        className="button-secondary builder-action-button builder-action-button--secondary"
        onClick={onClose}
      >
        Zurück zum Baustein
      </button>

      {focus.mode === "process_type" ? (
        <BuilderFocusedProcessTypeEditor
          focus={focus}
          processTypes={processTypes}
          onNotice={onNotice}
          onError={onError}
          onDataChanged={onDataChanged}
        />
      ) : null}

      {focus.mode === "answer_definition" ? (
        <BuilderFocusedAnswerDefinitionEditor
          focus={focus}
          onNotice={onNotice}
          onError={onError}
          onDataChanged={onDataChanged}
        />
      ) : null}

      {focus.mode === "task_template"
      || focus.mode === "task_template_conditions"
      || focus.mode === "task_template_dependencies" ? (
        <BuilderFocusedTaskTemplateEditor
          focus={focus}
          responsibilityOwners={responsibilityOwners}
          onNotice={onNotice}
          onError={onError}
          onDataChanged={onDataChanged}
        />
      ) : null}
    </section>
  );
}

function BuilderFocusedProcessTypeEditor({
  focus,
  processTypes,
  onNotice,
  onError,
  onDataChanged,
}: {
  focus: BuilderInspectorFocusTarget;
  processTypes: AdminProcessType[];
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
  onDataChanged: () => void | Promise<void>;
}) {
  const selectedProcessType = useMemo(
    () => processTypes.find((processType) => processType.id === focus.processTypeId) ?? null,
    [focus.processTypeId, processTypes]
  );
  const [draft, setDraft] = useState({
    name: "",
    description: "",
    iconKey: "",
    sortOrder: "",
  });
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    setDraft({
      name: selectedProcessType?.name ?? "",
      description: selectedProcessType?.description ?? "",
      iconKey: selectedProcessType?.iconKey ?? "",
      sortOrder: selectedProcessType ? String(selectedProcessType.sortOrder) : "",
    });
  }, [selectedProcessType]);

  const save = async () => {
    if (!selectedProcessType) {
      return;
    }

    const nextSortOrder = Number(draft.sortOrder);
    if (Number.isNaN(nextSortOrder)) {
      onNotice(null);
      onError("Die Reihenfolge muss eine Zahl sein.");
      return;
    }

    setIsSaving(true);
    onNotice(null);
    onError(null);

    try {
      await updateAdminProcessType(selectedProcessType.id, {
        name: draft.name.trim(),
        description: draft.description.trim() || null,
        iconKey: draft.iconKey.trim() || null,
        sortOrder: nextSortOrder,
      });
      onNotice(`Prozesstyp ${draft.name.trim() || selectedProcessType.name} wurde gespeichert.`);
      await onDataChanged();
    } catch (err) {
      onError(err instanceof Error ? err.message : "Der Prozesstyp konnte nicht gespeichert werden.");
    } finally {
      setIsSaving(false);
    }
  };

  const toggleActive = async () => {
    if (!selectedProcessType) {
      return;
    }

    if (!selectedProcessType.isActive && !selectedProcessType.canActivate) {
      onNotice(null);
      onError(selectedProcessType.activationBlockedReason ?? "Dieser Prozesstyp kann derzeit nicht aktiviert werden.");
      return;
    }

    setIsSaving(true);
    onNotice(null);
    onError(null);
    try {
      await updateAdminProcessType(selectedProcessType.id, { isActive: !selectedProcessType.isActive });
      onNotice(`Prozesstyp ${selectedProcessType.name} wurde ${selectedProcessType.isActive ? "deaktiviert" : "aktiviert"}.`);
      await onDataChanged();
    } catch (err) {
      onError(err instanceof Error ? err.message : "Der Status konnte nicht geändert werden.");
    } finally {
      setIsSaving(false);
    }
  };

  if (!selectedProcessType) {
    return (
      <div className="panel panel-warning">
        <p className="panel-text">Der referenzierte Prozesstyp konnte im aktuellen Kontext nicht geladen werden.</p>
      </div>
    );
  }

  return (
    <div className="content-stack">
      <div className="panel panel-muted content-stack" style={{ gap: "0.35rem" }}>
        <span className="badge badge--default">Quelle</span>
        <strong>{selectedProcessType.name}</strong>
        <span className="text-muted">
          Key {selectedProcessType.key} | {selectedProcessType.isActive ? "Aktiv" : "Inaktiv"}
        </span>
      </div>

      <label>
        <span>Name</span>
        <input
          className="form-input"
          value={draft.name}
          onChange={(event) => setDraft((current) => ({ ...current, name: event.target.value }))}
        />
      </label>
      <label>
        <span>Beschreibung</span>
        <textarea
          className="form-input"
          rows={4}
          value={draft.description}
          onChange={(event) => setDraft((current) => ({ ...current, description: event.target.value }))}
        />
      </label>
      <div className="grid-two-columns">
        <label>
          <span>Icon-Key</span>
          <input
            className="form-input"
            value={draft.iconKey}
            onChange={(event) => setDraft((current) => ({ ...current, iconKey: event.target.value }))}
          />
        </label>
        <label>
          <span>Reihenfolge</span>
          <input
            className="form-input"
            type="number"
            value={draft.sortOrder}
            onChange={(event) => setDraft((current) => ({ ...current, sortOrder: event.target.value }))}
          />
        </label>
      </div>

      <div className="panel panel-info">
        <p className="panel-text">
          Diese Änderung wirkt auf die fachliche Grundlage des Builders. Anforderungen und Maßnahmen bleiben davon getrennt und werden über Felder und Vorlagen gesteuert.
        </p>
      </div>

      <div style={{ display: "flex", gap: "0.75rem", flexWrap: "wrap" }}>
        <button
          type="button"
          className="btn btn-primary"
          disabled={isSaving}
          onClick={() => void save()}
        >
          {isSaving ? "Wird gespeichert..." : "Änderungen speichern"}
        </button>
        <button
          type="button"
          className="btn btn-outline"
          disabled={isSaving}
          onClick={() => void toggleActive()}
        >
          {selectedProcessType.isActive ? "Deaktivieren" : "Aktivieren"}
        </button>
      </div>
    </div>
  );
}

function BuilderFocusedAnswerDefinitionEditor({
  focus,
  onNotice,
  onError,
  onDataChanged,
}: {
  focus: BuilderInspectorFocusTarget;
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
  onDataChanged: () => void | Promise<void>;
}) {
  const management = useAdminAnswerDefinitionManagement({
    onNotice: (message) => {
      onNotice(message);
      if (message) {
        void onDataChanged();
      }
    },
    onError,
  });

  useEffect(() => {
    if (focus.processTypeId && management.selectedProcessTypeId !== focus.processTypeId) {
      management.selectProcessType(String(focus.processTypeId));
    }
  }, [focus.processTypeId, management]);

  useEffect(() => {
    if (!focus.answerDefinitionId || management.definitions.length === 0) {
      return;
    }

    const matchingDefinition = management.definitions.find((definition) => definition.id === focus.answerDefinitionId) ?? null;
    if (matchingDefinition && management.selectedDefinition?.id !== matchingDefinition.id) {
      management.selectDefinition(matchingDefinition);
    }
  }, [focus.answerDefinitionId, management]);

  const selectedDefinition = management.selectedDefinition;

  if (!focus.processTypeId) {
    return (
      <div className="panel panel-warning">
        <p className="panel-text">Für dieses Feld fehlt der zugehörige Prozesstyp-Kontext.</p>
      </div>
    );
  }

  return (
    <div className="content-stack">
      <div className="panel panel-muted content-stack" style={{ gap: "0.35rem" }}>
        <span className="badge badge--default">Quelle</span>
        <strong>{selectedDefinition?.title ?? "Feld wird geladen"}</strong>
        <span className="text-muted">
          {selectedDefinition
            ? `Kategorie ${selectedDefinition.category} | ${selectedDefinition.isRequired ? "Pflichtfeld" : "Optional"}`
            : "Das referenzierte Feld wird im gewählten Prozesstyp gesucht."}
        </span>
      </div>

      {management.isLoadingDefinitions ? <p className="text-muted">Feld wird geladen...</p> : null}
      {!management.isLoadingDefinitions && !selectedDefinition ? (
        <div className="panel panel-warning">
          <p className="panel-text">Das referenzierte Feld konnte im aktuellen Prozesstyp nicht gefunden werden.</p>
        </div>
      ) : null}

      {selectedDefinition ? (
        <>
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
                value={management.draft.answerKey}
                onChange={(event) => management.updateDraft("answerKey", event.target.value)}
              />
            </label>
            <label>
              <span className="form-label">Titel</span>
              <input
                className="form-input"
                value={management.draft.title}
                onChange={(event) => management.updateDraft("title", event.target.value)}
              />
            </label>
            <label>
              <span className="form-label">Kategorie</span>
              <input
                className="form-input"
                value={management.draft.category}
                onChange={(event) => management.updateDraft("category", event.target.value)}
              />
            </label>
            <label>
              <span className="form-label">Icon-Key</span>
              <input
                className="form-input"
                value={management.draft.iconKey}
                onChange={(event) => management.updateDraft("iconKey", event.target.value)}
              />
            </label>
            <label>
              <span className="form-label">Input-Typ</span>
              <select
                className="form-select"
                value={management.draft.inputType}
                onChange={(event) =>
                  management.updateDraft("inputType", event.target.value as "boolean" | "text" | "select" | "multi_select")
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
                value={management.draft.sortOrder}
                onChange={(event) => management.updateDraft("sortOrder", event.target.value)}
              />
            </label>
          </div>

          <label>
            <span className="form-label">Beschreibung</span>
            <textarea
              className="form-input"
              rows={4}
              value={management.draft.description}
              onChange={(event) => management.updateDraft("description", event.target.value)}
            />
          </label>

          <div style={{ display: "flex", gap: "1.5rem", flexWrap: "wrap" }}>
            <label style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
              <input
                type="checkbox"
                checked={management.draft.isRequired}
                onChange={(event) => management.updateDraft("isRequired", event.target.checked)}
              />
              <span>Pflichtfeld</span>
            </label>
            <label style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
              <input
                type="checkbox"
                checked={management.draft.isActive}
                onChange={(event) => management.updateDraft("isActive", event.target.checked)}
              />
              <span>Aktiv</span>
            </label>
          </div>

          <div style={{ display: "flex", gap: "0.75rem", flexWrap: "wrap" }}>
            <button
              type="button"
              className="btn btn-primary"
              disabled={management.isSaving}
              onClick={() => void management.saveDefinition()}
            >
              {management.isSaving ? "Wird gespeichert..." : "Änderungen speichern"}
            </button>
            <button
              type="button"
              className="btn btn-outline"
              disabled={management.isDeleting}
              onClick={() => void management.removeDefinition()}
            >
              {management.isDeleting ? "Wird gelöscht..." : "Feld löschen"}
            </button>
          </div>
        </>
      ) : null}
    </div>
  );
}

function BuilderFocusedTaskTemplateEditor({
  focus,
  responsibilityOwners,
  onNotice,
  onError,
  onDataChanged,
}: {
  focus: BuilderInspectorFocusTarget;
  responsibilityOwners: AdminResponsibilityOwner[];
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
  onDataChanged: () => void | Promise<void>;
}) {
  const management = useAdminTaskTemplateManagement({
    onNotice: (message) => {
      onNotice(message);
      if (message) {
        void onDataChanged();
      }
    },
    onError,
  });
  const [departments, setDepartments] = useState<AdminDepartmentAssignment[]>([]);
  const [isLoadingDepartments, setIsLoadingDepartments] = useState(true);
  const [activeSection, setActiveSection] = useState<Exclude<BuilderInspectorFocusSection, null>>(
    focus.templateSection ?? "details"
  );

  useEffect(() => {
    setActiveSection(focus.templateSection ?? "details");
  }, [focus.templateSection]);

  useEffect(() => {
    let isCancelled = false;
    setIsLoadingDepartments(true);
    getAdminDepartmentAssignments()
      .then((loadedDepartments) => {
        if (!isCancelled) {
          setDepartments(loadedDepartments);
        }
      })
      .catch(() => {
        if (!isCancelled) {
          setDepartments([]);
        }
      })
      .finally(() => {
        if (!isCancelled) {
          setIsLoadingDepartments(false);
        }
      });

    return () => {
      isCancelled = true;
    };
  }, []);

  useEffect(() => {
    if (focus.processTypeId && management.selectedProcessTypeId !== focus.processTypeId) {
      management.selectProcessType(String(focus.processTypeId));
    }
  }, [focus.processTypeId, management]);

  useEffect(() => {
    if (!focus.templateId || management.templates.length === 0) {
      return;
    }

    const matchingTemplate = management.templates.find((template) => template.id === focus.templateId) ?? null;
    if (matchingTemplate && management.selectedTemplate?.id !== matchingTemplate.id) {
      management.selectTemplate(matchingTemplate);
    }
  }, [focus.templateId, management]);

  const selectedTemplate = management.selectedTemplate;

  return (
    <div className="content-stack">
      <div className="panel panel-muted content-stack" style={{ gap: "0.35rem" }}>
        <span className="badge badge--default">Quelle</span>
        <strong>{selectedTemplate?.title ?? "Aufgabenvorlage wird geladen"}</strong>
        <span className="text-muted">
          {selectedTemplate
            ? `${selectedTemplate.category} | Bedingungen ${selectedTemplate.conditionCount} | Abhängigkeiten ${selectedTemplate.dependencyCount}`
            : "Die referenzierte Vorlage wird im gewählten Prozesstyp gesucht."}
        </span>
      </div>

      <div className="admin-entity-switcher" role="tablist" aria-label="Quellenbereich innerhalb der Vorlage wechseln">
        {[
          { key: "details" as const, label: "Vorlage" },
          { key: "conditions" as const, label: "Bedingungen" },
          { key: "dependencies" as const, label: "Abhängigkeiten" },
        ].map((section) => (
          <button
            key={section.key}
            type="button"
            role="tab"
            className={`admin-entity-switch ${activeSection === section.key ? "active" : ""}`}
            aria-selected={activeSection === section.key}
            onClick={() => setActiveSection(section.key)}
          >
            {section.label}
          </button>
        ))}
      </div>

      {management.isLoadingTemplates || isLoadingDepartments ? <p className="text-muted">Vorlage wird geladen...</p> : null}
      {!management.isLoadingTemplates && !selectedTemplate ? (
        <div className="panel panel-warning">
          <p className="panel-text">Die referenzierte Aufgabenvorlage konnte im aktuellen Prozesstyp nicht gefunden werden.</p>
        </div>
      ) : null}

      {selectedTemplate && activeSection === "details" ? (
        <AdminTaskTemplateEditor
          panelTitle={`Aufgabenvorlage bearbeiten: ${selectedTemplate.title}`}
          selectedProcessTypeId={management.selectedProcessTypeId}
          selectedTemplate={selectedTemplate}
          isCreatingNew={management.isCreatingNew}
          draft={management.draft}
          departments={departments}
          responsibilities={responsibilityOwners}
          isSaving={management.isSaving}
          isDeleting={management.isDeleting}
          onUpdateDraft={management.updateDraft}
          onCreateTemplate={management.createTemplate}
          onSaveTemplate={management.saveTemplate}
          onRemoveTemplate={management.removeTemplate}
        />
      ) : null}

      {selectedTemplate && activeSection === "conditions" ? (
        <AdminTaskTemplateConditionsPanel
          groupedConditions={management.groupedConditions}
          conditionDraft={management.conditionDraft}
          answerDefinitions={management.answerDefinitions}
          isLoadingConditions={management.isLoadingConditions}
          isSavingCondition={management.isSavingCondition}
          deletingConditionId={management.deletingConditionId}
          onUpdateConditionDraft={management.updateConditionDraft}
          onAddConditionGroup={management.addConditionGroup}
          onAddCondition={management.addCondition}
          onRemoveCondition={management.removeCondition}
        />
      ) : null}

      {selectedTemplate && activeSection === "dependencies" ? (
        <AdminTaskTemplateDependenciesPanel
          selectedTemplate={selectedTemplate}
          templates={management.templates}
          dependencyGraph={management.dependencyGraph}
          dependencies={management.dependencies}
          isLoadingDependencyGraph={management.isLoadingDependencyGraph}
          isLoadingTemplates={management.isLoadingTemplates}
          isLoadingDependencies={management.isLoadingDependencies}
          isSavingDependency={management.isSavingDependency}
          deletingDependencyId={management.deletingDependencyId}
          onSelectTemplate={() => {
            // Im Builder bleibt der Fokus bewusst auf der ursprünglich gewählten Quelle.
          }}
          onCreateDependencyFromGraph={management.createDependencyFromGraph}
          onRemoveDependencyFromGraph={management.removeDependencyFromGraph}
          onRemoveDependency={management.removeDependency}
        />
      ) : null}
    </div>
  );
}
