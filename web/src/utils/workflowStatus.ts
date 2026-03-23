import type { WorkflowStatus } from "../types/workflow";

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

function isCancelledWorkflowStatus(status?: string): boolean {
  return normalizeWorkflowStatus(status ?? "") === "cancelled";
}

export function toWorkflowLegacyStatus(status: string): WorkflowStatus {
  return normalizeWorkflowLegacyStatus(status) ?? "open";
}

export function isWorkflowTerminalStatus(status: string): boolean {
  const normalized = normalizeWorkflowStatus(status);
  return normalized === "completed" || normalized === "cancelled";
}

export function isDepartmentWorkflowPhase(status: string): boolean {
  const normalized = normalizeWorkflowStatus(status);
  return normalized === "waiting_for_department" || normalized === "in_progress";
}

export function matchesWorkflowLegacyStatusFilter(
  status: string,
  filter: "all" | WorkflowStatus,
  workflowStatus?: string
): boolean {
  if (isCancelledWorkflowStatus(workflowStatus)) {
    return filter === "all";
  }

  return filter === "all" || normalizeWorkflowLegacyStatus(status) === filter;
}

export function getWorkflowLegacyStatusLabel(status: string, workflowStatus?: string): string {
  const normalized = normalizeWorkflowStatus(status);
  if (normalized === "cancelled" || isCancelledWorkflowStatus(workflowStatus)) {
    return "Abgebrochen";
  }

  if (normalizeWorkflowLegacyStatus(status) === "completed") {
    return "Abgeschlossen";
  }

  return "Offen";
}

export function getWorkflowLegacyStatusPillClass(status: string, workflowStatus?: string): string {
  const normalized = normalizeWorkflowStatus(status);
  if (normalized === "cancelled" || isCancelledWorkflowStatus(workflowStatus)) {
    return "cancelled";
  }

  const legacyStatus = normalizeWorkflowLegacyStatus(status);
  return legacyStatus === "completed" ? legacyStatus : "running";
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
