import { describe, expect, it, vi, beforeEach } from "vitest";
import { loadDashboardInsights } from "../src/components/dashboard/dashboardInsights";
import * as lifecycleApi from "../src/services/lifecycleApi";
import { createWorkflowSummary } from "./testUtils";

vi.mock("../src/services/lifecycleApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/lifecycleApi")>("../src/services/lifecycleApi");
  return {
    ...actual,
    getAdminGroups: vi.fn(),
    getAdminRoles: vi.fn(),
    getAdminUsers: vi.fn(),
    getProcessTypes: vi.fn(),
    getMyTasks: vi.fn(),
    getSupervisorStepWorkflows: vi.fn(),
    getWorkflows: vi.fn(),
  };
});

const mockedGetProcessTypes = vi.mocked(lifecycleApi.getProcessTypes);
const mockedGetSupervisorStepWorkflows = vi.mocked(lifecycleApi.getSupervisorStepWorkflows);
const mockedGetWorkflows = vi.mocked(lifecycleApi.getWorkflows);

describe("dashboardInsights", () => {
  beforeEach(() => {
    mockedGetProcessTypes.mockReset();
    mockedGetSupervisorStepWorkflows.mockReset();
    mockedGetWorkflows.mockReset();
    mockedGetProcessTypes.mockResolvedValue([
      { key: "onboarding", name: "Onboarding" },
      { key: "offboarding", name: "Offboarding" },
    ]);
  });

  it("counts assigned supervisor workflows from the backend queue instead of inferring completeness in the frontend", async () => {
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

    const insights = await loadDashboardInsights("manager");

    expect(insights.stats[0]?.value).toBe(2);
    expect(insights.stats[1]?.value).toBe(2);
    expect(insights.queueItems[0]?.detail).toContain("2 von 2 beantwortet");
    expect(insights.queueItems[1]?.detail).toContain("1 von 3 beantwortet");
  });

  it("filters HR insights by the selected process type", async () => {
    mockedGetWorkflows.mockResolvedValue([
      createWorkflowSummary({
        uid: "wf-onboarding",
        workflowStatus: "waiting_for_department",
        processType: { key: "onboarding", name: "Onboarding" },
      }),
    ]);

    const insights = await loadDashboardInsights("hr", { processTypeKey: "onboarding" });

    expect(mockedGetWorkflows).toHaveBeenCalledWith(expect.objectContaining({ processTypeKey: "onboarding" }));
    expect(insights.stats[0]?.label).toBe("Offene Vorgänge");
    expect(insights.queueTitle).toBe("Vorgänge (Onboarding)");
    expect(insights.summary).toContain("vom Typ Onboarding");
  });

  it("filters manager insights against the assigned supervisor queue by process type", async () => {
    mockedGetSupervisorStepWorkflows.mockResolvedValue([
      createWorkflowSummary({
        uid: "wf-1",
        processType: { key: "onboarding", name: "Onboarding" },
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

    const insights = await loadDashboardInsights("manager", { processTypeKey: "offboarding" });

    expect(insights.stats[0]?.value).toBe(2);
    expect(insights.stats[1]?.value).toBe(1);
    expect(insights.queueItems).toHaveLength(1);
    expect(insights.queueTitle).toBe("Vorgänge (Offboarding)");
    expect(insights.summary).toContain("Offboarding");
  });
});
