// Beschreibt die Rollen der Anwendung und leitet daraus UI-Faehigkeiten und Standardrouten ab.
export const AUTH_ROLE_KEYS = {
  hr: "auth_hr",
  manager: "auth_manager",
  worker: "auth_worker",
  admin: "auth_admin",
  reader: "auth_reader",
} as const;

export type AuthRoleKey = (typeof AUTH_ROLE_KEYS)[keyof typeof AUTH_ROLE_KEYS];

export type DashboardPersona = "admin" | "hr" | "manager" | "worker" | "reader" | "generic";

export type AppFeature =
  | "dashboard"
  | "workflowCreate"
  | "workflowOverview"
  | "workflowSearch"
  | "technicalTasks"
  | "supervisorStep"
  | "adminConfig";

export type RoleCapabilities = {
  roleKeys: AuthRoleKey[];
  permissionKeys: string[];
  hasMultipleRoles: boolean;
  hasAdminRole: boolean;
  hasHrRole: boolean;
  hasManagerRole: boolean;
  hasWorkerRole: boolean;
  hasReaderRole: boolean;
  hasReadRole: boolean;
  hasProcessActorRole: boolean;
  canCreateWorkflow: boolean;
  canAccessSupervisorStep: boolean;
  canAccessTechnicalTasks: boolean;
  canManageAdminConfiguration: boolean;
  canAccessWorkflowOverview: boolean;
  dashboardPersona: DashboardPersona;
};

const ROLE_LABELS: Record<AuthRoleKey, string> = {
  [AUTH_ROLE_KEYS.hr]: "HR",
  [AUTH_ROLE_KEYS.manager]: "Abteilungsleitung",
  [AUTH_ROLE_KEYS.worker]: "Fachbereich",
  [AUTH_ROLE_KEYS.admin]: "Admin",
  [AUTH_ROLE_KEYS.reader]: "Leser",
};

const ROLE_ALIASES: Record<AuthRoleKey, string[]> = {
  [AUTH_ROLE_KEYS.hr]: ["auth_hr", "hr"],
  [AUTH_ROLE_KEYS.manager]: ["auth_manager", "manager", "abteilungsleitung"],
  [AUTH_ROLE_KEYS.worker]: ["auth_worker", "worker", "bearbeiter", "fachbereich"],
  [AUTH_ROLE_KEYS.admin]: ["auth_admin", "admin"],
  [AUTH_ROLE_KEYS.reader]: ["auth_reader", "reader", "leser"],
};

const ROLE_KEY_LOOKUP = new Map<string, AuthRoleKey>(
  Object.entries(ROLE_ALIASES).flatMap(([roleKey, aliases]) =>
    aliases.map((alias) => [normalizeRoleLookupKey(alias), roleKey as AuthRoleKey] as const)
  )
);

function normalizeRoleLookupKey(value: string): string {
  return value.trim().toLowerCase().replace(/[\s-]+/g, "_");
}

function normalizeRoleKey(roleKey: string): AuthRoleKey | null {
  if (!roleKey.trim()) {
    return null;
  }

  return ROLE_KEY_LOOKUP.get(normalizeRoleLookupKey(roleKey)) ?? null;
}

// Wandelt technische Rollenschluessel in lesbare Labels fuer Navigation und Benutzeranzeige um.
export function toRoleLabel(roleKey: string): string {
  const normalized = normalizeRoleKey(roleKey);
  if (normalized) {
    return ROLE_LABELS[normalized];
  }

  return roleKey;
}

// Verdichtet die vorhandenen Rollen in wiederverwendbare UI-Faehigkeiten.
function normalizePermissionKey(permissionKey: string): string {
  return permissionKey.trim().toLowerCase();
}

export function deriveRoleCapabilities(
  rawRoleKeys: string[],
  rawPermissionKeys: string[] = []
): RoleCapabilities {
  const roleSet = new Set<AuthRoleKey>();
  const permissionSet = new Set<string>();

  for (const rawRoleKey of rawRoleKeys) {
    const normalized = normalizeRoleKey(rawRoleKey);
    if (!normalized) {
      continue;
    }

    roleSet.add(normalized);
  }

  for (const rawPermissionKey of rawPermissionKeys) {
    if (!rawPermissionKey.trim()) {
      continue;
    }

    permissionSet.add(normalizePermissionKey(rawPermissionKey));
  }

  const hasPermission = (permissionKey: string) => permissionSet.has(normalizePermissionKey(permissionKey));
  const hasWorkflowCreatePermission = Array.from(permissionSet).some((permissionKey) =>
    permissionKey.startsWith("workflows.create.")
  );

  const hasAdmin = roleSet.has(AUTH_ROLE_KEYS.admin);
  const hasHr = roleSet.has(AUTH_ROLE_KEYS.hr);
  const hasManager = roleSet.has(AUTH_ROLE_KEYS.manager);
  const hasWorker = roleSet.has(AUTH_ROLE_KEYS.worker);
  const hasReader = roleSet.has(AUTH_ROLE_KEYS.reader);
  const hasMultipleRoles = roleSet.size > 1;

  const hasReadRole = hasAdmin || hasHr || hasManager || hasWorker || hasReader;
  const hasProcessActorRole = hasHr || hasManager || hasWorker;
  const canCreateWorkflow = hasWorkflowCreatePermission || hasHr || hasManager || hasAdmin;
  const canAccessSupervisorStep = hasPermission("tasks.execute.supervisor") || hasManager;
  const canAccessTechnicalTasks = hasPermission("tasks.execute.department") || hasWorker || canAccessSupervisorStep;
  const canManageAdminConfiguration =
    hasPermission("admin.permissions.manage") || hasPermission("admin.directory.manage") || hasAdmin;
  const canAccessWorkflowOverview =
    hasPermission("workflows.view_all")
    || hasPermission("workflows.view_department")
    || hasAdmin
    || hasHr
    || hasReader
    || hasManager;
  const dashboardPersona: DashboardPersona = hasAdmin
    ? "admin"
    : hasHr
      ? "hr"
      : hasManager
        ? "manager"
        : hasWorker
          ? "worker"
          : hasReader
            ? "reader"
            : "generic";

  return {
    roleKeys: Array.from(roleSet).sort((left, right) => left.localeCompare(right, "en")),
    permissionKeys: Array.from(permissionSet).sort((left, right) => left.localeCompare(right, "en")),
    hasMultipleRoles,
    hasAdminRole: hasAdmin,
    hasHrRole: hasHr,
    hasManagerRole: hasManager,
    hasWorkerRole: hasWorker,
    hasReaderRole: hasReader,
    hasReadRole,
    hasProcessActorRole,
    canCreateWorkflow,
    canAccessSupervisorStep,
    canAccessTechnicalTasks,
    canManageAdminConfiguration,
    canAccessWorkflowOverview,
    dashboardPersona,
  };
}

// Zentrale Stelle fuer die Sichtbarkeit einzelner App-Bereiche.
export function canAccessFeature(capabilities: RoleCapabilities, feature: AppFeature): boolean {
  switch (feature) {
    case "dashboard":
      return capabilities.hasReadRole;
    case "workflowCreate":
      return capabilities.canCreateWorkflow;
    case "workflowOverview":
    case "workflowSearch":
      return capabilities.canAccessWorkflowOverview;
    case "technicalTasks":
      return capabilities.canAccessTechnicalTasks;
    case "supervisorStep":
      return capabilities.canAccessSupervisorStep;
    case "adminConfig":
      return capabilities.canManageAdminConfiguration;
    default:
      return false;
  }
}

// Legt fest, wohin Benutzer nach Login oder fehlender Berechtigung geleitet werden.
export function getDefaultRoute(capabilities: RoleCapabilities): string {
  if (capabilities.hasMultipleRoles && canAccessFeature(capabilities, "dashboard")) {
    return "/";
  }

  switch (capabilities.dashboardPersona) {
    case "admin":
      if (canAccessFeature(capabilities, "adminConfig")) {
        return "/admin/config";
      }
      break;
    case "manager":
      if (canAccessFeature(capabilities, "supervisorStep")) {
        return "/supervisor";
      }
      break;
    case "worker":
      if (canAccessFeature(capabilities, "technicalTasks")) {
        return "/tasks/my";
      }
      break;
  }

  if (canAccessFeature(capabilities, "dashboard")) {
    return "/";
  }

  if (canAccessFeature(capabilities, "workflowCreate")) {
    return "/create";
  }

  if (canAccessFeature(capabilities, "workflowOverview")) {
    return "/workflows";
  }

  if (canAccessFeature(capabilities, "supervisorStep")) {
    return "/supervisor";
  }

  return "/";
}
