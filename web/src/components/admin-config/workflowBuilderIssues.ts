import type {
  WorkflowBuilderEdgeDraft,
  WorkflowBuilderLocalIssue,
  WorkflowBuilderNodeDraft,
  WorkflowBuilderVersionDraft,
} from "../../hooks/adminWorkflowBuilderModel";
import type { AdminWorkflowValidationIssue } from "../../types/auth";

export type WorkflowBuilderIssueRef = {
  origin: "local" | "backend";
  scope: string;
  message: string;
  referenceKey: string | null;
  severity: "error" | "warning" | "info";
  code: string | null;
};

export type WorkflowBuilderIssueIndex = {
  byNodeId: Map<string, WorkflowBuilderIssueRef[]>;
  byEdgeId: Map<string, WorkflowBuilderIssueRef[]>;
  general: WorkflowBuilderIssueRef[];
  total: number;
};

function normalize(value: string | null | undefined): string {
  return (value ?? "").trim().toLowerCase();
}

function normalizeBackendSeverity(value: string | null | undefined): WorkflowBuilderIssueRef["severity"] {
  switch (normalize(value)) {
    case "error":
      return "error";
    case "warning":
    case "warn":
      return "warning";
    default:
      return "info";
  }
}

function toRefFromLocal(issue: WorkflowBuilderLocalIssue): WorkflowBuilderIssueRef {
  return {
    origin: "local",
    scope: issue.scope,
    message: issue.message,
    referenceKey: issue.referenceKey ?? null,
    severity: "error",
    code: null,
  };
}

function toRefFromBackend(issue: AdminWorkflowValidationIssue): WorkflowBuilderIssueRef {
  return {
    origin: "backend",
    scope: issue.scope,
    message: issue.message,
    referenceKey: issue.referenceKey,
    severity: normalizeBackendSeverity(issue.severity),
    code: issue.code,
  };
}

export function buildWorkflowBuilderIssueIndex(
  localIssues: WorkflowBuilderLocalIssue[],
  backendIssues: AdminWorkflowValidationIssue[],
  draft: WorkflowBuilderVersionDraft
): WorkflowBuilderIssueIndex {
  const byNodeId = new Map<string, WorkflowBuilderIssueRef[]>();
  const byEdgeId = new Map<string, WorkflowBuilderIssueRef[]>();
  const general: WorkflowBuilderIssueRef[] = [];

  const nodesByKey = new Map<string, WorkflowBuilderNodeDraft>();
  for (const node of draft.nodes) {
    const key = normalize(node.nodeKey);
    if (key && !nodesByKey.has(key)) {
      nodesByKey.set(key, node);
    }
  }

  const edgesBySource = new Map<string, WorkflowBuilderEdgeDraft[]>();
  for (const edge of draft.edges) {
    const key = normalize(edge.sourceNodeKey);
    if (!key) continue;
    const arr = edgesBySource.get(key);
    if (arr) {
      arr.push(edge);
    } else {
      edgesBySource.set(key, [edge]);
    }
  }

  const pushNode = (nodeId: string, ref: WorkflowBuilderIssueRef) => {
    const arr = byNodeId.get(nodeId);
    if (arr) arr.push(ref);
    else byNodeId.set(nodeId, [ref]);
  };

  const pushEdge = (edgeId: string, ref: WorkflowBuilderIssueRef) => {
    const arr = byEdgeId.get(edgeId);
    if (arr) arr.push(ref);
    else byEdgeId.set(edgeId, [ref]);
  };

  const all: WorkflowBuilderIssueRef[] = [
    ...localIssues.map(toRefFromLocal),
    ...backendIssues.map(toRefFromBackend),
  ];

  // Slice 4 (Admin-Gated-Automation): Backend-Snapshot-Validator emittiert
  // Issues mit scope "workflow_node" bzw. "workflow_edge" (z.B. Slice-2-Codes
  // wie "missing_automation_admin_role_for_task_with_actions"). Lokale Issues
  // aus dem Frontend nutzen die kuerzeren scopes "node"/"action"/"edge". Beide
  // Schreibweisen werden hier akzeptiert, damit Backend-Issues nicht im
  // general-Bucket landen statt am betroffenen Step/Uebergang.
  const NODE_SCOPES = new Set(["node", "action", "workflow_node"]);
  const EDGE_SCOPES = new Set(["edge", "workflow_edge"]);

  for (const ref of all) {
    const refKey = normalize(ref.referenceKey);
    const scope = normalize(ref.scope);
    let placed = false;

    if (NODE_SCOPES.has(scope) && refKey) {
      const node = nodesByKey.get(refKey);
      if (node) {
        pushNode(node.id, ref);
        placed = true;
      }
    }

    if (!placed && EDGE_SCOPES.has(scope) && refKey) {
      const sourceEdges = edgesBySource.get(refKey);
      if (sourceEdges && sourceEdges.length > 0) {
        for (const edge of sourceEdges) {
          pushEdge(edge.id, ref);
        }
        placed = true;
      } else {
        const node = nodesByKey.get(refKey);
        if (node) {
          pushNode(node.id, ref);
          placed = true;
        }
      }
    }

    if (!placed) {
      general.push(ref);
    }
  }

  return { byNodeId, byEdgeId, general, total: all.length };
}

export function highestSeverity(issues: WorkflowBuilderIssueRef[] | undefined): WorkflowBuilderIssueRef["severity"] | null {
  if (!issues || issues.length === 0) return null;
  let best: WorkflowBuilderIssueRef["severity"] = "info";
  for (const issue of issues) {
    if (issue.severity === "error") return "error";
    if (issue.severity === "warning") best = "warning";
  }
  return best;
}
