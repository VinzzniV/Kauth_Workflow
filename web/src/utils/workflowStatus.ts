import type { WorkflowRuntimeStatus, WorkflowStatus } from "../types/workflow";

export type WorkflowRuntimeStatusLabelVariant = "compact" | "action";

function normalizeWorkflowStatus(status: string): string {
  return status.trim().toLowerCase();
}

function normalizeWorkflowLegacyStatus(status: string): WorkflowStatus | null {
  const normalized = normalizeWorkflowStatus(status);

  if (normalized === "open" || normalized === "completed") {
    return normalized;
  }

  return null;
}

export function toWorkflowLegacyStatus(status: string): WorkflowStatus {
  return normalizeWorkflowLegacyStatus(status) ?? "open";
}

export function isWorkflowTerminalStatus(status: string): boolean {
  const normalized = normalizeWorkflowStatus(status);
  return normalized === "completed";
}

export function isDepartmentWorkflowPhase(status: string): boolean {
  const normalized = normalizeWorkflowStatus(status);
  return normalized === "waiting_for_department" || normalized === "in_progress";
}

function normalizeWorkflowRuntimeStatus(status: string): WorkflowRuntimeStatus | null {
  const normalized = normalizeWorkflowStatus(status);

  switch (normalized) {
    case "draft":
    case "in_progress":
    case "waiting_for_supervisor":
    case "waiting_for_department":
    case "completed":
      return normalized;
    default:
      return null;
  }
}

export function matchesWorkflowRuntimeStatusFilter(
  status: string,
  filter: "all" | WorkflowRuntimeStatus
): boolean {
  return filter === "all" || normalizeWorkflowRuntimeStatus(status) === filter;
}

export function getWorkflowRuntimeStatusPillClass(status: string): string {
  const runtimeStatus = normalizeWorkflowRuntimeStatus(status);

  switch (runtimeStatus) {
    case "completed":
      return "completed";
    case "draft":
      return "open";
    case "waiting_for_supervisor":
    case "waiting_for_department":
    case "in_progress":
      return "running";
    default:
      return "running";
  }
}

export function getWorkflowRuntimeStatusLabel(
  status: string,
  variant: WorkflowRuntimeStatusLabelVariant = "compact"
): string {
  switch (normalizeWorkflowStatus(status)) {
    case "draft":
      return "HR startet";
    case "in_progress":
      return variant === "action" ? "Fachbereiche bearbeiten Aufgaben" : "Fachbereiche in Bearbeitung";
    case "waiting_for_supervisor":
      return "Abteilungsleitung wählt Anforderungen";
    case "waiting_for_department":
      return variant === "action" ? "Fachbereiche bearbeiten Aufgaben" : "Fachbereiche offen";
    case "completed":
      return "Abgeschlossen";
    default:
      return status;
  }
}
