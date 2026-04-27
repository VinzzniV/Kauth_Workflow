import { useMemo, useState } from "react";
import type {
  AdminAnswerDefinition,
  AdminResponsibilityOwner,
  AdminTaskTemplate,
  AdminTaskTemplateCondition,
  AdminTaskTemplateDependency,
  AdminWorkflowActionDefinition,
} from "../../types/auth";
import {
  getDefaultWorkflowBuilderNodeTitle,
  getExpectedMeasureNodeTypeForProcessKey,
  isMeasureGenerationNodeType,
  type WorkflowBuilderNodeDraft,
  type WorkflowBuilderVersionDraft,
} from "../../hooks/adminWorkflowBuilderModel";
import type { BuilderInspectorFocusTarget } from "../../hooks/useAdminWorkflowBuilder";
import { getWorkflowBuilderNodeTypeLabel, WORKFLOW_BUILDER_TECHNICAL_LABELS } from "./workflowBuilderLabels";

type BuilderInspectorPanelProps = {
  selectedNode: WorkflowBuilderNodeDraft;
  availableNodes: Array<{ key: string; label: string }>;
  versionDraft: WorkflowBuilderVersionDraft;
  actionDefinitions: AdminWorkflowActionDefinition[];
  processTypes: Array<{ id: number; key: string; name: string; description: string | null; sortOrder: number }>;
  responsibilityOwners: AdminResponsibilityOwner[];
  taskTemplates: AdminTaskTemplate[];
  answerDefinitions: AdminAnswerDefinition[];
  taskTemplateConditions: AdminTaskTemplateCondition[];
  taskTemplateDependencies: AdminTaskTemplateDependency[];
  canManageAdvanced: boolean;
  onOpenInspectorFocus: (focus: BuilderInspectorFocusTarget) => void;
  onUpdateNode: (nodeId: string, patch: Partial<WorkflowBuilderNodeDraft>) => void;
  onUpdateEdge: (edgeId: string, patch: Partial<WorkflowBuilderVersionDraft["edges"][number]>) => void;
  onRemoveEdge: (edgeId: string) => void;
  onAddActionFromDefinition: (nodeId: string, actionKey: string) => void;
  onUpdateAction: (
    nodeId: string,
    actionId: string,
    patch: Partial<WorkflowBuilderVersionDraft["nodes"][number]["actions"][number]>
  ) => void;
  onRemoveAction: (nodeId: string, actionId: string) => void;
};

type DerivedConditionSummary = {
  id: number;
  label: string;
  answerDefinitionId: number | null;
  answerLabel: string;
};

type DerivedConditionGroupSummary = {
  group: number;
  explanation: string;
  conditions: DerivedConditionSummary[];
};

type DerivedDependencySummary = {
  id: number;
  label: string;
  dependsOnTemplateId: number | null;
};

type DerivedMeasureTemplateCard = {
  templateId: number;
  title: string;
  description: string;
  areaLabel: string;
  moduleLabel: string;
  responsibilityLabel: string;
  isRequired: boolean;
  whyItems: string[];
  conditionGroups: DerivedConditionGroupSummary[];
  dependencies: DerivedDependencySummary[];
  sourceAnswerDefinitions: Array<{ id: number; label: string }>;
};

type DerivedMeasureDetails = {
  processTypeId: number | null;
  processTypeKey: string | null;
  processTypeName: string | null;
  processTypeDescription: string | null;
  measureTypeLabel: string;
  measureSummary: string;
  areaSummary: string[];
  templates: DerivedMeasureTemplateCard[];
};

type DerivedTemplateDetails = {
  template: AdminTaskTemplate | null;
  processTypeId: number | null;
  processTypeName: string | null;
  processTypeDescription: string | null;
  conditionGroups: DerivedConditionGroupSummary[];
  dependencies: DerivedDependencySummary[];
  sourceAnswerDefinitions: Array<{ id: number; label: string }>;
};

const NODE_TYPE_OPTIONS: WorkflowBuilderNodeDraft["nodeType"][] = [
  "start",
  "form",
  "approval",
  "measure_provision",
  "measure_deprovision",
  "measure_change",
  "measure_rename",
  "task",
  "decision",
  "parallel_split",
  "parallel_join",
  "automation",
  "end",
];

export function BuilderInspectorPanel({
  selectedNode,
  availableNodes,
  versionDraft,
  actionDefinitions,
  processTypes,
  responsibilityOwners,
  taskTemplates,
  answerDefinitions,
  taskTemplateConditions,
  taskTemplateDependencies,
  canManageAdvanced,
  onOpenInspectorFocus,
  onUpdateNode,
  onUpdateEdge,
  onRemoveEdge,
  onAddActionFromDefinition,
  onUpdateAction,
  onRemoveAction,
}: BuilderInspectorPanelProps) {
  const [showAdvanced, setShowAdvanced] = useState(false);
  const actionDefinitionsByKey = useMemo(
    () => new Map(actionDefinitions.map((definition) => [definition.actionKey.trim().toLowerCase(), definition] as const)),
    [actionDefinitions]
  );
  const outgoingEdges = useMemo(() => {
    const selectedKey = selectedNode.nodeKey.trim().toLowerCase();
    return versionDraft.edges.filter((edge) => edge.sourceNodeKey.trim().toLowerCase() === selectedKey);
  }, [selectedNode, versionDraft.edges]);
  const summaryText = useMemo(() => getNodeSummaryText(selectedNode.nodeType), [selectedNode.nodeType]);
  const configuredActions = selectedNode.nodeType === "automation" ? selectedNode.actions : [];
  const isLockedAutomationNode = selectedNode.nodeType === "automation" && !canManageAdvanced;
  const normalizedSelectedNodeType = selectedNode.nodeType;
  const selectableNodeTypeOptions = NODE_TYPE_OPTIONS.filter((nodeType) => nodeType !== "automation");
  const isSelectableNonAutomationNodeType =
    normalizedSelectedNodeType !== "automation" && selectableNodeTypeOptions.includes(normalizedSelectedNodeType);
  const allowedNodeTypeOptions = canManageAdvanced
    ? NODE_TYPE_OPTIONS.includes(normalizedSelectedNodeType)
      ? NODE_TYPE_OPTIONS
      : [...NODE_TYPE_OPTIONS, normalizedSelectedNodeType]
    : selectedNode.nodeType === "automation"
      ? (["automation"] as WorkflowBuilderNodeDraft["nodeType"][])
      : isSelectableNonAutomationNodeType
        ? selectableNodeTypeOptions
        : [...selectableNodeTypeOptions, normalizedSelectedNodeType];

  return (
    <section className="builder-sidebar-panel content-stack" aria-label="Eigenschaften">
      <div className="builder-sidebar-panel__header">
        <span className="builder-sidebar-panel__eyebrow">Eigenschaften</span>
        <h3>{selectedNode.title.trim() || selectedNode.nodeKey.trim() || "Neuer Schritt"}</h3>
        <p className="text-muted">{summaryText}</p>
      </div>

      <div className="builder-inspector-hero">
        <span className="badge badge--default">{getWorkflowBuilderNodeTypeLabel(selectedNode.nodeType)}</span>
        <span className="builder-inspector-hero__meta">
          {selectedNode.nodeType === "automation"
            ? "Läuft automatisch"
            : selectedNode.nodeType === "setup"
              ? "Legacy-Alias"
            : isMeasureGenerationNodeType(selectedNode.nodeType)
              ? "Erzeugt Maßnahmen"
            : selectedNode.nodeType === "decision"
              ? "Steuert mehrere Wege"
              : selectedNode.nodeType === "parallel_split"
                ? "Startet parallele Wege"
                : selectedNode.nodeType === "parallel_join"
                  ? "Führt parallele Wege zusammen"
                  : "Teil des Ablaufs"}
        </span>
      </div>

      <section className="content-stack">
        <div>
          <h4>Schritt</h4>
          <p className="text-muted" style={{ margin: 0 }}>Die wichtigsten Angaben für den ausgewählten Ablaufschritt.</p>
        </div>
        <label>
          <span>Titel</span>
          <input className="form-input" value={selectedNode.title} onChange={(event) => onUpdateNode(selectedNode.id, { title: event.target.value })} />
        </label>
        <label>
          <span>Baustein</span>
          <select
            className="form-select"
            value={selectedNode.nodeType}
            disabled={isLockedAutomationNode}
            onChange={(event) =>
              onUpdateNode(selectedNode.id, {
                nodeType: event.target.value as WorkflowBuilderNodeDraft["nodeType"],
                actions: event.target.value === "automation" ? selectedNode.actions : [],
              })
            }
          >
            {allowedNodeTypeOptions.map((nodeType) => (
              <option key={nodeType} value={nodeType}>{getWorkflowBuilderNodeTypeLabel(nodeType)}</option>
            ))}
          </select>
        </label>
      </section>

      <section className="content-stack">
        <div>
          <h4>Details</h4>
          <p className="text-muted" style={{ margin: 0 }}>Fachliche Angaben für diesen Schritt.</p>
        </div>
        {renderNodeConfigurationSection({
          node: selectedNode,
          versionDraft,
          processTypes,
          responsibilityOwners,
          taskTemplates,
          answerDefinitions,
          taskTemplateConditions,
          taskTemplateDependencies,
          onOpenInspectorFocus,
          onUpdateNode,
        })}
      </section>

      <section className="content-stack">
        <div>
          <h4>Nächste Schritte</h4>
          <p className="text-muted" style={{ margin: 0 }}>Hier bearbeitet ihr, wohin der Ablauf von diesem Schritt weitergeht.</p>
        </div>
        {outgoingEdges.length === 0 ? (
          <p className="text-muted">Von diesem Schritt führt aktuell noch kein weiterer Weg ab.</p>
        ) : (
          outgoingEdges.map((edge) => (
            <div key={edge.id} className="panel content-stack">
              <div className="grid-two-columns">
                <label>
                  <span>{WORKFLOW_BUILDER_TECHNICAL_LABELS.targetNode}</span>
                  <select className="form-select" value={edge.targetNodeKey} onChange={(event) => onUpdateEdge(edge.id, { targetNodeKey: event.target.value })}>
                    <option value="">Bitte wählen</option>
                    {availableNodes.map((node) => (
                      <option key={`sidebar-target-${edge.id}-${node.key}`} value={node.key}>{node.label}</option>
                    ))}
                  </select>
                </label>
                <label>
                  <span>{WORKFLOW_BUILDER_TECHNICAL_LABELS.priority}</span>
                  <input className="form-input" type="number" min="1" value={edge.priority} onChange={(event) => onUpdateEdge(edge.id, { priority: event.target.value })} />
                </label>
              </div>
              <button type="button" className="button-danger builder-action-button builder-action-button--danger" onClick={() => onRemoveEdge(edge.id)}>
                Verbindung löschen
              </button>
            </div>
          ))
        )}
      </section>

      <section className="content-stack">
        <div>
          <h4>Automatische Aktionen</h4>
          <p className="text-muted" style={{ margin: 0 }}>Automatische Schritte bleiben sichtbar und nutzen weiterhin den vorhandenen Aktionskatalog.</p>
        </div>
        {renderAutomationSection({
          selectedNode,
          configuredActions,
          actionDefinitions,
          actionDefinitionsByKey,
          canManageAdvanced,
          onAddActionFromDefinition,
          onUpdateAction,
          onRemoveAction,
        })}
      </section>

      <section className="content-stack">
        <button type="button" className="button-secondary builder-action-button builder-action-button--secondary" onClick={() => setShowAdvanced((current) => !current)}>
          {showAdvanced ? "Erweitert ausblenden" : "Erweitert anzeigen"}
        </button>
        {showAdvanced ? renderAdvancedSection(selectedNode, isLockedAutomationNode, onUpdateNode) : null}
      </section>
    </section>
  );
}

function renderAutomationSection({
  selectedNode,
  configuredActions,
  actionDefinitions,
  actionDefinitionsByKey,
  canManageAdvanced,
  onAddActionFromDefinition,
  onUpdateAction,
  onRemoveAction,
}: {
  selectedNode: WorkflowBuilderNodeDraft;
  configuredActions: WorkflowBuilderVersionDraft["nodes"][number]["actions"];
  actionDefinitions: AdminWorkflowActionDefinition[];
  actionDefinitionsByKey: Map<string, AdminWorkflowActionDefinition>;
  canManageAdvanced: boolean;
  onAddActionFromDefinition: (nodeId: string, actionKey: string) => void;
  onUpdateAction: (
    nodeId: string,
    actionId: string,
    patch: Partial<WorkflowBuilderVersionDraft["nodes"][number]["actions"][number]>
  ) => void;
  onRemoveAction: (nodeId: string, actionId: string) => void;
}) {
  if (selectedNode.nodeType !== "automation") {
    return (
      <div className="panel panel-info">
        <p className="panel-text">
          Automatische Aktionen werden nur auf Schritten vom Typ `{getWorkflowBuilderNodeTypeLabel("automation")}` gepflegt. Im Katalog sind aktuell {actionDefinitions.length} Aktion(en) verfügbar.
        </p>
      </div>
    );
  }

  return (
    <div className="content-stack">
      {!canManageAdvanced ? (
        <div className="panel panel-warning">
          <p className="panel-text">Dieser automatische Schritt ist sichtbar, aber nur im Admin-Modus editierbar.</p>
        </div>
      ) : null}
      <div className="content-stack">
        <div>
          <h5 style={{ margin: 0 }}>Verfügbare Aktionen</h5>
          <p className="text-muted" style={{ margin: "0.25rem 0 0" }}>
            Hier könnt ihr nur freigegebene Aktionen verwenden.
          </p>
        </div>
        {!canManageAdvanced ? (
          <div className="panel panel-info"><p className="panel-text">Der Aktionskatalog ist in diesem Modus gesperrt.</p></div>
        ) : actionDefinitions.length === 0 ? (
          <div className="panel panel-warning"><p className="panel-text">Es sind aktuell keine Aktionen geladen.</p></div>
        ) : (
          <div style={{ display: "grid", gap: "0.75rem" }}>
            {actionDefinitions.map((definition) => (
              <div key={`catalog-${definition.actionKey}`} className="panel content-stack" style={{ gap: "0.65rem" }}>
                <div className="flex-row" style={{ justifyContent: "space-between", gap: "0.75rem", alignItems: "flex-start" }}>
                  <div className="content-stack" style={{ gap: "0.2rem" }}>
                    <strong>{definition.displayName}</strong>
                    <span className="text-muted" style={{ fontSize: "0.82rem" }}>{definition.actionKey}</span>
                  </div>
                  <button
                    type="button"
                    className="button-secondary builder-action-button builder-action-button--secondary"
                    aria-label={`${definition.displayName} hinzufügen`}
                    disabled={!definition.isActive}
                    onClick={() => onAddActionFromDefinition(selectedNode.id, definition.actionKey)}
                  >
                    Aktion hinzufügen
                  </button>
                </div>
                {definition.description ? <p className="text-muted" style={{ margin: 0 }}>{definition.description}</p> : null}
                <div className="flex-row" style={{ gap: "0.45rem", flexWrap: "wrap" }}>
                  {renderActionBadge(definition.isActive ? "aktiv" : "inaktiv", definition.isActive ? "success" : "warning")}
                  {renderActionBadge(definition.isIdempotent ? "wiederholbar" : "einmalig", definition.isIdempotent ? "info" : "neutral")}
                  {definition.requiresApproval ? renderActionBadge("braucht Freigabe", "warning") : null}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
      <div className="content-stack">
        <div>
          <h5 style={{ margin: 0 }}>Hinterlegte Aktionen</h5>
          <p className="text-muted" style={{ margin: "0.25rem 0 0" }}>Reihenfolge und Eingabe-Mapping bleiben technisch.</p>
        </div>
        {configuredActions.length === 0 ? (
          <div className="panel panel-warning"><p className="panel-text">Dieser automatische Schritt hat noch keine Aktionen.</p></div>
        ) : null}
        {configuredActions.map((action) => {
          const actionDefinition = actionDefinitionsByKey.get(action.actionKey.trim().toLowerCase()) ?? null;
          return (
            <div key={action.id} className="panel content-stack">
              <strong>{actionDefinition?.displayName ?? action.actionKey ?? "Unbekannte Aktion"}</strong>
              {actionDefinition?.description ? <p className="text-muted" style={{ margin: 0 }}>{actionDefinition.description}</p> : null}
              {!canManageAdvanced ? (
                <div className="builder-readonly-grid">
                  <div className="panel panel-muted content-stack" style={{ gap: "0.35rem" }}>
                    <span className="text-muted" style={{ fontSize: "0.75rem" }}>{WORKFLOW_BUILDER_TECHNICAL_LABELS.executionOrder}</span>
                    <strong>{action.executionOrder}</strong>
                  </div>
                  <div className="panel panel-muted content-stack" style={{ gap: "0.35rem" }}>
                    <span className="text-muted" style={{ fontSize: "0.75rem" }}>{WORKFLOW_BUILDER_TECHNICAL_LABELS.inputMappingJson}</span>
                    <code style={{ whiteSpace: "pre-wrap", overflowWrap: "anywhere", fontSize: "0.78rem" }}>{action.inputMappingText.trim() || "{}"}</code>
                  </div>
                </div>
              ) : (
                <>
                  <div className="grid-two-columns">
                    <label>
                      <span>Aktion</span>
                      <select className="form-select" value={action.actionKey} onChange={(event) => onUpdateAction(selectedNode.id, action.id, { actionKey: event.target.value })}>
                        <option value="">Bitte wählen</option>
                        {actionDefinitions.map((definition) => (
                          <option key={definition.actionKey} value={definition.actionKey}>{definition.displayName}{definition.isActive ? "" : " (inaktiv)"}</option>
                        ))}
                      </select>
                    </label>
                    <label>
                      <span>{WORKFLOW_BUILDER_TECHNICAL_LABELS.executionOrder}</span>
                      <input className="form-input" type="number" min="1" value={action.executionOrder} onChange={(event) => onUpdateAction(selectedNode.id, action.id, { executionOrder: event.target.value })} />
                    </label>
                  </div>
                  <label>
                    <span>{WORKFLOW_BUILDER_TECHNICAL_LABELS.inputMappingJson}</span>
                    <textarea className="form-input" rows={5} value={action.inputMappingText} onChange={(event) => onUpdateAction(selectedNode.id, action.id, { inputMappingText: event.target.value })} />
                  </label>
                  <button type="button" className="button-danger builder-action-button builder-action-button--danger" onClick={() => onRemoveAction(selectedNode.id, action.id)}>
                    Aktion löschen
                  </button>
                </>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}

function renderAdvancedSection(
  selectedNode: WorkflowBuilderNodeDraft,
  isLockedAutomationNode: boolean,
  onUpdateNode: (nodeId: string, patch: Partial<WorkflowBuilderNodeDraft>) => void
) {
  return (
    <div className="panel content-stack">
      <div>
        <h4>Erweitert</h4>
        <p className="text-muted" style={{ margin: 0 }}>Technische Felder bleiben bewusst hinter den fachlichen Angaben.</p>
      </div>
      <div className="grid-two-columns">
        <label>
          <span>{WORKFLOW_BUILDER_TECHNICAL_LABELS.nodeKey}</span>
          <input className="form-input" value={selectedNode.nodeKey} disabled={isLockedAutomationNode} onChange={(event) => onUpdateNode(selectedNode.id, { nodeKey: event.target.value })} />
        </label>
        <label>
          <span>{WORKFLOW_BUILDER_TECHNICAL_LABELS.sortOrder}</span>
          <input className="form-input" type="number" min="1" value={selectedNode.sortOrder} disabled={isLockedAutomationNode} onChange={(event) => onUpdateNode(selectedNode.id, { sortOrder: event.target.value })} />
        </label>
        <label><span>Position X</span><input className="form-input" value={selectedNode.positionX ?? ""} disabled /></label>
        <label><span>Position Y</span><input className="form-input" value={selectedNode.positionY ?? ""} disabled /></label>
      </div>
      {!["start", "end", "parallel_split", "parallel_join"].includes(selectedNode.nodeType) ? (
        <label>
          <span>{WORKFLOW_BUILDER_TECHNICAL_LABELS.configJson}</span>
          <textarea className="form-input" rows={6} value={selectedNode.configText} disabled={isLockedAutomationNode} onChange={(event) => onUpdateNode(selectedNode.id, { configText: event.target.value })} />
        </label>
      ) : null}
    </div>
  );
}

function renderNodeConfigurationSection({
  node,
  versionDraft,
  processTypes,
  responsibilityOwners,
  taskTemplates,
  answerDefinitions,
  taskTemplateConditions,
  taskTemplateDependencies,
  onOpenInspectorFocus,
  onUpdateNode,
}: {
  node: WorkflowBuilderNodeDraft;
  versionDraft: WorkflowBuilderVersionDraft;
  processTypes: Array<{ id: number; key: string; name: string; description: string | null; sortOrder: number }>;
  responsibilityOwners: AdminResponsibilityOwner[];
  taskTemplates: AdminTaskTemplate[];
  answerDefinitions: AdminAnswerDefinition[];
  taskTemplateConditions: AdminTaskTemplateCondition[];
  taskTemplateDependencies: AdminTaskTemplateDependency[];
  onOpenInspectorFocus: (focus: BuilderInspectorFocusTarget) => void;
  onUpdateNode: (nodeId: string, patch: Partial<WorkflowBuilderNodeDraft>) => void;
}) {
  const currentConfig = tryParseConfigObject(node.configText);
  const responsibilityKey = readStringConfigValue(currentConfig, "responsibilityKey");
  const notificationLabel = readStringConfigValue(currentConfig, "notificationLabel");
  const summaryText = readStringConfigValue(currentConfig, "summaryText");
  const legacyProcessTypeKey = readStringConfigValue(currentConfig, "legacyProcessTypeKey");
  const legacyTemplateKey = readStringConfigValue(currentConfig, "legacyTemplateKey");
  const currentProcessTypeKey = resolveEffectiveProcessTypeKey(node, versionDraft, legacyProcessTypeKey);
  const currentProcessType = processTypes.find((processType) => processType.key.trim().toLowerCase() === currentProcessTypeKey.toLowerCase()) ?? null;
  const currentProcessTypeId = currentProcessType?.id ?? null;
  const currentTemplate = legacyTemplateKey ? taskTemplates.find((template) => template.templateKey.trim().toLowerCase() === legacyTemplateKey.toLowerCase()) ?? null : null;
  const activeTaskTemplates = taskTemplates.filter((template) => template.isActive).sort((left, right) => left.sortOrder - right.sortOrder || left.title.localeCompare(right.title, "de"));
  const filteredApprovalTemplates = activeTaskTemplates.filter((template) => !template.isDepartmentPhaseTask);
  const processTypeOptions = processTypes.slice().sort((left, right) => left.sortOrder - right.sortOrder || left.name.localeCompare(right.name, "de"));
  const expectedMeasureType = getExpectedMeasureNodeTypeForProcessKey(versionDraft.primaryLegacyProcessTypeKey);
  const processAnswerDefinitions = currentProcessTypeId
    ? answerDefinitions.filter((definition) => definition.processTypeId === currentProcessTypeId).sort((left, right) => left.sortOrder - right.sortOrder || left.title.localeCompare(right.title, "de"))
    : [];
  const derivedTemplateDetails = node.nodeType === "approval" || node.nodeType === "task"
    ? buildTemplateDetails(currentTemplate, currentProcessTypeId, currentProcessType?.name ?? null, currentProcessType?.description ?? null, answerDefinitions, taskTemplateConditions, taskTemplateDependencies)
    : null;
  const derivedMeasureDetails = isMeasureNodeType(node.nodeType)
    ? buildMeasureNodeDetails(node, currentProcessTypeId, currentProcessType?.key ?? null, currentProcessType?.name ?? null, currentProcessType?.description ?? null, taskTemplates, answerDefinitions, taskTemplateConditions, taskTemplateDependencies, responsibilityOwners)
    : null;

  if (node.nodeType === "start") {
    return <div className="panel panel-info"><p className="panel-text">Der Startschritt braucht keine weiteren fachlichen Angaben.</p></div>;
  }
  if (node.nodeType === "end") {
    return <div className="panel panel-info"><p className="panel-text">Der Endschritt beendet den Pfad und braucht keine weiteren fachlichen Angaben.</p></div>;
  }
  if (node.nodeType === "decision") {
    return <div className="panel panel-info"><p className="panel-text">Entscheidungen steuern Bedingungen und Folgewege. Die konkreten Verbindungen pflegst du unten im Bereich `Nächste Schritte`.</p></div>;
  }
  if (node.nodeType === "parallel_split") {
    return <div className="panel panel-info"><p className="panel-text">Dieser Gateway-Schritt startet mehrere Pfade gleichzeitig. Weitere fachliche Felder sind hier bewusst nicht nötig.</p></div>;
  }
  if (node.nodeType === "parallel_join") {
    return <div className="panel panel-info"><p className="panel-text">Dieser Gateway-Schritt wartet auf mehrere parallele Eingänge und führt den Ablauf danach gesammelt fort.</p></div>;
  }
  if (node.nodeType === "automation") {
    return <div className="panel panel-info"><p className="panel-text">Automatische Schritte beziehen ihr Verhalten aus den hinterlegten Aktionen weiter unten.</p></div>;
  }

  if (node.nodeType === "form") {
    return (
      <div className="content-stack">
        <InspectorSectionHeader title="Direkt am Baustein konfigurierbar" description="Diese Angaben werden direkt auf dem Formular-Baustein gespeichert." />
        <label>
          <span>Zuständige</span>
          <select className="form-select" value={responsibilityKey} onChange={(event) => onUpdateNode(node.id, { configText: updateStringConfigValue(node.configText, "responsibilityKey", event.target.value) })}>
            <option value="">Bitte wählen</option>
            {responsibilityOwners.map((responsibility) => (
              <option key={responsibility.responsibilityId} value={responsibility.responsibilityKey}>{responsibility.responsibilityName}</option>
            ))}
          </select>
        </label>
        <label><span>Benachrichtigte</span><input className="form-input" value={notificationLabel} onChange={(event) => onUpdateNode(node.id, { configText: updateStringConfigValue(node.configText, "notificationLabel", event.target.value) })} /></label>
        <label><span>Kurzbeschreibung</span><textarea className="form-input" rows={3} value={summaryText} onChange={(event) => onUpdateNode(node.id, { configText: updateStringConfigValue(node.configText, "summaryText", event.target.value) })} /></label>
        <label>
          <span>Prozessgrundlage</span>
          <select className="form-select" value={legacyProcessTypeKey} onChange={(event) => onUpdateNode(node.id, { configText: updateStringConfigValue(node.configText, "legacyProcessTypeKey", event.target.value) })}>
            <option value="">Bitte wählen</option>
            {processTypeOptions.map((processType) => (
              <option key={processType.id} value={processType.key}>{processType.name}</option>
            ))}
          </select>
        </label>
        <InspectorSectionHeader title="Aus Prozessdefinition abgeleitet" description="Diese Angaben zeigen, welche Formularfelder und Vorgaben fachlich zugrunde liegen." />
        <div className="panel content-stack">
          <strong>Formulargrundlage</strong>
          <p className="text-muted" style={{ margin: 0 }}>{currentProcessType ? `${currentProcessType.name}${currentProcessType.description ? `: ${currentProcessType.description}` : ""}` : "Noch kein Prozesstyp gewählt."}</p>
          <p className="text-muted" style={{ margin: 0 }}>{processAnswerDefinitions.length > 0 ? `Dieses Formular nutzt aktuell ${processAnswerDefinitions.length} Felddefinition(en).` : "Für den gewählten Prozesstyp sind noch keine Felder geladen."}</p>
        </div>
        {processAnswerDefinitions.slice(0, 8).map((definition) => (
          <div key={definition.id} className="panel content-stack">
            <strong>{definition.title}</strong>
            <p className="text-muted" style={{ margin: 0 }}>{definition.description?.trim() || "Keine zusätzliche Beschreibung hinterlegt."}</p>
          </div>
        ))}
        <InspectorSectionHeader title="Gezielt bearbeiten" description="Sie bleiben im Builder-Kontext und öffnen genau die relevante Quelle." />
        <SourceActionButton label="Prozesstyp bearbeiten" disabled={!currentProcessTypeId} onClick={() => currentProcessTypeId ? onOpenInspectorFocus({ mode: "process_type", processTypeId: currentProcessTypeId, answerDefinitionId: null, templateId: null, templateSection: null }) : undefined} />
        {processAnswerDefinitions.slice(0, 8).map((definition) => (
          <SourceActionButton key={`field-${definition.id}`} label={`Feld bearbeiten: ${definition.title}`} onClick={() => onOpenInspectorFocus({ mode: "answer_definition", processTypeId: definition.processTypeId, answerDefinitionId: definition.id, templateId: null, templateSection: null })} />
        ))}
      </div>
    );
  }

  if (node.nodeType === "approval" || node.nodeType === "task") {
    return (
      <div className="content-stack">
        <InspectorSectionHeader title="Direkt am Baustein konfigurierbar" description="Diese Angaben werden direkt am Aufgaben- oder Freigabe-Baustein gespeichert." />
        <label>
          <span>Zuständige</span>
          <select className="form-select" value={responsibilityKey} onChange={(event) => onUpdateNode(node.id, { configText: updateStringConfigValue(node.configText, "responsibilityKey", event.target.value) })}>
            <option value="">Bitte wählen</option>
            {responsibilityOwners.map((responsibility) => (
              <option key={responsibility.responsibilityId} value={responsibility.responsibilityKey}>{responsibility.responsibilityName}</option>
            ))}
          </select>
        </label>
        <label><span>Benachrichtigte</span><input className="form-input" value={notificationLabel} onChange={(event) => onUpdateNode(node.id, { configText: updateStringConfigValue(node.configText, "notificationLabel", event.target.value) })} /></label>
        <label><span>Kurzbeschreibung</span><textarea className="form-input" rows={3} value={summaryText} onChange={(event) => onUpdateNode(node.id, { configText: updateStringConfigValue(node.configText, "summaryText", event.target.value) })} /></label>
        <label>
          <span>{node.nodeType === "approval" ? "Freigabevorlage" : "Aufgabenvorlage"}</span>
          <select className="form-select" value={legacyTemplateKey} onChange={(event) => onUpdateNode(node.id, { configText: updateStringConfigValue(node.configText, "legacyTemplateKey", event.target.value) })}>
            <option value="">Bitte wählen</option>
            {(node.nodeType === "approval" ? filteredApprovalTemplates : activeTaskTemplates).map((template) => (
              <option key={template.id} value={template.templateKey}>{template.title}</option>
            ))}
          </select>
        </label>
        <InspectorSectionHeader title="Aus Prozessdefinition abgeleitet" description="Diese Angaben zeigen die konkrete Vorlage, ihre Ursache und ihre Abhängigkeiten." />
        {derivedTemplateDetails?.template ? <TemplateInspectorCard details={derivedTemplateDetails} /> : <div className="panel panel-info"><p className="panel-text">Für diesen Baustein ist aktuell noch keine passende Vorlage geladen.</p></div>}
        <InspectorSectionHeader title="Gezielt bearbeiten" description="Sie öffnen exakt die referenzierte Vorlage oder deren Detailquellen im selben Sidebar-Kontext." />
        <SourceActionButton label={node.nodeType === "approval" ? "Freigabevorlage bearbeiten" : "Aufgabenvorlage bearbeiten"} disabled={!derivedTemplateDetails?.template} onClick={() => derivedTemplateDetails?.template ? onOpenInspectorFocus({ mode: "task_template", processTypeId: derivedTemplateDetails.processTypeId, answerDefinitionId: null, templateId: derivedTemplateDetails.template.id, templateSection: "details" }) : undefined} />
        <SourceActionButton label="Bedingungen bearbeiten" disabled={!derivedTemplateDetails?.template} onClick={() => derivedTemplateDetails?.template ? onOpenInspectorFocus({ mode: "task_template_conditions", processTypeId: derivedTemplateDetails.processTypeId, answerDefinitionId: null, templateId: derivedTemplateDetails.template.id, templateSection: "conditions" }) : undefined} />
        <SourceActionButton label="Abhängigkeiten bearbeiten" disabled={!derivedTemplateDetails?.template} onClick={() => derivedTemplateDetails?.template ? onOpenInspectorFocus({ mode: "task_template_dependencies", processTypeId: derivedTemplateDetails.processTypeId, answerDefinitionId: null, templateId: derivedTemplateDetails.template.id, templateSection: "dependencies" }) : undefined} />
        {derivedTemplateDetails?.sourceAnswerDefinitions.map((definition) => (
          <SourceActionButton key={`template-field-${definition.id}`} label={`Feld bearbeiten: ${definition.label}`} onClick={() => onOpenInspectorFocus({ mode: "answer_definition", processTypeId: derivedTemplateDetails.processTypeId, answerDefinitionId: definition.id, templateId: null, templateSection: null })} />
        ))}
      </div>
    );
  }

  return (
    <div className="content-stack">
      <InspectorSectionHeader title="Direkt am Baustein konfigurierbar" description="Auf Maßnahmen-Bausteinen bleiben bewusst nur echte Node-Felder editierbar." />
      <label><span>Kurzbeschreibung</span><textarea className="form-input" rows={3} value={summaryText} onChange={(event) => onUpdateNode(node.id, { configText: updateStringConfigValue(node.configText, "summaryText", event.target.value) })} /></label>
      {node.nodeType === "setup" ? <div className="panel panel-warning"><p className="panel-text">`setup` bleibt hier nur als Legacy-Alias kompatibel.</p></div> : null}
      {expectedMeasureType && node.nodeType !== "setup" && node.nodeType !== expectedMeasureType ? <div className="panel panel-warning"><p className="panel-text">Für den Prozess `{versionDraft.primaryLegacyProcessTypeKey.trim()}` ist fachlich `{getDefaultWorkflowBuilderNodeTitle(expectedMeasureType)}` vorgesehen.</p></div> : null}
      <InspectorSectionHeader title="Aus Prozessdefinition abgeleitet" description="Diese Angaben zeigen vollständig, welche Maßnahmen entstehen, warum sie entstehen und wovon sie abhängen." />
      {derivedMeasureDetails ? renderMeasureSections(derivedMeasureDetails, onOpenInspectorFocus) : null}
    </div>
  );
}

function renderMeasureSections(
  details: DerivedMeasureDetails,
  onOpenInspectorFocus: (focus: BuilderInspectorFocusTarget) => void
) {
  return (
    <>
      <div className="panel content-stack">
        <strong>Maßnahmenblock</strong>
        <p className="text-muted" style={{ margin: 0 }}>{details.processTypeName ? `${details.measureTypeLabel} für ${details.processTypeName}` : `${details.measureTypeLabel} ohne gewählte Prozessgrundlage`}</p>
        {details.processTypeDescription ? <p className="text-muted" style={{ margin: 0 }}>{details.processTypeDescription}</p> : null}
        <p className="text-muted" style={{ margin: 0 }}>{details.measureSummary}</p>
        <ul style={{ margin: 0, paddingLeft: "1.1rem" }}>
          <li>Die Maßnahmen entstehen aus aktiven Vorlagen der Fachbereichsphase.</li>
          <li>Bedingte Maßnahmen erscheinen nur, wenn ihre Gruppenlogik greift.</li>
          <li>Abhängigkeiten verzögern die Abarbeitung, ändern aber nicht den Phasenblock.</li>
        </ul>
      </div>
      {details.areaSummary.length > 0 ? <div className="panel content-stack"><strong>Betroffene Bereiche / Module</strong><ul style={{ margin: 0, paddingLeft: "1.1rem" }}>{details.areaSummary.map((item) => <li key={item}>{item}</li>)}</ul></div> : null}
      {details.templates.length > 0 ? details.templates.map((card) => <MeasureTemplateInspectorCard key={card.templateId} card={card} processTypeId={details.processTypeId} onOpenInspectorFocus={onOpenInspectorFocus} />) : <div className="panel panel-info"><p className="panel-text">Für diesen Maßnahmen-Baustein wurden aktuell keine aktiven Fachbereichsvorlagen gefunden.</p></div>}
      <InspectorSectionHeader title="Gezielt bearbeiten" description="Sie öffnen exakt die relevante Quelle im Builder-Kontext." />
      <SourceActionButton label="Prozesstyp bearbeiten" disabled={!details.processTypeId} onClick={() => details.processTypeId ? onOpenInspectorFocus({ mode: "process_type", processTypeId: details.processTypeId, answerDefinitionId: null, templateId: null, templateSection: null }) : undefined} />
      {details.templates.map((card) => (
        <div key={`edit-actions-${card.templateId}`} className="panel panel-muted content-stack" style={{ gap: "0.55rem" }}>
          <strong>{card.title}</strong>
          <p className="text-muted" style={{ margin: 0 }}>{card.areaLabel} | {card.moduleLabel}</p>
          <div className="flex-row" style={{ gap: "0.45rem", flexWrap: "wrap" }}>
            <SourceActionButton label="Vorlage bearbeiten" onClick={() => onOpenInspectorFocus({ mode: "task_template", processTypeId: details.processTypeId, answerDefinitionId: null, templateId: card.templateId, templateSection: "details" })} />
            <SourceActionButton label="Bedingungen bearbeiten" disabled={card.conditionGroups.length === 0} onClick={() => onOpenInspectorFocus({ mode: "task_template_conditions", processTypeId: details.processTypeId, answerDefinitionId: null, templateId: card.templateId, templateSection: "conditions" })} />
            <SourceActionButton label="Abhängigkeiten bearbeiten" disabled={card.dependencies.length === 0} onClick={() => onOpenInspectorFocus({ mode: "task_template_dependencies", processTypeId: details.processTypeId, answerDefinitionId: null, templateId: card.templateId, templateSection: "dependencies" })} />
          </div>
          {card.sourceAnswerDefinitions.length > 0 ? <div className="flex-row" style={{ gap: "0.45rem", flexWrap: "wrap" }}>{card.sourceAnswerDefinitions.map((definition) => <SourceActionButton key={`measure-field-${card.templateId}-${definition.id}`} label={`Feld bearbeiten: ${definition.label}`} onClick={() => onOpenInspectorFocus({ mode: "answer_definition", processTypeId: details.processTypeId, answerDefinitionId: definition.id, templateId: null, templateSection: null })} />)}</div> : null}
        </div>
      ))}
    </>
  );
}

function InspectorSectionHeader({ title, description }: { title: string; description: string }) {
  return (
    <div>
      <h5 style={{ margin: 0 }}>{title}</h5>
      <p className="text-muted" style={{ margin: "0.25rem 0 0" }}>{description}</p>
    </div>
  );
}

function SourceActionButton({ label, disabled, onClick }: { label: string; disabled?: boolean; onClick: () => void }) {
  return (
    <button type="button" className="button-secondary builder-action-button builder-action-button--secondary" disabled={disabled} onClick={onClick}>
      {label}
    </button>
  );
}

function TemplateInspectorCard({ details }: { details: DerivedTemplateDetails }) {
  if (!details.template) {
    return null;
  }

  return (
    <div className="panel content-stack">
      <strong>{details.template.title}</strong>
      <p className="text-muted" style={{ margin: 0 }}>{details.template.description?.trim() || "Keine zusätzliche Beschreibung hinterlegt."}</p>
      <div className="flex-row" style={{ gap: "0.45rem", flexWrap: "wrap" }}>
        {renderActionBadge(details.template.isRequired ? "Pflichtaufgabe" : "Optional", details.template.isRequired ? "warning" : "neutral")}
        {renderActionBadge(details.template.isDepartmentPhaseTask ? "Fachbereichsphase" : "Vorbereitende Vorlage", details.template.isDepartmentPhaseTask ? "info" : "neutral")}
        {renderActionBadge(details.template.isActive ? "aktiv" : "inaktiv", details.template.isActive ? "success" : "warning")}
      </div>
      <ul style={{ margin: 0, paddingLeft: "1.1rem" }}>
        <li>Diese Aufgabe erscheint, weil genau diese Vorlage am Baustein referenziert ist.</li>
        <li>Sie nutzt die Prozessgrundlage {details.processTypeName ?? "des aktuellen Ablaufs"}.</li>
      </ul>
      {details.conditionGroups.length > 0 ? (
        <div className="content-stack">
          <strong>Warum entsteht diese Aufgabe?</strong>
          {details.conditionGroups.map((group) => <ConditionGroupCard key={`template-group-${group.group}`} group={group} />)}
        </div>
      ) : (
        <p className="text-muted" style={{ margin: 0 }}>Diese Aufgabe entsteht immer, sobald der zugehörige Baustein erreicht wird.</p>
      )}
      {details.dependencies.length > 0 ? (
        <div className="content-stack">
          <strong>Wovon hängt sie ab?</strong>
          <ul style={{ margin: 0, paddingLeft: "1.1rem" }}>{details.dependencies.map((dependency) => <li key={dependency.id}>{dependency.label}</li>)}</ul>
        </div>
      ) : null}
    </div>
  );
}

function MeasureTemplateInspectorCard({
  card,
  processTypeId,
  onOpenInspectorFocus,
}: {
  card: DerivedMeasureTemplateCard;
  processTypeId: number | null;
  onOpenInspectorFocus: (focus: BuilderInspectorFocusTarget) => void;
}) {
  return (
    <div className="panel content-stack">
      <div className="flex-row" style={{ justifyContent: "space-between", gap: "0.75rem", alignItems: "flex-start" }}>
        <div className="content-stack" style={{ gap: "0.18rem" }}>
          <strong>{card.title}</strong>
          <span className="text-muted">{card.areaLabel} | {card.moduleLabel}</span>
        </div>
        <div className="flex-row" style={{ gap: "0.45rem", flexWrap: "wrap" }}>
          {renderActionBadge(card.isRequired ? "Pflichtaufgabe" : "Optional", card.isRequired ? "warning" : "neutral")}
          {renderActionBadge(card.responsibilityLabel, "info")}
        </div>
      </div>
      <p className="text-muted" style={{ margin: 0 }}>{card.description || "Keine zusätzliche Beschreibung hinterlegt."}</p>
      <div className="content-stack">
        <strong>Warum entsteht diese Maßnahme?</strong>
        <ul style={{ margin: 0, paddingLeft: "1.1rem" }}>{card.whyItems.map((item) => <li key={`${card.templateId}-${item}`}>{item}</li>)}</ul>
      </div>
      {card.conditionGroups.length > 0 ? <div className="content-stack"><strong>Konkrete Bedingungen</strong>{card.conditionGroups.map((group) => <ConditionGroupCard key={`${card.templateId}-group-${group.group}`} group={group} />)}</div> : null}
      {card.dependencies.length > 0 ? <div className="content-stack"><strong>Konkrete Abhängigkeiten</strong><ul style={{ margin: 0, paddingLeft: "1.1rem" }}>{card.dependencies.map((dependency) => <li key={`${card.templateId}-dependency-${dependency.id}`}>{dependency.label}</li>)}</ul></div> : null}
      <div className="flex-row" style={{ gap: "0.45rem", flexWrap: "wrap" }}>
        <SourceActionButton label="Vorlage bearbeiten" onClick={() => onOpenInspectorFocus({ mode: "task_template", processTypeId, answerDefinitionId: null, templateId: card.templateId, templateSection: "details" })} />
        <SourceActionButton label="Bedingungen bearbeiten" disabled={card.conditionGroups.length === 0} onClick={() => onOpenInspectorFocus({ mode: "task_template_conditions", processTypeId, answerDefinitionId: null, templateId: card.templateId, templateSection: "conditions" })} />
        <SourceActionButton label="Abhängigkeiten bearbeiten" disabled={card.dependencies.length === 0} onClick={() => onOpenInspectorFocus({ mode: "task_template_dependencies", processTypeId, answerDefinitionId: null, templateId: card.templateId, templateSection: "dependencies" })} />
      </div>
    </div>
  );
}

function ConditionGroupCard({ group }: { group: DerivedConditionGroupSummary }) {
  return (
    <div className="panel panel-muted content-stack" style={{ gap: "0.45rem" }}>
      <strong>Gruppe {group.group}</strong>
      <p className="text-muted" style={{ margin: 0 }}>{group.explanation}</p>
      <ul style={{ margin: 0, paddingLeft: "1.1rem" }}>{group.conditions.map((condition) => <li key={condition.id}>{condition.label}</li>)}</ul>
    </div>
  );
}

function getNodeSummaryText(nodeType: WorkflowBuilderNodeDraft["nodeType"]) {
  switch (nodeType) {
    case "start":
      return "Dieser Schritt startet den Ablauf und führt in die ersten Folgeschritte.";
    case "end":
      return "Dieser Schritt beendet den aktuellen Pfad.";
    case "form":
      return "Hier werden Eingaben gesammelt, die den weiteren Ablauf steuern.";
    case "approval":
      return "Hier holt ihr eine Freigabe für den nächsten Schritt ein.";
    case "measure_provision":
      return "Hier werden Bereitstellungsmaßnahmen aus den erfassten Anforderungen erzeugt und parallel abgearbeitet.";
    case "measure_deprovision":
      return "Hier werden Entzugsmaßnahmen aus dem erfassten Umfang erzeugt und parallel abgearbeitet.";
    case "measure_change":
      return "Hier werden Änderungsmaßnahmen aus dem erfassten Wechsel- oder Anpassungsumfang erzeugt.";
    case "measure_rename":
      return "Hier werden Umbenennungsmaßnahmen aus den erfassten Namensänderungen erzeugt.";
    case "setup":
      return "Dieser Legacy-Baustein bündelt Maßnahmen für mehrere Bereiche und bleibt nur aus Kompatibilitätsgründen erhalten.";
    case "decision":
      return "Hier verzweigt ihr den Ablauf in unterschiedliche Wege.";
    case "parallel_split":
      return "Hier startet ihr mehrere Pfade gleichzeitig und sichtbar parallel.";
    case "parallel_join":
      return "Hier führt ihr mehrere parallele Pfade wieder kontrolliert zusammen.";
    case "automation":
      return "Dieser Schritt läuft automatisch und führt hinterlegte Aktionen aus.";
    case "task":
    default:
      return "Dieser Schritt beschreibt eine manuelle Aufgabe im Ablauf.";
  }
}

function renderActionBadge(label: string, tone: "success" | "info" | "warning" | "neutral") {
  const backgroundByTone = { success: "rgba(220, 252, 231, 0.98)", info: "rgba(224, 231, 255, 0.98)", warning: "rgba(254, 243, 199, 0.98)", neutral: "rgba(241, 245, 249, 0.98)" } as const;
  const borderByTone = { success: "rgba(21, 128, 61, 0.18)", info: "rgba(29, 78, 216, 0.18)", warning: "rgba(180, 83, 9, 0.2)", neutral: "rgba(71, 85, 105, 0.18)" } as const;
  const textByTone = { success: "#166534", info: "#1e40af", warning: "#92400e", neutral: "#334155" } as const;
  return <span className="badge badge--default" style={{ background: backgroundByTone[tone], borderColor: borderByTone[tone], color: textByTone[tone] }}>{label}</span>;
}

function tryParseConfigObject(configText: string): Record<string, unknown> | null {
  if (!configText.trim()) return null;
  try {
    const parsed = JSON.parse(configText) as unknown;
    if (parsed && typeof parsed === "object" && !Array.isArray(parsed)) return parsed as Record<string, unknown>;
  } catch {
    return null;
  }
  return null;
}

function readStringConfigValue(config: Record<string, unknown> | null, key: string) {
  const value = config?.[key];
  return typeof value === "string" ? value : "";
}

function updateStringConfigValue(configText: string, key: string, nextValue: string) {
  const config = tryParseConfigObject(configText) ?? {};
  if (nextValue.trim()) config[key] = nextValue.trim();
  else delete config[key];
  return Object.keys(config).length === 0 ? "" : JSON.stringify(config, null, 2);
}

function resolveEffectiveProcessTypeKey(node: WorkflowBuilderNodeDraft, versionDraft: WorkflowBuilderVersionDraft, legacyProcessTypeKey: string) {
  if (node.nodeType === "form" && legacyProcessTypeKey.trim()) return legacyProcessTypeKey.trim();
  return versionDraft.primaryLegacyProcessTypeKey.trim();
}

function isMeasureNodeType(nodeType: WorkflowBuilderNodeDraft["nodeType"]) {
  return nodeType === "setup" || isMeasureGenerationNodeType(nodeType);
}

function buildMeasureNodeDetails(
  node: WorkflowBuilderNodeDraft,
  processTypeId: number | null,
  processTypeKey: string | null,
  processTypeName: string | null,
  processTypeDescription: string | null,
  taskTemplates: AdminTaskTemplate[],
  answerDefinitions: AdminAnswerDefinition[],
  taskTemplateConditions: AdminTaskTemplateCondition[],
  taskTemplateDependencies: AdminTaskTemplateDependency[],
  responsibilityOwners: AdminResponsibilityOwner[]
): DerivedMeasureDetails {
  const measureTypeLabel = getMeasureTypeLabel(node.nodeType, processTypeKey, processTypeName);
  const measureSummary = getMeasureProcessSummary(node.nodeType, processTypeKey);
  const relevantTemplates = processTypeId
    ? taskTemplates.filter((template) => template.processTypeId === processTypeId && template.isActive && template.isDepartmentPhaseTask).sort((left, right) => left.sortOrder - right.sortOrder || left.title.localeCompare(right.title, "de"))
    : [];
  const answerDefinitionsByCompositeKey = new Map(answerDefinitions.map((definition) => [`${definition.processTypeId}:${definition.answerKey.trim().toLowerCase()}`, definition] as const));
  const templatesById = new Map(relevantTemplates.map((template) => [template.id, template] as const));
  const responsibilityOwnersById = new Map(responsibilityOwners.map((owner) => [owner.responsibilityId, owner] as const));
  const templates = relevantTemplates.map((template) => {
    const conditionGroups = buildConditionGroupSummaries(template.id, processTypeId, answerDefinitionsByCompositeKey, taskTemplateConditions);
    const dependencies = buildDependencySummaries(template.id, templatesById, taskTemplateDependencies);
    const owner = template.defaultResponsibilityId ? responsibilityOwnersById.get(template.defaultResponsibilityId) ?? null : null;
    const sourceAnswerDefinitions = Array.from(new Map(conditionGroups.flatMap((group) => group.conditions).filter((condition) => condition.answerDefinitionId !== null).map((condition) => [condition.answerDefinitionId, { id: condition.answerDefinitionId!, label: condition.answerLabel }] as const)).values());
    return {
      templateId: template.id,
      title: template.title,
      description: template.description,
      areaLabel: owner?.departmentName?.trim() || owner?.responsibilityName?.trim() || "Mehrere Bereiche",
      moduleLabel: buildMeasureModuleLabel(template.title, template.category),
      responsibilityLabel: owner?.responsibilityName?.trim() || "Noch nicht festgelegt",
      isRequired: template.isRequired,
      whyItems: buildMeasureWhyItems(template.title, processTypeKey, processTypeName, conditionGroups.length > 0),
      conditionGroups,
      dependencies,
      sourceAnswerDefinitions,
    } satisfies DerivedMeasureTemplateCard;
  });
  return { processTypeId, processTypeKey, processTypeName, processTypeDescription, measureTypeLabel, measureSummary, areaSummary: buildMeasureAreaSummaries(relevantTemplates, responsibilityOwnersById), templates };
}

function getMeasureTypeLabel(
  nodeType: WorkflowBuilderNodeDraft["nodeType"],
  processTypeKey: string | null,
  processTypeName: string | null
) {
  if (nodeType === "setup") {
    return "Legacy-Setup";
  }

  if (nodeType === "measure_change") {
    if (processTypeKey === "position_change") {
      return processTypeName ? `Änderungsmaßnahmen für ${processTypeName}` : "Änderungsmaßnahmen für Positionswechsel";
    }

    if (processTypeKey === "role_change") {
      return processTypeName ? `Änderungsmaßnahmen für ${processTypeName}` : "Änderungsmaßnahmen für Rollenwechsel";
    }
  }

  if (nodeType === "measure_rename" && processTypeName) {
    return `Umbenennungsmaßnahmen für ${processTypeName}`;
  }

  return getDefaultWorkflowBuilderNodeTitle(nodeType);
}

function getMeasureProcessSummary(
  nodeType: WorkflowBuilderNodeDraft["nodeType"],
  processTypeKey: string | null
) {
  switch (processTypeKey) {
    case "name_change":
      return "Der Baustein bündelt Namens-, Anzeigenamen-, Mail- und Verzeichnisumstellungen auf Basis des neuen Namens und des Wirksamkeitsdatums.";
    case "position_change":
      return "Der Baustein bündelt positionsbezogene Berechtigungs-, Zugriffs-, Schulungs- und Systemanpassungen auf Basis des erfassten Wechselumfangs.";
    case "role_change":
      return "Der Baustein bündelt rollenbezogene Rollen-, Berechtigungs- und Systemanpassungen auf Basis des erfassten Rollenwechsels.";
    default:
      if (nodeType === "measure_rename") {
        return "Der Baustein bündelt Umbenennungen in Identitäts- und angeschlossenen Systemdaten.";
      }

      if (nodeType === "measure_change") {
        return "Der Baustein bündelt Änderungsmaßnahmen für betroffene Bereiche auf Basis des erfassten Änderungsumfangs.";
      }

      if (nodeType === "measure_deprovision") {
        return "Der Baustein bündelt Entzugsmaßnahmen auf Basis des erfassten Offboarding-Umfangs.";
      }

      if (nodeType === "measure_provision") {
        return "Der Baustein bündelt Bereitstellungsmaßnahmen auf Basis der erfassten Anforderungen.";
      }

      return "Der Baustein bündelt interne Maßnahmen als Legacy-Setup-Block.";
  }
}

function buildMeasureWhyItems(
  templateTitle: string,
  processTypeKey: string | null,
  processTypeName: string | null,
  hasConditions: boolean
) {
  const processLabel = processTypeName ?? "dieses Ablaufs";
  const scopeExplanation = (() => {
    switch (processTypeKey) {
      case "name_change":
        return `${templateTitle} ist als aktive Umbenennungsmaßnahme des Prozesstyps ${processLabel} hinterlegt.`;
      case "position_change":
        return `${templateTitle} ist als positionsbezogene Änderungsmaßnahme des Prozesstyps ${processLabel} hinterlegt.`;
      case "role_change":
        return `${templateTitle} ist als rollenbezogene Änderungsmaßnahme des Prozesstyps ${processLabel} hinterlegt.`;
      default:
        return `${templateTitle} ist als aktive Maßnahme des Prozesstyps ${processLabel} hinterlegt.`;
    }
  })();

  return hasConditions
    ? [scopeExplanation, "Die Maßnahme entsteht nur, wenn mindestens eine der hinterlegten Bedingungsgruppen greift.", "Innerhalb einer Gruppe müssen alle Bedingungen gleichzeitig erfüllt sein."]
    : [scopeExplanation, "Die Maßnahme entsteht immer, sobald der Maßnahmen-Block erreicht wird."];
}

function buildTemplateDetails(
  template: AdminTaskTemplate | null,
  processTypeId: number | null,
  processTypeName: string | null,
  processTypeDescription: string | null,
  answerDefinitions: AdminAnswerDefinition[],
  taskTemplateConditions: AdminTaskTemplateCondition[],
  taskTemplateDependencies: AdminTaskTemplateDependency[]
): DerivedTemplateDetails {
  if (!template) return { template: null, processTypeId, processTypeName, processTypeDescription, conditionGroups: [], dependencies: [], sourceAnswerDefinitions: [] };
  const answerDefinitionsByCompositeKey = new Map(answerDefinitions.map((definition) => [`${definition.processTypeId}:${definition.answerKey.trim().toLowerCase()}`, definition] as const));
  const conditionGroups = buildConditionGroupSummaries(template.id, processTypeId, answerDefinitionsByCompositeKey, taskTemplateConditions);
  const dependencies = buildDependencySummaries(template.id, new Map([[template.id, template]]), taskTemplateDependencies);
  const sourceAnswerDefinitions = Array.from(new Map(conditionGroups.flatMap((group) => group.conditions).filter((condition) => condition.answerDefinitionId !== null).map((condition) => [condition.answerDefinitionId, { id: condition.answerDefinitionId!, label: condition.answerLabel }] as const)).values());
  return { template, processTypeId, processTypeName, processTypeDescription, conditionGroups, dependencies, sourceAnswerDefinitions };
}

function buildConditionGroupSummaries(
  templateId: number,
  processTypeId: number | null,
  answerDefinitionsByCompositeKey: Map<string, AdminAnswerDefinition>,
  taskTemplateConditions: AdminTaskTemplateCondition[]
) {
  const grouped = new Map<number, AdminTaskTemplateCondition[]>();
  for (const condition of taskTemplateConditions.filter((entry) => entry.taskTemplateId === templateId)) {
    const current = grouped.get(condition.conditionGroup) ?? [];
    current.push(condition);
    grouped.set(condition.conditionGroup, current);
  }
  return [...grouped.entries()].sort(([left], [right]) => left - right).map(([group, conditions]) => ({
    group,
    explanation: conditions.length > 1 ? "Alle Bedingungen dieser Gruppe müssen gleichzeitig erfüllt sein. Mehrere Gruppen gelten alternativ." : "Diese Bedingung reicht aus, um die Gruppe zu erfüllen. Mehrere Gruppen gelten alternativ.",
    conditions: conditions.map((condition) => {
      const answerDefinition = processTypeId !== null ? answerDefinitionsByCompositeKey.get(`${processTypeId}:${condition.answerKey.trim().toLowerCase()}`) ?? null : null;
      return { id: condition.id, label: formatConditionLabel(condition, answerDefinition), answerDefinitionId: answerDefinition?.id ?? null, answerLabel: answerDefinition?.title ?? "Passende Anforderung" } satisfies DerivedConditionSummary;
    }),
  }));
}

function buildDependencySummaries(templateId: number, templatesById: Map<number, AdminTaskTemplate>, taskTemplateDependencies: AdminTaskTemplateDependency[]) {
  return taskTemplateDependencies.filter((dependency) => dependency.taskTemplateId === templateId).map((dependency) => {
    const dependencySource = templatesById.get(dependency.dependsOnTaskTemplateId)?.title ?? dependency.dependsOnTemplateTitle ?? "vorgelagerte Maßnahme";
    return { id: dependency.id, label: formatDependencyLabel(dependency.requiredStatus, dependencySource), dependsOnTemplateId: dependency.dependsOnTaskTemplateId } satisfies DerivedDependencySummary;
  });
}

function buildMeasureAreaSummaries(templates: AdminTaskTemplate[], responsibilityOwnersById: Map<number, AdminResponsibilityOwner>) {
  const modulesByArea = new Map<string, Set<string>>();
  for (const template of templates) {
    const owner = template.defaultResponsibilityId ? responsibilityOwnersById.get(template.defaultResponsibilityId) ?? null : null;
    const areaLabel = owner?.departmentName?.trim() || owner?.responsibilityName?.trim() || "Bereiche";
    const modules = modulesByArea.get(areaLabel) ?? new Set<string>();
    modules.add(buildMeasureModuleLabel(template.title, template.category));
    modulesByArea.set(areaLabel, modules);
  }
  return [...modulesByArea.entries()].slice(0, 8).map(([areaLabel, modules]) => {
    const compactModules = [...modules].slice(0, 4);
    return `${areaLabel}: ${compactModules.join(", ")}${modules.size > compactModules.length ? " ..." : ""}`;
  });
}

function buildMeasureModuleLabel(title: string, category: string) {
  const normalizedTitle = title.trim();
  if (!normalizedTitle) return category.trim() || "Modul";
  const compactTitle = normalizedTitle.replace(/^(Anlegen|Einrichten|Bereitstellen|Vorbereiten|Aktualisieren|Vergeben|Übernehmen|Uebernehmen|Deaktivieren|Sperren|Entziehen|Einziehen|Tauschen|Ändern|Aendern)\s+/i, "").replace(/\s+(anlegen|einrichten|bereitstellen|vorbereiten|aktualisieren|vergeben|uebernehmen|übernehmen|deaktivieren|sperren|entziehen|einziehen|tauschen|ändern|aendern)$/i, "").replace(/\s+(für|fuer)\s+.*$/i, "").trim();
  if (/mailbox|alias|mail/i.test(compactTitle)) return "Mail";
  if (/hardware|telefon|badge|schlüssel|schluessel/i.test(compactTitle)) return "Hardware";
  if (/ad-user|ad-konto|ad-gruppen|ad-benutzername|ad/i.test(compactTitle)) return "AD";
  if (/berechtig/i.test(compactTitle)) return "Rechte";
  return compactTitle.replace(/\s+-\s+/g, " ").replace(/\s{2,}/g, " ").trim();
}

function formatConditionLabel(condition: AdminTaskTemplateCondition, answerDefinition: AdminAnswerDefinition | null) {
  const answerLabel = answerDefinition?.title ?? "passende Anforderung";
  switch (condition.operator) {
    case "is_true": return `${answerLabel} ausgewählt ist.`;
    case "is_false": return `${answerLabel} nicht ausgewählt ist.`;
    case "is_not_null": return `${answerLabel} gepflegt ist.`;
    case "is_null": return `${answerLabel} noch leer ist.`;
    case "eq":
      if (condition.expectedValueText?.trim()) return `${answerLabel} auf '${condition.expectedValueText.trim()}' gesetzt ist.`;
      if (condition.expectedValueBoolean !== null) return `${answerLabel} auf '${condition.expectedValueBoolean ? "Ja" : "Nein"}' gesetzt ist.`;
      if (condition.expectedValueNumber !== null) return `${answerLabel} auf '${condition.expectedValueNumber}' gesetzt ist.`;
      return `${answerLabel} passend gesetzt ist.`;
    case "neq":
      if (condition.expectedValueText?.trim()) return `${answerLabel} nicht auf '${condition.expectedValueText.trim()}' gesetzt ist.`;
      if (condition.expectedValueBoolean !== null) return `${answerLabel} nicht auf '${condition.expectedValueBoolean ? "Ja" : "Nein"}' gesetzt ist.`;
      if (condition.expectedValueNumber !== null) return `${answerLabel} nicht auf '${condition.expectedValueNumber}' gesetzt ist.`;
      return `${answerLabel} abweichend gesetzt ist.`;
    default: return `${answerLabel} passend ausgeprägt ist.`;
  }
}

function formatDependencyLabel(requiredStatus: AdminTaskTemplateDependency["requiredStatus"], dependencySource: string) {
  switch (requiredStatus) {
    case "done": return `Wird erst bearbeitbar, nachdem ${dependencySource} abgeschlossen ist.`;
    case "in_progress": return `Kann starten, sobald ${dependencySource} in Bearbeitung ist.`;
    case "ready": return `Kann starten, sobald ${dependencySource} bereitsteht.`;
    case "blocked": return `Wird relevant, wenn ${dependencySource} blockiert ist.`;
    case "open":
    default: return `Kann starten, sobald ${dependencySource} eröffnet wurde.`;
  }
}
