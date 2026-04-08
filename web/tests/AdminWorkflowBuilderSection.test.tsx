import { fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AdminWorkflowBuilderSection } from "../src/components/admin-config/AdminWorkflowBuilderSection";
import * as adminConfigApi from "../src/services/adminConfigApi";
import { renderWithApp } from "./testUtils";

vi.mock("@xyflow/react", () => ({
  Background: () => null,
  Controls: () => null,
  MiniMap: () => null,
  Handle: () => null,
  Position: { Left: "left", Right: "right" },
  MarkerType: { ArrowClosed: "arrowclosed" },
  ReactFlow: ({ nodes = [], edges = [], nodeTypes, onNodeClick, onNodeDragStop, onEdgeClick, onConnect, onPaneClick, children }: {
    nodes?: Array<{ id: string; type?: string; data?: Record<string, unknown> & { title?: string } }>;
    edges?: Array<{ id: string }>;
    nodeTypes?: Record<string, (props: { id: string; data: Record<string, unknown> }) => JSX.Element>;
    onNodeClick?: (event: unknown, node: { id: string }) => void;
    onNodeDragStop?: (event: unknown, node: { id: string; position: { x: number; y: number } }) => void;
    onEdgeClick?: (event: unknown, edge: { id: string }) => void;
    onConnect?: (connection: { source: string | null; target: string | null }) => void;
    onPaneClick?: () => void;
    children?: any;
  }) => (
    <div>
      <div data-testid="mock-react-flow">nodes:{nodes.length} edges:{edges.length}</div>
      {nodes.map((node) => (
        <div key={node.id}>
          <button type="button" onClick={() => onNodeClick?.({}, { id: node.id })}>
            {node.data?.title ?? node.id}
          </button>
          {node.type && nodeTypes?.[node.type]
            ? nodeTypes[node.type]!({ id: node.id, data: node.data ?? {} })
            : null}
        </div>
      ))}
      {edges.map((edge) => (
        <button key={edge.id} type="button" onClick={() => onEdgeClick?.({}, { id: edge.id })}>
          Select edge {edge.id}
        </button>
      ))}
      <button
        type="button"
        onClick={() => {
          const sourceNode = nodes[0];
          const targetNode = nodes[1];
          if (sourceNode && targetNode) {
            onConnect?.({ source: sourceNode.id, target: targetNode.id });
          }
        }}
      >
        Mock connect first two nodes
      </button>
      <button
        type="button"
        onClick={() => {
          const firstNode = nodes[0];
          if (firstNode) {
            onNodeDragStop?.({}, { id: firstNode.id, position: { x: 640, y: 240 } });
          }
        }}
      >
        Mock drag first node
      </button>
      <button type="button" onClick={() => onPaneClick?.()}>
        Mock pane click
      </button>
      {children}
    </div>
  ),
}));

vi.mock("../src/services/adminConfigApi", () => ({
  createAdminWorkflowDefinition: vi.fn(),
  createAdminWorkflowDefinitionVersion: vi.fn(),
  getAdminWorkflowActionDefinitions: vi.fn(),
  getAdminWorkflowDefinitionVersion: vi.fn(),
  getAdminWorkflowDefinitions: vi.fn(),
  publishAdminWorkflowDefinitionVersion: vi.fn(),
  replaceAdminWorkflowDefinitionVersion: vi.fn(),
  updateAdminWorkflowDefinition: vi.fn(),
}));

const mockedCreateAdminWorkflowDefinition = vi.mocked(adminConfigApi.createAdminWorkflowDefinition);
const mockedCreateAdminWorkflowDefinitionVersion = vi.mocked(adminConfigApi.createAdminWorkflowDefinitionVersion);
const mockedGetAdminWorkflowActionDefinitions = vi.mocked(adminConfigApi.getAdminWorkflowActionDefinitions);
const mockedGetAdminWorkflowDefinitionVersion = vi.mocked(adminConfigApi.getAdminWorkflowDefinitionVersion);
const mockedGetAdminWorkflowDefinitions = vi.mocked(adminConfigApi.getAdminWorkflowDefinitions);
const mockedPublishAdminWorkflowDefinitionVersion = vi.mocked(adminConfigApi.publishAdminWorkflowDefinitionVersion);
const mockedReplaceAdminWorkflowDefinitionVersion = vi.mocked(adminConfigApi.replaceAdminWorkflowDefinitionVersion);
const mockedUpdateAdminWorkflowDefinition = vi.mocked(adminConfigApi.updateAdminWorkflowDefinition);

function createDefinition(canPublish = true) {
  return {
    id: 1,
    key: "hr-onboarding",
    name: "HR Onboarding",
    description: "Definition",
    versions: [
      {
        id: 11,
        workflowDefinitionId: 1,
        versionNumber: 1,
        status: "draft",
        name: "Draft 1",
        description: "Desc",
        primaryLegacyProcessTypeKey: "onboarding",
        createdAt: "2026-04-08T10:00:00Z",
        updatedAt: "2026-04-08T10:00:00Z",
        publishedAt: null,
        canPublish,
        validationIssues: canPublish ? [] : [{ code: "x", severity: "error", scope: "workflow", message: "bad", referenceKey: null }],
      },
    ],
  };
}

function createVersionDetail(canPublish = true) {
  return {
    id: 11,
    workflowDefinitionId: 1,
    definitionKey: "hr-onboarding",
    definitionName: "HR Onboarding",
    definitionDescription: "Definition",
    versionNumber: 1,
    status: "draft",
    name: "Draft 1",
    description: "Desc",
    primaryLegacyProcessTypeKey: "onboarding",
    createdAt: "2026-04-08T10:00:00Z",
    updatedAt: "2026-04-08T10:00:00Z",
    publishedAt: null,
    canPublish,
    validationIssues: canPublish ? [] : [{ code: "x", severity: "error", scope: "workflow", message: "bad", referenceKey: null }],
    nodes: [
      { nodeKey: "start", nodeType: "start", title: "Start", sortOrder: 1, positionX: 80, positionY: 60, config: null, actions: [] },
      { nodeKey: "end", nodeType: "end", title: "Ende", sortOrder: 2, positionX: 420, positionY: 60, config: null, actions: [] },
    ],
    edges: [
      { sourceNodeKey: "start", targetNodeKey: "end", priority: 1, conditionExpression: null },
    ],
  };
}

function createRichVersionDetail() {
  return {
    ...createVersionDetail(),
    nodes: [
      { nodeKey: "start", nodeType: "start", title: "Start", sortOrder: 1, positionX: 80, positionY: 60, config: null, actions: [] },
      {
        nodeKey: "request_form",
        nodeType: "form",
        title: "Mitarbeiterdaten",
        sortOrder: 2,
        positionX: 360,
        positionY: 60,
        config: { legacyProcessTypeKey: "onboarding" },
        actions: [],
      },
      {
        nodeKey: "manager_approval",
        nodeType: "approval",
        title: "Manager Freigabe",
        sortOrder: 3,
        positionX: 640,
        positionY: 60,
        config: { legacyTemplateKey: "manager_check" },
        actions: [],
      },
      {
        nodeKey: "branch_decision",
        nodeType: "decision",
        title: "Weiterer Pfad",
        sortOrder: 4,
        positionX: 920,
        positionY: 60,
        config: null,
        actions: [],
      },
      {
        nodeKey: "create_accounts",
        nodeType: "automation",
        title: "Konten erstellen",
        sortOrder: 5,
        positionX: 1200,
        positionY: 20,
        config: null,
        actions: [
          {
            actionKey: "CreateAdUser",
            inputMapping: { userPrincipalName: "$.person.email" },
            executionOrder: 1,
            onErrorBehavior: "fail_workflow",
          },
          {
            actionKey: "CreateMailbox",
            inputMapping: { mailNickname: "$.person.alias" },
            executionOrder: 2,
            onErrorBehavior: "fail_workflow",
          },
        ],
      },
      {
        nodeKey: "equipment_task",
        nodeType: "task",
        title: "Equipment vorbereiten",
        sortOrder: 6,
        positionX: 1200,
        positionY: 180,
        config: { legacyTemplateKey: "collect_equipment" },
        actions: [],
      },
      { nodeKey: "end", nodeType: "end", title: "Abschluss", sortOrder: 7, positionX: 1480, positionY: 100, config: null, actions: [] },
    ],
    edges: [
      { sourceNodeKey: "start", targetNodeKey: "request_form", priority: 1, conditionExpression: null },
      { sourceNodeKey: "request_form", targetNodeKey: "manager_approval", priority: 1, conditionExpression: null },
      { sourceNodeKey: "manager_approval", targetNodeKey: "branch_decision", priority: 1, conditionExpression: null },
      { sourceNodeKey: "branch_decision", targetNodeKey: "create_accounts", priority: 1, conditionExpression: "{\"answerKey\":\"approved\",\"operator\":\"is_true\"}" },
      { sourceNodeKey: "branch_decision", targetNodeKey: "equipment_task", priority: 2, conditionExpression: "{\"answerKey\":\"approved\",\"operator\":\"is_false\"}" },
      { sourceNodeKey: "create_accounts", targetNodeKey: "end", priority: 1, conditionExpression: null },
      { sourceNodeKey: "equipment_task", targetNodeKey: "end", priority: 1, conditionExpression: null },
    ],
  };
}

describe("AdminWorkflowBuilderSection", () => {
  beforeEach(() => {
    mockedCreateAdminWorkflowDefinition.mockReset();
    mockedCreateAdminWorkflowDefinitionVersion.mockReset();
    mockedGetAdminWorkflowActionDefinitions.mockReset();
    mockedGetAdminWorkflowDefinitionVersion.mockReset();
    mockedGetAdminWorkflowDefinitions.mockReset();
    mockedPublishAdminWorkflowDefinitionVersion.mockReset();
    mockedReplaceAdminWorkflowDefinitionVersion.mockReset();
    mockedUpdateAdminWorkflowDefinition.mockReset();

    mockedGetAdminWorkflowDefinitions.mockResolvedValue([createDefinition()]);
    mockedGetAdminWorkflowActionDefinitions.mockResolvedValue([
      {
        id: 1,
        actionKey: "CreateAdUser",
        displayName: "Create AD User",
        description: "Legt ein AD-Konto an.",
        handlerKey: "create-ad-user",
        isIdempotent: true,
        isActive: true,
        requiresApproval: false,
        inputSchema: null,
      },
      {
        id: 2,
        actionKey: "CreateMailbox",
        displayName: "Create Mailbox",
        description: "Legt eine Mailbox an.",
        handlerKey: "create-mailbox",
        isIdempotent: true,
        isActive: true,
        requiresApproval: true,
        inputSchema: null,
      },
      {
        id: 3,
        actionKey: "AssignGroups",
        displayName: "Assign Groups",
        description: "Weist Gruppen kontrolliert zu.",
        handlerKey: "assign-groups",
        isIdempotent: true,
        isActive: false,
        requiresApproval: false,
        inputSchema: null,
      },
    ]);
    mockedGetAdminWorkflowDefinitionVersion.mockResolvedValue(createVersionDetail());
    mockedCreateAdminWorkflowDefinition.mockResolvedValue(createDefinition());
    mockedCreateAdminWorkflowDefinitionVersion.mockResolvedValue(createDefinition().versions[0]!);
    mockedReplaceAdminWorkflowDefinitionVersion.mockResolvedValue(createVersionDetail());
    mockedPublishAdminWorkflowDefinitionVersion.mockResolvedValue(createVersionDetail());
    mockedUpdateAdminWorkflowDefinition.mockResolvedValue(createDefinition());
  });

  it("loads definitions and renders the selected draft", async () => {
    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    expect(await screen.findByDisplayValue("HR Onboarding")).toBeTruthy();
    expect(screen.getAllByText("hr-onboarding").length).toBeGreaterThan(0);
    expect(screen.getAllByText(/V1 - draft/i).length).toBeGreaterThan(0);
    expect(screen.getByTestId("mock-react-flow").textContent).toContain("nodes:2 edges:1");
    expect(screen.getByText(/Noch kein Node ausgewaehlt/i)).toBeTruthy();
  });

  it("creates a new workflow definition", async () => {
    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    fireEvent.change(await screen.findByLabelText("Key"), { target: { value: "offboarding" } });
    fireEvent.change(screen.getAllByLabelText("Name")[0]!, { target: { value: "Offboarding" } });
    fireEvent.click(screen.getByRole("button", { name: "Definition anlegen" }));

    await waitFor(() => {
      expect(mockedCreateAdminWorkflowDefinition).toHaveBeenCalledWith({
        key: "offboarding",
        name: "Offboarding",
        description: null,
      });
    });
  });

  it("shows a visible action catalog for automation nodes", async () => {
    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    await waitFor(() => {
      expect(screen.getByTestId("mock-react-flow").textContent).toContain("nodes:2 edges:1");
    });
    fireEvent.click(screen.getByRole("button", { name: "Node hinzufuegen" }));
    expect(await screen.findByLabelText("Node Type")).toBeTruthy();
    fireEvent.change(screen.getByLabelText("Node Type"), { target: { value: "automation" } });

    expect(await screen.findByRole("heading", { name: "Verfuegbarer Action-Katalog" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Create AD User hinzufuegen" })).toBeTruthy();
    expect(screen.getByText("approval required")).toBeTruthy();
    expect((screen.getByRole("button", { name: "Assign Groups hinzufuegen" }) as HTMLButtonElement).disabled).toBe(true);
  });

  it("keeps the builder usable when the action catalog cannot be loaded", async () => {
    const onError = vi.fn();
    mockedGetAdminWorkflowActionDefinitions.mockRejectedValueOnce(new Error("Action-Katalog nicht verfuegbar."));

    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={onError} />,
      { roleKeys: ["auth_admin"] }
    );

    expect(await screen.findByText("HR Onboarding")).toBeTruthy();
    await waitFor(() => {
      expect(onError).toHaveBeenCalledWith(expect.stringContaining("Action-Katalog"));
    });
  });

  it("renders a visible builder toolbar with grouped builder actions", async () => {
    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    expect(await screen.findByRole("heading", { name: "Builder-Aktionen" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Node hinzufuegen" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Node loeschen" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Validieren" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Publish" })).toBeTruthy();
    expect(screen.getByText(/Node loeschen wird aktiv/i)).toBeTruthy();
  });

  it("validates locally without saving when the draft contains JSON errors", async () => {
    const onError = vi.fn();
    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={onError} />,
      { roleKeys: ["auth_admin"] }
    );

    await waitFor(() => {
      expect(screen.getByTestId("mock-react-flow").textContent).toContain("nodes:2 edges:1");
    });
    fireEvent.click(screen.getByRole("button", { name: "Node hinzufuegen" }));
    await screen.findByLabelText("Titel");
    fireEvent.click(screen.getByRole("button", { name: "Erweitert anzeigen" }));
    fireEvent.change(screen.getByLabelText("Node Key"), { target: { value: "task_a" } });
    fireEvent.change(screen.getByLabelText("Node Type"), { target: { value: "task" } });
    fireEvent.change(screen.getByLabelText("Config JSON"), { target: { value: "{invalid" } });
    fireEvent.click(screen.getByRole("button", { name: "Validieren" }));

    await waitFor(() => {
      expect(mockedReplaceAdminWorkflowDefinitionVersion).not.toHaveBeenCalled();
      expect(mockedPublishAdminWorkflowDefinitionVersion).not.toHaveBeenCalled();
      expect(onError).toHaveBeenCalledWith("Der Draft enthaelt lokale Validierungsfehler.");
    });
  });

  it("deletes the selected node from the toolbar", async () => {
    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    const deleteButton = await screen.findByRole("button", { name: "Node loeschen" });
    expect((deleteButton as HTMLButtonElement).disabled).toBe(true);

    fireEvent.click((await screen.findAllByRole("button", { name: "Start" }))[0]!);
    await waitFor(() => {
      expect((screen.getByRole("button", { name: "Node loeschen" }) as HTMLButtonElement).disabled).toBe(false);
    });

    fireEvent.click(screen.getByRole("button", { name: "Node loeschen" }));

    await waitFor(() => {
      expect(screen.getByTestId("mock-react-flow").textContent).toContain("nodes:1 edges:0");
    });
    expect(screen.getByText(/Noch kein Node ausgewaehlt/i)).toBeTruthy();
  });

  it("adds a controlled action from the catalog to an automation node", async () => {
    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    await waitFor(() => {
      expect(screen.getByTestId("mock-react-flow").textContent).toContain("nodes:2 edges:1");
    });

    fireEvent.click(screen.getByRole("button", { name: "Node hinzufuegen" }));
    fireEvent.change(await screen.findByLabelText("Node Type"), { target: { value: "automation" } });
    fireEvent.click(screen.getByRole("button", { name: "Create AD User hinzufuegen" }));

    expect(await screen.findByRole("heading", { name: "Konfigurierte Actions" })).toBeTruthy();
    expect(screen.getByDisplayValue("Create AD User")).toBeTruthy();
    expect((screen.getByLabelText("Execution Order") as HTMLInputElement).value).toBe("1");
    fireEvent.click(screen.getByRole("button", { name: "Erweitert anzeigen" }));
    fireEvent.change(screen.getByLabelText("Node Key"), { target: { value: "automation_step" } });

    fireEvent.click(screen.getByRole("button", { name: "Draft speichern" }));

    await waitFor(() => {
      expect(mockedReplaceAdminWorkflowDefinitionVersion).toHaveBeenCalledWith(
        11,
        expect.objectContaining({
          nodes: expect.arrayContaining([
            expect.objectContaining({
              nodeType: "automation",
              actions: expect.arrayContaining([
                expect.objectContaining({
                  actionKey: "CreateAdUser",
                  executionOrder: 1,
                }),
              ]),
            }),
          ]),
        })
      );
    });
  });

  it("publishes only publishable versions", async () => {
    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    const publishButton = await screen.findByRole("button", { name: "Publish" });
    await waitFor(() => {
      expect((publishButton as HTMLButtonElement).disabled).toBe(false);
    });

    fireEvent.click(publishButton);

    await waitFor(() => {
      expect(mockedPublishAdminWorkflowDefinitionVersion).toHaveBeenCalledWith(11);
    });
  });

  it("runs explicit local validation successfully without saving", async () => {
    const onNotice = vi.fn();
    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={onNotice} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    await waitFor(() => {
      expect(screen.getByTestId("mock-react-flow").textContent).toContain("nodes:2 edges:1");
    });
    fireEvent.click(await screen.findByRole("button", { name: "Validieren" }));

    await waitFor(() => {
      expect(mockedReplaceAdminWorkflowDefinitionVersion).not.toHaveBeenCalled();
      expect(mockedPublishAdminWorkflowDefinitionVersion).not.toHaveBeenCalled();
      expect(onNotice).toHaveBeenCalledWith("Lokale Builder-Validierung erfolgreich.");
    });
    expect(screen.queryByText("Lokale Validierung")).toBeNull();
  });

  it("updates node positions when a canvas drag finishes", async () => {
    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    await waitFor(() => {
      expect(screen.getByTestId("mock-react-flow").textContent).toContain("nodes:2 edges:1");
    });
    fireEvent.click(screen.getByRole("button", { name: "Mock drag first node" }));
    fireEvent.click((await screen.findAllByRole("button", { name: "Start" }))[0]!);
    fireEvent.click(await screen.findByRole("button", { name: "Erweitert anzeigen" }));
    expect((await screen.findByLabelText("Position X") as HTMLInputElement).value).toBe("640");
    expect((screen.getByLabelText("Position Y") as HTMLInputElement).value).toBe("240");

    fireEvent.click(screen.getByRole("button", { name: "Draft speichern" }));

    await waitFor(() => {
      expect(mockedReplaceAdminWorkflowDefinitionVersion).toHaveBeenCalledWith(
        11,
        expect.objectContaining({
          nodes: expect.arrayContaining([
            expect.objectContaining({
              nodeKey: "start",
              positionX: 640,
              positionY: 240,
            }),
          ]),
        })
      );
    });
  });

  it("creates a new edge via canvas connect and persists it on save", async () => {
    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    await waitFor(() => {
      expect(screen.getByTestId("mock-react-flow").textContent).toContain("edges:1");
    });
    fireEvent.click(screen.getByRole("button", { name: "Mock connect first two nodes" }));
    expect(screen.getByTestId("mock-react-flow").textContent).toContain("edges:2");

    fireEvent.click(screen.getByRole("button", { name: "Draft speichern" }));

    await waitFor(() => {
      expect(mockedReplaceAdminWorkflowDefinitionVersion).toHaveBeenCalledWith(
        11,
        expect.objectContaining({
          edges: expect.arrayContaining([
            expect.objectContaining({
              sourceNodeKey: "start",
              targetNodeKey: "end",
              priority: 2,
            }),
          ]),
        })
      );
    });
  });

  it("shows the node sidebar only after explicit node selection", async () => {
    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    expect(await screen.findByText(/Noch kein Node ausgewaehlt/i)).toBeTruthy();
    fireEvent.click((await screen.findAllByRole("button", { name: "Start" }))[0]!);
    expect(await screen.findByText("Startet den Workflow und fuehrt in die ersten verbundenen Schritte.")).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Basis" })).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Weiterleitung" })).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Action Layer" })).toBeTruthy();
  });

  it("closes the node sidebar again when the canvas pane is clicked", async () => {
    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    fireEvent.click((await screen.findAllByRole("button", { name: "Start" }))[0]!);
    expect(await screen.findByText("Startet den Workflow und fuehrt in die ersten verbundenen Schritte.")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Mock pane click" }));
    expect(await screen.findByText(/Noch kein Node ausgewaehlt/i)).toBeTruthy();
  });

  it("edits outgoing edge data for the selected node in the sidebar", async () => {
    mockedGetAdminWorkflowDefinitionVersion.mockResolvedValueOnce(createRichVersionDetail());

    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    fireEvent.click((await screen.findAllByRole("button", { name: "Weiterer Pfad" }))[0]!);
    expect(await screen.findByText(/Steuert die Weiterleitung ueber Bedingungen/i)).toBeTruthy();
    expect(screen.getAllByLabelText("Target Node").length).toBeGreaterThan(0);

    fireEvent.change(screen.getAllByLabelText("Condition Expression")[0]!, {
      target: { value: "{\"answerKey\":\"approved\",\"operator\":\"is_true\"}" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Draft speichern" }));

    await waitFor(() => {
      expect(mockedReplaceAdminWorkflowDefinitionVersion).toHaveBeenCalledWith(
        11,
        expect.objectContaining({
          edges: expect.arrayContaining([
            expect.objectContaining({
              sourceNodeKey: "branch_decision",
              targetNodeKey: "create_accounts",
              conditionExpression: "{\"answerKey\":\"approved\",\"operator\":\"is_true\"}",
            }),
          ]),
        })
      );
    });
  });

  it("shows automation actions inside the node sidebar", async () => {
    mockedGetAdminWorkflowDefinitionVersion.mockResolvedValueOnce(createRichVersionDetail());

    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    fireEvent.click((await screen.findAllByRole("button", { name: "Konten erstellen" }))[0]!);
    expect(await screen.findByRole("heading", { name: "Verfuegbarer Action-Katalog" })).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Konfigurierte Actions" })).toBeTruthy();
    expect(screen.getAllByLabelText("Action").length).toBeGreaterThan(0);
    expect(screen.getAllByLabelText("Input Mapping (JSON)").length).toBeGreaterThan(0);
  });

  it("keeps the action layer visible but read-only for non-automation nodes", async () => {
    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    fireEvent.click((await screen.findAllByRole("button", { name: "Start" }))[0]!);
    expect(await screen.findByRole("heading", { name: "Action Layer" })).toBeTruthy();
    expect(screen.getByText(/Technische Actions werden auf Automation-Nodes ausgefuehrt/i)).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Create AD User hinzufuegen" })).toBeNull();
  });

  it("keeps technical fields inside the advanced sidebar section", async () => {
    mockedGetAdminWorkflowDefinitionVersion.mockResolvedValueOnce(createRichVersionDetail());

    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    fireEvent.click((await screen.findAllByRole("button", { name: "Equipment vorbereiten" }))[0]!);
    expect(screen.queryByLabelText("Node Key")).toBeNull();
    expect(screen.queryByLabelText("Sort Order")).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Erweitert anzeigen" }));
    expect(await screen.findByLabelText("Node Key")).toBeTruthy();
    expect(screen.getByLabelText("Sort Order")).toBeTruthy();
  });

  it("renders semantic builder cards per node type instead of technical dataset labels", async () => {
    mockedGetAdminWorkflowDefinitionVersion.mockResolvedValueOnce(createRichVersionDetail());

    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    expect(await screen.findByText("Formular")).toBeTruthy();
    expect(screen.getByText("Formular fuer Prozess: onboarding")).toBeTruthy();
    expect(screen.getByText("Freigabe ueber Vorlage: manager_check")).toBeTruthy();
    expect(screen.getByText("Lenkt den Workflow ueber Bedingungen und Pfade.")).toBeTruthy();
    expect(screen.getByText("2 Bedingung(en) bzw. Pfade sind verbunden.")).toBeTruthy();
    expect(screen.getByText("Startet Create AD User + 1 weitere Action(s).")).toBeTruthy();
    expect(screen.getByText("Arbeitsvorlage: collect_equipment")).toBeTruthy();
    expect(screen.queryByText("branch_decision")).toBeNull();
    expect(screen.queryByText("sort_order")).toBeNull();
  });

  it("shows the builder in limited mode without loading the action catalog", async () => {
    mockedGetAdminWorkflowDefinitionVersion.mockResolvedValueOnce(createRichVersionDetail());

    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_manager"] }
    );

    await waitFor(() => {
      expect(screen.getByTestId("mock-react-flow").textContent).toContain("nodes:7 edges:7");
    });

    expect(mockedGetAdminWorkflowActionDefinitions).not.toHaveBeenCalled();
    expect(screen.queryByRole("button", { name: "Definition anlegen" })).toBeNull();
    expect((screen.getByRole("button", { name: "Publish" }) as HTMLButtonElement).disabled).toBe(true);

    fireEvent.click((await screen.findAllByRole("button", { name: "Konten erstellen" }))[0]!);
    expect(await screen.findByText(/nur im Admin Builder editierbar/i)).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Create AD User hinzufuegen" })).toBeNull();
  });
});
