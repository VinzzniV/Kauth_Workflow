import { createElement, useMemo, useState } from "react";
import { ArrowRight, ChevronDown, ChevronRight, ChevronUp, Copy, GripVertical, Plus, X } from "lucide-react";
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
import {
  getWorkflowBuilderNodeMeta,
} from "./workflowBuilderLabels";
import { summarizeCondition } from "./workflowBuilderEditorHelpers";
import { WorkflowBuilderMeasurePreview } from "./WorkflowBuilderMeasurePreview";
import { WorkflowBuilderActionEditor } from "./WorkflowBuilderActionEditor";
import { WorkflowBuilderStepConfigEditor } from "./WorkflowBuilderStepConfigEditor";
import { WorkflowBuilderSpecEditor } from "./WorkflowBuilderSpecEditor";
import {
  AUTOMATION_ADMIN_ROLES,
  getAutomationAdminRoleLabel,
} from "../../utils/automationAdminRoles";

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
  onAddOutgoingEdge: () => void;
  onJumpToEdge: (edgeId: string) => void;
  onDragStart?: () => void;
  onDragEnd?: () => void;
  isDragging?: boolean;
};

export function WorkflowBuilderStepCard(props: WorkflowBuilderStepCardProps) {
  const { node, index, totalCount, onUpdate, onMoveUp, onMoveDown, onRemove, onDragStart, onDragEnd, isDragging } = props;
  const [techOpen, setTechOpen] = useState(false);
  const [keyCopied, setKeyCopied] = useState(false);
  const { label: typeLabel, icon: nodeIcon, category } = getWorkflowBuilderNodeMeta(node.nodeType);
  const isFirst = index === 0;
  const isLast = index === totalCount - 1;
  const trimmedKey = node.nodeKey.trim();
  const dndEnabled = Boolean(onDragStart && onDragEnd);

  const handleCopyKey = async () => {
    if (!trimmedKey) return;
    try {
      await navigator.clipboard.writeText(trimmedKey);
      setKeyCopied(true);
      window.setTimeout(() => setKeyCopied(false), 1400);
    } catch {
      // Clipboard not available — silently ignore.
    }
  };

  return (
    <div
      className={`wf-step-card wf-step-card--${node.nodeType} wf-step-card--cat-${category}${isDragging ? " wf-step-card--dragging" : ""}`}
      draggable={dndEnabled}
      onDragStart={dndEnabled ? () => onDragStart!() : undefined}
      onDragEnd={dndEnabled ? () => onDragEnd!() : undefined}
    >
      <div className="wf-step-card-head">
        {dndEnabled ? (
          <span className="wf-step-card-drag-handle" aria-hidden="true" title="Schritt per Drag verschieben">
            <GripVertical size={14} />
          </span>
        ) : null}
        <span className="wf-step-card-num">#{index + 1}</span>
        <span className={`wf-step-card-type wf-step-card-type--${category}`} aria-label={`Schritt-Typ: ${typeLabel}`}>
          {createElement(nodeIcon, { size: 14, "aria-hidden": "true" })}
          <span>{typeLabel}</span>
        </span>
        <input
          className="form-input wf-step-card-title-input"
          type="text"
          value={node.title}
          onChange={(e) => onUpdate({ title: e.target.value })}
          placeholder="Schritt-Name"
          aria-label={`Schritt ${index + 1} Name`}
        />
        {trimmedKey ? (
          <button
            type="button"
            className="wf-step-card-key"
            onClick={() => void handleCopyKey()}
            title={keyCopied ? "Kopiert!" : `Step-Key kopieren: ${trimmedKey}`}
            aria-label={`Step-Key ${trimmedKey} kopieren`}
          >
            <code>{trimmedKey}</code>
            <Copy size={11} aria-hidden="true" />
          </button>
        ) : null}
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

      {node.nodeType !== "end" ? (
        <StepCardOutgoingEdges
          node={node}
          versionDraft={props.versionDraft}
          canManageAdvanced={props.canManageAdvanced}
          onAddOutgoingEdge={props.onAddOutgoingEdge}
          onJumpToEdge={props.onJumpToEdge}
        />
      ) : null}

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
              Roh-JSON. Form-Nodes nutzen <code>workflowDefinitionKey</code>, andere Schritte brauchen
              meist keine Konfig (Spezifikationen liegen in <code>workflow_node_task_specs</code>).
            </p>
          </div>
        </div>
      )}
    </div>
  );
}

function MeasureSpecSummary({
  node,
  answerDefinitions,
  responsibilityOwners,
  canManageAdvanced,
  onUpdate,
}: {
  node: WorkflowBuilderNodeDraft;
  answerDefinitions: AdminAnswerDefinition[];
  responsibilityOwners: AdminResponsibilityOwner[];
  canManageAdvanced: boolean;
  onUpdate: (patch: Partial<WorkflowBuilderNodeDraft>) => void;
}) {
  const [open, setOpen] = useState(false);
  const specs = node.specs;
  const nodeName = node.title.trim() || node.nodeKey.trim() || "Maßnahmen-Schritt";

  return (
    <div className="wf-spec-summary">
      <div className="wf-spec-summary-head">
        <span className="wf-spec-summary-count">
          {specs.length === 0
            ? "Noch keine versionierten Specs"
            : `${specs.length} ${specs.length === 1 ? "Spec" : "Specs"} in dieser Version`}
        </span>
        <button type="button" className="btn btn-secondary" onClick={() => setOpen(true)}>
          {specs.length === 0 ? "Specs anlegen" : "Specs bearbeiten"}
        </button>
      </div>
      {specs.length > 0 ? (
        <div className="wf-automation-summary-chips">
          {specs.slice(0, 5).map((spec, idx) => (
            <span key={`${node.id}-spec-${idx}`} className="chip">
              {spec.title.trim() || spec.specKey.trim() || `Spec ${idx + 1}`}
            </span>
          ))}
          {specs.length > 5 ? <span className="chip">+{specs.length - 5} weitere</span> : null}
        </div>
      ) : null}
      {open ? (
        <WorkflowBuilderSpecEditor
          specs={specs}
          answerDefinitions={answerDefinitions}
          responsibilityOwners={responsibilityOwners}
          canManageAdvanced={canManageAdvanced}
          onChangeSpecs={(updated) => onUpdate({ specs: updated })}
          onClose={() => setOpen(false)}
          nodeName={nodeName}
        />
      ) : null}
    </div>
  );
}

function StepCardOutgoingEdges({
  node,
  versionDraft,
  canManageAdvanced,
  onAddOutgoingEdge,
  onJumpToEdge,
}: {
  node: WorkflowBuilderNodeDraft;
  versionDraft: WorkflowBuilderVersionDraft;
  canManageAdvanced: boolean;
  onAddOutgoingEdge: () => void;
  onJumpToEdge: (edgeId: string) => void;
}) {
  const trimmedKey = node.nodeKey.trim();
  const outgoing = useMemo(() => {
    if (!trimmedKey) return [];
    const lowerKey = trimmedKey.toLowerCase();
    return versionDraft.edges
      .filter((edge) => edge.sourceNodeKey.trim().toLowerCase() === lowerKey)
      .map((edge) => {
        const target = versionDraft.nodes.find(
          (n) => n.nodeKey.trim().toLowerCase() === edge.targetNodeKey.trim().toLowerCase()
        );
        const targetLabel = target
          ? (target.title.trim() || target.nodeKey.trim() || "?")
          : (edge.targetNodeKey.trim() || "?");
        return { edge, targetLabel };
      });
  }, [trimmedKey, versionDraft.edges, versionDraft.nodes]);

  const isDecision = node.nodeType === "decision";
  const isParallelSplit = node.nodeType === "parallel_split";
  const showAddButton = canManageAdvanced && Boolean(trimmedKey);

  if (!trimmedKey) {
    return (
      <div className="wf-step-edges">
        <span className="wf-step-edges-label">Verbindet zu</span>
        <span className="wf-step-edges-empty">Schritt-Key zuerst setzen, dann sind Verbindungen möglich.</span>
      </div>
    );
  }

  return (
    <div className="wf-step-edges">
      <span className="wf-step-edges-label">Verbindet zu</span>
      {outgoing.length === 0 ? (
        <span className="wf-step-edges-empty">
          {isDecision
            ? "Decision braucht ≥ 2 ausgehende Verbindungen."
            : isParallelSplit
              ? "Parallel-Split braucht ≥ 2 ausgehende Verbindungen."
              : "Noch keine Verbindung."}
        </span>
      ) : (
        <div className="wf-step-edges-chips">
          {outgoing.map(({ edge, targetLabel }) => {
            const summary = isDecision ? summarizeCondition(edge.conditionExpression) : null;
            return (
              <button
                key={edge.id}
                type="button"
                className="wf-step-edges-chip"
                onClick={() => onJumpToEdge(edge.id)}
                title={summary ? `Bedingung: ${summary}` : "Übergang in Sektion 3 öffnen"}
              >
                <ArrowRight size={11} aria-hidden="true" />
                <span className="wf-step-edges-chip-target">{targetLabel}</span>
                {summary ? <span className="wf-step-edges-chip-condition">{summary}</span> : null}
              </button>
            );
          })}
        </div>
      )}
      {showAddButton ? (
        <button
          type="button"
          className="wf-step-edges-add"
          onClick={onAddOutgoingEdge}
          title="Neue ausgehende Verbindung anlegen"
        >
          <Plus size={11} aria-hidden="true" />
          <span>Verbindung</span>
        </button>
      ) : null}
    </div>
  );
}

function AutomationStepSummary({
  node,
  actionDefinitions,
  automationPropertyCatalog,
  answerDefinitions,
  canManageAdvanced,
  onAddAction,
  onUpdateAction,
  onRemoveAction,
}: {
  node: WorkflowBuilderNodeDraft;
  actionDefinitions: AdminWorkflowActionDefinition[];
  automationPropertyCatalog: AdminAutomationPropertyCatalog | null;
  answerDefinitions: AdminAnswerDefinition[];
  canManageAdvanced: boolean;
  onAddAction: (actionKey: string) => void;
  onUpdateAction: (actionId: string, patch: Partial<WorkflowBuilderActionDraft>) => void;
  onRemoveAction: (actionId: string) => void;
}) {
  const [open, setOpen] = useState(false);
  const actions = node.actions ?? [];
  const actionLabels = actions.map((action) => {
    const def = actionDefinitions.find((d) => d.actionKey === action.actionKey);
    return def?.displayName ?? action.actionKey;
  });

  return (
    <div className="wf-automation-summary">
      <div className="wf-automation-summary-head">
        <span className="wf-automation-summary-count">
          {actions.length === 0
            ? "Noch keine Aktionen konfiguriert"
            : `${actions.length} ${actions.length === 1 ? "Aktion" : "Aktionen"} konfiguriert`}
        </span>
        <button
          type="button"
          className="btn btn-secondary"
          onClick={() => setOpen(true)}
        >
          {actions.length === 0 ? "Aktionen anlegen" : "Aktionen bearbeiten"}
        </button>
      </div>
      {actions.length > 0 ? (
        <div className="wf-automation-summary-chips">
          {actionLabels.map((label, idx) => (
            <span key={`${node.id}-action-${idx}`} className="chip">
              {idx + 1}. {label}
            </span>
          ))}
        </div>
      ) : null}

      {open ? (
        <>
          <div className="admin-drawer-backdrop" onClick={() => setOpen(false)} aria-hidden="true" />
          <aside
            className="admin-drawer wf-automation-drawer"
            role="dialog"
            aria-modal="true"
            aria-label="Automatisierungs-Aktionen bearbeiten"
          >
            <header className="admin-drawer-head">
              <div className="admin-drawer-title">
                <h2>Automatisierungs-Aktionen</h2>
                <span className="wf-condition-drawer-route">
                  <strong>{node.title.trim() || node.nodeKey.trim() || "Automatisierung"}</strong>
                </span>
              </div>
              <button type="button" className="admin-drawer-close" onClick={() => setOpen(false)} aria-label="Schließen">
                ×
              </button>
            </header>
            <div className="admin-drawer-body content-stack">
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
            </div>
          </aside>
        </>
      ) : null}
    </div>
  );
}

// Slice 4 (Admin-Gated-Automation, Builder-UI): paralleler Block fuer task-Nodes.
// Wording "Automatisierung beim Abschluss" macht admin-gegated-Semantik sichtbar.
// Role-Selector liegt im Drawer ueber der Action-Liste; Step-Card zeigt zur
// Vorschau einen read-only Role-Chip wenn Actions vorhanden.
function TaskApprovalAutomationSummary({
  node,
  actionDefinitions,
  automationPropertyCatalog,
  answerDefinitions,
  canManageAdvanced,
  onUpdate,
  onAddAction,
  onUpdateAction,
  onRemoveAction,
}: {
  node: WorkflowBuilderNodeDraft;
  actionDefinitions: AdminWorkflowActionDefinition[];
  automationPropertyCatalog: AdminAutomationPropertyCatalog | null;
  answerDefinitions: AdminAnswerDefinition[];
  canManageAdvanced: boolean;
  onUpdate: (patch: Partial<WorkflowBuilderNodeDraft>) => void;
  onAddAction: (actionKey: string) => void;
  onUpdateAction: (actionId: string, patch: Partial<WorkflowBuilderActionDraft>) => void;
  onRemoveAction: (actionId: string) => void;
}) {
  const [open, setOpen] = useState(false);
  const actions = node.actions ?? [];
  const hasActions = actions.length > 0;
  const roleLabel = getAutomationAdminRoleLabel(node.automationAdminRole);
  const actionLabels = actions.map((action) => {
    const def = actionDefinitions.find((d) => d.actionKey === action.actionKey);
    return def?.displayName ?? action.actionKey;
  });

  return (
    <div className="wf-automation-summary">
      <div className="wf-automation-summary-head">
        <span className="wf-automation-summary-count">
          {!hasActions
            ? "Noch keine Automatisierung beim Abschluss konfiguriert"
            : `${actions.length} ${actions.length === 1 ? "Aktion" : "Aktionen"} beim Abschluss`}
        </span>
        <button
          type="button"
          className="btn btn-secondary"
          onClick={() => setOpen(true)}
        >
          {!hasActions ? "Automatisierung anlegen" : "Automatisierung bearbeiten"}
        </button>
      </div>
      {hasActions ? (
        <div className="wf-automation-summary-chips">
          {actionLabels.map((label, idx) => (
            <span key={`${node.id}-task-action-${idx}`} className="chip">
              {idx + 1}. {label}
            </span>
          ))}
          <span className="chip">
            Approval-Rolle: {roleLabel ?? "Rolle wählen"}
          </span>
        </div>
      ) : null}

      {open ? (
        <>
          <div className="admin-drawer-backdrop" onClick={() => setOpen(false)} aria-hidden="true" />
          <aside
            className="admin-drawer wf-automation-drawer"
            role="dialog"
            aria-modal="true"
            aria-label="Automatisierung beim Abschluss bearbeiten"
          >
            <header className="admin-drawer-head">
              <div className="admin-drawer-title">
                <h2>Automatisierung beim Abschluss</h2>
                <span className="wf-condition-drawer-route">
                  <strong>{node.title.trim() || node.nodeKey.trim() || "Aufgabe"}</strong>
                </span>
              </div>
              <button type="button" className="admin-drawer-close" onClick={() => setOpen(false)} aria-label="Schließen">
                ×
              </button>
            </header>
            <div className="admin-drawer-body content-stack">
              {hasActions ? (
                <TaskAutomationAdminRoleField
                  value={node.automationAdminRole}
                  disabled={!canManageAdvanced}
                  onChange={(next) => onUpdate({ automationAdminRole: next })}
                />
              ) : null}
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
            </div>
          </aside>
        </>
      ) : null}
    </div>
  );
}

function TaskAutomationAdminRoleField({
  value,
  disabled,
  onChange,
}: {
  value: string | null;
  disabled: boolean;
  onChange: (next: string | null) => void;
}) {
  return (
    <div className="form-field">
      <label>
        <span className="form-field-label">
          Approval-Rolle <span aria-hidden="true">*</span>
        </span>
        <select
          value={value ?? ""}
          disabled={disabled}
          onChange={(e) => onChange(e.target.value ? e.target.value : null)}
        >
          <option value="">— bitte wählen —</option>
          {AUTOMATION_ADMIN_ROLES.map((role) => (
            <option key={role.value} value={role.value}>
              {role.label}
            </option>
          ))}
        </select>
      </label>
      <p className="form-field-hint">
        Bestimmt, welche Rolle die geplanten Aktionen nach Re-Auth bestätigen darf.
      </p>
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
      <>
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
        <MeasureSpecSummary
          node={node}
          answerDefinitions={answerDefinitions}
          responsibilityOwners={responsibilityOwners}
          canManageAdvanced={canManageAdvanced}
          onUpdate={onUpdate}
        />
      </>
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
      <AutomationStepSummary
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
      return (
        <WorkflowBuilderStepConfigEditor
          node={node}
          responsibilityOwners={responsibilityOwners}
          taskTemplates={taskTemplates}
          answerDefinitions={answerDefinitions}
          onUpdate={onUpdate}
        />
      );

    case "task":
      return (
        <>
          <WorkflowBuilderStepConfigEditor
            node={node}
            responsibilityOwners={responsibilityOwners}
            taskTemplates={taskTemplates}
            answerDefinitions={answerDefinitions}
            onUpdate={onUpdate}
          />
          {/* Slice 4 (Admin-Gated-Automation): task-Nodes koennen optional
              ein Action-Bundle tragen, das beim Task-Abschluss admin-getriggert
              ausgefuehrt wird (Approval + Re-Auth Slice 3). Im Bearbeitungsmodus
              ohne Advanced-Access ist der Drawer-Button disabled (gleicher
              Mechanismus wie an automation-Nodes). */}
          {(canManageAdvanced || (node.actions ?? []).length > 0) ? (
            <TaskApprovalAutomationSummary
              node={node}
              actionDefinitions={actionDefinitions}
              automationPropertyCatalog={automationPropertyCatalog}
              answerDefinitions={answerDefinitions}
              canManageAdvanced={canManageAdvanced}
              onUpdate={onUpdate}
              onAddAction={onAddAction}
              onUpdateAction={onUpdateAction}
              onRemoveAction={onRemoveAction}
            />
          ) : null}
        </>
      );

    default:
      return null;
  }
}
