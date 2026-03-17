import type { WorkflowStatus } from "../types/workflow";

export type WorkflowRuntimeStatusLabelVariant = "compact" | "action";

function normalizeWorkflowStatus(status: string): string {
  return status.trim().toLowerCase();
}

export function toWorkflowLegacyStatus(status: string): WorkflowStatus {
  const normalized = normalizeWorkflowStatus(status);

  if (normalized === "completed" || normalized === "cancelled") {
    return normalized;
  }

  return "open";
}

export function isWorkflowTerminalStatus(status: string): boolean {
  const normalized = normalizeWorkflowStatus(status);
  return normalized === "completed" || normalized === "cancelled";
}

export function isDepartmentWorkflowPhase(status: string): boolean {
  const normalized = normalizeWorkflowStatus(status);
  return normalized === "waiting_for_department" || normalized === "in_progress";
}

export function matchesWorkflowLegacyStatusFilter(status: string, filter: "all" | WorkflowStatus): boolean {
  return filter === "all" || toWorkflowLegacyStatus(status) === filter;
}

export function getWorkflowLegacyStatusLabel(status: string): string {
  const legacyStatus = toWorkflowLegacyStatus(status);

  if (legacyStatus === "completed") {
    return "Abgeschlossen";
  }

  if (legacyStatus === "cancelled") {
    return "Abgebrochen";
  }

  return "Offen";
}

export function getWorkflowLegacyStatusPillClass(status: string): string {
  const legacyStatus = toWorkflowLegacyStatus(status);
  return legacyStatus === "open" ? "running" : legacyStatus;
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
    case "cancelled":
      return "Abgebrochen";
    default:
      return status;
  }
}
