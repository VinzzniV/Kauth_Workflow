import type {
  AdminDepartmentAssignment,
  AdminNotificationEmailConfiguration,
  AdminResponsibilityOwner,
  AdminUser,
} from "../../types/auth";
import { toNullableNumber, toNullableText } from "./adminConfigHelpers";

export type AdminWorkspaceSection =
  | "overview"
  | "organization"
  | "templates"
  | "answers"
  | "defaults"
  | "access"
  | "directory"
  | "system"
  | "operations";
export type AdminOrganizationEntity = "user" | "department" | "responsibility";
export type AdminWorkspaceSectionGroup = "start" | "configuration" | "technical" | "sensitive";

export type AdminWorkspaceSectionMeta = {
  key: AdminWorkspaceSection;
  label: string;
  description: string;
  group: AdminWorkspaceSectionGroup;
  groupLabel: string;
  groupDescription: string;
  audienceLabel: string;
  cautionLabel: string;
  audienceDescription: string;
  cautionDescription: string;
  impactLabel: string;
};

export type AdminWorkspaceWarning = {
  key: string;
  title: string;
  detail: string;
  actionLabel: string;
  targetEntity?: AdminOrganizationEntity;
  targetId?: number | null;
  targetSection?: AdminWorkspaceSection;
};

export const ADMIN_WORKSPACE_SECTION_META: AdminWorkspaceSectionMeta[] = [
  {
    key: "overview",
    label: "Übersicht",
    description: "Status prüfen, Warnhinweise klären und in den passenden Bereich springen.",
    group: "start",
    groupLabel: "Empfohlener Start",
    groupDescription: "Hilft beim Einstieg und bei der täglichen Orientierung.",
    audienceLabel: "Geeignet für",
    cautionLabel: "Hinweis",
    audienceDescription: "Wenn Sie zuerst verstehen wollen, wo Konfigurationslücken oder offene Aufgaben liegen.",
    cautionDescription: "Hier werden Hinweise gebündelt, aber noch keine Stammdaten geändert.",
    impactLabel: "Start hier",
  },
  {
    key: "organization",
    label: "Organisation",
    description: "Personen, Abteilungen und Zuständigkeiten fachlich pflegen.",
    group: "start",
    groupLabel: "Empfohlener Start",
    groupDescription: "Hilft beim Einstieg und bei der täglichen Orientierung.",
    audienceLabel: "Geeignet für",
    cautionLabel: "Vorsicht bei",
    audienceDescription: "Wenn sich Ansprechpersonen, Bereiche oder fachliche Verantwortungen ändern.",
    cautionDescription: "Login-Rechte und Gruppen bleiben bewusst getrennt und gehören in den Bereich Zugriffe & Gruppen.",
    impactLabel: "Häufig genutzt",
  },
  {
    key: "templates",
    label: "Aufgabenvorlagen",
    description: "Aufgabenlogik für neue Vorgänge pro Prozesstyp steuern.",
    group: "configuration",
    groupLabel: "Fachliche Konfiguration",
    groupDescription: "Steuert, wie neue Vorgänge vorbereitet und vorausgefüllt werden.",
    audienceLabel: "Geeignet für",
    cautionLabel: "Vorsicht bei",
    audienceDescription: "Wenn Aufbau, Reihenfolge oder Standardzuständigkeiten neuer Aufgaben angepasst werden sollen.",
    cautionDescription: "Änderungen wirken auf neue Vorgänge und sollten mit dem betroffenen Fachbereich abgestimmt sein.",
    impactLabel: "Neue Vorgänge",
  },
  {
    key: "answers",
    label: "Antwortfelder",
    description: "Eingaben und Antwortlogik für neue Vorgänge pflegen.",
    group: "configuration",
    groupLabel: "Fachliche Konfiguration",
    groupDescription: "Steuert, wie neue Vorgänge vorbereitet und vorausgefüllt werden.",
    audienceLabel: "Geeignet für",
    cautionLabel: "Vorsicht bei",
    audienceDescription: "Wenn neue Eingabefelder benötigt werden oder bestehende Antwortlogik angepasst werden soll.",
    cautionDescription: "Feldänderungen können Vorlagen und Standardwerte beeinflussen.",
    impactLabel: "Abhängigkeiten prüfen",
  },
  {
    key: "defaults",
    label: "Standardwerte",
    description: "Vorauswahlen für Rollen und Bereiche pflegen.",
    group: "configuration",
    groupLabel: "Fachliche Konfiguration",
    groupDescription: "Steuert, wie neue Vorgänge vorbereitet und vorausgefüllt werden.",
    audienceLabel: "Geeignet für",
    cautionLabel: "Vorsicht bei",
    audienceDescription: "Wenn bestimmte Rollen oder Bereiche bei neuen Vorgängen automatisch vorbelegt werden sollen.",
    cautionDescription: "Die Wirkung zeigt sich oft erst in Kombination mit Antwortfeldern und Vorlagen.",
    impactLabel: "Vorbelegung",
  },
  {
    key: "access",
    label: "Zugriffe & Gruppen",
    description: "Direkte Rechte und Gruppenpflege für Ausnahmen und Berechtigungen.",
    group: "technical",
    groupLabel: "Technische Bereiche",
    groupDescription: "Greifen tiefer in Login, Rechte und Systemverhalten ein.",
    audienceLabel: "Geeignet für",
    cautionLabel: "Vorsicht bei",
    audienceDescription: "Wenn Rollen, Gruppen oder individuelle Zugriffe bewusst angepasst werden müssen.",
    cautionDescription: "Direkte Rollen wirken sofort; Gruppenänderungen betreffen oft mehrere Personen gleichzeitig.",
    impactLabel: "Wirkt sofort",
  },
  {
    key: "directory",
    label: "Entra-Verzeichnis",
    description: "Synchronisierung und Gruppenverknüpfungen mit Entra betreiben.",
    group: "technical",
    groupLabel: "Technische Bereiche",
    groupDescription: "Greifen tiefer in Login, Rechte und Systemverhalten ein.",
    audienceLabel: "Geeignet für",
    cautionLabel: "Vorsicht bei",
    audienceDescription: "Wenn Identitäten oder Gruppen aus Entra aktualisiert oder Rollen über Gruppen verknüpft werden sollen.",
    cautionDescription: "Gruppen-Rollen-Verknüpfungen gelten für alle Mitglieder der betroffenen Gruppe beim nächsten Login.",
    impactLabel: "Breite Wirkung",
  },
  {
    key: "system",
    label: "Benachrichtigungen & System",
    description: "Benachrichtigungen und prozessnahe Systemeinstellungen prüfen.",
    group: "technical",
    groupLabel: "Technische Bereiche",
    groupDescription: "Greifen tiefer in Login, Rechte und Systemverhalten ein.",
    audienceLabel: "Geeignet für",
    cautionLabel: "Vorsicht bei",
    audienceDescription: "Wenn Mailversand, Testempfänger oder prozessnahe Einstellungen kontrolliert werden müssen.",
    cautionDescription: "Aktiver Mailversand ohne Sandbox kann reale Empfänger erreichen.",
    impactLabel: "Systemweit",
  },
  {
    key: "operations",
    label: "Massenänderungen",
    description: "Serienaktionen mit großer Reichweite bewusst ausführen.",
    group: "sensitive",
    groupLabel: "Besonders wirkstark",
    groupDescription: "Sollte nur mit aktuellem Kontext und klarer Freigabe genutzt werden.",
    audienceLabel: "Geeignet für",
    cautionLabel: "Vorsicht bei",
    audienceDescription: "Wenn eine fachlich geprüfte Serienaktion für viele Personen wirklich notwendig ist.",
    cautionDescription: "Diese Aktionen erzeugen oder verändern viele Datensätze auf einmal und brauchen eine aktuelle Vorschau.",
    impactLabel: "Hohe Wirkung",
  },
];

export function getAdminWorkspaceSectionMeta(section: AdminWorkspaceSection): AdminWorkspaceSectionMeta {
  return (
    ADMIN_WORKSPACE_SECTION_META.find((entry) => entry.key === section)
    ?? ADMIN_WORKSPACE_SECTION_META[0]
  );
}

export function normalizeAdminWorkspaceSection(value: string | null): AdminWorkspaceSection {
  switch ((value ?? "").trim().toLowerCase()) {
    case "organization":
    case "templates":
    case "answers":
    case "defaults":
    case "access":
    case "directory":
    case "system":
    case "operations":
      return value!.trim().toLowerCase() as AdminWorkspaceSection;
    default:
      return "overview";
  }
}

export function normalizeAdminOrganizationEntity(value: string | null): AdminOrganizationEntity {
  switch ((value ?? "").trim().toLowerCase()) {
    case "department":
    case "responsibility":
      return value!.trim().toLowerCase() as AdminOrganizationEntity;
    default:
      return "user";
  }
}

export function parseAdminWorkspaceId(value: string | null): number | null {
  if (!value) {
    return null;
  }

  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

export function isEligibleSupervisorSelection(
  selectedUserId: string,
  eligibleSupervisorUsers: AdminUser[]
): boolean {
  return !selectedUserId || eligibleSupervisorUsers.some((user) => String(user.userId) === selectedUserId);
}

function toStoredUserId(value: number | null): string {
  return value ? String(value) : "";
}

export function hasDepartmentAssignmentChanges(
  department: AdminDepartmentAssignment,
  draft: { departmentLeadUserId: string; requirementOwnerUserId: string }
): boolean {
  return (
    draft.departmentLeadUserId !== toStoredUserId(department.departmentLeadUserId)
    || draft.requirementOwnerUserId !== toStoredUserId(department.requirementOwnerUserId)
  );
}

export function hasResponsibilityAssignmentChanges(
  responsibility: AdminResponsibilityOwner,
  draft: { appUserId: string; departmentId: string }
): boolean {
  return (
    draft.appUserId !== toStoredUserId(responsibility.appUserId)
    || draft.departmentId !== toStoredUserId(responsibility.departmentId)
  );
}

export function hasUserMasterDataChanges(args: {
  selectedUser: AdminUser;
  userExternalKeyDraft: string;
  userDisplayNameDraft: string;
  userEmailDraft: string;
  userNotificationEmailDraft: string;
  userDepartmentIdDraft: string;
  userIsActiveDraft: boolean;
}): boolean {
  const {
    selectedUser,
    userExternalKeyDraft,
    userDisplayNameDraft,
    userEmailDraft,
    userNotificationEmailDraft,
    userDepartmentIdDraft,
    userIsActiveDraft,
  } = args;

  return (
    toNullableText(userExternalKeyDraft) !== selectedUser.externalKey
    || userDisplayNameDraft.trim() !== selectedUser.displayName
    || userEmailDraft.trim() !== selectedUser.email
    || toNullableText(userNotificationEmailDraft) !== selectedUser.notificationEmail
    || toNullableNumber(userDepartmentIdDraft) !== selectedUser.departmentId
    || userIsActiveDraft !== selectedUser.isActive
  );
}

export function filterOrganizationUsers(args: {
  users: AdminUser[];
  search: string;
  activityFilter: "all" | "active" | "inactive";
  departmentFilter: string;
}): AdminUser[] {
  const { users, search, activityFilter, departmentFilter } = args;
  const normalizedSearch = search.trim().toLowerCase();

  return users.filter((user) => {
    if (activityFilter === "active" && !user.isActive) {
      return false;
    }

    if (activityFilter === "inactive" && user.isActive) {
      return false;
    }

    if (departmentFilter && String(user.departmentId ?? "") !== departmentFilter) {
      return false;
    }

    if (!normalizedSearch) {
      return true;
    }

    return [user.displayName, user.email, user.departmentName ?? "", user.notificationEmail ?? ""]
      .join(" ")
      .toLowerCase()
      .includes(normalizedSearch);
  });
}

export function filterOrganizationDepartments(args: {
  departments: AdminDepartmentAssignment[];
  search: string;
  leadFilter: "all" | "valid" | "invalid";
  ownerFilter: "all" | "valid" | "invalid";
  eligibleSupervisorUsers: AdminUser[];
}): AdminDepartmentAssignment[] {
  const { departments, search, leadFilter, ownerFilter, eligibleSupervisorUsers } = args;
  const eligibleIds = new Set(eligibleSupervisorUsers.map((user) => user.userId));
  const normalizedSearch = search.trim().toLowerCase();

  return departments.filter((department) => {
    const hasValidLead = Boolean(department.departmentLeadUserId && eligibleIds.has(department.departmentLeadUserId));
    const hasValidOwner = Boolean(
      department.requirementOwnerUserId && eligibleIds.has(department.requirementOwnerUserId)
    );

    if (leadFilter === "valid" && !hasValidLead) {
      return false;
    }

    if (leadFilter === "invalid" && hasValidLead) {
      return false;
    }

    if (ownerFilter === "valid" && !hasValidOwner) {
      return false;
    }

    if (ownerFilter === "invalid" && hasValidOwner) {
      return false;
    }

    if (!normalizedSearch) {
      return true;
    }

    return department.departmentName.toLowerCase().includes(normalizedSearch);
  });
}

export function filterOrganizationResponsibilities(args: {
  responsibilities: AdminResponsibilityOwner[];
  search: string;
  typeFilter: "all" | "process" | "application";
  personFilter: "all" | "with_person" | "without_person";
  departmentFilter: "all" | "with_department" | "without_department";
}): AdminResponsibilityOwner[] {
  const { responsibilities, search, typeFilter, personFilter, departmentFilter } = args;
  const normalizedSearch = search.trim().toLowerCase();

  return responsibilities.filter((responsibility) => {
    const isApplication = responsibility.responsibilityType === "application";
    const hasPerson = Boolean(responsibility.appUserId);
    const hasDepartment = Boolean(responsibility.departmentId);

    if (typeFilter === "process" && isApplication) {
      return false;
    }

    if (typeFilter === "application" && !isApplication) {
      return false;
    }

    if (personFilter === "with_person" && !hasPerson) {
      return false;
    }

    if (personFilter === "without_person" && hasPerson) {
      return false;
    }

    if (departmentFilter === "with_department" && !hasDepartment) {
      return false;
    }

    if (departmentFilter === "without_department" && hasDepartment) {
      return false;
    }

    if (!normalizedSearch) {
      return true;
    }

    return [responsibility.responsibilityName, responsibility.departmentName ?? "", responsibility.systemKey ?? ""]
      .join(" ")
      .toLowerCase()
      .includes(normalizedSearch);
  });
}

export function getUserRelations(args: {
  user: AdminUser;
  departments: AdminDepartmentAssignment[];
  responsibilities: AdminResponsibilityOwner[];
}) {
  const { user, departments, responsibilities } = args;

  return {
    ledDepartments: departments.filter((department) => department.departmentLeadUserId === user.userId),
    requirementDepartments: departments.filter((department) => department.requirementOwnerUserId === user.userId),
    responsibilities: responsibilities.filter((responsibility) => responsibility.appUserId === user.userId),
  };
}

export function getDepartmentRelations(args: {
  department: AdminDepartmentAssignment;
  users: AdminUser[];
  responsibilities: AdminResponsibilityOwner[];
}) {
  const { department, users, responsibilities } = args;

  return {
    users: users.filter((user) => user.departmentId === department.departmentId),
    responsibilities: responsibilities.filter((responsibility) => responsibility.departmentId === department.departmentId),
  };
}

export function getResponsibilityRelations(args: {
  responsibility: AdminResponsibilityOwner;
  users: AdminUser[];
  departments: AdminDepartmentAssignment[];
}) {
  const { responsibility, users, departments } = args;

  return {
    user: users.find((user) => user.userId === responsibility.appUserId) ?? null,
    department: departments.find((department) => department.departmentId === responsibility.departmentId) ?? null,
  };
}

export function buildAdminOverviewWarnings(args: {
  departments: AdminDepartmentAssignment[];
  responsibilities: AdminResponsibilityOwner[];
  eligibleSupervisorUsers: AdminUser[];
  notificationEmailConfiguration: AdminNotificationEmailConfiguration | null;
}): AdminWorkspaceWarning[] {
  const { departments, responsibilities, eligibleSupervisorUsers, notificationEmailConfiguration } = args;
  const eligibleIds = new Set(eligibleSupervisorUsers.map((user) => user.userId));
  const warnings: AdminWorkspaceWarning[] = [];

  for (const department of departments) {
    const hasValidLead = Boolean(department.departmentLeadUserId && eligibleIds.has(department.departmentLeadUserId));
    if (!hasValidLead) {
      warnings.push({
        key: `department-lead-${department.departmentId}`,
        title: `Abteilung ohne gültige Leitung: ${department.departmentName}`,
        detail: "Die gespeicherte Leitung fehlt oder hat keine aktive Manager-Berechtigung.",
        actionLabel: "Abteilung öffnen",
        targetEntity: "department",
        targetId: department.departmentId,
      });
    }

    const hasValidOwner = Boolean(
      department.requirementOwnerUserId && eligibleIds.has(department.requirementOwnerUserId)
    );
    if (!hasValidOwner) {
      warnings.push({
        key: `department-owner-${department.departmentId}`,
        title: `Abteilung ohne gültige Anforderungsverantwortung: ${department.departmentName}`,
        detail: "Die gespeicherte anforderungsverantwortliche Person fehlt oder ist nicht mehr gültig.",
        actionLabel: "Abteilung öffnen",
        targetEntity: "department",
        targetId: department.departmentId,
      });
    }
  }

  for (const responsibility of responsibilities) {
    if (!responsibility.appUserId) {
      warnings.push({
        key: `responsibility-user-${responsibility.responsibilityId}`,
        title: `Zuständigkeit ohne feste Person: ${responsibility.responsibilityName}`,
        detail: "Die Zuständigkeit ist aktuell nur über die Abteilung oder den Standardfall abgesichert.",
        actionLabel: "Zuständigkeit öffnen",
        targetEntity: "responsibility",
        targetId: responsibility.responsibilityId,
      });
    }

    if (!responsibility.departmentId) {
      warnings.push({
        key: `responsibility-department-${responsibility.responsibilityId}`,
        title: `Zuständigkeit ohne Bereich: ${responsibility.responsibilityName}`,
        detail: "Die Zuständigkeit hat aktuell keine saubere Bereichszuordnung.",
        actionLabel: "Zuständigkeit öffnen",
        targetEntity: "responsibility",
        targetId: responsibility.responsibilityId,
      });
    }
  }

  if (notificationEmailConfiguration?.configurationStatus === "incomplete") {
    warnings.push({
      key: "mail-incomplete",
      title: "Mail-Konfiguration unvollständig",
      detail: notificationEmailConfiguration.configurationMessage ?? "Die Mail-Konfiguration ist noch nicht vollständig.",
      actionLabel: "System öffnen",
      targetSection: "system",
    });
  }

  if (
    notificationEmailConfiguration?.enabled
    && !notificationEmailConfiguration.sandboxRedirectEmail?.trim()
  ) {
    warnings.push({
      key: "mail-no-sandbox",
      title: "Mailversand aktiv ohne Sandbox",
      detail: "Aktiver Versand ohne Weiterleitungsadresse sendet an die hinterlegten Empfänger.",
      actionLabel: "System öffnen",
      targetSection: "system",
    });
  }

  return warnings;
}
