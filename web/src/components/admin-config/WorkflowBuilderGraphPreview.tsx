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
  selectedNodeId?: string | null;
  selectedEdgeId?: string | null;
  variant?: "preview" | "canvas";
  nodeIssueCounts?: Map<string, number>;
  edgeIssueCounts?: Map<string, number>;
  connectingFromNodeId?: string | null;
  onStartConnect?: (nodeId: string) => void;
  onCancelConnect?: () => void;
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
  selectedNodeId = null,
  selectedEdgeId = null,
  variant = "preview",
  nodeIssueCounts,
  edgeIssueCounts,
  connectingFromNodeId = null,
  onStartConnect,
  onCancelConnect,
}: Props) {
  const [open, setOpen] = useState(true);
  const layout = useMemo(() => computeWorkflowBuilderGraphLayout(nodes, edges), [nodes, edges]);

  if (nodes.length === 0) return null;

  const padding = 16;
  const viewWidth = layout.width + padding * 2;
  const viewHeight = layout.height + padding * 2;
  const isCanvasVariant = variant === "canvas";
  const showCollapsedHeader = !isCanvasVariant;
  const isOpen = isCanvasVariant ? true : open;
  const isConnecting = Boolean(connectingFromNodeId);

  return (
    <div className={`wf-graph-preview${isCanvasVariant ? " wf-graph-preview--canvas" : ""}${isConnecting ? " wf-graph-preview--connecting" : ""}`}>
      {showCollapsedHeader ? (
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
      ) : null}

      {isOpen ? (
        <div className="wf-graph-preview-canvas-wrap">
          <svg
            className="wf-graph-preview-canvas"
            width={viewWidth}
            height={viewHeight}
            viewBox={`${-padding} ${-padding} ${viewWidth} ${viewHeight}`}
            role="img"
            aria-label="Workflow-Topologie"
            onClick={(event) => {
              if (isConnecting && onCancelConnect && event.target === event.currentTarget) {
                onCancelConnect();
              }
            }}
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
              {layout.edges.map((edge) => {
                const isSelected = edge.edgeId === selectedEdgeId;
                const issueCount = edgeIssueCounts?.get(edge.edgeId) ?? 0;
                const hasIssue = issueCount > 0;
                return (
                  <path
                    key={edge.edgeId}
                    className={`wf-graph-edge${edge.isDecision ? " wf-graph-edge--decision" : ""}${edge.isBackward ? " wf-graph-edge--backward" : ""}${isSelected ? " wf-graph-edge--selected" : ""}${hasIssue ? " wf-graph-edge--issue" : ""}`}
                    d={edge.d}
                    fill="none"
                    markerEnd={`url(#${edge.isDecision ? ARROW_MARKER_DECISION_ID : ARROW_MARKER_ID})`}
                    onClick={() => onEdgeClick(edge.edgeId)}
                  >
                    <title>
                      {hasIssue
                        ? `${issueCount} ${issueCount === 1 ? "Issue" : "Issues"} an diesem Übergang`
                        : isCanvasVariant
                          ? "Übergang auswählen"
                          : "Übergang in Sektion 3 öffnen"}
                    </title>
                  </path>
                );
              })}
            </g>

            <g className="wf-graph-nodes">
              {layout.nodes.map((node) => {
                const Icon = getWorkflowBuilderNodeIcon(node.nodeType);
                const isSelected = node.nodeId === selectedNodeId;
                const issueCount = nodeIssueCounts?.get(node.nodeId) ?? 0;
                const hasIssue = issueCount > 0;
                const issueLabel = hasIssue
                  ? `${issueCount} ${issueCount === 1 ? "Issue" : "Issues"} an diesem Schritt`
                  : null;
                const isConnectSource = isConnecting && connectingFromNodeId === node.nodeId;
                const isConnectTarget = isConnecting && connectingFromNodeId !== node.nodeId;
                const handleAvailable = isCanvasVariant
                  && Boolean(onStartConnect)
                  && node.nodeType !== "end"
                  && node.nodeKey.trim().length > 0;
                const nodeAriaLabel = isConnectTarget
                  ? `Verbindung zu ${node.label} herstellen`
                  : hasIssue
                    ? `Schritt ${node.label} — ${issueLabel}`
                    : `Schritt ${node.label}`;
                const nodeTitle = isConnectSource
                  ? "Quelle der neuen Verbindung — Esc bricht ab"
                  : isConnectTarget
                    ? `Verbindung auf ${node.label} setzen`
                    : (issueLabel ?? `${node.label} — Schritt ${isCanvasVariant ? "auswählen" : "öffnen"}`);
                return (
                  <g
                    key={node.nodeId}
                    className={`wf-graph-node wf-graph-node--cat-${node.category}${isSelected ? " wf-graph-node--selected" : ""}${hasIssue ? " wf-graph-node--issue" : ""}${isConnectSource ? " wf-graph-node--connect-source" : ""}${isConnectTarget ? " wf-graph-node--connect-target" : ""}`}
                    transform={`translate(${node.x}, ${node.y})`}
                    onClick={() => onNodeClick(node.nodeId)}
                    role="button"
                    aria-label={nodeAriaLabel}
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
                    {hasIssue ? (
                      <g className="wf-graph-node-issue-marker" transform={`translate(${node.width - 8}, 8)`}>
                        <circle r={9} className="wf-graph-node-issue-marker-bg" />
                        <text
                          x={0}
                          y={0}
                          textAnchor="middle"
                          dominantBaseline="central"
                          className="wf-graph-node-issue-marker-text"
                        >
                          {issueCount > 9 ? "9+" : issueCount}
                        </text>
                      </g>
                    ) : null}
                    {handleAvailable ? (
                      <g
                        className={`wf-graph-node-connect-handle${isConnectSource ? " wf-graph-node-connect-handle--active" : ""}`}
                        transform={`translate(${node.width}, ${node.height / 2})`}
                        onClick={(event) => {
                          event.stopPropagation();
                          if (isConnectSource && onCancelConnect) {
                            onCancelConnect();
                          } else if (onStartConnect) {
                            onStartConnect(node.nodeId);
                          }
                        }}
                        role="button"
                        aria-label={
                          isConnectSource
                            ? `Verbindung von ${node.label} abbrechen`
                            : `Neue Verbindung von ${node.label} starten`
                        }
                        tabIndex={0}
                        onKeyDown={(event) => {
                          if (event.key === "Enter" || event.key === " ") {
                            event.preventDefault();
                            event.stopPropagation();
                            if (isConnectSource && onCancelConnect) {
                              onCancelConnect();
                            } else if (onStartConnect) {
                              onStartConnect(node.nodeId);
                            }
                          }
                        }}
                      >
                        <circle r={9} className="wf-graph-node-connect-handle-bg" />
                        <line x1={-3} y1={0} x2={3} y2={0} className="wf-graph-node-connect-handle-glyph" />
                        <line x1={0} y1={-3} x2={0} y2={3} className="wf-graph-node-connect-handle-glyph" />
                        <title>
                          {isConnectSource
                            ? "Verbindung abbrechen"
                            : "Verbindung von hier aus ziehen — danach Zielschritt anklicken"}
                        </title>
                      </g>
                    ) : null}
                    <title>{nodeTitle}</title>
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
