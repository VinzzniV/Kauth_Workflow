import { describe, expect, it } from "vitest";
import {
  autoLayoutVersionDraft,
  buildVersionReplacePayload,
  toVersionDraft,
  validateWorkflowBuilderDraft,
  type WorkflowBuilderVersionDraft,
} from "../src/hooks/adminWorkflowBuilderModel";

describe("adminWorkflowBuilderModel", () => {
  it("recomputes persisted node positions into the structured top-to-bottom layout", () => {
    const draft = toVersionDraft({
      id: 11,
      workflowDefinitionId: 1,
      definitionKey: "onboarding",
      definitionName: "Onboarding",
      definitionDescription: null,
      versionNumber: 1,
      status: "draft",
      name: "Draft 1",
      description: null,
      primaryLegacyProcessTypeKey: "onboarding",
      createdAt: "2026-04-08T10:00:00Z",
      updatedAt: "2026-04-08T10:00:00Z",
      publishedAt: null,
      canPublish: false,
      validationIssues: [],
      nodes: [
        {
          nodeKey: "start",
          nodeType: "start",
          title: "Start",
          sortOrder: 1,
          positionX: 80,
          positionY: 60,
          config: null,
          actions: [],
        },
        {
          nodeKey: "end",
          nodeType: "end",
          title: "End",
          sortOrder: 2,
          positionX: 420,
          positionY: 60,
          config: null,
          actions: [],
        },
      ],
      edges: [
        { sourceNodeKey: "start", targetNodeKey: "end", priority: 1, conditionExpression: null },
      ],
    });

    expect(draft.nodes[0]?.positionX).not.toBe(80);
    expect(draft.nodes[0]?.positionY).not.toBe(60);
    expect((draft.nodes[1]?.positionY ?? 0)).toBeGreaterThan(draft.nodes[0]?.positionY ?? 0);
  });

  it("fills fallback positions for nodes without persisted coordinates", () => {
    const draft = toVersionDraft({
      id: 11,
      workflowDefinitionId: 1,
      definitionKey: "onboarding",
      definitionName: "Onboarding",
      definitionDescription: null,
      versionNumber: 1,
      status: "draft",
      name: "Draft 1",
      description: null,
      primaryLegacyProcessTypeKey: "onboarding",
      createdAt: "2026-04-08T10:00:00Z",
      updatedAt: "2026-04-08T10:00:00Z",
      publishedAt: null,
      canPublish: false,
      validationIssues: [],
      nodes: [
        {
          nodeKey: "start",
          nodeType: "start",
          title: "Start",
          sortOrder: 1,
          positionX: null,
          positionY: null,
          config: null,
          actions: [],
        },
        {
          nodeKey: "task_a",
          nodeType: "task",
          title: "Task",
          sortOrder: 2,
          positionX: null,
          positionY: null,
          config: { legacyTemplateKey: "collect_equipment" },
          actions: [],
        },
        {
          nodeKey: "end",
          nodeType: "end",
          title: "End",
          sortOrder: 3,
          positionX: null,
          positionY: null,
          config: null,
          actions: [],
        },
      ],
      edges: [
        { sourceNodeKey: "start", targetNodeKey: "task_a", priority: 1, conditionExpression: null },
        { sourceNodeKey: "task_a", targetNodeKey: "end", priority: 1, conditionExpression: null },
      ],
    });

    expect(draft.nodes.every((node) => typeof node.positionX === "number" && typeof node.positionY === "number")).toBe(true);
    expect((draft.nodes[1]?.positionY ?? 0)).toBeGreaterThan(draft.nodes[0]?.positionY ?? 0);
  });

  it("serializes node positions into the replace payload", () => {
    const payload = buildVersionReplacePayload({
      name: "Draft 1",
      description: "",
      primaryLegacyProcessTypeKey: "onboarding",
      nodes: [
        {
          id: "node_1",
          nodeKey: "start",
          nodeType: "start",
          title: "Start",
          sortOrder: "1",
          positionX: 120,
          positionY: 80,
          configText: "",
          actions: [],
        },
      ],
      edges: [],
    });

    expect(payload.nodes[0]?.positionX).toBe(120);
    expect(payload.nodes[0]?.positionY).toBe(80);
  });

  it("recomputes positions when auto layout is requested", () => {
    const draft: WorkflowBuilderVersionDraft = {
      name: "",
      description: "",
      primaryLegacyProcessTypeKey: "",
      nodes: [
        {
          id: "node_start",
          nodeKey: "start",
          nodeType: "start",
          title: "Start",
          sortOrder: "1",
          positionX: 999,
          positionY: 999,
          configText: "",
          actions: [],
        },
        {
          id: "node_end",
          nodeKey: "end",
          nodeType: "end",
          title: "End",
          sortOrder: "2",
          positionX: 999,
          positionY: 999,
          configText: "",
          actions: [],
        },
      ],
      edges: [{ id: "edge_1", sourceNodeKey: "start", targetNodeKey: "end", priority: "1", conditionExpression: "" }],
    };

    const relaidOut = autoLayoutVersionDraft(draft);

    expect(relaidOut.nodes[0]?.positionY).not.toBe(999);
    expect(relaidOut.nodes[1]?.positionY).toBeGreaterThan(relaidOut.nodes[0]?.positionY ?? 0);
  });

  it("flags self-loops and duplicate priorities per source locally", () => {
    const issues = validateWorkflowBuilderDraft({
      name: "Draft 1",
      description: "",
      primaryLegacyProcessTypeKey: "onboarding",
      nodes: [
        {
          id: "node_start",
          nodeKey: "start",
          nodeType: "start",
          title: "Start",
          sortOrder: "1",
          positionX: 0,
          positionY: 0,
          configText: "",
          actions: [],
        },
        {
          id: "node_end",
          nodeKey: "end",
          nodeType: "end",
          title: "End",
          sortOrder: "2",
          positionX: 320,
          positionY: 0,
          configText: "",
          actions: [],
        },
      ],
      edges: [
        { id: "edge_1", sourceNodeKey: "start", targetNodeKey: "start", priority: "1", conditionExpression: "" },
        { id: "edge_2", sourceNodeKey: "start", targetNodeKey: "end", priority: "1", conditionExpression: "" },
      ],
    });

    expect(issues).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          scope: "edge",
          message: "Die Verbindung 'start -> start' darf kein Ruecksprung auf denselben Schritt sein.",
        }),
        expect.objectContaining({
          scope: "edge",
          message: "Der Schritt 'start' verwendet die Reihenfolge '1' mehrfach.",
        }),
      ])
    );
  });

  it("keeps decision nodes as decision when mapping version details", () => {
    const draft = toVersionDraft({
      id: 12,
      workflowDefinitionId: 1,
      definitionKey: "routing",
      definitionName: "Routing",
      definitionDescription: null,
      versionNumber: 2,
      status: "draft",
      name: "Draft 2",
      description: null,
      primaryLegacyProcessTypeKey: "routing",
      createdAt: "2026-04-08T10:00:00Z",
      updatedAt: "2026-04-08T10:00:00Z",
      publishedAt: null,
      canPublish: false,
      validationIssues: [],
      nodes: [
        {
          nodeKey: "branch",
          nodeType: "decision",
          title: "Branch",
          sortOrder: 1,
          positionX: 160,
          positionY: 90,
          config: null,
          actions: [],
        },
      ],
      edges: [],
    });

    expect(draft.nodes[0]?.nodeType).toBe("decision");
  });

  it("keeps parallel gateway nodes when mapping version details", () => {
    const draft = toVersionDraft({
      id: 13,
      workflowDefinitionId: 1,
      definitionKey: "parallel_flow",
      definitionName: "Parallel Flow",
      definitionDescription: null,
      versionNumber: 1,
      status: "draft",
      name: "Parallel Draft",
      description: null,
      primaryLegacyProcessTypeKey: "parallel_flow",
      createdAt: "2026-04-08T10:00:00Z",
      updatedAt: "2026-04-08T10:00:00Z",
      publishedAt: null,
      canPublish: false,
      validationIssues: [],
      nodes: [
        {
          nodeKey: "split",
          nodeType: "parallel_split",
          title: "Split",
          sortOrder: 1,
          positionX: null,
          positionY: null,
          config: null,
          actions: [],
        },
        {
          nodeKey: "join",
          nodeType: "parallel_join",
          title: "Join",
          sortOrder: 2,
          positionX: null,
          positionY: null,
          config: null,
          actions: [],
        },
      ],
      edges: [],
    });

    expect(draft.nodes[0]?.nodeType).toBe("parallel_split");
    expect(draft.nodes[1]?.nodeType).toBe("parallel_join");
  });

  it("requires explicit parallel split and join topology locally", () => {
    const issues = validateWorkflowBuilderDraft({
      name: "Parallel Draft",
      description: "",
      primaryLegacyProcessTypeKey: "",
      nodes: [
        {
          id: "start",
          nodeKey: "start",
          nodeType: "start",
          title: "Start",
          sortOrder: "1",
          positionX: 0,
          positionY: 0,
          configText: "",
          actions: [],
        },
        {
          id: "split",
          nodeKey: "split",
          nodeType: "parallel_split",
          title: "Split",
          sortOrder: "2",
          positionX: 0,
          positionY: 0,
          configText: "",
          actions: [],
        },
        {
          id: "join",
          nodeKey: "join",
          nodeType: "parallel_join",
          title: "Join",
          sortOrder: "3",
          positionX: 0,
          positionY: 0,
          configText: "",
          actions: [],
        },
        {
          id: "end",
          nodeKey: "end",
          nodeType: "end",
          title: "End",
          sortOrder: "4",
          positionX: 0,
          positionY: 0,
          configText: "",
          actions: [],
        },
      ],
      edges: [
        { id: "edge_1", sourceNodeKey: "start", targetNodeKey: "split", priority: "1", conditionExpression: "" },
        { id: "edge_2", sourceNodeKey: "split", targetNodeKey: "join", priority: "1", conditionExpression: "" },
        { id: "edge_3", sourceNodeKey: "join", targetNodeKey: "end", priority: "1", conditionExpression: "" },
      ],
    });

    expect(issues).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          scope: "node",
          message: "Der Parallel-Split 'split' braucht mindestens zwei ausgehende Pfade.",
        }),
        expect.objectContaining({
          scope: "node",
          message: "Der Parallel-Join 'join' braucht mindestens zwei eingehende Pfade.",
        }),
      ])
    );
  });
});
