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
import type { WorkflowBuilderNodeDraft } from "../../hooks/adminWorkflowBuilderModel";
import { getWorkflowBuilderNodeTypeLabel, WORKFLOW_BUILDER_TECHNICAL_LABELS } from "./workflowBuilderLabels";
import {
  WorkflowBuilderCanvasNode,
  type WorkflowBuilderCanvasNodeData,
} from "./WorkflowBuilderCanvasNode";
import { WorkflowBuilderSidebar } from "./WorkflowBuilderSidebar";

type AdminWorkflowBuilderSectionProps = {
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
};

const NODE_TYPES: NodeTypes = {
  workflowBuilderNode: WorkflowBuilderCanvasNode,
};

type NodeEdgeCounts = {
  incomingCount: number;
  outgoingCount: number;
};

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
  counts: NodeEdgeCounts,
  actionDefinitionsByKey: Map<string, {
    displayName: string;
    isActive: boolean;
  }>
): Omit<WorkflowBuilderCanvasNodeData, "isSelected" | "title"> {
  const config = parseNodeConfig(node.configText);
  const legacyProcessTypeKey = readStringConfigValue(config, "legacyProcessTypeKey");
  const legacyTemplateKey = readStringConfigValue(config, "legacyTemplateKey");
  const firstActionKey = node.actions[0]?.actionKey.trim() || null;
  const additionalActions = Math.max(node.actions.length - 1, 0);
  const firstActionDefinition = firstActionKey
    ? actionDefinitionsByKey.get(firstActionKey.toLowerCase()) ?? null
    : null;
  const firstActionLabel = firstActionDefinition?.displayName ?? firstActionKey;

  switch (node.nodeType) {
    case "start":
      return {
        nodeType: node.nodeType,
        displayTypeLabel: getWorkflowBuilderNodeTypeLabel(node.nodeType),
        primaryHint: "Startet den Workflow und oeffnet den ersten Pfad.",
        secondaryHint: counts.outgoingCount > 0 ? "Startpfad ist verbunden." : "Noch kein Folgeschritt verbunden.",
        statusTone: counts.outgoingCount > 0 ? "success" : "warning",
        statusText: counts.outgoingCount > 0 ? "Bereit" : "Offen",
        incomingCount: counts.incomingCount,
        outgoingCount: counts.outgoingCount,
      };
    case "end":
      return {
        nodeType: node.nodeType,
        displayTypeLabel: getWorkflowBuilderNodeTypeLabel(node.nodeType),
        primaryHint: "Beendet diesen Workflow-Pfad.",
        secondaryHint: counts.incomingCount > 0 ? "Mindestens ein Pfad fuehrt hierhin." : "Noch kein Pfad fuehrt ins Ende.",
        statusTone: counts.incomingCount > 0 ? "success" : "warning",
        statusText: counts.incomingCount > 0 ? "Verbunden" : "Offen",
        incomingCount: counts.incomingCount,
        outgoingCount: counts.outgoingCount,
      };
    case "form":
      return {
        nodeType: node.nodeType,
        displayTypeLabel: getWorkflowBuilderNodeTypeLabel(node.nodeType),
        primaryHint: legacyProcessTypeKey
          ? `Formular fuer Prozess: ${legacyProcessTypeKey}`
          : "Erfasst Eingaben fuer den weiteren Ablauf.",
        secondaryHint: legacyProcessTypeKey
          ? "Das Formular ist an einen fachlichen Prozess gebunden."
          : "Dem Formular fehlt noch ein Process-Type-Hinweis.",
        statusTone: legacyProcessTypeKey ? "success" : "warning",
        statusText: legacyProcessTypeKey ? "Konfiguriert" : "Fehlt",
        incomingCount: counts.incomingCount,
        outgoingCount: counts.outgoingCount,
      };
    case "approval":
      return {
        nodeType: node.nodeType,
        displayTypeLabel: getWorkflowBuilderNodeTypeLabel(node.nodeType),
        primaryHint: legacyTemplateKey
          ? `Freigabe ueber Vorlage: ${legacyTemplateKey}`
          : "Fordert eine Freigabe fuer den naechsten Schritt an.",
        secondaryHint: legacyTemplateKey
          ? "Die Entscheidung wird ueber eine fachliche Vorlage gesteuert."
          : "Dieser Freigabe-Schritt ist noch nicht mit einer Vorlage verknuepft.",
        statusTone: legacyTemplateKey ? "success" : "warning",
        statusText: legacyTemplateKey ? "Bereit" : "Fehlt",
        incomingCount: counts.incomingCount,
        outgoingCount: counts.outgoingCount,
      };
    case "decision":
      return {
        nodeType: node.nodeType,
        displayTypeLabel: getWorkflowBuilderNodeTypeLabel(node.nodeType),
        primaryHint: "Lenkt den Workflow ueber Bedingungen und Pfade.",
        secondaryHint: counts.outgoingCount > 0
          ? `${counts.outgoingCount} Bedingung(en) bzw. Pfade sind verbunden.`
          : "Noch keine Bedingungen oder Folgepfade verbunden.",
        statusTone: counts.outgoingCount >= 2 ? "info" : "warning",
        statusText: counts.outgoingCount >= 2 ? "Verzweigt" : "Unvollstaendig",
        incomingCount: counts.incomingCount,
        outgoingCount: counts.outgoingCount,
      };
    case "automation":
      return {
        nodeType: node.nodeType,
        displayTypeLabel: getWorkflowBuilderNodeTypeLabel(node.nodeType),
        primaryHint: firstActionLabel
          ? additionalActions > 0
            ? `Startet ${firstActionLabel} + ${additionalActions} weitere Action(s).`
            : `Startet Action: ${firstActionLabel}`
          : "Fuehrt automatische Schritte ohne manuelle Aktion aus.",
        secondaryHint: firstActionLabel
          ? firstActionDefinition
            ? firstActionDefinition.isActive
              ? "Der technische Schritt ist fuer den sichtbaren Action Layer vorbereitet."
              : "Mindestens eine konfigurierte Action ist im Katalog inaktiv."
            : "Mindestens eine konfigurierte Action ist nicht mehr im Katalog vorhanden."
          : "Diesem Automation-Node fehlen noch sichtbare Actions.",
        statusTone: !firstActionLabel
          ? "warning"
          : firstActionDefinition?.isActive === false || !firstActionDefinition
            ? "warning"
            : "success",
        statusText: !firstActionLabel
          ? "Keine Actions"
          : firstActionDefinition?.isActive === false || !firstActionDefinition
            ? "Pruefen"
            : "Bereit",
        incomingCount: counts.incomingCount,
        outgoingCount: counts.outgoingCount,
      };
    case "task":
    default:
      return {
        nodeType: node.nodeType,
        displayTypeLabel: getWorkflowBuilderNodeTypeLabel(node.nodeType),
        primaryHint: legacyTemplateKey
          ? `Arbeitsvorlage: ${legacyTemplateKey}`
          : "Human Task fuer einen konkreten Arbeitsschritt.",
        secondaryHint: legacyTemplateKey
          ? "Die Aufgabe ist ueber eine fachliche Vorlage beschrieben."
          : "Diesem Task fehlt noch eine fachliche Vorlage.",
        statusTone: legacyTemplateKey ? "success" : "warning",
        statusText: legacyTemplateKey ? "Bereit" : "Fehlt",
        incomingCount: counts.incomingCount,
        outgoingCount: counts.outgoingCount,
      };
  }
}

function renderModeBadge(isAdvanced: boolean) {
  return (
    <span className={`badge badge--default ${isAdvanced ? "builder-mode-badge builder-mode-badge--advanced" : "builder-mode-badge"}`}>
      {isAdvanced ? "Admin Builder" : "Builder"}
    </span>
  );
}

export function AdminWorkflowBuilderSection({
  onNotice,
  onError,
}: AdminWorkflowBuilderSectionProps) {
  const { capabilities } = useCurrentUser();
  const canManageAdvanced = capabilities.canManageAdminConfiguration;
  const builder = useAdminWorkflowBuilder({ onNotice, onError, canManageAdvanced });
  const [flowInstance, setFlowInstance] = useState<ReactFlowInstance<Node<WorkflowBuilderCanvasNodeData>, Edge> | null>(null);
  const availableNodeKeys = useMemo(
    () => builder.versionDraft.nodes.map((node) => node.nodeKey.trim()).filter(Boolean),
    [builder.versionDraft.nodes]
  );
  const actionDefinitionsByKey = useMemo(() => {
    return new Map(
      builder.actionDefinitions.map((definition) => [
        definition.actionKey.trim().toLowerCase(),
        {
          displayName: definition.displayName,
          isActive: definition.isActive,
        },
      ] as const)
    );
  }, [builder.actionDefinitions]);
  const edgeCountsByNodeId = useMemo(() => {
    const countsByNodeId = new Map<string, NodeEdgeCounts>();
    const nodeIdByKey = new Map<string, string>();

    for (const node of builder.versionDraft.nodes) {
      countsByNodeId.set(node.id, { incomingCount: 0, outgoingCount: 0 });
      const normalizedKey = node.nodeKey.trim().toLowerCase();
      if (normalizedKey) {
        nodeIdByKey.set(normalizedKey, node.id);
      }
    }

    for (const edge of builder.versionDraft.edges) {
      const sourceNodeId = nodeIdByKey.get(edge.sourceNodeKey.trim().toLowerCase());
      const targetNodeId = nodeIdByKey.get(edge.targetNodeKey.trim().toLowerCase());

      if (sourceNodeId) {
        const current = countsByNodeId.get(sourceNodeId);
        if (current) {
          current.outgoingCount += 1;
        }
      }

      if (targetNodeId) {
        const current = countsByNodeId.get(targetNodeId);
        if (current) {
          current.incomingCount += 1;
        }
      }
    }

    return countsByNodeId;
  }, [builder.versionDraft.edges, builder.versionDraft.nodes]);

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
          edgeCountsByNodeId.get(node.id) ?? { incomingCount: 0, outgoingCount: 0 },
          actionDefinitionsByKey
        ),
        title: node.title.trim() || node.nodeKey.trim() || "Neuer Node",
        isSelected: builder.selectedNode?.id === node.id,
      },
    }));
  }, [actionDefinitionsByKey, builder.selectedNode?.id, builder.versionDraft.nodes, edgeCountsByNodeId]);

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
          label: edge.conditionExpression.trim() || undefined,
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

  const selectedDefinitionLabel = builder.selectedDefinition
    ? builder.selectedDefinition.key
    : "Noch keine Definition aktiv";
  const selectedVersionLabel = builder.selectedVersionSummary
    ? `V${builder.selectedVersionSummary.versionNumber} - ${builder.selectedVersionSummary.status}`
    : "Noch keine Version aktiv";
  const builderStageLabel = builder.selectedVersionSummary
    ? builder.selectedNode
      ? `Node '${builder.selectedNode.title.trim() || builder.selectedNode.nodeKey.trim() || "Neuer Node"}' aktiv`
      : "Canvas bereit"
    : "Version auswaehlen";
  const selectedNodeLabel = builder.selectedNode
    ? builder.selectedNode.title.trim() || builder.selectedNode.nodeKey.trim() || "Neuer Node"
    : "Kein Node aktiv";
  const publishDisabledReason = !canManageAdvanced
    ? "Publish ist nur im Admin Builder erlaubt."
    : builder.hasUnsavedChanges
      ? "Bitte erst den Draft speichern."
      : !builder.selectedVersionSummary?.canPublish
        ? "Diese Version ist noch nicht publish-faehig."
        : undefined;
  const canDeleteSelectedNode = Boolean(
    builder.selectedVersionSummary
    && builder.selectedNode
    && (canManageAdvanced || builder.selectedNode.nodeType !== "automation")
  );

  return (
    <div className="builder-product-section">
      <div className="builder-workspace-layout" style={{ gridTemplateColumns: "minmax(300px, 360px) minmax(0, 1.35fr) minmax(340px, 420px)" }}>
        <aside className="panel master-detail-sidebar builder-rail">
          <div className="content-stack">
            <div>
              <div className="builder-mode-badges">
                {renderModeBadge(canManageAdvanced)}
                {!canManageAdvanced ? (
                  <span className="badge badge--default builder-mode-badge builder-mode-badge--locked">
                    Automation gesperrt
                  </span>
                ) : null}
              </div>
              <h3>Workflow Builder</h3>
              <p className="text-muted">Definitionen, Versionen, Validierung und Publish in einem eigenen Builder-Workspace.</p>
            </div>

            {canManageAdvanced ? (
              <section className="content-stack">
                <h4>Neue Definition</h4>
                <label>
                  <span>Key</span>
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
                  Definition anlegen
                </button>
              </section>
            ) : (
              <section className="panel panel-info builder-lock-card">
                <p className="panel-text">
                  Neue Definitionen und neue Versionen bleiben dem Admin Builder vorbehalten. Im Builder-Modus bearbeitest du bestehende Drafts.
                </p>
              </section>
            )}

            <section className="content-stack">
              <h4>Definitionen</h4>
              {builder.isLoading ? <p className="text-muted">Definitionen werden geladen...</p> : null}
              {builder.definitions.map((definition) => (
                <button
                  key={definition.id}
                  type="button"
                  className={`admin-workspace-tab ${builder.selectedDefinition?.id === definition.id ? "active" : ""}`}
                  onClick={() => builder.selectDefinition(definition.id)}
                >
                  <span className="admin-workspace-tab-title">{definition.name}</span>
                  <span className="admin-workspace-tab-description">{definition.key}</span>
                </button>
              ))}
            </section>

            {canManageAdvanced ? (
              <section className="content-stack">
                <h4>Neue Version</h4>
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
                  Version anlegen
                </button>
              </section>
            ) : null}

            <section className="content-stack">
              <h4>Versionen</h4>
              {builder.selectedDefinition?.versions.map((version) => (
                <button
                  key={version.id}
                  type="button"
                  className={`admin-workspace-tab ${builder.selectedVersionSummary?.id === version.id ? "active" : ""}`}
                  onClick={() => builder.selectVersion(version.id)}
                >
                  <span className="admin-workspace-tab-title">
                    V{version.versionNumber} - {version.status}
                  </span>
                  <span className="admin-workspace-tab-description">
                    {version.name || "Ohne Namen"} - {version.validationIssues.length} Issue(s)
                  </span>
                </button>
              ))}
            </section>
          </div>
        </aside>

        <div className="content-stack admin-detail-main master-detail-main builder-stage">
          <section className="panel panel-muted content-stack">
            <div>
              <h3>Builder-Ablauf</h3>
              <p className="text-muted" style={{ margin: 0 }}>
                Der Ablauf bleibt klar getrennt: erst Definition, dann Version, dann visuell im Builder arbeiten.
              </p>
            </div>
            <div className="builder-flow-steps">
              <div className="builder-flow-step">
                <span className="builder-flow-step__index">1</span>
                <div className="builder-flow-step__content">
                  <strong>Definition</strong>
                  <span>{selectedDefinitionLabel}</span>
                </div>
              </div>
              <div className="builder-flow-step">
                <span className="builder-flow-step__index">2</span>
                <div className="builder-flow-step__content">
                  <strong>Version</strong>
                  <span>{selectedVersionLabel}</span>
                </div>
              </div>
              <div className="builder-flow-step">
                <span className="builder-flow-step__index">3</span>
                <div className="builder-flow-step__content">
                  <strong>Builder</strong>
                  <span>{builderStageLabel}</span>
                </div>
              </div>
            </div>
          </section>

          <section className="panel content-stack">
            <div className="flex-row" style={{ justifyContent: "space-between", gap: "1rem", alignItems: "center" }}>
              <div>
                <h3>Workflow-Definition</h3>
                <p className="text-muted">
                  {builder.selectedDefinition
                    ? `${builder.selectedDefinition.key}${builder.hasUnsavedChanges ? " - ungespeicherte Aenderungen" : ""}`
                    : "Bitte eine Definition auswaehlen."}
                </p>
              </div>
              <div className="flex-row" style={{ gap: "0.75rem" }}>
                <button
                  type="button"
                  className="button-primary"
                  onClick={() => void builder.saveVersion()}
                  disabled={!builder.selectedVersionSummary || builder.isSaving || builder.isLoadingVersion}
                >
                  Draft speichern
                </button>
              </div>
            </div>

            {builder.selectedDefinition ? (
              <div className="content-stack">
                <div className="builder-summary-grid">
                  <div className="panel panel-muted content-stack" style={{ gap: "0.35rem" }}>
                    <span className="badge badge--default">Definition</span>
                    <strong>{builder.selectedDefinition.key}</strong>
                    <span className="text-muted">System-Key bleibt erhalten, steht aber nicht mehr im Vordergrund des Builders.</span>
                  </div>
                  <div className="panel panel-muted content-stack" style={{ gap: "0.35rem" }}>
                    <span className="badge badge--default">Status</span>
                    <strong>{builder.hasUnsavedChanges ? "Ungespeicherte Aenderungen" : "Synchron"}</strong>
                    <span className="text-muted">Metadaten bleiben editierbar, der eigentliche Ablauf lebt im Canvas.</span>
                  </div>
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
              </div>
            ) : null}
          </section>

          <section className="panel content-stack">
            <h3>Draft-Version</h3>
            {builder.isLoadingVersion ? <p className="text-muted">Versionsdetail wird geladen...</p> : null}
            {!builder.selectedVersionSummary ? <p className="text-muted">Bitte eine Version auswaehlen.</p> : null}
            {builder.selectedVersionSummary ? (
              <div className="content-stack">
                <div className="builder-summary-grid">
                  <div className="panel panel-muted content-stack" style={{ gap: "0.35rem" }}>
                    <span className="badge badge--default">Version</span>
                    <strong>V{builder.selectedVersionSummary.versionNumber}</strong>
                    <span className="text-muted">Status: {builder.selectedVersionSummary.status}</span>
                  </div>
                  <div className="panel panel-muted content-stack" style={{ gap: "0.35rem" }}>
                    <span className="badge badge--default">{WORKFLOW_BUILDER_TECHNICAL_LABELS.processTypeKey}</span>
                    <strong>{builder.versionDraft.primaryLegacyProcessTypeKey.trim() || "Noch nicht gesetzt"}</strong>
                    <span className="text-muted">Bleibt intern erhalten, wird hier aber nur noch als Kontext gezeigt.</span>
                  </div>
                </div>
                <label>
                  <span>Versionsname</span>
                  <input
                    className="form-input"
                    value={builder.versionDraft.name}
                    onChange={(event) => builder.updateVersionDraftField("name", event.target.value)}
                  />
                </label>
                <label>
                  <span>Versionsbeschreibung</span>
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
                <div className="panel panel-info">
                  <p className="panel-text">
                    Publish ist erst aktiv, wenn die Version validierbar und gespeichert ist.
                  </p>
                </div>
              </div>
            ) : null}
          </section>

          <section className="panel content-stack">
            <div>
              <h3>Canvas</h3>
              <p className="text-muted" style={{ margin: 0 }}>
                Verbindungen werden direkt im Canvas angelegt, ausgewaehlt und geloescht. Die Node-Bearbeitung erfolgt jetzt rechts in der Sidebar.
              </p>
            </div>

            <div
              className="panel panel-muted content-stack builder-toolbar"
              aria-label="Builder Toolbar"
              style={{ gap: "0.85rem" }}
            >
              <div className="flex-row builder-toolbar__main" style={{ justifyContent: "space-between", gap: "1rem", alignItems: "center", flexWrap: "wrap" }}>
                <div className="builder-toolbar__intro">
                  <h4 style={{ margin: 0 }}>Builder-Aktionen</h4>
                  <p className="text-muted" style={{ margin: "0.25rem 0 0" }}>
                    {builder.selectedNode
                      ? `Aktiver Kontext: ${selectedNodeLabel}`
                      : "Node loeschen wird aktiv, sobald ein Node im Canvas ausgewaehlt ist."}
                  </p>
                </div>
                <div className="flex-row builder-toolbar__group" style={{ gap: "0.75rem", flexWrap: "wrap" }}>
                  <button
                    type="button"
                    className="button-secondary"
                    onClick={builder.addNode}
                    disabled={!builder.selectedVersionSummary}
                  >
                    Node hinzufuegen
                  </button>
                  <button
                    type="button"
                    className="button-danger"
                    onClick={builder.removeSelectedNode}
                    disabled={!canDeleteSelectedNode}
                    title={
                      builder.selectedNode
                        ? canDeleteSelectedNode
                          ? "Selektierten Node loeschen"
                          : "Automation-Nodes koennen nur im Admin Builder geloescht werden."
                        : "Bitte zuerst einen Node im Canvas auswaehlen."
                    }
                  >
                    Node loeschen
                  </button>
                  <button
                    type="button"
                    className="button-secondary"
                    onClick={builder.validateDraft}
                    disabled={!builder.selectedVersionSummary}
                  >
                    Validieren
                  </button>
                </div>
                <button
                  type="button"
                  className="button-secondary"
                  onClick={() => void builder.publishVersion()}
                  disabled={!canManageAdvanced || !builder.selectedVersionSummary?.canPublish || builder.isPublishing || builder.hasUnsavedChanges}
                  title={publishDisabledReason}
                >
                  Publish
                </button>
              </div>

              <div className="flex-row builder-toolbar__helpers" style={{ gap: "0.75rem", flexWrap: "wrap" }}>
                <span className="text-muted" style={{ fontSize: "0.82rem" }}>Hilfswerkzeuge</span>
                <button type="button" className="button-secondary" onClick={builder.autoLayoutNodes} disabled={!builder.selectedVersionSummary}>
                  Auto-layout
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
                  Fit view
                </button>
              </div>
            </div>

            {!builder.selectedVersionSummary ? (
              <p className="text-muted">Bitte zuerst eine Version auswaehlen.</p>
            ) : (
              <div
                className="builder-canvas-surface"
                style={{
                  position: "relative",
                  height: "72vh",
                  minHeight: "42rem",
                  border: "1px solid var(--graph-surface-border)",
                  borderRadius: "1rem",
                  overflow: "hidden",
                  background: "var(--graph-surface-background)",
                }}
              >
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
          </section>

          <section className="panel content-stack">
            <h3>Validierung und Publish</h3>
            {builder.localValidationIssues.length > 0 ? (
              <div className="panel panel-error" role="alert">
                <p className="panel-text text-error">Lokale Validierung</p>
                <ul>
                  {builder.localValidationIssues.map((issue) => (
                    <li key={issue}>{issue}</li>
                  ))}
                </ul>
              </div>
            ) : null}

            {builder.versionDetail?.validationIssues.length ? (
              <div className="panel panel-warning">
                <p className="panel-text">Serverseitige Validation Issues</p>
                <ul>
                  {builder.versionDetail.validationIssues.map((issue) => (
                    <li key={`${issue.code}-${issue.message}`}>
                      [{issue.scope}] {issue.message}
                    </li>
                  ))}
                </ul>
              </div>
            ) : (
              <p className="text-muted">Keine serverseitigen Validation Issues geladen.</p>
            )}
          </section>
        </div>
        <WorkflowBuilderSidebar
          selectedNode={builder.selectedNode}
          availableNodeKeys={availableNodeKeys}
          versionDraft={builder.versionDraft}
          actionDefinitions={builder.actionDefinitions}
          canManageAdvanced={canManageAdvanced}
          onUpdateNode={builder.updateNode}
          onUpdateEdge={builder.updateEdge}
          onRemoveEdge={builder.removeEdge}
          onAddActionFromDefinition={builder.addActionFromDefinition}
          onUpdateAction={builder.updateAction}
          onRemoveAction={builder.removeAction}
        />
      </div>
    </div>
  );
}
