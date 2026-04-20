import type { TaskFamily, TaskStatus, WorkflowTaskSlaStatus } from "../types/workflow";

export type VisibleTaskStatus = "open" | "in_progress" | "blocked" | "done" | "failed" | "cancelled";

export const TASK_STATUS_ORDER: TaskStatus[] = [
  "open",
  "ready",
  "in_progress",
  "blocked",
  "done",
  "completed",
  "failed",
  "cancelled",
];

const TASK_STATUS_LABELS: Record<TaskStatus, string> = {
  open: "Offen",
  ready: "Offen",
  in_progress: "In Bearbeitung",
  blocked: "Blockiert",
  done: "Erledigt",
  completed: "Erledigt",
  failed: "Fehlgeschlagen",
  cancelled: "Storniert",
};

export const VISIBLE_TASK_STATUS_ORDER: VisibleTaskStatus[] = [
  "open",
  "in_progress",
  "blocked",
  "done",
  "failed",
  "cancelled",
];

const VISIBLE_TASK_STATUS_LABELS: Record<VisibleTaskStatus, string> = {
  open: "Offen",
  in_progress: "In Bearbeitung",
  blocked: "Blockiert",
  done: "Erledigt",
  failed: "Fehlgeschlagen",
  cancelled: "Storniert",
};

export function getVisibleTaskStatus(status: TaskStatus): VisibleTaskStatus {
  if (status === "in_progress") {
    return "in_progress";
  }

  if (status === "done" || status === "completed") {
    return "done";
  }

  if (status === "blocked") {
    return "blocked";
  }

  if (status === "failed") {
    return "failed";
  }

  if (status === "cancelled") {
    return "cancelled";
  }

  return "open";
}

export function getTaskStatusLabel(status: TaskStatus): string {
  return TASK_STATUS_LABELS[status] ?? status;
}

export function getVisibleTaskStatusLabel(status: VisibleTaskStatus): string {
  return VISIBLE_TASK_STATUS_LABELS[status];
}

export function mapVisibleTaskStatusToWorkflowStatus(
  status: VisibleTaskStatus,
  currentStatus?: TaskStatus,
  taskFamily: TaskFamily = "workflow"
): TaskStatus {
  if (taskFamily === "rotation") {
    switch (status) {
      case "open":
        return "open";
      case "in_progress":
        return "in_progress";
      case "done":
        return "completed";
      case "failed":
        return "failed";
      case "cancelled":
        return "cancelled";
      case "blocked":
        return currentStatus ?? "open";
      default:
        return currentStatus ?? "open";
    }
  }

  switch (status) {
    case "open":
      return currentStatus === "ready" ? "ready" : "open";
    case "in_progress":
      return "in_progress";
    case "blocked":
      return "blocked";
    case "done":
      return currentStatus === "completed" ? "completed" : "done";
    case "failed":
      return "failed";
    case "cancelled":
      return "cancelled";
    default:
      return currentStatus ?? "open";
  }
}

export function getAvailableVisibleTaskStatuses(
  currentStatus: TaskStatus,
  taskFamily: TaskFamily = "workflow"
): VisibleTaskStatus[] {
  if (taskFamily === "rotation") {
    switch (currentStatus) {
      case "open":
        return ["open", "in_progress", "done", "failed"];
      case "in_progress":
        return ["in_progress", "done", "failed"];
      case "completed":
        return ["done"];
      case "failed":
        return ["failed"];
      case "cancelled":
        return ["cancelled"];
      default:
        return ["open"];
    }
  }

  switch (currentStatus) {
    case "open":
    case "ready":
      return ["open", "in_progress", "blocked", "done"];
    case "blocked":
      return ["blocked", "open"];
    case "in_progress":
      return ["in_progress", "blocked", "done"];
    case "done":
    case "completed":
      return ["done"];
    case "failed":
      return ["open", "in_progress", "blocked", "done"];
    case "cancelled":
      return ["open", "in_progress", "blocked", "done"];
    default:
      return ["open"];
  }
}

export function getTaskStatusClassName(status: TaskStatus): string {
  return `task-pill--${status}`;
}

const TASK_SLA_LABELS: Record<WorkflowTaskSlaStatus, string> = {
  none: "Keine Frist",
  on_track: "Im Zeitplan",
  due_today: "Heute fällig",
  overdue: "Überfällig",
};

export function getTaskSlaLabel(status: WorkflowTaskSlaStatus): string {
  return TASK_SLA_LABELS[status];
}

export function getTaskSlaClassName(status: WorkflowTaskSlaStatus): string {
  return `task-sla-pill--${status}`;
}
