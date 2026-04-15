import dagre from "@dagrejs/dagre";

export type WorkflowBuilderStructuredNodeInput = {
  id: string;
  nodeKey: string;
  nodeType: string;
  title: string;
};

export type WorkflowBuilderStructuredEdgeInput = {
  id: string;
  sourceNodeKey: string;
  targetNodeKey: string;
  priority: string | number;
  conditionExpression: string | null;
};

export type WorkflowBuilderStructuredDraftInput = {
  nodes: WorkflowBuilderStructuredNodeInput[];
  edges: WorkflowBuilderStructuredEdgeInput[];
};

export type WorkflowBuilderStructuredRenderNode = {
  id: string;
  sourceNodeId?: string;
  nodeType: string;
  title: string;
  x: number;
  y: number;
  width: number;
  height: number;
  isVirtual: boolean;
  junctionRole?: "exclusive_split" | "parallel_split" | "merge" | "parallel_join";
};

export type WorkflowBuilderStructuredRenderEdge = {
  id: string;
  source: string;
  target: string;
  label: string;
  isVirtual: boolean;
};

export type WorkflowBuilderStructuredLayoutResult = {
  nodes: WorkflowBuilderStructuredRenderNode[];
  edges: WorkflowBuilderStructuredRenderEdge[];
  positionsBySourceNodeId: Map<string, { x: number; y: number }>;
};

const CARD_WIDTH = 290;
const CARD_HEIGHT = 194;
const JUNCTION_SIZE = 34;

type EndpointDescriptor = {
  id: string;
  nodeType: string;
  title: string;
  width: number;
  height: number;
  isVirtual: boolean;
  sourceNodeId?: string;
  junctionRole?: WorkflowBuilderStructuredRenderNode["junctionRole"];
};

export function buildWorkflowBuilderStructuredLayout(
  draft: WorkflowBuilderStructuredDraftInput
): WorkflowBuilderStructuredLayoutResult {
  const actualNodes = draft.nodes.map((node) => ({
    ...node,
    normalizedKey: normalizeNodeKey(node.nodeKey),
  }));

  const nodeByKey = new Map(
    actualNodes
      .filter((node) => node.normalizedKey)
      .map((node) => [node.normalizedKey, node] as const)
  );

  const edges = draft.edges
    .map((edge) => {
      const sourceNode = nodeByKey.get(normalizeNodeKey(edge.sourceNodeKey));
      const targetNode = nodeByKey.get(normalizeNodeKey(edge.targetNodeKey));
      if (!sourceNode || !targetNode) {
        return null;
      }

      return {
        ...edge,
        sourceNode,
        targetNode,
      };
    })
    .filter((edge): edge is NonNullable<typeof edge> => Boolean(edge));

  const outgoingByNodeId = new Map<string, typeof edges>();
  const incomingByNodeId = new Map<string, typeof edges>();

  for (const node of actualNodes) {
    outgoingByNodeId.set(node.id, []);
    incomingByNodeId.set(node.id, []);
  }

  for (const edge of edges) {
    outgoingByNodeId.get(edge.sourceNode.id)?.push(edge);
    incomingByNodeId.get(edge.targetNode.id)?.push(edge);
  }

  const endpointNodes = new Map<string, EndpointDescriptor>();
  for (const node of actualNodes) {
    endpointNodes.set(node.id, {
      id: node.id,
      nodeType: node.nodeType,
      title: node.title,
      width: CARD_WIDTH,
      height: isMeasureGenerationNodeType(node.nodeType) ? 236 : CARD_HEIGHT,
      isVirtual: false,
      sourceNodeId: node.id,
    });
  }

  const splitJunctionIdsByNodeId = new Map<string, string>();
  const mergeJunctionIdsByNodeId = new Map<string, string>();

  for (const node of actualNodes) {
    const outgoingCount = outgoingByNodeId.get(node.id)?.length ?? 0;
    if (outgoingCount > 1) {
      const junctionId = `junction_split_${node.id}`;
      splitJunctionIdsByNodeId.set(node.id, junctionId);
      endpointNodes.set(junctionId, {
        id: junctionId,
        nodeType: "junction",
        title: "",
        width: JUNCTION_SIZE,
        height: JUNCTION_SIZE,
        isVirtual: true,
        sourceNodeId: node.id,
        junctionRole: node.nodeType === "parallel_split" ? "parallel_split" : "exclusive_split",
      });
    }

    const incomingCount = incomingByNodeId.get(node.id)?.length ?? 0;
    if (incomingCount > 1) {
      const junctionId = `junction_merge_${node.id}`;
      mergeJunctionIdsByNodeId.set(node.id, junctionId);
      endpointNodes.set(junctionId, {
        id: junctionId,
        nodeType: "junction",
        title: "",
        width: JUNCTION_SIZE,
        height: JUNCTION_SIZE,
        isVirtual: true,
        sourceNodeId: node.id,
        junctionRole: node.nodeType === "parallel_join" ? "parallel_join" : "merge",
      });
    }
  }

  const renderEdges: WorkflowBuilderStructuredRenderEdge[] = [];
  const introducedSplitEdgeIds = new Set<string>();
  const introducedMergeEdgeIds = new Set<string>();

  for (const edge of edges) {
    const splitJunctionId = splitJunctionIdsByNodeId.get(edge.sourceNode.id) ?? null;
    const mergeJunctionId = mergeJunctionIdsByNodeId.get(edge.targetNode.id) ?? null;

    if (splitJunctionId && !introducedSplitEdgeIds.has(splitJunctionId)) {
      introducedSplitEdgeIds.add(splitJunctionId);
      renderEdges.push({
        id: `virtual_source_${edge.sourceNode.id}`,
        source: edge.sourceNode.id,
        target: splitJunctionId,
        label: "",
        isVirtual: true,
      });
    }

    if (mergeJunctionId && !introducedMergeEdgeIds.has(mergeJunctionId)) {
      introducedMergeEdgeIds.add(mergeJunctionId);
      renderEdges.push({
        id: `virtual_target_${edge.targetNode.id}`,
        source: mergeJunctionId,
        target: edge.targetNode.id,
        label: "",
        isVirtual: true,
      });
    }

    renderEdges.push({
      id: edge.id,
      source: splitJunctionId ?? edge.sourceNode.id,
      target: mergeJunctionId ?? edge.targetNode.id,
      label: getEdgeLabel(edge.priority, edge.conditionExpression),
      isVirtual: false,
    });
  }

  const dagreGraph = new dagre.graphlib.Graph().setDefaultEdgeLabel(() => ({}));
  dagreGraph.setGraph({
    rankdir: "TB",
    align: "UL",
    ranksep: 84,
    nodesep: 54,
    edgesep: 24,
    marginx: 48,
    marginy: 48,
  });

  for (const endpointNode of endpointNodes.values()) {
    dagreGraph.setNode(endpointNode.id, {
      width: endpointNode.width,
      height: endpointNode.height,
    });
  }

  for (const edge of renderEdges) {
    dagreGraph.setEdge(edge.source, edge.target, {
      weight: edge.isVirtual ? 2 : 1,
      minlen: edge.isVirtual ? 1 : 2,
    });
  }

  dagre.layout(dagreGraph);

  const positionedNodes = [...endpointNodes.values()].map((endpointNode) => {
    const layoutNode = dagreGraph.node(endpointNode.id) as { x: number; y: number } | undefined;
    const x = layoutNode ? Math.round(layoutNode.x - endpointNode.width / 2) : 0;
    const y = layoutNode ? Math.round(layoutNode.y - endpointNode.height / 2) : 0;

    return {
      id: endpointNode.id,
      sourceNodeId: endpointNode.sourceNodeId,
      nodeType: endpointNode.nodeType,
      title: endpointNode.title,
      x,
      y,
      width: endpointNode.width,
      height: endpointNode.height,
      isVirtual: endpointNode.isVirtual,
      junctionRole: endpointNode.junctionRole,
    } satisfies WorkflowBuilderStructuredRenderNode;
  });

  const positionsBySourceNodeId = new Map<string, { x: number; y: number }>();
  for (const node of positionedNodes) {
    if (!node.isVirtual && node.sourceNodeId) {
      positionsBySourceNodeId.set(node.sourceNodeId, { x: node.x, y: node.y });
    }
  }

  return {
    nodes: positionedNodes,
    edges: renderEdges,
    positionsBySourceNodeId,
  };
}

function getEdgeLabel(priority: string | number, conditionExpression: string | null) {
  const priorityText = String(priority ?? "").trim();
  const conditionText = String(conditionExpression ?? "").trim();
  if (conditionText && priorityText) {
    return `P${priorityText} · ${conditionText}`;
  }

  if (conditionText) {
    return conditionText;
  }

  if (priorityText) {
    return `P${priorityText}`;
  }

  return "";
}

function isMeasureGenerationNodeType(nodeType: string) {
  return [
    "setup",
    "measure_provision",
    "measure_deprovision",
    "measure_change",
    "measure_rename",
  ].includes(nodeType);
}

function normalizeNodeKey(value: string | null | undefined) {
  return (value ?? "").trim().toLowerCase();
}
