import { Handle, Position, type NodeProps } from "@xyflow/react";

export type WorkflowBuilderCanvasNodeData = {
  title: string;
  nodeType: string;
  isVirtual?: boolean;
  junctionRole?: "exclusive_split" | "parallel_split" | "merge" | "parallel_join";
  typeLabel: string;
  modeLabel: string;
  responsibleLabel: string;
  notificationLabel: string | null;
  dueLabel: string | null;
  effectText: string;
  nextStepLabel: string;
  isSelected: boolean;
};

export function WorkflowBuilderCanvasNode({ data }: NodeProps) {
  const nodeData = data as WorkflowBuilderCanvasNodeData;
  if (nodeData.isVirtual) {
    return <WorkflowBuilderJunctionNode data={nodeData} />;
  }

  const typeStyle = getNodeTypeStyle(nodeData.nodeType, nodeData.isSelected);
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
        position={Position.Top}
        isConnectable={canReceiveConnections}
        style={handleStyle(canReceiveConnections)}
      />
      <div
        style={{
          width: 290,
          minHeight: 194,
          borderRadius: "1.15rem",
          border: `1px solid ${typeStyle.borderColor}`,
          background: typeStyle.background,
          boxShadow: nodeData.isSelected
            ? "0 26px 44px rgba(15, 23, 42, 0.18)"
            : "0 18px 34px rgba(15, 23, 42, 0.12)",
          overflow: "hidden",
        }}
      >
        <div
          style={{
            padding: "0.9rem 1rem 0.82rem",
            borderBottom: "1px solid rgba(148, 163, 184, 0.14)",
            background: typeStyle.headerBackground,
            display: "grid",
            gap: "0.65rem",
          }}
        >
          <div style={{ display: "flex", justifyContent: "space-between", gap: "0.75rem", alignItems: "flex-start" }}>
            <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
              <span
                className="badge badge--default"
                style={{
                  color: typeStyle.badgeText,
                  background: typeStyle.badgeBackground,
                  borderColor: typeStyle.badgeBorder,
                  boxShadow: "var(--graph-chip-shadow)",
                }}
              >
                {nodeData.typeLabel}
              </span>
              <span
                className="badge badge--default"
                style={{
                  color: "var(--graph-node-title)",
                  background: "rgba(255, 255, 255, 0.82)",
                  borderColor: "rgba(148, 163, 184, 0.22)",
                  boxShadow: "var(--graph-chip-shadow)",
                }}
              >
                {nodeData.modeLabel}
              </span>
            </div>
          </div>
          <strong
            style={{
              fontSize: "1.02rem",
              lineHeight: 1.15,
              color: "var(--graph-node-title)",
              letterSpacing: "-0.02em",
            }}
          >
            {nodeData.title}
          </strong>
        </div>
        <div style={{ padding: "0.92rem 1rem 1rem", display: "grid", gap: "0.82rem" }}>
          <div
            style={{
              display: "grid",
              gap: "0.48rem",
              padding: "0.7rem 0.78rem",
              borderRadius: "0.9rem",
              background: "rgba(255, 255, 255, 0.72)",
              border: "1px solid rgba(148, 163, 184, 0.14)",
            }}
          >
            <NodeMetaRow label="Zuständig" value={nodeData.responsibleLabel} />
            {nodeData.notificationLabel ? <NodeMetaRow label="Benachrichtigt" value={nodeData.notificationLabel} /> : null}
            {nodeData.dueLabel ? <NodeMetaRow label="Frist" value={nodeData.dueLabel} /> : null}
          </div>
          <div style={{ display: "grid", gap: "0.28rem" }}>
            <span className="text-muted" style={{ fontSize: "0.75rem", letterSpacing: "0.01em" }}>
              Wirkung
            </span>
            <p style={{ margin: 0, fontSize: "0.84rem", lineHeight: 1.42, color: "var(--graph-node-title)" }}>
              {nodeData.effectText}
            </p>
          </div>
          <div
            style={{
              display: "grid",
              gap: "0.28rem",
              borderTop: "1px solid rgba(148, 163, 184, 0.12)",
              paddingTop: "0.72rem",
            }}
          >
            <span className="text-muted" style={{ fontSize: "0.75rem", letterSpacing: "0.01em" }}>
              Danach
            </span>
            <p style={{ margin: 0, fontSize: "0.82rem", lineHeight: 1.35, color: "var(--text-secondary)" }}>
              {nodeData.nextStepLabel}
            </p>
          </div>
        </div>
      </div>
      <Handle
        type="source"
        position={Position.Bottom}
        isConnectable={canCreateConnections}
        style={handleStyle(canCreateConnections)}
      />
    </>
  );
}

function WorkflowBuilderJunctionNode({ data }: { data: WorkflowBuilderCanvasNodeData }) {
  const role = data.junctionRole ?? "merge";
  const tone = getJunctionTone(role);

  return (
    <>
      <Handle
        type="target"
        position={Position.Top}
        isConnectable={false}
        style={{
          background: tone.borderColor,
          width: 8,
          height: 8,
          border: "2px solid rgba(255,255,255,0.9)",
          opacity: 0.95,
        }}
      />
      <div
        style={{
          width: 34,
          height: 34,
          borderRadius: role === "merge" ? "999px" : "0.6rem",
          transform: role === "merge" ? undefined : "rotate(45deg)",
          background: tone.background,
          border: `2px solid ${tone.borderColor}`,
          boxShadow: "0 8px 18px rgba(15, 23, 42, 0.16)",
          display: "grid",
          placeItems: "center",
        }}
        title={tone.label}
      >
        <span
          style={{
            transform: role === "merge" ? undefined : "rotate(-45deg)",
            fontSize: "0.74rem",
            fontWeight: 800,
            color: tone.textColor,
            letterSpacing: "0.02em",
          }}
        >
          {tone.symbol}
        </span>
      </div>
      <Handle
        type="source"
        position={Position.Bottom}
        isConnectable={false}
        style={{
          background: tone.borderColor,
          width: 8,
          height: 8,
          border: "2px solid rgba(255,255,255,0.9)",
          opacity: 0.95,
        }}
      />
    </>
  );
}

function NodeMetaRow({ label, value }: { label: string; value: string }) {
  return (
    <div style={{ display: "grid", gap: "0.08rem" }}>
      <span className="text-muted" style={{ fontSize: "0.74rem", letterSpacing: "0.01em" }}>
        {label}
      </span>
      <strong style={{ fontSize: "0.84rem", lineHeight: 1.25, color: "var(--graph-node-title)" }}>{value}</strong>
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
  parallel_split: {
    accent: "#7c3aed",
    borderSoft: "rgba(124, 58, 237, 0.24)",
    borderStrong: "rgba(124, 58, 237, 0.76)",
    background: "linear-gradient(180deg, rgba(245, 243, 255, 0.98), rgba(255, 255, 255, 0.98))",
    backgroundSelected: "linear-gradient(180deg, rgba(237, 233, 254, 0.98), rgba(255, 255, 255, 0.98))",
    headerBackground: "linear-gradient(135deg, rgba(237, 233, 254, 0.98), rgba(248, 250, 252, 0.88))",
    badgeBackground: "rgba(237, 233, 254, 0.98)",
    badgeBorder: "rgba(124, 58, 237, 0.18)",
    badgeText: "#6d28d9",
  },
  parallel_join: {
    accent: "#9333ea",
    borderSoft: "rgba(147, 51, 234, 0.24)",
    borderStrong: "rgba(147, 51, 234, 0.76)",
    background: "linear-gradient(180deg, rgba(250, 245, 255, 0.98), rgba(255, 255, 255, 0.98))",
    backgroundSelected: "linear-gradient(180deg, rgba(243, 232, 255, 0.98), rgba(255, 255, 255, 0.98))",
    headerBackground: "linear-gradient(135deg, rgba(243, 232, 255, 0.98), rgba(248, 250, 252, 0.88))",
    badgeBackground: "rgba(243, 232, 255, 0.98)",
    badgeBorder: "rgba(147, 51, 234, 0.18)",
    badgeText: "#7e22ce",
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

function getJunctionTone(role: NonNullable<WorkflowBuilderCanvasNodeData["junctionRole"]>) {
  switch (role) {
    case "exclusive_split":
      return {
        symbol: "X",
        label: "Exklusive Verzweigung",
        background: "linear-gradient(180deg, rgba(224, 231, 255, 0.98), rgba(255,255,255,0.98))",
        borderColor: "rgba(29, 78, 216, 0.85)",
        textColor: "#1e40af",
      };
    case "parallel_split":
      return {
        symbol: "+",
        label: "Paralleler Split",
        background: "linear-gradient(180deg, rgba(237, 233, 254, 0.98), rgba(255,255,255,0.98))",
        borderColor: "rgba(124, 58, 237, 0.85)",
        textColor: "#6d28d9",
      };
    case "parallel_join":
      return {
        symbol: "&",
        label: "Paralleler Join",
        background: "linear-gradient(180deg, rgba(243, 232, 255, 0.98), rgba(255,255,255,0.98))",
        borderColor: "rgba(147, 51, 234, 0.85)",
        textColor: "#7e22ce",
      };
    case "merge":
    default:
      return {
        symbol: "M",
        label: "Zusammenführung",
        background: "linear-gradient(180deg, rgba(241, 245, 249, 0.98), rgba(255,255,255,0.98))",
        borderColor: "rgba(71, 85, 105, 0.82)",
        textColor: "#334155",
      };
  }
}
