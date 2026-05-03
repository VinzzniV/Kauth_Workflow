import { getAdminGroups, getAdminRoles, getAdminUsers } from "../../services/adminApi";
import { getMyTasks } from "../../services/taskApi";
import { getSupervisorStepWorkflows, getWorkflows } from "../../services/workflowApi";
import type { WorkflowSummary, WorkflowTask } from "../../types/workflow";
import { formatDate } from "../../utils/dateFormat";
import { getTaskStatusLabel } from "../../utils/taskStatus";
import { getWorkflowRuntimeStatusLabel, isWorkflowTerminalStatus } from "../../utils/workflowStatus";
import type { DashboardInsights, DashboardInsightsOptions, DashboardQueueItem } from "./dashboardInsights.shared";
import {
  getManagerWorkflowAction,
  getManagerWorkflowContextText,
  getManagerWorkflowPriority,
  getWorkflowDefinitionContext,
  isOpenTask,
  isRecentlyCompletedWorkflow,
  RECENT_COMPLETION_WINDOW_DAYS,
  summarizeWorkflows,
  toEpoch,
} from "./dashboardInsights.shared";

export async function loadHrInsights(options: DashboardInsightsOptions = {}): Promise<DashboardInsights> {
  const selectedWorkflowDefinition = options.selectedWorkflowDefinition ?? null;
  const workflowDefinitionContext = getWorkflowDefinitionContext(selectedWorkflowDefinition);
  const workflows = await getWorkflows({ workflowDefinitionKey: options.workflowDefinitionKey ?? null });
  const metrics = summarizeWorkflows(workflows);
  const departmentInProgress = metrics.waitingDepartment + metrics.inProgress;
  const activeWorkflows = workflows
    .filter((workflow) => !isWorkflowTerminalStatus(workflow.workflowStatus))
    .slice()
    .sort((left, right) => {
      const statusPriority: Record<WorkflowSummary["workflowStatus"], number> = {
        draft: 0,
        waiting_for_supervisor: 1,
        waiting_for_department: 2,
        in_progress: 3,
        completed: 4,
      };

      const statusDelta = statusPriority[left.workflowStatus] - statusPriority[right.workflowStatus];
      if (statusDelta !== 0) {
        return statusDelta;
      }

      return toEpoch(right.createdAt) - toEpoch(left.createdAt);
    })
    .slice(0, 5);

  return {
    heading: "HR auf einen Blick",
    nextStep: selectedWorkflowDefinition
      ? `${selectedWorkflowDefinition.name}-Fälle in Startphase und Rücklauf prüfen.`
      : "Startphase und Rückläufe prüfen.",
    stats: [
      { label: "Offene Vorgänge", value: metrics.open, note: "laufend", statusLabel: "Offen", tone: "neutral" },
      {
        label: "Wartet auf Abteilungsleitung",
        value: metrics.waitingSupervisor,
        note: "Rückmeldung fehlt",
        statusLabel: "wartet auf Abteilungsleitung",
        tone: "attention",
      },
      {
        label: "In Bearbeitung in den Fachbereichen",
        value: departmentInProgress,
        note: "in Fachbereichen",
        statusLabel: "in Bearbeitung",
        tone: "progress",
      },
      { label: "Abgeschlossen", value: metrics.completed, note: "fertig", statusLabel: "abgeschlossen", tone: "success" },
    ],
    queueTitle: workflowDefinitionContext.scopedTitle,
    queueItems: activeWorkflows.map((workflow) => ({
      key: workflow.uid,
      title: `${workflow.firstName} ${workflow.lastName}`.trim() || "Unbekannter Mitarbeitender",
      detail: getWorkflowRuntimeStatusLabel(workflow.workflowStatus, "action"),
      to: `/workflows/${workflow.uid}`,
      actionLabel: "Öffnen",
    })),
    emptyQueueText: workflowDefinitionContext.scopedEmptyQueueText,
  };
}

export async function loadManagerInsights(options: DashboardInsightsOptions = {}): Promise<DashboardInsights> {
  const selectedWorkflowDefinition = options.selectedWorkflowDefinition ?? null;
  const nowEpoch = Date.now();
  const [supervisorWorkflows, visibleWorkflows] = await Promise.all([
    getSupervisorStepWorkflows(),
    getWorkflows({ workflowDefinitionKey: options.workflowDefinitionKey ?? null }),
  ]);
  const filteredSupervisorWorkflows = options.workflowDefinitionKey
    ? supervisorWorkflows.filter((workflow) => workflow.processType.key === options.workflowDefinitionKey)
    : supervisorWorkflows;
  const relevantWorkflows = visibleWorkflows.filter(
    (workflow) => !isWorkflowTerminalStatus(workflow.workflowStatus) || isRecentlyCompletedWorkflow(workflow, nowEpoch)
  );
  const waitingForSupervisor = filteredSupervisorWorkflows;
  const activeWorkflows = relevantWorkflows.filter((workflow) => !isWorkflowTerminalStatus(workflow.workflowStatus));
  const recentlyCompletedWorkflows = relevantWorkflows.filter((workflow) => workflow.workflowStatus === "completed");
  const pendingSelections = waitingForSupervisor.reduce(
    (count, workflow) => count + workflow.requirementSummary.pendingVisibleCount,
    0
  );
  const visibleDepartmentCount = new Set(relevantWorkflows.map((workflow) => workflow.departmentId)).size;
  const shouldShowDepartmentName = visibleDepartmentCount > 1;

  const employeeItems = relevantWorkflows
    .slice()
    .sort((left, right) => {
      const priorityDelta = getManagerWorkflowPriority(left.workflowStatus) - getManagerWorkflowPriority(right.workflowStatus);
      if (priorityDelta !== 0) {
        return priorityDelta;
      }

      if (left.workflowStatus === "completed" && right.workflowStatus === "completed") {
        return toEpoch(right.completedAt ?? right.createdAt) - toEpoch(left.completedAt ?? left.createdAt);
      }

      return toEpoch(right.createdAt) - toEpoch(left.createdAt);
    })
    .slice(0, 8)
    .map((workflow) => {
      const action = getManagerWorkflowAction(workflow);
      const isCompleted = workflow.workflowStatus === "completed";

      return {
        key: workflow.uid,
        name: `${workflow.firstName} ${workflow.lastName}`.trim() || "Unbekannter Mitarbeitender",
        roleName: workflow.roleName,
        departmentName: shouldShowDepartmentName ? workflow.departmentName : null,
        processTypeName: workflow.processType.name,
        workflowStatus: workflow.workflowStatus,
        contextText: getManagerWorkflowContextText(workflow),
        dateLabel: isCompleted ? "Abgeschlossen" : "Gestartet",
        dateValue: formatDate(isCompleted ? workflow.completedAt : workflow.createdAt),
        to: action.to,
        actionLabel: action.actionLabel,
      };
    });

  const compactSummaryText = waitingForSupervisor.length > 0
    ? `${waitingForSupervisor.length} Vorgänge warten auf Ihre Rückmeldung.`
    : activeWorkflows.length > 0
      ? `${activeWorkflows.length} aktive Vorgänge in Ihren Abteilungen.`
      : recentlyCompletedWorkflows.length > 0
        ? `${recentlyCompletedWorkflows.length} kürzlich abgeschlossene Vorgänge in Ihren Abteilungen.`
        : "Aktuell gibt es keine aktiven oder kürzlich abgeschlossenen Vorgänge in Ihren Abteilungen.";

  const queueItems: DashboardQueueItem[] = waitingForSupervisor.length > 0
    ? [
        {
          key: "manager-supervisor-summary",
          title: compactSummaryText,
          detail: selectedWorkflowDefinition
            ? `Öffnen Sie die offenen ${selectedWorkflowDefinition.name}-Fälle im Leitungs-Schritt.`
            : "Öffnen Sie die offenen Fälle im Leitungs-Schritt.",
          to: "/supervisor",
          actionLabel: "Freigaben öffnen",
        },
      ]
    : [];

  return {
    heading: "Vorgänge meiner Mitarbeitenden",
    nextStep: waitingForSupervisor.length > 0
      ? "Offene Freigaben zuerst bearbeiten."
      : activeWorkflows.length > 0
        ? "Laufende Vorgänge Ihrer Mitarbeitenden im Blick behalten."
        : recentlyCompletedWorkflows.length > 0
          ? "Kürzlich abgeschlossene Vorgänge prüfen."
          : "Aktuell sind keine relevanten Vorgänge offen.",
    stats: [
      { label: "Offene Anforderungen", value: pendingSelections, note: "offene Auswahlpunkte", tone: "attention" },
      { label: "Wartet auf Sie", value: waitingForSupervisor.length, note: "Leitungs-Schritt", tone: "attention" },
      {
        label: "Aktive Vorgänge",
        value: activeWorkflows.length,
        note: selectedWorkflowDefinition ? `${selectedWorkflowDefinition.name}-Fälle` : "in Ihren Abteilungen",
        tone: "progress",
      },
      {
        label: "Kürzlich abgeschlossen",
        value: recentlyCompletedWorkflows.length,
        note: `letzte ${RECENT_COMPLETION_WINDOW_DAYS} Tage`,
        tone: "success",
      },
    ],
    queueTitle: selectedWorkflowDefinition ? `Mitarbeitende (${selectedWorkflowDefinition.name})` : "Mitarbeitende",
    queueItems,
    emptyQueueText: compactSummaryText,
    employeeListTitle: selectedWorkflowDefinition ? `Mitarbeitende (${selectedWorkflowDefinition.name})` : "Mitarbeitende",
    employeeListDescription: "Aktive und kürzlich abgeschlossene Vorgänge Ihrer sichtbaren Abteilungen.",
    employeeItems,
  };
}

export async function loadWorkerInsights(): Promise<DashboardInsights> {
  const tasksWithWorkflow = await getMyTasks();
  const openTasks: Array<{
    taskId: number;
    workflowUid: string;
    workflowDisplayName: string;
    taskTitle: string;
    taskStatus: WorkflowTask["status"];
  }> = [];
  let openTaskCount = 0;
  let inProgressTaskCount = 0;
  let doneTaskCount = 0;

  for (const item of tasksWithWorkflow) {
    const workflowUid = item.workflow?.workflowUid ?? item.taskRef;
    const workflowDisplayName = item.workflow
      ? `${item.workflow.firstName} ${item.workflow.lastName}`.trim() || "Unbekannter Mitarbeitender"
      : item.rotation?.displayName ?? "Unbekannter Mitarbeitender";

    if (isOpenTask(item.task)) {
      openTasks.push({
        taskId: item.task.id,
        workflowUid,
        workflowDisplayName,
        taskTitle: item.task.title,
        taskStatus: item.task.status,
      });
      openTaskCount += 1;
    }

    if (item.task.status === "in_progress") {
      inProgressTaskCount += 1;
    }

    if (item.task.status === "done") {
      doneTaskCount += 1;
    }
  }

  const statusPriority: Record<WorkflowTask["status"], number> = {
    blocked: 0,
    in_progress: 1,
    ready: 2,
    open: 3,
    done: 4,
    completed: 4,
    failed: 5,
    cancelled: 6,
  };

  const queueItems = openTasks
    .slice()
    .sort((left, right) => {
      const statusDelta = statusPriority[left.taskStatus] - statusPriority[right.taskStatus];
      if (statusDelta !== 0) {
        return statusDelta;
      }

      return left.workflowDisplayName.localeCompare(right.workflowDisplayName, "de");
    })
    .slice(0, 6)
    .map((task) => ({
      key: `${task.workflowUid}:${task.taskId}`,
      title: task.taskTitle,
      detail: getTaskStatusLabel(task.taskStatus),
      to: "/tasks/my",
      actionLabel: "Aufgaben",
    }));

  return {
    heading: "Meine Aufgaben",
    nextStep: "Blockierte und laufende Aufgaben zuerst prüfen.",
    stats: [
      { label: "Meine offenen Aufgaben", value: openTaskCount, note: "offen" },
      { label: "In Bearbeitung", value: inProgressTaskCount, note: "läuft" },
      { label: "Erledigt", value: doneTaskCount, note: "erledigt" },
    ],
    queueTitle: "Danach relevant",
    queueItems,
    emptyQueueText: "Aktuell sind keine offenen Aufgaben vorhanden.",
  };
}

export async function loadAdminInsights(options: DashboardInsightsOptions = {}): Promise<DashboardInsights> {
  const [users, roles, groups, workflows] = await Promise.all([
    getAdminUsers(),
    getAdminRoles(),
    getAdminGroups(),
    getWorkflows({ workflowDefinitionKey: options.workflowDefinitionKey ?? null }),
  ]);
  const selectedWorkflowDefinition = options.selectedWorkflowDefinition ?? null;
  const activeUsers = users.filter((user) => user.isActive).length;
  const inactiveUsers = users.length - activeUsers;
  const groupsWithoutRoles = groups.filter((group) => group.roles.length === 0).length;
  const metrics = summarizeWorkflows(workflows);
  const queueItems: DashboardQueueItem[] = [
    {
      key: "admin-config",
      title: "Stammdaten und Rechte pflegen",
      detail: "Pflege offen",
      to: "/admin/config",
      actionLabel: "Verwaltung",
    },
  ];

  if (groupsWithoutRoles > 0) {
    queueItems.push({
      key: "groups-without-roles",
      title: "Gruppen ohne Rollen prüfen",
      detail: `${groupsWithoutRoles} ohne Rollen`,
      to: "/admin/config",
      actionLabel: "Gruppen",
    });
  }

  if (inactiveUsers > 0) {
    queueItems.push({
      key: "inactive-users",
      title: "Inaktive Benutzer verifizieren",
      detail: `${inactiveUsers} inaktiv`,
      to: "/admin/config",
      actionLabel: "Benutzer",
    });
  }

  if (metrics.waitingSupervisor > 0 || metrics.waitingDepartment > 0) {
    queueItems.push({
      key: "workflow-bottlenecks",
      title: "Prozess-Engpässe verfolgen",
      detail: `${metrics.waitingSupervisor + metrics.waitingDepartment} warten`,
      to: "/workflows",
      actionLabel: "Übersicht",
    });
  }

  return {
    heading: "Verwaltung",
    nextStep: selectedWorkflowDefinition
      ? `Stammdaten, Rechte und Engpässe für ${selectedWorkflowDefinition.name} prüfen.`
      : "Stammdaten, Rechte und Engpässe prüfen.",
    stats: [
      { label: "Benutzer", value: users.length, note: `${activeUsers} aktiv, ${inactiveUsers} inaktiv` },
      { label: "Rollen", value: roles.length, note: "hinterlegt" },
      { label: "Gruppen", value: groups.length, note: `${groupsWithoutRoles} ohne Rollen` },
      { label: "Vorgänge gesamt", value: metrics.total, note: "gesamt" },
    ],
    queueTitle: "Danach relevant",
    queueItems,
    emptyQueueText: "Es sind aktuell keine administrativen Prüfpunkte vorhanden.",
  };
}

export async function loadViewerInsights(options: DashboardInsightsOptions = {}): Promise<DashboardInsights> {
  const selectedWorkflowDefinition = options.selectedWorkflowDefinition ?? null;
  const workflowDefinitionContext = getWorkflowDefinitionContext(selectedWorkflowDefinition);
  const workflows = await getWorkflows({ workflowDefinitionKey: options.workflowDefinitionKey ?? null });
  const metrics = summarizeWorkflows(workflows);
  const queueItems = workflows
    .slice()
    .sort((left, right) => toEpoch(right.createdAt) - toEpoch(left.createdAt))
    .slice(0, 5)
    .map((workflow) => ({
      key: workflow.uid,
      title: `${workflow.firstName} ${workflow.lastName}`.trim() || "Unbekannter Mitarbeitender",
      detail: getWorkflowRuntimeStatusLabel(workflow.workflowStatus, "action"),
      to: `/workflows/${workflow.uid}`,
      actionLabel: "Ansehen",
    }));

  return {
    heading: "Übersicht",
    nextStep: "Aktuellen Stand prüfen.",
    stats: [
      { label: "Vorgänge gesamt", value: metrics.total, note: "freigegeben" },
      { label: "Offen", value: metrics.open, note: "noch offen" },
      { label: "Abgeschlossen", value: metrics.completed, note: "fertig" },
    ],
    queueTitle: workflowDefinitionContext.scopedTitle,
    queueItems,
    emptyQueueText: workflowDefinitionContext.scopedEmptyQueueText,
  };
}

export function loadGenericInsights(): DashboardInsights {
  return {
    heading: "Übersicht",
    nextStep: "Freigegebenen Bereich wählen.",
    stats: [],
    queueTitle: "Danach relevant",
    queueItems: [],
    emptyQueueText: "Keine rollenspezifischen Aufgaben vorhanden.",
  };
}
