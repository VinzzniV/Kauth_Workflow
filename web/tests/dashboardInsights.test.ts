import { beforeEach, describe, expect, it, vi } from "vitest";
import { loadDashboardInsights } from "../src/components/dashboard/dashboardInsights";
import * as workflowApi from "../src/services/workflowApi";
import { createWorkflowSummary } from "./testUtils";

vi.mock("../src/services/workflowApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/workflowApi")>("../src/services/workflowApi");
  return {
    ...actual,
    getWorkflows: vi.fn(),
  };
});

const mockedGetWorkflows = vi.mocked(workflowApi.getWorkflows);

describe("dashboardInsights", () => {
  beforeEach(() => {
    mockedGetWorkflows.mockReset();
  });

  it("summarizes manager insights from the visible workflow list", async () => {
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
      processTypeKey: "onboarding",
      selectedProcessType: { key: "onboarding", name: "Onboarding" },
    });

    expect(mockedGetWorkflows).toHaveBeenCalledWith(expect.objectContaining({ processTypeKey: "onboarding" }));
    expect(insights.stats[0]?.label).toBe("Offene Vorgänge");
    expect(insights.queueTitle).toBe("Vorgänge (Onboarding)");
    expect(insights.nextStep).toBe("Onboarding-Fälle in Startphase und Rücklauf prüfen.");
  });

  it("filters manager insights by process type using the visible workflow list", async () => {
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
      processTypeKey: "offboarding",
      selectedProcessType: { key: "offboarding", name: "Offboarding" },
    });

    expect(insights.stats[0]?.value).toBe(2);
    expect(insights.stats[1]?.value).toBe(1);
    expect(insights.queueItems).toHaveLength(1);
    expect(insights.queueTitle).toBe("Mitarbeitende (Offboarding)");
    expect(insights.employeeItems).toHaveLength(1);
  });
});
