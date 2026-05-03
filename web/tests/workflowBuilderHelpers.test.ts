import { describe, expect, it } from "vitest";
import { parseCondition } from "../src/components/admin-config/workflowBuilderEditorHelpers";
import { topologicallyOrderNodes } from "../src/hooks/adminWorkflowBuilderModel";
import type {
  WorkflowBuilderEdgeDraft,
  WorkflowBuilderNodeDraft,
} from "../src/hooks/adminWorkflowBuilderModel";

// ─── parseCondition edge cases ───────────────────────────────────────────────

describe("parseCondition", () => {
  it("ignores unknown extra fields and returns valid ParsedCondition", () => {
    const result = parseCondition(
      JSON.stringify({
        answerKey: "has_laptop",
        operator: "is_true",
        unknownField: { deeply: { nested: true } },
        anotherArray: [1, 2, 3],
      })
    );
    expect(result).not.toBe("invalid");
    if (result === "invalid") return;
    expect(result.answerKey).toBe("has_laptop");
    expect(result.operator).toBe("is_true");
  });

  it("returns invalid when operator is an object instead of string", () => {
    expect(
      parseCondition(JSON.stringify({ answerKey: "key", operator: { nested: true } }))
    ).toBe("invalid");
  });

  it("returns invalid when top-level value is an array", () => {
    expect(parseCondition(JSON.stringify([{ answerKey: "key", operator: "eq" }]))).toBe("invalid");
  });

  it("returns empty ParsedCondition for empty/whitespace input", () => {
    const result = parseCondition("   ");
    expect(result).not.toBe("invalid");
    if (result === "invalid") return;
    expect(result.answerKey).toBe("");
    expect(result.operator).toBe("is_true");
  });
});

// ─── topologicallyOrderNodes cycle/edge cases ────────────────────────────────

function makeNode(
  id: string,
  nodeKey: string,
  sortIndex: number
): WorkflowBuilderNodeDraft {
  return {
    id,
    nodeKey,
    nodeType: "task",
    title: `Node ${id}`,
    sortOrder: String(sortIndex),
    positionX: null,
    positionY: null,
    configText: "",
    actions: [],
  };
}

function makeEdge(sourceNodeKey: string, targetNodeKey: string): WorkflowBuilderEdgeDraft {
  return {
    id: `${sourceNodeKey}->${targetNodeKey}`,
    sourceNodeKey,
    targetNodeKey,
    priority: 0,
    conditionExpression: "",
  };
}

describe("topologicallyOrderNodes", () => {
  it("returns all nodes even when a full cycle A→B→A exists", () => {
    const a = makeNode("1", "a", 0);
    const b = makeNode("2", "b", 1);
    const nodes = [a, b];
    const edges = [makeEdge("a", "b"), makeEdge("b", "a")];

    const result = topologicallyOrderNodes(nodes, edges);

    expect(result).toHaveLength(2);
    const ids = result.map((n) => n.id);
    expect(ids).toContain("1");
    expect(ids).toContain("2");
  });

  it("appends cyclic nodes after acyclic ones in original array order", () => {
    // c is the entry, a↔b form a cycle
    const a = makeNode("1", "a", 0);
    const b = makeNode("2", "b", 1);
    const c = makeNode("3", "c", 2);
    const nodes = [a, b, c];
    const edges = [makeEdge("a", "b"), makeEdge("b", "a"), makeEdge("c", "a")];

    const result = topologicallyOrderNodes(nodes, edges);

    // c has in-degree 0 and should appear first
    expect(result[0]!.id).toBe("3");
    // a and b are in a cycle — both must still be present
    expect(result).toHaveLength(3);
  });

  it("handles empty edges (no ordering constraint) preserving array order", () => {
    const nodes = [makeNode("1", "x", 0), makeNode("2", "y", 1), makeNode("3", "z", 2)];
    const result = topologicallyOrderNodes(nodes, []);
    expect(result.map((n) => n.id)).toEqual(["1", "2", "3"]);
  });
});
