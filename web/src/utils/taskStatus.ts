import type { WorkflowTaskSlaStatus, WorkflowTaskStatus } from "../types/workflow";

export type VisibleTaskStatus = "open" | "in_progress" | "blocked" | "done";

export const TASK_STATUS_ORDER: WorkflowTaskStatus[] = [
  "open",
  "ready",
  "in_progress",
  "blocked",
  "done",
];

const TASK_STATUS_LABELS: Record<WorkflowTaskStatus, string> = {
  open: "Offen",
  ready: "Offen",
  in_progress: "In Bearbeitung",
  blocked: "Blockiert",
  done: "Erledigt",
};

export const VISIBLE_TASK_STATUS_ORDER: VisibleTaskStatus[] = ["open", "in_progress", "blocked", "done"];

const VISIBLE_TASK_STATUS_LABELS: Record<VisibleTaskStatus, string> = {
  open: "Offen",
  in_progress: "In Bearbeitung",
  blocked: "Blockiert",
  done: "Erledigt",
};

export function getVisibleTaskStatus(status: WorkflowTaskStatus): VisibleTaskStatus {
  if (status === "in_progress") {
    return "in_progress";
  }

  if (status === "done") {
    return "done";
  }

  if (status === "blocked") {
    return "blocked";
  }

  return "open";
}

export function getTaskStatusLabel(status: WorkflowTaskStatus): string {
  return TASK_STATUS_LABELS[status] ?? status;
}

export function getVisibleTaskStatusLabel(status: VisibleTaskStatus): string {
  return VISIBLE_TASK_STATUS_LABELS[status];
}

export function mapVisibleTaskStatusToWorkflowStatus(
  status: VisibleTaskStatus,
  currentStatus?: WorkflowTaskStatus
): WorkflowTaskStatus {
  switch (status) {
    case "open":
      return currentStatus === "ready" ? "ready" : "open";
    case "in_progress":
      return "in_progress";
    case "blocked":
      return "blocked";
    case "done":
      return "done";
    default:
      return currentStatus ?? "open";
  }
}

export function getAvailableVisibleTaskStatuses(currentStatus: WorkflowTaskStatus): VisibleTaskStatus[] {
  switch (currentStatus) {
    case "open":
    case "ready":
      return ["open", "in_progress", "blocked", "done"];
    case "blocked":
      return ["blocked", "open"];
    case "in_progress":
      return ["in_progress", "blocked", "done"];
    case "done":
      return ["done"];
    default:
      return ["open"];
  }
}

export function getTaskStatusClassName(status: WorkflowTaskStatus): string {
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
