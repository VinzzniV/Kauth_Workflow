import type {
  AdminAnswerDefinition,
  AdminResponsibilityOwner,
  AdminTaskTemplate,
} from "../../types/auth";
import type { WorkflowBuilderNodeDraft } from "../../hooks/adminWorkflowBuilderModel";
import { parseConfig, writeString } from "./workflowBuilderEditorHelpers";

export type WorkflowBuilderStepConfigEditorProps = {
  node: WorkflowBuilderNodeDraft;
  responsibilityOwners: AdminResponsibilityOwner[];
  taskTemplates: AdminTaskTemplate[];
  answerDefinitions: AdminAnswerDefinition[];
  onUpdate: (patch: Partial<WorkflowBuilderNodeDraft>) => void;
};

export function WorkflowBuilderStepConfigEditor({
  node,
  responsibilityOwners,
  onUpdate,
  answerDefinitions,
}: WorkflowBuilderStepConfigEditorProps) {
  const config = parseConfig(node.configText);
  const responsibilityKey = readString(config, "responsibilityKey");
  const notificationLabel = readString(config, "notificationLabel");
  const summaryText = readString(config, "summaryText");

  const setField = (key: string, value: string) => {
    onUpdate({ configText: writeString(node.configText, key, value) });
  };

  const sortedResponsibilities = [...responsibilityOwners].sort((l, r) =>
    l.responsibilityName.localeCompare(r.responsibilityName, "de")
  );

  return (
    <div className="wf-step-config">
      <div className="wf-form-field">
        <label className="form-label" htmlFor={`step-resp-${node.id}`}>Zuständig</label>
        <select
          id={`step-resp-${node.id}`}
          className="form-select"
          value={responsibilityKey}
          onChange={(e) => setField("responsibilityKey", e.target.value)}
        >
          <option value="">– bitte wählen –</option>
          {sortedResponsibilities.map((r) => (
            <option key={r.responsibilityId} value={r.responsibilityKey}>
              {r.responsibilityName}
              {r.departmentName ? ` (${r.departmentName})` : ""}
            </option>
          ))}
        </select>
      </div>

      {(node.nodeType === "approval" || node.nodeType === "task") && (
        <div className="wf-form-field">
          <p className="wf-form-field-hint">
            LA5: Aufgabe/Freigabe-Spezifikation wird im Admin-Bereich „Task-Templates"
            am Massnahmen-Node der Definition gepflegt — kein eigener Konfig-Eintrag mehr im Builder.
          </p>
        </div>
      )}

      {node.nodeType === "form" && answerDefinitions.length > 0 && (
        <div className="wf-form-field">
          <p className="wf-form-field-hint">
            Formular nutzt {answerDefinitions.length} Antwort-Definition(en) aus dem Prozess. Felder werden
            in der Antwort-Definitionen-Verwaltung gepflegt, nicht hier.
          </p>
        </div>
      )}

      <div className="wf-form-field">
        <label className="form-label" htmlFor={`step-notif-${node.id}`}>Benachrichtigte (optional)</label>
        <input
          id={`step-notif-${node.id}`}
          className="form-input"
          type="text"
          value={notificationLabel}
          onChange={(e) => setField("notificationLabel", e.target.value)}
          placeholder="z. B. HR-Team"
        />
      </div>

      <div className="wf-form-field">
        <label className="form-label" htmlFor={`step-summary-${node.id}`}>Kurzbeschreibung (optional)</label>
        <textarea
          id={`step-summary-${node.id}`}
          className="form-textarea"
          rows={2}
          value={summaryText}
          onChange={(e) => setField("summaryText", e.target.value)}
          placeholder="Was passiert in diesem Schritt?"
        />
      </div>
    </div>
  );
}

function readString(config: Record<string, unknown> | null, key: string): string {
  if (!config) return "";
  const value = config[key];
  return typeof value === "string" ? value : "";
}
