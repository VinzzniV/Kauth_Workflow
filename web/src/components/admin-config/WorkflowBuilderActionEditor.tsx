import { useState } from "react";
import { ChevronDown, ChevronUp, X } from "lucide-react";
import type {
  AdminAnswerDefinition,
  AdminWorkflowActionDefinition,
} from "../../types/auth";
import type { AdminAutomationPropertyCatalog } from "../../services/adminConfigApi";
import type {
  WorkflowBuilderActionDraft,
  WorkflowBuilderNodeDraft,
} from "../../hooks/adminWorkflowBuilderModel";
import { WorkflowBuilderActionMappingEditor } from "./WorkflowBuilderActionMappingEditor";

export type WorkflowBuilderActionEditorProps = {
  node: WorkflowBuilderNodeDraft;
  actionDefinitions: AdminWorkflowActionDefinition[];
  automationPropertyCatalog: AdminAutomationPropertyCatalog | null;
  answerDefinitions: AdminAnswerDefinition[];
  canManageAdvanced: boolean;
  onAddAction: (actionKey: string) => void;
  onUpdateAction: (actionId: string, patch: Partial<WorkflowBuilderActionDraft>) => void;
  onRemoveAction: (actionId: string) => void;
};

export function WorkflowBuilderActionEditor({
  node,
  actionDefinitions,
  automationPropertyCatalog,
  answerDefinitions,
  canManageAdvanced,
  onAddAction,
  onUpdateAction,
  onRemoveAction,
}: WorkflowBuilderActionEditorProps) {
  const [addOpen, setAddOpen] = useState(false);
  const sortedActions = [...node.actions].sort((a, b) => {
    const ao = Number(a.executionOrder);
    const bo = Number(b.executionOrder);
    return (Number.isFinite(ao) ? ao : 999) - (Number.isFinite(bo) ? bo : 999);
  });

  const moveAction = (actionId: string, direction: "up" | "down") => {
    const index = sortedActions.findIndex((a) => a.id === actionId);
    if (index < 0) return;
    const targetIndex = direction === "up" ? index - 1 : index + 1;
    if (targetIndex < 0 || targetIndex >= sortedActions.length) return;
    const a = sortedActions[index]!;
    const b = sortedActions[targetIndex]!;
    onUpdateAction(a.id, { executionOrder: b.executionOrder });
    onUpdateAction(b.id, { executionOrder: a.executionOrder });
  };

  return (
    <div className="wf-action-editor">
      <div className="wf-action-editor-head">
        <strong>Aktionen ({sortedActions.length})</strong>
        <p className="wf-form-field-hint">
          Bei Fehler wird der Workflow abgebrochen (Standardverhalten).
        </p>
      </div>

      {sortedActions.length === 0 ? (
        <p className="wf-step-card-hint wf-step-card-hint--info">
          Noch keine Aktion definiert. Über „+ Aktion hinzufügen" eine aus dem Katalog wählen.
        </p>
      ) : (
        <ol className="wf-action-list">
          {sortedActions.map((action, index) => {
            const def = actionDefinitions.find(
              (d) => d.actionKey.trim().toLowerCase() === action.actionKey.trim().toLowerCase()
            ) ?? null;
            const isFirst = index === 0;
            const isLast = index === sortedActions.length - 1;

            return (
              <li key={action.id} className="wf-action-item">
                <div className="wf-action-item-head">
                  <span className="wf-action-item-num">{index + 1}.</span>
                  <select
                    className="form-select wf-action-item-key"
                    value={action.actionKey}
                    onChange={(e) => onUpdateAction(action.id, { actionKey: e.target.value })}
                    disabled={!canManageAdvanced}
                    aria-label="Aktion auswählen"
                  >
                    <option value="">– Aktion wählen –</option>
                    {actionDefinitions.map((d) => (
                      <option key={d.actionKey} value={d.actionKey}>
                        {d.displayName}{d.isActive ? "" : " (inaktiv)"}
                      </option>
                    ))}
                  </select>
                  <div className="wf-action-item-actions">
                    <button
                      type="button"
                      className="wf-step-card-iconbtn"
                      onClick={() => moveAction(action.id, "up")}
                      disabled={isFirst || !canManageAdvanced}
                      aria-label="Nach oben"
                    ><ChevronUp size={16} aria-hidden="true" /></button>
                    <button
                      type="button"
                      className="wf-step-card-iconbtn"
                      onClick={() => moveAction(action.id, "down")}
                      disabled={isLast || !canManageAdvanced}
                      aria-label="Nach unten"
                    ><ChevronDown size={16} aria-hidden="true" /></button>
                    <button
                      type="button"
                      className="wf-step-card-iconbtn wf-step-card-iconbtn--danger"
                      onClick={() => onRemoveAction(action.id)}
                      disabled={!canManageAdvanced}
                      aria-label="Aktion entfernen"
                    ><X size={16} aria-hidden="true" /></button>
                  </div>
                </div>

                {def?.description && (
                  <p className="wf-action-item-desc">{def.description}</p>
                )}

                <div className="wf-action-item-mapping">
                  <span className="form-label">Eingabe-Mapping</span>
                  <WorkflowBuilderActionMappingEditor
                    actionKey={action.actionKey}
                    actionDefinitions={actionDefinitions}
                    propertyCatalog={automationPropertyCatalog}
                    inputMappingText={action.inputMappingText}
                    answerDefinitions={answerDefinitions}
                    onChange={(next) => onUpdateAction(action.id, { inputMappingText: next })}
                    disabled={!canManageAdvanced}
                  />
                </div>
              </li>
            );
          })}
        </ol>
      )}

      <div className="wf-action-add">
        <div className="wf-step-add-dropdown">
          <button
            type="button"
            className="btn-secondary"
            onClick={() => setAddOpen((v) => !v)}
            aria-expanded={addOpen}
            disabled={!canManageAdvanced || actionDefinitions.length === 0}
          >
            <span>+ Aktion hinzufügen</span>
            <ChevronDown size={14} aria-hidden="true" />
          </button>
          {addOpen && (
            <div className="wf-step-add-menu" role="menu" style={{ maxHeight: "280px", overflowY: "auto" }}>
              {actionDefinitions.length === 0 ? (
                <p className="wf-step-add-menu-hint">Aktionskatalog ist leer.</p>
              ) : (
                actionDefinitions.map((d) => (
                  <button
                    key={d.actionKey}
                    type="button"
                    className="wf-step-add-menu-item"
                    role="menuitem"
                    disabled={!d.isActive}
                    onClick={() => {
                      onAddAction(d.actionKey);
                      setAddOpen(false);
                    }}
                  >
                    {d.displayName}{d.isActive ? "" : " (inaktiv)"}
                  </button>
                ))
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
