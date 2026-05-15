// Slice 4 (Admin-Gated-Automation, Builder-UI): Whitelist der Approval-Rollen
// fuer task-Nodes mit Action-Bundle. Deckungsgleich mit Backend-Whitelist
// (AuthorizationRoles.Admin/Hr/Manager + WorkflowDefinitionValidationCatalog
// .AllowedAutomationAdminRoles). Drift zwischen Backend und Frontend wuerde
// dazu fuehren, dass die UI Rollen anbietet, die der Server ablehnt — oder
// umgekehrt.

export const AUTOMATION_ADMIN_ROLES = [
  { value: "auth_admin", label: "Administrator" },
  { value: "auth_hr", label: "HR" },
  { value: "auth_manager", label: "Abteilungsleitung" },
] as const;

export type AutomationAdminRoleSlug = typeof AUTOMATION_ADMIN_ROLES[number]["value"];

export function isAutomationAdminRoleSlug(value: string | null | undefined): value is AutomationAdminRoleSlug {
  if (!value) return false;
  const normalized = value.trim().toLowerCase();
  return AUTOMATION_ADMIN_ROLES.some((role) => role.value === normalized);
}

// Unbekannte/Legacy-Slugs werden als raw-Wert zurueckgegeben, damit der
// Builder einen Drift-Stand sichtbar machen kann statt ihn zu verschlucken.
export function getAutomationAdminRoleLabel(slug: string | null | undefined): string | null {
  if (!slug) return null;
  const normalized = slug.trim().toLowerCase();
  const match = AUTOMATION_ADMIN_ROLES.find((role) => role.value === normalized);
  return match?.label ?? slug;
}
