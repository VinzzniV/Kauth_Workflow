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

  it("normalizes legacy setup nodes to the matching measure node type for migrated lifecycle processes", () => {
    const draft = toVersionDraft({
      id: 14,
      workflowDefinitionId: 1,
      definitionKey: "onboarding",
      definitionName: "Onboarding",
      definitionDescription: null,
      versionNumber: 1,
      status: "draft",
      name: "Business Phase Draft",
      description: null,
      primaryLegacyProcessTypeKey: "onboarding",
      createdAt: "2026-04-08T10:00:00Z",
      updatedAt: "2026-04-08T10:00:00Z",
      publishedAt: null,
      canPublish: false,
      validationIssues: [],
      nodes: [
        {
          nodeKey: "setup",
          nodeType: "setup",
          title: "IT/Fachbereichs-Setup",
          sortOrder: 1,
          positionX: null,
          positionY: null,
          config: null,
          actions: [],
        },
      ],
      edges: [],
    });

    expect(draft.nodes[0]?.nodeType).toBe("measure_provision");
    expect(draft.nodes[0]?.title).toBe("Bereitstellungsmaßnahmen erzeugen");
  });

  it("normalizes legacy setup nodes for name change to measure_rename", () => {
    const draft = toVersionDraft({
      id: 15,
      workflowDefinitionId: 1,
      definitionKey: "name_change",
      definitionName: "Namensaenderung",
      definitionDescription: null,
      versionNumber: 1,
      status: "draft",
      name: "Business Phase Draft",
      description: null,
      primaryLegacyProcessTypeKey: "name_change",
      createdAt: "2026-04-08T10:00:00Z",
      updatedAt: "2026-04-08T10:00:00Z",
      publishedAt: null,
      canPublish: false,
      validationIssues: [],
      nodes: [
        {
          nodeKey: "setup",
          nodeType: "setup",
          title: "Legacy Setup",
          sortOrder: 1,
          positionX: null,
          positionY: null,
          config: null,
          actions: [],
        },
      ],
      edges: [],
    });

    expect(draft.nodes[0]?.nodeType).toBe("measure_rename");
    expect(draft.nodes[0]?.title).toBe("Umbenennungsmaßnahmen erzeugen");
  });

  it("normalizes legacy setup nodes for position and role changes to measure_change", () => {
    for (const processTypeKey of ["position_change", "role_change"] as const) {
      const draft = toVersionDraft({
        id: processTypeKey === "position_change" ? 16 : 17,
        workflowDefinitionId: 1,
        definitionKey: processTypeKey,
        definitionName: processTypeKey,
        definitionDescription: null,
        versionNumber: 1,
        status: "draft",
        name: "Business Phase Draft",
        description: null,
        primaryLegacyProcessTypeKey: processTypeKey,
        createdAt: "2026-04-08T10:00:00Z",
        updatedAt: "2026-04-08T10:00:00Z",
        publishedAt: null,
        canPublish: false,
        validationIssues: [],
        nodes: [
          {
            nodeKey: "setup",
            nodeType: "setup",
            title: "Legacy Setup",
            sortOrder: 1,
            positionX: null,
            positionY: null,
            config: null,
            actions: [],
          },
        ],
        edges: [],
      });

      expect(draft.nodes[0]?.nodeType).toBe("measure_change");
      expect(draft.nodes[0]?.title).toBe("Änderungsmaßnahmen erzeugen");
    }
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
