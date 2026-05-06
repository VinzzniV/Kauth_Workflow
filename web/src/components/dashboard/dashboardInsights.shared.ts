import type { StartableWorkflowDefinition, WorkflowSummary, WorkflowTask } from "../../types/workflow";
import type { DashboardAction } from "../../navigation/useRoleAwareNavigation";
import { formatDate } from "../../utils/dateFormat";
import { getWorkflowRuntimeStatusLabel, isWorkflowTerminalStatus } from "../../utils/workflowStatus";

export type DashboardStat = {
  label: string;
  value: number;
  note: string;
  statusLabel?: string;
  tone?: "neutral" | "attention" | "progress" | "success";
};

export type DashboardQueueItem = {
  key: string;
  title: string;
  detail: string;
  to: string;
  actionLabel: string;
};

export type DashboardEmployeeItem = {
  key: string;
  name: string;
  roleName: string;
  departmentName: string | null;
  processTypeName: string;
  workflowStatus: WorkflowSummary["workflowStatus"];
  contextText: string;
  dateLabel: string;
  dateValue: string;
  to: string;
  actionLabel: string;
};

export type DashboardAdminWarningItem = {
  key: string;
  title: string;
  detail: string;
  count: number;
  affectedLabels: string[];
  actionLabel: string;
  to: string;
};

export type DashboardAdminWarningCluster = {
  key: string;
  title: string;
  totalCount: number;
  groups: DashboardAdminWarningItem[];
};

export type DashboardAdminOperationItem = DashboardQueueItem & {
  tone?: "neutral" | "attention" | "progress" | "success";
};

export type DashboardAdminSummary = {
  statusKicker: string;
  statusTitle: string;
  statusDetail: string;
  action?: DashboardAction | null;
  stats: DashboardStat[];
};

export type DashboardInsights = {
  heading: string;
  nextStep: string;
  stats: DashboardStat[];
  queueTitle: string;
  queueItems: DashboardQueueItem[];
  emptyQueueText: string;
  adminSummary?: DashboardAdminSummary;
  adminWarnings?: DashboardAdminWarningCluster[];
  adminOperations?: DashboardAdminOperationItem[];
  employeeListTitle?: string;
  employeeListDescription?: string;
  employeeItems?: DashboardEmployeeItem[];
};

export type DashboardInsightsOptions = {
  workflowDefinitionKey?: string | null;
  selectedWorkflowDefinition?: StartableWorkflowDefinition | null;
};

export type WorkflowMetrics = {
  total: number;
  open: number;
  waitingSupervisor: number;
  waitingDepartment: number;
  inProgress: number;
  completed: number;
};

export const RECENT_COMPLETION_WINDOW_DAYS = 30;
export const STUCK_WORKFLOW_THRESHOLD_DAYS = 7;
export const COMPLETION_THIS_WEEK_DAYS = 7;

export function toEpoch(value: string): number {
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return 0;
  }

  return parsed.getTime();
}

export function summarizeWorkflows(workflows: WorkflowSummary[]): WorkflowMetrics {
  return workflows.reduce<WorkflowMetrics>(
    (acc, workflow) => {
      acc.total += 1;
      if (!isWorkflowTerminalStatus(workflow.workflowStatus)) {
        acc.open += 1;
      }
      if (workflow.workflowStatus === "waiting_for_supervisor") {
        acc.waitingSupervisor += 1;
      }
      if (workflow.workflowStatus === "waiting_for_department") {
        acc.waitingDepartment += 1;
      }
      if (workflow.workflowStatus === "in_progress") {
        acc.inProgress += 1;
      }
      if (workflow.workflowStatus === "completed") {
        acc.completed += 1;
      }
      return acc;
    },
    {
      total: 0,
      open: 0,
      waitingSupervisor: 0,
      waitingDepartment: 0,
      inProgress: 0,
      completed: 0,
    }
  );
}

export function getWorkflowDefinitionContext(selectedWorkflowDefinition: StartableWorkflowDefinition | null) {
  if (!selectedWorkflowDefinition) {
    return {
      scopedTitle: "Vorgänge",
      scopedEmptyQueueText: "Aktuell sind keine Vorgänge vorhanden.",
    };
  }

  return {
    scopedTitle: `Vorgänge (${selectedWorkflowDefinition.name})`,
    scopedEmptyQueueText: `Aktuell sind keine Vorgänge vom Typ ${selectedWorkflowDefinition.name} vorhanden.`,
  };
}

export function isRecentlyCompletedWorkflow(workflow: WorkflowSummary, nowEpoch: number): boolean {
  if (workflow.workflowStatus !== "completed" || !workflow.completedAt) {
    return false;
  }

  const completedEpoch = toEpoch(workflow.completedAt);
  if (completedEpoch === 0) {
    return false;
  }

  const completionWindowMs = RECENT_COMPLETION_WINDOW_DAYS * 24 * 60 * 60 * 1000;
  return completedEpoch >= nowEpoch - completionWindowMs;
}

export function getManagerWorkflowPriority(status: WorkflowSummary["workflowStatus"]): number {
  switch (status) {
    case "waiting_for_supervisor":
      return 0;
    case "waiting_for_department":
      return 1;
    case "in_progress":
      return 2;
    case "draft":
      return 3;
    case "completed":
      return 4;
    default:
      return 5;
  }
}

export function getManagerWorkflowContextText(workflow: WorkflowSummary): string {
  switch (workflow.workflowStatus) {
    case "waiting_for_supervisor":
      return "Wartet auf Ihre Rückmeldung.";
    case "waiting_for_department":
      return "Fachbereiche bearbeiten Aufgaben.";
    case "in_progress":
      return "Vorgang läuft in den Fachbereichen.";
    case "draft":
      return "HR bereitet den Vorgang vor.";
    case "completed":
      return workflow.completedAt
        ? `Abgeschlossen am ${formatDate(workflow.completedAt)}.`
        : "Abgeschlossen.";
    default:
      return getWorkflowRuntimeStatusLabel(workflow.workflowStatus, "action");
  }
}

export function getManagerWorkflowAction(
  workflow: WorkflowSummary
): Pick<DashboardEmployeeItem, "to" | "actionLabel"> {
  if (workflow.workflowStatus === "waiting_for_supervisor") {
    return {
      to: "/supervisor",
      actionLabel: "Bearbeiten",
    };
  }

  return {
    to: `/workflows/${workflow.uid}`,
    actionLabel: workflow.workflowStatus === "completed" ? "Ansehen" : "Öffnen",
  };
}

export function isOpenTask(task: Pick<WorkflowTask, "status">): boolean {
  return task.status === "open" || task.status === "ready" || task.status === "in_progress" || task.status === "blocked";
}

export function isStuckWorkflow(workflow: WorkflowSummary, nowEpoch: number): boolean {
  if (workflow.workflowStatus !== "waiting_for_supervisor" && workflow.workflowStatus !== "waiting_for_department") {
    return false;
  }

  const createdEpoch = toEpoch(workflow.createdAt);
  if (createdEpoch === 0) {
    return false;
  }

  const thresholdMs = STUCK_WORKFLOW_THRESHOLD_DAYS * 24 * 60 * 60 * 1000;
  return nowEpoch - createdEpoch >= thresholdMs;
}

export function isCompletedThisWeek(workflow: WorkflowSummary, nowEpoch: number): boolean {
  if (workflow.workflowStatus !== "completed" || !workflow.completedAt) {
    return false;
  }

  const completedEpoch = toEpoch(workflow.completedAt);
  if (completedEpoch === 0) {
    return false;
  }

  const windowMs = COMPLETION_THIS_WEEK_DAYS * 24 * 60 * 60 * 1000;
  return completedEpoch >= nowEpoch - windowMs;
}

export function hasUnresolvedRequirements(workflow: WorkflowSummary): boolean {
  return workflow.requirementSummary.pendingVisibleCount > 0;
}

export function isDeadlineThisWeek(workflow: WorkflowSummary, nowEpoch: number): boolean {
  if (!workflow.deadlineDate) {
    return false;
  }

  const deadlineEpoch = toEpoch(workflow.deadlineDate);
  if (deadlineEpoch === 0) {
    return false;
  }

  const windowMs = COMPLETION_THIS_WEEK_DAYS * 24 * 60 * 60 * 1000;
  return deadlineEpoch >= nowEpoch && deadlineEpoch <= nowEpoch + windowMs;
}
