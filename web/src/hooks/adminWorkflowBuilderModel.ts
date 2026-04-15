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
    | "measure_provision"
    | "measure_deprovision"
    | "measure_change"
    | "measure_rename"
    | "setup"
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

const MEASURE_GENERATION_NODE_TYPES = [
  "setup",
  "measure_provision",
  "measure_deprovision",
  "measure_change",
  "measure_rename",
] as const satisfies readonly WorkflowBuilderNodeDraft["nodeType"][];

const PROCESS_MEASURE_NODE_TYPES = {
  onboarding: "measure_provision",
  offboarding: "measure_deprovision",
  department_change: "measure_change",
  position_change: "measure_change",
  role_change: "measure_change",
  name_change: "measure_rename",
} as const satisfies Partial<Record<string, WorkflowBuilderNodeDraft["nodeType"]>>;

const LEGACY_SETUP_TITLES = new Set([
  "setup",
  "it/fachbereichs-setup",
  "legacy setup",
  "legacy-setup",
]);

export function createLocalId(prefix: string): string {
  return `${prefix}_${Math.random().toString(36).slice(2, 10)}`;
}

export function isMeasureGenerationNodeType(
  nodeType: string | null | undefined
): nodeType is Extract<WorkflowBuilderNodeDraft["nodeType"], "setup" | "measure_provision" | "measure_deprovision" | "measure_change" | "measure_rename"> {
  return typeof nodeType === "string"
    && MEASURE_GENERATION_NODE_TYPES.includes(nodeType as (typeof MEASURE_GENERATION_NODE_TYPES)[number]);
}

export function getExpectedMeasureNodeTypeForProcessKey(
  processTypeKey: string | null | undefined
): Extract<WorkflowBuilderNodeDraft["nodeType"], "measure_provision" | "measure_deprovision" | "measure_change" | "measure_rename"> | null {
  const normalizedProcessTypeKey = processTypeKey?.trim().toLowerCase() ?? "";
  return PROCESS_MEASURE_NODE_TYPES[normalizedProcessTypeKey as keyof typeof PROCESS_MEASURE_NODE_TYPES] ?? null;
}

export function getDefaultWorkflowBuilderNodeTitle(
  nodeType: WorkflowBuilderNodeDraft["nodeType"]
): string {
  switch (nodeType) {
    case "measure_provision":
      return "Bereitstellungsmaßnahmen erzeugen";
    case "measure_deprovision":
      return "Entzugsmaßnahmen erzeugen";
    case "measure_change":
      return "Änderungsmaßnahmen erzeugen";
    case "measure_rename":
      return "Umbenennungsmaßnahmen erzeugen";
    default:
      return "";
  }
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
    title: getDefaultWorkflowBuilderNodeTitle(nodeType),
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
    nodes: detail.nodes.map((node) => toNodeDraft(node, detail.primaryLegacyProcessTypeKey ?? null)),
    edges: detail.edges.map(toEdgeDraft),
  };

  return autoLayoutVersionDraft(draft);
}

function toNodeDraft(
  node: AdminWorkflowDefinitionNode,
  primaryLegacyProcessTypeKey: string | null
): WorkflowBuilderNodeDraft {
  const normalizedSourceNodeType = normalizeNodeType(node.nodeType);
  const nodeType = normalizeLifecycleMeasureNodeType(
    normalizedSourceNodeType,
    primaryLegacyProcessTypeKey
  );
  return {
    id: createLocalId("node"),
    nodeKey: node.nodeKey ?? "",
    nodeType,
    title: normalizeLifecycleMeasureNodeTitle(node.title, normalizedSourceNodeType, nodeType),
    sortOrder: String(node.sortOrder),
    positionX: node.positionX ?? null,
    positionY: node.positionY ?? null,
    configText: toJsonText(node.config),
    actions: node.actions.map(toActionDraft),
  };
}

function normalizeLifecycleMeasureNodeType(
  nodeType: WorkflowBuilderNodeDraft["nodeType"],
  primaryLegacyProcessTypeKey: string | null
): WorkflowBuilderNodeDraft["nodeType"] {
  if (nodeType !== "setup") {
    return nodeType;
  }

  return getExpectedMeasureNodeTypeForProcessKey(primaryLegacyProcessTypeKey) ?? nodeType;
}

function normalizeLifecycleMeasureNodeTitle(
  title: string | null,
  sourceNodeType: WorkflowBuilderNodeDraft["nodeType"],
  normalizedNodeType: WorkflowBuilderNodeDraft["nodeType"]
): string {
  const normalizedTitle = title ?? "";
  if (sourceNodeType !== "setup" || normalizedNodeType === "setup") {
    return normalizedTitle;
  }

  const compactTitle = normalizedTitle.trim().toLowerCase();
  if (!compactTitle || LEGACY_SETUP_TITLES.has(compactTitle)) {
    return getDefaultWorkflowBuilderNodeTitle(normalizedNodeType);
  }

  return normalizedTitle;
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
    case "measure_provision":
    case "measure_deprovision":
    case "measure_change":
    case "measure_rename":
    case "setup":
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

  validateMeasurePhaseStructure(draft, issues);
  validateMeasurePhaseProcessCompatibility(draft, issues);

  return issues;
}

function validateMeasurePhaseStructure(
  draft: WorkflowBuilderVersionDraft,
  issues: WorkflowBuilderLocalIssue[]
) {
  const measureNodes = draft.nodes.filter((node) => isMeasureGenerationNodeType(node.nodeType));
  if (measureNodes.length === 0) {
    return;
  }

  if (measureNodes.length !== 1) {
    issues.push({ scope: "version", message: "Ein fachlicher Ablauf mit Maßnahmen-Baustein braucht genau einen Maßnahmen-Schritt." });
    return;
  }

  const technicalNodes = draft.nodes.filter((node) =>
    ["task", "decision", "parallel_split", "parallel_join", "automation"].includes(node.nodeType)
  );
  for (const node of technicalNodes) {
    issues.push({
      scope: "node",
      message: `Ein Ablauf mit Maßnahmen-Baustein darf keinen technischen Hauptschritt vom Typ '${node.nodeType}' enthalten.`,
      referenceKey: node.nodeKey.trim() || undefined,
    });
  }

  const startNode = draft.nodes.find((node) => node.nodeType === "start") ?? null;
  const formNodes = draft.nodes.filter((node) => node.nodeType === "form");
  const approvalNodes = draft.nodes.filter((node) => node.nodeType === "approval");
  const endNodes = draft.nodes.filter((node) => node.nodeType === "end");
  if (!startNode || formNodes.length !== 1 || endNodes.length !== 1 || approvalNodes.length > 1) {
    return;
  }

  const measureNode = measureNodes[0]!;
  const formNode = formNodes[0]!;
  const endNode = endNodes[0]!;
  const approvalNode = approvalNodes[0] ?? null;
  const expectedEdges = approvalNode
    ? [
        [startNode.nodeKey, formNode.nodeKey],
        [formNode.nodeKey, approvalNode.nodeKey],
        [approvalNode.nodeKey, measureNode.nodeKey],
        [measureNode.nodeKey, endNode.nodeKey],
      ]
    : [
        [startNode.nodeKey, formNode.nodeKey],
        [formNode.nodeKey, measureNode.nodeKey],
        [measureNode.nodeKey, endNode.nodeKey],
      ];

  const normalizedActualEdges = draft.edges.map((edge) => [
    normalizeNodeKey(edge.sourceNodeKey),
    normalizeNodeKey(edge.targetNodeKey),
  ]);
  if (normalizedActualEdges.length !== expectedEdges.length) {
    issues.push({
      scope: "version",
      message: "Ein fachlicher Ablauf mit Maßnahmen-Baustein darf nur Start, Formular, optionale Freigabe, Maßnahmen und Abschluss im Hauptfluss enthalten.",
    });
    return;
  }

  for (const [sourceNodeKey, targetNodeKey] of expectedEdges) {
    const hasExpectedEdge = normalizedActualEdges.some(([source, target]) =>
      source === normalizeNodeKey(sourceNodeKey) && target === normalizeNodeKey(targetNodeKey)
    );
    if (!hasExpectedEdge) {
      issues.push({
        scope: "edge",
        message: `Im Hauptfluss fehlt die Verbindung '${sourceNodeKey} -> ${targetNodeKey}'.`,
        referenceKey: sourceNodeKey,
      });
    }
  }
}

function validateMeasurePhaseProcessCompatibility(
  draft: WorkflowBuilderVersionDraft,
  issues: WorkflowBuilderLocalIssue[]
) {
  const expectedMeasureNodeType = getExpectedMeasureNodeTypeForProcessKey(draft.primaryLegacyProcessTypeKey);
  if (!expectedMeasureNodeType) {
    return;
  }

  const measureNodes = draft.nodes.filter((node) => isMeasureGenerationNodeType(node.nodeType));
  if (measureNodes.length !== 1) {
    return;
  }

  const measureNode = measureNodes[0]!;
  if (measureNode.nodeType === "setup" || measureNode.nodeType === expectedMeasureNodeType) {
    return;
  }

  issues.push({
    scope: "node",
    message: `Der Prozess '${draft.primaryLegacyProcessTypeKey.trim()}' benötigt den Baustein '${getDefaultWorkflowBuilderNodeTitle(expectedMeasureNodeType)}'.`,
    referenceKey: measureNode.nodeKey.trim() || undefined,
  });
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
