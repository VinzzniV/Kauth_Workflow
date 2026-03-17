import type { DashboardPersona } from "../../auth/roleModel";
import {
  getAdminGroups,
  getAdminRoles,
  getAdminUsers,
  getMyTasks,
  getSupervisorStepWorkflows,
  getWorkflows,
  getWorkflowSupervisorStep,
} from "../../services/onboardingApi";
import type {
  WorkflowRequirementSnapshot,
  WorkflowSummary,
  WorkflowTask,
} from "../../types/workflow";
import { getVisibleRequirements, hasRequirementAnswer } from "../../utils/requirements";
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

export type DashboardInsights = {
  heading: string;
  summary: string;
  nextStep: string;
  stats: DashboardStat[];
  queueTitle: string;
  queueDescription: string;
  queueItems: DashboardQueueItem[];
  emptyQueueText: string;
};

type WorkflowMetrics = {
  total: number;
  open: number;
  waitingSupervisor: number;
  waitingDepartment: number;
  inProgress: number;
  completed: number;
  cancelled: number;
};

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
      if (workflow.workflowStatus === "cancelled") {
        acc.cancelled += 1;
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
      cancelled: 0,
    }
  );
}

function countPendingSelections(requirements: WorkflowRequirementSnapshot[]): number {
  const relevantRequirements = getVisibleRequirements(requirements);
  return relevantRequirements.filter((requirement) => !hasRequirementAnswer(requirement)).length;
}

async function loadHrInsights(): Promise<DashboardInsights> {
  const workflows = await getWorkflows();
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
        cancelled: 5,
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
    summary: "Hier sehen Sie die HR-Startphase, offene Rückläufe und die laufende Bearbeitung in den Fachbereichen.",
    nextStep: "Starten Sie ein neues Onboarding oder prüfen Sie Fälle in der HR-Startphase und mit offenem Rücklauf.",
    stats: [
      {
        label: "Offene Onboardings",
        value: metrics.open,
        note: "Alle aktuell laufenden Vorgänge.",
        statusLabel: "Offen",
        tone: "neutral",
      },
      {
        label: "Wartet auf Abteilungsleitung",
        value: metrics.waitingSupervisor,
        note: "Hier fehlen noch Angaben der Abteilungsleitung.",
        statusLabel: "wartet auf Abteilungsleitung",
        tone: "attention",
      },
      {
        label: "In Bearbeitung in den Fachbereichen",
        value: departmentInProgress,
        note: "Diese Fälle werden aktuell in den Bereichen bearbeitet.",
        statusLabel: "in Bearbeitung",
        tone: "progress",
      },
      {
        label: "Abgeschlossen",
        value: metrics.completed,
        note: "Diese Onboardings sind fertig.",
        statusLabel: "abgeschlossen",
        tone: "success",
      },
    ],
    queueTitle: "Offene Onboardings",
    queueDescription: "Diese Fälle haben aktuell den nächsten Handlungsbedarf.",
    queueItems: activeWorkflows.map((workflow) => ({
      key: workflow.uid,
      title: `${workflow.firstName} ${workflow.lastName}`.trim() || "Unbekannter Mitarbeitender",
      detail: `${workflow.departmentName} | ${getWorkflowRuntimeStatusLabel(workflow.workflowStatus, "action")}`,
      to: `/workflows/${workflow.uid}`,
      actionLabel: "Öffnen",
    })),
    emptyQueueText: "Es sind noch keine Onboarding-Fälle vorhanden.",
  };
}

async function loadManagerInsights(): Promise<DashboardInsights> {
  const workflows = await getSupervisorStepWorkflows();
  const pendingSelectionsByWorkflow = new Map<string, number>();

  let pendingSelections = 0;
  let unresolvedWorkflowCount = 0;
  let requirementLoadFailures = 0;

  if (workflows.length > 0) {
    const requirementResults = await Promise.allSettled(
      workflows.map(async (workflow) => {
        const requirements = await getWorkflowSupervisorStep(workflow.uid);
        return {
          uid: workflow.uid,
          pendingSelections: countPendingSelections(requirements),
        };
      })
    );

    for (const result of requirementResults) {
      if (result.status !== "fulfilled") {
        requirementLoadFailures += 1;
        continue;
      }

      pendingSelectionsByWorkflow.set(result.value.uid, result.value.pendingSelections);
      pendingSelections += result.value.pendingSelections;
      if (result.value.pendingSelections > 0) {
        unresolvedWorkflowCount += 1;
      }
    }
  }

  const queueItems = workflows
    .slice()
    .sort((left, right) => toEpoch(left.createdAt) - toEpoch(right.createdAt))
    .slice(0, 5)
    .map((workflow) => {
      const workflowPendingSelections = pendingSelectionsByWorkflow.get(workflow.uid);
      const selectionText =
        typeof workflowPendingSelections === "number"
          ? `${workflowPendingSelections} offene Auswahlpunkte`
          : "Auswahlpunkte werden geladen";

      return {
        key: workflow.uid,
        title: `${workflow.firstName} ${workflow.lastName}`.trim() || "Unbekannter Mitarbeitender",
        detail: `${workflow.departmentName} | ${selectionText}`,
        to: "/supervisor",
        actionLabel: "Zur Auswahl",
      };
    });

  return {
    heading: "Meine offenen Anforderungen",
    summary: "Diese Onboardings warten noch auf Ihre Rückmeldung.",
    nextStep: "Öffnen Sie den nächsten offenen Fall und vervollständigen Sie die Angaben.",
    stats: [
      {
        label: "Offene Anforderungen",
        value: pendingSelections,
        note: "Noch nicht beantwortete Auswahlpunkte.",
      },
      {
        label: "Noch zu bearbeitende Onboardings",
        value: unresolvedWorkflowCount > 0 ? unresolvedWorkflowCount : workflows.length,
        note: "Diese Fälle warten noch auf Ihre Rückmeldung.",
      },
    ],
    queueTitle: "Offene Onboardings",
    queueDescription:
      requirementLoadFailures > 0
        ? "Einige Auswahlpunkte konnten nicht geladen werden."
        : "Bearbeiten Sie zuerst die ältesten offenen Onboardings.",
    queueItems,
    emptyQueueText: "Aktuell warten keine Onboardings auf Eingaben durch die Abteilungsleitung.",
  };
}

function isOpenTask(task: Pick<WorkflowTask, "status">): boolean {
  return task.status === "open" || task.status === "ready" || task.status === "in_progress" || task.status === "blocked";
}

async function loadWorkerInsights(): Promise<DashboardInsights> {
  const tasksWithWorkflow = await getMyTasks();

  type OpenTaskRecord = {
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
    skipped: 5,
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
      key: `${task.workflowUid}:${task.taskTitle}`,
      title: task.taskTitle,
      detail: `${task.workflowDisplayName} | ${getTaskStatusLabel(task.taskStatus)}`,
      to: "/tasks/my",
      actionLabel: "Aufgaben",
    }));

  return {
    heading: "Meine Aufgaben",
    summary: "Hier sehen Sie sofort, was in Ihren Fachbereichen offen ist, was bereits läuft und was erledigt ist.",
    nextStep: "Bearbeiten Sie zuerst offene Aufgaben und führen Sie begonnene Aufgaben zu Ende.",
    stats: [
      {
        label: "Meine offenen Aufgaben",
        value: openTaskCount,
        note: "Noch nicht erledigte Aufgaben.",
      },
      {
        label: "In Bearbeitung",
        value: inProgressTaskCount,
        note: "Aktuell laufende Aufgaben.",
      },
      {
        label: "Erledigt",
        value: doneTaskCount,
        note: "Bereits erledigte Aufgaben.",
      },
    ],
    queueTitle: "Als Nächstes",
    queueDescription: "Beginnen Sie mit blockierten oder bereits laufenden Aufgaben.",
    queueItems,
    emptyQueueText: "Aktuell sind keine offenen Aufgaben vorhanden.",
  };
}

async function loadAdminInsights(): Promise<DashboardInsights> {
  const [users, roles, groups, workflows] = await Promise.all([
    getAdminUsers(),
    getAdminRoles(),
    getAdminGroups(),
    getWorkflows(),
  ]);

  const activeUsers = users.filter((user) => user.isActive).length;
  const inactiveUsers = users.length - activeUsers;
  const groupsWithoutRoles = groups.filter((group) => group.roles.length === 0).length;
  const metrics = summarizeWorkflows(workflows);

  const queueItems: DashboardQueueItem[] = [
    {
      key: "admin-config",
      title: "Stammdaten und Rechte pflegen",
      detail: "Verwaltung von Personen, Rollen, Gruppen und Zuständigkeiten.",
      to: "/admin/config",
      actionLabel: "Verwaltung",
    },
  ];

  if (groupsWithoutRoles > 0) {
    queueItems.push({
      key: "groups-without-roles",
      title: "Gruppen ohne Rollen prüfen",
      detail: `${groupsWithoutRoles} Gruppe(n) haben aktuell keine Rollen.`,
      to: "/admin/config",
      actionLabel: "Gruppen",
    });
  }

  if (inactiveUsers > 0) {
    queueItems.push({
      key: "inactive-users",
      title: "Inaktive Benutzer verifizieren",
      detail: `${inactiveUsers} Benutzer sind deaktiviert.`,
      to: "/admin/config",
      actionLabel: "Benutzer",
    });
  }

  if (metrics.waitingSupervisor > 0 || metrics.waitingDepartment > 0) {
    queueItems.push({
      key: "workflow-bottlenecks",
      title: "Onboarding-Engpässe verfolgen",
      detail: `${metrics.waitingSupervisor + metrics.waitingDepartment} Onboardings warten auf den nächsten Schritt.`,
      to: "/workflows",
      actionLabel: "Übersicht",
    });
  }

  return {
    heading: "Verwaltung",
    summary: "Die wichtigsten Verwaltungs- und Prozesskennzahlen im Überblick.",
    nextStep: "Prüfen Sie zuerst Stammdaten und Berechtigungen, danach Engpässe im Ablauf.",
    stats: [
      {
        label: "Benutzer",
        value: users.length,
        note: `${activeUsers} aktiv / ${inactiveUsers} inaktiv`,
      },
      {
        label: "Rollen",
        value: roles.length,
        note: "Anzahl hinterlegter Rollen.",
      },
      {
        label: "Gruppen",
        value: groups.length,
        note: `${groupsWithoutRoles} ohne Rollen`,
      },
      {
        label: "Onboardings gesamt",
        value: metrics.total,
        note: "Systemweite Prozessanzahl.",
      },
    ],
    queueTitle: "Nächste Schritte in der Verwaltung",
    queueDescription: "Kurzliste der wichtigsten Prüfpunkte.",
    queueItems,
    emptyQueueText: "Es sind aktuell keine administrativen Prüfpunkte vorhanden.",
  };
}

async function loadViewerInsights(): Promise<DashboardInsights> {
  const workflows = await getWorkflows();
  const metrics = summarizeWorkflows(workflows);

  const queueItems = workflows
    .slice()
    .sort((left, right) => toEpoch(right.createdAt) - toEpoch(left.createdAt))
    .slice(0, 5)
    .map((workflow) => ({
      key: workflow.uid,
      title: `${workflow.firstName} ${workflow.lastName}`.trim() || "Unbekannter Mitarbeitender",
      detail: `${workflow.departmentName} | ${getWorkflowRuntimeStatusLabel(workflow.workflowStatus, "action")}`,
      to: `/workflows/${workflow.uid}`,
      actionLabel: "Ansehen",
    }));

  return {
    heading: "Übersicht",
    summary: "Lesender Überblick über den aktuellen Stand aller freigegebenen Onboardings.",
    nextStep: "Nutzen Sie die Übersicht zur Nachverfolgung, ohne Daten zu ändern.",
    stats: [
      {
        label: "Onboardings gesamt",
        value: metrics.total,
        note: "Alle freigegebenen Lesedaten.",
      },
      {
        label: "Offen",
        value: metrics.open,
        note: "Noch nicht abgeschlossen.",
      },
      {
        label: "Abgeschlossen",
        value: metrics.completed,
        note: "Bereits abgeschlossen.",
      },
      {
        label: "Beendet",
        value: metrics.cancelled,
        note: "Vorzeitig beendet.",
      },
    ],
    queueTitle: "Aktuelle Onboardings",
    queueDescription: "Die zuletzt geänderten Onboardings in der Leseansicht.",
    queueItems,
    emptyQueueText: "Aktuell sind keine Onboardings sichtbar.",
  };
}

function loadGenericInsights(): DashboardInsights {
  return {
    heading: "Übersicht",
    summary: "Hier sehen Sie den nächsten sinnvollen Schritt für Ihre Rolle.",
    nextStep: "Wählen Sie einen freigegebenen Bereich aus den Hauptaktionen.",
    stats: [],
    queueTitle: "Nächste Schritte",
    queueDescription: "Keine weiteren rollenspezifischen Werte verfügbar.",
    queueItems: [],
    emptyQueueText: "Keine rollenspezifischen Aufgaben vorhanden.",
  };
}

export async function loadDashboardInsights(dashboardPersona: DashboardPersona): Promise<DashboardInsights> {
  switch (dashboardPersona) {
    case "admin":
      return loadAdminInsights();
    case "hr":
      return loadHrInsights();
    case "manager":
      return loadManagerInsights();
    case "worker":
      return loadWorkerInsights();
    case "reader":
      return loadViewerInsights();
    default:
      return loadGenericInsights();
  }
}
