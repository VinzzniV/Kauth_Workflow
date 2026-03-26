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
  const borderColor = nodeData.isSelected ? "rgba(15, 118, 110, 0.9)" : "rgba(148, 163, 184, 0.65)";
  const background = nodeData.isSelected
    ? "linear-gradient(180deg, rgba(240, 253, 250, 1), rgba(255, 255, 255, 0.98))"
    : "linear-gradient(180deg, rgba(255, 255, 255, 0.98), rgba(248, 250, 252, 0.98))";

  return (
    <>
      <Handle
        type="target"
        position={Position.Top}
        isConnectable
        style={{ background: "#0f766e", width: 10, height: 10, border: "2px solid white" }}
      />
      <div
        style={{
          width: 238,
          borderRadius: "1rem",
          border: `1px solid ${borderColor}`,
          background,
          boxShadow: data.isSelected
            ? "0 0 0 3px rgba(15, 118, 110, 0.14), 0 18px 34px rgba(15, 23, 42, 0.12)"
            : "0 12px 26px rgba(15, 23, 42, 0.08)",
          overflow: "hidden",
        }}
      >
        <div
          style={{
            display: "flex",
            alignItems: "center",
            gap: "0.75rem",
            padding: "0.9rem 1rem 0.75rem",
            borderBottom: "1px solid rgba(226, 232, 240, 0.9)",
            background: "linear-gradient(135deg, rgba(236, 254, 255, 0.95), rgba(248, 250, 252, 0.75))",
          }}
        >
          <RequirementIcon iconKey={nodeData.iconKey ?? "berechtigungen"} title={nodeData.title} size="md" />
          <div style={{ minWidth: 0, display: "grid", gap: "0.2rem", flex: 1 }}>
            <strong
              style={{
                fontSize: "0.95rem",
                lineHeight: 1.2,
                color: "#0f172a",
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
                background: "rgba(241, 245, 249, 0.9)",
                border: "1px solid rgba(226, 232, 240, 0.95)",
              }}
            >
              <div className="text-muted" style={{ fontSize: "0.72rem" }}>
                Bedingungen
              </div>
              <strong style={{ fontSize: "1rem", color: "#0f172a" }}>{nodeData.conditionCount}</strong>
            </div>
            <div
              style={{
                padding: "0.55rem 0.65rem",
                borderRadius: "0.75rem",
                background: "rgba(241, 245, 249, 0.9)",
                border: "1px solid rgba(226, 232, 240, 0.95)",
              }}
            >
              <div className="text-muted" style={{ fontSize: "0.72rem" }}>
                Abhängigkeiten
              </div>
              <strong style={{ fontSize: "1rem", color: "#0f172a" }}>{nodeData.dependencyCount}</strong>
            </div>
          </div>
        </div>
      </div>
      <Handle
        type="source"
        position={Position.Bottom}
        isConnectable
        style={{ background: "#0f766e", width: 10, height: 10, border: "2px solid white" }}
      />
    </>
  );
}
