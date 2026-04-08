import type {
  AdminWorkflowDefinitionEdge,
  AdminWorkflowDefinitionNode,
  AdminWorkflowDefinitionSummary,
  AdminWorkflowDefinitionVersionDetail,
  AdminWorkflowDefinitionVersionSummary,
  AdminWorkflowNodeAction,
} from "../types/auth";

const DEFAULT_LAYOUT_X = 320;
const DEFAULT_LAYOUT_Y = 180;

export type WorkflowBuilderNodeDraft = {
  id: string;
  nodeKey: string;
  nodeType: "start" | "end" | "form" | "approval" | "task" | "decision" | "automation";
  title: string;
  sortOrder: string;
  positionX: number | null;
  positionY: number | null;
  configText: string;
  actions: WorkflowBuilderActionDraft[];
};

export type WorkflowBuilderActionDraft = {
  id: string;
  actionKey: string;
  executionOrder: string;
  onErrorBehavior: "fail_workflow";
  inputMappingText: string;
};

export type WorkflowBuilderEdgeDraft = {
  id: string;
  sourceNodeKey: string;
  targetNodeKey: string;
  priority: string;
  conditionExpression: string;
};

export type WorkflowBuilderVersionDraft = {
  name: string;
  description: string;
  primaryLegacyProcessTypeKey: string;
  nodes: WorkflowBuilderNodeDraft[];
  edges: WorkflowBuilderEdgeDraft[];
};

export type WorkflowBuilderLocalIssue = {
  scope: "definition" | "version" | "node" | "edge" | "action";
  message: string;
  referenceKey?: string;
};

export function createLocalId(prefix: string): string {
  return `${prefix}_${Math.random().toString(36).slice(2, 10)}`;
}

export function createEmptyDefinitionDraft() {
  return {
    key: "",
    name: "",
    description: "",
  };
}

export function createEmptyVersionCreateDraft() {
  return {
    name: "",
    description: "",
  };
}

export function createEmptyVersionDraft(): WorkflowBuilderVersionDraft {
  return {
    name: "",
    description: "",
    primaryLegacyProcessTypeKey: "",
    nodes: [],
    edges: [],
  };
}

export function createEmptyNodeDraft(
  nodeType: WorkflowBuilderNodeDraft["nodeType"] = "task",
  sortOrder?: number
): WorkflowBuilderNodeDraft {
  return {
    id: createLocalId("node"),
    nodeKey: "",
    nodeType,
    title: "",
    sortOrder: typeof sortOrder === "number" ? String(sortOrder) : "",
    positionX: null,
    positionY: null,
    configText: "",
    actions: [],
  };
}

export function createEmptyActionDraft(): WorkflowBuilderActionDraft {
  return {
    id: createLocalId("action"),
    actionKey: "",
    executionOrder: "",
    onErrorBehavior: "fail_workflow",
    inputMappingText: "",
  };
}

export function createEmptyEdgeDraft(): WorkflowBuilderEdgeDraft {
  return {
    id: createLocalId("edge"),
    sourceNodeKey: "",
    targetNodeKey: "",
    priority: "",
    conditionExpression: "",
  };
}

export function toVersionDraft(detail: AdminWorkflowDefinitionVersionDetail): WorkflowBuilderVersionDraft {
  const draft = {
    name: detail.name ?? "",
    description: detail.description ?? "",
    primaryLegacyProcessTypeKey: detail.primaryLegacyProcessTypeKey ?? "",
    nodes: detail.nodes.map(toNodeDraft),
    edges: detail.edges.map(toEdgeDraft),
  };

  return {
    ...draft,
    nodes: withFallbackNodePositions(draft.nodes, draft.edges),
  };
}

function toNodeDraft(node: AdminWorkflowDefinitionNode): WorkflowBuilderNodeDraft {
  const nodeType = normalizeNodeType(node.nodeType);
  return {
    id: createLocalId("node"),
    nodeKey: node.nodeKey ?? "",
    nodeType,
    title: node.title ?? "",
    sortOrder: String(node.sortOrder),
    positionX: node.positionX ?? null,
    positionY: node.positionY ?? null,
    configText: toJsonText(node.config),
    actions: node.actions.map(toActionDraft),
  };
}

function toActionDraft(action: AdminWorkflowNodeAction): WorkflowBuilderActionDraft {
  return {
    id: createLocalId("action"),
    actionKey: action.actionKey ?? "",
    executionOrder: String(action.executionOrder),
    onErrorBehavior: "fail_workflow",
    inputMappingText: toJsonText(action.inputMapping),
  };
}

function toEdgeDraft(edge: AdminWorkflowDefinitionEdge): WorkflowBuilderEdgeDraft {
  return {
    id: createLocalId("edge"),
    sourceNodeKey: edge.sourceNodeKey ?? "",
    targetNodeKey: edge.targetNodeKey ?? "",
    priority: String(edge.priority),
    conditionExpression: edge.conditionExpression ?? "",
  };
}

function normalizeNodeType(value: string | null): WorkflowBuilderNodeDraft["nodeType"] {
  switch ((value ?? "").trim().toLowerCase()) {
    case "start":
    case "end":
    case "form":
    case "approval":
    case "decision":
    case "automation":
      return value!.trim().toLowerCase() as WorkflowBuilderNodeDraft["nodeType"];
    default:
      return "task";
  }
}

export function toJsonText(value: unknown | null): string {
  if (value === null || typeof value === "undefined") {
    return "";
  }

  return JSON.stringify(value, null, 2);
}

export function buildVersionReplacePayload(draft: WorkflowBuilderVersionDraft) {
  return {
    name: toNullableText(draft.name),
    description: toNullableText(draft.description),
    primaryLegacyProcessTypeKey: toNullableText(draft.primaryLegacyProcessTypeKey),
    nodes: draft.nodes.map((node, index) => ({
      nodeKey: toNullableText(node.nodeKey),
      nodeType: node.nodeType,
      title: toNullableText(node.title),
      sortOrder: toPositiveInteger(node.sortOrder, index + 1),
      positionX: toNullableInteger(node.positionX),
      positionY: toNullableInteger(node.positionY),
      config: parseOptionalJsonObject(node.configText),
      actions: node.nodeType === "automation"
        ? node.actions.map((action, actionIndex) => ({
            actionKey: toNullableText(action.actionKey),
            inputMapping: parseOptionalJsonObject(action.inputMappingText),
            executionOrder: toPositiveInteger(action.executionOrder, actionIndex + 1),
            onErrorBehavior: action.onErrorBehavior,
          }))
        : [],
    })),
    edges: draft.edges.map((edge, index) => ({
      sourceNodeKey: toNullableText(edge.sourceNodeKey),
      targetNodeKey: toNullableText(edge.targetNodeKey),
      priority: toPositiveInteger(edge.priority, index + 1),
      conditionExpression: toNullableText(edge.conditionExpression),
    })),
  };
}

export function autoLayoutVersionDraft(draft: WorkflowBuilderVersionDraft): WorkflowBuilderVersionDraft {
  return {
    ...draft,
    nodes: forceAutoLayoutNodePositions(draft.nodes, draft.edges),
  };
}

export function withFallbackNodePositions(
  nodes: WorkflowBuilderNodeDraft[],
  edges: WorkflowBuilderEdgeDraft[]
): WorkflowBuilderNodeDraft[] {
  const fallback = buildFallbackPositions(nodes, edges);
  return nodes.map((node) => {
    const position = fallback.get(node.id);
    return {
      ...node,
      positionX: node.positionX ?? position?.x ?? null,
      positionY: node.positionY ?? position?.y ?? null,
    };
  });
}

function forceAutoLayoutNodePositions(
  nodes: WorkflowBuilderNodeDraft[],
  edges: WorkflowBuilderEdgeDraft[]
): WorkflowBuilderNodeDraft[] {
  const fallback = buildFallbackPositions(nodes, edges);
  return nodes.map((node) => {
    const position = fallback.get(node.id);
    return {
      ...node,
      positionX: position?.x ?? 0,
      positionY: position?.y ?? 0,
    };
  });
}

function buildFallbackPositions(
  nodes: WorkflowBuilderNodeDraft[],
  edges: WorkflowBuilderEdgeDraft[]
): Map<string, { x: number; y: number }> {
  const orderedNodes = [...nodes].sort(compareNodesForLayout);
  const nodeIdByKey = new Map<string, string>();
  const layerByNodeId = new Map<string, number>();
  const incoming = new Map<string, Set<string>>();
  const outgoing = new Map<string, Set<string>>();

  for (const node of orderedNodes) {
    incoming.set(node.id, new Set<string>());
    outgoing.set(node.id, new Set<string>());
    const normalizedKey = normalizeNodeKey(node.nodeKey);
    if (normalizedKey) {
      nodeIdByKey.set(normalizedKey, node.id);
    }
  }

  for (const edge of edges) {
    const sourceNodeId = nodeIdByKey.get(normalizeNodeKey(edge.sourceNodeKey));
    const targetNodeId = nodeIdByKey.get(normalizeNodeKey(edge.targetNodeKey));
    if (!sourceNodeId || !targetNodeId) {
      continue;
    }

    outgoing.get(sourceNodeId)?.add(targetNodeId);
    incoming.get(targetNodeId)?.add(sourceNodeId);
  }

  const roots = orderedNodes.filter((node) =>
    node.nodeType === "start" || (incoming.get(node.id)?.size ?? 0) === 0
  );
  const queue = roots.map((node) => ({ nodeId: node.id, layer: 0 }));
  const seen = new Set<string>();

  while (queue.length > 0) {
    const current = queue.shift()!;
    const previousLayer = layerByNodeId.get(current.nodeId);
    if (typeof previousLayer !== "number" || current.layer > previousLayer) {
      layerByNodeId.set(current.nodeId, current.layer);
    }

    if (seen.has(current.nodeId) && current.layer <= (previousLayer ?? -1)) {
      continue;
    }

    seen.add(current.nodeId);
    const targets = [...(outgoing.get(current.nodeId) ?? [])].sort((left, right) => {
      const leftNode = orderedNodes.find((node) => node.id === left);
      const rightNode = orderedNodes.find((node) => node.id === right);
      return compareNodesForLayout(leftNode, rightNode);
    });
    for (const targetNodeId of targets) {
      queue.push({ nodeId: targetNodeId, layer: current.layer + 1 });
    }
  }

  let nextLayer = layerByNodeId.size > 0 ? Math.max(...layerByNodeId.values()) + 1 : 0;
  for (const node of orderedNodes) {
    if (layerByNodeId.has(node.id)) {
      continue;
    }

    const seedLayer = nextLayer;
    queue.push({ nodeId: node.id, layer: seedLayer });
    nextLayer += 1;

    while (queue.length > 0) {
      const current = queue.shift()!;
      const previousLayer = layerByNodeId.get(current.nodeId);
      if (typeof previousLayer !== "number" || current.layer > previousLayer) {
        layerByNodeId.set(current.nodeId, current.layer);
      }

      if (seen.has(current.nodeId) && current.layer <= (previousLayer ?? -1)) {
        continue;
      }

      seen.add(current.nodeId);
      const targets = [...(outgoing.get(current.nodeId) ?? [])].sort((left, right) => {
        const leftNode = orderedNodes.find((item) => item.id === left);
        const rightNode = orderedNodes.find((item) => item.id === right);
        return compareNodesForLayout(leftNode, rightNode);
      });
      for (const targetNodeId of targets) {
        queue.push({ nodeId: targetNodeId, layer: current.layer + 1 });
      }
    }
  }

  const nodesByLayer = new Map<number, WorkflowBuilderNodeDraft[]>();
  for (const node of orderedNodes) {
    const layer = layerByNodeId.get(node.id) ?? 0;
    const items = nodesByLayer.get(layer) ?? [];
    items.push(node);
    nodesByLayer.set(layer, items);
  }

  const positions = new Map<string, { x: number; y: number }>();
  let previousLayerPositions = new Map<string, number>();
  for (const [layer, layerNodes] of [...nodesByLayer.entries()].sort((left, right) => left[0] - right[0])) {
    layerNodes.sort((left, right) => {
      const leftAnchor = getLayerAnchor(left.id, incoming, previousLayerPositions);
      const rightAnchor = getLayerAnchor(right.id, incoming, previousLayerPositions);
      if (leftAnchor !== rightAnchor) {
        return leftAnchor - rightAnchor;
      }

      return compareNodesForLayout(left, right);
    });

    const layerHeight = Math.max(layerNodes.length - 1, 0) * DEFAULT_LAYOUT_Y;
    const startY = layerHeight === 0 ? 0 : -Math.round(layerHeight / 2);
    layerNodes.forEach((node, index) => {
      const y = startY + index * DEFAULT_LAYOUT_Y;
      positions.set(node.id, {
        x: layer * DEFAULT_LAYOUT_X,
        y,
      });
    });
    previousLayerPositions = new Map(layerNodes.map((node, index) => [node.id, startY + index * DEFAULT_LAYOUT_Y]));
  }

  return positions;
}

function getLayerAnchor(
  nodeId: string,
  incoming: Map<string, Set<string>>,
  previousLayerPositions: Map<string, number>
): number {
  const sourcePositions = [...(incoming.get(nodeId) ?? [])]
    .map((sourceId) => previousLayerPositions.get(sourceId))
    .filter((value): value is number => typeof value === "number");

  if (sourcePositions.length === 0) {
    return 0;
  }

  return sourcePositions.reduce((sum, value) => sum + value, 0) / sourcePositions.length;
}

function compareNodesForLayout(
  left?: Pick<WorkflowBuilderNodeDraft, "sortOrder" | "nodeKey">,
  right?: Pick<WorkflowBuilderNodeDraft, "sortOrder" | "nodeKey">
): number {
  if (!left && !right) {
    return 0;
  }
  if (!left) {
    return 1;
  }
  if (!right) {
    return -1;
  }

  const leftOrder = parseSortOrder(left.sortOrder);
  const rightOrder = parseSortOrder(right.sortOrder);
  if (leftOrder !== rightOrder) {
    return leftOrder - rightOrder;
  }

  return normalizeNodeKey(left.nodeKey).localeCompare(normalizeNodeKey(right.nodeKey));
}

function normalizeNodeKey(value: string | null | undefined): string {
  return (value ?? "").trim().toLowerCase();
}

function parseSortOrder(value: string): number {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : Number.MAX_SAFE_INTEGER;
}

export function validateWorkflowBuilderDraft(draft: WorkflowBuilderVersionDraft): WorkflowBuilderLocalIssue[] {
  const issues: WorkflowBuilderLocalIssue[] = [];
  const normalizedNodeKeys = new Set<string>();
  let startCount = 0;
  let endCount = 0;

  for (const node of draft.nodes) {
    const nodeKey = node.nodeKey.trim();
    if (!nodeKey) {
      issues.push({ scope: "node", message: "Jeder Schritt braucht einen Schritt-Key." });
    } else {
      const normalized = nodeKey.toLowerCase();
      if (normalizedNodeKeys.has(normalized)) {
        issues.push({ scope: "node", message: `Der Schritt-Key '${nodeKey}' ist doppelt vergeben.`, referenceKey: nodeKey });
      }
      normalizedNodeKeys.add(normalized);
    }

    if (node.nodeType === "start") {
      startCount += 1;
    }

    if (node.nodeType === "end") {
      endCount += 1;
    }

    if (node.configText.trim()) {
      try {
        const parsed = JSON.parse(node.configText);
        if (parsed === null || Array.isArray(parsed) || typeof parsed !== "object") {
          issues.push({ scope: "node", message: `Der Schritt '${nodeKey || "?"}' braucht ein gueltiges JSON-Objekt in der technischen Konfiguration.` });
        }
      } catch {
        issues.push({ scope: "node", message: `Der Schritt '${nodeKey || "?"}' hat ungueltiges JSON in der technischen Konfiguration.` });
      }
    }

    if (node.nodeType !== "automation" && node.actions.length > 0) {
      issues.push({ scope: "node", message: `Der Schritt '${nodeKey || "?"}' darf keine automatischen Aktionen enthalten.` });
    }

    if (node.nodeType === "automation") {
      if (node.actions.length === 0) {
        issues.push({ scope: "node", message: `Die Automatisierung '${nodeKey || "?"}' braucht mindestens eine Aktion.` });
      }

      const executionOrders = new Set<number>();
      for (const action of node.actions) {
        if (!action.actionKey.trim()) {
          issues.push({ scope: "action", message: `Die Automatisierung '${nodeKey || "?"}' enthaelt eine Aktion ohne Aktion-Key.` });
        }

        const parsedOrder = Number(action.executionOrder);
        if (!Number.isInteger(parsedOrder) || parsedOrder <= 0) {
          issues.push({ scope: "action", message: `Die Automatisierung '${nodeKey || "?"}' enthaelt eine Aktion mit ungueltiger Reihenfolge.` });
        } else if (executionOrders.has(parsedOrder)) {
          issues.push({ scope: "action", message: `Die Automatisierung '${nodeKey || "?"}' verwendet die Reihenfolge '${parsedOrder}' doppelt.` });
        } else {
          executionOrders.add(parsedOrder);
        }

        if (action.inputMappingText.trim()) {
          try {
            const parsed = JSON.parse(action.inputMappingText);
            if (parsed === null || Array.isArray(parsed) || typeof parsed !== "object") {
              issues.push({ scope: "action", message: `Die Aktion '${action.actionKey || "?"}' braucht ein JSON-Objekt im Eingabe-Mapping.` });
            }
          } catch {
            issues.push({ scope: "action", message: `Die Aktion '${action.actionKey || "?"}' hat ungueltiges JSON im Eingabe-Mapping.` });
          }
        }
      }
    }
  }

  if (startCount !== 1) {
    issues.push({ scope: "version", message: "Ein Ablauf braucht genau einen Start." });
  }

  if (endCount < 1) {
    issues.push({ scope: "version", message: "Ein Ablauf braucht mindestens ein Ende." });
  }

  const prioritiesBySource = new Set<string>();
  for (const edge of draft.edges) {
    const sourceNodeKey = edge.sourceNodeKey.trim();
    const targetNodeKey = edge.targetNodeKey.trim();
    const normalizedSourceNodeKey = normalizeNodeKey(sourceNodeKey);
    const normalizedTargetNodeKey = normalizeNodeKey(targetNodeKey);
    if (!sourceNodeKey || !targetNodeKey) {
      issues.push({ scope: "edge", message: "Jede Verbindung braucht Quelle und Ziel." });
      continue;
    }

    if (normalizedSourceNodeKey === normalizedTargetNodeKey) {
      issues.push({ scope: "edge", message: `Die Verbindung '${sourceNodeKey} -> ${targetNodeKey}' darf kein Ruecksprung auf denselben Schritt sein.` });
    }

    if (!normalizedNodeKeys.has(normalizedSourceNodeKey) || !normalizedNodeKeys.has(normalizedTargetNodeKey)) {
      issues.push({ scope: "edge", message: `Die Verbindung '${sourceNodeKey} -> ${targetNodeKey}' verweist auf unbekannte Schritte.` });
    }

    const priorityKey = edge.priority.trim();
    if (priorityKey) {
      const compositeKey = `${normalizedSourceNodeKey}::${priorityKey}`;
      if (prioritiesBySource.has(compositeKey)) {
        issues.push({
          scope: "edge",
          message: `Der Schritt '${sourceNodeKey}' verwendet die Reihenfolge '${priorityKey}' mehrfach.`,
          referenceKey: sourceNodeKey,
        });
      } else {
        prioritiesBySource.add(compositeKey);
      }
    }
  }

  return issues;
}

function toNullableText(value: string): string | null {
  const trimmed = value.trim();
  return trimmed ? trimmed : null;
}

function toPositiveInteger(value: string, fallback: number): number {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback;
}

function toNullableInteger(value: number | null): number | null {
  return Number.isInteger(value) ? value : null;
}

function parseOptionalJsonObject(value: string): unknown | null {
  if (!value.trim()) {
    return null;
  }

  const parsed = JSON.parse(value);
  return parsed;
}

export function isDefinitionMetadataChanged(
  summary: AdminWorkflowDefinitionSummary | null,
  name: string,
  description: string
): boolean {
  if (!summary) {
    return false;
  }

  return summary.name !== name.trim() || (summary.description ?? "") !== description.trim();
}

export function findVersionSummary(
  definitions: AdminWorkflowDefinitionSummary[],
  versionId: number | null
): AdminWorkflowDefinitionVersionSummary | null {
  if (!versionId) {
    return null;
  }

  for (const definition of definitions) {
    const match = definition.versions.find((version) => version.id === versionId);
    if (match) {
      return match;
    }
  }

  return null;
}
