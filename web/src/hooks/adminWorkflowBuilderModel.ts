import type {
  AdminWorkflowDefinitionEdge,
  AdminWorkflowDefinitionNode,
  AdminWorkflowDefinitionSummary,
  AdminWorkflowDefinitionVersionDetail,
  AdminWorkflowDefinitionVersionSummary,
  AdminWorkflowNodeAction,
} from "../types/auth";
import { buildWorkflowBuilderStructuredLayout } from "../components/admin-config/workflowBuilderStructuredLayout";

export type WorkflowBuilderNodeDraft = {
  id: string;
  nodeKey: string;
  nodeType:
    | "start"
    | "end"
    | "form"
    | "approval"
    | "task"
    | "decision"
    | "automation"
    | "parallel_split"
    | "parallel_join";
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

  return autoLayoutVersionDraft(draft);
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
    case "parallel_split":
    case "parallel_join":
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
  const fallback = buildStructuredPositions(nodes, edges);
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
  const fallback = buildStructuredPositions(nodes, edges);
  return nodes.map((node) => {
    const position = fallback.get(node.id);
    return {
      ...node,
      positionX: position?.x ?? 0,
      positionY: position?.y ?? 0,
    };
  });
}

function buildStructuredPositions(
  nodes: WorkflowBuilderNodeDraft[],
  edges: WorkflowBuilderEdgeDraft[]
): Map<string, { x: number; y: number }> {
  const layout = buildWorkflowBuilderStructuredLayout({ nodes, edges });
  return layout.positionsBySourceNodeId;
}

function normalizeNodeKey(value: string | null | undefined): string {
  return (value ?? "").trim().toLowerCase();
}

export function validateWorkflowBuilderDraft(draft: WorkflowBuilderVersionDraft): WorkflowBuilderLocalIssue[] {
  const issues: WorkflowBuilderLocalIssue[] = [];
  const normalizedNodeKeys = new Set<string>();
  let startCount = 0;
  let endCount = 0;
  const incomingCounts = new Map<string, number>();
  const outgoingCounts = new Map<string, number>();

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
      incomingCounts.set(normalized, incomingCounts.get(normalized) ?? 0);
      outgoingCounts.set(normalized, outgoingCounts.get(normalized) ?? 0);
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
          issues.push({ scope: "node", message: `Der Schritt '${nodeKey || "?"}' braucht ein gültiges JSON-Objekt in der technischen Konfiguration.` });
        }
      } catch {
        issues.push({ scope: "node", message: `Der Schritt '${nodeKey || "?"}' hat ungültiges JSON in der technischen Konfiguration.` });
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
          issues.push({ scope: "action", message: `Die Automatisierung '${nodeKey || "?"}' enthält eine Aktion ohne Aktion-Key.` });
        }

        const parsedOrder = Number(action.executionOrder);
        if (!Number.isInteger(parsedOrder) || parsedOrder <= 0) {
          issues.push({ scope: "action", message: `Die Automatisierung '${nodeKey || "?"}' enthält eine Aktion mit ungültiger Reihenfolge.` });
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
            issues.push({ scope: "action", message: `Die Aktion '${action.actionKey || "?"}' hat ungültiges JSON im Eingabe-Mapping.` });
          }
        }
      }
    }

    if ((node.nodeType === "parallel_split" || node.nodeType === "parallel_join") && node.configText.trim()) {
      issues.push({ scope: "node", message: `Der Gateway-Schritt '${nodeKey || "?"}' darf keine technische Konfiguration enthalten.` });
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
      issues.push({ scope: "edge", message: `Die Verbindung '${sourceNodeKey} -> ${targetNodeKey}' darf kein Rücksprung auf denselben Schritt sein.` });
    }

    if (!normalizedNodeKeys.has(normalizedSourceNodeKey) || !normalizedNodeKeys.has(normalizedTargetNodeKey)) {
      issues.push({ scope: "edge", message: `Die Verbindung '${sourceNodeKey} -> ${targetNodeKey}' verweist auf unbekannte Schritte.` });
    }

    if (normalizedNodeKeys.has(normalizedSourceNodeKey)) {
      outgoingCounts.set(normalizedSourceNodeKey, (outgoingCounts.get(normalizedSourceNodeKey) ?? 0) + 1);
    }

    if (normalizedNodeKeys.has(normalizedTargetNodeKey)) {
      incomingCounts.set(normalizedTargetNodeKey, (incomingCounts.get(normalizedTargetNodeKey) ?? 0) + 1);
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

  for (const node of draft.nodes) {
    const normalizedNodeKey = normalizeNodeKey(node.nodeKey);
    if (!normalizedNodeKey) {
      continue;
    }

    const incomingCount = incomingCounts.get(normalizedNodeKey) ?? 0;
    const outgoingCount = outgoingCounts.get(normalizedNodeKey) ?? 0;

    if (node.nodeType === "decision" && outgoingCount < 2) {
      issues.push({ scope: "node", message: `Die Entscheidung '${node.nodeKey || "?"}' braucht mindestens zwei Folgepfade.` });
    }

    if (node.nodeType === "parallel_split" && outgoingCount < 2) {
      issues.push({ scope: "node", message: `Der Parallel-Split '${node.nodeKey || "?"}' braucht mindestens zwei ausgehende Pfade.` });
    }

    if (node.nodeType === "parallel_join" && incomingCount < 2) {
      issues.push({ scope: "node", message: `Der Parallel-Join '${node.nodeKey || "?"}' braucht mindestens zwei eingehende Pfade.` });
    }

    if (!["decision", "parallel_split"].includes(node.nodeType) && outgoingCount > 1) {
      issues.push({
        scope: "node",
        message: `Der Schritt '${node.nodeKey || "?"}' darf nur einen Folgepfad haben. Für echte Parallelität nutze Parallel-Split.`,
      });
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
