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
        stroke: "#0f766e",
        labelBg: "rgba(204, 251, 241, 0.96)",
        labelBorder: "rgba(15, 118, 110, 0.4)",
        labelText: "#115e59",
      };
    case "ready":
      return {
        stroke: "#0ea5e9",
        labelBg: "rgba(224, 242, 254, 0.96)",
        labelBorder: "rgba(14, 165, 233, 0.4)",
        labelText: "#0369a1",
      };
    case "in_progress":
      return {
        stroke: "#f59e0b",
        labelBg: "rgba(254, 243, 199, 0.96)",
        labelBorder: "rgba(245, 158, 11, 0.4)",
        labelText: "#92400e",
      };
    case "blocked":
      return {
        stroke: "#ef4444",
        labelBg: "rgba(254, 226, 226, 0.96)",
        labelBorder: "rgba(239, 68, 68, 0.4)",
        labelText: "#991b1b",
      };
    case "open":
    default:
      return {
        stroke: "#64748b",
        labelBg: "rgba(241, 245, 249, 0.98)",
        labelBorder: "rgba(100, 116, 139, 0.4)",
        labelText: "#334155",
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
            boxShadow: "0 4px 14px rgba(15, 23, 42, 0.12)",
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
