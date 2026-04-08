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
import type { WorkflowBuilderNodeDraft, WorkflowBuilderVersionDraft } from "../../hooks/adminWorkflowBuilderModel";
import { getWorkflowBuilderNodeTypeLabel, WORKFLOW_BUILDER_TECHNICAL_LABELS } from "./workflowBuilderLabels";
import {
  WorkflowBuilderCanvasNode,
  type WorkflowBuilderCanvasNodeData,
} from "./WorkflowBuilderCanvasNode";
import { WorkflowBuilderSidebar } from "./WorkflowBuilderSidebar";

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

  const canvasNodes = useMemo<Node<WorkflowBuilderCanvasNodeData>[]>(() => {
    return builder.versionDraft.nodes.map((node) => ({
      id: node.id,
      type: "workflowBuilderNode",
      position: {
        x: node.positionX ?? 0,
        y: node.positionY ?? 0,
      },
      data: {
        ...buildCanvasNodeSummary(
          node,
          builder.versionDraft,
          nodesByKey,
          actionDefinitionsByKey,
          processTypesByKey,
          taskTemplatesByKey,
          responsibilityOwnersByKey,
          responsibilityOwnersById
        ),
        title: node.title.trim() || node.nodeKey.trim() || "Neuer Schritt",
        isSelected: builder.selectedNode?.id === node.id,
      },
    }));
  }, [
    actionDefinitionsByKey,
    builder.selectedNode?.id,
    builder.versionDraft,
    nodesByKey,
    processTypesByKey,
    responsibilityOwnersById,
    responsibilityOwnersByKey,
    taskTemplatesByKey,
  ]);

  const canvasEdges = useMemo<Edge[]>(() => {
    const nodeIdByKey = new Map<string, string>();
    for (const node of builder.versionDraft.nodes) {
      const normalizedKey = node.nodeKey.trim().toLowerCase();
      if (normalizedKey) {
        nodeIdByKey.set(normalizedKey, node.id);
      }
    }

    const nextEdges: Edge[] = [];
    for (const edge of builder.versionDraft.edges) {
      const source = nodeIdByKey.get(edge.sourceNodeKey.trim().toLowerCase());
      const target = nodeIdByKey.get(edge.targetNodeKey.trim().toLowerCase());
      if (!source || !target) {
        continue;
      }

      const isSelected = builder.selectedEdge?.id === edge.id;

      nextEdges.push({
        id: edge.id,
        source,
        target,
        animated: false,
        selectable: true,
        label: getEdgeLabel(edge),
        markerEnd: {
          type: MarkerType.ArrowClosed,
          width: 20,
          height: 20,
          color: isSelected ? "var(--graph-edge-done)" : "var(--graph-edge-open)",
        },
        style: {
          stroke: isSelected ? "var(--graph-edge-done)" : "var(--graph-edge-open)",
          strokeWidth: isSelected ? 3 : 2,
        },
        labelStyle: {
          fill: "var(--graph-node-title)",
          fontSize: 12,
          fontWeight: 600,
        },
        labelBgPadding: [8, 4] as [number, number],
        labelBgBorderRadius: 999,
        labelBgStyle: {
          fill: isSelected ? "rgba(219, 234, 254, 0.96)" : "rgba(255,255,255,0.92)",
          fillOpacity: 1,
          stroke: isSelected ? "rgba(59, 130, 246, 0.95)" : "rgba(148, 163, 184, 0.7)",
        },
      });
    }

    return nextEdges;
  }, [builder.selectedEdge?.id, builder.versionDraft.edges, builder.versionDraft.nodes]);

  const selectedWorkflowName = builder.selectedDefinition?.name ?? "Ablauf waehlen";
  const selectedStandLabel = builder.selectedVersionSummary
    ? `${formatVersionStatus(builder.selectedVersionSummary.status)} ${builder.selectedVersionSummary.versionNumber}`
    : "Stand waehlen";
  const publishDisabledReason = !canManageAdvanced
    ? "Freigeben ist nur im Admin-Modus erlaubt."
    : builder.hasUnsavedChanges
      ? "Bitte erst speichern."
      : !builder.selectedVersionSummary?.canPublish
        ? "Dieser Stand ist noch nicht freigabefaehig."
        : undefined;
  const canDeleteSelectedNode = Boolean(
    builder.selectedVersionSummary
    && builder.selectedNode
    && (canManageAdvanced || builder.selectedNode.nodeType !== "automation")
  );
  const workspaceStatusLabel = builder.selectedNode
    ? `Eigenschaften offen fuer ${builder.selectedNode.title.trim() || builder.selectedNode.nodeKey.trim() || "Schritt"}`
    : builder.selectedVersionSummary
      ? "Bausteine sichtbar"
      : "Noch kein Stand geoeffnet";
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
              Baue und lies Ablaeufe direkt im Canvas. Auswahl links, Ablauf in der Mitte, Eigenschaften rechts.
            </p>
          </div>

          <div className="builder-studio-topbar__status">
            <div className="builder-topbar-stat">
              <span>Ablauf</span>
              <strong>{selectedWorkflowName}</strong>
            </div>
            <div className="builder-topbar-stat">
              <span>Stand</span>
              <strong>{selectedStandLabel}</strong>
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
              className="button-primary"
              onClick={() => void builder.saveVersion()}
              disabled={!builder.selectedVersionSummary || builder.isSaving || builder.isLoadingVersion}
            >
              Speichern
            </button>
            <button
              type="button"
              className="button-secondary"
              onClick={builder.validateDraft}
              disabled={!builder.selectedVersionSummary}
            >
              Pruefen
            </button>
            <button
              type="button"
              className="button-secondary"
              onClick={() => void builder.publishVersion()}
              disabled={!canManageAdvanced || !builder.selectedVersionSummary?.canPublish || builder.isPublishing || builder.hasUnsavedChanges}
              title={publishDisabledReason}
            >
              Freigeben
            </button>
          </div>

          <div className="builder-studio-action-group builder-studio-action-group--secondary">
            <button type="button" className="button-secondary" onClick={builder.autoLayoutNodes} disabled={!builder.selectedVersionSummary}>
              Anordnen
            </button>
            <button
              type="button"
              className="button-secondary"
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
              className="button-danger"
              onClick={builder.removeSelectedNode}
              disabled={!canDeleteSelectedNode}
              title={
                builder.selectedNode
                  ? canDeleteSelectedNode
                    ? "Ausgewaehlten Schritt loeschen"
                    : "Automatisierungen koennen nur im Admin-Modus geloescht werden."
                  : "Bitte zuerst einen Schritt im Canvas auswaehlen."
              }
            >
              Schritt loeschen
            </button>
          </div>

          <div className="builder-studio-validation-strip">
            <span className={`badge badge--default ${builder.hasUnsavedChanges ? "builder-mode-badge builder-mode-badge--locked" : "builder-mode-badge builder-mode-badge--advanced"}`}>
              {builder.hasUnsavedChanges ? "Ungespeichert" : "Gespeichert"}
            </span>
            <span className="builder-validation-pill">Pruefung {totalIssueCount}</span>
          </div>
        </div>
      </section>

      <div className="builder-studio-layout">
        <aside className="builder-studio-rail builder-studio-rail--left">
          <div className="builder-studio-rail-stack">
            <section className="builder-studio-rail-panel panel content-stack">
              <div className="builder-rail-panel__header">
                <span className="builder-sidebar-panel__eyebrow">Auswahl</span>
                <h3>Ablaufe</h3>
                <p className="text-muted">Waehle links den Ablauf, den ihr im Canvas bearbeiten wollt.</p>
              </div>
              {builder.isLoading ? <p className="text-muted">Ablaufe werden geladen...</p> : null}
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
                <span className="builder-sidebar-panel__eyebrow">Stand</span>
                <h3>Bearbeitungsstand</h3>
                <p className="text-muted">Hier waehlt ihr den Stand, der im Canvas geoeffnet wird.</p>
              </div>
              <div className="builder-selection-list">
                {builder.selectedDefinition?.versions.map((version) => (
                  <button
                    key={version.id}
                    type="button"
                    className={`admin-workspace-tab ${builder.selectedVersionSummary?.id === version.id ? "active" : ""}`}
                    onClick={() => builder.selectVersion(version.id)}
                  >
                    <span className="admin-workspace-tab-title">
                      {formatVersionStatus(version.status)} {version.versionNumber}
                    </span>
                    <span className="admin-workspace-tab-description">
                      {version.name || "Ohne Namen"} · {version.validationIssues.length} Hinweis(e)
                    </span>
                  </button>
                )) ?? <p className="text-muted">Bitte zuerst einen Ablauf auswaehlen.</p>}
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
                  <span className="text-muted">{builder.selectedDefinition?.description?.trim() || "Noch kein Ablauf gewaehlt."}</span>
                </div>
                <div className="panel panel-muted content-stack" style={{ gap: "0.35rem" }}>
                  <span className="badge badge--default">Stand</span>
                  <strong>{selectedStandLabel}</strong>
                  <span className="text-muted">{builder.versionDraft.name.trim() || "Noch kein Stand geoeffnet."}</span>
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

            {!builder.selectedVersionSummary ? (
              <div className="builder-empty-stage">
                <h3>Bitte zuerst einen Ablauf und einen Stand waehlen.</h3>
                <p className="text-muted">
                  Links oeffnet ihr zuerst den Ablauf und den gewuenschten Stand. Danach wird der Ablauf im Canvas sichtbar.
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
                  onNodeClick={(_, node) => builder.selectNode(node.id)}
                  onEdgeClick={(_, edge) => builder.selectEdge(edge.id)}
                  onConnect={(connection) => builder.connectNodes(connection.source ?? null, connection.target ?? null)}
                  onNodeDragStop={(_, node) => builder.updateNodePosition(node.id, node.position)}
                  onPaneClick={() => {
                    builder.selectNode(null);
                    builder.selectEdge(null);
                  }}
                  nodesDraggable
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
            availableNodes={availableNodes}
            versionDraft={builder.versionDraft}
            actionDefinitions={builder.actionDefinitions}
            responsibilityOwners={builder.responsibilityOwners}
            canManageAdvanced={canManageAdvanced}
            hasVersionSelected={Boolean(builder.selectedVersionSummary)}
            localValidationIssues={builder.localValidationIssues}
            serverValidationIssues={builder.versionDetail?.validationIssues ?? []}
            onAddNode={builder.addNode}
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
  processTypesByKey: Map<string, { name: string; description: string | null }>,
  taskTemplatesByKey: Map<string, { title: string; description: string; defaultResponsibilityId: number | null; dueInDays: number | null }>,
  responsibilityOwnersByKey: Map<string, { responsibilityName: string }>,
  responsibilityOwnersById: Map<number, { responsibilityName: string }>
): Omit<WorkflowBuilderCanvasNodeData, "isSelected" | "title"> {
  const config = parseNodeConfig(node.configText);
  const legacyProcessTypeKey = readStringConfigValue(config, "legacyProcessTypeKey");
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
  const processType = legacyProcessTypeKey
    ? processTypesByKey.get(legacyProcessTypeKey.toLowerCase()) ?? null
    : null;
  const taskTemplate = legacyTemplateKey
    ? taskTemplatesByKey.get(legacyTemplateKey.toLowerCase()) ?? null
    : null;
  const configuredResponsibility = responsibilityKey
    ? responsibilityOwnersByKey.get(responsibilityKey.toLowerCase()) ?? null
    : null;
  const responsibleLabel = configuredResponsibility?.responsibilityName
    ?? findResponsibilityNameById(taskTemplate?.defaultResponsibilityId ?? null, responsibilityOwnersById)
    ?? (node.nodeType === "automation" || node.nodeType === "decision" || node.nodeType === "start" || node.nodeType === "end"
      ? "System"
      : "Noch nicht festgelegt");
  const dueLabel = taskTemplate?.dueInDays
    ? formatDueInDays(taskTemplate.dueInDays)
    : null;
  const nextStepLabel = buildNextStepLabel(node, versionDraft, nodesByKey);
  const modeLabel = isAutomaticNodeType(node.nodeType) ? "Automatisch" : "Manuell";

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
          ?? "Hier werden die benoetigten Angaben fuer den weiteren Ablauf erfasst.",
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
          ?? "Hier wird eine Freigabe fuer den weiteren Ablauf eingeholt.",
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
        effectText: summaryText ?? "Hier wird entschieden, welcher Weg danach weiterlaeuft.",
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
                <span>Technischer Ablauf-Key</span>
                <input
                  className="form-input"
                  value={builder.newDefinitionDraft.key}
                  onChange={(event) => builder.updateNewDefinitionDraft("key", event.target.value)}
                />
              </label>
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
                className="button-secondary"
                onClick={() => void builder.createDefinition()}
                disabled={builder.isCreatingDefinition}
              >
                Ablauf anlegen
              </button>
            </section>

            <section className="content-stack">
              <div className="builder-rail-panel__header">
                <span className="builder-sidebar-panel__eyebrow">Neu</span>
                <h3>Stand anlegen</h3>
              </div>
              <label>
                <span>Name</span>
                <input
                  className="form-input"
                  value={builder.newVersionDraft.name}
                  onChange={(event) => builder.updateNewVersionDraft("name", event.target.value)}
                />
              </label>
              <label>
                <span>Beschreibung</span>
                <textarea
                  className="form-input"
                  rows={3}
                  value={builder.newVersionDraft.description}
                  onChange={(event) => builder.updateNewVersionDraft("description", event.target.value)}
                />
              </label>
              <button
                type="button"
                className="button-secondary"
                onClick={() => void builder.createVersion()}
                disabled={!builder.selectedDefinition || builder.isCreatingVersion}
              >
                Stand anlegen
              </button>
            </section>
          </>
        ) : (
          <div className="panel panel-info builder-lock-card">
            <p className="panel-text">
              Neue Ablaeufe, neue Staende, Automatisierungen und Freigabe bleiben dem Admin-Modus vorbehalten.
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
              <h3>Details zum Stand</h3>
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
          </section>
        ) : null}
      </div>
    </details>
  );
}

function isAutomaticNodeType(nodeType: WorkflowBuilderNodeDraft["nodeType"]) {
  return nodeType === "start" || nodeType === "end" || nodeType === "decision" || nodeType === "automation";
}

function buildAutomationEffectText(firstActionLabel: string | null, additionalActions: number, actionDescription: string | null) {
  if (!firstActionLabel) {
    return "Dieser Schritt fuehrt automatische Systemaktionen aus, sobald er erreicht wird.";
  }

  if (actionDescription?.trim()) {
    return actionDescription.trim();
  }

  if (additionalActions > 0) {
    return `${firstActionLabel} startet zusammen mit ${additionalActions} weiteren automatischen Aktion(en).`;
  }

  return `${firstActionLabel} wird automatisch ausgefuehrt.`;
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
    const nextLabel = nextNode?.title.trim() || nextNode?.nodeKey.trim() || "naechster Schritt";
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

  return `${outgoingEdges.length} moegliche Wege`;
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

function getEdgeLabel(edge: WorkflowBuilderVersionDraft["edges"][number]) {
  const expression = edge.conditionExpression.trim();
  if (!expression) {
    const priority = Number(edge.priority);
    return Number.isInteger(priority) && priority > 1 ? `Pfad ${priority}` : "Weiter";
  }

  try {
    const parsed = JSON.parse(expression) as { operator?: string } | null;
    switch (parsed?.operator) {
      case "is_true":
        return "Ja";
      case "is_false":
        return "Nein";
      default:
        return "Bedingung";
    }
  } catch {
    return "Bedingung";
  }
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
