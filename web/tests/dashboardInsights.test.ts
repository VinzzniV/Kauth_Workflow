import { beforeEach, describe, expect, it, vi } from "vitest";
import { loadDashboardInsights } from "../src/components/dashboard/dashboardInsights";
import * as adminApi from "../src/services/adminApi";
import * as workflowApi from "../src/services/workflowApi";
import { createAdminDepartmentAssignment, createWorkflowSummary } from "./testUtils";

vi.mock("../src/services/workflowApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/workflowApi")>("../src/services/workflowApi");
  return {
    ...actual,
    getWorkflows: vi.fn(),
    getSupervisorStepWorkflows: vi.fn(),
  };
});

vi.mock("../src/services/adminApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/adminApi")>("../src/services/adminApi");
  return {
    ...actual,
    getAdminUsers: vi.fn(),
    getAdminDepartmentAssignments: vi.fn(),
    getAdminResponsibilityOwners: vi.fn(),
    getAdminNotificationEmailConfiguration: vi.fn(),
  };
});

const mockedGetWorkflows = vi.mocked(workflowApi.getWorkflows);
const mockedGetSupervisorStepWorkflows = vi.mocked(workflowApi.getSupervisorStepWorkflows);
const mockedGetAdminUsers = vi.mocked(adminApi.getAdminUsers);
const mockedGetAdminDepartmentAssignments = vi.mocked(adminApi.getAdminDepartmentAssignments);
const mockedGetAdminResponsibilityOwners = vi.mocked(adminApi.getAdminResponsibilityOwners);
const mockedGetAdminNotificationEmailConfiguration = vi.mocked(adminApi.getAdminNotificationEmailConfiguration);

describe("dashboardInsights", () => {
  beforeEach(() => {
    mockedGetWorkflows.mockReset();
    mockedGetSupervisorStepWorkflows.mockReset();
    mockedGetAdminUsers.mockReset();
    mockedGetAdminDepartmentAssignments.mockReset();
    mockedGetAdminResponsibilityOwners.mockReset();
    mockedGetAdminNotificationEmailConfiguration.mockReset();
  });

  it("summarizes manager insights from the visible workflow list", async () => {
    mockedGetSupervisorStepWorkflows.mockResolvedValue([
      createWorkflowSummary({
        uid: "wf-1",
        workflowStatus: "waiting_for_supervisor",
        requirementSummary: {
          totalCount: 2,
          visibleCount: 2,
          answeredVisibleCount: 2,
          pendingVisibleCount: 0,
        },
      }),
      createWorkflowSummary({
        uid: "wf-2",
        workflowStatus: "waiting_for_supervisor",
        requirementSummary: {
          totalCount: 3,
          visibleCount: 3,
          answeredVisibleCount: 1,
          pendingVisibleCount: 2,
        },
      }),
    ]);
    mockedGetWorkflows.mockResolvedValue([
      createWorkflowSummary({
        uid: "wf-1",
        workflowStatus: "waiting_for_supervisor",
        requirementSummary: {
          totalCount: 2,
          visibleCount: 2,
          answeredVisibleCount: 2,
          pendingVisibleCount: 0,
        },
      }),
      createWorkflowSummary({
        uid: "wf-2",
        workflowStatus: "waiting_for_supervisor",
        requirementSummary: {
          totalCount: 3,
          visibleCount: 3,
          answeredVisibleCount: 1,
          pendingVisibleCount: 2,
        },
      }),
    ]);

    const insights = await loadDashboardInsights("manager");

    expect(insights.stats[0]?.value).toBe(2);
    expect(insights.stats[1]?.value).toBe(2);
    expect(insights.queueItems).toHaveLength(1);
    expect(insights.queueItems[0]?.title).toContain("2 Vorgänge warten auf Ihre Rückmeldung");
    expect(insights.employeeItems).toHaveLength(2);
    expect(mockedGetSupervisorStepWorkflows).toHaveBeenCalledTimes(1);
  });

  it("filters HR insights by the selected process type", async () => {
    mockedGetWorkflows.mockResolvedValue([
      createWorkflowSummary({
        uid: "wf-onboarding",
        workflowStatus: "waiting_for_department",
        processType: { key: "onboarding", name: "Onboarding" },
      }),
    ]);

    const insights = await loadDashboardInsights("hr", {
      workflowDefinitionKey: "onboarding",
      selectedWorkflowDefinition: { definitionKey: "onboarding", name: "Onboarding", requiresTargetPerson: false, primaryLegacyProcessTypeKey: "onboarding", latestPublishedVersionNumber: 1 },
    });

    expect(mockedGetWorkflows).toHaveBeenCalledWith(expect.objectContaining({ workflowDefinitionKey: "onboarding" }));
    expect(insights.stats[0]?.label).toBe("Wartet auf Freigabe / Fachbereich");
    expect(insights.queueTitle).toBe("Vorgänge (Onboarding)");
    expect(insights.nextStep).toBe("Engpässe bei Abteilungsleitung und Fachbereichen zuerst entlasten.");
  });

  it("filters manager insights by process type using the visible workflow list", async () => {
    mockedGetSupervisorStepWorkflows.mockResolvedValue([
      createWorkflowSummary({
        uid: "wf-2",
        processType: { key: "offboarding", name: "Offboarding" },
        workflowStatus: "waiting_for_supervisor",
        requirementSummary: {
          totalCount: 3,
          visibleCount: 3,
          answeredVisibleCount: 1,
          pendingVisibleCount: 2,
        },
      }),
      createWorkflowSummary({
        uid: "wf-ignored",
        processType: { key: "onboarding", name: "Onboarding" },
        workflowStatus: "waiting_for_supervisor",
        requirementSummary: {
          totalCount: 1,
          visibleCount: 1,
          answeredVisibleCount: 1,
          pendingVisibleCount: 0,
        },
      }),
    ]);
    mockedGetWorkflows.mockResolvedValue([
      createWorkflowSummary({
        uid: "wf-2",
        processType: { key: "offboarding", name: "Offboarding" },
        workflowStatus: "waiting_for_supervisor",
        requirementSummary: {
          totalCount: 3,
          visibleCount: 3,
          answeredVisibleCount: 1,
          pendingVisibleCount: 2,
        },
      }),
    ]);

    const insights = await loadDashboardInsights("manager", {
      workflowDefinitionKey: "offboarding",
      selectedWorkflowDefinition: { definitionKey: "offboarding", name: "Offboarding", requiresTargetPerson: true, primaryLegacyProcessTypeKey: "offboarding", latestPublishedVersionNumber: 1 },
    });

    expect(insights.stats[0]?.value).toBe(1); // waitingForSupervisor (offboarding only)
    expect(insights.stats[1]?.value).toBe(2); // pendingSelections from wf-2
    expect(insights.queueItems).toHaveLength(1);
    expect(insights.queueTitle).toBe("Mitarbeitende (Offboarding)");
    expect(insights.employeeItems).toHaveLength(1);
    expect(mockedGetWorkflows).toHaveBeenCalledWith(expect.objectContaining({ workflowDefinitionKey: "offboarding" }));
    expect(mockedGetSupervisorStepWorkflows).toHaveBeenCalledTimes(1);
  });

  it("uses the supervisor-step endpoint for action metrics while keeping the visible workflow overview", async () => {
    mockedGetWorkflows.mockResolvedValue([
      createWorkflowSummary({
        uid: "wf-assigned",
        workflowStatus: "waiting_for_supervisor",
        requirementSummary: {
          totalCount: 3,
          visibleCount: 3,
          answeredVisibleCount: 2,
          pendingVisibleCount: 1,
        },
      }),
      createWorkflowSummary({
        uid: "wf-not-assigned",
        workflowStatus: "waiting_for_supervisor",
        requirementSummary: {
          totalCount: 4,
          visibleCount: 4,
          answeredVisibleCount: 1,
          pendingVisibleCount: 3,
        },
      }),
    ]);
    mockedGetSupervisorStepWorkflows.mockResolvedValue([
      createWorkflowSummary({
        uid: "wf-assigned",
        workflowStatus: "waiting_for_supervisor",
        requirementSummary: {
          totalCount: 3,
          visibleCount: 3,
          answeredVisibleCount: 2,
          pendingVisibleCount: 1,
        },
      }),
    ]);

    const insights = await loadDashboardInsights("manager");

    expect(insights.stats[0]?.value).toBe(1); // waitingForSupervisor
    expect(insights.stats[1]?.value).toBe(1); // pendingSelections from supervisor-step
    expect(insights.stats[3]?.value).toBe(2); // activeWorkflows (now at index 3 after deadline insert)
    expect(insights.queueItems).toHaveLength(1);
    expect(insights.queueItems[0]?.title).toContain("1 Vorgänge warten auf Ihre Rückmeldung");
    expect(insights.employeeItems).toHaveLength(2);
  });

  it("structures admin insights into governance warnings, operations and summary cards", async () => {
    mockedGetWorkflows.mockResolvedValue([
      createWorkflowSummary({
        uid: "wf-stuck",
        createdAt: "2026-04-20T10:00:00.000Z",
        workflowStatus: "waiting_for_department",
      }),
      createWorkflowSummary({
        uid: "wf-bottleneck",
        createdAt: "2026-05-05T10:00:00.000Z",
        workflowStatus: "waiting_for_supervisor",
      }),
    ]);
    mockedGetAdminUsers.mockResolvedValue([]);
    mockedGetAdminDepartmentAssignments.mockResolvedValue({
      items: [
        createAdminDepartmentAssignment({
          departmentId: 1,
          departmentName: "IT",
          departmentLeadUserId: null,
          requirementOwnerUserId: null,
        }),
      ],
      totalCount: 1,
      page: 1,
      pageSize: 200,
      totalPages: 1,
    });
    mockedGetAdminResponsibilityOwners.mockResolvedValue({
      items: [
        {
          responsibilityId: 10,
          responsibilityKey: "it-access",
          systemKey: null,
          responsibilityName: "IT-Zugriffe",
          responsibilityType: "process",
          departmentId: null,
          departmentName: null,
          appUserId: null,
          appUserDisplayName: null,
          updatedAt: "2026-05-01T08:00:00.000Z",
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 200,
      totalPages: 1,
    });
    mockedGetAdminNotificationEmailConfiguration.mockResolvedValue({
      enabled: false,
      mode: "disabled",
      senderEmail: null,
      frontendBaseUrl: "http://localhost:5173",
      testRecipientEmail: null,
      sandboxRedirectEmail: null,
      notifyOnWorkflowCreated: false,
      notifyOnTaskReady: false,
      notifyOnWorkflowCompleted: false,
      lastTestStatus: "disabled",
      lastTestAt: null,
      lastError: null,
      updatedAt: null,
      hasClientSecret: false,
      configurationStatus: "incomplete",
      configurationMessage: "Mail-Konfiguration unvollständig",
    });

    const insights = await loadDashboardInsights("admin");

    expect(insights.adminSummary?.stats).toHaveLength(4);
    expect(insights.adminSummary?.statusTitle).toContain("Governance");
    expect(insights.adminWarnings).toHaveLength(3);
    expect(insights.adminWarnings?.[0]?.title).toBe("Stammdaten-Lücken");
    expect(insights.adminWarnings?.[0]?.groups[0]?.title).toBe("Abteilungen ohne gültige Leitung");
    expect(insights.adminOperations?.[0]?.key).toBe("stuck-wf-stuck");
    expect(insights.adminOperations?.some((item) => item.key === "workflow-bottlenecks")).toBe(true);
    expect(insights.queueItems.some((item) => item.key.startsWith("admin-warning-"))).toBe(false);
  });
});
