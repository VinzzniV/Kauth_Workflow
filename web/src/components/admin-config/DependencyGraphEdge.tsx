import {
  BaseEdge,
  EdgeLabelRenderer,
  getBezierPath,
  type EdgeProps,
} from "@xyflow/react";

type DependencyGraphEdgeData = {
  requiredStatus: "open" | "ready" | "in_progress" | "blocked" | "done";
};

function getStatusStyle(status: DependencyGraphEdgeData["requiredStatus"]) {
  switch (status) {
    case "done":
      return {
        stroke: "var(--graph-edge-done)",
        labelBg: "var(--graph-edge-done-bg)",
        labelBorder: "var(--graph-edge-done-border)",
        labelText: "var(--graph-edge-done-text)",
      };
    case "ready":
      return {
        stroke: "var(--graph-edge-ready)",
        labelBg: "var(--graph-edge-ready-bg)",
        labelBorder: "var(--graph-edge-ready-border)",
        labelText: "var(--graph-edge-ready-text)",
      };
    case "in_progress":
      return {
        stroke: "var(--graph-edge-in-progress)",
        labelBg: "var(--graph-edge-in-progress-bg)",
        labelBorder: "var(--graph-edge-in-progress-border)",
        labelText: "var(--graph-edge-in-progress-text)",
      };
    case "blocked":
      return {
        stroke: "var(--graph-edge-blocked)",
        labelBg: "var(--graph-edge-blocked-bg)",
        labelBorder: "var(--graph-edge-blocked-border)",
        labelText: "var(--graph-edge-blocked-text)",
      };
    case "open":
    default:
      return {
        stroke: "var(--graph-edge-open)",
        labelBg: "var(--graph-edge-open-bg)",
        labelBorder: "var(--graph-edge-open-border)",
        labelText: "var(--graph-edge-open-text)",
      };
  }
}

export function DependencyGraphEdge({
  id,
  sourceX,
  sourceY,
  targetX,
  targetY,
  sourcePosition,
  targetPosition,
  markerEnd,
  data,
}: EdgeProps) {
  const edgeData = data as DependencyGraphEdgeData | undefined;
  const requiredStatus = edgeData?.requiredStatus ?? "open";
  const style = getStatusStyle(requiredStatus);

  const [edgePath, labelX, labelY] = getBezierPath({
    sourceX,
    sourceY,
    sourcePosition,
    targetX,
    targetY,
    targetPosition,
  });

  return (
    <>
      <BaseEdge
        id={id}
        path={edgePath}
        markerEnd={markerEnd}
        style={{
          stroke: style.stroke,
          strokeWidth: 1.9,
        }}
      />
      <EdgeLabelRenderer>
        <div
          style={{
            position: "absolute",
            transform: `translate(-50%, -50%) translate(${labelX}px, ${labelY}px)`,
            pointerEvents: "none",
            background: style.labelBg,
            border: `1px solid ${style.labelBorder}`,
            color: style.labelText,
            borderRadius: "999px",
            padding: "0.18rem 0.55rem",
            fontSize: "0.72rem",
            fontWeight: 700,
            letterSpacing: "0.02em",
            textTransform: "uppercase",
            boxShadow: "var(--graph-chip-shadow)",
            backdropFilter: "blur(2px)",
          }}
          className="nodrag nopan"
        >
          {requiredStatus}
        </div>
      </EdgeLabelRenderer>
    </>
  );
}
