import type { DashboardPersona } from "../../auth/roleModel";
import {
  getAdminGroups,
  getAdminRoles,
  getAdminUsers,
} from "../../services/adminApi";
import { getMyTasks } from "../../services/taskApi";
import { getWorkflows } from "../../services/workflowApi";
import type {
  ProcessType,
  WorkflowSummary,
  WorkflowTask,
} from "../../types/workflow";
import { formatDate } from "../../utils/dateFormat";
import { getTaskStatusLabel } from "../../utils/taskStatus";
import {
  getWorkflowRuntimeStatusLabel,
  isWorkflowTerminalStatus,
} from "../../utils/workflowStatus";

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

type WorkflowMetrics = {
  total: number;
  open: number;
  waitingSupervisor: number;
  waitingDepartment: number;
  inProgress: number;
  completed: number;
};

const RECENT_COMPLETION_WINDOW_DAYS = 30;

function toEpoch(value: string): number {
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return 0;
  }

  return parsed.getTime();
}

function summarizeWorkflows(workflows: WorkflowSummary[]): WorkflowMetrics {
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

function getProcessTypeContext(selectedProcessType: ProcessType | null) {
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

function isRecentlyCompletedWorkflow(workflow: WorkflowSummary, nowEpoch: number): boolean {
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

function getManagerWorkflowPriority(status: WorkflowSummary["workflowStatus"]): number {
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

function getManagerWorkflowContextText(workflow: WorkflowSummary): string {
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

function getManagerWorkflowAction(workflow: WorkflowSummary): Pick<DashboardEmployeeItem, "to" | "actionLabel"> {
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

async function loadHrInsights(options: DashboardInsightsOptions = {}): Promise<DashboardInsights> {
  const selectedProcessType = options.selectedProcessType ?? null;
  const processTypeContext = getProcessTypeContext(selectedProcessType);
  const workflows = await getWorkflows({ processTypeKey: options.processTypeKey ?? null });
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
    nextStep: selectedProcessType
      ? `${selectedProcessType.name}-Fälle in Startphase und Rücklauf prüfen.`
      : "Startphase und Rückläufe prüfen.",
    stats: [
      {
        label: "Offene Vorgänge",
        value: metrics.open,
        note: "laufend",
        statusLabel: "Offen",
        tone: "neutral",
      },
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
      {
        label: "Abgeschlossen",
        value: metrics.completed,
        note: "fertig",
        statusLabel: "abgeschlossen",
        tone: "success",
      },
    ],
    queueTitle: processTypeContext.scopedTitle,
    queueItems: activeWorkflows.map((workflow) => ({
      key: workflow.uid,
      title: `${workflow.firstName} ${workflow.lastName}`.trim() || "Unbekannter Mitarbeitender",
      detail: getWorkflowRuntimeStatusLabel(workflow.workflowStatus, "action"),
      to: `/workflows/${workflow.uid}`,
      actionLabel: "Öffnen",
    })),
    emptyQueueText: processTypeContext.scopedEmptyQueueText,
  };
}

async function loadManagerInsights(options: DashboardInsightsOptions = {}): Promise<DashboardInsights> {
  const selectedProcessType = options.selectedProcessType ?? null;
  const nowEpoch = Date.now();
  const visibleWorkflows = await getWorkflows({ processTypeKey: options.processTypeKey ?? null });
  const relevantWorkflows = visibleWorkflows.filter(
    (workflow) => !isWorkflowTerminalStatus(workflow.workflowStatus) || isRecentlyCompletedWorkflow(workflow, nowEpoch)
  );
  const waitingForSupervisor = relevantWorkflows.filter((workflow) => workflow.workflowStatus === "waiting_for_supervisor");
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
          detail: selectedProcessType
            ? `Öffnen Sie die offenen ${selectedProcessType.name}-Fälle im Leitungs-Schritt.`
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
      {
        label: "Offene Anforderungen",
        value: pendingSelections,
        note: "offene Auswahlpunkte",
        tone: "attention",
      },
      {
        label: "Wartet auf Sie",
        value: waitingForSupervisor.length,
        note: "Leitungs-Schritt",
        tone: "attention",
      },
      {
        label: "Aktive Vorgänge",
        value: activeWorkflows.length,
        note: selectedProcessType
          ? `${selectedProcessType.name}-Fälle`
          : "in Ihren Abteilungen",
        tone: "progress",
      },
      {
        label: "Kürzlich abgeschlossen",
        value: recentlyCompletedWorkflows.length,
        note: selectedProcessType
          ? `letzte ${RECENT_COMPLETION_WINDOW_DAYS} Tage`
          : `letzte ${RECENT_COMPLETION_WINDOW_DAYS} Tage`,
        tone: "success",
      },
    ],
    queueTitle: selectedProcessType ? `Mitarbeitende (${selectedProcessType.name})` : "Mitarbeitende",
    queueItems,
    emptyQueueText: compactSummaryText,
    employeeListTitle: selectedProcessType ? `Mitarbeitende (${selectedProcessType.name})` : "Mitarbeitende",
    employeeListDescription: "Aktive und kürzlich abgeschlossene Vorgänge Ihrer sichtbaren Abteilungen.",
    employeeItems,
  };
}

function isOpenTask(task: Pick<WorkflowTask, "status">): boolean {
  return task.status === "open" || task.status === "ready" || task.status === "in_progress" || task.status === "blocked";
}

async function loadWorkerInsights(): Promise<DashboardInsights> {
  const tasksWithWorkflow = await getMyTasks();

  type OpenTaskRecord = {
    taskId: number;
    workflowUid: string;
    workflowDisplayName: string;
    taskTitle: string;
    taskStatus: WorkflowTask["status"];
  };

  const openTasks: OpenTaskRecord[] = [];
  let openTaskCount = 0;
  let inProgressTaskCount = 0;
  let doneTaskCount = 0;

  for (const item of tasksWithWorkflow) {
    const workflowUid = item.workflow.workflowUid;
    const workflowDisplayName =
      `${item.workflow.firstName} ${item.workflow.lastName}`.trim() || "Unbekannter Mitarbeitender";

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
      {
        label: "Meine offenen Aufgaben",
        value: openTaskCount,
        note: "offen",
      },
      {
        label: "In Bearbeitung",
        value: inProgressTaskCount,
        note: "läuft",
      },
      {
        label: "Erledigt",
        value: doneTaskCount,
        note: "erledigt",
      },
    ],
    queueTitle: "Danach relevant",
    queueItems,
    emptyQueueText: "Aktuell sind keine offenen Aufgaben vorhanden.",
  };
}

async function loadAdminInsights(options: DashboardInsightsOptions = {}): Promise<DashboardInsights> {
  const [users, roles, groups, workflows] = await Promise.all([
    getAdminUsers(),
    getAdminRoles(),
    getAdminGroups(),
    getWorkflows({ processTypeKey: options.processTypeKey ?? null }),
  ]);
  const selectedProcessType = options.selectedProcessType ?? null;

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
    nextStep: selectedProcessType
      ? `Stammdaten, Rechte und Engpässe für ${selectedProcessType.name} prüfen.`
      : "Stammdaten, Rechte und Engpässe prüfen.",
    stats: [
      {
        label: "Benutzer",
        value: users.length,
        note: `${activeUsers} aktiv, ${inactiveUsers} inaktiv`,
      },
      {
        label: "Rollen",
        value: roles.length,
        note: "hinterlegt",
      },
      {
        label: "Gruppen",
        value: groups.length,
        note: `${groupsWithoutRoles} ohne Rollen`,
      },
      {
        label: "Vorgänge gesamt",
        value: metrics.total,
        note: "gesamt",
      },
    ],
    queueTitle: "Danach relevant",
    queueItems,
    emptyQueueText: "Es sind aktuell keine administrativen Prüfpunkte vorhanden.",
  };
}

async function loadViewerInsights(options: DashboardInsightsOptions = {}): Promise<DashboardInsights> {
  const selectedProcessType = options.selectedProcessType ?? null;
  const processTypeContext = getProcessTypeContext(selectedProcessType);
  const workflows = await getWorkflows({ processTypeKey: options.processTypeKey ?? null });
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
      {
        label: "Vorgänge gesamt",
        value: metrics.total,
        note: "freigegeben",
      },
      {
        label: "Offen",
        value: metrics.open,
        note: "noch offen",
      },
      {
        label: "Abgeschlossen",
        value: metrics.completed,
        note: "fertig",
      },
    ],
    queueTitle: processTypeContext.scopedTitle,
    queueItems,
    emptyQueueText: processTypeContext.scopedEmptyQueueText,
  };
}

function loadGenericInsights(): DashboardInsights {
  return {
    heading: "Übersicht",
    nextStep: "Freigegebenen Bereich wählen.",
    stats: [],
    queueTitle: "Danach relevant",
    queueItems: [],
    emptyQueueText: "Keine rollenspezifischen Aufgaben vorhanden.",
  };
}

export async function loadDashboardInsights(
  dashboardPersona: DashboardPersona,
  options: DashboardInsightsOptions = {}
): Promise<DashboardInsights> {
  switch (dashboardPersona) {
    case "admin":
      return loadAdminInsights(options);
    case "hr":
      return loadHrInsights(options);
    case "manager":
      return loadManagerInsights(options);
    case "worker":
      return loadWorkerInsights();
    case "reader":
      return loadViewerInsights(options);
    default:
      return loadGenericInsights();
  }
}
