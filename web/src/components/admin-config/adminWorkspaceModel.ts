import type {
  AdminDepartmentAssignment,
  AdminNotificationEmailConfiguration,
  AdminResponsibilityOwner,
  AdminUser,
} from "../../types/auth";
import { toNullableNumber, toNullableText } from "./adminConfigHelpers";

export type AdminWorkspaceSection =
  | "overview"
  | "personen"
  | "abteilungen"
  | "zustaendigkeiten"
  | "massnahmenvorlagen"
  | "templates"
  | "builder"
  | "answers"
  | "defaults"
  | "access"
  | "directory"
  | "system_logs"
  | "system_mail_templates"
  | "system_configuration";
export type AdminOrganizationEntity = "user" | "department" | "responsibility";
export type AdminWorkspaceArea = "personen_zugriff" | "massnahmen_rotation" | "system";
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

export type AdminWorkspaceWarningCluster = "stammdaten" | "zustaendigkeit" | "mail";

export type AdminWorkspaceWarningGroup = {
  category: AdminWorkspaceWarningCategory;
  cluster: AdminWorkspaceWarningCluster;
  title: string;
  detail: string;
  count: number;
  affectedLabels: string[];
  actionLabel: string;
  targetEntity?: AdminOrganizationEntity;
  targetId?: number | null;
  targetSection?: AdminWorkspaceSection;
};

export type AdminWorkspaceWarningClusterMeta = {
  cluster: AdminWorkspaceWarningCluster;
  title: string;
};

const ADMIN_WARNING_CLUSTER_BY_CATEGORY: Record<AdminWorkspaceWarningCategory, AdminWorkspaceWarningCluster> = {
  department_lead: "stammdaten",
  department_owner: "stammdaten",
  responsibility_user: "zustaendigkeit",
  responsibility_department: "zustaendigkeit",
  mail_configuration: "mail",
};

export const ADMIN_WARNING_CLUSTER_META: Record<AdminWorkspaceWarningCluster, AdminWorkspaceWarningClusterMeta> = {
  stammdaten: { cluster: "stammdaten", title: "Stammdaten-Lücken" },
  zustaendigkeit: { cluster: "zustaendigkeit", title: "Zuständigkeits-Lücken" },
  mail: { cluster: "mail", title: "Mail-Konfiguration" },
};

function hasAssignedResponsibilityUser(responsibility: AdminResponsibilityOwner): boolean {
  return Boolean(
    responsibility.appUserId
    || responsibility.appUserDisplayName?.trim()
  );
}

function hasAssignedResponsibilityDepartment(responsibility: AdminResponsibilityOwner): boolean {
  return Boolean(
    responsibility.departmentId
    || responsibility.departmentName?.trim()
  );
}

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
    key: "personen",
    label: "Personen",
    description: "Benutzerkonten anlegen, Stammdaten pflegen und Abteilungszuordnungen prüfen.",
    navLabel: "Personen",
    navDescription: "Benutzerkonten, Stammdaten und Abteilungszuordnungen pflegen.",
    area: "personen_zugriff",
    introTitle: "Benutzerkonten verwalten",
    introDescription:
      "Hier pflegen Sie die Benutzerkonten der App: Stammdaten, Abteilungszuordnung und Aktivierungsstatus.",
    whatYouCanDo: [
      "Personen anlegen und Stammdaten pflegen",
      "Abteilungszuordnung und Anzeigedaten aktualisieren",
      "Konten deaktivieren oder reaktivieren",
    ],
    affectedObjects: ["Benutzerkonten", "Login-Identitäten"],
    impactNote: "Änderungen wirken sofort auf Zuordnungen, Filter und Prüfhinweise in der Administration.",
    riskNote: "Falsche Abteilungszuordnungen oder Deaktivierungen können Berechtigungen und Aufgabenzuweisungen unbeabsichtigt verändern.",
  },
  {
    key: "abteilungen",
    label: "Abteilungen",
    description: "Abteilungen, Leitung und Anforderungsverantwortung pflegen.",
    navLabel: "Abteilungen",
    navDescription: "Abteilungen mit Leitung und Anforderungsverantwortung pflegen.",
    area: "personen_zugriff",
    introTitle: "Abteilungen und Verantwortlichkeiten pflegen",
    introDescription:
      "Hier pflegen Sie die organisatorische Struktur: Abteilungen, deren Leitung und die anforderungsverantwortlichen Personen.",
    whatYouCanDo: [
      "Abteilungen anlegen und umbenennen",
      "Leitung und Anforderungsverantwortung pro Abteilung zuweisen",
      "Bereichsspezifische Positionen pflegen",
    ],
    affectedObjects: ["Abteilungen", "Leitungs-Zuordnungen", "Anforderungsverantwortung"],
    impactNote: "Änderungen wirken sofort auf Zuordnungen, Filter und Prüfhinweise in der Administration.",
    riskNote: "Fehlende Leitung oder fehlende Anforderungsverantwortung erzeugen Lücken in nachgelagerten Prozessen.",
  },
  {
    key: "zustaendigkeiten",
    label: "Zuständigkeiten",
    description: "Fachliche Zuständigkeiten für Aufgaben in Vorgängen pflegen.",
    navLabel: "Zuständigkeiten",
    navDescription: "Fachliche Zuständigkeiten verwalten und Personen zuweisen.",
    area: "personen_zugriff",
    introTitle: "Fachliche Zuständigkeiten",
    introDescription:
      "Hier pflegen Sie fachliche Zuständigkeiten: wer für die Aufgaben in Onboarding-Vorgängen oder Abteilungsanforderungen verantwortlich ist.",
    whatYouCanDo: [
      "Fachliche Zuständigkeiten anlegen",
      "Personen oder Bereiche zuordnen",
      "Unkonfigurierte Zuständigkeiten erkennen und vervollständigen",
    ],
    affectedObjects: ["fachliche Zuständigkeiten", "Aufgabenzuweisungen in Vorgängen"],
    impactNote: "Änderungen wirken sofort auf zukünftige Aufgabenzuordnungen.",
    riskNote: "Das Löschen einer Zuständigkeit kann Aufgaben ohne Verantwortlichkeit hinterlassen.",
  },
  {
    key: "massnahmenvorlagen",
    label: "Maßnahmenvorlagen",
    description: "Vorlagen für Maßnahmen bei Eintritt oder Austritt in eine Abteilung pflegen.",
    navLabel: "Maßnahmenvorlagen",
    navDescription: "Maßnahmen für Eintritt und Austritt pro Abteilung pflegen.",
    area: "massnahmen_rotation",
    introTitle: "Maßnahmenvorlagen für Abteilungswechsel",
    introDescription:
      "Hier definieren Sie, welche Maßnahmen automatisch entstehen, wenn jemand in eine Abteilung eintritt oder sie verlässt — inklusive Zuständigkeit, Fälligkeit und optionaler Automatisierung.",
    whatYouCanDo: [
      "Maßnahmenvorlagen pro Abteilung und Auslöser konfigurieren",
      "Zuständige Stelle, Fälligkeit und Erinnerung definieren",
      "Automatisierbare Maßnahmen mit einem Automation-Key vorbereiten",
    ],
    affectedObjects: ["Maßnahmenvorlagen", "generierte Aufgaben in Durchläufen"],
    impactNote: "Neue Vorlagen werden bei der nächsten Task-Synchronisierung eines Plans aktiv.",
    riskNote: "Vorlagen sollten deaktiviert statt gelöscht werden, wenn laufende Pläne betroffen sind.",
  },
  {
    key: "templates",
    label: "Aufgaben",
    description: "Aufgaben für neue Vorgänge strukturieren und die entstehende Vorgangslogik pflegen.",
    navLabel: "Aufgaben",
    navDescription: "Vorlagen für automatisch entstehende Aufgaben in neuen Vorgängen pflegen.",
    area: null,
    visibleInSubnav: false,
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
    area: null,
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
    area: null,
    visibleInSubnav: false,
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
    area: null,
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
    area: "personen_zugriff",
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
    area: "personen_zugriff",
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
    key: "system_logs",
    label: "Logs",
    description: "Zentrale Betriebslogs, Fehler und technische Ereignisse app-weit überwachen.",
    navLabel: "Logs",
    navDescription: "Zentrale Betriebslogs und Fehlermeldungen prüfen.",
    area: "system",
    introTitle: "Zentrale System-Logs überwachen",
    introDescription:
      "Hier sehen Sie zentrale Betriebsereignisse der App, darunter Nutzerfehler, Backend-Fehler, Mailversand sowie Directory- und Automationsereignisse.",
    whatYouCanDo: [
      "Fehler, Warnungen und wichtige Systemereignisse filtern",
      "Nutzermeldungen, HTTP-Kontext und technische Details nachvollziehen",
      "Direkt aus Logs in betroffene Workflows, Pläne oder Tasks springen",
    ],
    affectedObjects: ["System-Logs", "Fehlerereignisse", "laufender Betrieb"],
    impactNote: "Die Logansicht ist die zentrale Stelle, um produktive Probleme, Nutzerfehler und technische Auffälligkeiten schnell einzugrenzen.",
    riskNote: "Leere oder fehlerhafte Logansichten verdecken betriebliche Probleme. Filter und Zeiträume sollten deshalb nachvollziehbar gesetzt werden.",
  },
  {
    key: "system_mail_templates",
    label: "Mail-Vorlagen",
    description: "Inhalt, Trigger-Info und Preview der versendeten System-Mails pflegen.",
    navLabel: "Mail-Vorlagen",
    navDescription: "Mailtexte, Trigger und echte Vorschau verwalten.",
    area: "system",
    introTitle: "Mailtexte fachlich steuern und realistisch prüfen",
    introDescription:
      "Hier pflegen Sie Betreff und Text der System-Mails. Die Vorschau nutzt echte Vorgänge oder Durchlaufpläne und zeigt nur Mails, die aktuell real auslösbar wären.",
    whatYouCanDo: [
      "Betreff und Text pro Mailtyp anpassen",
      "Erlaubte Platzhalter und Trigger je Mailtyp prüfen",
      "Mit echten Vorgängen oder Durchlaufplänen eine reale Vorschau rendern",
    ],
    affectedObjects: ["Mail-Vorlagen", "ausgehende Benachrichtigungen", "Preview auf Basis echter Vorgänge"],
    impactNote: "Änderungen wirken auf zukünftige Mails dieses Typs und bleiben unabhängig von der technischen Versandkonfiguration.",
    riskNote: "Unpassende Formulierungen oder fehlerhafte Platzhalter wirken sofort auf produktive Benachrichtigungen. Vorschau deshalb vor dem Speichern prüfen.",
  },
  {
    key: "system_configuration",
    label: "Konfiguration",
    description: "Mailversand und technische Laufzeitkonfiguration für den laufenden Betrieb steuern.",
    navLabel: "Konfiguration",
    navDescription: "Mailversand und technische Systemkonfiguration pflegen.",
    area: "system",
    introTitle: "Laufenden Systembetrieb konfigurieren",
    introDescription:
      "Hier steuern Sie, wie Benachrichtigungen versendet werden und welche technische Laufzeitkonfiguration die App verwendet.",
    whatYouCanDo: [
      "Mailversand und Sandbox-Verhalten konfigurieren",
      "Technische Anbindungen und App-Konfiguration prüfen",
    ],
    affectedObjects: ["Benachrichtigungen", "Graph-Anbindung", "Systemkonfiguration"],
    impactNote: "Viele Änderungen wirken sofort im laufenden Betrieb, etwa beim Mailversand oder bei technischen Laufzeitwerten.",
    riskNote: "Produktive Mail- oder Systemänderungen sollten bewusst geprüft werden, weil sie unmittelbar Nutzer und laufende Prozesse betreffen können.",
  },
];

export const ADMIN_WORKSPACE_AREA_META: AdminWorkspaceAreaMeta[] = [
  {
    key: "personen_zugriff",
    label: "Personen & Zugriff",
    description: "Personen, Abteilungen, Zuständigkeiten und App-Rechte gemeinsam pflegen.",
    defaultSection: "personen",
    sections: ["personen", "abteilungen", "zustaendigkeiten", "access", "directory"],
  },
  {
    key: "massnahmen_rotation",
    label: "Maßnahmen & Rotation",
    description: "Maßnahmenvorlagen für Eintritt, Austritt und Rotation steuern.",
    defaultSection: "massnahmenvorlagen",
    sections: ["massnahmenvorlagen"],
  },
  {
    key: "system",
    label: "System",
    description: "Zentrale Betriebslogs, Mail-Vorlagen und Systemkonfiguration steuern.",
    defaultSection: "system_logs",
    sections: ["system_logs", "system_mail_templates", "system_configuration"],
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
    case "personen":
    case "abteilungen":
    case "zustaendigkeiten":
    case "massnahmenvorlagen":
    case "builder":
    case "access":
    case "directory":
    case "system_logs":
    case "system_mail_templates":
    case "system_configuration":
      return value!.trim().toLowerCase() as AdminWorkspaceSection;
    case "organization":
      // Legacy alias — entity-aware split into personen/abteilungen happens in AdminConfigPage redirect.
      return "personen";
    case "rotation_requirements":
      return "zustaendigkeiten";
    case "system":
      return "system_logs";
    case "operations":
      return "system_logs";
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
  includeTechnicalActors?: boolean;
}): AdminUser[] {
  const { users, search, activityFilter, departmentFilter, includeTechnicalActors = false } = args;
  const normalizedSearch = search.trim().toLowerCase();

  return users.filter((user) => {
    if (!includeTechnicalActors && user.isTechnicalActor) {
      return false;
    }

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
    const hasPerson = hasAssignedResponsibilityUser(responsibility);
    const hasDepartment = hasAssignedResponsibilityDepartment(responsibility);

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
    if (!hasAssignedResponsibilityUser(responsibility)) {
      warnings.push({
        key: `responsibility-user-${responsibility.responsibilityId}`,
        category: "responsibility_user",
        subjectLabel: responsibility.responsibilityName,
        title: `Zuständigkeit ohne feste Person: ${responsibility.responsibilityName}`,
        detail: "Die Zuständigkeit ist aktuell nur über die Abteilung oder den Standardfall abgesichert.",
        actionLabel: "Zuständigkeit öffnen",
        targetSection: "zustaendigkeiten",
      });
    }

    if (!hasAssignedResponsibilityDepartment(responsibility)) {
      warnings.push({
        key: `responsibility-department-${responsibility.responsibilityId}`,
        category: "responsibility_department",
        subjectLabel: responsibility.responsibilityName,
        title: `Zuständigkeit ohne Bereich: ${responsibility.responsibilityName}`,
        detail: "Die Zuständigkeit hat aktuell keine saubere Bereichszuordnung.",
        actionLabel: "Zuständigkeit öffnen",
        targetSection: "zustaendigkeiten",
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
      targetSection: "system_configuration",
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
      targetSection: "system_configuration",
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
    targetSection: "zustaendigkeiten",
  },
  responsibility_department: {
    title: "Zuständigkeiten ohne Bereich",
    detail: "Bei diesen Zuständigkeiten fehlt die organisatorische Bereichszuordnung.",
    actionLabel: "Zuständigkeiten prüfen",
    targetSection: "zustaendigkeiten",
  },
  mail_configuration: {
    title: "Mail- und Versandkonfiguration",
    detail: "Systemeinstellungen für Benachrichtigungen brauchen eine Prüfung.",
    actionLabel: "System prüfen",
    targetSection: "system_configuration",
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
      cluster: ADMIN_WARNING_CLUSTER_BY_CATEGORY[category],
      title: meta.title,
      detail: meta.detail,
      count: categoryWarnings.length,
      affectedLabels: categoryWarnings.slice(0, 4).map((warning) => warning.subjectLabel),
      actionLabel: meta.actionLabel,
      targetEntity: meta.targetEntity,
      targetId: categoryWarnings[0]?.targetId ?? null,
      targetSection: meta.targetSection,
    });
  }

  return groups;
}

export type AdminWorkspaceWarningClusterBucket = {
  cluster: AdminWorkspaceWarningCluster;
  title: string;
  totalCount: number;
  groups: AdminWorkspaceWarningGroup[];
};

export function clusterAdminOverviewWarnings(
  warningGroups: AdminWorkspaceWarningGroup[]
): AdminWorkspaceWarningClusterBucket[] {
  const order: AdminWorkspaceWarningCluster[] = ["stammdaten", "zustaendigkeit", "mail"];
  return order
    .map<AdminWorkspaceWarningClusterBucket>((cluster) => {
      const groups = warningGroups.filter((group) => group.cluster === cluster);
      return {
        cluster,
        title: ADMIN_WARNING_CLUSTER_META[cluster].title,
        totalCount: groups.reduce((sum, group) => sum + group.count, 0),
        groups,
      };
    })
    .filter((bucket) => bucket.groups.length > 0);
}
