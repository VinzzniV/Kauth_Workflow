import type {
  AdminWorkflowDefinitionEdge,
  AdminWorkflowDefinitionNode,
  AdminWorkflowDefinitionNodeSpec,
  AdminWorkflowDefinitionNodeSpecCondition,
  AdminWorkflowDefinitionNodeSpecDependency,
  AdminWorkflowDefinitionSummary,
  AdminWorkflowDefinitionVersionDetail,
  AdminWorkflowDefinitionVersionSummary,
  AdminWorkflowNodeAction,
} from "../types/auth";
import { isAutomationAdminRoleSlug } from "../utils/automationAdminRoles";

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
  // FE-9: Specs reisen mit der Version. Builder editiert sie noch nicht inline,
  // muss sie aber durch Save round-trippen, sonst werden sie beim Replace
  // ueber die DELETE/CASCADE-Logik geloescht. Pflege passiert (vorerst) im
  // AdminTaskTemplate-Editor. Bei UI-Inline-Edit wird dieses Feld aufgeruestet.
  specs: AdminWorkflowDefinitionNodeSpec[];
  // Slice 4 (Admin-Gated-Automation): Approval-Rolle fuer task-Nodes mit
  // Action-Bundle. Whitelist auth_admin/auth_hr/auth_manager; null fuer alle
  // anderen Node-Typen.
  automationAdminRole: string | null;
};

// Slice 4 (Admin-Gated-Automation, Builder-UI): Single Source of Truth fuer
// "welche Node-Typen duerfen Action-Bundles tragen". Mirror Backend
// WorkflowDefinitionValidationCatalog.AllowsActions. Wird im Hook (Validation +
// addActionFromDefinition) UND im Save-Mapping unten genutzt — Drift zwischen
// den drei Stellen wuerde die UI inkonsistent machen.
export function nodeTypeAllowsActions(nodeType: WorkflowBuilderNodeDraft["nodeType"]): boolean {
  return nodeType === "automation" || nodeType === "task";
}

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
  workflowDefinitionKey: string;
  nodes: WorkflowBuilderNodeDraft[];
  edges: WorkflowBuilderEdgeDraft[];
};

export type WorkflowBuilderLocalIssue = {
  scope: "definition" | "version" | "node" | "edge" | "action";
  message: string;
  referenceKey?: string;
};

const MEASURE_GENERATION_NODE_TYPES = [
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

export function createLocalId(prefix: string): string {
  return `${prefix}_${Math.random().toString(36).slice(2, 10)}`;
}

export function isMeasureGenerationNodeType(
  nodeType: string | null | undefined
): nodeType is Extract<WorkflowBuilderNodeDraft["nodeType"], "measure_provision" | "measure_deprovision" | "measure_change" | "measure_rename"> {
  return typeof nodeType === "string"
    && MEASURE_GENERATION_NODE_TYPES.includes(nodeType as (typeof MEASURE_GENERATION_NODE_TYPES)[number]);
}

export function getExpectedMeasureNodeTypeForDefinitionKey(
  workflowDefinitionKey: string | null | undefined
): Extract<WorkflowBuilderNodeDraft["nodeType"], "measure_provision" | "measure_deprovision" | "measure_change" | "measure_rename"> | null {
  const normalizedWorkflowDefinitionKey = workflowDefinitionKey?.trim().toLowerCase() ?? "";
  return PROCESS_MEASURE_NODE_TYPES[normalizedWorkflowDefinitionKey as keyof typeof PROCESS_MEASURE_NODE_TYPES] ?? null;
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
    workflowDefinitionKey: "",
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
    specs: [],
    automationAdminRole: null,
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
  // Der Builder arbeitet intern auf dem kanonischen Definition-Key.
  return {
    name: detail.name ?? "",
    description: detail.description ?? "",
    workflowDefinitionKey: detail.definitionKey,
    nodes: detail.nodes.map(toNodeDraft),
    edges: detail.edges.map(toEdgeDraft),
  };
}

function toNodeDraft(
  node: AdminWorkflowDefinitionNode
): WorkflowBuilderNodeDraft {
  return {
    id: createLocalId("node"),
    nodeKey: node.nodeKey ?? "",
    nodeType: normalizeNodeType(node.nodeType ?? ""),
    title: node.title ?? "",
    sortOrder: String(node.sortOrder),
    positionX: node.positionX ?? null,
    positionY: node.positionY ?? null,
    configText: toJsonText(node.config),
    actions: node.actions.map(toActionDraft),
    // FE-9: Specs durchreichen (frueher fehlte das, weshalb Replace die Specs geloescht haette).
    // Defensive []-Default, falls Backend ein altes (Pre-FE-9) Detail-DTO liefert.
    specs: node.specs ?? [],
    // Slice 4: Approval-Rolle (kanonisch lowercase oder null). Defensive ??.
    automationAdminRole: node.automationAdminRole ?? null,
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
    case "measure_provision":
    case "measure_deprovision":
    case "measure_change":
    case "measure_rename":
    case "decision":
    case "automation":
    case "parallel_split":
    case "parallel_join":
      return value!.trim().toLowerCase() as WorkflowBuilderNodeDraft["nodeType"];
    default:
      return "task";
  }
}

/**
 * Topologische Sortierung der Knoten anhand der Edges (Kahn).
 * Tie-Breaker: ursprüngliche Array-Position (= manuelle Reihenfolge via moveNode + sortOrder).
 * Knoten ohne nodeKey oder Knoten in Zyklen werden ans Ende angehängt in Array-Reihenfolge.
 */
export function topologicallyOrderNodes(
  nodes: WorkflowBuilderNodeDraft[],
  edges: WorkflowBuilderEdgeDraft[]
): WorkflowBuilderNodeDraft[] {
  const indexById = new Map(nodes.map((node, idx) => [node.id, idx] as const));
  const nodesByNormalizedKey = new Map<string, WorkflowBuilderNodeDraft>();
  for (const node of nodes) {
    const key = node.nodeKey.trim().toLowerCase();
    if (key && !nodesByNormalizedKey.has(key)) {
      nodesByNormalizedKey.set(key, node);
    }
  }

  const inDegree = new Map<string, number>();
  const adjacency = new Map<string, string[]>();
  for (const node of nodes) {
    inDegree.set(node.id, 0);
    adjacency.set(node.id, []);
  }

  for (const edge of edges) {
    const source = nodesByNormalizedKey.get(edge.sourceNodeKey.trim().toLowerCase());
    const target = nodesByNormalizedKey.get(edge.targetNodeKey.trim().toLowerCase());
    if (!source || !target || source.id === target.id) continue;
    adjacency.get(source.id)!.push(target.id);
    inDegree.set(target.id, (inDegree.get(target.id) ?? 0) + 1);
  }

  const ready: string[] = [];
  for (const node of nodes) {
    if ((inDegree.get(node.id) ?? 0) === 0) {
      ready.push(node.id);
    }
  }
  // Stabile Sortierung nach Array-Index — sorgt für deterministische Reihenfolge bei
  // mehreren Wurzeln und Tie-Break bei parallelen Pfaden.
  ready.sort((a, b) => (indexById.get(a) ?? 0) - (indexById.get(b) ?? 0));

  const visited = new Set<string>();
  const result: WorkflowBuilderNodeDraft[] = [];
  while (ready.length > 0) {
    const nextId = ready.shift()!;
    if (visited.has(nextId)) continue;
    visited.add(nextId);
    const node = nodes.find((n) => n.id === nextId);
    if (node) result.push(node);
    const successors = adjacency.get(nextId) ?? [];
    const newlyReady: string[] = [];
    for (const succId of successors) {
      const next = (inDegree.get(succId) ?? 0) - 1;
      inDegree.set(succId, next);
      if (next === 0 && !visited.has(succId)) {
        newlyReady.push(succId);
      }
    }
    if (newlyReady.length > 0) {
      ready.push(...newlyReady);
      ready.sort((a, b) => (indexById.get(a) ?? 0) - (indexById.get(b) ?? 0));
    }
  }

  // Knoten in Zyklen oder ohne erreichbare Wurzel: an Array-Reihenfolge anhängen.
  for (const node of nodes) {
    if (!visited.has(node.id)) {
      result.push(node);
    }
  }

  return result;
}

export function toJsonText(value: unknown | null): string {
  if (value === null || typeof value === "undefined") {
    return "";
  }

  return JSON.stringify(value, null, 2);
}

export function buildVersionReplacePayload(
  draft: WorkflowBuilderVersionDraft,
  expectedUpdatedAt?: string | null
) {
  return {
    name: toNullableText(draft.name),
    description: toNullableText(draft.description),
    expectedUpdatedAt: expectedUpdatedAt ?? null,
    nodes: draft.nodes.map((node, index) => ({
      nodeKey: toNullableText(node.nodeKey),
      nodeType: node.nodeType,
      title: toNullableText(node.title),
      sortOrder: toPositiveInteger(node.sortOrder, index + 1),
      positionX: toNullableInteger(node.positionX),
      positionY: toNullableInteger(node.positionY),
      config: parseOptionalJsonObject(node.configText),
      // Slice 4: Approval-Rolle nur an task-Nodes; bei automation oder anderen
      // immer null (Backend ignoriert/lehnt sonst ab). Trim+lowercase damit
      // identisch zum Backend NormalizeAutomationAdminRole-Pfad.
      automationAdminRole: node.nodeType === "task"
        ? (node.automationAdminRole?.trim().toLowerCase() || null)
        : null,
      actions: nodeTypeAllowsActions(node.nodeType)
        ? node.actions.map((action, actionIndex) => ({
            actionKey: toNullableText(action.actionKey),
            inputMapping: parseOptionalJsonObject(action.inputMappingText),
            executionOrder: toPositiveInteger(action.executionOrder, actionIndex + 1),
            onErrorBehavior: action.onErrorBehavior,
          }))
        : [],
      // FE-9: Specs werden am Save mitgeschickt — sonst loescht der Backend-Replace
      // (DELETE FROM workflow_nodes -> CASCADE) sie. Builder editiert sie noch nicht
      // inline, aber Round-Trip muss funktionieren.
      specs: node.specs.map(toSpecPayload),
    })),
    edges: draft.edges.map((edge, index) => ({
      sourceNodeKey: toNullableText(edge.sourceNodeKey),
      targetNodeKey: toNullableText(edge.targetNodeKey),
      priority: toPositiveInteger(edge.priority, index + 1),
      conditionExpression: toNullableText(edge.conditionExpression),
    })),
  };
}

function toSpecPayload(spec: AdminWorkflowDefinitionNodeSpec) {
  return {
    specKey: spec.specKey,
    title: spec.title,
    category: spec.category,
    description: spec.description,
    iconKey: spec.iconKey,
    defaultResponsibilityId: spec.defaultResponsibilityId,
    processAreaLabel: spec.processAreaLabel,
    isDepartmentPhaseTask: spec.isDepartmentPhaseTask,
    isRequired: spec.isRequired,
    dueInDays: spec.dueInDays,
    sortOrder: spec.sortOrder,
    conditions: spec.conditions.map(toSpecConditionPayload),
    dependencies: spec.dependencies.map(toSpecDependencyPayload),
  };
}

function toSpecConditionPayload(condition: AdminWorkflowDefinitionNodeSpecCondition) {
  return {
    answerKey: condition.answerKey,
    operator: condition.operator,
    expectedValueText: condition.expectedValueText,
    expectedValueBoolean: condition.expectedValueBoolean,
    expectedValueNumber: condition.expectedValueNumber,
  };
}

function toSpecDependencyPayload(dependency: AdminWorkflowDefinitionNodeSpecDependency) {
  return {
    dependsOnSpecKey: dependency.dependsOnSpecKey,
  };
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
    const refKey = nodeKey || undefined;
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
          issues.push({ scope: "node", message: `Der Schritt '${nodeKey || "?"}' braucht ein gültiges JSON-Objekt in der technischen Konfiguration.`, referenceKey: refKey });
        }
      } catch {
        issues.push({ scope: "node", message: `Der Schritt '${nodeKey || "?"}' hat ungültiges JSON in der technischen Konfiguration.`, referenceKey: refKey });
      }
    }

    // Slice 4 (Admin-Gated-Automation, Builder-UI): Actions sind erlaubt fuer
    // automation und task; alle anderen Node-Typen lehnen Actions UND
    // automationAdminRole ab. Mirror der Slice-2-Backend-Regeln, damit der
    // Admin keine "Surprise"-Validation erst beim Save bekommt.
    if (!nodeTypeAllowsActions(node.nodeType) && node.actions.length > 0) {
      issues.push({ scope: "node", message: `Der Schritt '${nodeKey || "?"}' darf keine automatischen Aktionen enthalten.`, referenceKey: refKey });
    }

    if (node.nodeType !== "task" && node.automationAdminRole) {
      issues.push({ scope: "node", message: `Der Schritt '${nodeKey || "?"}' darf keine Approval-Rolle setzen.`, referenceKey: refKey });
    }

    if (node.nodeType === "task") {
      if (node.actions.length === 0 && node.automationAdminRole) {
        issues.push({ scope: "node", message: `Der Schritt '${nodeKey || "?"}' hat eine Approval-Rolle gesetzt, aber keine Aktionen.`, referenceKey: refKey });
      }
      if (node.actions.length > 0 && !node.automationAdminRole) {
        issues.push({ scope: "node", message: `Der Schritt '${nodeKey || "?"}' hat Aktionen, aber keine Approval-Rolle gewählt.`, referenceKey: refKey });
      } else if (node.actions.length > 0
        && node.automationAdminRole
        && !isAutomationAdminRoleSlug(node.automationAdminRole)) {
        issues.push({ scope: "node", message: `Der Schritt '${nodeKey || "?"}' nutzt eine unbekannte Approval-Rolle ('${node.automationAdminRole}').`, referenceKey: refKey });
      }
    }

    if (node.nodeType === "automation") {
      if (node.actions.length === 0) {
        issues.push({ scope: "node", message: `Die Automatisierung '${nodeKey || "?"}' braucht mindestens eine Aktion.`, referenceKey: refKey });
      }

      const executionOrders = new Set<number>();
      for (const action of node.actions) {
        if (!action.actionKey.trim()) {
          issues.push({ scope: "action", message: `Die Automatisierung '${nodeKey || "?"}' enthält eine Aktion ohne Aktion-Key.`, referenceKey: refKey });
        }

        const parsedOrder = Number(action.executionOrder);
        if (!Number.isInteger(parsedOrder) || parsedOrder <= 0) {
          issues.push({ scope: "action", message: `Die Automatisierung '${nodeKey || "?"}' enthält eine Aktion mit ungültiger Reihenfolge.`, referenceKey: refKey });
        } else if (executionOrders.has(parsedOrder)) {
          issues.push({ scope: "action", message: `Die Automatisierung '${nodeKey || "?"}' verwendet die Reihenfolge '${parsedOrder}' doppelt.`, referenceKey: refKey });
        } else {
          executionOrders.add(parsedOrder);
        }

        if (action.inputMappingText.trim()) {
          try {
            const parsed = JSON.parse(action.inputMappingText);
            if (parsed === null || Array.isArray(parsed) || typeof parsed !== "object") {
              issues.push({ scope: "action", message: `Die Aktion '${action.actionKey || "?"}' braucht ein JSON-Objekt im Eingabe-Mapping.`, referenceKey: refKey });
            }
          } catch {
            issues.push({ scope: "action", message: `Die Aktion '${action.actionKey || "?"}' hat ungültiges JSON im Eingabe-Mapping.`, referenceKey: refKey });
          }
        }
      }
    }

    if ((node.nodeType === "parallel_split" || node.nodeType === "parallel_join") && node.configText.trim()) {
      issues.push({ scope: "node", message: `Der Gateway-Schritt '${nodeKey || "?"}' darf keine technische Konfiguration enthalten.`, referenceKey: refKey });
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

    const refKey = node.nodeKey.trim() || undefined;

    if (node.nodeType === "decision" && outgoingCount < 2) {
      issues.push({ scope: "node", message: `Die Entscheidung '${node.nodeKey || "?"}' braucht mindestens zwei Folgepfade.`, referenceKey: refKey });
    }

    if (node.nodeType === "parallel_split" && outgoingCount < 2) {
      issues.push({ scope: "node", message: `Der Parallel-Split '${node.nodeKey || "?"}' braucht mindestens zwei ausgehende Pfade.`, referenceKey: refKey });
    }

    if (node.nodeType === "parallel_join" && incomingCount < 2) {
      issues.push({ scope: "node", message: `Der Parallel-Join '${node.nodeKey || "?"}' braucht mindestens zwei eingehende Pfade.`, referenceKey: refKey });
    }

    if (!["decision", "parallel_split"].includes(node.nodeType) && outgoingCount > 1) {
      issues.push({
        scope: "node",
        message: `Der Schritt '${node.nodeKey || "?"}' darf nur einen Folgepfad haben. Für echte Parallelität nutze Parallel-Split.`,
        referenceKey: refKey,
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
  const expectedMeasureNodeType = getExpectedMeasureNodeTypeForDefinitionKey(draft.workflowDefinitionKey);
  if (!expectedMeasureNodeType) {
    return;
  }

  const measureNodes = draft.nodes.filter((node) => isMeasureGenerationNodeType(node.nodeType));
  if (measureNodes.length !== 1) {
    return;
  }

  const measureNode = measureNodes[0]!;
  if (measureNode.nodeType === expectedMeasureNodeType) {
    return;
  }

  issues.push({
    scope: "node",
    message: `Die Workflow-Definition '${draft.workflowDefinitionKey.trim()}' benötigt den Baustein '${getDefaultWorkflowBuilderNodeTitle(expectedMeasureNodeType)}'.`,
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

function parseOptionalJsonObject(value: string): Record<string, unknown> | null {
  if (!value.trim()) {
    return null;
  }

  const parsed: unknown = JSON.parse(value);
  if (parsed === null || typeof parsed !== "object" || Array.isArray(parsed)) {
    throw new Error("Erwartet wurde ein JSON-Objekt.");
  }
  return parsed as Record<string, unknown>;
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
