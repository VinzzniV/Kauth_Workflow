import { toRoleLabel } from "../../auth/roleModel";
import type {
  AdminNotificationEmailConfiguration,
  AdminResponsibilityOwner,
  AdminRole,
  AdminUser,
} from "../../types/auth";

export function toggleId(list: number[], id: number): number[] {
  if (list.includes(id)) {
    return list.filter((item) => item !== id);
  }

  return [...list, id];
}

export function roleDisplayName(role: AdminRole): string {
  const departmentPrefix = role.departmentName ? `${role.departmentName} / ` : "";
  const roleName = role.roleKind === "system" ? toRoleLabel(role.roleKey) : role.roleName;
  return `${departmentPrefix}${roleName}`;
}

export function toNullableNumber(value: string): number | null {
  if (!value) {
    return null;
  }

  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

export function formatTimestamp(value: string | null): string {
  if (!value) {
    return "Noch nicht gespeichert";
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return "Noch nicht gespeichert";
  }

  return new Intl.DateTimeFormat("de-DE", {
    dateStyle: "short",
    timeStyle: "short",
  }).format(parsed);
}

export function toNullableText(value: string): string | null {
  const normalized = value.trim();
  return normalized.length > 0 ? normalized : null;
}

export function notificationModeLabel(configuration: AdminNotificationEmailConfiguration | null): string {
  if (!configuration) {
    return "Noch nicht geladen";
  }

  switch (configuration.mode) {
    case "sandbox":
      return "Testpostfach aktiv";
    case "enabled":
      return "Aktiviert";
    default:
      return "Deaktiviert";
  }
}

export function notificationConfigurationStatusLabel(configuration: AdminNotificationEmailConfiguration | null): string {
  if (!configuration) {
    return "Unbekannt";
  }

  switch (configuration.configurationStatus) {
    case "ready":
      return "Konfiguration vollständig";
    case "disabled":
      return "Versand deaktiviert";
    default:
      return "Konfiguration unvollständig";
  }
}

export function notificationTestStatusLabel(configuration: AdminNotificationEmailConfiguration | null): string {
  if (!configuration) {
    return "Noch nicht geladen";
  }

  switch (configuration.lastTestStatus) {
    case "succeeded":
      return "Erfolgreich";
    case "failed":
      return "Fehlgeschlagen";
    case "disabled":
      return "Nicht ausgeführt: Versand deaktiviert";
    default:
      return "Noch nicht getestet";
  }
}

export function userOptionLabel(user: AdminUser): string {
  const suffix = [user.departmentName, user.isActive ? null : "inaktiv"].filter(Boolean).join(" | ");
  return suffix ? `${user.displayName} (${suffix})` : user.displayName;
}

export function responsibilityTypeLabel(item: AdminResponsibilityOwner): string {
  return item.responsibilityType === "process" ? "Prozess" : "System / Anwendung";
}

export function responsibilityAreaLabel(item: AdminResponsibilityOwner): string {
  if (item.departmentName) {
    return item.departmentName;
  }

  return item.responsibilityType === "process" ? "Bereichsübergreifend" : "Ohne Bereich";
}
