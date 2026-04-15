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
  createAdminAnswerDefinition: vi.fn(),
  createAdminTaskTemplate: vi.fn(),
  createAdminTaskTemplateCondition: vi.fn(),
  createAdminTaskTemplateDependency: vi.fn(),
  createAdminWorkflowDefinition: vi.fn(),
  createAdminWorkflowDefinitionVersion: vi.fn(),
  deleteAdminAnswerDefinition: vi.fn(),
  deleteAdminTaskTemplate: vi.fn(),
  deleteAdminTaskTemplateCondition: vi.fn(),
  deleteAdminTaskTemplateDependency: vi.fn(),
  deleteAdminWorkflowDefinition: vi.fn(),
  getAdminAnswerDefinitions: vi.fn(),
  getAdminDependencyGraph: vi.fn(),
  getAdminWorkflowActionDefinitions: vi.fn(),
  getAdminProcessTypes: vi.fn(),
  getAdminTaskTemplateConditions: vi.fn(),
  getAdminTaskTemplateDependencies: vi.fn(),
  getAdminTaskTemplates: vi.fn(),
  getAdminWorkflowDefinitionVersion: vi.fn(),
  getOrCreateAdminWorkflowDefinitionWorkingDraft: vi.fn(),
  getAdminWorkflowDefinitions: vi.fn(),
  publishAdminWorkflowDefinitionVersion: vi.fn(),
  replaceAdminWorkflowDefinitionVersion: vi.fn(),
  updateAdminAnswerDefinition: vi.fn(),
  updateAdminProcessType: vi.fn(),
  updateAdminTaskTemplate: vi.fn(),
  updateAdminWorkflowDefinition: vi.fn(),
}));

vi.mock("../src/services/adminApi", () => ({
  getAdminDepartmentAssignments: vi.fn(),
  getAdminResponsibilityOwners: vi.fn(),
}));

const mockedCreateAdminAnswerDefinition = vi.mocked(adminConfigApi.createAdminAnswerDefinition);
const mockedCreateAdminTaskTemplate = vi.mocked(adminConfigApi.createAdminTaskTemplate);
const mockedCreateAdminTaskTemplateCondition = vi.mocked(adminConfigApi.createAdminTaskTemplateCondition);
const mockedCreateAdminTaskTemplateDependency = vi.mocked(adminConfigApi.createAdminTaskTemplateDependency);
const mockedCreateAdminWorkflowDefinition = vi.mocked(adminConfigApi.createAdminWorkflowDefinition);
const mockedCreateAdminWorkflowDefinitionVersion = vi.mocked(adminConfigApi.createAdminWorkflowDefinitionVersion);
const mockedDeleteAdminAnswerDefinition = vi.mocked(adminConfigApi.deleteAdminAnswerDefinition);
const mockedDeleteAdminTaskTemplate = vi.mocked(adminConfigApi.deleteAdminTaskTemplate);
const mockedDeleteAdminTaskTemplateCondition = vi.mocked(adminConfigApi.deleteAdminTaskTemplateCondition);
const mockedDeleteAdminTaskTemplateDependency = vi.mocked(adminConfigApi.deleteAdminTaskTemplateDependency);
const mockedDeleteAdminWorkflowDefinition = vi.mocked(adminConfigApi.deleteAdminWorkflowDefinition);
const mockedGetAdminAnswerDefinitions = vi.mocked(adminConfigApi.getAdminAnswerDefinitions);
const mockedGetAdminDependencyGraph = vi.mocked(adminConfigApi.getAdminDependencyGraph);
const mockedGetAdminWorkflowActionDefinitions = vi.mocked(adminConfigApi.getAdminWorkflowActionDefinitions);
const mockedGetAdminProcessTypes = vi.mocked(adminConfigApi.getAdminProcessTypes);
const mockedGetAdminTaskTemplateConditions = vi.mocked(adminConfigApi.getAdminTaskTemplateConditions);
const mockedGetAdminTaskTemplateDependencies = vi.mocked(adminConfigApi.getAdminTaskTemplateDependencies);
const mockedGetAdminTaskTemplates = vi.mocked(adminConfigApi.getAdminTaskTemplates);
const mockedGetAdminWorkflowDefinitionVersion = vi.mocked(adminConfigApi.getAdminWorkflowDefinitionVersion);
const mockedGetOrCreateAdminWorkflowDefinitionWorkingDraft = vi.mocked(adminConfigApi.getOrCreateAdminWorkflowDefinitionWorkingDraft);
const mockedGetAdminWorkflowDefinitions = vi.mocked(adminConfigApi.getAdminWorkflowDefinitions);
const mockedPublishAdminWorkflowDefinitionVersion = vi.mocked(adminConfigApi.publishAdminWorkflowDefinitionVersion);
const mockedReplaceAdminWorkflowDefinitionVersion = vi.mocked(adminConfigApi.replaceAdminWorkflowDefinitionVersion);
const mockedUpdateAdminAnswerDefinition = vi.mocked(adminConfigApi.updateAdminAnswerDefinition);
const mockedUpdateAdminProcessType = vi.mocked(adminConfigApi.updateAdminProcessType);
const mockedUpdateAdminTaskTemplate = vi.mocked(adminConfigApi.updateAdminTaskTemplate);
const mockedUpdateAdminWorkflowDefinition = vi.mocked(adminConfigApi.updateAdminWorkflowDefinition);
const mockedGetAdminDepartmentAssignments = vi.mocked(adminApi.getAdminDepartmentAssignments);
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

function createSetupVersionDetail() {
  return {
    ...createVersionDetail(),
    nodes: [
      { nodeKey: "start", nodeType: "start", title: "Start", sortOrder: 1, positionX: 80, positionY: 60, config: null, actions: [] },
      {
        nodeKey: "request_form",
        nodeType: "form",
        title: "Anforderungen erfassen",
        sortOrder: 2,
        positionX: 360,
        positionY: 60,
        config: { legacyProcessTypeKey: "onboarding" },
        actions: [],
      },
      {
        nodeKey: "manager_approval",
        nodeType: "approval",
        title: "Supervisor / Freigabe",
        sortOrder: 3,
        positionX: 640,
        positionY: 60,
        config: { legacyTemplateKey: "manager_check" },
        actions: [],
      },
      {
        nodeKey: "department_setup",
        nodeType: "measure_provision",
        title: "Bereitstellungsmaßnahmen erzeugen",
        sortOrder: 4,
        positionX: 920,
        positionY: 60,
        config: null,
        actions: [],
      },
      { nodeKey: "end", nodeType: "end", title: "Abschluss", sortOrder: 5, positionX: 1200, positionY: 60, config: null, actions: [] },
    ],
    edges: [
      { sourceNodeKey: "start", targetNodeKey: "request_form", priority: 1, conditionExpression: null },
      { sourceNodeKey: "request_form", targetNodeKey: "manager_approval", priority: 1, conditionExpression: null },
      { sourceNodeKey: "manager_approval", targetNodeKey: "department_setup", priority: 1, conditionExpression: null },
      { sourceNodeKey: "department_setup", targetNodeKey: "end", priority: 1, conditionExpression: null },
    ],
  };
}

function createLifecycleMeasureVersionDetail(
  processTypeKey: string,
  formTitle: string,
  measureNodeType: "measure_change" | "measure_rename",
  measureTitle: string
) {
  return {
    ...createVersionDetail(),
    primaryLegacyProcessTypeKey: processTypeKey,
    nodes: [
      { nodeKey: "start", nodeType: "start", title: "Start", sortOrder: 1, positionX: 80, positionY: 60, config: null, actions: [] },
      {
        nodeKey: "collect_requirements",
        nodeType: "form",
        title: formTitle,
        sortOrder: 2,
        positionX: 360,
        positionY: 60,
        config: { legacyProcessTypeKey: processTypeKey },
        actions: [],
      },
      {
        nodeKey: "department_setup",
        nodeType: measureNodeType,
        title: measureTitle,
        sortOrder: 3,
        positionX: 640,
        positionY: 60,
        config: null,
        actions: [],
      },
      { nodeKey: "end", nodeType: "end", title: "Abschluss", sortOrder: 4, positionX: 920, positionY: 60, config: null, actions: [] },
    ],
    edges: [
      { sourceNodeKey: "start", targetNodeKey: "collect_requirements", priority: 1, conditionExpression: null },
      { sourceNodeKey: "collect_requirements", targetNodeKey: "department_setup", priority: 1, conditionExpression: null },
      { sourceNodeKey: "department_setup", targetNodeKey: "end", priority: 1, conditionExpression: null },
    ],
  };
}

describe("AdminWorkflowBuilderSection", () => {
  beforeEach(() => {
    mockedCreateAdminAnswerDefinition.mockReset();
    mockedCreateAdminTaskTemplate.mockReset();
    mockedCreateAdminTaskTemplateCondition.mockReset();
    mockedCreateAdminTaskTemplateDependency.mockReset();
    mockedCreateAdminWorkflowDefinition.mockReset();
    mockedCreateAdminWorkflowDefinitionVersion.mockReset();
    mockedDeleteAdminAnswerDefinition.mockReset();
    mockedDeleteAdminTaskTemplate.mockReset();
    mockedDeleteAdminTaskTemplateCondition.mockReset();
    mockedDeleteAdminTaskTemplateDependency.mockReset();
    mockedDeleteAdminWorkflowDefinition.mockReset();
    mockedGetAdminAnswerDefinitions.mockReset();
    mockedGetAdminDependencyGraph.mockReset();
    mockedGetAdminWorkflowActionDefinitions.mockReset();
    mockedGetAdminProcessTypes.mockReset();
    mockedGetAdminTaskTemplateConditions.mockReset();
    mockedGetAdminTaskTemplateDependencies.mockReset();
    mockedGetAdminTaskTemplates.mockReset();
    mockedGetAdminWorkflowDefinitionVersion.mockReset();
    mockedGetOrCreateAdminWorkflowDefinitionWorkingDraft.mockReset();
    mockedGetAdminWorkflowDefinitions.mockReset();
    mockedPublishAdminWorkflowDefinitionVersion.mockReset();
    mockedReplaceAdminWorkflowDefinitionVersion.mockReset();
    mockedUpdateAdminAnswerDefinition.mockReset();
    mockedUpdateAdminProcessType.mockReset();
    mockedUpdateAdminTaskTemplate.mockReset();
    mockedUpdateAdminWorkflowDefinition.mockReset();
    mockedGetAdminDepartmentAssignments.mockReset();
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
    mockedGetOrCreateAdminWorkflowDefinitionWorkingDraft.mockResolvedValue(createVersionDetail());
    mockedGetAdminAnswerDefinitions.mockResolvedValue([]);
    mockedGetAdminDependencyGraph.mockResolvedValue({ nodes: [], edges: [] });
    mockedGetAdminProcessTypes.mockResolvedValue([
      {
        id: 7,
        key: "onboarding",
        name: "Onboarding",
        description: "Führt die Angaben für den Eintritt zusammen.",
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
    mockedGetAdminDepartmentAssignments.mockResolvedValue([]);
    mockedGetAdminResponsibilityOwners.mockResolvedValue([
      {
        responsibilityId: 55,
        responsibilityKey: "manager",
        systemKey: null,
        responsibilityName: "Führungskraft",
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
    mockedCreateAdminAnswerDefinition.mockResolvedValue({
      id: 301,
      processTypeId: 7,
      answerKey: "new_field",
      title: "Neues Feld",
      category: "general",
      description: "",
      iconKey: null,
      inputType: "boolean",
      isRequired: false,
      sortOrder: 99,
      isActive: true,
    });
    mockedCreateAdminTaskTemplate.mockResolvedValue({
      id: 999,
      processTypeId: 7,
      templateKey: "new_template",
      title: "Neue Vorlage",
      category: "task",
      description: "",
      iconKey: null,
      owningDepartmentId: null,
      defaultResponsibilityId: null,
      processAreaLabel: null,
      isDepartmentPhaseTask: true,
      isRequired: false,
      dueInDays: null,
      sortOrder: 99,
      isActive: true,
      createdAt: "2026-04-08T10:00:00Z",
      conditionCount: 0,
      dependencyCount: 0,
    });
    mockedCreateAdminTaskTemplateCondition.mockResolvedValue({
      id: 401,
      taskTemplateId: 999,
      conditionGroup: 1,
      answerKey: "new_field",
      operator: "is_true",
      expectedValueText: null,
      expectedValueBoolean: true,
      expectedValueNumber: null,
    });
    mockedCreateAdminTaskTemplateDependency.mockResolvedValue({
      id: 501,
      taskTemplateId: 999,
      dependsOnTaskTemplateId: 102,
      dependsOnTemplateTitle: "Equipment vorbereiten",
      requiredStatus: "done",
    });
    mockedDeleteAdminAnswerDefinition.mockResolvedValue();
    mockedDeleteAdminTaskTemplate.mockResolvedValue();
    mockedDeleteAdminTaskTemplateCondition.mockResolvedValue();
    mockedDeleteAdminTaskTemplateDependency.mockResolvedValue();
    mockedDeleteAdminWorkflowDefinition.mockResolvedValue();
    mockedReplaceAdminWorkflowDefinitionVersion.mockResolvedValue(createVersionDetail());
    mockedPublishAdminWorkflowDefinitionVersion.mockResolvedValue(createVersionDetail());
    mockedUpdateAdminAnswerDefinition.mockImplementation(async (_id, payload) => ({
      id: 302,
      processTypeId: 7,
      answerKey: String(payload.answerKey),
      title: String(payload.title),
      category: String(payload.category),
      description: String(payload.description ?? ""),
      iconKey: payload.iconKey as string | null,
      inputType: payload.inputType as "boolean" | "text" | "select" | "multi_select",
      isRequired: Boolean(payload.isRequired),
      sortOrder: Number(payload.sortOrder),
      isActive: Boolean(payload.isActive),
    }));
    mockedUpdateAdminProcessType.mockImplementation(async (processTypeId, payload) => ({
      id: processTypeId,
      key: "onboarding",
      name: String(payload.name ?? "Onboarding"),
      description: (payload.description as string | null | undefined) ?? null,
      requiresSupervisorStep: false,
      approvalTaskTemplateKey: null,
      requiresTargetPerson: false,
      iconKey: (payload.iconKey as string | null | undefined) ?? null,
      isActive: (payload.isActive as boolean | undefined) ?? true,
      sortOrder: Number(payload.sortOrder ?? 1),
      workflowCount: 0,
      answerDefinitionCount: 0,
      taskTemplateCount: 2,
      canActivate: true,
      activationBlockedReason: null,
    }));
    mockedUpdateAdminTaskTemplate.mockImplementation(async (templateId, payload) => ({
      id: templateId,
      processTypeId: Number(payload.processTypeId),
      templateKey: String(payload.templateKey),
      title: String(payload.title),
      category: String(payload.category),
      description: String(payload.description),
      iconKey: payload.iconKey as string | null,
      owningDepartmentId: payload.owningDepartmentId as number | null,
      defaultResponsibilityId: payload.defaultResponsibilityId as number | null,
      processAreaLabel: payload.processAreaLabel as string | null,
      isDepartmentPhaseTask: Boolean(payload.isDepartmentPhaseTask),
      isRequired: Boolean(payload.isRequired),
      dueInDays: payload.dueInDays as number | null,
      sortOrder: Number(payload.sortOrder),
      isActive: Boolean(payload.isActive),
      createdAt: "2026-04-08T10:00:00Z",
      conditionCount: 0,
      dependencyCount: 0,
    }));
    mockedUpdateAdminWorkflowDefinition.mockResolvedValue(createDefinition());
  });

  it("loads definitions and renders the selected draft", async () => {
    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    expect(await screen.findByRole("heading", { name: "Abläufe" })).toBeTruthy();
    expect(screen.getAllByText("HR Onboarding").length).toBeGreaterThan(0);
    expect(screen.getAllByText(/Entwurf 1/i).length).toBeGreaterThan(0);
    await waitFor(() => {
      expect(screen.getByTestId("mock-react-flow").textContent).toContain("nodes:2 edges:1");
    });
    expect(screen.getByRole("heading", { name: /Schritte.*Ablauf/i })).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Details zum Arbeitsentwurf" })).toBeTruthy();
  });

  it("creates a new workflow definition", async () => {
    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    fireEvent.click(await screen.findByText("Verwaltung"));
    fireEvent.change(screen.getAllByLabelText("Name")[0]!, { target: { value: "Offboarding" } });
    fireEvent.click(screen.getByRole("button", { name: "Ablauf anlegen" }));

    await waitFor(() => {
      expect(mockedCreateAdminWorkflowDefinition).toHaveBeenCalledWith({
        key: null,
        name: "Offboarding",
        description: null,
      });
    });
  });

  it("deletes the selected workflow definition from the toolbar", async () => {
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);
    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    fireEvent.click(await screen.findByRole("button", { name: "Ablauf löschen" }));

    await waitFor(() => {
      expect(mockedDeleteAdminWorkflowDefinition).toHaveBeenCalledWith(1);
    });

    confirmSpy.mockRestore();
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

    expect(await screen.findByRole("heading", { name: "Verfügbare Aktionen" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Create AD User hinzufügen" })).toBeTruthy();
    expect(screen.getByText("braucht Freigabe")).toBeTruthy();
    expect((screen.getByRole("button", { name: "Assign Groups hinzufügen" }) as HTMLButtonElement).disabled).toBe(true);
  });

  it("keeps the builder usable when the action catalog cannot be loaded", async () => {
    const onError = vi.fn();
    mockedGetAdminWorkflowActionDefinitions.mockRejectedValueOnce(new Error("Action-Katalog nicht verfuegbar."));

    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={onError} />,
      { roleKeys: ["auth_admin"] }
    );

    expect((await screen.findAllByText("HR Onboarding")).length).toBeGreaterThan(0);
    expect(screen.getByRole("heading", { name: /Schritte.*Ablauf/i })).toBeTruthy();
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
    expect(screen.getByRole("button", { name: "Schritt löschen" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Prüfen" })).toBeTruthy();
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
    fireEvent.click(screen.getByTitle("Aufgabe hinzufügen"));
    await screen.findByLabelText("Titel");
    fireEvent.click(screen.getByRole("button", { name: "Erweitert anzeigen" }));
    fireEvent.change(screen.getByLabelText("Technischer Schritt-Key"), { target: { value: "task_a" } });
    fireEvent.change(screen.getByLabelText("Technische Konfiguration"), { target: { value: "{invalid" } });
    fireEvent.click(screen.getByRole("button", { name: "Prüfen" }));

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

    const deleteButton = await screen.findByRole("button", { name: "Schritt löschen" });
    expect((deleteButton as HTMLButtonElement).disabled).toBe(true);

    fireEvent.click((await screen.findAllByRole("button", { name: "Start" }))[0]!);
    await waitFor(() => {
      expect((screen.getByRole("button", { name: "Schritt löschen" }) as HTMLButtonElement).disabled).toBe(false);
    });

    fireEvent.click(screen.getByRole("button", { name: "Schritt löschen" }));

    await waitFor(() => {
      expect(screen.getByTestId("mock-react-flow").textContent).toContain("nodes:1 edges:0");
    });
    expect(screen.getByRole("heading", { name: /Schritte.*Ablauf/i })).toBeTruthy();
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
    fireEvent.click(screen.getByRole("button", { name: "Create AD User hinzufügen" }));

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
    fireEvent.click(await screen.findByRole("button", { name: "Prüfen" }));

    await waitFor(() => {
      expect(mockedReplaceAdminWorkflowDefinitionVersion).not.toHaveBeenCalled();
      expect(mockedPublishAdminWorkflowDefinitionVersion).not.toHaveBeenCalled();
      expect(onNotice).toHaveBeenCalledWith("Lokale Pruefung erfolgreich.");
    });
    expect(screen.queryByText("Lokale Pruefung")).toBeNull();
  });

  it("persists structured auto-layout positions on save", async () => {
    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    await waitFor(() => {
      expect(screen.getByTestId("mock-react-flow").textContent).toContain("nodes:2 edges:1");
    });
    fireEvent.click((await screen.findAllByRole("button", { name: "Start" }))[0]!);
    fireEvent.click(await screen.findByRole("button", { name: "Erweitert anzeigen" }));
    expect((await screen.findByLabelText("Position X") as HTMLInputElement).value).not.toBe("");
    expect((screen.getByLabelText("Position Y") as HTMLInputElement).value).not.toBe("");

    fireEvent.click(screen.getByRole("button", { name: "Speichern" }));

    await waitFor(() => {
      expect(mockedReplaceAdminWorkflowDefinitionVersion).toHaveBeenCalledWith(
        11,
        expect.objectContaining({
          nodes: expect.arrayContaining([
            expect.objectContaining({
              nodeKey: "start",
              positionX: expect.any(Number),
              positionY: expect.any(Number),
            }),
          ]),
        })
      );
    });
  });

  it("blocks saving when canvas connect creates an invalid second outgoing edge on a non-branch node", async () => {
    const onError = vi.fn();
    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={onError} />,
      { roleKeys: ["auth_admin"] }
    );

    await waitFor(() => {
      expect(screen.getByTestId("mock-react-flow").textContent).toContain("edges:1");
    });
    fireEvent.click(screen.getByRole("button", { name: "Mock connect first two nodes" }));
    expect(screen.getByTestId("mock-react-flow").textContent).toContain("nodes:4 edges:4");

    fireEvent.click(screen.getByRole("button", { name: "Speichern" }));

    await waitFor(() => {
      expect(mockedReplaceAdminWorkflowDefinitionVersion).not.toHaveBeenCalled();
      expect(onError).toHaveBeenCalledWith("Der aktuelle Stand enthaelt lokale Fehler.");
    });
  });

  it("switches from palette to inspector after explicit node selection", async () => {
    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    expect(await screen.findByRole("heading", { name: /Schritte.*Ablauf/i })).toBeTruthy();
    fireEvent.click((await screen.findAllByRole("button", { name: "Start" }))[0]!);
    expect(await screen.findByText("Dieser Schritt startet den Ablauf und führt in die ersten Folgeschritte.")).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Schritt" })).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Nächste Schritte" })).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Automatische Aktionen" })).toBeTruthy();
  });

  it("returns from inspector to palette when the canvas pane is clicked", async () => {
    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    fireEvent.click((await screen.findAllByRole("button", { name: "Start" }))[0]!);
    expect(await screen.findByText("Dieser Schritt startet den Ablauf und führt in die ersten Folgeschritte.")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Mock pane click" }));
    expect(await screen.findByRole("heading", { name: /Schritte.*Ablauf/i })).toBeTruthy();
  });

  it("edits visible outgoing path data but keeps technical conditions hidden", async () => {
    mockedGetOrCreateAdminWorkflowDefinitionWorkingDraft.mockResolvedValueOnce(createRichVersionDetail());

    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    fireEvent.click((await screen.findAllByRole("button", { name: "Weiterer Pfad" }))[0]!);
    expect(await screen.findByText(/verzweigt ihr den Ablauf/i)).toBeTruthy();
    expect(screen.getAllByLabelText("Nächster Schritt").length).toBeGreaterThan(0);
    expect(screen.queryByLabelText("Bedingung")).toBeNull();

    fireEvent.change(screen.getAllByLabelText("Pfad-Reihenfolge")[0]!, {
      target: { value: "3" },
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
              priority: 3,
            }),
          ]),
        })
      );
    });
  });

  it("renders provision measure blocks as grouped business modules instead of technical main-flow nodes", async () => {
    mockedGetOrCreateAdminWorkflowDefinitionWorkingDraft.mockResolvedValueOnce(createSetupVersionDetail());
    mockedGetAdminTaskTemplates.mockResolvedValue([
      {
        id: 201,
        processTypeId: 7,
        templateKey: "ad_user_setup",
        title: "AD-Konto anlegen",
        category: "task",
        description: "AD Konto fuer den Start vorbereiten.",
        iconKey: null,
        owningDepartmentId: null,
        defaultResponsibilityId: 56,
        processAreaLabel: null,
        isDepartmentPhaseTask: true,
        isRequired: true,
        dueInDays: 2,
        sortOrder: 1,
        isActive: true,
        createdAt: "2026-04-08T10:00:00Z",
        conditionCount: 1,
        dependencyCount: 0,
      },
      {
        id: 202,
        processTypeId: 7,
        templateKey: "mailbox_setup",
        title: "Mailbox anlegen",
        category: "task",
        description: "Mailbox fuer den Start vorbereiten.",
        iconKey: null,
        owningDepartmentId: null,
        defaultResponsibilityId: 56,
        processAreaLabel: null,
        isDepartmentPhaseTask: true,
        isRequired: true,
        dueInDays: 2,
        sortOrder: 2,
        isActive: true,
        createdAt: "2026-04-08T10:00:00Z",
        conditionCount: 1,
        dependencyCount: 0,
      },
      {
        id: 203,
        processTypeId: 7,
        templateKey: "hardware_setup",
        title: "Hardware vorbereiten",
        category: "task",
        description: "Hardware fuer den Start vorbereiten.",
        iconKey: null,
        owningDepartmentId: null,
        defaultResponsibilityId: 56,
        processAreaLabel: null,
        isDepartmentPhaseTask: true,
        isRequired: true,
        dueInDays: 5,
        sortOrder: 3,
        isActive: true,
        createdAt: "2026-04-08T10:00:00Z",
        conditionCount: 1,
        dependencyCount: 0,
      },
      {
        id: 204,
        processTypeId: 7,
        templateKey: "quality_setup",
        title: "Qualitaetseinweisung",
        category: "task",
        description: "QS vorbereitet die Einweisung.",
        iconKey: null,
        owningDepartmentId: null,
        defaultResponsibilityId: 57,
        processAreaLabel: null,
        isDepartmentPhaseTask: true,
        isRequired: true,
        dueInDays: 3,
        sortOrder: 4,
        isActive: true,
        createdAt: "2026-04-08T10:00:00Z",
        conditionCount: 1,
        dependencyCount: 0,
      },
    ]);
    mockedGetAdminTaskTemplateConditions.mockResolvedValue([]);
    mockedGetAdminTaskTemplateDependencies.mockResolvedValue([]);
    mockedGetAdminResponsibilityOwners.mockResolvedValue([
      {
        responsibilityId: 55,
        responsibilityKey: "manager",
        systemKey: null,
        responsibilityName: "Führungskraft",
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
        departmentId: 10,
        departmentName: "IT",
        appUserId: null,
        appUserDisplayName: null,
        updatedAt: null,
      },
      {
        responsibilityId: 57,
        responsibilityKey: "quality",
        systemKey: null,
        responsibilityName: "QS-Team",
        responsibilityType: "application",
        departmentId: 20,
        departmentName: "QS",
        appUserId: null,
        appUserDisplayName: null,
        updatedAt: null,
      },
    ]);

    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    await waitFor(() => {
      expect(screen.getAllByText("Bereitstellungsmaßnahmen erzeugen").length).toBeGreaterThan(0);
      expect(screen.getAllByText("Bereitstellung").length).toBeGreaterThan(0);
      expect(screen.getAllByText("IT: AD, Mail, Hardware").length).toBeGreaterThan(0);
      expect(screen.getAllByText("QS: Qualitaetseinweisung").length).toBeGreaterThan(0);
    });
    expect(screen.queryByText("AD angefordert?")).toBeNull();
    expect(screen.queryByText("Mailbox angefordert?")).toBeNull();
  });

  it("shows automation actions inside the node sidebar", async () => {
    mockedGetOrCreateAdminWorkflowDefinitionWorkingDraft.mockResolvedValueOnce(createRichVersionDetail());

    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    fireEvent.click((await screen.findAllByRole("button", { name: "Konten erstellen" }))[0]!);
    expect(await screen.findByRole("heading", { name: "Verfügbare Aktionen" })).toBeTruthy();
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
    expect(screen.queryByRole("button", { name: "Create AD User hinzufügen" })).toBeNull();
  });

  it("keeps technical fields inside the advanced sidebar section", async () => {
    mockedGetOrCreateAdminWorkflowDefinitionWorkingDraft.mockResolvedValueOnce(createRichVersionDetail());

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
    mockedGetOrCreateAdminWorkflowDefinitionWorkingDraft.mockResolvedValueOnce(createRichVersionDetail());

    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    await waitFor(() => {
      expect(screen.getByTestId("mock-react-flow").textContent).toContain("nodes:9 edges:9");
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
    expect(screen.getAllByText("Zuständig").length).toBeGreaterThan(0);
      expect(screen.getAllByText("Führungskraft").length).toBeGreaterThan(0);
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

  it("shows complete derived measure details without exposing raw condition keys", async () => {
    mockedGetOrCreateAdminWorkflowDefinitionWorkingDraft.mockResolvedValueOnce(createSetupVersionDetail());
    mockedGetAdminAnswerDefinitions.mockResolvedValue([
      {
        id: 401,
        processTypeId: 7,
        answerKey: "needs_ad",
        title: "AD angefordert",
        category: "it",
        description: "Legt fest, ob ein AD-Konto benötigt wird.",
        iconKey: null,
        inputType: "boolean",
        isRequired: false,
        sortOrder: 1,
        isActive: true,
      },
    ]);
    mockedGetAdminTaskTemplates.mockResolvedValue([
      {
        id: 201,
        processTypeId: 7,
        templateKey: "ad_user_setup",
        title: "AD-Konto anlegen",
        category: "task",
        description: "Erstellt das AD-Konto für den Einstieg.",
        iconKey: null,
        owningDepartmentId: null,
        defaultResponsibilityId: 56,
        processAreaLabel: null,
        isDepartmentPhaseTask: true,
        isRequired: true,
        dueInDays: 2,
        sortOrder: 1,
        isActive: true,
        createdAt: "2026-04-08T10:00:00Z",
        conditionCount: 1,
        dependencyCount: 1,
      },
    ]);
    mockedGetAdminTaskTemplateConditions.mockResolvedValue([
      {
        id: 501,
        taskTemplateId: 201,
        conditionGroup: 1,
        answerKey: "needs_ad",
        operator: "is_true",
        expectedValueText: null,
        expectedValueBoolean: true,
        expectedValueNumber: null,
      },
    ]);
    mockedGetAdminTaskTemplateDependencies.mockResolvedValue([
      {
        id: 601,
        taskTemplateId: 201,
        dependsOnTaskTemplateId: 999,
        dependsOnTemplateTitle: "Supervisor / Freigabe",
        requiredStatus: "done",
      },
    ]);

    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    fireEvent.click((await screen.findAllByRole("button", { name: "Bereitstellungsmaßnahmen erzeugen" }))[0]!);
    expect((await screen.findAllByText("AD-Konto anlegen")).length).toBeGreaterThan(0);
    expect(screen.getByText("AD angefordert ausgewählt ist.")).toBeTruthy();
    expect(screen.getByText("Wird erst bearbeitbar, nachdem Supervisor / Freigabe abgeschlossen ist.")).toBeTruthy();
    expect(screen.queryByText("needs_ad")).toBeNull();
    expect(screen.queryByText("is_true")).toBeNull();
  });

  it("opens a focused source editor inside the builder sidebar and returns to the selected node", async () => {
    mockedGetOrCreateAdminWorkflowDefinitionWorkingDraft.mockResolvedValueOnce(createSetupVersionDetail());
    mockedGetAdminAnswerDefinitions.mockResolvedValue([
      {
        id: 401,
        processTypeId: 7,
        answerKey: "needs_ad",
        title: "AD angefordert",
        category: "it",
        description: "Legt fest, ob ein AD-Konto benötigt wird.",
        iconKey: null,
        inputType: "boolean",
        isRequired: false,
        sortOrder: 1,
        isActive: true,
      },
    ]);
    mockedGetAdminTaskTemplates.mockResolvedValue([
      {
        id: 201,
        processTypeId: 7,
        templateKey: "ad_user_setup",
        title: "AD-Konto anlegen",
        category: "task",
        description: "Erstellt das AD-Konto für den Einstieg.",
        iconKey: null,
        owningDepartmentId: null,
        defaultResponsibilityId: 56,
        processAreaLabel: null,
        isDepartmentPhaseTask: true,
        isRequired: true,
        dueInDays: 2,
        sortOrder: 1,
        isActive: true,
        createdAt: "2026-04-08T10:00:00Z",
        conditionCount: 1,
        dependencyCount: 0,
      },
    ]);
    mockedGetAdminTaskTemplateConditions.mockResolvedValue([
      {
        id: 501,
        taskTemplateId: 201,
        conditionGroup: 1,
        answerKey: "needs_ad",
        operator: "is_true",
        expectedValueText: null,
        expectedValueBoolean: true,
        expectedValueNumber: null,
      },
    ]);
    mockedGetAdminTaskTemplateDependencies.mockResolvedValue([]);
    mockedGetAdminDepartmentAssignments.mockResolvedValue([]);

    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    fireEvent.click((await screen.findAllByRole("button", { name: "Bereitstellungsmaßnahmen erzeugen" }))[0]!);
    fireEvent.click((await screen.findAllByRole("button", { name: "Bedingungen bearbeiten" }))[0]!);

    expect(await screen.findByText(/Template-Bedingungen/i)).toBeTruthy();
    await waitFor(() => {
      expect(screen.getAllByText("AD-Konto anlegen").length).toBeGreaterThan(0);
      expect(screen.getByRole("button", { name: "Bedingung hinzufügen" })).toBeTruthy();
    });
    expect(screen.getByRole("button", { name: "Zurück zum Baustein" })).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Zurück zum Baustein" }));
    expect(await screen.findByText("Maßnahmenblock")).toBeTruthy();
    expect(screen.getAllByText("AD-Konto anlegen").length).toBeGreaterThan(0);
  });

  it("shows rename-specific measure semantics for name change", async () => {
    mockedGetOrCreateAdminWorkflowDefinitionWorkingDraft.mockResolvedValueOnce(
      createLifecycleMeasureVersionDetail("name_change", "Namensänderung erfassen", "measure_rename", "Umbenennungsmaßnahmen erzeugen")
    );
    mockedGetAdminProcessTypes.mockResolvedValueOnce([
      {
        id: 8,
        key: "name_change",
        name: "Namensaenderung",
        description: "Koordinierte Aktualisierung eines Mitarbeiternamens in Stammdaten und Systemen.",
        requiresSupervisorStep: false,
        approvalTaskTemplateKey: null,
        requiresTargetPerson: true,
        iconKey: null,
        isActive: true,
        sortOrder: 2,
        workflowCount: 0,
        answerDefinitionCount: 3,
        taskTemplateCount: 2,
        canActivate: true,
        activationBlockedReason: null,
      },
    ]);
    mockedGetAdminTaskTemplates.mockResolvedValueOnce([
      {
        id: 301,
        processTypeId: 8,
        templateKey: "nc_mailbox_update",
        title: "Mailbox und Alias aktualisieren",
        category: "Zugaenge",
        description: "Mailbox, primäre Adresse und Alias auf den neuen Namen umstellen.",
        iconKey: null,
        owningDepartmentId: null,
        defaultResponsibilityId: 56,
        processAreaLabel: null,
        isDepartmentPhaseTask: true,
        isRequired: true,
        dueInDays: 2,
        sortOrder: 1,
        isActive: true,
        createdAt: "2026-04-08T10:00:00Z",
        conditionCount: 0,
        dependencyCount: 0,
      },
    ]);

    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    expect(await screen.findByText(/neuem Namen und Wirksamkeitsdatum/i)).toBeTruthy();

    fireEvent.click((await screen.findAllByRole("button", { name: "Umbenennungsmaßnahmen erzeugen" }))[0]!);
    expect(await screen.findByText("Maßnahmenblock")).toBeTruthy();
  });

  it("explains measure_change differently for position and role changes", async () => {
    mockedGetOrCreateAdminWorkflowDefinitionWorkingDraft.mockResolvedValueOnce(
      createLifecycleMeasureVersionDetail("position_change", "Positionswechsel erfassen", "measure_change", "Änderungsmaßnahmen erzeugen")
    );
    mockedGetAdminProcessTypes.mockResolvedValueOnce([
      {
        id: 9,
        key: "position_change",
        name: "Positionswechsel",
        description: "Koordinierter Wechsel in eine neue Position.",
        requiresSupervisorStep: false,
        approvalTaskTemplateKey: null,
        requiresTargetPerson: true,
        iconKey: null,
        isActive: true,
        sortOrder: 3,
        workflowCount: 0,
        answerDefinitionCount: 4,
        taskTemplateCount: 2,
        canActivate: true,
        activationBlockedReason: null,
      },
    ]);
    mockedGetAdminTaskTemplates.mockResolvedValueOnce([
      {
        id: 401,
        processTypeId: 9,
        templateKey: "pc_training_assign",
        title: "Schulungen einplanen",
        category: "Qualifizierung",
        description: "Noetige Schulungen und Einweisungen fuer die neue Position planen.",
        iconKey: null,
        owningDepartmentId: null,
        defaultResponsibilityId: 55,
        processAreaLabel: null,
        isDepartmentPhaseTask: true,
        isRequired: true,
        dueInDays: 5,
        sortOrder: 1,
        isActive: true,
        createdAt: "2026-04-08T10:00:00Z",
        conditionCount: 0,
        dependencyCount: 0,
      },
    ]);

    const { unmount } = renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    expect(await screen.findByText(/positionsbezogene Berechtigungs-, Zugriffs-, Schulungs- und Systemanpassungen/i)).toBeTruthy();
    fireEvent.click((await screen.findAllByRole("button", { name: "Änderungsmaßnahmen erzeugen" }))[0]!);
    expect(await screen.findByText(/positionsbezogene Berechtigungs-, Zugriffs-, Schulungs- und Systemanpassungen/i)).toBeTruthy();
    unmount();

    mockedGetOrCreateAdminWorkflowDefinitionWorkingDraft.mockResolvedValueOnce(
      createLifecycleMeasureVersionDetail("role_change", "Rollenwechsel erfassen", "measure_change", "Änderungsmaßnahmen erzeugen")
    );
    mockedGetAdminProcessTypes.mockResolvedValueOnce([
      {
        id: 10,
        key: "role_change",
        name: "Rollenwechsel",
        description: "Koordinierte Anpassung einer Mitarbeiterrolle.",
        requiresSupervisorStep: false,
        approvalTaskTemplateKey: null,
        requiresTargetPerson: true,
        iconKey: null,
        isActive: true,
        sortOrder: 4,
        workflowCount: 0,
        answerDefinitionCount: 4,
        taskTemplateCount: 2,
        canActivate: true,
        activationBlockedReason: null,
      },
    ]);
    mockedGetAdminTaskTemplates.mockResolvedValueOnce([
      {
        id: 402,
        processTypeId: 10,
        templateKey: "rc_role_assignment_update",
        title: "Rollen-Zuweisung aktualisieren",
        category: "Berechtigungen",
        description: "Fachliche und technische Rollen auf die neue Rolle umstellen.",
        iconKey: null,
        owningDepartmentId: null,
        defaultResponsibilityId: 56,
        processAreaLabel: null,
        isDepartmentPhaseTask: true,
        isRequired: true,
        dueInDays: 2,
        sortOrder: 1,
        isActive: true,
        createdAt: "2026-04-08T10:00:00Z",
        conditionCount: 0,
        dependencyCount: 0,
      },
    ]);

    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_admin"] }
    );

    expect(await screen.findByText(/rollenbezogene Rollen-, Berechtigungs- und Systemanpassungen/i)).toBeTruthy();
    fireEvent.click((await screen.findAllByRole("button", { name: "Änderungsmaßnahmen erzeugen" }))[0]!);
    expect(await screen.findByText(/rollenbezogene Rollen-, Berechtigungs- und Systemanpassungen/i)).toBeTruthy();
  });

  it("shows the builder in limited mode without loading the action catalog", async () => {
    mockedGetOrCreateAdminWorkflowDefinitionWorkingDraft.mockResolvedValueOnce(createRichVersionDetail());

    renderWithApp(
      <AdminWorkflowBuilderSection onNotice={() => undefined} onError={() => undefined} />,
      { roleKeys: ["auth_manager"] }
    );

    await waitFor(() => {
      expect(screen.getByTestId("mock-react-flow").textContent).toContain("nodes:9 edges:9");
    });

    expect(mockedGetAdminWorkflowActionDefinitions).not.toHaveBeenCalled();
    expect(screen.queryByRole("button", { name: "Ablauf anlegen" })).toBeNull();
    expect((screen.getByRole("button", { name: "Freigeben" }) as HTMLButtonElement).disabled).toBe(true);

    fireEvent.click((await screen.findAllByRole("button", { name: "Konten erstellen" }))[0]!);
    expect(await screen.findByText(/nur im Admin-Modus editierbar/i)).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Create AD User hinzufügen" })).toBeNull();
  });
});
