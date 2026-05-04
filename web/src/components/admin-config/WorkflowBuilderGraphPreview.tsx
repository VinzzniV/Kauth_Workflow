import { useMemo, useState } from "react";
import { ChevronDown, ChevronRight } from "lucide-react";
import type {
  WorkflowBuilderEdgeDraft,
  WorkflowBuilderNodeDraft,
} from "../../hooks/adminWorkflowBuilderModel";
import { computeWorkflowBuilderGraphLayout } from "./workflowBuilderGraph";
import { getWorkflowBuilderNodeIcon } from "./workflowBuilderLabels";

type Props = {
  nodes: WorkflowBuilderNodeDraft[];
  edges: WorkflowBuilderEdgeDraft[];
  onNodeClick: (nodeId: string) => void;
  onEdgeClick: (edgeId: string) => void;
};

const ARROW_MARKER_ID = "wf-graph-arrow";
const ARROW_MARKER_DECISION_ID = "wf-graph-arrow-decision";

function truncate(value: string, max: number): string {
  if (value.length <= max) return value;
  return `${value.slice(0, max - 1)}…`;
}

export function WorkflowBuilderGraphPreview({
  nodes,
  edges,
  onNodeClick,
  onEdgeClick,
}: Props) {
  const [open, setOpen] = useState(true);
  const layout = useMemo(() => computeWorkflowBuilderGraphLayout(nodes, edges), [nodes, edges]);

  if (nodes.length === 0) return null;

  const padding = 16;
  const viewWidth = layout.width + padding * 2;
  const viewHeight = layout.height + padding * 2;

  return (
    <div className="wf-graph-preview">
      <button
        type="button"
        className="wf-graph-preview-toggle"
        onClick={() => setOpen((value) => !value)}
        aria-expanded={open}
      >
        {open ? <ChevronDown size={14} aria-hidden="true" /> : <ChevronRight size={14} aria-hidden="true" />}
        <span>Vorschau (Topologie)</span>
        <span className="wf-graph-preview-meta">
          {layout.nodeCount} {layout.nodeCount === 1 ? "Schritt" : "Schritte"} · {layout.edgeCount}{" "}
          {layout.edgeCount === 1 ? "Übergang" : "Übergänge"}
        </span>
      </button>

      {open ? (
        <div className="wf-graph-preview-canvas-wrap">
          <svg
            className="wf-graph-preview-canvas"
            width={viewWidth}
            height={viewHeight}
            viewBox={`${-padding} ${-padding} ${viewWidth} ${viewHeight}`}
            role="img"
            aria-label="Workflow-Topologie"
          >
            <defs>
              <marker
                id={ARROW_MARKER_ID}
                viewBox="0 0 8 8"
                refX="7"
                refY="4"
                markerWidth="6"
                markerHeight="6"
                orient="auto"
              >
                <path d="M 0 0 L 8 4 L 0 8 z" />
              </marker>
              <marker
                id={ARROW_MARKER_DECISION_ID}
                viewBox="0 0 8 8"
                refX="7"
                refY="4"
                markerWidth="6"
                markerHeight="6"
                orient="auto"
              >
                <path d="M 0 0 L 8 4 L 0 8 z" />
              </marker>
            </defs>

            <g className="wf-graph-edges">
              {layout.edges.map((edge) => (
                <path
                  key={edge.edgeId}
                  className={`wf-graph-edge${edge.isDecision ? " wf-graph-edge--decision" : ""}${edge.isBackward ? " wf-graph-edge--backward" : ""}`}
                  d={edge.d}
                  fill="none"
                  markerEnd={`url(#${edge.isDecision ? ARROW_MARKER_DECISION_ID : ARROW_MARKER_ID})`}
                  onClick={() => onEdgeClick(edge.edgeId)}
                >
                  <title>Übergang in Sektion 3 öffnen</title>
                </path>
              ))}
            </g>

            <g className="wf-graph-nodes">
              {layout.nodes.map((node) => {
                const Icon = getWorkflowBuilderNodeIcon(node.nodeType);
                return (
                  <g
                    key={node.nodeId}
                    className={`wf-graph-node wf-graph-node--cat-${node.category}`}
                    transform={`translate(${node.x}, ${node.y})`}
                    onClick={() => onNodeClick(node.nodeId)}
                    role="button"
                    aria-label={`Schritt ${node.label}`}
                    tabIndex={0}
                    onKeyDown={(event) => {
                      if (event.key === "Enter" || event.key === " ") {
                        event.preventDefault();
                        onNodeClick(node.nodeId);
                      }
                    }}
                  >
                    <rect
                      className="wf-graph-node-bg"
                      width={node.width}
                      height={node.height}
                      rx={8}
                      ry={8}
                    />
                    <foreignObject
                      x={6}
                      y={6}
                      width={node.width - 12}
                      height={node.height - 12}
                    >
                      <div className="wf-graph-node-content">
                        <Icon size={12} aria-hidden="true" />
                        <span className="wf-graph-node-label">{truncate(node.label, 18)}</span>
                      </div>
                    </foreignObject>
                    <title>{`${node.label} — Schritt öffnen`}</title>
                  </g>
                );
              })}
            </g>
          </svg>
        </div>
      ) : null}
    </div>
  );
}
