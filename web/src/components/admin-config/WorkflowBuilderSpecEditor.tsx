import { useState } from "react";
import { ChevronDown, ChevronRight, Plus, X } from "lucide-react";
import type {
  AdminAnswerDefinition,
  AdminResponsibilityOwner,
  AdminWorkflowDefinitionNodeSpec,
  AdminWorkflowDefinitionNodeSpecCondition,
  AdminWorkflowDefinitionNodeSpecDependency,
} from "../../types/auth";

const VALUE_OPERATORS = new Set<string>(["eq", "neq"]);

const OPERATORS: { key: AdminWorkflowDefinitionNodeSpecCondition["operator"]; label: string }[] = [
  { key: "eq", label: "gleich (=)" },
  { key: "neq", label: "ungleich (≠)" },
  { key: "is_true", label: "ist wahr" },
  { key: "is_false", label: "ist falsch" },
  { key: "is_null", label: "ist leer" },
  { key: "is_not_null", label: "ist vorhanden" },
];

function createEmptySpec(sortOrder: number): AdminWorkflowDefinitionNodeSpec {
  return {
    specKey: "",
    title: "",
    category: "",
    description: "",
    iconKey: null,
    defaultResponsibilityId: null,
    processAreaLabel: null,
    isDepartmentPhaseTask: false,
    isRequired: false,
    dueInDays: null,
    sortOrder,
    conditions: [],
    dependencies: [],
  };
}

function createEmptyCondition(): AdminWorkflowDefinitionNodeSpecCondition {
  return {
    answerKey: "",
    operator: "eq",
    expectedValueText: null,
    expectedValueBoolean: null,
    expectedValueNumber: null,
  };
}

type SpecRowProps = {
  spec: AdminWorkflowDefinitionNodeSpec;
  specIndex: number;
  allSpecs: AdminWorkflowDefinitionNodeSpec[];
  answerDefinitions: AdminAnswerDefinition[];
  responsibilityOwners: AdminResponsibilityOwner[];
  canManageAdvanced: boolean;
  onUpdate: (updated: AdminWorkflowDefinitionNodeSpec) => void;
  onRemove: () => void;
};

function SpecRow({
  spec,
  specIndex,
  allSpecs,
  answerDefinitions,
  responsibilityOwners,
  canManageAdvanced,
  onUpdate,
  onRemove,
}: SpecRowProps) {
  const [open, setOpen] = useState(false);

  const otherSpecKeys = allSpecs
    .map((s, i) => (i !== specIndex && s.specKey.trim() ? s.specKey.trim() : null))
    .filter((k): k is string => k !== null);

  const updateField = <K extends keyof AdminWorkflowDefinitionNodeSpec>(
    key: K,
    value: AdminWorkflowDefinitionNodeSpec[K]
  ) => {
    onUpdate({ ...spec, [key]: value });
  };

  const updateCondition = (idx: number, patch: Partial<AdminWorkflowDefinitionNodeSpecCondition>) => {
    onUpdate({ ...spec, conditions: spec.conditions.map((c, i) => (i === idx ? { ...c, ...patch } : c)) });
  };

  const removeCondition = (idx: number) => {
    onUpdate({ ...spec, conditions: spec.conditions.filter((_, i) => i !== idx) });
  };

  const addCondition = () => {
    onUpdate({ ...spec, conditions: [...spec.conditions, createEmptyCondition()] });
  };

  const updateDependency = (idx: number, dependsOnSpecKey: string) => {
    onUpdate({
      ...spec,
      dependencies: spec.dependencies.map((d, i): AdminWorkflowDefinitionNodeSpecDependency =>
        i === idx ? { ...d, dependsOnSpecKey } : d
      ),
    });
  };

  const removeDependency = (idx: number) => {
    onUpdate({ ...spec, dependencies: spec.dependencies.filter((_, i) => i !== idx) });
  };

  const addDependency = () => {
    if (otherSpecKeys.length === 0) return;
    const newDep: AdminWorkflowDefinitionNodeSpecDependency = { dependsOnSpecKey: otherSpecKeys[0] };
    onUpdate({ ...spec, dependencies: [...spec.dependencies, newDep] });
  };

  return (
    <div className="wf-spec-row">
      <div className="wf-spec-row-head">
        <button
          type="button"
          className="wf-spec-row-toggle"
          onClick={() => setOpen((v) => !v)}
          aria-expanded={open}
        >
          {open ? <ChevronDown size={14} aria-hidden="true" /> : <ChevronRight size={14} aria-hidden="true" />}
          <span className="wf-spec-row-title">
            {spec.title.trim() || spec.specKey.trim() || `Spec ${specIndex + 1}`}
          </span>
          {spec.category.trim() ? <span className="chip">{spec.category}</span> : null}
          <span className={`badge ${spec.isRequired ? "badge-warning" : "badge-neutral"}`}>
            {spec.isRequired ? "Pflicht" : "Optional"}
          </span>
        </button>
        {canManageAdvanced ? (
          <button
            type="button"
            className="wf-step-card-iconbtn wf-step-card-iconbtn--danger"
            onClick={onRemove}
            aria-label="Spec entfernen"
          >
            <X size={14} aria-hidden="true" />
          </button>
        ) : null}
      </div>

      {open ? (
        <div className="wf-spec-row-body">
          <div className="wf-form-fields">
            <div className="wf-form-field">
              <label className="form-label">
                Spec-Key <span className="text-error">*</span>
              </label>
              <input
                className="form-input"
                type="text"
                value={spec.specKey}
                onChange={(e) => updateField("specKey", e.target.value)}
                placeholder="z. B. provision_it"
                disabled={!canManageAdvanced}
              />
            </div>
            <div className="wf-form-field">
              <label className="form-label">
                Bezeichnung <span className="text-error">*</span>
              </label>
              <input
                className="form-input"
                type="text"
                value={spec.title}
                onChange={(e) => updateField("title", e.target.value)}
                placeholder="z. B. IT-Zugang bereitstellen"
                disabled={!canManageAdvanced}
              />
            </div>
            <div className="wf-form-field">
              <label className="form-label">Kategorie</label>
              <input
                className="form-input"
                type="text"
                value={spec.category}
                onChange={(e) => updateField("category", e.target.value)}
                placeholder="z. B. IT"
                disabled={!canManageAdvanced}
              />
            </div>
            <div className="wf-form-field">
              <label className="form-label">Beschreibung</label>
              <textarea
                className="form-textarea"
                rows={2}
                value={spec.description}
                onChange={(e) => updateField("description", e.target.value)}
                disabled={!canManageAdvanced}
              />
            </div>
            <div className="wf-spec-inline-fields">
              <div className="wf-form-field">
                <label className="form-label">Fällig in Tagen</label>
                <input
                  className="form-input"
                  type="number"
                  min={1}
                  value={spec.dueInDays ?? ""}
                  onChange={(e) =>
                    updateField("dueInDays", e.target.value ? Number(e.target.value) : null)
                  }
                  disabled={!canManageAdvanced}
                />
              </div>
              <div className="wf-form-field">
                <label className="form-label">Sortierung</label>
                <input
                  className="form-input"
                  type="number"
                  min={1}
                  value={spec.sortOrder}
                  onChange={(e) => updateField("sortOrder", Number(e.target.value) || 1)}
                  disabled={!canManageAdvanced}
                />
              </div>
            </div>
            <div className="wf-spec-checkboxes">
              <label className="wf-spec-checkbox-label">
                <input
                  type="checkbox"
                  checked={spec.isRequired}
                  onChange={(e) => updateField("isRequired", e.target.checked)}
                  disabled={!canManageAdvanced}
                />
                <span>Pflicht</span>
              </label>
              <label className="wf-spec-checkbox-label">
                <input
                  type="checkbox"
                  checked={spec.isDepartmentPhaseTask}
                  onChange={(e) => updateField("isDepartmentPhaseTask", e.target.checked)}
                  disabled={!canManageAdvanced}
                />
                <span>Abteilungs-Phasenaufgabe</span>
              </label>
            </div>
            <div className="wf-form-field">
              <label className="form-label">Zuständigkeit (Standard)</label>
              <select
                className="form-select"
                value={spec.defaultResponsibilityId ?? ""}
                onChange={(e) =>
                  updateField("defaultResponsibilityId", e.target.value ? Number(e.target.value) : null)
                }
                disabled={!canManageAdvanced}
              >
                <option value="">– keine –</option>
                {responsibilityOwners.map((r) => (
                  <option key={r.responsibilityId} value={r.responsibilityId}>
                    {r.responsibilityName}
                  </option>
                ))}
              </select>
            </div>
            <div className="wf-form-field">
              <label className="form-label">Prozessbereich-Label</label>
              <input
                className="form-input"
                type="text"
                value={spec.processAreaLabel ?? ""}
                onChange={(e) => updateField("processAreaLabel", e.target.value || null)}
                disabled={!canManageAdvanced}
              />
            </div>
          </div>

          <div className="wf-spec-subsection">
            <div className="wf-spec-subsection-head">
              <span className="wf-spec-subsection-label">
                Bedingungen ({spec.conditions.length})
              </span>
              {canManageAdvanced ? (
                <button
                  type="button"
                  className="btn btn-ghost wf-spec-subsection-add"
                  onClick={addCondition}
                >
                  <Plus size={12} aria-hidden="true" />
                  Bedingung
                </button>
              ) : null}
            </div>
            {spec.conditions.length === 0 ? (
              <p className="wf-form-field-hint">Keine Bedingungen — Spec gilt immer.</p>
            ) : (
              <div className="wf-spec-conditions">
                {spec.conditions.map((condition, cIdx) => {
                  const needsValue = VALUE_OPERATORS.has(condition.operator);
                  return (
                    <div key={cIdx} className="wf-spec-condition-row">
                      <select
                        className="form-select"
                        value={condition.answerKey}
                        onChange={(e) => updateCondition(cIdx, { answerKey: e.target.value })}
                        disabled={!canManageAdvanced}
                        aria-label="Antwort-Key"
                      >
                        <option value="">– Antwort wählen –</option>
                        {answerDefinitions.map((a) => (
                          <option key={a.answerKey} value={a.answerKey}>
                            {a.title || a.answerKey}
                          </option>
                        ))}
                      </select>
                      <select
                        className="form-select"
                        value={condition.operator}
                        onChange={(e) =>
                          updateCondition(cIdx, {
                            operator: e.target.value as AdminWorkflowDefinitionNodeSpecCondition["operator"],
                            expectedValueText: null,
                            expectedValueBoolean: null,
                            expectedValueNumber: null,
                          })
                        }
                        disabled={!canManageAdvanced}
                        aria-label="Operator"
                      >
                        {OPERATORS.map((op) => (
                          <option key={op.key} value={op.key}>
                            {op.label}
                          </option>
                        ))}
                      </select>
                      {needsValue ? (
                        <input
                          className="form-input"
                          type="text"
                          value={
                            condition.expectedValueText ??
                            condition.expectedValueNumber?.toString() ??
                            ""
                          }
                          onChange={(e) =>
                            updateCondition(cIdx, { expectedValueText: e.target.value || null })
                          }
                          placeholder="Erwarteter Wert"
                          disabled={!canManageAdvanced}
                          aria-label="Erwarteter Wert"
                        />
                      ) : (
                        <span className="wf-spec-condition-noval text-secondary">—</span>
                      )}
                      {canManageAdvanced ? (
                        <button
                          type="button"
                          className="wf-step-card-iconbtn wf-step-card-iconbtn--danger"
                          onClick={() => removeCondition(cIdx)}
                          aria-label="Bedingung entfernen"
                        >
                          <X size={12} aria-hidden="true" />
                        </button>
                      ) : null}
                    </div>
                  );
                })}
              </div>
            )}
          </div>

          <div className="wf-spec-subsection">
            <div className="wf-spec-subsection-head">
              <span className="wf-spec-subsection-label">
                Abhängigkeiten ({spec.dependencies.length})
              </span>
              {canManageAdvanced && otherSpecKeys.length > 0 ? (
                <button
                  type="button"
                  className="btn btn-ghost wf-spec-subsection-add"
                  onClick={addDependency}
                >
                  <Plus size={12} aria-hidden="true" />
                  Abhängigkeit
                </button>
              ) : null}
            </div>
            {spec.dependencies.length === 0 ? (
              <p className="wf-form-field-hint">Keine Abhängigkeiten.</p>
            ) : (
              <div className="wf-spec-deps">
                {spec.dependencies.map((dep, dIdx) => (
                  <div key={dIdx} className="wf-spec-dep-row">
                    <span className="wf-spec-dep-label text-secondary">hängt ab von</span>
                    <select
                      className="form-select"
                      value={dep.dependsOnSpecKey}
                      onChange={(e) => updateDependency(dIdx, e.target.value)}
                      disabled={!canManageAdvanced}
                      aria-label="Abhängt von Spec"
                    >
                      <option value="">– Spec wählen –</option>
                      {otherSpecKeys.map((key) => (
                        <option key={key} value={key}>
                          {key}
                        </option>
                      ))}
                    </select>
                    {canManageAdvanced ? (
                      <button
                        type="button"
                        className="wf-step-card-iconbtn wf-step-card-iconbtn--danger"
                        onClick={() => removeDependency(dIdx)}
                        aria-label="Abhängigkeit entfernen"
                      >
                        <X size={12} aria-hidden="true" />
                      </button>
                    ) : null}
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      ) : null}
    </div>
  );
}

type WorkflowBuilderSpecEditorProps = {
  specs: AdminWorkflowDefinitionNodeSpec[];
  answerDefinitions: AdminAnswerDefinition[];
  responsibilityOwners: AdminResponsibilityOwner[];
  canManageAdvanced: boolean;
  onChangeSpecs: (specs: AdminWorkflowDefinitionNodeSpec[]) => void;
  onClose: () => void;
  nodeName: string;
};

export function WorkflowBuilderSpecEditor({
  specs,
  answerDefinitions,
  responsibilityOwners,
  canManageAdvanced,
  onChangeSpecs,
  onClose,
  nodeName,
}: WorkflowBuilderSpecEditorProps) {
  const addSpec = () => {
    const maxSortOrder = specs.reduce((m, s) => Math.max(m, s.sortOrder), 0);
    onChangeSpecs([...specs, createEmptySpec(maxSortOrder + 10)]);
  };

  const updateSpec = (index: number, updated: AdminWorkflowDefinitionNodeSpec) => {
    onChangeSpecs(specs.map((s, i) => (i === index ? updated : s)));
  };

  const removeSpec = (index: number) => {
    onChangeSpecs(specs.filter((_, i) => i !== index));
  };

  return (
    <>
      <div className="admin-drawer-backdrop" onClick={onClose} aria-hidden="true" />
      <aside
        className="admin-drawer wf-spec-drawer"
        role="dialog"
        aria-modal="true"
        aria-label="Aufgaben-Specs bearbeiten"
      >
        <header className="admin-drawer-head">
          <div className="admin-drawer-title">
            <h2>Aufgaben-Specs</h2>
            <span className="wf-condition-drawer-route">
              <strong>{nodeName}</strong>
            </span>
          </div>
          <button type="button" className="admin-drawer-close" onClick={onClose} aria-label="Schließen">
            ×
          </button>
        </header>

        <div className="admin-drawer-body content-stack">
          <p className="wf-form-field-hint">
            Specs sind die versionierten Aufgaben-Maßnahmen dieses Schritts. Änderungen gelten erst
            nach dem Speichern des Entwurfs.
          </p>

          {specs.length === 0 ? (
            <p className="wf-step-card-hint wf-step-card-hint--info">Noch keine Specs definiert.</p>
          ) : (
            <div className="wf-spec-list">
              {specs.map((spec, idx) => (
                <SpecRow
                  key={idx}
                  spec={spec}
                  specIndex={idx}
                  allSpecs={specs}
                  answerDefinitions={answerDefinitions}
                  responsibilityOwners={responsibilityOwners}
                  canManageAdvanced={canManageAdvanced}
                  onUpdate={(updated) => updateSpec(idx, updated)}
                  onRemove={() => removeSpec(idx)}
                />
              ))}
            </div>
          )}

          {canManageAdvanced ? (
            <button type="button" className="btn btn-secondary" onClick={addSpec}>
              <Plus size={14} aria-hidden="true" />
              <span>Spec hinzufügen</span>
            </button>
          ) : null}
        </div>
      </aside>
    </>
  );
}
