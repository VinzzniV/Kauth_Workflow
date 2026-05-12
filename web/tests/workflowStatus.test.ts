import { describe, expect, it } from "vitest";
import {
  getWorkflowRuntimeStatusLabel,
  getWorkflowRuntimeStatusPillClass,
  isWorkflowTerminalStatus,
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

  it("treats cancelled as terminal alongside completed", () => {
    expect(isWorkflowTerminalStatus("completed")).toBe(true);
    expect(isWorkflowTerminalStatus("cancelled")).toBe(true);
    expect(isWorkflowTerminalStatus("CANCELLED")).toBe(true);
    expect(isWorkflowTerminalStatus("in_progress")).toBe(false);
    expect(isWorkflowTerminalStatus("draft")).toBe(false);
  });

  it("renders cancelled label and pill class distinctly", () => {
    expect(getWorkflowRuntimeStatusLabel("cancelled")).toBe("Storniert");
    expect(getWorkflowRuntimeStatusPillClass("cancelled")).toBe("cancelled");
  });

  it("includes cancelled in status filter matching", () => {
    expect(matchesWorkflowRuntimeStatusFilter("cancelled", "cancelled")).toBe(true);
    expect(matchesWorkflowRuntimeStatusFilter("cancelled", "completed")).toBe(false);
    expect(matchesWorkflowRuntimeStatusFilter("cancelled", "all")).toBe(true);
  });
});
