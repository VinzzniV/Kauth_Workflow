import { fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AdminWorkflowBuilderSection } from "../src/components/admin-config/AdminWorkflowBuilderSection";
import * as adminConfigApi from "../src/services/adminConfigApi";
import * as adminApi from "../src/services/adminApi";
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
  getAdminProcessTypes: vi.fn(),
  getAdminTaskTemplates: vi.fn(),
  getAdminWorkflowDefinitionVersion: vi.fn(),
  getAdminWorkflowDefinitions: vi.fn(),
  publishAdminWorkflowDefinitionVersion: vi.fn(),
  replaceAdminWorkflowDefinitionVersion: vi.fn(),
  updateAdminWorkflowDefinition: vi.fn(),
}));

vi.mock("../src/services/adminApi", () => ({
  getAdminResponsibilityOwners: vi.fn(),
}));

const mockedCreateAdminWorkflowDefinition = vi.mocked(adminConfigApi.createAdminWorkflowDefinition);
const mockedCreateAdminWorkflowDefinitionVersion = vi.mocked(adminConfigApi.createAdminWorkflowDefinitionVersion);
const mockedGetAdminWorkflowActionDefinitions = vi.mocked(adminConfigApi.getAdminWorkflowActionDefinitions);
const mockedGetAdminProcessTypes = vi.mocked(adminConfigApi.getAdminProcessTypes);
const mockedGetAdminTaskTemplates = vi.mocked(adminConfigApi.getAdminTaskTemplates);
const mockedGetAdminWorkflowDefinitionVersion = vi.mocked(adminConfigApi.getAdminWorkflowDefinitionVersion);
const mockedGetAdminWorkflowDefinitions = vi.mocked(adminConfigApi.getAdminWorkflowDefinitions);
const mockedPublishAdminWorkflowDefinitionVersion = vi.mocked(adminConfigApi.publishAdminWorkflowDefinitionVersion);
const mockedReplaceAdminWorkflowDefinitionVersion = vi.mocked(adminConfigApi.replaceAdminWorkflowDefinitionVersion);
const mockedUpdateAdminWorkflowDefinition = vi.mocked(adminConfigApi.updateAdminWorkflowDefinition);
const mockedGetAdminResponsibilityOwners = vi.mocked(adminApi.getAdminResponsibilityOwners);

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
    mockedGetAdminProcessTypes.mockReset();
    mockedGetAdminTaskTemplates.mockReset();
    mockedGetAdminWorkflowDefinitionVersion.mockReset();
    mockedGetAdminWorkflowDefinitions.mockReset();
    mockedPublishAdminWorkflowDefinitionVersion.mockReset();
    mockedReplaceAdminWorkflowDefinitionVersion.mockReset();
    mockedUpdateAdminWorkflowDefinition.mockReset();
    mockedGetAdminResponsibilityOwners.mockReset();

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
    mockedGetAdminProcessTypes.mockResolvedValue([
      {
        id: 7,
        key: "onboarding",
        name: "Onboarding",
        description: "Fuehrt die Angaben fuer den Eintritt zusammen.",
        requiresSupervisorStep: false,
        approvalTaskTemplateKey: null,
        requiresTargetPerson: false,
        iconKey: null,
        isActive: true,
        sortOrder: 1,
        workflowCount: 0,
        answerDefinitionCount: 0,
        taskTemplateCount: 2,
        canActivate: true,
        activationBlockedReason: null,
      },
    ]);
    mockedGetAdminTaskTemplates.mockResolvedValue([
      {
        id: 101,
        processTypeId: 7,
        templateKey: "manager_check",
        title: "Manager Freigabe",
        category: "approval",
        description: "Die vorgesetzte Rolle prueft und bestaetigt den Eintritt.",
        iconKey: null,
        owningDepartmentId: null,
        defaultResponsibilityId: 55,
        processAreaLabel: null,
        isDepartmentPhaseTask: false,
        isRequired: true,
        dueInDays: 2,
        sortOrder: 1,
        isActive: true,
        createdAt: "2026-04-08T10:00:00Z",
        conditionCount: 0,
        dependencyCount: 0,
      },
      {
        id: 102,
        processTypeId: 7,
        templateKey: "collect_equipment",
        title: "Equipment vorbereiten",
        category: "task",
        description: "Das Arbeitsplatz-Equipment wird fuer den Start vorbereitet.",
        iconKey: null,
        owningDepartmentId: null,
        defaultResponsibilityId: 56,
        processAreaLabel: null,
        isDepartmentPhaseTask: false,
        isRequired: true,
        dueInDays: 5,
        sortOrder: 2,
        isActive: true,
        createdAt: "2026-04-08T10:00:00Z",
        conditionCount: 0,
        dependencyCount: 0,
      },
    ]);
    mockedGetAdminResponsibilityOwners.mockResolvedValue([
      {
        responsibilityId: 55,
        responsibilityKey: "manager",
        systemKey: null,
        responsibilityName: "Fuehrungskraft",
        responsibilityType: "process",
        departmentId: null,
        departmentName: null,
        appUserId: null,
        appUserDisplayName: null,
        updatedAt: null,
      },
      {
        responsibilityId: 56,
        responsibilityKey: "it_service",
        systemKey: null,
        responsibilityName: "IT-Service",
        responsibilityType: "application",
        departmentId: null,
        departmentName: null,
        appUserId: null,
        appUserDisplayName: null,
        updatedAt: null,
      },
    ]);
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

    expect(await screen.findByRole("heading", { name: "Ablaufe" })).toBeTruthy();
    expect(screen.getAllByText("HR Onboarding").length).toBeGreaterThan(0);
    expect(screen.getAllByText(/Entwurf 1/i).length).toBeGreaterThan(0);
    await waitFor(() => {
      expect(screen.getByTestId("mock-react-flow").textContent).toContain("nodes:2 edges:1");
    });
    expect(screen.getByRole("heading", { name: "Schritte fuer den Ablauf" })).toBeTruthy();
    expect(screen.getByText(/Fuege neue Schritte direkt aus der rechten Seitenleiste hinzu/i)).toBeTruthy();
  });

  it("creates a new workflow definition", async () => {
    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    fireEvent.click(await screen.findByText("Verwaltung"));
    fireEvent.change(screen.getByLabelText("Technischer Ablauf-Key"), { target: { value: "offboarding" } });
    fireEvent.change(screen.getAllByLabelText("Name")[0]!, { target: { value: "Offboarding" } });
    fireEvent.click(screen.getByRole("button", { name: "Ablauf anlegen" }));

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
    fireEvent.click(screen.getByRole("button", { name: /Automatisierung/i }));
    expect(await screen.findByLabelText("Baustein")).toBeTruthy();

    expect(await screen.findByRole("heading", { name: "Verfuegbare Aktionen" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Create AD User hinzufuegen" })).toBeTruthy();
    expect(screen.getByText("braucht Freigabe")).toBeTruthy();
    expect((screen.getByRole("button", { name: "Assign Groups hinzufuegen" }) as HTMLButtonElement).disabled).toBe(true);
  });

  it("keeps the builder usable when the action catalog cannot be loaded", async () => {
    const onError = vi.fn();
    mockedGetAdminWorkflowActionDefinitions.mockRejectedValueOnce(new Error("Action-Katalog nicht verfuegbar."));

    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={onError} />,
      { roleKeys: ["auth_admin"] }
    );

    expect((await screen.findAllByText("HR Onboarding")).length).toBeGreaterThan(0);
    expect(screen.getByRole("heading", { name: "Schritte fuer den Ablauf" })).toBeTruthy();
    await waitFor(() => {
      expect(onError).toHaveBeenCalledWith(expect.stringContaining("Aktionskatalog"));
    });
  });

  it("renders a visible studio top bar with grouped builder actions", async () => {
    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    expect(await screen.findByRole("heading", { name: "Ablauf-Editor" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Schritt loeschen" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Pruefen" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Freigeben" })).toBeTruthy();
    expect(screen.getByText("Bausteine sichtbar")).toBeTruthy();
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
    fireEvent.click(screen.getByRole("button", { name: /Aufgabe/i }));
    await screen.findByLabelText("Titel");
    fireEvent.click(screen.getByRole("button", { name: "Erweitert anzeigen" }));
    fireEvent.change(screen.getByLabelText("Technischer Schritt-Key"), { target: { value: "task_a" } });
    fireEvent.change(screen.getByLabelText("Technische Konfiguration"), { target: { value: "{invalid" } });
    fireEvent.click(screen.getByRole("button", { name: "Pruefen" }));

    await waitFor(() => {
      expect(mockedReplaceAdminWorkflowDefinitionVersion).not.toHaveBeenCalled();
      expect(mockedPublishAdminWorkflowDefinitionVersion).not.toHaveBeenCalled();
      expect(onError).toHaveBeenCalledWith("Der aktuelle Stand enthaelt lokale Fehler.");
    });
  });

  it("deletes the selected node from the toolbar", async () => {
    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    const deleteButton = await screen.findByRole("button", { name: "Schritt loeschen" });
    expect((deleteButton as HTMLButtonElement).disabled).toBe(true);

    fireEvent.click((await screen.findAllByRole("button", { name: "Start" }))[0]!);
    await waitFor(() => {
      expect((screen.getByRole("button", { name: "Schritt loeschen" }) as HTMLButtonElement).disabled).toBe(false);
    });

    fireEvent.click(screen.getByRole("button", { name: "Schritt loeschen" }));

    await waitFor(() => {
      expect(screen.getByTestId("mock-react-flow").textContent).toContain("nodes:1 edges:0");
    });
    expect(screen.getByRole("heading", { name: "Schritte fuer den Ablauf" })).toBeTruthy();
  });

  it("adds a controlled action from the catalog to an automation node", async () => {
    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    await waitFor(() => {
      expect(screen.getByTestId("mock-react-flow").textContent).toContain("nodes:2 edges:1");
    });

    fireEvent.click(screen.getByRole("button", { name: /Automatisierung/i }));
    fireEvent.click(screen.getByRole("button", { name: "Create AD User hinzufuegen" }));

    expect(await screen.findByRole("heading", { name: "Hinterlegte Aktionen" })).toBeTruthy();
    expect(screen.getByDisplayValue("Create AD User")).toBeTruthy();
    expect((screen.getByLabelText("Aktions-Reihenfolge") as HTMLInputElement).value).toBe("1");
    fireEvent.click(screen.getByRole("button", { name: "Erweitert anzeigen" }));
    fireEvent.change(screen.getByLabelText("Technischer Schritt-Key"), { target: { value: "automation_step" } });

    fireEvent.click(screen.getByRole("button", { name: "Speichern" }));

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

    const publishButton = await screen.findByRole("button", { name: "Freigeben" });
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
    fireEvent.click(await screen.findByRole("button", { name: "Pruefen" }));

    await waitFor(() => {
      expect(mockedReplaceAdminWorkflowDefinitionVersion).not.toHaveBeenCalled();
      expect(mockedPublishAdminWorkflowDefinitionVersion).not.toHaveBeenCalled();
      expect(onNotice).toHaveBeenCalledWith("Lokale Pruefung erfolgreich.");
    });
    expect(screen.queryByText("Lokale Pruefung")).toBeNull();
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

    fireEvent.click(screen.getByRole("button", { name: "Speichern" }));

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

    fireEvent.click(screen.getByRole("button", { name: "Speichern" }));

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

  it("switches from palette to inspector after explicit node selection", async () => {
    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    expect(await screen.findByRole("heading", { name: "Schritte fuer den Ablauf" })).toBeTruthy();
    fireEvent.click((await screen.findAllByRole("button", { name: "Start" }))[0]!);
    expect(await screen.findByText("Dieser Schritt startet den Ablauf und fuehrt in die ersten Folgeschritte.")).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Schritt" })).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Naechste Schritte" })).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Automatische Aktionen" })).toBeTruthy();
  });

  it("returns from inspector to palette when the canvas pane is clicked", async () => {
    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    fireEvent.click((await screen.findAllByRole("button", { name: "Start" }))[0]!);
    expect(await screen.findByText("Dieser Schritt startet den Ablauf und fuehrt in die ersten Folgeschritte.")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Mock pane click" }));
    expect(await screen.findByRole("heading", { name: "Schritte fuer den Ablauf" })).toBeTruthy();
  });

  it("edits outgoing edge data for the selected node in the sidebar", async () => {
    mockedGetAdminWorkflowDefinitionVersion.mockResolvedValueOnce(createRichVersionDetail());

    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    fireEvent.click((await screen.findAllByRole("button", { name: "Weiterer Pfad" }))[0]!);
    expect(await screen.findByText(/verzweigt ihr den Ablauf/i)).toBeTruthy();
    expect(screen.getAllByLabelText("Naechster Schritt").length).toBeGreaterThan(0);

    fireEvent.change(screen.getAllByLabelText("Bedingung")[0]!, {
      target: { value: "{\"answerKey\":\"approved\",\"operator\":\"is_true\"}" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Speichern" }));

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
    expect(await screen.findByRole("heading", { name: "Verfuegbare Aktionen" })).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Hinterlegte Aktionen" })).toBeTruthy();
    expect(screen.getAllByLabelText("Aktion").length).toBeGreaterThan(0);
    expect(screen.getAllByLabelText("Eingabe-Mapping (JSON)").length).toBeGreaterThan(0);
  });

  it("keeps the action layer visible but read-only for non-automation nodes", async () => {
    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    fireEvent.click((await screen.findAllByRole("button", { name: "Start" }))[0]!);
    expect(await screen.findByRole("heading", { name: "Automatische Aktionen" })).toBeTruthy();
    expect(screen.getByText(/Automatische Aktionen werden nur auf Schritten vom Typ/i)).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Create AD User hinzufuegen" })).toBeNull();
  });

  it("keeps technical fields inside the advanced sidebar section", async () => {
    mockedGetAdminWorkflowDefinitionVersion.mockResolvedValueOnce(createRichVersionDetail());

    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    fireEvent.click((await screen.findAllByRole("button", { name: "Equipment vorbereiten" }))[0]!);
    expect(screen.queryByLabelText("Technischer Schritt-Key")).toBeNull();
    expect(screen.queryByLabelText("Sortierung")).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Erweitert anzeigen" }));
    expect(await screen.findByLabelText("Technischer Schritt-Key")).toBeTruthy();
    expect(screen.getByLabelText("Sortierung")).toBeTruthy();
  });

  it("renders semantic builder cards per node type instead of technical dataset labels", async () => {
    mockedGetAdminWorkflowDefinitionVersion.mockResolvedValueOnce(createRichVersionDetail());

    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    await waitFor(() => {
      expect(screen.getByTestId("mock-react-flow").textContent).toContain("nodes:7 edges:7");
    });
    expect(screen.getAllByText("Formular").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Freigabe").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Entscheidung").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Mitarbeiterdaten").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Manager Freigabe").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Weiterer Pfad").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Konten erstellen").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Equipment vorbereiten").length).toBeGreaterThan(0);
    await waitFor(() => {
      expect(screen.getAllByText("Zustaendig").length).toBeGreaterThan(0);
      expect(screen.getAllByText("Fuehrungskraft").length).toBeGreaterThan(0);
      expect(screen.getAllByText("IT-Service").length).toBeGreaterThan(0);
      expect(screen.getAllByText("Frist").length).toBeGreaterThan(0);
      expect(screen.getByText("2 Tage")).toBeTruthy();
      expect(screen.getByText("5 Tage")).toBeTruthy();
      expect(screen.getAllByText("Danach").length).toBeGreaterThan(0);
      expect(screen.getAllByText("Manuell").length).toBeGreaterThan(0);
      expect(screen.getAllByText("Automatisch").length).toBeGreaterThan(0);
    });
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
    expect(screen.queryByRole("button", { name: "Ablauf anlegen" })).toBeNull();
    expect((screen.getByRole("button", { name: "Freigeben" }) as HTMLButtonElement).disabled).toBe(true);

    fireEvent.click((await screen.findAllByRole("button", { name: "Konten erstellen" }))[0]!);
    expect(await screen.findByText(/nur im Admin-Modus editierbar/i)).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Create AD User hinzufuegen" })).toBeNull();
  });
});
