import { describe, expect, it } from "vitest";
import {
  toPhaseOwnerArea,
  toRegularEditingLabel,
} from "../src/components/workflow-detail/workflowDetailModel";
import type { WorkflowDetail } from "../src/types/workflow";

function createWorkflowDetail(overrides: Partial<WorkflowDetail> = {}): WorkflowDetail {
  return {
    uid: "wf-1",
    firstName: "Alice",
    lastName: "Example",
    employeeNumber: 1001,
    badgeNumber: 2001,
    departmentId: 10,
    departmentName: "IT",
    roleId: 5,
    roleName: "Engineer",
    status: "open",
    workflowStatus: "waiting_for_department",
    createdAt: "2026-03-20T10:00:00.000Z",
    deadlineDate: null,
    requirements: [],
    requirementSummary: {
      totalCount: 0,
      visibleCount: 0,
      answeredVisibleCount: 0,
      pendingVisibleCount: 0,
    },
    tasks: [],
    taskMetrics: {
      overall: {
        totalCount: 0,
        openCount: 0,
        inProgressCount: 0,
        doneCount: 0,
        completedCount: 0,
        activeCount: 0,
      },
      departmentPhase: {
        totalCount: 0,
        openCount: 0,
        inProgressCount: 0,
        doneCount: 0,
        completedCount: 0,
        activeCount: 0,
      },
    },
    taskAreas: [],
    notifications: [],
    ...overrides,
  };
}

describe("workflowDetailModel", () => {
  it("uses phase labels instead of frontend access-role claims", () => {
    expect(toRegularEditingLabel(createWorkflowDetail({ workflowStatus: "draft" }))).toBe("HR-Startphase");
    expect(toRegularEditingLabel(createWorkflowDetail({ workflowStatus: "waiting_for_supervisor" }))).toBe(
      "Anforderungsphase"
    );
    expect(toRegularEditingLabel(createWorkflowDetail({ workflowStatus: "in_progress" }))).toBe(
      "Fachbereichsphase"
    );
    expect(toRegularEditingLabel(createWorkflowDetail({ workflowStatus: "completed" }))).toBe("Abgeschlossen");
  });

  it("keeps current owner area derived from workflow phase", () => {
    expect(toPhaseOwnerArea(createWorkflowDetail({ workflowStatus: "draft" }))).toBe("HR");
    expect(toPhaseOwnerArea(createWorkflowDetail({ workflowStatus: "waiting_for_supervisor" }))).toBe(
      "Abteilungsleitung"
    );
    expect(toPhaseOwnerArea(createWorkflowDetail({ workflowStatus: "waiting_for_department" }))).toBe(
      "Fachbereiche"
    );
  });
});
