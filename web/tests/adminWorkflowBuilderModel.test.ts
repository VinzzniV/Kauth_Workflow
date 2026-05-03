import { describe, expect, it } from "vitest";
import {
  buildVersionReplacePayload,
  toVersionDraft,
  topologicallyOrderNodes,
  validateWorkflowBuilderDraft,
} from "../src/hooks/adminWorkflowBuilderModel";

describe("adminWorkflowBuilderModel", () => {
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

  it("orders nodes topologically based on edges", () => {
    const ordered = topologicallyOrderNodes(
      [
        { id: "n_end", nodeKey: "end", nodeType: "end", title: "End", sortOrder: "3", positionX: null, positionY: null, configText: "", actions: [] },
        { id: "n_task", nodeKey: "task", nodeType: "task", title: "Task", sortOrder: "2", positionX: null, positionY: null, configText: "", actions: [] },
        { id: "n_start", nodeKey: "start", nodeType: "start", title: "Start", sortOrder: "1", positionX: null, positionY: null, configText: "", actions: [] },
      ],
      [
        { id: "e1", sourceNodeKey: "start", targetNodeKey: "task", priority: "1", conditionExpression: "" },
        { id: "e2", sourceNodeKey: "task", targetNodeKey: "end", priority: "1", conditionExpression: "" },
      ]
    );

    expect(ordered.map((n) => n.nodeKey)).toEqual(["start", "task", "end"]);
  });

  it("handles cycles by appending unsorted nodes at end", () => {
    const ordered = topologicallyOrderNodes(
      [
        { id: "n_a", nodeKey: "a", nodeType: "task", title: "A", sortOrder: "1", positionX: null, positionY: null, configText: "", actions: [] },
        { id: "n_b", nodeKey: "b", nodeType: "task", title: "B", sortOrder: "2", positionX: null, positionY: null, configText: "", actions: [] },
      ],
      [
        { id: "e1", sourceNodeKey: "a", targetNodeKey: "b", priority: "1", conditionExpression: "" },
        { id: "e2", sourceNodeKey: "b", targetNodeKey: "a", priority: "1", conditionExpression: "" },
      ]
    );
    // both in cycle → both appended in array order
    expect(ordered.map((n) => n.nodeKey)).toEqual(["a", "b"]);
  });

  it("ignores edges with self-loops", () => {
    const ordered = topologicallyOrderNodes(
      [
        { id: "n_start", nodeKey: "start", nodeType: "start", title: "S", sortOrder: "1", positionX: null, positionY: null, configText: "", actions: [] },
        { id: "n_a", nodeKey: "a", nodeType: "task", title: "A", sortOrder: "2", positionX: null, positionY: null, configText: "", actions: [] },
      ],
      [
        { id: "e1", sourceNodeKey: "start", targetNodeKey: "a", priority: "1", conditionExpression: "" },
        { id: "e2", sourceNodeKey: "a", targetNodeKey: "a", priority: "2", conditionExpression: "" },
      ]
    );
    expect(ordered.map((n) => n.nodeKey)).toEqual(["start", "a"]);
  });

  it("ignores edges with unknown source/target node keys", () => {
    const ordered = topologicallyOrderNodes(
      [
        { id: "n_start", nodeKey: "start", nodeType: "start", title: "S", sortOrder: "1", positionX: null, positionY: null, configText: "", actions: [] },
        { id: "n_end", nodeKey: "end", nodeType: "end", title: "E", sortOrder: "2", positionX: null, positionY: null, configText: "", actions: [] },
      ],
      [
        { id: "e1", sourceNodeKey: "ghost", targetNodeKey: "end", priority: "1", conditionExpression: "" },
        { id: "e2", sourceNodeKey: "start", targetNodeKey: "end", priority: "1", conditionExpression: "" },
      ]
    );
    expect(ordered.map((n) => n.nodeKey)).toEqual(["start", "end"]);
  });

  it("handles nodes without nodeKey by appending in array order", () => {
    const ordered = topologicallyOrderNodes(
      [
        { id: "n_start", nodeKey: "start", nodeType: "start", title: "S", sortOrder: "1", positionX: null, positionY: null, configText: "", actions: [] },
        { id: "n_blank", nodeKey: "", nodeType: "task", title: "X", sortOrder: "2", positionX: null, positionY: null, configText: "", actions: [] },
      ],
      []
    );
    expect(ordered.map((n) => n.id)).toEqual(["n_start", "n_blank"]);
  });

  it("uses array index as tie-breaker for parallel paths", () => {
    const ordered = topologicallyOrderNodes(
      [
        { id: "n_start", nodeKey: "start", nodeType: "start", title: "S", sortOrder: "1", positionX: null, positionY: null, configText: "", actions: [] },
        { id: "n_a", nodeKey: "a", nodeType: "task", title: "A", sortOrder: "2", positionX: null, positionY: null, configText: "", actions: [] },
        { id: "n_b", nodeKey: "b", nodeType: "task", title: "B", sortOrder: "3", positionX: null, positionY: null, configText: "", actions: [] },
        { id: "n_end", nodeKey: "end", nodeType: "end", title: "E", sortOrder: "4", positionX: null, positionY: null, configText: "", actions: [] },
      ],
      [
        { id: "e1", sourceNodeKey: "start", targetNodeKey: "a", priority: "1", conditionExpression: "" },
        { id: "e2", sourceNodeKey: "start", targetNodeKey: "b", priority: "2", conditionExpression: "" },
        { id: "e3", sourceNodeKey: "a", targetNodeKey: "end", priority: "1", conditionExpression: "" },
        { id: "e4", sourceNodeKey: "b", targetNodeKey: "end", priority: "1", conditionExpression: "" },
      ]
    );

    // start, then a (lower array index), then b, then end
    expect(ordered.map((n) => n.nodeKey)).toEqual(["start", "a", "b", "end"]);
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
          message: "Die Verbindung 'start -> start' darf kein Rücksprung auf denselben Schritt sein.",
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

  // LA2 (2026-05-03) hat den `setup`-Node-Type komplett aus dem Code entfernt — Auto-Migration
  // `setup` → `measure_*` nach definitionKey existiert nicht mehr. normalizeNodeType faellt
  // unbekannte Typen auf 'task' zurueck. Die alten Tests fuer das Auto-Mapping sind obsolet.

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

  it("requires a strict business phase path when a measure block is used", () => {
    const issues = validateWorkflowBuilderDraft({
      name: "Onboarding",
      description: "",
      primaryLegacyProcessTypeKey: "onboarding",
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
          id: "form",
          nodeKey: "collect_requirements",
          nodeType: "form",
          title: "Requirements",
          sortOrder: "2",
          positionX: 0,
          positionY: 0,
          configText: "{\"legacyProcessTypeKey\":\"onboarding\"}",
          actions: [],
        },
        {
          id: "task",
          nodeKey: "ad_task",
          nodeType: "task",
          title: "AD",
          sortOrder: "3",
          positionX: 0,
          positionY: 0,
          configText: "{\"legacyTemplateKey\":\"collect_equipment\"}",
          actions: [],
        },
        {
          id: "setup",
          nodeKey: "department_setup",
          nodeType: "measure_provision",
          title: "Bereitstellungsmaßnahmen erzeugen",
          sortOrder: "4",
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
          sortOrder: "5",
          positionX: 0,
          positionY: 0,
          configText: "",
          actions: [],
        },
      ],
      edges: [
        { id: "edge_1", sourceNodeKey: "start", targetNodeKey: "collect_requirements", priority: "1", conditionExpression: "" },
        { id: "edge_2", sourceNodeKey: "collect_requirements", targetNodeKey: "department_setup", priority: "1", conditionExpression: "" },
        { id: "edge_3", sourceNodeKey: "department_setup", targetNodeKey: "end", priority: "1", conditionExpression: "" },
        { id: "edge_4", sourceNodeKey: "collect_requirements", targetNodeKey: "ad_task", priority: "2", conditionExpression: "" },
      ],
    });

    expect(issues).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          scope: "node",
          message: "Ein Ablauf mit Maßnahmen-Baustein darf keinen technischen Hauptschritt vom Typ 'task' enthalten.",
        }),
        expect.objectContaining({
          scope: "version",
          message: "Ein fachlicher Ablauf mit Maßnahmen-Baustein darf nur Start, Formular, optionale Freigabe, Maßnahmen und Abschluss im Hauptfluss enthalten.",
        }),
      ])
    );
  });

  it("rejects semantically wrong measure types for phase-c lifecycle processes locally", () => {
    const issues = validateWorkflowBuilderDraft({
      name: "Role Change",
      description: "",
      primaryLegacyProcessTypeKey: "role_change",
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
          id: "form",
          nodeKey: "collect_requirements",
          nodeType: "form",
          title: "Requirements",
          sortOrder: "2",
          positionX: 0,
          positionY: 0,
          configText: "{\"legacyProcessTypeKey\":\"role_change\"}",
          actions: [],
        },
        {
          id: "measure",
          nodeKey: "department_setup",
          nodeType: "measure_rename",
          title: "Umbenennungsmaßnahmen erzeugen",
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
        { id: "edge_1", sourceNodeKey: "start", targetNodeKey: "collect_requirements", priority: "1", conditionExpression: "" },
        { id: "edge_2", sourceNodeKey: "collect_requirements", targetNodeKey: "department_setup", priority: "1", conditionExpression: "" },
        { id: "edge_3", sourceNodeKey: "department_setup", targetNodeKey: "end", priority: "1", conditionExpression: "" },
      ],
    });

    expect(issues).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          scope: "node",
          message: "Der Prozess 'role_change' benötigt den Baustein 'Änderungsmaßnahmen erzeugen'.",
        }),
      ])
    );
  });
});
