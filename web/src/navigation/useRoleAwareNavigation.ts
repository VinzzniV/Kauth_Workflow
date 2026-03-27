// Baut aus den Rollen des aktuellen Benutzers die sichtbare Navigation und den Dashboard-Kontext auf.
import { useMemo } from "react";
import { useCurrentUser } from "../auth/useCurrentUser";
import type { AppFeature, DashboardPersona, RoleCapabilities } from "../auth/roleModel";

export type HeaderNavItem = {
  to: string;
  label: string;
  end?: boolean;
};

export type DashboardAction = {
  to: string;
  label: string;
  description: string;
};

type NavActionDefinition = {
  to: string;
  label: string;
  description: string;
  feature: AppFeature;
  end?: boolean;
};

type NavigationContext = {
  title: string;
  description: string;
};

const ACTIONS = {
  dashboard: {
    to: "/",
    label: "Übersicht",
    description: "Ihr Einstieg in die Mitarbeiterprozesse.",
    feature: "dashboard",
    end: true,
  },
  hrCreate: {
    to: "/create",
    label: "Neuer Vorgang",
    description: "Einen neuen Mitarbeiterprozess anlegen.",
    feature: "workflowCreate",
  },
  managerCreate: {
    to: "/create",
    label: "Änderung starten",
    description: "Einen Änderungsprozess für Mitarbeitende starten.",
    feature: "workflowCreate",
  },
  hrWorkflows: {
    to: "/workflows",
    label: "Laufende Vorgänge",
    description: "Aktuelle Vorgänge und ihren Stand ansehen.",
    feature: "workflowOverview",
  },
  workflowSearch: {
    to: "/search",
    label: "Suche",
    description: "Vorgänge gezielt finden.",
    feature: "workflowSearch",
  },
  supervisorInbox: {
    to: "/supervisor",
    label: "Anforderungen der Abteilungsleitung",
    description: "Offene Anforderungen als Abteilungsleitung bearbeiten.",
    feature: "supervisorStep",
  },
  departmentTasks: {
    to: "/tasks/my",
    label: "Meine Aufgaben",
    description: "Offene Aufgaben Ihrer Fachbereiche bearbeiten.",
    feature: "technicalTasks",
  },
  adminConfig: {
    to: "/admin/config",
    label: "Verwaltung",
    description: "Stammdaten, Rollen und Gruppen pflegen.",
    feature: "adminConfig",
  },
} satisfies Record<string, NavActionDefinition>;

type ActionKey = keyof typeof ACTIONS;

function toAction(actionKey: ActionKey): DashboardAction {
  const action = ACTIONS[actionKey];
  return {
    to: action.to,
    label: action.label,
    description: action.description,
  };
}

function toHeaderNavItem(actionKey: ActionKey): HeaderNavItem {
  const action = ACTIONS[actionKey];
  return {
    to: action.to,
    label: action.label,
    end: "end" in action ? action.end : undefined,
  };
}

function collectActionKeys(args: {
  surface: "dashboard" | "header";
  capabilities: RoleCapabilities;
  canAccessFeature: (feature: AppFeature) => boolean;
}): ActionKey[] {
  const { surface, capabilities, canAccessFeature } = args;
  const keys: ActionKey[] = [];
  const addKey = (actionKey: ActionKey, isAllowed: boolean) => {
    if (!isAllowed || keys.includes(actionKey)) {
      return;
    }

    keys.push(actionKey);
  };

  addKey("dashboard", canAccessFeature("dashboard"));
  addKey("hrCreate", capabilities.hasHrRole && canAccessFeature("workflowCreate"));
  addKey("managerCreate", capabilities.hasManagerRole && !capabilities.hasHrRole && canAccessFeature("workflowCreate"));
  addKey("hrWorkflows", canAccessFeature("workflowOverview"));

  if (surface === "header") {
    addKey("workflowSearch", canAccessFeature("workflowSearch"));
  }

  addKey("supervisorInbox", capabilities.hasManagerRole && canAccessFeature("supervisorStep"));
  addKey("departmentTasks", capabilities.hasWorkerRole && canAccessFeature("technicalTasks"));
  addKey("adminConfig", capabilities.canManageAdminConfiguration && canAccessFeature("adminConfig"));

  return keys;
}

export function useRoleAwareNavigation() {
  const { canAccessFeature, capabilities } = useCurrentUser();

  const dashboardPersona = useMemo<DashboardPersona>(() => {
    return capabilities.hasMultipleRoles ? "generic" : capabilities.dashboardPersona;
  }, [capabilities.dashboardPersona, capabilities.hasMultipleRoles]);

  // Primaere Aktionen zeigen den naechsten sinnvollen Schritt fuer die jeweilige Rolle.
  const dashboardActions = useMemo<DashboardAction[]>(() => {
    return collectActionKeys({
      surface: "dashboard",
      capabilities,
      canAccessFeature,
    }).map(toAction);
  }, [canAccessFeature, capabilities]);

  const secondaryDashboardActions = useMemo<DashboardAction[]>(() => {
    // Bewusst leer: keine technische Neben-Navigation in der Demo.
    return [];
  }, []);

  // Der Kontext liefert lesbare Titel und Einordnung fuer die Startseite.
  const dashboardContext = useMemo<NavigationContext>(() => {
    if (capabilities.hasMultipleRoles) {
      return {
        title: "Ihre Arbeitsbereiche",
        description: "Sie haben Zugriff auf mehrere Bereiche. Wählen Sie den passenden Einstieg für Ihren aktuellen Prozessschritt.",
      };
    }

    if (capabilities.dashboardPersona === "admin") {
      return {
        title: "Verwaltung",
        description: "Hier pflegen Sie Stammdaten, Zuständigkeiten und Berechtigungen.",
      };
    }

    if (capabilities.dashboardPersona === "hr") {
      return {
        title: "HR-Übersicht",
        description: "Starten Sie neue Vorgänge und behalten Sie laufende Fälle im Blick.",
      };
    }

    if (capabilities.dashboardPersona === "manager") {
      return {
        title: "Vorgänge meiner Mitarbeitenden",
        description: "Starten Sie Änderungen für Ihre Mitarbeitenden, bearbeiten Sie offene Anforderungen und beobachten Sie den Fortschritt.",
      };
    }

    if (capabilities.dashboardPersona === "worker") {
      return {
        title: "Meine Aufgaben",
        description: "Hier bearbeiten Sie die offenen Aufgaben Ihrer Fachbereiche.",
      };
    }

    if (capabilities.dashboardPersona === "reader") {
      return {
        title: "Übersicht",
        description: "Sie sehen den Prozess im Lesemodus.",
      };
    }

    return {
      title: "Startbereich",
      description: "Nutzen Sie die freigegebenen Bereiche für Ihren Prozessschritt.",
    };
  }, [capabilities.dashboardPersona, capabilities.hasMultipleRoles]);

  // Die Kopf-Navigation bleibt bewusst kompakt und zeigt nur freigegebene Hauptbereiche.
  const headerNavItems = useMemo<HeaderNavItem[]>(() => {
    return collectActionKeys({
      surface: "header",
      capabilities,
      canAccessFeature,
    }).map(toHeaderNavItem);
  }, [canAccessFeature, capabilities]);

  return {
    headerNavItems,
    dashboardActions,
    secondaryDashboardActions,
    dashboardContext,
    dashboardPersona,
  };
}
