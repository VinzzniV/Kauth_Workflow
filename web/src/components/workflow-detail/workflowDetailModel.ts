import type {
  WorkflowDetail,
  WorkflowTask,
  WorkflowTaskAreaSummary,
  WorkflowTaskArea,
} from "../../types/workflow";
import {
  getWorkflowRuntimeStatusLabel,
  isDepartmentWorkflowPhase as isDepartmentWorkflowPhaseStatus,
  isWorkflowTerminalStatus,
} from "../../utils/workflowStatus";
import {
  formatDate as formatDateValue,
  formatDateTime as formatDateTimeValue,
} from "../../utils/dateFormat";

export type ProcessAreaName = WorkflowTaskArea;

export type ProcessAreaGroup = {
  name: ProcessAreaName;
  tasks: WorkflowTask[];
  totalCount: number;
  openCount: number;
  inProgressCount: number;
  blockedCount: number;
  completedCount: number;
  isCurrentArea: boolean;
};

export function formatDate(value: string | null): string {
  return formatDateValue(value);
}

export function formatDateTime(value: string | null): string {
  return formatDateTimeValue(value);
}

export function toRuntimeStatusLabel(status: WorkflowDetail["workflowStatus"]): string {
  return getWorkflowRuntimeStatusLabel(status);
}

export function isOpenStatus(status: WorkflowTask["status"]): boolean {
  return status === "open" || status === "ready";
}

export function isInProgressStatus(status: WorkflowTask["status"]): boolean {
  return status === "in_progress";
}

export function isDoneStatus(status: WorkflowTask["status"]): boolean {
  return status === "done" || status === "completed";
}

export function isActiveStatus(status: WorkflowTask["status"]): boolean {
  return isOpenStatus(status) || isInProgressStatus(status) || status === "blocked";
}

export function inferAreaFromTask(task: WorkflowTask): ProcessAreaName | null {
  return task.processArea;
}

export function toAreaStatus(group: ProcessAreaGroup): "none" | "open" | "in_progress" | "done" {
  if (group.totalCount === 0) {
    return "none";
  }

  if (group.inProgressCount > 0) {
    return "in_progress";
  }

  if (group.openCount > 0 || group.blockedCount > 0) {
    return "open";
  }

  return "done";
}

export function compareAreaGroupsForDisplay(left: ProcessAreaGroup, right: ProcessAreaGroup): number {
  if (left.isCurrentArea !== right.isCurrentArea) {
    return left.isCurrentArea ? -1 : 1;
  }

  const statusPriority: Record<ReturnType<typeof toAreaStatus>, number> = {
    in_progress: 0,
    open: 1,
    none: 2,
    done: 3,
  };
  const statusDelta = statusPriority[toAreaStatus(left)] - statusPriority[toAreaStatus(right)];
  if (statusDelta !== 0) {
    return statusDelta;
  }

  const openDelta = right.openCount - left.openCount;
  if (openDelta !== 0) {
    return openDelta;
  }

  return left.name.localeCompare(right.name, "de");
}

export function toAreaStatusLabel(status: ReturnType<typeof toAreaStatus>): string {
  if (status === "none") {
    return "Noch kein Schritt";
  }

  if (status === "in_progress") {
    return "In Bearbeitung";
  }

  if (status === "open") {
    return "Offen";
  }

  return "Abgeschlossen";
}

export function toAreaStatusNote(group: ProcessAreaGroup): string {
  if (group.totalCount === 0) {
    return "In diesem Bereich sind aktuell keine Aufgaben vorgesehen.";
  }

  if (group.inProgressCount > 0 && group.openCount > 0) {
    return `${group.inProgressCount} in Bearbeitung, ${group.openCount} offen.`;
  }

  if (group.inProgressCount > 0) {
    return `${group.inProgressCount} Aufgabe${group.inProgressCount === 1 ? "" : "n"} in Bearbeitung.`;
  }

  if (group.blockedCount > 0 && group.openCount > 0) {
    return `${group.openCount} offen, ${group.blockedCount} blockiert.`;
  }

  if (group.blockedCount > 0) {
    return `${group.blockedCount} Aufgabe${group.blockedCount === 1 ? "" : "n"} blockiert.`;
  }

  if (group.openCount > 0) {
    return `${group.openCount} offene Aufgabe${group.openCount === 1 ? "" : "n"}.`;
  }

  return "Alle Aufgaben dieses Bereichs sind erledigt.";
}

export function toTaskDisplayTitle(task: WorkflowTask): string {
  return task.title;
}

export function compareTasksForDisplay(left: WorkflowTask, right: WorkflowTask): number {
  const statusPriority: Record<WorkflowTask["status"], number> = {
    in_progress: 0,
    ready: 1,
    open: 2,
    blocked: 3,
    done: 4,
    completed: 4,
    failed: 5,
    cancelled: 6,
  };
  const statusDelta = statusPriority[left.status] - statusPriority[right.status];
  if (statusDelta !== 0) {
    return statusDelta;
  }

  const sortOrderDelta = left.sortOrder - right.sortOrder;
  if (sortOrderDelta !== 0) {
    return sortOrderDelta;
  }

  return left.id - right.id;
}

export function isDepartmentWorkflowPhase(status: WorkflowDetail["workflowStatus"]): boolean {
  return isDepartmentWorkflowPhaseStatus(status);
}

export function hasSupervisorStep(workflow: WorkflowDetail): boolean {
  return workflow.workflowStatus === "waiting_for_supervisor" || workflow.tasks.some((task) => task.isApprovalTask);
}

export function toPhaseOwnerArea(workflow: WorkflowDetail): string {
  if (isWorkflowTerminalStatus(workflow.workflowStatus)) {
    return getWorkflowRuntimeStatusLabel(workflow.workflowStatus);
  }

  switch (workflow.workflowStatus) {
    case "draft":
      return "HR";
    case "waiting_for_supervisor":
      return "Abteilungsleitung";
    case "waiting_for_department":
    case "in_progress":
      return "Fachbereiche";
    default:
      return "-";
  }
}

export function toRegularEditingLabel(workflow: WorkflowDetail): string {
  if (isWorkflowTerminalStatus(workflow.workflowStatus)) {
    return "Abgeschlossen";
  }

  switch (workflow.workflowStatus) {
    case "draft":
      return "HR-Startphase";
    case "waiting_for_supervisor":
      return "Anforderungsphase";
    case "waiting_for_department":
    case "in_progress":
      return "Fachbereichsphase";
    default:
      return "-";
  }
}

export function findCurrentTask(tasks: WorkflowTask[]): WorkflowTask | null {
  const priority: Record<WorkflowTask["status"], number> = {
    in_progress: 0,
    ready: 1,
    open: 2,
    blocked: 3,
    done: 4,
    completed: 4,
    failed: 5,
    cancelled: 6,
  };

  const activeTasks = tasks.filter((task) => isActiveStatus(task.status));
  if (activeTasks.length === 0) {
    return null;
  }

  return activeTasks
    .slice()
    .sort((left, right) => {
      const delta = priority[left.status] - priority[right.status];
      if (delta !== 0) {
        return delta;
      }

      return left.sortOrder - right.sortOrder || left.id - right.id;
    })[0];
}

export function buildTasksByArea(
  sortedTasks: WorkflowTask[],
  taskAreas: WorkflowTaskAreaSummary[]
): ProcessAreaGroup[] {
  const byArea = new Map<ProcessAreaName, WorkflowTask[]>();
  const orderedAreaNames = taskAreas.map((area) => area.name);

  for (const task of sortedTasks) {
    const areaName = inferAreaFromTask(task);
    if (!areaName) {
      continue;
    }

    let tasks = byArea.get(areaName);
    if (!tasks) {
      tasks = [];
      byArea.set(areaName, tasks);
      if (!orderedAreaNames.includes(areaName)) {
        orderedAreaNames.push(areaName);
      }
    }

    tasks.push(task);
  }

  return orderedAreaNames.map((name) => {
    const tasks = byArea.get(name) ?? [];
    const summary = taskAreas.find((area) => area.name === name);
    return {
      name,
      tasks,
      totalCount: summary?.counts.totalCount ?? tasks.length,
      openCount: summary?.counts.openCount ?? tasks.filter((task) => isOpenStatus(task.status)).length,
      inProgressCount: summary?.counts.inProgressCount ?? tasks.filter((task) => isInProgressStatus(task.status)).length,
      blockedCount: summary?.counts.blockedCount ?? tasks.filter((task) => task.status === "blocked").length,
      completedCount: summary?.counts.completedCount ?? tasks.filter((task) => isDoneStatus(task.status)).length,
      isCurrentArea: summary?.isCurrentArea ?? false,
    };
  });
}
