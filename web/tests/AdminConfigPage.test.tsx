import { fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import AdminConfigPage from "../src/pages/AdminConfigPage";
import * as adminApi from "../src/services/adminApi";
import { createAdminDepartmentAssignment, createAdminUser, renderWithApp } from "./testUtils";
import type {
  AdminGraphApplicationConfiguration,
  AdminGroup,
  AdminNotificationEmailConfiguration,
  AdminResponsibilityOwner,
  AdminRole,
} from "../src/types/auth";
import type { WorkflowConfig } from "../src/types/workflow";

vi.mock("../src/services/adminApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/adminApi")>(
    "../src/services/adminApi"
  );

  return {
    ...actual,
    createAdminUser: vi.fn(),
    getAdminDepartmentAssignments: vi.fn(),
    getAdminGroups: vi.fn(),
    getAdminGraphApplicationConfiguration: vi.fn(),
    getAdminNotificationEmailConfiguration: vi.fn(),
    getAdminPermissionAudit: vi.fn(),
    getAdminPermissions: vi.fn(),
    getAdminResponsibilityOwners: vi.fn(),
    getAdminRoles: vi.fn(),
    getAdminUsers: vi.fn(),
    getAdminWorkflowConfig: vi.fn(),
  };
});

const mockedCreateAdminUser = vi.mocked(adminApi.createAdminUser);
const mockedGetAdminDepartmentAssignments = vi.mocked(adminApi.getAdminDepartmentAssignments);
const mockedGetAdminGroups = vi.mocked(adminApi.getAdminGroups);
const mockedGetAdminGraphApplicationConfiguration = vi.mocked(adminApi.getAdminGraphApplicationConfiguration);
const mockedGetAdminNotificationEmailConfiguration = vi.mocked(
  adminApi.getAdminNotificationEmailConfiguration
);
const mockedGetAdminPermissionAudit = vi.mocked(adminApi.getAdminPermissionAudit);
const mockedGetAdminPermissions = vi.mocked(adminApi.getAdminPermissions);
const mockedGetAdminResponsibilityOwners = vi.mocked(adminApi.getAdminResponsibilityOwners);
const mockedGetAdminRoles = vi.mocked(adminApi.getAdminRoles);
const mockedGetAdminUsers = vi.mocked(adminApi.getAdminUsers);
const mockedGetAdminWorkflowConfig = vi.mocked(adminApi.getAdminWorkflowConfig);

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

function createWorkflowConfig(): WorkflowConfig {
  return {
    requirements: [
      {
        id: 1,
        key: "ad_user",
        title: "AD-Benutzer",
        description: "Active Directory Benutzer anlegen",
        category: "accounts",
        iconKey: "user",
        inputType: "boolean",
        isRecommended: true,
        isDefault: false,
        isRequired: true,
        sortOrder: 1,
        defaultValueBoolean: null,
        defaultValueText: null,
        defaultValueNumber: null,
        defaultSelectedOptionId: null,
        defaultSelectedOptionIds: [],
        behavior: {
          visibilityDependencies: [],
          validation: null,
          resetTargetsWhenNotTrue: [],
          singleSelectReset: null,
        },
        options: [],
      },
    ],
    roleRecommendations: {
      recommendedRequirementIds: [1],
      defaultValues: [],
      defaultSelectedOptions: [],
    },
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
  mockedGetAdminDepartmentAssignments.mockResolvedValue([createAdminDepartmentAssignment()]);
  mockedGetAdminResponsibilityOwners.mockResolvedValue([createResponsibility()]);
  mockedGetAdminGraphApplicationConfiguration.mockResolvedValue(createGraphConfiguration());
  mockedGetAdminNotificationEmailConfiguration.mockResolvedValue(createNotificationConfiguration());
  mockedGetAdminPermissionAudit.mockResolvedValue([]);
  mockedGetAdminPermissions.mockResolvedValue([]);
  mockedGetAdminWorkflowConfig.mockResolvedValue(createWorkflowConfig());
  mockedGetAdminRoles.mockResolvedValue([createRole()]);
  mockedGetAdminGroups.mockResolvedValue([createGroup()]);
}

describe("AdminConfigPage", () => {
  beforeEach(() => {
    mockedCreateAdminUser.mockReset();
    mockedGetAdminUsers.mockReset();
    mockedGetAdminDepartmentAssignments.mockReset();
    mockedGetAdminResponsibilityOwners.mockReset();
    mockedGetAdminGraphApplicationConfiguration.mockReset();
    mockedGetAdminNotificationEmailConfiguration.mockReset();
    mockedGetAdminPermissionAudit.mockReset();
    mockedGetAdminPermissions.mockReset();
    mockedGetAdminWorkflowConfig.mockReset();
    mockedGetAdminRoles.mockReset();
    mockedGetAdminGroups.mockReset();
    mockSuccessfulLoad();
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
    expect(screen.getByRole("button", { name: "Personen" })).toBeTruthy();
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

  it("renders mail configuration and workflow configuration separately in the system section", async () => {
    renderWithApp(<AdminConfigPage />, {
      roleKeys: ["auth_admin"],
      route: "/admin/config?section=system",
    });

    expect(await screen.findByText("Konfiguration: Mailversand")).toBeTruthy();
    expect(screen.getByText("Workflow-Konfiguration")).toBeTruthy();
  });

  it("loads roles and groups only when the access section is opened", async () => {
    renderWithApp(<AdminConfigPage />, {
      roleKeys: ["auth_admin"],
      route: "/admin/config?section=access",
    });

    expect(await screen.findByRole("heading", { name: "Zugriffe & Gruppen" })).toBeTruthy();

    await waitFor(() => {
      expect(mockedGetAdminRoles).toHaveBeenCalled();
      expect(mockedGetAdminGroups).toHaveBeenCalled();
    });
  });

  it("allows selecting a user directly inside the access section", async () => {
    renderWithApp(<AdminConfigPage />, {
      roleKeys: ["auth_admin"],
      route: "/admin/config?section=access",
    });

    expect(await screen.findByRole("heading", { name: "Zugriffe & Gruppen" })).toBeTruthy();

    fireEvent.change(screen.getByRole("combobox", { name: "Person" }), {
      target: { value: "2" },
    });

    expect(await screen.findByText("Direkte Rollen: Mia Manager")).toBeTruthy();
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

    expect(await screen.findByText("Neue Person")).toBeTruthy();

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
