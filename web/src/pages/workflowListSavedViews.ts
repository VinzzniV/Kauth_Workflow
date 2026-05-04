import type { RoleCapabilities } from "../auth/roleModel";
import type { WorkflowRuntimeStatus } from "../types/workflow";

export type SavedViewFilterParams = {
  statusFilter: "all" | WorkflowRuntimeStatus;
  departmentFilter: string;
  workflowDefinitionFilter: string;
  responsibilityFilter: string;
};

export type SavedView = SavedViewFilterParams & {
  id: string;
  label: string;
};

const DEFAULTS: SavedViewFilterParams = {
  statusFilter: "all",
  departmentFilter: "all",
  workflowDefinitionFilter: "all",
  responsibilityFilter: "all",
};

const VIEW_ALL: SavedView = { ...DEFAULTS, id: "all", label: "Alle" };
const VIEW_DRAFT: SavedView = { ...DEFAULTS, id: "draft", label: "HR startet", statusFilter: "draft" };
const VIEW_WAITING_SUPERVISOR: SavedView = {
  ...DEFAULTS,
  id: "waiting_for_supervisor",
  label: "Wartet auf Freigabe",
  statusFilter: "waiting_for_supervisor",
};
const VIEW_WAITING_DEPT: SavedView = {
  ...DEFAULTS,
  id: "waiting_for_department",
  label: "Fachbereiche offen",
  statusFilter: "waiting_for_department",
};
const VIEW_IN_PROGRESS: SavedView = {
  ...DEFAULTS,
  id: "in_progress",
  label: "In Bearbeitung",
  statusFilter: "in_progress",
};
const VIEW_COMPLETED: SavedView = { ...DEFAULTS, id: "completed", label: "Abgeschlossen", statusFilter: "completed" };

export function getSavedViewsForRole(capabilities: RoleCapabilities): SavedView[] {
  if (capabilities.hasHrRole) {
    return [VIEW_ALL, VIEW_DRAFT, VIEW_WAITING_SUPERVISOR, VIEW_WAITING_DEPT, VIEW_IN_PROGRESS];
  }
  if (capabilities.hasManagerRole && !capabilities.hasHrRole) {
    return [VIEW_ALL, VIEW_WAITING_SUPERVISOR, VIEW_WAITING_DEPT, VIEW_IN_PROGRESS];
  }
  if (capabilities.hasWorkerRole && !capabilities.hasHrRole && !capabilities.hasManagerRole) {
    return [VIEW_ALL, VIEW_WAITING_DEPT, VIEW_IN_PROGRESS];
  }
  if (capabilities.hasAdminRole || capabilities.hasReaderRole) {
    return [VIEW_ALL, VIEW_DRAFT, VIEW_IN_PROGRESS, VIEW_COMPLETED];
  }
  return [];
}

export function detectActiveSavedView(
  views: SavedView[],
  current: SavedViewFilterParams,
): SavedView | null {
  return (
    views.find(
      (view) =>
        view.statusFilter === current.statusFilter &&
        view.departmentFilter === current.departmentFilter &&
        view.workflowDefinitionFilter === current.workflowDefinitionFilter &&
        view.responsibilityFilter === current.responsibilityFilter,
    ) ?? null
  );
}
