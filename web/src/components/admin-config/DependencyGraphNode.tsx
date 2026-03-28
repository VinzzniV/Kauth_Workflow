import { Handle, Position, type NodeProps } from "@xyflow/react";
import RequirementIcon from "../workflows/RequirementIcon";

export type DependencyGraphNodeData = {
  title: string;
  category: string;
  iconKey: string | null;
  conditionCount: number;
  dependencyCount: number;
  processAreaLabel: string | null;
  isRequired: boolean;
  isSelected: boolean;
};

export function DependencyGraphNode({ data }: NodeProps) {
  const nodeData = data as DependencyGraphNodeData;
  const borderColor = nodeData.isSelected ? "var(--graph-node-border-selected)" : "var(--graph-node-border)";
  const background = nodeData.isSelected
    ? "var(--graph-node-background-selected)"
    : "var(--graph-node-background)";

  return (
    <>
      <Handle
        type="target"
        position={Position.Top}
        isConnectable
        style={{ background: "var(--graph-node-handle)", width: 10, height: 10, border: "2px solid var(--graph-handle-border)" }}
      />
      <div
        style={{
          width: 238,
          borderRadius: "1rem",
          border: `1px solid ${borderColor}`,
          background,
          boxShadow: data.isSelected
            ? "var(--graph-node-shadow-selected)"
            : "var(--graph-node-shadow)",
          overflow: "hidden",
        }}
      >
        <div
          style={{
            display: "flex",
            alignItems: "center",
            gap: "0.75rem",
            padding: "0.9rem 1rem 0.75rem",
            borderBottom: "1px solid var(--graph-node-header-border)",
            background: "var(--graph-node-header-background)",
          }}
        >
          <RequirementIcon iconKey={nodeData.iconKey ?? "berechtigungen"} title={nodeData.title} size="md" />
          <div style={{ minWidth: 0, display: "grid", gap: "0.2rem", flex: 1 }}>
            <strong
              style={{
                fontSize: "0.95rem",
                lineHeight: 1.2,
                color: "var(--graph-node-title)",
                overflow: "hidden",
                textOverflow: "ellipsis",
              }}
            >
              {nodeData.title}
            </strong>
            <span className="text-muted" style={{ fontSize: "0.78rem" }}>
              {nodeData.processAreaLabel?.trim() || "Allgemeiner Bereich"}
            </span>
          </div>
        </div>

        <div style={{ padding: "0.8rem 1rem 0.95rem", display: "grid", gap: "0.7rem" }}>
          <div style={{ display: "flex", gap: "0.45rem", flexWrap: "wrap" }}>
            <span className="badge badge--default">{nodeData.category}</span>
            <span className={`badge badge--${nodeData.isRequired ? "success" : "default"}`}>
              {nodeData.isRequired ? "Pflicht" : "Optional"}
            </span>
          </div>

          <div style={{ display: "grid", gridTemplateColumns: "repeat(2, minmax(0, 1fr))", gap: "0.55rem" }}>
            <div
              style={{
                padding: "0.55rem 0.65rem",
                borderRadius: "0.75rem",
                background: "var(--graph-node-metric-background)",
                border: "1px solid var(--graph-node-metric-border)",
              }}
            >
              <div className="text-muted" style={{ fontSize: "0.72rem" }}>
                Bedingungen
              </div>
              <strong style={{ fontSize: "1rem", color: "var(--graph-node-title)" }}>{nodeData.conditionCount}</strong>
            </div>
            <div
              style={{
                padding: "0.55rem 0.65rem",
                borderRadius: "0.75rem",
                background: "var(--graph-node-metric-background)",
                border: "1px solid var(--graph-node-metric-border)",
              }}
            >
              <div className="text-muted" style={{ fontSize: "0.72rem" }}>
                Abhängigkeiten
              </div>
              <strong style={{ fontSize: "1rem", color: "var(--graph-node-title)" }}>{nodeData.dependencyCount}</strong>
            </div>
          </div>
        </div>
      </div>
      <Handle
        type="source"
        position={Position.Bottom}
        isConnectable
        style={{ background: "var(--graph-node-handle)", width: 10, height: 10, border: "2px solid var(--graph-handle-border)" }}
      />
    </>
  );
}
