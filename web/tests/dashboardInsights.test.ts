import { describe, expect, it, vi, beforeEach } from "vitest";
import { loadDashboardInsights } from "../src/components/dashboard/dashboardInsights";
import * as onboardingApi from "../src/services/onboardingApi";
import { createWorkflowSummary } from "./testUtils";

vi.mock("../src/services/onboardingApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/onboardingApi")>("../src/services/onboardingApi");
  return {
    ...actual,
    getAdminGroups: vi.fn(),
    getAdminRoles: vi.fn(),
    getAdminUsers: vi.fn(),
    getMyTasks: vi.fn(),
    getSupervisorStepWorkflows: vi.fn(),
    getWorkflows: vi.fn(),
  };
});

const mockedGetSupervisorStepWorkflows = vi.mocked(onboardingApi.getSupervisorStepWorkflows);

describe("dashboardInsights", () => {
  beforeEach(() => {
    mockedGetSupervisorStepWorkflows.mockReset();
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
});
