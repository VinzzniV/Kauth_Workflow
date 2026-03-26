import type { DashboardPersona } from "../../auth/roleModel";
import {
  getAdminGroups,
  getAdminRoles,
  getAdminUsers,
  getProcessTypes,
  getMyTasks,
  getSupervisorStepWorkflows,
  getWorkflows,
} from "../../services/lifecycleApi";
import type {
  ProcessType,
  WorkflowSummary,
  WorkflowTask,
} from "../../types/workflow";
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

export type DashboardInsightsOptions = {
  processTypeKey?: string | null;
};

type WorkflowMetrics = {
  total: number;
  open: number;
  waitingSupervisor: number;
  waitingDepartment: number;
  inProgress: number;
  completed: number;
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

function getSelectedProcessType(processTypes: ProcessType[], processTypeKey?: string | null): ProcessType | null {
  if (!processTypeKey) {
    return null;
  }

  return processTypes.find((processType) => processType.key === processTypeKey) ?? null;
}

function matchesProcessType(processTypeKey: string | null | undefined, workflow: Pick<WorkflowSummary, "processType">): boolean {
  if (!processTypeKey) {
    return true;
  }

  return workflow.processType.key === processTypeKey;
}

function getProcessTypeContext(selectedProcessType: ProcessType | null) {
  if (!selectedProcessType) {
    return {
      scopedTitle: "Vorgänge",
      scopedDescription: "alle sichtbaren Mitarbeiterprozesse",
      scopedQueueDescription: "Diese Fälle haben aktuell den nächsten Handlungsbedarf.",
      scopedEmptyQueueText: "Aktuell sind keine Vorgänge vorhanden.",
    };
  }

  return {
    scopedTitle: `Vorgänge (${selectedProcessType.name})`,
    scopedDescription: `alle sichtbaren Mitarbeiterprozesse vom Typ ${selectedProcessType.name}`,
    scopedQueueDescription: `Diese Fälle vom Typ ${selectedProcessType.name} haben aktuell den nächsten Handlungsbedarf.`,
    scopedEmptyQueueText: `Aktuell sind keine Vorgänge vom Typ ${selectedProcessType.name} vorhanden.`,
  };
}

async function loadHrInsights(options: DashboardInsightsOptions = {}): Promise<DashboardInsights> {
  const processTypes = await getProcessTypes();
  const selectedProcessType = getSelectedProcessType(processTypes, options.processTypeKey);
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
    summary: `Hier sehen Sie die HR-Startphase, offene Rückläufe und die laufende Bearbeitung in den Fachbereichen für ${processTypeContext.scopedDescription}.`,
    nextStep: selectedProcessType
      ? `Prüfen Sie zuerst ${selectedProcessType.name}-Fälle in der HR-Startphase und mit offenem Rücklauf.`
      : "Starten Sie einen neuen Vorgang oder prüfen Sie Fälle in der HR-Startphase und mit offenem Rücklauf.",
    stats: [
      {
        label: "Offene Vorgänge",
        value: metrics.open,
        note: `Alle aktuell laufenden Fälle für ${processTypeContext.scopedDescription}.`,
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
        note: `Diese Fälle sind abgeschlossen für ${processTypeContext.scopedDescription}.`,
        statusLabel: "abgeschlossen",
        tone: "success",
      },
    ],
    queueTitle: processTypeContext.scopedTitle,
    queueDescription: processTypeContext.scopedQueueDescription,
    queueItems: activeWorkflows.map((workflow) => ({
      key: workflow.uid,
      title: `${workflow.firstName} ${workflow.lastName}`.trim() || "Unbekannter Mitarbeitender",
      detail: `${workflow.processType.name} | ${workflow.departmentName} | ${getWorkflowRuntimeStatusLabel(workflow.workflowStatus, "action")}`,
      to: `/workflows/${workflow.uid}`,
      actionLabel: "Öffnen",
    })),
    emptyQueueText: processTypeContext.scopedEmptyQueueText,
  };
}

async function loadManagerInsights(options: DashboardInsightsOptions = {}): Promise<DashboardInsights> {
  const processTypes = await getProcessTypes();
  const selectedProcessType = getSelectedProcessType(processTypes, options.processTypeKey);
  const processTypeContext = getProcessTypeContext(selectedProcessType);
  const workflows = (await getSupervisorStepWorkflows()).filter((workflow) => matchesProcessType(options.processTypeKey, workflow));
  const pendingSelections = workflows.reduce(
    (count, workflow) => count + workflow.requirementSummary.pendingVisibleCount,
    0
  );

  const queueItems = workflows
    .slice()
    .sort((left, right) => toEpoch(left.createdAt) - toEpoch(right.createdAt))
    .slice(0, 5)
    .map((workflow) => {
      const selectionText = `${workflow.requirementSummary.answeredVisibleCount} von ${workflow.requirementSummary.visibleCount} beantwortet`;

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
    summary: selectedProcessType
      ? `Diese ${selectedProcessType.name}-Vorgänge warten noch auf Ihre Rückmeldung.`
      : "Diese Vorgänge warten noch auf Ihre Rückmeldung.",
    nextStep: "Öffnen Sie den nächsten offenen Fall und vervollständigen Sie die Angaben.",
    stats: [
      {
        label: "Offene Anforderungen",
        value: pendingSelections,
        note: "Noch nicht beantwortete Auswahlpunkte.",
      },
      {
        label: "Noch zu bearbeitende Vorgänge",
        value: workflows.length,
        note: selectedProcessType
          ? `Diese zugewiesenen ${selectedProcessType.name}-Fälle warten im Schritt der Abteilungsleitung.`
          : "Diese zugewiesenen Fälle warten im Schritt der Abteilungsleitung.",
      },
    ],
    queueTitle: processTypeContext.scopedTitle,
    queueDescription: selectedProcessType
      ? `Bearbeiten Sie zuerst die ältesten offenen ${selectedProcessType.name}-Fälle.`
      : "Bearbeiten Sie zuerst die ältesten offenen Vorgänge.",
    queueItems,
    emptyQueueText: selectedProcessType
      ? `Aktuell warten keine ${selectedProcessType.name}-Fälle auf Eingaben durch die Abteilungsleitung.`
      : "Aktuell warten keine Vorgänge auf Eingaben durch die Abteilungsleitung.",
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

async function loadAdminInsights(options: DashboardInsightsOptions = {}): Promise<DashboardInsights> {
  const processTypesPromise = getProcessTypes();
  const [users, roles, groups, workflows] = await Promise.all([
    getAdminUsers(),
    getAdminRoles(),
    getAdminGroups(),
    getWorkflows({ processTypeKey: options.processTypeKey ?? null }),
  ]);
  const selectedProcessType = getSelectedProcessType(await processTypesPromise, options.processTypeKey);
  const processTypeContext = getProcessTypeContext(selectedProcessType);

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
      title: "Prozess-Engpässe verfolgen",
      detail: `${metrics.waitingSupervisor + metrics.waitingDepartment} Vorgänge warten auf den nächsten Schritt.`,
      to: "/workflows",
      actionLabel: "Übersicht",
    });
  }

  return {
    heading: "Verwaltung",
    summary: `Die wichtigsten Verwaltungs- und Prozesskennzahlen im Überblick für ${processTypeContext.scopedDescription}.`,
    nextStep: selectedProcessType
      ? `Prüfen Sie zuerst Stammdaten und Berechtigungen, danach Engpässe im Ablauf für ${selectedProcessType.name}.`
      : "Prüfen Sie zuerst Stammdaten und Berechtigungen, danach Engpässe im Ablauf.",
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
        label: "Vorgänge gesamt",
        value: metrics.total,
        note: `Systemweite Prozessanzahl für ${processTypeContext.scopedDescription}.`,
      },
    ],
    queueTitle: "Nächste Schritte in der Verwaltung",
    queueDescription: "Kurzliste der wichtigsten Prüfpunkte.",
    queueItems,
    emptyQueueText: "Es sind aktuell keine administrativen Prüfpunkte vorhanden.",
  };
}

async function loadViewerInsights(options: DashboardInsightsOptions = {}): Promise<DashboardInsights> {
  const processTypes = await getProcessTypes();
  const selectedProcessType = getSelectedProcessType(processTypes, options.processTypeKey);
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
      detail: `${workflow.departmentName} | ${getWorkflowRuntimeStatusLabel(workflow.workflowStatus, "action")}`,
      to: `/workflows/${workflow.uid}`,
      actionLabel: "Ansehen",
    }));

  return {
    heading: "Übersicht",
    summary: `Lesender Überblick über den aktuellen Stand für ${processTypeContext.scopedDescription}.`,
    nextStep: "Nutzen Sie die Übersicht zur Nachverfolgung, ohne Daten zu ändern.",
    stats: [
      {
        label: "Vorgänge gesamt",
        value: metrics.total,
        note: `Alle freigegebenen Lesedaten für ${processTypeContext.scopedDescription}.`,
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
    ],
    queueTitle: processTypeContext.scopedTitle,
    queueDescription: selectedProcessType
      ? `Die zuletzt geänderten ${selectedProcessType.name}-Vorgänge in der Leseansicht.`
      : "Die zuletzt geänderten Vorgänge in der Leseansicht.",
    queueItems,
    emptyQueueText: processTypeContext.scopedEmptyQueueText,
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
