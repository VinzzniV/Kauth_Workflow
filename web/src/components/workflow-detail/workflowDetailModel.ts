import type {
  RequirementSelectionPayload,
  RequirementSelectionState,
  WorkflowDetail,
  WorkflowRequirementSnapshot,
  WorkflowTask,
  WorkflowTaskArea,
} from "../../types/workflow";
import { createEmptyRequirementSelection } from "../../utils/requirements";
import {
  getWorkflowLegacyStatusLabel,
  getWorkflowRuntimeStatusLabel,
  isDepartmentWorkflowPhase as isDepartmentWorkflowPhaseStatus,
  isWorkflowTerminalStatus,
} from "../../utils/workflowStatus";

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
  openCount: number;
  inProgressCount: number;
  doneCount: number;
  isCurrentArea: boolean;
};

const PROCESS_AREA_ORDER: ProcessAreaName[] = ["HR", "Abteilungsleitung", "IT", "QS", "AV", "QMB"];

const TASK_TITLE_OVERRIDES: Partial<Record<string, string>> = {
  contract_archived: "Vertrag ablegen",
  kaba_user_created: "Kaba-Eintrag anlegen",
  supervisor_fills_document: "Anforderungen bestätigen",
  document_sent_to_distribution: "Unterlagen an Fachbereiche senden",
  ad_user_create: "AD-User anlegen",
  permissions_from_reference_user: "AD-Berechtigungen übernehmen",
  exchange_create: "Mailbox anlegen",
  habel_user_create: "Habel-User anlegen",
  ln_user_create: "LN-User anlegen",
  internet_access_enable: "Internetzugang einrichten",
  internal_drive_access_grant: "Laufwerksrechte vergeben",
  office_install: "Microsoft Office bereitstellen",
  hardware_procure: "Hardware beschaffen",
  hardware_setup: "Hardware einrichten",
  hardware_handover: "Hardware bereitstellen",
  phone_prepare: "Tragbares Telefon bereitstellen",
  catia_install: "Catia bereitstellen",
  datev_install: "DATEV bereitstellen",
  tisoware_install: "Tisoware bereitstellen",
  babtec_user_create: "Babtec-User anlegen",
  gewatec_user_create: "Gewatec-User anlegen",
  provis_user_create: "Provis-User anlegen",
  consense_setup: "Spinfire anlegen",
  consense_training: "Spinfire-Schulung planen",
};

export const PRE_SUPERVISOR_TASK_KEYS = new Set([
  "contract_archived",
  "kaba_user_created",
  "supervisor_fills_document",
]);

export function formatDate(value: string | null): string {
  if (!value) {
    return "-";
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return "-";
  }

  return parsed.toLocaleDateString("de-DE");
}

export function formatDateTime(value: string | null): string {
  if (!value) {
    return "-";
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return "-";
  }

  return parsed.toLocaleString("de-DE");
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
  return status === "done" || status === "skipped" || status === "cancelled";
}

export function isActiveStatus(status: WorkflowTask["status"]): boolean {
  return isOpenStatus(status) || isInProgressStatus(status);
}

export function inferAreaFromTask(task: WorkflowTask): ProcessAreaName | null {
  return task.processArea;
}

export function toAreaStatus(group: ProcessAreaGroup): "none" | "open" | "in_progress" | "done" {
  if (group.tasks.length === 0) {
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
  if (group.tasks.length === 0) {
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
  return TASK_TITLE_OVERRIDES[task.taskKey] ?? task.title;
}

export function isDepartmentWorkflowPhase(status: WorkflowDetail["workflowStatus"]): boolean {
  return isDepartmentWorkflowPhaseStatus(status);
}

export function toPhaseOwnerArea(workflow: WorkflowDetail): string {
  if (isWorkflowTerminalStatus(workflow.workflowStatus)) {
    return getWorkflowLegacyStatusLabel(workflow.workflowStatus);
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
    return "Keine reguläre Bearbeitung";
  }

  switch (workflow.workflowStatus) {
    case "draft":
      return "HR";
    case "waiting_for_supervisor":
      return "Zuständige Abteilungsleitung";
    case "waiting_for_department":
    case "in_progress":
      return "Zuständige Fachbereiche";
    default:
      return "-";
  }
}

export function toReadAccessLabel(workflow: WorkflowDetail): string {
  if (isWorkflowTerminalStatus(workflow.workflowStatus)) {
    return "Je nach Rolle";
  }

  switch (workflow.workflowStatus) {
    case "draft":
      return "Admin";
    case "waiting_for_supervisor":
    case "waiting_for_department":
    case "in_progress":
      return "HR, Admin";
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
    skipped: 5,
    cancelled: 6,
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

export function toRequirementPayload(
  requirements: WorkflowRequirementSnapshot[],
  selections: Record<number, RequirementSelectionState>
): RequirementSelectionPayload[] {
  return requirements.map((requirement) => {
    const selection = selections[requirement.id] ?? createEmptyRequirementSelection();

    if (requirement.inputType === "boolean") {
      return {
        requirementId: requirement.id,
        valueBoolean: selection.valueBoolean,
      };
    }

    if (requirement.inputType === "text") {
      return {
        requirementId: requirement.id,
        valueText: selection.valueText,
      };
    }

    if (requirement.inputType === "select") {
      return {
        requirementId: requirement.id,
        selectedOptionId: selection.selectedOptionId,
      };
    }

    return {
      requirementId: requirement.id,
      selectedOptionIds: [...selection.selectedOptionIds],
    };
  });
}

export function buildProcessSteps(args: {
  workflow: WorkflowDetail;
  requirementSummary: { total: number; answered: number };
  departmentOpenTaskCount: number;
  departmentInProgressTaskCount: number;
  departmentDoneTaskCount: number;
  departmentTaskCount: number;
}): ProcessStep[] {
  const {
    workflow,
    requirementSummary,
    departmentOpenTaskCount,
    departmentInProgressTaskCount,
    departmentDoneTaskCount,
    departmentTaskCount,
  } = args;

  const isDepartmentPhase = isDepartmentWorkflowPhase(workflow.workflowStatus);
  const isTerminalWorkflow = isWorkflowTerminalStatus(workflow.workflowStatus);
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

  return [
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
      key: "requirements",
      title: "Abteilungsleitung wählt Anforderungen",
      detail:
        workflow.workflowStatus === "draft"
          ? "Startet, sobald HR den Vorgang freigibt."
          : `Beantwortet: ${requirementSummary.answered} von ${requirementSummary.total}.`,
      state: requirementStepState,
    },
    {
      key: "departments",
      title: "Fachbereiche bearbeiten Aufgaben",
      detail:
        workflow.workflowStatus === "draft" || workflow.workflowStatus === "waiting_for_supervisor"
          ? "Aufgaben für Fachbereiche entstehen erst nach Auswahl der Anforderungen."
          : `Offen: ${departmentOpenTaskCount} | In Bearbeitung: ${departmentInProgressTaskCount} | Erledigt: ${departmentDoneTaskCount} von ${departmentTaskCount}.`,
      state: departmentStepState,
    },
    {
      key: "completed",
      title: "Abgeschlossen",
      detail:
        workflow.workflowStatus === "completed"
          ? "Onboarding ist abgeschlossen."
          : workflow.workflowStatus === "cancelled"
            ? "Onboarding wurde abgebrochen."
            : "Abschluss steht noch aus.",
      state: completedStepState,
    },
  ];
}

export function buildTasksByArea(
  sortedTasks: WorkflowTask[],
  activeAreas: ReadonlySet<ProcessAreaName>
): ProcessAreaGroup[] {
  const byArea = new Map<ProcessAreaName, WorkflowTask[]>(PROCESS_AREA_ORDER.map((name) => [name, [] as WorkflowTask[]]));

  for (const task of sortedTasks) {
    const group = inferAreaFromTask(task);
    if (group) {
      byArea.get(group)?.push(task);
    }
  }

  return PROCESS_AREA_ORDER.map((name) => {
    const tasks = byArea.get(name) ?? [];
    return {
      name,
      tasks,
      openCount: tasks.filter((task) => isOpenStatus(task.status)).length,
      inProgressCount: tasks.filter((task) => isInProgressStatus(task.status)).length,
      doneCount: tasks.filter((task) => isDoneStatus(task.status)).length,
      isCurrentArea: activeAreas.has(name),
    };
  });
}
