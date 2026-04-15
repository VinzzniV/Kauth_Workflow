import { describe, expect, it } from "vitest";
import { getResponsibleResponsibilityLabel } from "../src/utils/taskAssignment";
import type { WorkflowTask } from "../src/types/workflow";

function createTask(overrides: Partial<WorkflowTask>): WorkflowTask {
  return {
    id: 1,
    taskTemplateId: 1,
    taskKey: "babtec_user_create",
    nodeInstanceId: null,
    isApprovalTask: false,
    isRuntimeNodeTask: false,
    title: "Babtec-User anlegen",
    description: "",
    category: "Fachanwendungen",
    iconKey: "babtec",
    status: "ready",
    isRequired: true,
    dueInDays: 3,
    dueAt: null,
    slaStatus: "on_track",
    sortOrder: 1,
    createdAt: "2026-04-15T10:00:00Z",
    readyAt: null,
    startedAt: null,
    completedAt: null,
    processArea: null,
    isDepartmentPhaseTask: true,
    canUpdateStatus: false,
    canDecideApproval: false,
    canAddComment: false,
    assignments: [],
    dependencies: [],
    comments: [],
    ...overrides,
  };
}

describe("taskAssignment", () => {
  it("uses the process area as responsibility label for direct user assignments", () => {
    const task = createTask({
      processArea: "QS",
      assignments: [
        {
          id: 1,
          assignmentType: "user",
          isPrimary: true,
          assignedAt: "2026-04-15T10:00:00Z",
          completedAt: null,
          assigneeUserId: 42,
          assigneeUserName: "Max Mustermann",
          assigneeUserEmail: "max@example.test",
          assigneeResponsibilityId: null,
          assigneeResponsibilityKey: null,
          assigneeResponsibilityName: null,
          assigneeResponsibilityType: null,
        },
      ],
    });

    expect(getResponsibleResponsibilityLabel(task)).toBe("QS");
  });
});
