import { Handle, Position, type NodeProps } from "@xyflow/react";
import { WORKFLOW_BUILDER_TECHNICAL_LABELS } from "./workflowBuilderLabels";

export type WorkflowBuilderCanvasNodeData = {
  title: string;
  nodeType: string;
  displayTypeLabel: string;
  primaryHint: string;
  secondaryHint: string;
  statusTone: "success" | "info" | "warning" | "neutral";
  statusText: string;
  incomingCount: number;
  outgoingCount: number;
  isSelected: boolean;
};

export function WorkflowBuilderCanvasNode({ data }: NodeProps) {
  const nodeData = data as WorkflowBuilderCanvasNodeData;
  const typeStyle = getNodeTypeStyle(nodeData.nodeType, nodeData.isSelected);
  const statusStyle = getStatusStyle(nodeData.statusTone);
  const canReceiveConnections = nodeData.nodeType !== "start";
  const canCreateConnections = nodeData.nodeType !== "end";
  const handleStyle = (isConnectable: boolean) => ({
    background: typeStyle.accent,
    width: 10,
    height: 10,
    border: "2px solid var(--graph-handle-border)",
    opacity: isConnectable ? 1 : 0.35,
  });

  return (
    <>
      <Handle
        type="target"
        position={Position.Left}
        isConnectable={canReceiveConnections}
        style={handleStyle(canReceiveConnections)}
      />
      <div
        style={{
          width: 280,
          minHeight: 176,
          borderRadius: "1rem",
          border: `1px solid ${typeStyle.borderColor}`,
          background: typeStyle.background,
          boxShadow: nodeData.isSelected
            ? "var(--graph-node-shadow-selected)"
            : "var(--graph-node-shadow)",
          overflow: "hidden",
        }}
      >
        <div
          style={{
            padding: "0.95rem 1rem 0.8rem",
            borderBottom: "1px solid var(--graph-node-header-border)",
            background: typeStyle.headerBackground,
            display: "grid",
            gap: "0.55rem",
          }}
        >
          <div style={{ display: "flex", justifyContent: "space-between", gap: "0.75rem", alignItems: "flex-start" }}>
            <span
              className="badge badge--default"
              style={{
                color: typeStyle.badgeText,
                background: typeStyle.badgeBackground,
                borderColor: typeStyle.badgeBorder,
                boxShadow: "var(--graph-chip-shadow)",
              }}
            >
              {nodeData.displayTypeLabel}
            </span>
            <span
              className="badge badge--default"
              style={{
                color: statusStyle.text,
                background: statusStyle.background,
                borderColor: statusStyle.border,
                boxShadow: "var(--graph-chip-shadow)",
              }}
            >
              {nodeData.statusText}
            </span>
          </div>
          <strong
            style={{
              fontSize: "1rem",
              lineHeight: 1.2,
              color: "var(--graph-node-title)",
            }}
          >
            {nodeData.title}
          </strong>
        </div>
        <div style={{ padding: "0.9rem 1rem 1rem", display: "grid", gap: "0.75rem" }}>
          <div style={{ display: "grid", gap: "0.35rem" }}>
            <p
              style={{
                margin: 0,
                fontSize: "0.88rem",
                lineHeight: 1.35,
                color: "var(--graph-node-title)",
                fontWeight: 600,
              }}
            >
              {nodeData.primaryHint}
            </p>
            <p className="text-muted" style={{ margin: 0, fontSize: "0.82rem", lineHeight: 1.35 }}>
              {nodeData.secondaryHint}
            </p>
          </div>
          <div style={{ display: "flex", gap: "0.6rem" }}>
            <MetricCard label={WORKFLOW_BUILDER_TECHNICAL_LABELS.incoming} value={String(nodeData.incomingCount)} />
            <MetricCard label={WORKFLOW_BUILDER_TECHNICAL_LABELS.outgoing} value={String(nodeData.outgoingCount)} />
          </div>
          <p className="text-muted" style={{ margin: 0, fontSize: "0.82rem" }}>
            Klick fuer Details, Ziehen fuer Position.
          </p>
        </div>
      </div>
      <Handle
        type="source"
        position={Position.Right}
        isConnectable={canCreateConnections}
        style={handleStyle(canCreateConnections)}
      />
    </>
  );
}

function MetricCard({ label, value }: { label: string; value: string }) {
  return (
    <div
      style={{
        flex: 1,
        minWidth: 0,
        padding: "0.65rem 0.7rem",
        borderRadius: "0.9rem",
        background: "var(--graph-node-metric-background)",
        border: "1px solid var(--graph-node-metric-border)",
      }}
    >
      <div className="text-muted" style={{ fontSize: "0.72rem", lineHeight: 1.1, textTransform: "uppercase" }}>
        {label}
      </div>
      <strong style={{ display: "block", marginTop: "0.2rem", fontSize: "1rem", color: "var(--graph-node-title)" }}>
        {value}
      </strong>
    </div>
  );
}

function getNodeTypeStyle(nodeType: string, isSelected: boolean) {
  const tone = NODE_TYPE_STYLES[nodeType] ?? NODE_TYPE_STYLES.task;
  return {
    accent: tone.accent,
    borderColor: isSelected ? tone.borderStrong : tone.borderSoft,
    background: isSelected ? tone.backgroundSelected : tone.background,
    headerBackground: tone.headerBackground,
    badgeBackground: tone.badgeBackground,
    badgeBorder: tone.badgeBorder,
    badgeText: tone.badgeText,
  };
}

function getStatusStyle(statusTone: WorkflowBuilderCanvasNodeData["statusTone"]) {
  switch (statusTone) {
    case "success":
      return {
        background: "var(--graph-edge-done-bg)",
        border: "var(--graph-edge-done-border)",
        text: "var(--graph-edge-done-text)",
      };
    case "info":
      return {
        background: "var(--graph-edge-ready-bg)",
        border: "var(--graph-edge-ready-border)",
        text: "var(--graph-edge-ready-text)",
      };
    case "warning":
      return {
        background: "var(--graph-edge-in-progress-bg)",
        border: "var(--graph-edge-in-progress-border)",
        text: "var(--graph-edge-in-progress-text)",
      };
    default:
      return {
        background: "var(--graph-edge-open-bg)",
        border: "var(--graph-edge-open-border)",
        text: "var(--graph-edge-open-text)",
      };
  }
}

const NODE_TYPE_STYLES: Record<string, {
  accent: string;
  borderSoft: string;
  borderStrong: string;
  background: string;
  backgroundSelected: string;
  headerBackground: string;
  badgeBackground: string;
  badgeBorder: string;
  badgeText: string;
}> = {
  start: {
    accent: "#0f766e",
    borderSoft: "rgba(15, 118, 110, 0.3)",
    borderStrong: "rgba(15, 118, 110, 0.85)",
    background: "linear-gradient(180deg, rgba(240, 253, 250, 0.98), rgba(255, 255, 255, 0.98))",
    backgroundSelected: "linear-gradient(180deg, rgba(204, 251, 241, 0.98), rgba(255, 255, 255, 0.98))",
    headerBackground: "linear-gradient(135deg, rgba(204, 251, 241, 0.98), rgba(248, 250, 252, 0.88))",
    badgeBackground: "rgba(204, 251, 241, 0.98)",
    badgeBorder: "rgba(15, 118, 110, 0.28)",
    badgeText: "#115e59",
  },
  form: {
    accent: "#0369a1",
    borderSoft: "rgba(3, 105, 161, 0.28)",
    borderStrong: "rgba(3, 105, 161, 0.78)",
    background: "linear-gradient(180deg, rgba(239, 246, 255, 0.98), rgba(255, 255, 255, 0.98))",
    backgroundSelected: "linear-gradient(180deg, rgba(224, 242, 254, 0.98), rgba(255, 255, 255, 0.98))",
    headerBackground: "linear-gradient(135deg, rgba(224, 242, 254, 0.98), rgba(248, 250, 252, 0.88))",
    badgeBackground: "rgba(224, 242, 254, 0.98)",
    badgeBorder: "rgba(3, 105, 161, 0.22)",
    badgeText: "#075985",
  },
  approval: {
    accent: "#be123c",
    borderSoft: "rgba(190, 18, 60, 0.2)",
    borderStrong: "rgba(190, 18, 60, 0.7)",
    background: "linear-gradient(180deg, rgba(255, 241, 242, 0.98), rgba(255, 255, 255, 0.98))",
    backgroundSelected: "linear-gradient(180deg, rgba(255, 228, 230, 0.98), rgba(255, 255, 255, 0.98))",
    headerBackground: "linear-gradient(135deg, rgba(255, 228, 230, 0.98), rgba(248, 250, 252, 0.88))",
    badgeBackground: "rgba(255, 228, 230, 0.98)",
    badgeBorder: "rgba(190, 18, 60, 0.2)",
    badgeText: "#9f1239",
  },
  task: {
    accent: "#b45309",
    borderSoft: "rgba(180, 83, 9, 0.24)",
    borderStrong: "rgba(180, 83, 9, 0.7)",
    background: "linear-gradient(180deg, rgba(255, 251, 235, 0.98), rgba(255, 255, 255, 0.98))",
    backgroundSelected: "linear-gradient(180deg, rgba(254, 243, 199, 0.98), rgba(255, 255, 255, 0.98))",
    headerBackground: "linear-gradient(135deg, rgba(254, 243, 199, 0.98), rgba(248, 250, 252, 0.88))",
    badgeBackground: "rgba(254, 243, 199, 0.98)",
    badgeBorder: "rgba(180, 83, 9, 0.2)",
    badgeText: "#92400e",
  },
  decision: {
    accent: "#1d4ed8",
    borderSoft: "rgba(29, 78, 216, 0.24)",
    borderStrong: "rgba(29, 78, 216, 0.76)",
    background: "linear-gradient(180deg, rgba(238, 242, 255, 0.98), rgba(255, 255, 255, 0.98))",
    backgroundSelected: "linear-gradient(180deg, rgba(224, 231, 255, 0.98), rgba(255, 255, 255, 0.98))",
    headerBackground: "linear-gradient(135deg, rgba(224, 231, 255, 0.98), rgba(248, 250, 252, 0.88))",
    badgeBackground: "rgba(224, 231, 255, 0.98)",
    badgeBorder: "rgba(29, 78, 216, 0.18)",
    badgeText: "#1e40af",
  },
  automation: {
    accent: "#15803d",
    borderSoft: "rgba(21, 128, 61, 0.24)",
    borderStrong: "rgba(21, 128, 61, 0.76)",
    background: "linear-gradient(180deg, rgba(240, 253, 244, 0.98), rgba(255, 255, 255, 0.98))",
    backgroundSelected: "linear-gradient(180deg, rgba(220, 252, 231, 0.98), rgba(255, 255, 255, 0.98))",
    headerBackground: "linear-gradient(135deg, rgba(220, 252, 231, 0.98), rgba(248, 250, 252, 0.88))",
    badgeBackground: "rgba(220, 252, 231, 0.98)",
    badgeBorder: "rgba(21, 128, 61, 0.18)",
    badgeText: "#166534",
  },
  end: {
    accent: "#475569",
    borderSoft: "rgba(71, 85, 105, 0.28)",
    borderStrong: "rgba(71, 85, 105, 0.82)",
    background: "linear-gradient(180deg, rgba(248, 250, 252, 0.98), rgba(255, 255, 255, 0.98))",
    backgroundSelected: "linear-gradient(180deg, rgba(241, 245, 249, 0.98), rgba(255, 255, 255, 0.98))",
    headerBackground: "linear-gradient(135deg, rgba(241, 245, 249, 0.98), rgba(248, 250, 252, 0.88))",
    badgeBackground: "rgba(241, 245, 249, 0.98)",
    badgeBorder: "rgba(71, 85, 105, 0.18)",
    badgeText: "#334155",
  },
};
