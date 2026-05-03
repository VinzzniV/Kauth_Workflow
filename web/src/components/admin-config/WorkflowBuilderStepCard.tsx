import { useState } from "react";
import { ChevronDown, ChevronRight, ChevronUp, X } from "lucide-react";
import type {
  WorkflowBuilderActionDraft,
  WorkflowBuilderNodeDraft,
  WorkflowBuilderVersionDraft,
} from "../../hooks/adminWorkflowBuilderModel";
import { isMeasureGenerationNodeType } from "../../hooks/adminWorkflowBuilderModel";
import type {
  AdminAnswerDefinition,
  AdminResponsibilityOwner,
  AdminTaskSpec,
  AdminTaskSpecCondition,
  AdminTaskSpecDependency,
  AdminWorkflowActionDefinition,
  AdminWorkflowDefinitionSummary,
} from "../../types/auth";
import type { AdminAutomationPropertyCatalog } from "../../services/adminConfigApi";
import { getWorkflowBuilderNodeTypeLabel } from "./workflowBuilderLabels";
import { WorkflowBuilderMeasurePreview } from "./WorkflowBuilderMeasurePreview";
import { WorkflowBuilderActionEditor } from "./WorkflowBuilderActionEditor";
import { WorkflowBuilderStepConfigEditor } from "./WorkflowBuilderStepConfigEditor";

export type WorkflowBuilderStepCardProps = {
  node: WorkflowBuilderNodeDraft;
  index: number;
  totalCount: number;
  canManageAdvanced: boolean;

  // Domain data (for measure preview + automation editor)
  versionDraft: WorkflowBuilderVersionDraft;
  workflowDefinitions: AdminWorkflowDefinitionSummary[];
  actionDefinitions: AdminWorkflowActionDefinition[];
  automationPropertyCatalog: AdminAutomationPropertyCatalog | null;
  taskTemplates: AdminTaskSpec[];
  answerDefinitions: AdminAnswerDefinition[];
  taskTemplateConditions: AdminTaskSpecCondition[];
  taskTemplateDependencies: AdminTaskSpecDependency[];
  responsibilityOwners: AdminResponsibilityOwner[];

  onUpdate: (patch: Partial<WorkflowBuilderNodeDraft>) => void;
  onMoveUp: () => void;
  onMoveDown: () => void;
  onRemove: () => void;
  onAddAction: (actionKey: string) => void;
  onUpdateAction: (actionId: string, patch: Partial<WorkflowBuilderActionDraft>) => void;
  onRemoveAction: (actionId: string) => void;
};

export function WorkflowBuilderStepCard(props: WorkflowBuilderStepCardProps) {
  const { node, index, totalCount, onUpdate, onMoveUp, onMoveDown, onRemove } = props;
  const [techOpen, setTechOpen] = useState(false);
  const typeLabel = getWorkflowBuilderNodeTypeLabel(node.nodeType);
  const isFirst = index === 0;
  const isLast = index === totalCount - 1;

  return (
    <div className={`wf-step-card wf-step-card--${node.nodeType}`}>
      <div className="wf-step-card-head">
        <span className="wf-step-card-num">#{index + 1}</span>
        <span className="wf-step-card-type">{typeLabel}</span>
        <input
          className="form-input wf-step-card-title-input"
          type="text"
          value={node.title}
          onChange={(e) => onUpdate({ title: e.target.value })}
          placeholder="Schritt-Name"
          aria-label={`Schritt ${index + 1} Name`}
        />
        <div className="wf-step-card-actions">
          <button
            type="button"
            className="wf-step-card-iconbtn"
            onClick={onMoveUp}
            disabled={isFirst}
            title="Nach oben"
            aria-label="Nach oben verschieben"
          ><ChevronUp size={16} aria-hidden="true" /></button>
          <button
            type="button"
            className="wf-step-card-iconbtn"
            onClick={onMoveDown}
            disabled={isLast}
            title="Nach unten"
            aria-label="Nach unten verschieben"
          ><ChevronDown size={16} aria-hidden="true" /></button>
          <button
            type="button"
            className="wf-step-card-iconbtn wf-step-card-iconbtn--danger"
            onClick={onRemove}
            title="Schritt entfernen"
            aria-label="Schritt entfernen"
          ><X size={16} aria-hidden="true" /></button>
        </div>
      </div>

      <div className="wf-step-card-body">
        <StepCardBody {...props} />
      </div>

      <button
        type="button"
        className="wf-form-tech-toggle wf-step-card-tech-toggle"
        onClick={() => setTechOpen((v) => !v)}
        aria-expanded={techOpen}
      >
        {techOpen
          ? <ChevronDown size={14} aria-hidden="true" />
          : <ChevronRight size={14} aria-hidden="true" />}
        <span>Technische Details</span>
      </button>

      {techOpen && (
        <div className="wf-form-tech-block wf-form-fields">
          <div className="wf-form-field">
            <label className="form-label" htmlFor={`step-key-${node.id}`}>Technischer Schritt-Key</label>
            <input
              id={`step-key-${node.id}`}
              className="form-input"
              type="text"
              value={node.nodeKey}
              onChange={(e) => onUpdate({ nodeKey: e.target.value })}
              placeholder="z. B. step_form_1"
            />
            <p className="wf-form-field-hint">
              Eindeutiger Schlüssel für Verbindungen und Backend-Referenzen.
            </p>
          </div>

          <div className="wf-form-field">
            <label className="form-label" htmlFor={`step-config-${node.id}`}>Technische Konfiguration (JSON)</label>
            <textarea
              id={`step-config-${node.id}`}
              className="form-textarea wf-step-card-configtext"
              rows={4}
              value={node.configText}
              onChange={(e) => onUpdate({ configText: e.target.value })}
              placeholder='{ }'
              spellCheck={false}
            />
            <p className="wf-form-field-hint">
              Roh-JSON. Form-Nodes nutzen <code>legacyProcessTypeKey</code>, andere Knoten brauchen
              meist keine Konfig (Spezifikationen liegen in <code>workflow_node_task_specs</code>).
            </p>
          </div>
        </div>
      )}
    </div>
  );
}

function StepCardBody(props: WorkflowBuilderStepCardProps) {
  const {
    node, versionDraft, workflowDefinitions,
    actionDefinitions, automationPropertyCatalog, answerDefinitions,
    taskTemplates, taskTemplateConditions, taskTemplateDependencies,
    responsibilityOwners, canManageAdvanced,
    onUpdate, onAddAction, onUpdateAction, onRemoveAction,
  } = props;

  if (isMeasureGenerationNodeType(node.nodeType)) {
    return (
      <WorkflowBuilderMeasurePreview
        node={node}
        versionDraft={versionDraft}
        workflowDefinitions={workflowDefinitions}
        taskTemplates={taskTemplates}
        answerDefinitions={answerDefinitions}
        taskTemplateConditions={taskTemplateConditions}
        taskTemplateDependencies={taskTemplateDependencies}
        responsibilityOwners={responsibilityOwners}
      />
    );
  }

  if (node.nodeType === "automation") {
    if (!canManageAdvanced) {
      return (
        <p className="wf-step-card-hint wf-step-card-hint--info">
          Automatisierungen können nur im Admin-Modus bearbeitet werden.
        </p>
      );
    }
    return (
      <WorkflowBuilderActionEditor
        node={node}
        actionDefinitions={actionDefinitions}
        automationPropertyCatalog={automationPropertyCatalog}
        answerDefinitions={answerDefinitions}
        canManageAdvanced={canManageAdvanced}
        onAddAction={onAddAction}
        onUpdateAction={onUpdateAction}
        onRemoveAction={onRemoveAction}
      />
    );
  }

  switch (node.nodeType) {
    case "start":
    case "end":
      return null;

    case "decision":
      return (
        <p className="wf-step-card-hint">
          Bedingungen werden in <strong>Sektion 3 — Übergänge</strong> definiert.
          Mindestens zwei ausgehende Verbindungen werden benötigt.
        </p>
      );

    case "parallel_split":
      return (
        <p className="wf-step-card-hint">
          Startet mehrere parallele Pfade. Mindestens zwei ausgehende Verbindungen werden benötigt.
        </p>
      );

    case "parallel_join":
      return (
        <p className="wf-step-card-hint">
          Sammelt parallele Pfade wieder ein. Mindestens zwei eingehende Verbindungen werden benötigt.
        </p>
      );

    case "form":
    case "approval":
    case "task":
      return (
        <WorkflowBuilderStepConfigEditor
          node={node}
          responsibilityOwners={responsibilityOwners}
          taskTemplates={taskTemplates}
          answerDefinitions={answerDefinitions}
          onUpdate={onUpdate}
        />
      );

    default:
      return null;
  }
}
