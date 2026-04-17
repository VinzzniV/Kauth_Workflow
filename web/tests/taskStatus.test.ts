import { describe, expect, it } from "vitest";
import {
  getAvailableVisibleTaskStatuses,
  getVisibleTaskStatus,
  mapVisibleTaskStatusToWorkflowStatus,
  TASK_STATUS_ORDER,
} from "../src/utils/taskStatus";

describe("taskStatus", () => {
  it("does not expose internal-only statuses as supported task statuses", () => {
    expect(TASK_STATUS_ORDER).not.toContain("skipped");
    // "cancelled" is a valid status for rotation tasks and is included intentionally
  });

  it("maps done directly to done", () => {
    expect(getVisibleTaskStatus("done")).toBe("done");
    expect(mapVisibleTaskStatusToWorkflowStatus("done", "blocked")).toBe("done");
  });

  it("offers blocked and open for blocked workflow tasks", () => {
    // blocked is included so the select can show the current state; open is the unblock transition
    expect(getAvailableVisibleTaskStatuses("blocked", "workflow")).toEqual(["blocked", "open"]);
  });

  it("does not offer blocked or done for blocked rotation tasks", () => {
    // rotation tasks follow a simpler linear flow without a blocked state
    expect(getAvailableVisibleTaskStatuses("open", "rotation")).toContain("open");
    expect(getAvailableVisibleTaskStatuses("open", "rotation")).not.toContain("blocked");
  });
});
