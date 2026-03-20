import type { WorkflowTask, WorkflowTaskAssignment } from "../types/workflow";

export const UNASSIGNED_RESPONSIBILITY_FILTER = "__unassigned__";
export const DIRECT_USER_ASSIGNMENT_FILTER = "__direct_user__";

export type ResponsibilityFilterOption = {
  value: string;
  label: string;
};

export function getPrimaryAssignment(task: Pick<WorkflowTask, "assignments">): WorkflowTaskAssignment | null {
  if (task.assignments.length === 0) {
    return null;
  }

  return task.assignments.find((assignment) => assignment.isPrimary) ?? task.assignments[0];
}

export function getResponsibleResponsibilityLabel(task: Pick<WorkflowTask, "assignments" | "processArea">): string {
  const primary = getPrimaryAssignment(task);
  if (!primary) {
    if ("processArea" in task && typeof task.processArea === "string" && task.processArea.trim()) {
      return task.processArea;
    }

    return "Nicht zugewiesen";
  }

  if (primary.assigneeResponsibilityName && primary.assigneeResponsibilityName.trim()) {
    return primary.assigneeResponsibilityName;
  }

  if (primary.assignmentType === "user") {
    return "Direkt zugewiesen";
  }

  return "Ohne Zuständigkeits-Zuordnung";
}

export function getResponsibleUserLabel(task: Pick<WorkflowTask, "assignments">): string {
  const primary = getPrimaryAssignment(task);
  if (!primary) {
    return "Nicht zugewiesen";
  }

  if (primary.assigneeUserName && primary.assigneeUserName.trim()) {
    return primary.assigneeUserName;
  }

  if (primary.assigneeUserEmail && primary.assigneeUserEmail.trim()) {
    return primary.assigneeUserEmail;
  }

  if (primary.assignmentType === "user") {
    return "Direkte Personenzuordnung ausgeblendet";
  }

  return "Nicht zugewiesen";
}

export function getResponsibleResponsibilityFilterValue(task: Pick<WorkflowTask, "assignments" | "processArea">): string {
  return getResponsibleResponsibilityFilterOption(task).value;
}

export function getResponsibleResponsibilityFilterOption(task: Pick<WorkflowTask, "assignments" | "processArea">): ResponsibilityFilterOption {
  const primary = getPrimaryAssignment(task);
  if (!primary) {
    if (task.processArea) {
      return {
        value: task.processArea,
        label: task.processArea,
      };
    }

    return {
      value: UNASSIGNED_RESPONSIBILITY_FILTER,
      label: "Nicht zugewiesen",
    };
  }

  if (primary.assigneeResponsibilityKey && primary.assigneeResponsibilityName) {
    return {
      value: primary.assigneeResponsibilityKey,
      label: primary.assigneeResponsibilityName,
    };
  }

  if (primary.assigneeResponsibilityName) {
    return {
      value: primary.assigneeResponsibilityName,
      label: primary.assigneeResponsibilityName,
    };
  }

  if (primary.assignmentType === "user") {
    return {
      value: DIRECT_USER_ASSIGNMENT_FILTER,
      label: "Direkt zugewiesen",
    };
  }

  return {
    value: UNASSIGNED_RESPONSIBILITY_FILTER,
    label: "Ohne Zuständigkeits-Zuordnung",
  };
}
