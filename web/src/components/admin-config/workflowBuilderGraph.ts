// Pure layout helper for the workflow builder graph preview.
// Computes a layered DAG layout from nodes + edges, returning absolute
// pixel coordinates for SVG rendering. No React dependency.

import type {
  WorkflowBuilderEdgeDraft,
  WorkflowBuilderNodeDraft,
} from "../../hooks/adminWorkflowBuilderModel";
import {
  getWorkflowBuilderNodeCategory,
  getWorkflowBuilderNodeTypeLabel,
  type WorkflowBuilderNodeCategory,
  type WorkflowBuilderNodeKind,
} from "./workflowBuilderLabels";

export type GraphNodeLayout = {
  nodeId: string;
  nodeKey: string;
  nodeType: WorkflowBuilderNodeKind;
  category: WorkflowBuilderNodeCategory;
  label: string;
  x: number;
  y: number;
  width: number;
  height: number;
};

export type GraphEdgeLayout = {
  edgeId: string;
  sourceNodeId: string;
  targetNodeId: string;
  d: string;
  isDecision: boolean;
  isBackward: boolean;
};

export type GraphLayout = {
  width: number;
  height: number;
  nodes: GraphNodeLayout[];
  edges: GraphEdgeLayout[];
  nodeCount: number;
  edgeCount: number;
};

const NODE_W = 132;
const NODE_H = 38;
const COL_GAP = 56;
const ROW_GAP = 18;
const COL_W = NODE_W + COL_GAP;
const ROW_H = NODE_H + ROW_GAP;

export function computeWorkflowBuilderGraphLayout(
  nodes: WorkflowBuilderNodeDraft[],
  edges: WorkflowBuilderEdgeDraft[]
): GraphLayout {
  const nodeByKey = new Map<string, WorkflowBuilderNodeDraft>();
  for (const node of nodes) {
    const key = node.nodeKey.trim().toLowerCase();
    if (key) nodeByKey.set(key, node);
  }

  const outgoing = new Map<string, string[]>();
  const incoming = new Map<string, string[]>();
  type ResolvedEdge = {
    edge: WorkflowBuilderEdgeDraft;
    sourceId: string;
    targetId: string;
  };
  const resolvedEdges: ResolvedEdge[] = [];
  for (const edge of edges) {
    const sourceKey = edge.sourceNodeKey.trim().toLowerCase();
    const targetKey = edge.targetNodeKey.trim().toLowerCase();
    if (!sourceKey || !targetKey) continue;
    const source = nodeByKey.get(sourceKey);
    const target = nodeByKey.get(targetKey);
    if (!source || !target) continue;
    resolvedEdges.push({ edge, sourceId: source.id, targetId: target.id });
    if (!outgoing.has(source.id)) outgoing.set(source.id, []);
    outgoing.get(source.id)!.push(target.id);
    if (!incoming.has(target.id)) incoming.set(target.id, []);
    incoming.get(target.id)!.push(source.id);
  }

  // Rank via longest-path BFS from start nodes / nodes without incoming edges.
  const ranks = new Map<string, number>();
  const queue: string[] = [];
  for (const node of nodes) {
    const noIncoming = (incoming.get(node.id) ?? []).length === 0;
    if (node.nodeType === "start" || noIncoming) {
      ranks.set(node.id, 0);
      queue.push(node.id);
    }
  }
  // BFS — handles graphs with cycles by capping iterations.
  let safety = nodes.length * (nodes.length + 1);
  while (queue.length > 0 && safety-- > 0) {
    const id = queue.shift()!;
    const currentRank = ranks.get(id) ?? 0;
    for (const target of outgoing.get(id) ?? []) {
      const existing = ranks.get(target);
      const nextRank = currentRank + 1;
      if (existing === undefined || existing < nextRank) {
        ranks.set(target, nextRank);
        queue.push(target);
      }
    }
  }
  // Cycle members or fully disconnected nodes: rank 0 fallback.
  for (const node of nodes) {
    if (!ranks.has(node.id)) ranks.set(node.id, 0);
  }

  // Group by rank, preserving original array order within column.
  const byRank = new Map<number, WorkflowBuilderNodeDraft[]>();
  for (const node of nodes) {
    const rank = ranks.get(node.id) ?? 0;
    if (!byRank.has(rank)) byRank.set(rank, []);
    byRank.get(rank)!.push(node);
  }
  const maxRank = Math.max(0, ...Array.from(byRank.keys()));

  const layouted: GraphNodeLayout[] = [];
  let maxRowsInColumn = 0;
  for (let rank = 0; rank <= maxRank; rank++) {
    const column = byRank.get(rank) ?? [];
    maxRowsInColumn = Math.max(maxRowsInColumn, column.length);
    column.forEach((node, idx) => {
      const x = rank * COL_W;
      const y = idx * ROW_H;
      const labelSource = node.title.trim() || node.nodeKey.trim();
      layouted.push({
        nodeId: node.id,
        nodeKey: node.nodeKey,
        nodeType: node.nodeType,
        category: getWorkflowBuilderNodeCategory(node.nodeType),
        label: labelSource || getWorkflowBuilderNodeTypeLabel(node.nodeType),
        x,
        y,
        width: NODE_W,
        height: NODE_H,
      });
    });
  }

  const totalWidth = Math.max(NODE_W, (maxRank + 1) * COL_W - COL_GAP);
  const totalHeight = Math.max(NODE_H, maxRowsInColumn * ROW_H - ROW_GAP);

  const layoutById = new Map(layouted.map((n) => [n.nodeId, n]));
  const edgePaths: GraphEdgeLayout[] = [];
  for (const { edge, sourceId, targetId } of resolvedEdges) {
    const s = layoutById.get(sourceId);
    const t = layoutById.get(targetId);
    if (!s || !t) continue;

    const sourceNode = nodes.find((n) => n.id === sourceId);
    const isDecision = sourceNode?.nodeType === "decision";
    const isBackward = (ranks.get(targetId) ?? 0) <= (ranks.get(sourceId) ?? 0);

    let d: string;
    if (isBackward) {
      // Loop back below the source node: down → left → up.
      const sx = s.x + s.width / 2;
      const sy = s.y + s.height;
      const tx = t.x + t.width / 2;
      const ty = t.y + t.height;
      const dipY = Math.max(sy, ty) + ROW_GAP;
      d = `M ${sx},${sy} C ${sx},${dipY} ${tx},${dipY} ${tx},${ty}`;
    } else {
      const sx = s.x + s.width;
      const sy = s.y + s.height / 2;
      const tx = t.x;
      const ty = t.y + t.height / 2;
      const dx = Math.max(20, (tx - sx) / 2);
      d = `M ${sx},${sy} C ${sx + dx},${sy} ${tx - dx},${ty} ${tx},${ty}`;
    }

    edgePaths.push({
      edgeId: edge.id,
      sourceNodeId: sourceId,
      targetNodeId: targetId,
      d,
      isDecision,
      isBackward,
    });
  }

  return {
    width: totalWidth,
    height: totalHeight,
    nodes: layouted,
    edges: edgePaths,
    nodeCount: nodes.length,
    edgeCount: edgePaths.length,
  };
}
