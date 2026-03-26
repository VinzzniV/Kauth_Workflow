import { fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import AdminConfigPage from "../src/pages/AdminConfigPage";
import * as lifecycleApi from "../src/services/lifecycleApi";
import { renderWithApp } from "./testUtils";
import type {
  AdminDepartmentAssignment,
  AdminGroup,
  AdminNotificationEmailConfiguration,
  AdminResponsibilityOwner,
  AdminRole,
  AdminUser,
} from "../src/types/auth";
import type { WorkflowConfig } from "../src/types/workflow";

vi.mock("../src/services/lifecycleApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/lifecycleApi")>(
    "../src/services/lifecycleApi"
  );

  return {
    ...actual,
    createAdminUser: vi.fn(),
    getAdminDepartmentAssignments: vi.fn(),
    getAdminGroups: vi.fn(),
    getAdminNotificationEmailConfiguration: vi.fn(),
    getAdminResponsibilityOwners: vi.fn(),
    getAdminRoles: vi.fn(),
    getAdminUsers: vi.fn(),
    getAdminWorkflowConfig: vi.fn(),
  };
});

const mockedCreateAdminUser = vi.mocked(lifecycleApi.createAdminUser);
const mockedGetAdminDepartmentAssignments = vi.mocked(lifecycleApi.getAdminDepartmentAssignments);
const mockedGetAdminGroups = vi.mocked(lifecycleApi.getAdminGroups);
const mockedGetAdminNotificationEmailConfiguration = vi.mocked(
  lifecycleApi.getAdminNotificationEmailConfiguration
);
const mockedGetAdminResponsibilityOwners = vi.mocked(lifecycleApi.getAdminResponsibilityOwners);
const mockedGetAdminRoles = vi.mocked(lifecycleApi.getAdminRoles);
const mockedGetAdminUsers = vi.mocked(lifecycleApi.getAdminUsers);
const mockedGetAdminWorkflowConfig = vi.mocked(lifecycleApi.getAdminWorkflowConfig);

function createUser(overrides: Partial<AdminUser> = {}): AdminUser {
  return {
    userId: 1,
    externalKey: "lea.lead",
    displayName: "Lea Lead",
    email: "lea.lead@demo.local",
    notificationEmail: null,
    isActive: true,
    hasManagerAccess: true,
    departmentId: 1,
    departmentName: "IT",
    roles: [],
    groups: [],
    ...overrides,
  };
}

function createDepartment(overrides: Partial<AdminDepartmentAssignment> = {}): AdminDepartmentAssignment {
  return {
    departmentId: 1,
    departmentName: "IT",
    departmentLeadUserId: 1,
    departmentLeadDisplayName: "Lea Lead",
    requirementOwnerUserId: 2,
    requirementOwnerDisplayName: "Mia Manager",
    updatedAt: "2026-03-24T08:00:00.000Z",
    ...overrides,
  };
}

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
    isActive: true,
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
    tenantId: null,
    clientId: null,
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
    createUser(),
    createUser({
      userId: 2,
      externalKey: "mia.manager",
      displayName: "Mia Manager",
      email: "mia.manager@demo.local",
    }),
  ]);
  mockedGetAdminDepartmentAssignments.mockResolvedValue([createDepartment()]);
  mockedGetAdminResponsibilityOwners.mockResolvedValue([createResponsibility()]);
  mockedGetAdminNotificationEmailConfiguration.mockResolvedValue(createNotificationConfiguration());
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
    mockedGetAdminNotificationEmailConfiguration.mockReset();
    mockedGetAdminWorkflowConfig.mockReset();
    mockedGetAdminRoles.mockReset();
    mockedGetAdminGroups.mockReset();
    mockSuccessfulLoad();
    mockedCreateAdminUser.mockResolvedValue(
      createUser({
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

    expect(await screen.findByText("Admin-Übersicht")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Neue Person" })).toBeTruthy();
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

    expect(await screen.findByText("Benutzerrechte und Gruppen")).toBeTruthy();

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

    expect(await screen.findByText("Benutzerrechte und Gruppen")).toBeTruthy();

    fireEvent.change(screen.getByRole("combobox", { name: "Person" }), {
      target: { value: "2" },
    });

    expect(await screen.findByText("Rollen zuweisen: Mia Manager")).toBeTruthy();
    expect(screen.getByText("Gruppen zuweisen: Mia Manager")).toBeTruthy();
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
      expect(dispatchEventSpy).toHaveBeenCalledWith(expect.objectContaining({ type: "demo-users-refresh" }));
    });

    dispatchEventSpy.mockRestore();
  });
});
