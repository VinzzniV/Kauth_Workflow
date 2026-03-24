import { describe, expect, it } from "vitest";
import {
  getWorkflowRuntimeStatusLabel,
  getWorkflowRuntimeStatusPillClass,
  matchesWorkflowRuntimeStatusFilter,
} from "../src/utils/workflowStatus";

describe("workflowStatus", () => {
  it("filters by runtime status instead of legacy open/completed", () => {
    expect(matchesWorkflowRuntimeStatusFilter("waiting_for_supervisor", "waiting_for_supervisor")).toBe(true);
    expect(matchesWorkflowRuntimeStatusFilter("waiting_for_supervisor", "completed")).toBe(false);
  });

  it("maps runtime status labels and pill classes consistently", () => {
    expect(getWorkflowRuntimeStatusLabel("waiting_for_department")).toBe("Fachbereiche offen");
    expect(getWorkflowRuntimeStatusPillClass("waiting_for_department")).toBe("running");
    expect(getWorkflowRuntimeStatusPillClass("completed")).toBe("completed");
    expect(getWorkflowRuntimeStatusPillClass("draft")).toBe("open");
  });
});
