import { useMemo, useState } from "react";
import {
  Background,
  Controls,
  MarkerType,
  MiniMap,
  ReactFlow,
  type Edge,
  type Node,
  type NodeTypes,
  type ReactFlowInstance,
} from "@xyflow/react";
import "@xyflow/react/dist/style.css";
import { useCurrentUser } from "../../auth/useCurrentUser";
import { useAdminWorkflowBuilder } from "../../hooks/useAdminWorkflowBuilder";
import {
  isMeasureGenerationNodeType,
  type WorkflowBuilderNodeDraft,
  type WorkflowBuilderVersionDraft,
} from "../../hooks/adminWorkflowBuilderModel";
import { getWorkflowBuilderNodeTypeLabel, WORKFLOW_BUILDER_TECHNICAL_LABELS } from "./workflowBuilderLabels";
import {
  WorkflowBuilderCanvasNode,
  type WorkflowBuilderCanvasNodeData,
} from "./WorkflowBuilderCanvasNode";
import { WorkflowBuilderSidebar } from "./WorkflowBuilderSidebar";
import { buildWorkflowBuilderStructuredLayout } from "./workflowBuilderStructuredLayout";

type AdminWorkflowBuilderSectionProps = {
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
  pageModeLabel?: string;
};

const NODE_TYPES: NodeTypes = {
  workflowBuilderNode: WorkflowBuilderCanvasNode,
};

export function AdminWorkflowBuilderSection({
  onNotice,
  onError,
  pageModeLabel,
}: AdminWorkflowBuilderSectionProps) {
  const { capabilities } = useCurrentUser();
  const canManageAdvanced = capabilities.canManageAdminConfiguration;
  const builder = useAdminWorkflowBuilder({ onNotice, onError, canManageAdvanced });
  const [flowInstance, setFlowInstance] = useState<ReactFlowInstance<Node<WorkflowBuilderCanvasNodeData>, Edge> | null>(null);

  const availableNodes = useMemo(
    () => builder.versionDraft.nodes
      .map((node) => {
        const key = node.nodeKey.trim();
        if (!key) {
          return null;
        }

        const title = node.title.trim();
        return {
          key,
          label: title ? `${title} (${key})` : key,
        };
      })
      .filter((node): node is { key: string; label: string } => Boolean(node)),
    [builder.versionDraft.nodes]
  );

  const actionDefinitionsByKey = useMemo(() => {
    return new Map(
      builder.actionDefinitions.map((definition) => [
        definition.actionKey.trim().toLowerCase(),
        {
          displayName: definition.displayName,
          isActive: definition.isActive,
          description: definition.description,
        },
      ] as const)
    );
  }, [builder.actionDefinitions]);

  const processTypesByKey = useMemo(() => {
    return new Map(
      builder.processTypes.map((processType) => [processType.key.trim().toLowerCase(), processType] as const)
    );
  }, [builder.processTypes]);

  const responsibilityOwnersByKey = useMemo(() => {
    return new Map(
      builder.responsibilityOwners.map((responsibility) => [
        responsibility.responsibilityKey.trim().toLowerCase(),
        responsibility,
      ] as const)
    );
  }, [builder.responsibilityOwners]);

  const responsibilityOwnersById = useMemo(() => {
    return new Map(
      builder.responsibilityOwners.map((responsibility) => [responsibility.responsibilityId, responsibility] as const)
    );
  }, [builder.responsibilityOwners]);

  const taskTemplatesByKey = useMemo(() => {
    return new Map(
      builder.taskTemplates.map((template) => [template.templateKey.trim().toLowerCase(), template] as const)
    );
  }, [builder.taskTemplates]);

  const nodesByKey = useMemo(() => {
    return new Map(
      builder.versionDraft.nodes
        .map((node) => {
          const normalizedKey = node.nodeKey.trim().toLowerCase();
          return normalizedKey ? ([normalizedKey, node] as const) : null;
        })
        .filter((entry): entry is readonly [string, WorkflowBuilderNodeDraft] => Boolean(entry))
    );
  }, [builder.versionDraft.nodes]);

  const structuredLayout = useMemo(() => {
    return buildWorkflowBuilderStructuredLayout(builder.versionDraft);
  }, [builder.versionDraft]);

  const canvasNodes = useMemo<Node<WorkflowBuilderCanvasNodeData>[]>(() => {
    const actualNodesById = new Map(builder.versionDraft.nodes.map((node) => [node.id, node] as const));
    return structuredLayout.nodes.map((layoutNode) => {
      const actualNode = layoutNode.sourceNodeId
        ? actualNodesById.get(layoutNode.sourceNodeId) ?? null
        : null;

      return {
        id: layoutNode.id,
        type: "workflowBuilderNode",
        position: {
          x: layoutNode.x,
          y: layoutNode.y,
        },
        draggable: !layoutNode.isVirtual,
        selectable: !layoutNode.isVirtual,
        connectable: !layoutNode.isVirtual,
        data: layoutNode.isVirtual || !actualNode
          ? {
              title: "",
              nodeType: layoutNode.nodeType,
              isVirtual: true,
              junctionRole: layoutNode.junctionRole,
              typeLabel: "",
              modeLabel: "",
              responsibleLabel: "",
              notificationLabel: null,
              dueLabel: null,
              effectText: "",
              detailItems: [],
              nextStepLabel: "",
              isSelected: false,
            }
          : {
              ...buildCanvasNodeSummary(
                actualNode,
                builder.versionDraft,
                nodesByKey,
                actionDefinitionsByKey,
                processTypesByKey,
                builder.taskTemplates,
                taskTemplatesByKey,
                responsibilityOwnersByKey,
                responsibilityOwnersById
              ),
              title: actualNode.title.trim() || actualNode.nodeKey.trim() || "Neuer Schritt",
              isSelected: builder.selectedNode?.id === actualNode.id,
            },
      };
    });
  }, [
    actionDefinitionsByKey,
    builder.selectedNode?.id,
    builder.taskTemplates,
    builder.versionDraft,
    nodesByKey,
    processTypesByKey,
    responsibilityOwnersById,
    responsibilityOwnersByKey,
    structuredLayout.nodes,
    taskTemplatesByKey,
  ]);

  const canvasEdges = useMemo<Edge[]>(() => {
    return structuredLayout.edges.map((edge) => {
      const isSelected = !edge.isVirtual && builder.selectedEdge?.id === edge.id;

      return {
        id: edge.id,
        source: edge.source,
        target: edge.target,
        animated: false,
        selectable: !edge.isVirtual,
        label: edge.label,
        markerEnd: {
          type: MarkerType.ArrowClosed,
          width: edge.isVirtual ? 16 : 20,
          height: edge.isVirtual ? 16 : 20,
          color: edge.isVirtual
            ? "rgba(100, 116, 139, 0.78)"
            : isSelected
              ? "var(--graph-edge-done)"
              : "var(--graph-edge-open)",
        },
        style: {
          stroke: edge.isVirtual
            ? "rgba(100, 116, 139, 0.58)"
            : isSelected
              ? "var(--graph-edge-done)"
              : "var(--graph-edge-open)",
          strokeWidth: edge.isVirtual ? 1.8 : isSelected ? 3 : 2.4,
          strokeDasharray: edge.isVirtual ? "4 4" : undefined,
        },
        labelStyle: {
          fill: "var(--graph-node-title)",
          fontSize: 12,
          fontWeight: 600,
        },
        labelBgPadding: [8, 4] as [number, number],
        labelBgBorderRadius: 999,
        labelBgStyle: edge.label
          ? {
              fill: isSelected ? "rgba(219, 234, 254, 0.96)" : "rgba(255,255,255,0.92)",
              fillOpacity: 1,
              stroke: isSelected ? "rgba(59, 130, 246, 0.95)" : "rgba(148, 163, 184, 0.7)",
            }
          : undefined,
      } satisfies Edge;
    });
  }, [builder.selectedEdge?.id, structuredLayout.edges]);

  const selectedWorkflowName = builder.selectedDefinition?.name ?? "Ablauf wählen";
  const selectedWorkingDraftLabel = builder.selectedVersionSummary
    ? `${formatVersionStatus(builder.selectedVersionSummary.status)} ${builder.selectedVersionSummary.versionNumber}`
    : builder.isLoadingVersion
      ? "Arbeitsentwurf wird geöffnet"
      : "Noch kein Arbeitsentwurf geöffnet";
  const publishDisabledReason = !canManageAdvanced
    ? "Freigeben ist nur im Admin-Modus erlaubt."
    : builder.hasUnsavedChanges
      ? "Bitte erst speichern."
      : !builder.selectedVersionSummary?.canPublish
        ? "Dieser Stand ist noch nicht freigabefähig."
        : undefined;
  const canDeleteSelectedNode = Boolean(
    builder.selectedVersionSummary
    && builder.selectedNode
    && (canManageAdvanced || builder.selectedNode.nodeType !== "automation")
  );
  const workspaceStatusLabel = builder.selectedNode
    ? `Eigenschaften offen für ${builder.selectedNode.title.trim() || builder.selectedNode.nodeKey.trim() || "Schritt"}`
    : builder.selectedDefinition
      ? "Bausteine sichtbar"
      : "Noch kein Ablauf geöffnet";
  const localIssueCount = builder.localValidationIssues.length;
  const serverIssueCount = builder.versionDetail?.validationIssues.length ?? 0;
  const totalIssueCount = localIssueCount + serverIssueCount;

  return (
    <div className="builder-product-section">
      <section className="builder-studio-topbar panel">
        <div className="builder-studio-topbar__main">
          <div className="builder-studio-topbar__copy">
            <div className="builder-mode-badges">
              <span className={`badge badge--default ${canManageAdvanced ? "builder-mode-badge builder-mode-badge--advanced" : "builder-mode-badge"}`}>
                {pageModeLabel ?? (canManageAdvanced ? "Admin-Modus" : "Bearbeitungsmodus")}
              </span>
              {!canManageAdvanced ? (
                <span className="badge badge--default builder-mode-badge builder-mode-badge--locked">
                  Automatisierung gesperrt
                </span>
              ) : null}
            </div>
            <h1>Ablauf-Editor</h1>
            <p className="text-muted">
              Baue und lies Abläufe direkt im Canvas. Auswahl links, Ablauf in der Mitte, Eigenschaften rechts.
            </p>
          </div>

          <div className="builder-studio-topbar__status">
            <div className="builder-topbar-stat">
              <span>Ablauf</span>
              <strong>{selectedWorkflowName}</strong>
            </div>
            <div className="builder-topbar-stat">
              <span>Status</span>
              <strong>{workspaceStatusLabel}</strong>
            </div>
          </div>
        </div>

        <div className="builder-studio-topbar__actions">
          <div className="builder-studio-action-group">
            <button
              type="button"
              className="button-primary builder-action-button builder-action-button--primary"
              onClick={() => void builder.saveVersion()}
              disabled={!builder.selectedVersionSummary || builder.isSaving || builder.isLoadingVersion}
            >
              Speichern
            </button>
            <button
              type="button"
              className="button-secondary builder-action-button builder-action-button--secondary"
              onClick={builder.validateDraft}
              disabled={!builder.selectedVersionSummary}
            >
              Prüfen
            </button>
            <button
              type="button"
              className="button-secondary builder-action-button builder-action-button--secondary"
              onClick={() => void builder.publishVersion()}
              disabled={!canManageAdvanced || !builder.selectedVersionSummary?.canPublish || builder.isPublishing || builder.hasUnsavedChanges}
              title={publishDisabledReason}
            >
              Freigeben
            </button>
          </div>

          <div className="builder-studio-action-group builder-studio-action-group--secondary">
            <button
              type="button"
              className="button-secondary builder-action-button builder-action-button--secondary"
              onClick={builder.autoLayoutNodes}
              disabled={!builder.selectedVersionSummary}
            >
              Anordnen
            </button>
            <button
              type="button"
              className="button-secondary builder-action-button builder-action-button--secondary"
              onClick={() => {
                if (flowInstance) {
                  setTimeout(() => {
                    void flowInstance.fitView({ padding: 0.18, duration: 250 });
                  }, 0);
                }
              }}
              disabled={!flowInstance}
            >
              Zentrieren
            </button>
            <button
              type="button"
              className="button-danger builder-action-button builder-action-button--danger"
              onClick={() => void builder.deleteDefinition()}
              disabled={!canManageAdvanced || !builder.selectedDefinition || builder.isDeletingDefinition}
              title={!builder.selectedDefinition ? "Bitte zuerst einen Ablauf auswählen." : undefined}
            >
              Ablauf löschen
            </button>
            <button
              type="button"
              className="button-danger builder-action-button builder-action-button--danger"
              onClick={builder.removeSelectedNode}
              disabled={!canDeleteSelectedNode}
              title={
                builder.selectedNode
                  ? canDeleteSelectedNode
                    ? "Ausgewählten Schritt löschen"
                    : "Automatisierungen können nur im Admin-Modus gelöscht werden."
                  : "Bitte zuerst einen Schritt im Canvas auswählen."
              }
            >
              Schritt löschen
            </button>
          </div>

          <div className="builder-studio-validation-strip">
            <span className={`badge badge--default ${builder.hasUnsavedChanges ? "builder-mode-badge builder-mode-badge--locked" : "builder-mode-badge builder-mode-badge--advanced"}`}>
              {builder.hasUnsavedChanges ? "Ungespeichert" : "Gespeichert"}
            </span>
            <span className="builder-validation-pill">Prüfung {totalIssueCount}</span>
          </div>
        </div>
      </section>

      <div className="builder-studio-layout">
        <aside className="builder-studio-rail builder-studio-rail--left">
          <div className="builder-studio-rail-stack">
            <section className="builder-studio-rail-panel panel content-stack">
              <div className="builder-rail-panel__header">
                <span className="builder-sidebar-panel__eyebrow">Auswahl</span>
                <h3>Abläufe</h3>
                <p className="text-muted">Wähle links den Ablauf. Der Builder öffnet direkt einen bearbeitbaren Arbeitsentwurf.</p>
              </div>
              {builder.isLoading ? <p className="text-muted">Abläufe werden geladen...</p> : null}
              <div className="builder-selection-list">
                {builder.definitions.map((definition) => (
                  <button
                    key={definition.id}
                    type="button"
                    className={`admin-workspace-tab ${builder.selectedDefinition?.id === definition.id ? "active" : ""}`}
                    onClick={() => builder.selectDefinition(definition.id)}
                  >
                    <span className="admin-workspace-tab-title">{definition.name}</span>
                    <span className="admin-workspace-tab-description">
                      {definition.description?.trim() || definition.key}
                    </span>
                  </button>
                ))}
              </div>
            </section>

            <section className="builder-studio-rail-panel panel content-stack">
              <div className="builder-rail-panel__header">
                <span className="builder-sidebar-panel__eyebrow">Kontext</span>
                <h3>Aktueller Ablauf</h3>
              </div>
              <div className="builder-summary-grid">
                <div className="panel panel-muted content-stack" style={{ gap: "0.35rem" }}>
                  <span className="badge badge--default">Ablauf</span>
                  <strong>{selectedWorkflowName}</strong>
                  <span className="text-muted">{builder.selectedDefinition?.description?.trim() || "Noch kein Ablauf gewählt."}</span>
                </div>
                <div className="panel panel-muted content-stack" style={{ gap: "0.35rem" }}>
                  <span className="badge badge--default">Arbeitsentwurf</span>
                  <strong>{selectedWorkingDraftLabel}</strong>
                  <span className="text-muted">{builder.versionDraft.name.trim() || "Der Builder arbeitet intern weiter mit Versionen, zeigt aber direkt den aktuellen Entwurf."}</span>
                </div>
              </div>
            </section>

            {renderLeftRail({
              builder,
              canManageAdvanced,
            })}
          </div>
        </aside>

        <section className="builder-studio-canvas">
          <div className="builder-canvas-panel panel">
            <div className="builder-canvas-panel__header">
              <div>
                <span className="builder-sidebar-panel__eyebrow">Canvas</span>
                <h2>Ablauf</h2>
                <p className="text-muted">
                  Lest und bearbeitet den Ablauf direkt im Canvas. Verbindungen entstehen durch Ziehen zwischen den Schritten.
                </p>
              </div>
              <div className="builder-canvas-panel__meta">
                <div className="builder-topbar-stat">
                  <span>Schritte</span>
                  <strong>{builder.versionDraft.nodes.length}</strong>
                </div>
                <div className="builder-topbar-stat">
                  <span>Wege</span>
                  <strong>{builder.versionDraft.edges.length}</strong>
                </div>
              </div>
            </div>

            {!builder.selectedDefinition || (builder.isLoadingVersion && !builder.selectedVersionSummary) ? (
              <div className="builder-empty-stage">
                <h3>{builder.selectedDefinition ? "Arbeitsentwurf wird geöffnet." : "Bitte zuerst einen Ablauf wählen oder neu anlegen."}</h3>
                <p className="text-muted">
                  {builder.selectedDefinition
                    ? "Der Builder bereitet gerade den passenden bearbeitbaren Entwurf vor."
                    : "Links wählt ihr einen Ablauf aus. Der Builder öffnet automatisch den passenden Arbeitsentwurf und zeigt dann den Canvas."}
                </p>
              </div>
            ) : (
              <div className="builder-canvas-surface">
                <ReactFlow
                  nodes={canvasNodes}
                  edges={canvasEdges}
                  nodeTypes={NODE_TYPES}
                  fitView
                  fitViewOptions={{ padding: 0.18 }}
                  onInit={(instance) => setFlowInstance(instance)}
                  onNodeClick={(_, node) => {
                    if (builder.versionDraft.nodes.some((draftNode) => draftNode.id === node.id)) {
                      builder.selectNode(node.id);
                    }
                  }}
                  onEdgeClick={(_, edge) => {
                    if (builder.versionDraft.edges.some((draftEdge) => draftEdge.id === edge.id)) {
                      builder.selectEdge(edge.id);
                    }
                  }}
                  onConnect={(connection) => builder.connectNodes(connection.source ?? null, connection.target ?? null)}
                  onNodeDragStop={(_, node) => {
                    if (builder.versionDraft.nodes.some((draftNode) => draftNode.id === node.id)) {
                      builder.updateNodePosition(node.id, node.position);
                    }
                  }}
                  onPaneClick={() => {
                    builder.selectNode(null);
                    builder.selectEdge(null);
                  }}
                  nodesDraggable={false}
                  nodesConnectable
                  elementsSelectable
                  connectOnClick={false}
                  proOptions={{ hideAttribution: true }}
                >
                  <Background gap={18} size={1} color="var(--graph-grid-color)" />
                  <MiniMap
                    pannable
                    zoomable
                    nodeColor={(node) => (node.id === builder.selectedNode?.id ? "var(--graph-edge-done)" : "var(--graph-edge-open)")}
                    style={{ background: "var(--graph-minimap-background)" }}
                  />
                  <Controls showInteractive={false} />
                </ReactFlow>
              </div>
            )}
          </div>
        </section>

        <div className="builder-studio-rail builder-studio-rail--right">
          <WorkflowBuilderSidebar
            selectedNode={builder.selectedNode}
            inspectorFocus={builder.inspectorFocus}
            availableNodes={availableNodes}
            versionDraft={builder.versionDraft}
            actionDefinitions={builder.actionDefinitions}
            processTypes={builder.processTypes}
            responsibilityOwners={builder.responsibilityOwners}
            taskTemplates={builder.taskTemplates}
            answerDefinitions={builder.answerDefinitions}
            taskTemplateConditions={builder.taskTemplateConditions}
            taskTemplateDependencies={builder.taskTemplateDependencies}
            canManageAdvanced={canManageAdvanced}
            hasVersionSelected={Boolean(builder.selectedVersionSummary)}
            localValidationIssues={builder.localValidationIssues}
            serverValidationIssues={builder.versionDetail?.validationIssues ?? []}
            onNotice={onNotice}
            onError={onError}
            onAddNode={builder.addNode}
            onOpenInspectorFocus={builder.openInspectorFocus}
            onCloseInspectorFocus={builder.closeInspectorFocus}
            onRefreshReferenceData={builder.refreshReferenceData}
            onUpdateNode={builder.updateNode}
            onUpdateEdge={builder.updateEdge}
            onRemoveEdge={builder.removeEdge}
            onAddActionFromDefinition={builder.addActionFromDefinition}
            onUpdateAction={builder.updateAction}
            onRemoveAction={builder.removeAction}
          />
        </div>
      </div>
    </div>
  );
}

function parseNodeConfig(configText: string): Record<string, unknown> | null {
  if (!configText.trim()) {
    return null;
  }

  try {
    const parsed = JSON.parse(configText) as unknown;
    if (parsed && typeof parsed === "object" && !Array.isArray(parsed)) {
      return parsed as Record<string, unknown>;
    }
  } catch {
    return null;
  }

  return null;
}

function readStringConfigValue(config: Record<string, unknown> | null, key: string): string | null {
  const value = config?.[key];
  return typeof value === "string" && value.trim() ? value.trim() : null;
}

function buildCanvasNodeSummary(
  node: WorkflowBuilderNodeDraft,
  versionDraft: WorkflowBuilderVersionDraft,
  nodesByKey: Map<string, WorkflowBuilderNodeDraft>,
  actionDefinitionsByKey: Map<string, {
    displayName: string;
    isActive: boolean;
    description?: string | null;
  }>,
  processTypesByKey: Map<string, { id: number; name: string; description: string | null }>,
  taskTemplates: Array<{
    processTypeId: number;
    title: string;
    category: string;
    defaultResponsibilityId: number | null;
    isDepartmentPhaseTask: boolean;
    isActive: boolean;
  }>,
  taskTemplatesByKey: Map<string, { title: string; description: string; defaultResponsibilityId: number | null; dueInDays: number | null }>,
  responsibilityOwnersByKey: Map<string, { responsibilityName: string }>,
  responsibilityOwnersById: Map<number, { responsibilityName: string; departmentName: string | null }>
): Omit<WorkflowBuilderCanvasNodeData, "isSelected" | "title"> {
  const config = parseNodeConfig(node.configText);
  const isMeasureNode = node.nodeType === "setup" || isMeasureGenerationNodeType(node.nodeType);
  const legacyProcessTypeKey = readStringConfigValue(config, "legacyProcessTypeKey");
  const workflowProcessTypeKey = legacyProcessTypeKey ?? (versionDraft.primaryLegacyProcessTypeKey.trim() || null);
  const legacyTemplateKey = readStringConfigValue(config, "legacyTemplateKey");
  const responsibilityKey = readStringConfigValue(config, "responsibilityKey");
  const notificationLabel = readStringConfigValue(config, "notificationLabel");
  const summaryText = readStringConfigValue(config, "summaryText");
  const firstActionKey = node.actions[0]?.actionKey.trim() || null;
  const additionalActions = Math.max(node.actions.length - 1, 0);
  const firstActionDefinition = firstActionKey
    ? actionDefinitionsByKey.get(firstActionKey.toLowerCase()) ?? null
    : null;
  const firstActionLabel = firstActionDefinition?.displayName ?? firstActionKey;
  const processType = workflowProcessTypeKey
    ? processTypesByKey.get(workflowProcessTypeKey.toLowerCase()) ?? null
    : null;
  const taskTemplate = legacyTemplateKey
    ? taskTemplatesByKey.get(legacyTemplateKey.toLowerCase()) ?? null
    : null;
  const configuredResponsibility = responsibilityKey
    ? responsibilityOwnersByKey.get(responsibilityKey.toLowerCase()) ?? null
    : null;
  const responsibleLabel = configuredResponsibility?.responsibilityName
    ?? findResponsibilityNameById(taskTemplate?.defaultResponsibilityId ?? null, responsibilityOwnersById)
    ?? (node.nodeType === "automation"
      || node.nodeType === "decision"
      || node.nodeType === "parallel_split"
      || node.nodeType === "parallel_join"
      || node.nodeType === "start"
      || node.nodeType === "end"
      ? "System"
      : "Noch nicht festgelegt");
  const dueLabel = taskTemplate?.dueInDays
    ? formatDueInDays(taskTemplate.dueInDays)
    : null;
  const nextStepLabel = buildNextStepLabel(node, versionDraft, nodesByKey);
  const measureDetailItems = isMeasureNode
    ? buildMeasureDetailItems(processType?.id ?? null, taskTemplates, responsibilityOwnersById)
    : [];
  const modeLabel = isMeasureNode
    ? "Parallel"
    : isAutomaticNodeType(node.nodeType)
      ? "Automatisch"
      : "Manuell";

  switch (node.nodeType) {
    case "start":
      return {
        nodeType: node.nodeType,
        typeLabel: getWorkflowBuilderNodeTypeLabel(node.nodeType),
        modeLabel,
        responsibleLabel,
        notificationLabel: null,
        dueLabel: null,
        effectText: summaryText ?? "Hier beginnt der Ablauf und der erste Schritt wird vorbereitet.",
        nextStepLabel,
      };
    case "end":
      return {
        nodeType: node.nodeType,
        typeLabel: getWorkflowBuilderNodeTypeLabel(node.nodeType),
        modeLabel,
        responsibleLabel,
        notificationLabel: null,
        dueLabel: null,
        effectText: summaryText ?? "Hier endet dieser Ablaufpfad.",
        nextStepLabel,
      };
    case "form":
      return {
        nodeType: node.nodeType,
        typeLabel: getWorkflowBuilderNodeTypeLabel(node.nodeType),
        modeLabel,
        responsibleLabel,
        notificationLabel,
        dueLabel: null,
        effectText: summaryText
          ?? processType?.description?.trim()
          ?? "Hier werden die benötigten Angaben für den weiteren Ablauf erfasst.",
        detailItems: [],
        nextStepLabel,
      };
    case "approval":
      return {
        nodeType: node.nodeType,
        typeLabel: getWorkflowBuilderNodeTypeLabel(node.nodeType),
        modeLabel,
        responsibleLabel,
        notificationLabel,
        dueLabel,
        effectText: summaryText
          ?? taskTemplate?.description?.trim()
          ?? "Hier wird eine Freigabe für den weiteren Ablauf eingeholt.",
        detailItems: [],
        nextStepLabel,
      };
    case "measure_provision":
    case "measure_deprovision":
    case "measure_change":
    case "measure_rename":
    case "setup":
      return {
        nodeType: node.nodeType,
        typeLabel: getWorkflowBuilderNodeTypeLabel(node.nodeType),
        modeLabel,
        responsibleLabel: measureDetailItems.length > 1 ? "Mehrere Bereiche" : measureDetailItems[0] ?? "Bereiche",
        notificationLabel: null,
        dueLabel: null,
        effectText: summaryText
          ?? buildMeasureEffectText(node.nodeType, workflowProcessTypeKey),
        detailItems: measureDetailItems,
        nextStepLabel,
      };
    case "decision":
      return {
        nodeType: node.nodeType,
        typeLabel: getWorkflowBuilderNodeTypeLabel(node.nodeType),
        modeLabel,
        responsibleLabel,
        notificationLabel: null,
        dueLabel: null,
        effectText: summaryText ?? "Hier wird entschieden, welcher Weg danach weiterläuft.",
        detailItems: [],
        nextStepLabel,
      };
    case "parallel_split":
      return {
        nodeType: node.nodeType,
        typeLabel: getWorkflowBuilderNodeTypeLabel(node.nodeType),
        modeLabel,
        responsibleLabel,
        notificationLabel: null,
        dueLabel: null,
        effectText: "Hier verzweigt sich der Ablauf in mehrere parallele Pfade.",
        detailItems: [],
        nextStepLabel,
      };
    case "parallel_join":
      return {
        nodeType: node.nodeType,
        typeLabel: getWorkflowBuilderNodeTypeLabel(node.nodeType),
        modeLabel,
        responsibleLabel,
        notificationLabel: null,
        dueLabel: null,
        effectText: "Hier laufen parallele Pfade wieder in einem gemeinsamen Schritt zusammen.",
        detailItems: [],
        nextStepLabel,
      };
    case "automation":
      return {
        nodeType: node.nodeType,
        typeLabel: getWorkflowBuilderNodeTypeLabel(node.nodeType),
        modeLabel,
        responsibleLabel,
        notificationLabel,
        dueLabel: null,
        effectText: summaryText
          ?? buildAutomationEffectText(firstActionLabel, additionalActions, firstActionDefinition?.description ?? null),
        detailItems: [],
        nextStepLabel,
      };
    case "task":
    default:
      return {
        nodeType: node.nodeType,
        typeLabel: getWorkflowBuilderNodeTypeLabel(node.nodeType),
        modeLabel,
        responsibleLabel,
        notificationLabel,
        dueLabel,
        effectText: summaryText
          ?? taskTemplate?.description?.trim()
          ?? "Hier wird eine Aufgabe im Ablauf bearbeitet.",
        detailItems: [],
        nextStepLabel,
      };
  }
}

function renderLeftRail({
  builder,
  canManageAdvanced,
}: {
  builder: ReturnType<typeof useAdminWorkflowBuilder>;
  canManageAdvanced: boolean;
}) {
  return (
    <details className="builder-admin-disclosure panel">
      <summary>Verwaltung</summary>
      <div className="content-stack">
        {canManageAdvanced ? (
          <>
            <section className="content-stack">
              <div className="builder-rail-panel__header">
                <span className="builder-sidebar-panel__eyebrow">Neu</span>
                <h3>Ablauf anlegen</h3>
              </div>
              <label>
                <span>Name</span>
                <input
                  className="form-input"
                  value={builder.newDefinitionDraft.name}
                  onChange={(event) => builder.updateNewDefinitionDraft("name", event.target.value)}
                />
              </label>
              <label>
                <span>Beschreibung</span>
                <textarea
                  className="form-input"
                  rows={3}
                  value={builder.newDefinitionDraft.description}
                  onChange={(event) => builder.updateNewDefinitionDraft("description", event.target.value)}
                />
              </label>
              <button
                type="button"
                className="button-secondary builder-action-button builder-action-button--secondary"
                onClick={() => void builder.createDefinition()}
                disabled={builder.isCreatingDefinition}
              >
                Ablauf anlegen
              </button>
            </section>
          </>
        ) : (
          <div className="panel panel-info builder-lock-card">
            <p className="panel-text">
              Neue Abläufe, Löschen, Automatisierungen und Freigabe bleiben dem Admin-Modus vorbehalten.
            </p>
          </div>
        )}

        {builder.selectedDefinition ? (
          <section className="content-stack">
            <div className="builder-rail-panel__header">
              <span className="builder-sidebar-panel__eyebrow">Erweitert</span>
              <h3>Details zur Ablaufvorlage</h3>
            </div>
            <label>
              <span>Name</span>
              <input
                className="form-input"
                value={builder.definitionDraft.name}
                disabled={!canManageAdvanced}
                onChange={(event) => builder.updateDefinitionDraft("name", event.target.value)}
              />
            </label>
            <label>
              <span>Beschreibung</span>
              <textarea
                className="form-input"
                rows={3}
                value={builder.definitionDraft.description}
                disabled={!canManageAdvanced}
                onChange={(event) => builder.updateDefinitionDraft("description", event.target.value)}
              />
            </label>
          </section>
        ) : null}

        {builder.selectedVersionSummary ? (
          <section className="content-stack">
            <div className="builder-rail-panel__header">
              <span className="builder-sidebar-panel__eyebrow">Erweitert</span>
              <h3>Details zum Arbeitsentwurf</h3>
            </div>
            {builder.isLoadingVersion ? <p className="text-muted">Stand wird geladen...</p> : null}
            <label>
              <span>Name</span>
              <input
                className="form-input"
                value={builder.versionDraft.name}
                onChange={(event) => builder.updateVersionDraftField("name", event.target.value)}
              />
            </label>
            <label>
              <span>Beschreibung</span>
              <textarea
                className="form-input"
                rows={3}
                value={builder.versionDraft.description}
                onChange={(event) => builder.updateVersionDraftField("description", event.target.value)}
              />
            </label>
            <label>
              <span>{WORKFLOW_BUILDER_TECHNICAL_LABELS.processTypeKey}</span>
              <input
                className="form-input"
              value={builder.versionDraft.primaryLegacyProcessTypeKey}
              onChange={(event) => builder.updateVersionDraftField("primaryLegacyProcessTypeKey", event.target.value)}
            />
          </label>
          <div className="panel panel-muted content-stack" style={{ gap: "0.35rem" }}>
            <span className="badge badge--default">Versionen intern</span>
            <span className="text-muted">
              {builder.selectedDefinition?.versions.map((version) =>
                `${formatVersionStatus(version.status)} ${version.versionNumber}`
              ).join(" · ") || "Noch keine internen Versionen sichtbar."}
            </span>
          </div>
          </section>
        ) : null}
      </div>
    </details>
  );
}

function isAutomaticNodeType(nodeType: WorkflowBuilderNodeDraft["nodeType"]) {
  return [
    "start",
    "end",
    "decision",
    "automation",
    "parallel_split",
    "parallel_join",
  ].includes(nodeType);
}

function buildAutomationEffectText(firstActionLabel: string | null, additionalActions: number, actionDescription: string | null) {
  if (!firstActionLabel) {
    return "Dieser Schritt führt automatische Systemaktionen aus, sobald er erreicht wird.";
  }

  if (actionDescription?.trim()) {
    return actionDescription.trim();
  }

  if (additionalActions > 0) {
    return `${firstActionLabel} startet zusammen mit ${additionalActions} weiteren automatischen Aktion(en).`;
  }

  return `${firstActionLabel} wird automatisch ausgeführt.`;
}

function buildNextStepLabel(
  node: WorkflowBuilderNodeDraft,
  versionDraft: WorkflowBuilderVersionDraft,
  nodesByKey: Map<string, WorkflowBuilderNodeDraft>
) {
  const normalizedNodeKey = node.nodeKey.trim().toLowerCase();
  const outgoingEdges = versionDraft.edges.filter((edge) => edge.sourceNodeKey.trim().toLowerCase() === normalizedNodeKey);

  if (outgoingEdges.length === 0) {
    return "Noch kein Folgeschritt";
  }

  if (outgoingEdges.length === 1) {
    const nextNode = nodesByKey.get(outgoingEdges[0]!.targetNodeKey.trim().toLowerCase()) ?? null;
    const nextLabel = nextNode?.title.trim() || nextNode?.nodeKey.trim() || "nächster Schritt";
    return nextLabel;
  }

  const namedTargets = outgoingEdges
    .map((edge) => nodesByKey.get(edge.targetNodeKey.trim().toLowerCase()) ?? null)
    .filter((entry): entry is WorkflowBuilderNodeDraft => Boolean(entry))
    .map((entry) => entry.title.trim() || entry.nodeKey.trim())
    .filter((entry) => Boolean(entry));

  if (namedTargets.length > 0) {
    return `${namedTargets.slice(0, 2).join(" / ")}${namedTargets.length > 2 ? " ..." : ""}`;
  }

  if (node.nodeType === "parallel_split") {
    return `${outgoingEdges.length} parallele Wege`;
  }

  return `${outgoingEdges.length} mögliche Wege`;
}

function findResponsibilityNameById(
  responsibilityId: number | null,
  responsibilityOwnersById: Map<number, { responsibilityName: string }>
) {
  if (!responsibilityId) {
    return null;
  }

  return responsibilityOwnersById.get(responsibilityId)?.responsibilityName ?? null;
}

function formatDueInDays(dueInDays: number) {
  if (dueInDays === 1) {
    return "1 Tag";
  }

  return `${dueInDays} Tage`;
}

function buildMeasureEffectText(
  nodeType: WorkflowBuilderNodeDraft["nodeType"],
  processTypeKey: string | null
) {
  switch (processTypeKey) {
    case "name_change":
      return "Erzeugt aus neuem Namen und Wirksamkeitsdatum Umbenennungsmaßnahmen für Identitäts-, Mail- und Verzeichnisdaten.";
    case "position_change":
      return "Erzeugt aus Position und angefordertem Wechselumfang positionsbezogene Berechtigungs-, Zugriffs-, Schulungs- und Systemanpassungen.";
    case "role_change":
      return "Erzeugt aus neuer Rolle und angeforderten Änderungsumfängen rollenbezogene Rollen-, Berechtigungs- und Systemanpassungen.";
    default:
      switch (nodeType) {
        case "measure_provision":
          return "Erzeugt aus den erfassten Anforderungen Bereitstellungsmaßnahmen für betroffene Bereiche und startet die parallele Abarbeitung.";
        case "measure_deprovision":
          return "Erzeugt aus dem erfassten Umfang Entzugsmaßnahmen für betroffene Bereiche und startet die parallele Abarbeitung.";
        case "measure_change":
          return "Erzeugt aus dem erfassten Änderungsumfang Anpassungsmaßnahmen für betroffene Bereiche und startet die parallele Abarbeitung.";
        case "measure_rename":
          return "Erzeugt aus dem erfassten Umbenennungsumfang Maßnahmen für Identitäts- und Systemdaten.";
        case "setup":
        default:
          return "Bündelt interne Maßnahmen als Legacy-Setup-Block und startet die parallele Abarbeitung.";
      }
  }
}

function buildMeasureDetailItems(
  processTypeId: number | null,
  taskTemplates: Array<{
    processTypeId: number;
    title: string;
    category: string;
    defaultResponsibilityId: number | null;
    isDepartmentPhaseTask: boolean;
    isActive: boolean;
  }>,
  responsibilityOwnersById: Map<number, { responsibilityName: string; departmentName: string | null }>
) {
  if (!processTypeId) {
    return [];
  }

  const modulesByArea = new Map<string, Set<string>>();
  for (const template of taskTemplates) {
    if (template.processTypeId !== processTypeId || !template.isActive || !template.isDepartmentPhaseTask) {
      continue;
    }

    const owner = template.defaultResponsibilityId
      ? responsibilityOwnersById.get(template.defaultResponsibilityId) ?? null
      : null;
    const areaLabel = owner?.departmentName?.trim() || owner?.responsibilityName?.trim() || "Bereiche";
    const moduleLabel = buildMeasureModuleLabel(template.title, template.category);
    const modules = modulesByArea.get(areaLabel) ?? new Set<string>();
    modules.add(moduleLabel);
    modulesByArea.set(areaLabel, modules);
  }

  return [...modulesByArea.entries()]
    .slice(0, 4)
    .map(([areaLabel, modules]) => {
      const compactModules = [...modules].slice(0, 4);
      return `${areaLabel}: ${compactModules.join(", ")}${modules.size > compactModules.length ? " ..." : ""}`;
    });
}

function buildMeasureModuleLabel(title: string, category: string) {
  const normalizedTitle = title.trim();
  if (!normalizedTitle) {
    return category.trim() || "Modul";
  }

  const titleWithoutActionPrefix = normalizedTitle
    .replace(/^(Anlegen|Einrichten|Bereitstellen|Vorbereiten|Aktualisieren|Vergeben|Übernehmen|Uebernehmen|Deaktivieren|Sperren|Entziehen|Einziehen|Tauschen|Ändern|Aendern)\s+/i, "")
    .replace(/\s+(anlegen|einrichten|bereitstellen|vorbereiten|aktualisieren|vergeben|uebernehmen|übernehmen|deaktivieren|sperren|entziehen|einziehen|tauschen|ändern|aendern)$/i, "")
    .replace(/\s+(für|fuer)\s+.*$/i, "")
    .trim();

  if (/ad-berecht/i.test(titleWithoutActionPrefix)) {
    return "Rechte";
  }

  if (/mailbox/i.test(titleWithoutActionPrefix)) {
    return "Mail";
  }

  if (/hardware|telefon/i.test(titleWithoutActionPrefix)) {
    return "Hardware";
  }

  if (/ad-user|ad-konto|ad-gruppen/i.test(titleWithoutActionPrefix)) {
    return "AD";
  }

  return titleWithoutActionPrefix
    .replace(/\s+-\s+/g, " ")
    .replace(/\s{2,}/g, " ")
    .trim();
}

function formatVersionStatus(status: string) {
  switch (status.trim().toLowerCase()) {
    case "draft":
      return "Entwurf";
    case "published":
      return "Freigabe";
    default:
      return "Stand";
  }
}
