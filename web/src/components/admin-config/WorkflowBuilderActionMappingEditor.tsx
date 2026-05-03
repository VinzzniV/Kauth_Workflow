import { useMemo } from "react";
import { X } from "lucide-react";
import type { AdminAnswerDefinition, AdminWorkflowActionDefinition } from "../../types/auth";
import type { AdminAutomationPropertyCatalog } from "../../services/adminConfigApi";
import {
  entryToJson,
  parseMapping,
  readEntry,
  stringifyMapping,
  type MappingEntry,
  type MappingSource,
} from "./workflowBuilderEditorHelpers";

const SOURCE_OPTIONS: { value: MappingSource; label: string }[] = [
  { value: "static", label: "Fester Wert" },
  { value: "workflow", label: "Workflow-Feld" },
  { value: "target_person", label: "Person (Ziel)" },
  { value: "directory_identity", label: "Verzeichnis-Identität" },
  { value: "answer", label: "Antwort aus Formular" },
];

// Fallback-Listen, falls der `/admin/config/automation-property-catalog`-Endpoint
// (LQ1) nicht erreichbar ist. Source-of-truth ist immer der Backend-Resolver in
// PostgresWorkflowAutomationOperations.ResolveAutomationReference. Die Listen hier
// werden nur genutzt, wenn `propertyCatalog` null ist.
const FALLBACK_WORKFLOW_PROPERTIES = [
  "workflowId", "workflowUid", "definitionKey", "departmentId", "roleId",
  "firstName", "lastName", "employeeNumber", "badgeNumber", "deadlineDate", "targetPersonId",
];

const FALLBACK_PERSON_PROPERTIES = [
  "personId", "departmentId", "roleId", "appUserId", "directoryIdentityId",
  "firstName", "lastName", "employeeNumber", "badgeNumber",
  "employmentStatus", "entryDate", "exitDate", "displayName", "email",
];

const FALLBACK_DIRECTORY_PROPERTIES = [
  "directoryIdentityId", "userPrincipalName", "mail", "displayName",
  "departmentName", "employeeNumber", "accountEnabled",
];

function resolveProperties(
  catalog: AdminAutomationPropertyCatalog | null,
  source: string,
  fallback: string[],
): string[] {
  const found = catalog?.sources.find((s) => s.source === source)?.properties;
  return found && found.length > 0 ? found : fallback;
}

export type WorkflowBuilderActionMappingEditorProps = {
  actionKey: string;
  actionDefinitions: AdminWorkflowActionDefinition[];
  propertyCatalog: AdminAutomationPropertyCatalog | null;
  inputMappingText: string;
  answerDefinitions: AdminAnswerDefinition[];
  onChange: (nextInputMappingText: string) => void;
  disabled?: boolean;
};

export function WorkflowBuilderActionMappingEditor({
  actionKey,
  actionDefinitions,
  propertyCatalog,
  inputMappingText,
  answerDefinitions,
  onChange,
  disabled,
}: WorkflowBuilderActionMappingEditorProps) {
  const workflowProperties = useMemo(
    () => resolveProperties(propertyCatalog, "workflow", FALLBACK_WORKFLOW_PROPERTIES),
    [propertyCatalog]
  );
  const personProperties = useMemo(
    () => resolveProperties(propertyCatalog, "target_person", FALLBACK_PERSON_PROPERTIES),
    [propertyCatalog]
  );
  const directoryProperties = useMemo(
    () => resolveProperties(propertyCatalog, "directory_identity", FALLBACK_DIRECTORY_PROPERTIES),
    [propertyCatalog]
  );
  const definition = useMemo(
    () => actionDefinitions.find((d) => d.actionKey.trim().toLowerCase() === actionKey.trim().toLowerCase()) ?? null,
    [actionDefinitions, actionKey]
  );

  const parameters = useMemo(() => extractParameterNames(definition?.inputSchema), [definition]);
  const requiredSet = useMemo(() => extractRequiredSet(definition?.inputSchema), [definition]);
  const mapping = useMemo(() => parseMapping(inputMappingText), [inputMappingText]);
  const isMappingValid = mapping !== null;

  const allKnownKeys = useMemo(() => {
    const merged = new Set<string>(parameters);
    if (mapping) {
      for (const k of Object.keys(mapping)) merged.add(k);
    }
    return [...merged];
  }, [parameters, mapping]);

  const handleChange = (paramKey: string, entry: MappingEntry | null) => {
    if (!mapping) {
      // mapping is currently invalid raw JSON — start fresh
      const next = entry ? { [paramKey]: entryToJson(entry) } : {};
      onChange(stringifyMapping(next));
      return;
    }
    const next = { ...mapping };
    if (entry) {
      next[paramKey] = entryToJson(entry);
    } else {
      delete next[paramKey];
    }
    onChange(stringifyMapping(next));
  };

  const handleAddCustomParameter = () => {
    const name = window.prompt("Name des Parameters (frei):");
    if (!name?.trim()) return;
    handleChange(name.trim(), { source: "static", value: "" });
  };

  if (!isMappingValid) {
    return (
      <div className="wf-mapping-fallback">
        <p className="wf-step-card-hint wf-step-card-hint--info">
          Das Eingabe-Mapping ist kein gültiges JSON-Objekt. Form-Builder pausiert — bitte JSON unten korrigieren oder leer machen.
        </p>
        <textarea
          className="form-textarea wf-step-card-configtext"
          rows={4}
          value={inputMappingText}
          onChange={(e) => onChange(e.target.value)}
          spellCheck={false}
          disabled={disabled}
        />
      </div>
    );
  }

  return (
    <div className="wf-mapping-editor">
      {!definition && actionKey.trim() && (
        <p className="wf-step-card-hint wf-step-card-hint--info">
          Diese Action ist im Katalog nicht verfügbar. Form-Builder zeigt nur die bereits gemappten Parameter.
        </p>
      )}

      {allKnownKeys.length === 0 ? (
        <p className="wf-step-card-hint">
          Keine Parameter im Schema definiert. Du kannst eigene Parameter über „+ Parameter hinzufügen" anlegen.
        </p>
      ) : (
        <div className="wf-mapping-param-list">
          {allKnownKeys.map((paramKey) => {
            const isRequired = requiredSet.has(paramKey);
            const entry = readEntry(mapping, paramKey);
            return (
              <ParameterRow
                key={paramKey}
                paramKey={paramKey}
                isRequired={isRequired}
                isInSchema={parameters.includes(paramKey)}
                entry={entry}
                answerDefinitions={answerDefinitions}
                workflowProperties={workflowProperties}
                personProperties={personProperties}
                directoryProperties={directoryProperties}
                onChange={(next) => handleChange(paramKey, next)}
                onRemove={parameters.includes(paramKey) ? null : () => handleChange(paramKey, null)}
                disabled={disabled}
              />
            );
          })}
        </div>
      )}

      <div className="wf-mapping-add-row">
        <button
          type="button"
          className="btn-secondary"
          onClick={handleAddCustomParameter}
          disabled={disabled}
        >
          + Parameter hinzufügen
        </button>
      </div>
    </div>
  );
}

function ParameterRow({
  paramKey,
  isRequired,
  isInSchema,
  entry,
  answerDefinitions,
  workflowProperties,
  personProperties,
  directoryProperties,
  onChange,
  onRemove,
  disabled,
}: {
  paramKey: string;
  isRequired: boolean;
  isInSchema: boolean;
  entry: MappingEntry | null;
  answerDefinitions: AdminAnswerDefinition[];
  workflowProperties: string[];
  personProperties: string[];
  directoryProperties: string[];
  onChange: (next: MappingEntry) => void;
  onRemove: (() => void) | null;
  disabled?: boolean;
}) {
  const source: MappingSource = entry?.source ?? "static";

  const handleSourceChange = (nextSource: MappingSource) => {
    onChange(buildDefaultEntry(nextSource));
  };

  return (
    <div className="wf-mapping-param-row">
      <div className="wf-mapping-param-head">
        <span className="wf-mapping-param-name">
          {paramKey}
          {isRequired && <span className="text-error" title="Pflichtparameter"> *</span>}
          {!isInSchema && <span className="wf-mapping-param-badge"> custom</span>}
        </span>
        {onRemove && (
          <button
            type="button"
            className="wf-step-card-iconbtn wf-step-card-iconbtn--danger"
            onClick={onRemove}
            title="Parameter entfernen"
            aria-label="Parameter entfernen"
            disabled={disabled}
          >
            <X size={16} aria-hidden="true" />
          </button>
        )}
      </div>

      <div className="wf-mapping-param-fields">
        <select
          className="form-select"
          value={source}
          onChange={(e) => handleSourceChange(e.target.value as MappingSource)}
          disabled={disabled}
          aria-label={`Quelle für ${paramKey}`}
        >
          {SOURCE_OPTIONS.map((opt) => (
            <option key={opt.value} value={opt.value}>{opt.label}</option>
          ))}
        </select>

        {source === "static" && (
          <input
            className="form-input"
            type="text"
            value={entry?.source === "static" ? entry.value : ""}
            onChange={(e) => onChange({ source: "static", value: e.target.value })}
            placeholder="Fester Wert"
            disabled={disabled}
          />
        )}

        {source === "workflow" && (
          <PropertyDropdown
            value={entry?.source === "workflow" ? entry.property : ""}
            options={workflowProperties}
            onChange={(v) => onChange({ source: "workflow", property: v })}
            disabled={disabled}
          />
        )}

        {source === "target_person" && (
          <PropertyDropdown
            value={entry?.source === "target_person" ? entry.property : ""}
            options={personProperties}
            onChange={(v) => onChange({ source: "target_person", property: v })}
            disabled={disabled}
          />
        )}

        {source === "directory_identity" && (
          <PropertyDropdown
            value={entry?.source === "directory_identity" ? entry.property : ""}
            options={directoryProperties}
            onChange={(v) => onChange({ source: "directory_identity", property: v })}
            disabled={disabled}
          />
        )}

        {source === "answer" && (
          <select
            className="form-select"
            value={entry?.source === "answer" ? entry.answerKey : ""}
            onChange={(e) => onChange({ source: "answer", answerKey: e.target.value })}
            disabled={disabled}
            aria-label={`Antwort-Key für ${paramKey}`}
          >
            <option value="">– Antwort wählen –</option>
            {answerDefinitions.map((ans) => (
              <option key={ans.id} value={ans.answerKey}>
                {ans.title} ({ans.answerKey})
              </option>
            ))}
          </select>
        )}
      </div>
    </div>
  );
}

function PropertyDropdown({
  value,
  options,
  onChange,
  disabled,
}: {
  value: string;
  options: string[];
  onChange: (v: string) => void;
  disabled?: boolean;
}) {
  return (
    <select
      className="form-select"
      value={value}
      onChange={(e) => onChange(e.target.value)}
      disabled={disabled}
    >
      <option value="">– Feld wählen –</option>
      {options.map((opt) => (
        <option key={opt} value={opt}>{opt}</option>
      ))}
    </select>
  );
}

// ─── helpers ──────────────────────────────────────────────────────────────────

function extractParameterNames(inputSchema: Record<string, unknown> | null | undefined): string[] {
  if (!inputSchema || typeof inputSchema !== "object") return [];
  const props = (inputSchema as { properties?: unknown }).properties;
  if (!props || typeof props !== "object" || Array.isArray(props)) return [];
  return Object.keys(props as Record<string, unknown>);
}

function extractRequiredSet(inputSchema: Record<string, unknown> | null | undefined): Set<string> {
  if (!inputSchema || typeof inputSchema !== "object") return new Set();
  const required = (inputSchema as { required?: unknown }).required;
  if (!Array.isArray(required)) return new Set();
  return new Set(required.filter((r): r is string => typeof r === "string"));
}

function buildDefaultEntry(source: MappingSource): MappingEntry {
  switch (source) {
    case "static": return { source: "static", value: "" };
    case "workflow": return { source: "workflow", property: "" };
    case "target_person": return { source: "target_person", property: "" };
    case "directory_identity": return { source: "directory_identity", property: "" };
    case "answer": return { source: "answer", answerKey: "" };
  }
}
