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
  | "builder"
  | "answers"
  | "defaults"
  | "access"
  | "directory"
  | "system"
  | "operations";
export type AdminOrganizationEntity = "user" | "department" | "responsibility";
export type AdminWorkspaceArea = "organization" | "configuration" | "access" | "system";
export type AdminWorkspaceWarningCategory =
  | "department_lead"
  | "department_owner"
  | "responsibility_user"
  | "responsibility_department"
  | "mail_configuration";

export type AdminWorkspaceSectionMeta = {
  key: AdminWorkspaceSection;
  label: string;
  description: string;
  navLabel: string;
  navDescription: string;
  area: AdminWorkspaceArea | null;
  introTitle: string;
  introDescription: string;
  whatYouCanDo: string[];
  affectedObjects: string[];
  impactNote: string;
  riskNote: string;
  visibleInSubnav?: boolean;
  groupedWithSection?: AdminWorkspaceSection;
};

export type AdminWorkspaceAreaMeta = {
  key: AdminWorkspaceArea;
  label: string;
  description: string;
  defaultSection: AdminWorkspaceSection;
  sections: AdminWorkspaceSection[];
};

export type AdminWorkspaceWarning = {
  key: string;
  category: AdminWorkspaceWarningCategory;
  subjectLabel: string;
  title: string;
  detail: string;
  actionLabel: string;
  targetEntity?: AdminOrganizationEntity;
  targetId?: number | null;
  targetSection?: AdminWorkspaceSection;
};

export type AdminWorkspaceWarningGroup = {
  category: AdminWorkspaceWarningCategory;
  title: string;
  detail: string;
  count: number;
  affectedLabels: string[];
  actionLabel: string;
  targetEntity?: AdminOrganizationEntity;
  targetSection?: AdminWorkspaceSection;
};

export const ADMIN_WORKSPACE_SECTION_META: AdminWorkspaceSectionMeta[] = [
  {
    key: "overview",
    label: "Übersicht",
    description: "Systemzustand prüfen und offene Admin-Aufgaben priorisieren.",
    navLabel: "Übersicht",
    navDescription: "Systemzustand prüfen und offene Admin-Aufgaben priorisieren.",
    area: null,
    introTitle: "Gesamtzustand und offene Punkte im Blick behalten",
    introDescription:
      "Die Übersicht bündelt den aktuellen Zustand der Administration und zeigt, welche Themen zuerst Aufmerksamkeit brauchen.",
    whatYouCanDo: [
      "Offene Warnungen und Systemhinweise priorisieren",
      "In den passenden Bereich für die Bearbeitung springen",
      "Den aktuellen Zustand von Organisation, Rechten und System prüfen",
    ],
    affectedObjects: ["gesamte Administration", "offene Prüfaufgaben", "Betriebszustand der App"],
    impactNote: "Von hier aus ändern Sie noch nichts direkt, sondern entscheiden, was als Nächstes geprüft oder bearbeitet werden sollte.",
    riskNote: "Warnungen auf der Übersicht sind Hinweise auf fehlende Zuordnungen oder Konfigurationen, die in anderen Bereichen bereinigt werden sollten.",
  },
  {
    key: "organization",
    label: "Personen & Organisation",
    description: "Personen verwalten, Abteilungen pflegen und Zuständigkeiten sauber zuordnen.",
    navLabel: "Personen & Organisation",
    navDescription: "Personen, Abteilungen und Zuständigkeiten verständlich pflegen.",
    area: "organization",
    introTitle: "Personen, Bereiche und Verantwortungen aktuell halten",
    introDescription:
      "Hier pflegen Sie die organisatorische Grundlage der App. Änderungen wirken auf Verantwortlichkeiten, Bereichszuordnungen und Auswahlmöglichkeiten im laufenden Betrieb.",
    whatYouCanDo: [
      "Personen anlegen und Stammdaten pflegen",
      "Abteilungen mit Leitung und Anforderungsverantwortung hinterlegen",
      "Zuständigkeiten sauber einer Person oder einem Bereich zuordnen",
    ],
    affectedObjects: ["Benutzerkonten", "Abteilungen", "fachliche Zuständigkeiten"],
    impactNote: "Änderungen wirken sofort auf Zuordnungen, Filter, Verantwortlichkeiten und Prüfhinweise in der Administration.",
    riskNote: "Fehlende Leitung, fehlende Anforderungsverantwortung oder unklare Bereichszuordnungen erzeugen Lücken in nachgelagerten Prozessen.",
  },
  {
    key: "templates",
    label: "Aufgaben",
    description: "Aufgaben für neue Vorgänge strukturieren und die entstehende Vorgangslogik pflegen.",
    navLabel: "Aufgaben",
    navDescription: "Vorlagen für automatisch entstehende Aufgaben in neuen Vorgängen pflegen.",
    area: "configuration",
    introTitle: "Aufgaben definieren, die in neuen Vorgängen entstehen",
    introDescription:
      "Hier legen Sie fest, welche Aufgaben in einem Vorgang angelegt werden und unter welchen Bedingungen sie sichtbar oder abhängig voneinander sind.",
    whatYouCanDo: [
      "Aufgabenvorlagen pro Prozesstyp anlegen und pflegen",
      "Abhängigkeiten zwischen Aufgaben festlegen",
      "Bedingungen definieren, wann Aufgaben entstehen oder sichtbar werden",
    ],
    affectedObjects: ["Aufgabenvorlagen", "Abhängigkeiten", "Bedingungen und Vorgangslogik"],
    impactNote: "Änderungen wirken in der Regel auf neue Vorgänge. Laufende Vorgänge übernehmen diese Logik normalerweise nicht rückwirkend.",
    riskNote: "Falsch gesetzte Bedingungen oder Abhängigkeiten können dazu führen, dass Aufgaben zu früh, zu spät oder gar nicht erscheinen.",
  },
  {
    key: "builder",
    label: "Workflow Builder",
    description: "Workflow-Definitionen, Versionen, Nodes und Edges geführt konfigurieren.",
    navLabel: "Workflow Builder",
    navDescription: "Versionierte Workflow-Definitionen als Draft pflegen und veröffentlichen.",
    area: "configuration",
    introTitle: "Workflow-Definitionen geführt modellieren",
    introDescription:
      "Hier entsteht der erste formularbasierte Builder für versionierte Workflow-Definitionen inklusive Nodes, Edges, Validierung und Veröffentlichung.",
    whatYouCanDo: [
      "Workflow-Definitionen anlegen und Versionen als Draft pflegen",
      "Nodes und Kanten ohne freien JSON-Grafikeditor bearbeiten",
      "Validierungsfehler prüfen und gültige Drafts veröffentlichen",
    ],
    affectedObjects: ["Workflow-Definitionen", "Versionen", "Runtime-fähige Node- und Edge-Strukturen"],
    impactNote: "Änderungen wirken zunächst auf Drafts und erst nach Publish auf neu gestartete Runtime-Instanzen.",
    riskNote: "Ungespeicherte Entwurfsstände gehen beim Wechsel verloren. Publish sollte erst nach geprüfter Validierung erfolgen.",
  },
  {
    key: "answers",
    label: "Felder & Vorgaben",
    description: "Felder für neue Vorgänge definieren und Vorgaben je Rolle vorbereiten.",
    navLabel: "Felder & Vorgaben",
    navDescription: "Formularfelder und Vorgaben für neue Vorgänge gemeinsam steuern.",
    area: "configuration",
    introTitle: "Felder und Vorgaben für neue Vorgänge steuern",
    introDescription:
      "Hier definieren Sie Eingabefelder und legen fest, welche Vorgaben neue Vorgänge je Rolle oder Bereich mitbringen.",
    whatYouCanDo: [
      "Felder für einzelne Prozesstypen anlegen und pflegen",
      "Pflichtfelder, Eingabetypen und Sortierung festlegen",
      "Vorgaben je Rolle für neue Vorgänge vorbelegen",
    ],
    affectedObjects: ["Antwortfelder", "Vorgaben je Rolle", "Formulare in neuen Vorgängen"],
    impactNote: "Neue oder geänderte Felder prägen die Formulare neuer Vorgänge. Vorgaben helfen dabei, Eingaben für Rollen oder Bereiche vorzubereiten.",
    riskNote: "Strukturänderungen an Feldern sollten bewusst erfolgen, damit Folgekonfigurationen und bestehende Auswertungen verständlich bleiben.",
  },
  {
    key: "defaults",
    label: "Vorgaben je Rolle",
    description: "Vorgaben für neue Vorgänge je Rolle und Prozesstyp festlegen.",
    navLabel: "Vorgaben je Rolle",
    navDescription: "Vorgaben für neue Vorgänge je Rolle vorbereiten.",
    area: "configuration",
    introTitle: "Vorgaben für neue Vorgänge je Rolle festlegen",
    introDescription:
      "Hier bereiten Sie Vorauswahlen für neue Vorgänge vor, damit Rollen und Bereiche passende Werte bereits mitbringen.",
    whatYouCanDo: [
      "Vorgaben je Prozesstyp und Rolle pflegen",
      "Vorauswahlen für Eingabefelder festlegen",
      "Neue Vorgänge für typische Bearbeitungsfälle vorbereiten",
    ],
    affectedObjects: ["Vorgaben je Rolle", "Prozesstypen", "Formulare in neuen Vorgängen"],
    impactNote: "Diese Vorgaben werden bei neuen Vorgängen als vorbereitete Werte genutzt und beschleunigen die Bearbeitung.",
    riskNote: "Unpassende Vorgaben können in neuen Vorgängen falsche Vorauswahlen setzen und dadurch Folgearbeit verursachen.",
    visibleInSubnav: false,
    groupedWithSection: "answers",
  },
  {
    key: "access",
    label: "App-Rechte",
    description: "Standardzugriff über Rollen steuern und gezielte Ausnahmen für einzelne Personen pflegen.",
    navLabel: "App-Rechte",
    navDescription: "Standardrechte, Gruppen und Ausnahmen verständlich steuern.",
    area: "access",
    introTitle: "Standardzugriff und Ausnahmen verständlich steuern",
    introDescription:
      "Hier definieren Sie, welche Rechte Rollen grundsätzlich mitbringen und wo einzelne Personen oder Gruppen bewusst abweichend behandelt werden.",
    whatYouCanDo: [
      "Rollen als Standardzugriff pflegen",
      "Gruppen und direkte Rollen für einzelne Personen zuordnen",
      "Gezielte Ausnahmen für einzelne Benutzer dokumentieren",
    ],
    affectedObjects: ["Rollen", "Gruppen", "Berechtigungen und Benutzer-Ausnahmen"],
    impactNote: "Änderungen an Rollen oder Gruppen können viele Nutzer gleichzeitig betreffen. Direkte Ausnahmen wirken gezielt auf einzelne Personen.",
    riskNote: "Bitte immer prüfen, ob Rechte direkt, über Gruppen oder über Rollen wirken. Kombinationen können mehr oder weniger Zugriff erzeugen als beabsichtigt.",
  },
  {
    key: "directory",
    label: "Verzeichnis & Gruppen",
    description: "Externe Gruppen abgleichen, Identitäten prüfen und Gruppen mit App-Rollen verbinden.",
    navLabel: "Verzeichnis & Gruppen",
    navDescription: "Externe Gruppen holen, prüfen und mit App-Rollen verknüpfen.",
    area: "access",
    introTitle: "Verzeichnis-Sync und Gruppenanbindung verwalten",
    introDescription:
      "Hier holen Sie externe Gruppen und Identitäten in die App und koppeln Gruppenmitgliedschaften an Rollen in der Anwendung.",
    whatYouCanDo: [
      "Gruppen und Identitäten aus dem Verzeichnis synchronisieren",
      "Verknüpfte Identitäten prüfen",
      "Verzeichnisgruppen mit App-Rollen oder Bereichen verbinden",
    ],
    affectedObjects: ["externe Gruppen", "Identitäten", "Gruppen-Rollen-Zuordnungen"],
    impactNote: "Neue Gruppen-Zuordnungen wirken für alle Mitglieder der jeweiligen Gruppe, sobald die Mitgliedschaft beim Login ausgewertet wird.",
    riskNote: "Falsche Gruppen-Mappings verteilen Rechte schnell breit. Änderungen deshalb zuerst mit kleiner Gruppe oder nach gezieltem Sync prüfen.",
  },
  {
    key: "system",
    label: "Benachrichtigungen & Vorgänge",
    description: "Mailversand, Prozesstypen und Workflow-Grundkonfiguration für den laufenden Betrieb steuern.",
    navLabel: "Benachrichtigungen & Vorgänge",
    navDescription: "Laufenden Systembetrieb, Versand und Prozessgrundlagen konfigurieren.",
    area: "system",
    introTitle: "Laufenden Systembetrieb konfigurieren",
    introDescription:
      "Hier steuern Sie, wie Benachrichtigungen versendet werden, welche Prozesstypen verfügbar sind und welche Grundkonfiguration die App verwendet.",
    whatYouCanDo: [
      "Mailversand und Sandbox-Verhalten konfigurieren",
      "Prozesstypen und Workflow-Grundlagen pflegen",
      "Technische Anbindungen und App-Konfiguration prüfen",
    ],
    affectedObjects: ["Benachrichtigungen", "Prozesstypen", "Workflow- und Systemkonfiguration"],
    impactNote: "Viele Änderungen wirken sofort im laufenden Betrieb, etwa beim Mailversand oder bei der Verfügbarkeit von Prozesstypen.",
    riskNote: "Produktive Mail- oder Systemänderungen sollten bewusst geprüft werden, weil sie unmittelbar Nutzer und laufende Prozesse betreffen können.",
  },
  {
    key: "operations",
    label: "Massenaktionen",
    description: "Serienaktionen vorbereitet prüfen und erst nach Vorschau gezielt ausführen.",
    navLabel: "Massenaktionen",
    navDescription: "Serienaktionen mit Vorschau absichern und kontrolliert ausführen.",
    area: "system",
    introTitle: "Serienaktionen mit Vorschau absichern",
    introDescription:
      "Hier führen Sie Änderungen aus, die viele Datensätze oder Mitarbeitende gleichzeitig betreffen können.",
    whatYouCanDo: [
      "Massenläufe vorbereiten",
      "Vorschau prüfen, bevor reale Vorgänge erstellt werden",
      "Ergebnisse und Ausnahmen nach dem Lauf kontrollieren",
    ],
    affectedObjects: ["viele Personen oder Vorgänge gleichzeitig", "Zielabteilungen", "neu erzeugte Vorgänge"],
    impactNote: "Nach dem Ausführen entstehen reale Vorgänge oder breite Änderungen. Die Vorschau hilft, Umfang und Auswirkungen vorab zu prüfen.",
    riskNote: "Dieser Bereich ist bewusst risikobehaftet. Reale Aktionen sollten nur mit frischer Vorschau und klar geprüften Parametern gestartet werden.",
  },
];

export const ADMIN_WORKSPACE_AREA_META: AdminWorkspaceAreaMeta[] = [
  {
    key: "organization",
    label: "Personen & Organisation",
    description: "Stammdaten, Abteilungen und Zuständigkeiten pflegen.",
    defaultSection: "organization",
    sections: ["organization"],
  },
  {
    key: "access",
    label: "App-Zugriff",
    description: "Rechte, Gruppen und Verzeichnisanbindung gemeinsam steuern.",
    defaultSection: "access",
    sections: ["access", "directory"],
  },
  {
    key: "system",
    label: "Betrieb & Versand",
    description: "Laufende Systemkonfiguration und Serienaktionen steuern.",
    defaultSection: "system",
    sections: ["system", "operations"],
  },
];

export function getAdminWorkspaceSectionMeta(section: AdminWorkspaceSection): AdminWorkspaceSectionMeta {
  return (
    ADMIN_WORKSPACE_SECTION_META.find((entry) => entry.key === section)
    ?? ADMIN_WORKSPACE_SECTION_META[0]
  );
}

export function getAdminWorkspaceArea(section: AdminWorkspaceSection): AdminWorkspaceArea | null {
  return getAdminWorkspaceSectionMeta(section).area;
}

export function getAdminWorkspaceAreaMeta(area: AdminWorkspaceArea): AdminWorkspaceAreaMeta {
  return (
    ADMIN_WORKSPACE_AREA_META.find((entry) => entry.key === area)
    ?? ADMIN_WORKSPACE_AREA_META[0]
  );
}

export function getAdminWorkspaceSectionsForArea(area: AdminWorkspaceArea): AdminWorkspaceSectionMeta[] {
  const areaMeta = getAdminWorkspaceAreaMeta(area);
  return areaMeta.sections
    .map((section) => getAdminWorkspaceSectionMeta(section))
    .filter((sectionMeta) => sectionMeta.visibleInSubnav !== false);
}

export function getAdminWorkspacePresentationSection(section: AdminWorkspaceSection): AdminWorkspaceSection {
  const meta = getAdminWorkspaceSectionMeta(section);
  return meta.groupedWithSection ?? section;
}

export function normalizeAdminWorkspaceSection(value: string | null): AdminWorkspaceSection {
  switch ((value ?? "").trim().toLowerCase()) {
    case "organization":
    case "builder":
    case "access":
    case "directory":
    case "system":
    case "operations":
      return value!.trim().toLowerCase() as AdminWorkspaceSection;
    case "templates":
    case "answers":
    case "defaults":
      return "builder";
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
  eligibleRequirementOwnerUsers: AdminUser[];
}): AdminDepartmentAssignment[] {
  const { departments, search, leadFilter, ownerFilter, eligibleSupervisorUsers, eligibleRequirementOwnerUsers } = args;
  const eligibleLeadIds = new Set(eligibleSupervisorUsers.map((user) => user.userId));
  const eligibleOwnerIds = new Set(eligibleRequirementOwnerUsers.map((user) => user.userId));
  const normalizedSearch = search.trim().toLowerCase();

  return departments.filter((department) => {
    const hasValidLead = Boolean(department.departmentLeadUserId && eligibleLeadIds.has(department.departmentLeadUserId));
    const hasValidOwner = Boolean(
      department.requirementOwnerUserId && eligibleOwnerIds.has(department.requirementOwnerUserId)
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
  eligibleRequirementOwnerUsers: AdminUser[];
  notificationEmailConfiguration: AdminNotificationEmailConfiguration | null;
}): AdminWorkspaceWarning[] {
  const { departments, responsibilities, eligibleSupervisorUsers, eligibleRequirementOwnerUsers, notificationEmailConfiguration } = args;
  const eligibleLeadIds = new Set(eligibleSupervisorUsers.map((user) => user.userId));
  const eligibleOwnerIds = new Set(eligibleRequirementOwnerUsers.map((user) => user.userId));
  const warnings: AdminWorkspaceWarning[] = [];

  for (const department of departments) {
    const hasValidLead = Boolean(
      department.departmentLeadUserId && eligibleLeadIds.has(department.departmentLeadUserId)
    );
    if (!hasValidLead) {
      warnings.push({
        key: `department-lead-${department.departmentId}`,
        category: "department_lead",
        subjectLabel: department.departmentName,
        title: `Abteilung ohne gültige Leitung: ${department.departmentName}`,
        detail: "Die gespeicherte Leitung fehlt oder hat keine aktive Supervisor-Berechtigung.",
        actionLabel: "Abteilung öffnen",
        targetEntity: "department",
        targetId: department.departmentId,
      });
    }

    const hasValidOwner = Boolean(
      department.requirementOwnerUserId && eligibleOwnerIds.has(department.requirementOwnerUserId)
    );
    if (!hasValidOwner) {
      warnings.push({
        key: `department-owner-${department.departmentId}`,
        category: "department_owner",
        subjectLabel: department.departmentName,
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
        category: "responsibility_user",
        subjectLabel: responsibility.responsibilityName,
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
        category: "responsibility_department",
        subjectLabel: responsibility.responsibilityName,
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
      category: "mail_configuration",
      subjectLabel: "Mail-Konfiguration",
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
      category: "mail_configuration",
      subjectLabel: "Mailversand",
      title: "Mailversand aktiv ohne Sandbox",
      detail: "Aktiver Versand ohne Weiterleitungsadresse sendet an die hinterlegten Empfänger.",
      actionLabel: "System öffnen",
      targetSection: "system",
    });
  }

  return warnings;
}

const ADMIN_WARNING_GROUP_META: Record<AdminWorkspaceWarningCategory, {
  title: string;
  detail: string;
  actionLabel: string;
  targetEntity?: AdminOrganizationEntity;
  targetSection?: AdminWorkspaceSection;
}> = {
  department_lead: {
    title: "Abteilungen ohne gültige Leitung",
    detail: "In diesen Bereichen fehlt eine aktive Person mit Supervisor-Berechtigung.",
    actionLabel: "Abteilungen prüfen",
    targetEntity: "department",
  },
  department_owner: {
    title: "Abteilungen ohne Anforderungsverantwortung",
    detail: "Für diese Abteilungen ist keine gültige anforderungsverantwortliche Person hinterlegt.",
    actionLabel: "Abteilungen prüfen",
    targetEntity: "department",
  },
  responsibility_user: {
    title: "Zuständigkeiten ohne feste Person",
    detail: "Diese Zuständigkeiten sind aktuell nicht eindeutig einer Person zugeordnet.",
    actionLabel: "Zuständigkeiten prüfen",
    targetEntity: "responsibility",
  },
  responsibility_department: {
    title: "Zuständigkeiten ohne Bereich",
    detail: "Bei diesen Zuständigkeiten fehlt die organisatorische Bereichszuordnung.",
    actionLabel: "Zuständigkeiten prüfen",
    targetEntity: "responsibility",
  },
  mail_configuration: {
    title: "Mail- und Versandkonfiguration",
    detail: "Systemeinstellungen für Benachrichtigungen brauchen eine Prüfung.",
    actionLabel: "System prüfen",
    targetSection: "system",
  },
};

const ADMIN_WARNING_GROUP_ORDER: AdminWorkspaceWarningCategory[] = [
  "department_lead",
  "department_owner",
  "responsibility_user",
  "responsibility_department",
  "mail_configuration",
];

export function groupAdminOverviewWarnings(
  warnings: AdminWorkspaceWarning[]
): AdminWorkspaceWarningGroup[] {
  const groups: AdminWorkspaceWarningGroup[] = [];

  for (const category of ADMIN_WARNING_GROUP_ORDER) {
    const categoryWarnings = warnings.filter((warning) => warning.category === category);

    if (categoryWarnings.length === 0) {
      continue;
    }

    const meta = ADMIN_WARNING_GROUP_META[category];

    groups.push({
      category,
      title: meta.title,
      detail: meta.detail,
      count: categoryWarnings.length,
      affectedLabels: categoryWarnings.slice(0, 4).map((warning) => warning.subjectLabel),
      actionLabel: meta.actionLabel,
      targetEntity: meta.targetEntity,
      targetSection: meta.targetSection,
    });
  }

  return groups;
}
