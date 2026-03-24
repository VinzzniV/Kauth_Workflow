import { describe, expect, it } from "vitest";
import {
  getAvailableVisibleTaskStatuses,
  getVisibleTaskStatus,
  mapVisibleTaskStatusToWorkflowStatus,
  TASK_STATUS_ORDER,
} from "../src/utils/taskStatus";

describe("taskStatus", () => {
  it("does not expose skipped as a supported task status", () => {
    expect(TASK_STATUS_ORDER).not.toContain("skipped");
    expect(TASK_STATUS_ORDER).not.toContain("cancelled");
  });

  it("maps done directly to done", () => {
    expect(getVisibleTaskStatus("done")).toBe("done");
    expect(mapVisibleTaskStatusToWorkflowStatus("done", "blocked")).toBe("done");
  });

  it("does not offer done for blocked tasks", () => {
    expect(getAvailableVisibleTaskStatuses("blocked")).toEqual(["open"]);
  });
});
