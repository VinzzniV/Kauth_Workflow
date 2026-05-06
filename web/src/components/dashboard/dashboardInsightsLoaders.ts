import {
  getAdminDepartmentAssignments,
  getAdminNotificationEmailConfiguration,
  getAdminResponsibilityOwners,
  getAdminUsers,
} from "../../services/adminApi";
import { getMyTasks } from "../../services/taskApi";
import { getSupervisorStepWorkflows, getWorkflows } from "../../services/workflowApi";
import {
  buildAdminOverviewWarnings,
  clusterAdminOverviewWarnings,
  groupAdminOverviewWarnings,
} from "../admin-config/adminWorkspaceModel";
import type { WorkflowSummary, WorkflowTask } from "../../types/workflow";
import { formatDate } from "../../utils/dateFormat";
import { getTaskStatusLabel } from "../../utils/taskStatus";
import { getWorkflowRuntimeStatusLabel, isWorkflowTerminalStatus } from "../../utils/workflowStatus";
import type {
  DashboardAdminOperationItem,
  DashboardAdminWarningCluster,
  DashboardInsights,
  DashboardInsightsOptions,
  DashboardQueueItem,
} from "./dashboardInsights.shared";
import {
  COMPLETION_THIS_WEEK_DAYS,
  getManagerWorkflowAction,
  getManagerWorkflowContextText,
  getManagerWorkflowPriority,
  getWorkflowDefinitionContext,
  hasUnresolvedRequirements,
  isCompletedThisWeek,
  isDeadlineThisWeek,
  isOpenTask,
  isRecentlyCompletedWorkflow,
  isStuckWorkflow,
  RECENT_COMPLETION_WINDOW_DAYS,
  STUCK_WORKFLOW_THRESHOLD_DAYS,
  summarizeWorkflows,
  toEpoch,
} from "./dashboardInsights.shared";

export async function loadHrInsights(options: DashboardInsightsOptions = {}): Promise<DashboardInsights> {
  const selectedWorkflowDefinition = options.selectedWorkflowDefinition ?? null;
  const workflowDefinitionContext = getWorkflowDefinitionContext(selectedWorkflowDefinition);
  const nowEpoch = Date.now();
  const workflows = await getWorkflows({ workflowDefinitionKey: options.workflowDefinitionKey ?? null });
  const metrics = summarizeWorkflows(workflows);
  const bottleneckCount = metrics.waitingSupervisor + metrics.waitingDepartment;
  const completedThisWeek = workflows.filter((workflow) => isCompletedThisWeek(workflow, nowEpoch)).length;
  const unresolvedRequirementWorkflows = workflows.filter(
    (workflow) => !isWorkflowTerminalStatus(workflow.workflowStatus) && hasUnresolvedRequirements(workflow)
  );
  const draftWorkflows = workflows.filter((workflow) => workflow.workflowStatus === "draft");
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
    nextStep: bottleneckCount > 0
      ? "Engpässe bei Abteilungsleitung und Fachbereichen zuerst entlasten."
      : unresolvedRequirementWorkflows.length > 0
        ? "Vorgänge mit offenen Anforderungen prüfen."
        : draftWorkflows.length > 0
          ? "Entwürfe finalisieren oder verwerfen."
          : selectedWorkflowDefinition
            ? `${selectedWorkflowDefinition.name}-Fälle in Startphase und Rücklauf prüfen.`
            : "Startphase und Rückläufe prüfen.",
    stats: [
      {
        label: "Wartet auf Freigabe / Fachbereich",
        value: bottleneckCount,
        note: "aktuelle Engpässe",
        statusLabel: "Engpass",
        tone: bottleneckCount > 0 ? "attention" : "neutral",
      },
      {
        label: "Offene Anforderungen",
        value: unresolvedRequirementWorkflows.length,
        note: "Vorgänge mit fehlenden Antworten",
        tone: unresolvedRequirementWorkflows.length > 0 ? "attention" : "neutral",
      },
      {
        label: "Aktive Vorgänge",
        value: metrics.open,
        note: "laufend",
        statusLabel: "Offen",
        tone: "progress",
      },
      {
        label: "Abgeschlossen (7 Tage)",
        value: completedThisWeek,
        note: `letzte ${COMPLETION_THIS_WEEK_DAYS} Tage`,
        statusLabel: "abgeschlossen",
        tone: "success",
      },
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
      {
        label: "Wartet auf Ihre Freigabe",
        value: waitingForSupervisor.length,
        note: "Leitungs-Schritt",
        tone: waitingForSupervisor.length > 0 ? "attention" : "neutral",
      },
      {
        label: "Offene Anforderungen",
        value: pendingSelections,
        note: "Auswahlpunkte aus Freigaben",
        tone: pendingSelections > 0 ? "attention" : "neutral",
      },
      {
        label: "Deadline diese Woche",
        value: relevantWorkflows.filter((workflow) => isDeadlineThisWeek(workflow, nowEpoch)).length,
        note: `nächste ${COMPLETION_THIS_WEEK_DAYS} Tage`,
        tone: "attention",
      },
      {
        label: "Aktive Vorgänge",
        value: activeWorkflows.length,
        note: selectedWorkflowDefinition ? `${selectedWorkflowDefinition.name}-Fälle` : "in Ihren Abteilungen",
        tone: "progress",
      },
      {
        label: "Abgeschlossen (30 Tage)",
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
    createdAt: string | null;
  }> = [];
  let openTaskCount = 0;
  let inProgressTaskCount = 0;
  let blockedTaskCount = 0;
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
        createdAt: item.task.createdAt ?? null,
      });
      openTaskCount += 1;
    }

    if (item.task.status === "in_progress") {
      inProgressTaskCount += 1;
    }

    if (item.task.status === "blocked") {
      blockedTaskCount += 1;
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

      const leftEpoch = left.createdAt ? toEpoch(left.createdAt) : 0;
      const rightEpoch = right.createdAt ? toEpoch(right.createdAt) : 0;
      if (leftEpoch !== rightEpoch) {
        return leftEpoch - rightEpoch;
      }

      return left.workflowDisplayName.localeCompare(right.workflowDisplayName, "de");
    })
    .slice(0, 6)
    .map((task) => ({
      key: `${task.workflowUid}:${task.taskId}`,
      title: task.taskTitle,
      detail: task.createdAt
        ? `${getTaskStatusLabel(task.taskStatus)} – seit ${formatDate(task.createdAt)}`
        : getTaskStatusLabel(task.taskStatus),
      to: "/tasks/my",
      actionLabel: "Aufgaben",
    }));

  return {
    heading: "Meine Aufgaben",
    nextStep: blockedTaskCount > 0
      ? "Blockierte Aufgaben zuerst klären."
      : inProgressTaskCount > 0
        ? "Laufende Aufgaben fertigstellen."
        : openTaskCount > 0
          ? "Älteste offene Aufgabe zuerst angehen."
          : "Keine offenen Aufgaben — alles erledigt.",
    stats: [
      {
        label: "Blockiert",
        value: blockedTaskCount,
        note: "warten auf Klärung",
        tone: blockedTaskCount > 0 ? "attention" : "neutral",
      },
      {
        label: "In Bearbeitung",
        value: inProgressTaskCount,
        note: "läuft",
        tone: "progress",
      },
      {
        label: "Offen",
        value: openTaskCount,
        note: "noch zu erledigen",
        tone: "neutral",
      },
      {
        label: "Erledigt",
        value: doneTaskCount,
        note: "erledigt",
        tone: "success",
      },
    ],
    queueTitle: "Aufgabenqueue",
    queueItems,
    emptyQueueText: "Aktuell sind keine offenen Aufgaben vorhanden.",
  };
}

export async function loadAdminInsights(options: DashboardInsightsOptions = {}): Promise<DashboardInsights> {
  const nowEpoch = Date.now();
  const [workflows, users, departments, responsibilities, notificationEmailConfiguration] = await Promise.all([
    getWorkflows({ workflowDefinitionKey: options.workflowDefinitionKey ?? null }),
    getAdminUsers().catch(() => []),
    getAdminDepartmentAssignments({ limit: 200 }).then((page) => page.items).catch(() => []),
    getAdminResponsibilityOwners({ limit: 200 }).then((page) => page.items).catch(() => []),
    getAdminNotificationEmailConfiguration().catch(() => null),
  ]);
  const selectedWorkflowDefinition = options.selectedWorkflowDefinition ?? null;
  const metrics = summarizeWorkflows(workflows);
  const bottleneckCount = metrics.waitingSupervisor + metrics.waitingDepartment;
  const stuckWorkflows = workflows.filter((workflow) => isStuckWorkflow(workflow, nowEpoch));

  const eligibleSupervisorUsers = users.filter(
    (user) => user.isActive && Boolean(user.canAccessSupervisorStep ?? user.hasManagerAccess)
  );
  const eligibleRequirementOwnerUsers = users.filter((user) => user.isActive);
  const adminWarnings = buildAdminOverviewWarnings({
    departments,
    responsibilities,
    eligibleSupervisorUsers,
    eligibleRequirementOwnerUsers,
    notificationEmailConfiguration,
  });
  const warningClusters = clusterAdminOverviewWarnings(groupAdminOverviewWarnings(adminWarnings));
  const adminWarningCount = adminWarnings.length;
  const clusterTargetSection: Record<string, string> = {
    stammdaten: "abteilungen",
    zustaendigkeit: "zustaendigkeiten",
    mail: "system_configuration",
  };

  const mapAdminTargetSection = (section: string | undefined): string =>
    `/admin/config?section=${section ?? "overview"}`;

  const adminWarningsByCluster: DashboardAdminWarningCluster[] = warningClusters.map((cluster) => ({
    key: cluster.cluster,
    title: cluster.title,
    totalCount: cluster.totalCount,
    groups: cluster.groups.map((group) => ({
      key: `${cluster.cluster}-${group.category}`,
      title: group.title,
      detail: group.detail,
      count: group.count,
      affectedLabels: group.affectedLabels,
      actionLabel: group.actionLabel,
      to: mapAdminTargetSection(group.targetSection ?? clusterTargetSection[cluster.cluster]),
    })),
  }));

  const adminOperations: DashboardAdminOperationItem[] = [];

  if (stuckWorkflows.length > 0) {
    const sorted = stuckWorkflows
      .slice()
      .sort((left, right) => toEpoch(left.createdAt) - toEpoch(right.createdAt))
      .slice(0, 4);
    for (const workflow of sorted) {
      const employeeName = `${workflow.firstName} ${workflow.lastName}`.trim() || "Unbekannter Mitarbeitender";
      adminOperations.push({
        key: `stuck-${workflow.uid}`,
        title: `Steckt fest: ${employeeName}`,
        detail: `${getWorkflowRuntimeStatusLabel(workflow.workflowStatus, "action")} – seit ${formatDate(workflow.createdAt)}`,
        to: `/workflows/${workflow.uid}`,
        actionLabel: "Öffnen",
        tone: "attention",
      });
    }
  }

  if (bottleneckCount > 0) {
    adminOperations.push({
      key: "workflow-bottlenecks",
      title: "Prozess-Engpässe verfolgen",
      detail: `${bottleneckCount} ${bottleneckCount === 1 ? "Vorgang wartet" : "Vorgänge warten"}`,
      to: "/workflows",
      actionLabel: "Übersicht",
      tone: "attention",
    });
  }

  const primaryWarningCluster = adminWarningsByCluster[0] ?? null;
  const statusDetail = primaryWarningCluster
    ? `${primaryWarningCluster.title} zuerst prüfen. ${primaryWarningCluster.totalCount} ${primaryWarningCluster.totalCount === 1 ? "offene Warnung" : "offene Warnungen"} sind aktuell gruppiert sichtbar.`
    : stuckWorkflows.length > 0
      ? `${stuckWorkflows.length} ${stuckWorkflows.length === 1 ? "Vorgang ist seit mindestens" : "Vorgänge sind seit mindestens"} ${STUCK_WORKFLOW_THRESHOLD_DAYS} Tagen blockiert.`
      : bottleneckCount > 0
        ? `${bottleneckCount} ${bottleneckCount === 1 ? "Vorgang wartet" : "Vorgänge warten"} aktuell auf Freigabe oder Fachbereich.`
        : "Aktuell gibt es keine Governance-Lücken oder Betriebsengpässe mit direktem Handlungsbedarf.";
  const statusAction = primaryWarningCluster
    ? {
        to: primaryWarningCluster.groups[0]?.to ?? mapAdminTargetSection(clusterTargetSection[primaryWarningCluster.key]),
        label: "Warnungen prüfen",
        description: "Öffnet die kritischsten Governance-Lücken in der Administration.",
      }
    : stuckWorkflows.length > 0
      ? {
          to: "/workflows",
          label: "Festhängende prüfen",
          description: "Öffnet die betroffenen Vorgänge in der Übersicht.",
        }
      : bottleneckCount > 0
        ? {
            to: "/workflows",
            label: "Engpässe prüfen",
            description: "Öffnet die laufenden Vorgänge mit Rückstau.",
          }
        : {
            to: "/admin/config",
            label: "Administration öffnen",
            description: "Öffnet die zentrale Konfigurationsübersicht.",
          };
  const summaryStats = [
    {
      label: "Admin-Warnungen",
      value: adminWarningCount,
      note: warningClusters.length > 0
        ? warningClusters.map((cluster) => cluster.title).join(", ")
        : "Stammdaten, Zuständigkeiten, Mail",
      tone: adminWarningCount === 0 ? "neutral" : "attention",
    },
    {
      label: "Festhängende Vorgänge",
      value: stuckWorkflows.length,
      note: `seit ≥ ${STUCK_WORKFLOW_THRESHOLD_DAYS} Tagen`,
      tone: stuckWorkflows.length > 0 ? "attention" : "neutral",
    },
    {
      label: "Wartet auf Freigabe / Fachbereich",
      value: bottleneckCount,
      note: "aktuelle Engpässe",
      tone: bottleneckCount > 0 ? "attention" : "neutral",
    },
    {
      label: "Aktive Vorgänge",
      value: metrics.open,
      note: "laufend",
      tone: "progress",
    },
  ] as const;

  return {
    heading: "Administration",
    nextStep: selectedWorkflowDefinition
      ? `Festhängende ${selectedWorkflowDefinition.name}-Vorgänge und Health-Signale prüfen.`
      : stuckWorkflows.length > 0
        ? "Festhängende Vorgänge zuerst entlasten."
        : bottleneckCount > 0
          ? "Aktuelle Engpässe in den Vorgängen prüfen."
          : adminWarningCount > 0
            ? `${adminWarningCount} offene Admin-Warnung${adminWarningCount === 1 ? "" : "en"} bereinigen.`
            : "System läuft. Keine offenen Admin-Aufgaben.",
    stats: [...summaryStats],
    adminSummary: {
      statusKicker: "Governance",
      statusTitle: adminWarningCount > 0
        ? `${adminWarningCount} offene Governance-Lücken priorisieren.`
        : stuckWorkflows.length > 0
          ? "Festhängende Vorgänge entlasten."
          : bottleneckCount > 0
            ? "Operative Engpässe aktiv steuern."
            : "Administration und Betrieb sind aktuell stabil.",
      statusDetail,
      action: statusAction,
      stats: [...summaryStats],
    },
    adminWarnings: adminWarningsByCluster,
    adminOperations,
    queueTitle: "Operative Risiken",
    queueItems: adminOperations,
    emptyQueueText: adminWarningCount === 0 && bottleneckCount === 0
      ? "Aktuell sind keine offenen Admin-Aufgaben oder Engpässe sichtbar."
      : "Keine festhängenden Vorgänge — die Übersicht zeigt aktuelle Engpässe und Warnungen.",
  };
}

export async function loadViewerInsights(options: DashboardInsightsOptions = {}): Promise<DashboardInsights> {
  const selectedWorkflowDefinition = options.selectedWorkflowDefinition ?? null;
  const workflowDefinitionContext = getWorkflowDefinitionContext(selectedWorkflowDefinition);
  const nowEpoch = Date.now();
  const workflows = await getWorkflows({ workflowDefinitionKey: options.workflowDefinitionKey ?? null });
  const metrics = summarizeWorkflows(workflows);
  const completedThisWeek = workflows.filter((workflow) => isCompletedThisWeek(workflow, nowEpoch)).length;
  const completedRecent = workflows.filter((workflow) => isRecentlyCompletedWorkflow(workflow, nowEpoch)).length;
  const completionRatePercent = metrics.total > 0
    ? Math.round((completedRecent / metrics.total) * 100)
    : 0;
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
    nextStep: metrics.open > 0
      ? `${metrics.open} ${metrics.open === 1 ? "Vorgang läuft" : "Vorgänge laufen"} aktuell.`
      : "Aktuell laufen keine Vorgänge.",
    stats: [
      {
        label: `Erfolgsquote (${RECENT_COMPLETION_WINDOW_DAYS} Tage)`,
        value: completionRatePercent,
        note: `${completedRecent} von ${metrics.total} sichtbaren Vorgängen abgeschlossen`,
        tone: "success",
      },
      {
        label: "Aktive Vorgänge",
        value: metrics.open,
        note: "laufend",
        tone: "progress",
      },
      {
        label: "Abgeschlossen (7 Tage)",
        value: completedThisWeek,
        note: `letzte ${COMPLETION_THIS_WEEK_DAYS} Tage`,
        tone: "success",
      },
      {
        label: "Vorgänge gesamt",
        value: metrics.total,
        note: "sichtbar",
        tone: "neutral",
      },
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
    queueTitle: "Weitere Themen",
    queueItems: [],
    emptyQueueText: "Keine rollenspezifischen Aufgaben vorhanden.",
  };
}
