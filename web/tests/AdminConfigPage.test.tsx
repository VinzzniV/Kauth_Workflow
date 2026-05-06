import { fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { Route, Routes } from "react-router-dom";
import type { ReactNode } from "react";
import AdminConfigPage from "../src/pages/AdminConfigPage";
import * as adminApi from "../src/services/adminApi";
import * as adminConfigApi from "../src/services/adminConfigApi";
import { createAdminDepartmentAssignment, createAdminUser, renderWithApp } from "./testUtils";
import type {
  AdminGraphApplicationConfiguration,
  AdminGroup,
  AdminNotificationEmailConfiguration,
  AdminResponsibilityOwner,
  AdminRole,
} from "../src/types/auth";

vi.mock("@xyflow/react", () => ({
  Background: () => null,
  Controls: () => null,
  MiniMap: () => null,
  Handle: () => null,
  Position: { Left: "left", Right: "right" },
  MarkerType: { ArrowClosed: "arrowclosed" },
  ReactFlow: ({ children }: { children?: ReactNode }) => <div>{children}</div>,
}));

vi.mock("../src/services/adminApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/adminApi")>(
    "../src/services/adminApi"
  );

  return {
    ...actual,
    createAdminUser: vi.fn(),
    getAdminDepartmentAssignments: vi.fn(),
    getAdminDepartmentPositions: vi.fn(),
    getAdminGroups: vi.fn(),
    getAdminGraphApplicationConfiguration: vi.fn(),
    getAdminNotificationEmailConfiguration: vi.fn(),
    getAdminNotificationTemplates: vi.fn(),
    getAdminPermissionAudit: vi.fn(),
    getAdminPermissions: vi.fn(),
    getAdminResponsibilityOwners: vi.fn(),
    getAdminRoles: vi.fn(),
    getAdminUsers: vi.fn(),
    previewAdminNotificationTemplate: vi.fn(),
    searchAdminNotificationTemplateRotationPlans: vi.fn(),
    searchAdminNotificationTemplateWorkflows: vi.fn(),
    updateAdminNotificationTemplate: vi.fn(),
  };
});

vi.mock("../src/services/adminConfigApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/adminConfigApi")>(
    "../src/services/adminConfigApi"
  );

  return {
    ...actual,
    createAdminWorkflowDefinition: vi.fn(),
    createAdminWorkflowDefinitionVersion: vi.fn(),
    getAdminWorkflowActionDefinitions: vi.fn(),
    getAdminWorkflowDefinitionVersion: vi.fn(),
    getAdminWorkflowDefinitions: vi.fn(),
    publishAdminWorkflowDefinitionVersion: vi.fn(),
    replaceAdminWorkflowDefinitionVersion: vi.fn(),
    updateAdminWorkflowDefinition: vi.fn(),
  };
});

const mockedCreateAdminUser = vi.mocked(adminApi.createAdminUser);
const mockedGetAdminWorkflowActionDefinitions = vi.mocked(adminConfigApi.getAdminWorkflowActionDefinitions);
const mockedGetAdminWorkflowDefinitionVersion = vi.mocked(adminConfigApi.getAdminWorkflowDefinitionVersion);
const mockedGetAdminWorkflowDefinitions = vi.mocked(adminConfigApi.getAdminWorkflowDefinitions);
const mockedGetAdminDepartmentAssignments = vi.mocked(adminApi.getAdminDepartmentAssignments);
const mockedGetAdminDepartmentPositions = vi.mocked(adminApi.getAdminDepartmentPositions);
const mockedGetAdminGroups = vi.mocked(adminApi.getAdminGroups);
const mockedGetAdminGraphApplicationConfiguration = vi.mocked(adminApi.getAdminGraphApplicationConfiguration);
const mockedGetAdminNotificationEmailConfiguration = vi.mocked(
  adminApi.getAdminNotificationEmailConfiguration
);
const mockedGetAdminNotificationTemplates = vi.mocked(adminApi.getAdminNotificationTemplates);
const mockedGetAdminPermissionAudit = vi.mocked(adminApi.getAdminPermissionAudit);
const mockedGetAdminPermissions = vi.mocked(adminApi.getAdminPermissions);
const mockedGetAdminResponsibilityOwners = vi.mocked(adminApi.getAdminResponsibilityOwners);
const mockedGetAdminRoles = vi.mocked(adminApi.getAdminRoles);
const mockedGetAdminUsers = vi.mocked(adminApi.getAdminUsers);
const mockedPreviewAdminNotificationTemplate = vi.mocked(adminApi.previewAdminNotificationTemplate);
const mockedSearchAdminNotificationTemplateRotationPlans = vi.mocked(adminApi.searchAdminNotificationTemplateRotationPlans);
const mockedSearchAdminNotificationTemplateWorkflows = vi.mocked(adminApi.searchAdminNotificationTemplateWorkflows);

function createResponsibility(
  overrides: Partial<AdminResponsibilityOwner> = {}
): AdminResponsibilityOwner {
  return {
    responsibilityId: 10,
    responsibilityKey: "sap",
    systemKey: "SAP",
    responsibilityName: "SAP Betreuung",
    responsibilityType: "application",
    departmentId: 1,
    departmentName: "IT",
    appUserId: 1,
    appUserDisplayName: "Lea Lead",
    updatedAt: "2026-03-24T08:00:00.000Z",
    ...overrides,
  };
}

function createRole(overrides: Partial<AdminRole> = {}): AdminRole {
  return {
    roleId: 100,
    roleKey: "auth_manager",
    roleName: "Abteilungsleitung",
    roleKind: "system",
    departmentId: null,
    departmentName: null,
    scope: "global",
    scopeDepartmentId: null,
    scopeDepartmentName: null,
    isActive: true,
    permissions: [],
    ...overrides,
  };
}

function createGroup(overrides: Partial<AdminGroup> = {}): AdminGroup {
  return {
    groupId: 200,
    groupKey: "it_core",
    groupName: "IT Core",
    description: "IT Gruppe",
    isActive: true,
    roles: [createRole()],
    ...overrides,
  };
}

function createNotificationConfiguration(
  overrides: Partial<AdminNotificationEmailConfiguration> = {}
): AdminNotificationEmailConfiguration {
  return {
    enabled: false,
    mode: "disabled",
    senderEmail: null,
    frontendBaseUrl: "http://localhost:5173",
    testRecipientEmail: null,
    sandboxRedirectEmail: "sandbox@demo.local",
    notifyOnWorkflowCreated: true,
    notifyOnTaskReady: true,
    notifyOnWorkflowCompleted: false,
    lastTestStatus: "never",
    lastTestAt: null,
    lastError: null,
    updatedAt: "2026-03-24T08:00:00.000Z",
    hasClientSecret: false,
    configurationStatus: "disabled",
    configurationMessage: null,
    ...overrides,
  };
}

function createGraphConfiguration(
  overrides: Partial<AdminGraphApplicationConfiguration> = {}
): AdminGraphApplicationConfiguration {
  return {
    tenantId: "tenant-1",
    clientId: "client-1",
    hasClientSecret: true,
    updatedAt: null,
    configurationSource: "runtime",
    configurationStatus: "ready",
    configurationMessage: null,
    ...overrides,
  };
}

function createNotificationTemplate(overrides: Partial<import("../src/types/auth").AdminNotificationTemplate> = {}) {
  return {
    templateKey: "workflow_created",
    displayName: "Vorgang gestartet",
    triggerDescription: "Startet bei neuem Vorgang.",
    subjectTemplate: "{{workflow_label}} gestartet",
    bodyTemplate: "Ein neuer {{workflow_label}} wurde gestartet.",
    isSystemLocked: false,
    updatedAt: "2026-04-22T08:00:00.000Z",
    previewTargetType: "workflow" as const,
    placeholders: [
      { key: "workflow_label", label: "Workflow-Label", description: "Lesbarer Vorgangsname" },
      { key: "recipient_name", label: "Empfänger", description: "Anzeigename" },
      { key: "workflow_url", label: "Link", description: "Direkter Link" },
    ],
    ...overrides,
  };
}

function mockSuccessfulLoad() {
  mockedGetAdminUsers.mockResolvedValue([
    createAdminUser(),
    createAdminUser({
      userId: 2,
      externalKey: "mia.manager",
      displayName: "Mia Manager",
      email: "mia.manager@demo.local",
    }),
  ]);
  mockedGetAdminDepartmentAssignments.mockResolvedValue({
    items: [createAdminDepartmentAssignment()],
    total: 1,
    limit: 200,
    offset: 0,
  });
  mockedGetAdminDepartmentPositions.mockResolvedValue({
    items: [],
    total: 0,
    limit: 200,
    offset: 0,
  });
  mockedGetAdminResponsibilityOwners.mockResolvedValue({
    items: [createResponsibility()],
    total: 1,
    limit: 200,
    offset: 0,
  });
  mockedGetAdminGraphApplicationConfiguration.mockResolvedValue(createGraphConfiguration());
  mockedGetAdminNotificationEmailConfiguration.mockResolvedValue(createNotificationConfiguration());
  mockedGetAdminNotificationTemplates.mockResolvedValue([createNotificationTemplate()]);
  mockedGetAdminPermissionAudit.mockResolvedValue({
    items: [],
    nextCursor: null,
    hasMore: false,
  });
  mockedGetAdminPermissions.mockResolvedValue([]);
  mockedGetAdminRoles.mockResolvedValue([createRole()]);
  mockedGetAdminGroups.mockResolvedValue([createGroup()]);
  mockedSearchAdminNotificationTemplateWorkflows.mockResolvedValue([
    {
      workflowUid: "11111111-1111-1111-1111-111111111111",
      displayName: "Lea Lead",
      processName: "Onboarding",
      departmentName: "IT",
      workflowStatus: "draft",
      createdAt: "2026-04-22T08:00:00.000Z",
    },
  ]);
  mockedSearchAdminNotificationTemplateRotationPlans.mockResolvedValue([]);
  mockedPreviewAdminNotificationTemplate.mockResolvedValue({
    templateKey: "workflow_created",
    displayName: "Vorgang gestartet",
    triggerDescription: "Startet bei neuem Vorgang.",
    previewTargetType: "workflow",
    isCurrentlyTriggerable: true,
    blockingReason: null,
    target: {
      targetType: "workflow",
      workflowUid: "11111111-1111-1111-1111-111111111111",
      rotationPlanId: null,
      primaryLabel: "Lea Lead",
      secondaryLabel: "Onboarding | IT",
      status: "draft",
    },
    variants: [
      {
        recipient: {
          recipientUserId: 1,
          name: "Lea Lead",
          email: "lea.lead@demo.local",
        },
        renderedSubject: "Onboarding-Workflow gestartet",
        renderedTextBody: "Hallo Lea Lead,\n\nEin neuer Onboarding-Workflow wurde gestartet.",
        renderedHtmlBody: "<p>Hallo Lea Lead</p>",
        placeholderValues: [
          { key: "workflow_label", value: "Onboarding-Workflow" },
        ],
      },
    ],
  });
}

describe("AdminConfigPage", () => {
  beforeEach(() => {
    mockedCreateAdminUser.mockReset();
    mockedGetAdminWorkflowActionDefinitions.mockReset();
    mockedGetAdminWorkflowDefinitionVersion.mockReset();
    mockedGetAdminWorkflowDefinitions.mockReset();
    mockedGetAdminUsers.mockReset();
    mockedGetAdminDepartmentAssignments.mockReset();
    mockedGetAdminDepartmentPositions.mockReset();
    mockedGetAdminResponsibilityOwners.mockReset();
    mockedGetAdminGraphApplicationConfiguration.mockReset();
    mockedGetAdminNotificationEmailConfiguration.mockReset();
    mockedGetAdminNotificationTemplates.mockReset();
    mockedGetAdminPermissionAudit.mockReset();
    mockedGetAdminPermissions.mockReset();
    mockedGetAdminRoles.mockReset();
    mockedGetAdminGroups.mockReset();
    mockedPreviewAdminNotificationTemplate.mockReset();
    mockedSearchAdminNotificationTemplateRotationPlans.mockReset();
    mockedSearchAdminNotificationTemplateWorkflows.mockReset();
    mockSuccessfulLoad();
    mockedGetAdminWorkflowDefinitions.mockResolvedValue([
      {
        id: 1,
        key: "onboarding",
        name: "Onboarding",
        description: "Definition",
        versions: [
          {
            id: 10,
            workflowDefinitionId: 1,
            versionNumber: 1,
            status: "draft",
            name: "Draft 1",
            description: "Initial draft",
            primaryLegacyProcessTypeKey: "onboarding",
            createdAt: "2026-04-08T10:00:00Z",
            updatedAt: "2026-04-08T10:00:00Z",
            publishedAt: null,
            canPublish: true,
            validationIssues: [],
          },
        ],
      },
    ]);
    mockedGetAdminWorkflowActionDefinitions.mockResolvedValue([]);
    mockedGetAdminWorkflowDefinitionVersion.mockResolvedValue({
      id: 10,
      workflowDefinitionId: 1,
      definitionKey: "onboarding",
      definitionName: "Onboarding",
      definitionDescription: "Definition",
      versionNumber: 1,
      status: "draft",
      name: "Draft 1",
      description: "Initial draft",
      primaryLegacyProcessTypeKey: "onboarding",
      createdAt: "2026-04-08T10:00:00Z",
      updatedAt: "2026-04-08T10:00:00Z",
      publishedAt: null,
      canPublish: true,
      validationIssues: [],
      nodes: [
        { nodeKey: "start", nodeType: "start", title: "Start", sortOrder: 1, positionX: 80, positionY: 60, config: null, actions: [] },
        { nodeKey: "end", nodeType: "end", title: "Ende", sortOrder: 2, positionX: 420, positionY: 60, config: null, actions: [] },
      ],
      edges: [{ sourceNodeKey: "start", targetNodeKey: "end", priority: 1, conditionExpression: null }],
    });
    mockedCreateAdminUser.mockResolvedValue(
      createAdminUser({
        userId: 3,
        externalKey: "neue.person",
        displayName: "Neue Person",
        email: "neue.person@demo.local",
      })
    );
  });

  it("starts in the overview by default", async () => {
    renderWithApp(<AdminConfigPage />, {
      roleKeys: ["auth_admin"],
      route: "/admin/config",
    });

    expect(await screen.findByText("Arbeitsbereiche")).toBeTruthy();
    expect(screen.getByRole("button", { name: /Personen & Zugriff/i })).toBeTruthy();
    expect(mockedGetAdminRoles).not.toHaveBeenCalled();
    expect(mockedGetAdminGroups).not.toHaveBeenCalled();
  });

  it("opens the department workspace from query parameters", async () => {
    renderWithApp(<AdminConfigPage />, {
      roleKeys: ["auth_admin"],
      route: "/admin/config?section=organization&entity=department&id=1",
    });

    expect(await screen.findByText("Abteilung pflegen: IT")).toBeTruthy();
    expect(screen.getByText(/Leitung und Anforderungsverantwortung/)).toBeTruthy();
  });

  it("does not show responsibilities inside the organization workspace anymore", async () => {
    renderWithApp(<AdminConfigPage />, {
      roleKeys: ["auth_admin"],
      route: "/admin/config?section=organization&entity=responsibility&id=10",
    });

    // Legacy responsibility entity should redirect to the personen list, not surface responsibility editing.
    expect(await screen.findByRole("button", { name: "Neue Person" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Fachbereiche / Zuständigkeiten" })).toBeNull();
    expect(screen.queryByText("Feste Zuständigkeiten")).toBeNull();
  });

  it("renders only graph and mail configuration in the system section", async () => {
    renderWithApp(<AdminConfigPage />, {
      roleKeys: ["auth_admin"],
      route: "/admin/config?section=system",
    });

    expect(await screen.findByText("Konfiguration: Graph-Anwendung")).toBeTruthy();
    expect(await screen.findByText("Konfiguration: Mailversand")).toBeTruthy();
    expect(screen.queryByText("Prozesstypen")).toBeNull();
    expect(screen.queryByText("Workflow-Konfiguration")).toBeNull();
  });

  it("renders the mail template workspace with preview controls", async () => {
    renderWithApp(<AdminConfigPage />, {
      roleKeys: ["auth_admin"],
      route: "/admin/config?section=system_mail_templates",
    });

    expect(await screen.findByText("Konfiguration: Mail-Vorlagen")).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Vorgang gestartet" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Preview laden" })).toBeTruthy();
  });

  it("loads roles and groups only when the access section is opened", async () => {
    renderWithApp(<AdminConfigPage />, {
      roleKeys: ["auth_admin"],
      route: "/admin/config?section=access",
    });

    expect(await screen.findByRole("heading", { name: "Direkte Rollen und Gruppen" })).toBeTruthy();

    await waitFor(() => {
      expect(mockedGetAdminRoles).toHaveBeenCalled();
      expect(mockedGetAdminGroups).toHaveBeenCalled();
    });
  });

  it("redirects legacy builder sections to the standalone builder page", async () => {
    renderWithApp(
      <Routes>
        <Route path="/admin/config" element={<AdminConfigPage />} />
        <Route path="/builder" element={<div>Builder Route</div>} />
      </Routes>,
      {
      roleKeys: ["auth_admin"],
      route: "/admin/config?section=templates",
      }
    );

    expect(await screen.findByText("Builder Route")).toBeTruthy();
  });

  it("allows selecting a user directly inside the access section", async () => {
    renderWithApp(<AdminConfigPage />, {
      roleKeys: ["auth_admin"],
      route: "/admin/config?section=access",
    });

    expect(await screen.findByRole("heading", { name: "Direkte Rollen und Gruppen" })).toBeTruthy();

    fireEvent.change(screen.getByRole("combobox", { name: "Person" }), {
      target: { value: "2" },
    });

    expect(await screen.findByText("Direkte Rollen für Mia Manager")).toBeTruthy();
    expect(screen.getByText("Gruppen für Mia Manager")).toBeTruthy();
  });

  it("navigates from a user relation to the linked department without leaving the page", async () => {
    renderWithApp(<AdminConfigPage />, {
      roleKeys: ["auth_admin"],
      route: "/admin/config?section=organization&entity=user&id=1",
    });

    expect(await screen.findByText("Person pflegen: Lea Lead")).toBeTruthy();

    fireEvent.click(screen.getAllByRole("button", { name: "IT" })[0]!);

    expect(await screen.findByText("Abteilung pflegen: IT")).toBeTruthy();
  });

  it("dispatches a demo-user refresh after creating a new user", async () => {
    const dispatchEventSpy = vi.spyOn(window, "dispatchEvent");

    renderWithApp(<AdminConfigPage />, {
      roleKeys: ["auth_admin"],
      route: "/admin/config?section=organization&entity=user",
    });

    // Open the create drawer via the toolbar button (replaces the always-visible new-person form).
    fireEvent.click(await screen.findByRole("button", { name: "Neue Person" }));

    fireEvent.change(screen.getByLabelText("Anzeigename"), {
      target: { value: "Neue Person" },
    });
    fireEvent.change(screen.getByLabelText("Login-E-Mail"), {
      target: { value: "neue.person@demo.local" },
    });
    fireEvent.change(screen.getByLabelText("Anmeldename"), {
      target: { value: "neue.person" },
    });

    fireEvent.click(screen.getByRole("button", { name: "Person anlegen" }));

    await waitFor(() => {
      expect(mockedCreateAdminUser).toHaveBeenCalled();
    });

    await waitFor(() => {
      expect(dispatchEventSpy).toHaveBeenCalledWith(expect.objectContaining({ type: "sim-users-refresh" }));
    });

    dispatchEventSpy.mockRestore();
  });
});
