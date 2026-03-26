import type {
  WorkflowDetail,
  WorkflowTask,
  WorkflowTaskAreaSummary,
  WorkflowTaskArea,
} from "../../types/workflow";
import {
  getWorkflowRuntimeStatusLabel,
  isDepartmentWorkflowPhase as isDepartmentWorkflowPhaseStatus,
  isWorkflowTerminalStatus,
} from "../../utils/workflowStatus";
import {
  formatDate as formatDateValue,
  formatDateTime as formatDateTimeValue,
} from "../../utils/dateFormat";

export type ProcessStepState = "done" | "active" | "pending";

export type ProcessStep = {
  key: string;
  title: string;
  detail: string;
  state: ProcessStepState;
};

export type ProcessAreaName = WorkflowTaskArea;

export type ProcessAreaGroup = {
  name: ProcessAreaName;
  tasks: WorkflowTask[];
  totalCount: number;
  openCount: number;
  inProgressCount: number;
  completedCount: number;
  isCurrentArea: boolean;
};

export function formatDate(value: string | null): string {
  return formatDateValue(value);
}

export function formatDateTime(value: string | null): string {
  return formatDateTimeValue(value);
}

export function toRuntimeStatusLabel(status: WorkflowDetail["workflowStatus"]): string {
  return getWorkflowRuntimeStatusLabel(status);
}

export function toProcessStepClass(state: ProcessStepState): string {
  if (state === "done") {
    return "process-step-state-done";
  }

  if (state === "active") {
    return "process-step-state-active";
  }

  return "process-step-state-pending";
}

export function isOpenStatus(status: WorkflowTask["status"]): boolean {
  return status === "open" || status === "ready" || status === "blocked";
}

export function isInProgressStatus(status: WorkflowTask["status"]): boolean {
  return status === "in_progress";
}

export function isDoneStatus(status: WorkflowTask["status"]): boolean {
  return status === "done";
}

export function isActiveStatus(status: WorkflowTask["status"]): boolean {
  return isOpenStatus(status) || isInProgressStatus(status);
}

export function inferAreaFromTask(task: WorkflowTask): ProcessAreaName | null {
  return task.processArea;
}

export function toAreaStatus(group: ProcessAreaGroup): "none" | "open" | "in_progress" | "done" {
  if (group.totalCount === 0) {
    return "none";
  }

  if (group.inProgressCount > 0) {
    return "in_progress";
  }

  if (group.openCount > 0) {
    return "open";
  }

  return "done";
}

export function toAreaStatusLabel(status: ReturnType<typeof toAreaStatus>): string {
  if (status === "none") {
    return "Noch kein Schritt";
  }

  if (status === "in_progress") {
    return "In Bearbeitung";
  }

  if (status === "open") {
    return "Offen";
  }

  return "Abgeschlossen";
}

export function toAreaStatusNote(group: ProcessAreaGroup): string {
  if (group.totalCount === 0) {
    return "In diesem Bereich sind aktuell keine Aufgaben vorgesehen.";
  }

  if (group.inProgressCount > 0 && group.openCount > 0) {
    return `${group.inProgressCount} in Bearbeitung, ${group.openCount} offen.`;
  }

  if (group.inProgressCount > 0) {
    return `${group.inProgressCount} Aufgabe${group.inProgressCount === 1 ? "" : "n"} in Bearbeitung.`;
  }

  if (group.openCount > 0) {
    return `${group.openCount} offene Aufgabe${group.openCount === 1 ? "" : "n"}.`;
  }

  return "Alle Aufgaben dieses Bereichs sind erledigt.";
}

export function toTaskDisplayTitle(task: WorkflowTask): string {
  return task.title;
}

export function isDepartmentWorkflowPhase(status: WorkflowDetail["workflowStatus"]): boolean {
  return isDepartmentWorkflowPhaseStatus(status);
}

export function hasSupervisorStep(workflow: WorkflowDetail): boolean {
  return workflow.workflowStatus === "waiting_for_supervisor" || workflow.tasks.some((task) => task.isApprovalTask);
}

export function toPhaseOwnerArea(workflow: WorkflowDetail): string {
  if (isWorkflowTerminalStatus(workflow.workflowStatus)) {
    return getWorkflowRuntimeStatusLabel(workflow.workflowStatus);
  }

  switch (workflow.workflowStatus) {
    case "draft":
      return "HR";
    case "waiting_for_supervisor":
      return "Abteilungsleitung";
    case "waiting_for_department":
    case "in_progress":
      return "Fachbereiche";
    default:
      return "-";
  }
}

export function toRegularEditingLabel(workflow: WorkflowDetail): string {
  if (isWorkflowTerminalStatus(workflow.workflowStatus)) {
    return "Abgeschlossen";
  }

  switch (workflow.workflowStatus) {
    case "draft":
      return "HR-Startphase";
    case "waiting_for_supervisor":
      return "Anforderungsphase";
    case "waiting_for_department":
    case "in_progress":
      return "Fachbereichsphase";
    default:
      return "-";
  }
}

export function findCurrentTask(tasks: WorkflowTask[]): WorkflowTask | null {
  const priority: Record<WorkflowTask["status"], number> = {
    in_progress: 0,
    ready: 1,
    open: 2,
    blocked: 3,
    done: 4,
  };

  const activeTasks = tasks.filter((task) => isActiveStatus(task.status));
  if (activeTasks.length === 0) {
    return null;
  }

  return activeTasks
    .slice()
    .sort((left, right) => {
      const delta = priority[left.status] - priority[right.status];
      if (delta !== 0) {
        return delta;
      }

      return left.sortOrder - right.sortOrder || left.id - right.id;
    })[0];
}

export function buildProcessSteps(workflow: WorkflowDetail): ProcessStep[] {
  const requirementSummary = workflow.requirementSummary;
  const departmentTaskMetrics = workflow.taskMetrics.departmentPhase;
  const isDepartmentPhase = isDepartmentWorkflowPhase(workflow.workflowStatus);
  const isTerminalWorkflow = isWorkflowTerminalStatus(workflow.workflowStatus);
  const includesSupervisorStep = hasSupervisorStep(workflow);
  const hrStepState: ProcessStepState = workflow.workflowStatus === "draft" ? "active" : "done";
  const requirementStepState: ProcessStepState =
    workflow.workflowStatus === "waiting_for_supervisor"
      ? "active"
      : isDepartmentPhase || isTerminalWorkflow
        ? "done"
        : "pending";
  const departmentStepState: ProcessStepState =
    isDepartmentPhase
      ? "active"
      : isTerminalWorkflow
        ? "done"
        : "pending";
  const completedStepState: ProcessStepState = isTerminalWorkflow ? "done" : "pending";

  const steps: ProcessStep[] = [
    {
      key: "hr-start",
      title: "HR gestartet",
      detail:
        workflow.workflowStatus === "draft"
          ? "HR prüft die Stammdaten und startet den Vorgang."
          : `Start am ${formatDateTime(workflow.createdAt)}.`,
      state: hrStepState,
    },
    {
      key: "departments",
      title: "Fachbereiche bearbeiten Aufgaben",
      detail:
        workflow.workflowStatus === "draft" || (includesSupervisorStep && workflow.workflowStatus === "waiting_for_supervisor")
          ? includesSupervisorStep
            ? "Aufgaben für Fachbereiche entstehen erst nach Abschluss der Freigabe."
            : "Aufgaben für Fachbereiche entstehen nach Abschluss der Startphase."
          : `Offen: ${departmentTaskMetrics.openCount} | In Bearbeitung: ${departmentTaskMetrics.inProgressCount} | Erledigt: ${departmentTaskMetrics.completedCount} von ${departmentTaskMetrics.totalCount}.`,
      state: departmentStepState,
    },
    {
      key: "completed",
      title: "Abgeschlossen",
      detail:
        workflow.workflowStatus === "completed"
          ? "Vorgang ist abgeschlossen."
          : "Abschluss steht noch aus.",
      state: completedStepState,
    },
  ];

  if (includesSupervisorStep) {
    steps.splice(1, 0, {
      key: "requirements",
      title: "Abteilungsleitung bestätigt Anforderungen",
      detail:
        workflow.workflowStatus === "draft"
          ? "Startet, sobald HR den Vorgang freigibt."
          : `Beantwortet: ${requirementSummary.answeredVisibleCount} von ${requirementSummary.visibleCount}.`,
      state: requirementStepState,
    });
  }

  return steps;
}

export function buildTasksByArea(
  sortedTasks: WorkflowTask[],
  taskAreas: WorkflowTaskAreaSummary[]
): ProcessAreaGroup[] {
  const byArea = new Map<ProcessAreaName, WorkflowTask[]>();
  const orderedAreaNames = taskAreas.map((area) => area.name);

  for (const task of sortedTasks) {
    const areaName = inferAreaFromTask(task);
    if (!areaName) {
      continue;
    }

    let tasks = byArea.get(areaName);
    if (!tasks) {
      tasks = [];
      byArea.set(areaName, tasks);
      if (!orderedAreaNames.includes(areaName)) {
        orderedAreaNames.push(areaName);
      }
    }

    tasks.push(task);
  }

  return orderedAreaNames.map((name) => {
    const tasks = byArea.get(name) ?? [];
    const summary = taskAreas.find((area) => area.name === name);
    return {
      name,
      tasks,
      totalCount: summary?.counts.totalCount ?? tasks.length,
      openCount: summary?.counts.openCount ?? tasks.filter((task) => isOpenStatus(task.status)).length,
      inProgressCount: summary?.counts.inProgressCount ?? tasks.filter((task) => isInProgressStatus(task.status)).length,
      completedCount: summary?.counts.completedCount ?? tasks.filter((task) => isDoneStatus(task.status)).length,
      isCurrentArea: summary?.isCurrentArea ?? false,
    };
  });
}
