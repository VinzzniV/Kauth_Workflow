// Aggregiert offene Aufgaben und Benachrichtigungen ueber alle aktiven Vorgaenge einer Person
// fuer die 360-Grad-Personenakte. Backend-Vertrag bleibt unveraendert: nur clientseitige Aggregation
// ueber WorkflowDetail (`getWorkflowByUid`).
import { useQueries } from "@tanstack/react-query";
import { useMemo } from "react";
import { getWorkflowByUid } from "../services/workflowApi";
import { queryKeys } from "../services/queryKeys";
import type {
  PersonWorkflowSummary,
  WorkflowDetail,
  WorkflowNotification,
  WorkflowTask,
} from "../types/workflow";

export type PersonWorkflowContext = {
  workflowUid: string;
  processTypeKey: string;
  processTypeName: string;
  roleName: string;
  departmentName: string;
};

export type AggregatedTask = {
  task: WorkflowTask;
  context: PersonWorkflowContext;
};

export type AggregatedNotification = {
  notification: WorkflowNotification;
  context: PersonWorkflowContext;
};

export type PersonWorkflowAggregates = {
  activeWorkflowCount: number;
  openTasks: AggregatedTask[];
  pendingNotifications: AggregatedNotification[];
  failedNotifications: AggregatedNotification[];
  isLoading: boolean;
  errorCount: number;
  totalActiveQueries: number;
};

const TERMINAL_STATUSES: ReadonlyArray<string> = ["completed"];
const OPEN_TASK_STATUSES: ReadonlyArray<string> = ["open", "ready", "in_progress", "blocked"];

function isActiveWorkflow(summary: PersonWorkflowSummary): boolean {
  if (summary.archivedAt) {
    return false;
  }
  return !TERMINAL_STATUSES.includes(summary.workflowStatus);
}

function buildContext(summary: PersonWorkflowSummary): PersonWorkflowContext {
  return {
    workflowUid: summary.uid,
    processTypeKey: summary.workflowDefinition.key,
    processTypeName: summary.workflowDefinition.name,
    roleName: summary.roleName,
    departmentName: summary.departmentName,
  };
}

function compareSlaPriority(left: WorkflowTask, right: WorkflowTask): number {
  const slaOrder: Record<WorkflowTask["slaStatus"], number> = {
    overdue: 0,
    due_today: 1,
    on_track: 2,
    none: 3,
  };
  return slaOrder[left.slaStatus] - slaOrder[right.slaStatus];
}

function compareDueAt(left: WorkflowTask, right: WorkflowTask): number {
  const leftTime = left.dueAt ? new Date(left.dueAt).getTime() : Number.MAX_SAFE_INTEGER;
  const rightTime = right.dueAt ? new Date(right.dueAt).getTime() : Number.MAX_SAFE_INTEGER;
  return leftTime - rightTime;
}

export function usePersonWorkflowAggregates(
  workflows: PersonWorkflowSummary[] | undefined
): PersonWorkflowAggregates {
  const activeWorkflows = useMemo(
    () => (workflows ?? []).filter(isActiveWorkflow),
    [workflows]
  );

  const detailQueries = useQueries({
    queries: activeWorkflows.map((summary) => ({
      queryKey: queryKeys.workflows.detail(summary.uid),
      queryFn: () => getWorkflowByUid(summary.uid),
      staleTime: 30 * 1000,
    })),
  });

  return useMemo<PersonWorkflowAggregates>(() => {
    const openTasks: AggregatedTask[] = [];
    const pendingNotifications: AggregatedNotification[] = [];
    const failedNotifications: AggregatedNotification[] = [];

    let isLoading = false;
    let errorCount = 0;

    detailQueries.forEach((query, index) => {
      const summary = activeWorkflows[index];
      if (!summary) {
        return;
      }

      if (query.isLoading) {
        isLoading = true;
      }
      if (query.error) {
        errorCount += 1;
      }

      const detail = query.data as WorkflowDetail | undefined;
      if (!detail) {
        return;
      }

      const context = buildContext(summary);

      detail.tasks.forEach((task) => {
        if (OPEN_TASK_STATUSES.includes(task.status)) {
          openTasks.push({ task, context });
        }
      });

      detail.notifications.forEach((notification) => {
        if (notification.status === "pending") {
          pendingNotifications.push({ notification, context });
        } else if (notification.status === "failed") {
          failedNotifications.push({ notification, context });
        }
      });
    });

    openTasks.sort((left, right) => {
      const slaCmp = compareSlaPriority(left.task, right.task);
      if (slaCmp !== 0) {
        return slaCmp;
      }
      const dueCmp = compareDueAt(left.task, right.task);
      if (dueCmp !== 0) {
        return dueCmp;
      }
      return left.task.sortOrder - right.task.sortOrder;
    });

    pendingNotifications.sort(
      (left, right) =>
        new Date(right.notification.createdAt).getTime() -
        new Date(left.notification.createdAt).getTime()
    );
    failedNotifications.sort(
      (left, right) =>
        new Date(right.notification.createdAt).getTime() -
        new Date(left.notification.createdAt).getTime()
    );

    return {
      activeWorkflowCount: activeWorkflows.length,
      openTasks,
      pendingNotifications,
      failedNotifications,
      isLoading,
      errorCount,
      totalActiveQueries: detailQueries.length,
    };
  }, [activeWorkflows, detailQueries]);
}
