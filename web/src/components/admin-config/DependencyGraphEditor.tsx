import { useMemo, useState } from "react";
import dagre from "@dagrejs/dagre";
import {
  Background,
  type Connection,
  Controls,
  MarkerType,
  MiniMap,
  ReactFlow,
  type EdgeTypes,
  type NodeTypes,
  type Edge,
  type Node,
  type ReactFlowInstance,
} from "@xyflow/react";
import "@xyflow/react/dist/style.css";
import type { AdminDependencyGraph, AdminTaskTemplate } from "../../types/auth";
import { useConfirmationDialog } from "../feedback/useConfirmationDialog";
import { DependencyGraphEdge } from "./DependencyGraphEdge";
import { DependencyGraphNode, type DependencyGraphNodeData } from "./DependencyGraphNode";

type DependencyGraphEditorProps = {
  graph: AdminDependencyGraph;
  templates: AdminTaskTemplate[];
  selectedTemplateId: number | null;
  isLoading: boolean;
  isCreatingDependency: boolean;
  isDeletingDependency: boolean;
  onSelectTemplate: (templateId: number) => void;
  onCreateDependency: (
    sourceTemplateId: number,
    targetTemplateId: number,
    requiredStatus: "open" | "ready" | "in_progress" | "blocked" | "done"
  ) => Promise<void>;
  onDeleteDependency: (dependencyId: number) => Promise<void>;
};

const NODE_WIDTH = 238;
const NODE_HEIGHT = 176;
const NODE_TYPES: NodeTypes = {
  template: DependencyGraphNode,
};
const EDGE_TYPES: EdgeTypes = {
  dependency: DependencyGraphEdge,
};

export function DependencyGraphEditor({
  graph,
  templates,
  selectedTemplateId,
  isLoading,
  isCreatingDependency,
  isDeletingDependency,
  onSelectTemplate,
  onCreateDependency,
  onDeleteDependency,
}: DependencyGraphEditorProps) {
  const confirm = useConfirmationDialog();
  const [pendingConnection, setPendingConnection] = useState<{ sourceTemplateId: number; targetTemplateId: number } | null>(null);
  const [pendingRequiredStatus, setPendingRequiredStatus] = useState<"open" | "ready" | "in_progress" | "blocked" | "done">("done");
  const [flowInstance, setFlowInstance] = useState<ReactFlowInstance<Node<DependencyGraphNodeData>, Edge> | null>(null);

  const templateIndex = useMemo(() => {
    return new Map(templates.map((template) => [template.id, template]));
  }, [templates]);

  const nodeTitleIndex = useMemo(() => {
    return new Map(graph.nodes.map((node) => [node.id, node.title]));
  }, [graph.nodes]);

  const nodes = useMemo<Node<DependencyGraphNodeData>[]>(() => {
    const dagreGraph = new dagre.graphlib.Graph().setDefaultEdgeLabel(() => ({}));
    dagreGraph.setGraph({
      rankdir: "TB",
      align: "UL",
      ranksep: 90,
      nodesep: 52,
      edgesep: 30,
      marginx: 32,
      marginy: 32,
    });

    graph.nodes.forEach((node) => {
      dagreGraph.setNode(String(node.id), { width: NODE_WIDTH, height: NODE_HEIGHT });
    });
    graph.edges.forEach((edge) => {
      dagreGraph.setEdge(String(edge.sourceTemplateId), String(edge.targetTemplateId));
    });

    dagre.layout(dagreGraph);

    return graph.nodes.map((node) => {
      const template = templateIndex.get(node.id);
      const layoutNode = dagreGraph.node(String(node.id)) as { x: number; y: number } | undefined;
      const x = layoutNode ? layoutNode.x - NODE_WIDTH / 2 : 0;
      const y = layoutNode ? layoutNode.y - NODE_HEIGHT / 2 : 0;

      return {
        id: String(node.id),
        type: "template",
        position: {
          x,
          y,
        },
        data: {
          title: node.title,
          category: node.category,
          iconKey: template?.iconKey ?? null,
          conditionCount: template?.conditionCount ?? 0,
          dependencyCount: template?.dependencyCount ?? 0,
          processAreaLabel: template?.processAreaLabel ?? null,
          isRequired: template?.isRequired ?? true,
          isSelected: node.id === selectedTemplateId,
        },
        draggable: false,
        selectable: true,
      };
    });
  }, [graph.edges, graph.nodes, selectedTemplateId, templateIndex]);

  const edges = useMemo<Edge[]>(() => {
    return graph.edges.map((edge) => ({
      id: String(edge.id),
      type: "dependency",
      source: String(edge.sourceTemplateId),
      target: String(edge.targetTemplateId),
      data: {
        requiredStatus: edge.requiredStatus,
      },
      markerEnd: {
        type: MarkerType.ArrowClosed,
        width: 20,
        height: 20,
        color: "var(--graph-edge-open)",
      },
    }));
  }, [graph.edges]);

  if (isLoading) {
    return <p className="panel-note">Dependency-Graph wird geladen...</p>;
  }

  if (graph.nodes.length === 0) {
    return <p className="panel-note">Für diesen Prozesstyp sind noch keine Templates im Graph vorhanden.</p>;
  }

  const pendingSourceTitle = pendingConnection ? nodeTitleIndex.get(pendingConnection.sourceTemplateId) ?? `#${pendingConnection.sourceTemplateId}` : "";
  const pendingTargetTitle = pendingConnection ? nodeTitleIndex.get(pendingConnection.targetTemplateId) ?? `#${pendingConnection.targetTemplateId}` : "";

  return (
    <div
      style={{
        position: "relative",
        height: "34rem",
        border: "1px solid var(--graph-surface-border)",
        borderRadius: "1rem",
        overflow: "hidden",
        background: "var(--graph-surface-background)",
      }}
    >
      <ReactFlow
        nodes={nodes}
        edges={edges}
        nodeTypes={NODE_TYPES}
        edgeTypes={EDGE_TYPES}
        onInit={(instance) => {
          setFlowInstance(instance);
        }}
        fitView
        fitViewOptions={{ padding: 0.18 }}
        nodesDraggable={false}
        nodesConnectable
        elementsSelectable
        connectOnClick={false}
        onConnect={(connection: Connection) => {
          const sourceTemplateId = connection.source ? Number(connection.source) : Number.NaN;
          const targetTemplateId = connection.target ? Number(connection.target) : Number.NaN;

          if (
            !Number.isFinite(sourceTemplateId) ||
            !Number.isFinite(targetTemplateId) ||
            sourceTemplateId <= 0 ||
            targetTemplateId <= 0 ||
            sourceTemplateId === targetTemplateId
          ) {
            return;
          }

          const alreadyExists = graph.edges.some(
            (edge) => edge.sourceTemplateId === sourceTemplateId && edge.targetTemplateId === targetTemplateId
          );
          if (alreadyExists) {
            return;
          }

          setPendingConnection({
            sourceTemplateId,
            targetTemplateId,
          });
          setPendingRequiredStatus("done");
        }}
        onEdgeClick={(_, edge) => {
          const dependencyId = Number(edge.id);
          if (!Number.isFinite(dependencyId) || dependencyId <= 0) {
            return;
          }

          void (async () => {
            const shouldDelete = await confirm({
              title: "Abhängigkeit löschen?",
              description: "Die Verbindung zwischen den beiden Aufgabenvorlagen wird entfernt.",
              confirmLabel: "Abhängigkeit löschen",
              tone: "danger",
            });
            if (!shouldDelete) {
              return;
            }

            void onDeleteDependency(dependencyId).catch(() => {
              // Fehler kommt bereits als Notice/Error aus dem Hook.
            });
          })();
        }}
        proOptions={{ hideAttribution: true }}
        onNodeClick={(_, node) => {
          const templateId = Number(node.id);
          if (Number.isFinite(templateId)) {
            onSelectTemplate(templateId);
          }
        }}
      >
        <Background gap={18} size={1} color="var(--graph-grid-color)" />
        <MiniMap
          pannable
          zoomable
          nodeColor={(node) => (Number(node.id) === selectedTemplateId ? "var(--graph-edge-done)" : "var(--graph-edge-open)")}
          style={{ background: "var(--graph-minimap-background)" }}
        />
        <Controls showInteractive={false} />
      </ReactFlow>

      <div style={{ position: "absolute", left: "1rem", top: "1rem", zIndex: 20 }}>
        <button
          type="button"
          className="btn btn-outline"
          onClick={() => {
            if (flowInstance) {
              setTimeout(() => {
                void flowInstance.fitView({ padding: 0.18, duration: 250 });
              }, 0);
            }
          }}
        >
          Re-layout
        </button>
      </div>

      {pendingConnection ? (
        <div
          className="panel panel-muted"
          style={{
            position: "absolute",
            right: "1rem",
            top: "1rem",
            width: "22rem",
            boxShadow: "var(--graph-overlay-shadow)",
            zIndex: 20,
          }}
        >
          <div className="panel-head">
            <h2>Abhängigkeit anlegen</h2>
            <p>
              Von <strong>{pendingSourceTitle}</strong> nach <strong>{pendingTargetTitle}</strong>.
            </p>
          </div>
          <div className="panel-body">
            <label>
              <span className="form-label">Required Status</span>
              <select
                className="form-select"
                value={pendingRequiredStatus}
                disabled={isCreatingDependency}
                onChange={(event) =>
                  setPendingRequiredStatus(
                    event.target.value as "open" | "ready" | "in_progress" | "blocked" | "done"
                  )
                }
              >
                <option value="open">open</option>
                <option value="ready">ready</option>
                <option value="in_progress">in_progress</option>
                <option value="blocked">blocked</option>
                <option value="done">done</option>
              </select>
            </label>

            <div style={{ display: "flex", gap: "0.6rem", marginTop: "1rem" }}>
              <button
                type="button"
                className="btn btn-outline"
                disabled={isCreatingDependency || isDeletingDependency}
                onClick={() => setPendingConnection(null)}
              >
                Abbrechen
              </button>
              <button
                type="button"
                className="btn btn-primary"
                disabled={isCreatingDependency || isDeletingDependency}
                onClick={() => {
                  void onCreateDependency(
                    pendingConnection.sourceTemplateId,
                    pendingConnection.targetTemplateId,
                    pendingRequiredStatus
                  )
                    .then(() => setPendingConnection(null))
                    .catch(() => {
                      // Fehler kommt bereits als Notice/Error aus dem Hook.
                    });
                }}
              >
                {isCreatingDependency ? "Wird angelegt..." : "Speichern"}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
