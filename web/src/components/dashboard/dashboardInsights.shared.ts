import type { ProcessType, WorkflowSummary, WorkflowTask } from "../../types/workflow";
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

export type DashboardInsights = {
  heading: string;
  nextStep: string;
  stats: DashboardStat[];
  queueTitle: string;
  queueItems: DashboardQueueItem[];
  emptyQueueText: string;
  employeeListTitle?: string;
  employeeListDescription?: string;
  employeeItems?: DashboardEmployeeItem[];
};

export type DashboardInsightsOptions = {
  processTypeKey?: string | null;
  selectedProcessType?: ProcessType | null;
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

export function getProcessTypeContext(selectedProcessType: ProcessType | null) {
  if (!selectedProcessType) {
    return {
      scopedTitle: "Vorgänge",
      scopedEmptyQueueText: "Aktuell sind keine Vorgänge vorhanden.",
    };
  }

  return {
    scopedTitle: `Vorgänge (${selectedProcessType.name})`,
    scopedEmptyQueueText: `Aktuell sind keine Vorgänge vom Typ ${selectedProcessType.name} vorhanden.`,
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
