// Baut aus den Rollen des aktuellen Benutzers die sichtbare Navigation und den Dashboard-Kontext auf.
import { createElement, type ReactNode, useMemo } from "react";
import { useCurrentUser } from "../auth/useCurrentUser";
import type { AppFeature, DashboardPersona, RoleCapabilities } from "../auth/roleModel";

export type HeaderNavItem = {
  to: string;
  label: string;
  icon: ReactNode;
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
  icon: ReactNode;
  end?: boolean;
};

type NavigationContext = {
  title: string;
  description: string;
};

function createIcon(...children: ReactNode[]): ReactNode {
  return createElement(
    "svg",
    {
      viewBox: "0 0 24 24",
      fill: "none",
      stroke: "currentColor",
      strokeWidth: "1.8",
      strokeLinecap: "round",
      strokeLinejoin: "round",
      "aria-hidden": "true",
    },
    ...children
  );
}

const homeIcon = createIcon(
  createElement("path", {
    d: "M3.75 10.5 12 4.5l8.25 6v8.25a.75.75 0 0 1-.75.75h-4.5v-5.25a.75.75 0 0 0-.75-.75h-3a.75.75 0 0 0-.75.75v5.25H4.5a.75.75 0 0 1-.75-.75V10.5Z",
  })
);
const plusCircleIcon = createIcon(
  createElement("circle", { cx: "12", cy: "12", r: "8.25" }),
  createElement("path", { d: "M12 8.5v7" }),
  createElement("path", { d: "M8.5 12h7" })
);
const listBulletIcon = createIcon(
  createElement("path", { d: "M8.25 6.75h11.25" }),
  createElement("path", { d: "M8.25 12h11.25" }),
  createElement("path", { d: "M8.25 17.25h11.25" }),
  createElement("circle", { cx: "4.5", cy: "6.75", r: "1", fill: "currentColor", stroke: "none" }),
  createElement("circle", { cx: "4.5", cy: "12", r: "1", fill: "currentColor", stroke: "none" }),
  createElement("circle", { cx: "4.5", cy: "17.25", r: "1", fill: "currentColor", stroke: "none" })
);
const magnifyingGlassIcon = createIcon(
  createElement("circle", { cx: "11", cy: "11", r: "5.5" }),
  createElement("path", { d: "m15 15 4.25 4.25" })
);
const checkBadgeIcon = createIcon(
  createElement("path", { d: "m9.1 11.9 1.8 1.8 4-4" }),
  createElement("path", {
    d: "M9.75 4.75A2.25 2.25 0 0 1 12 3.5a2.25 2.25 0 0 1 2.25 1.25 2.25 2.25 0 0 0 2.66 1.17 2.25 2.25 0 0 1 2.77 2 2.25 2.25 0 0 0 1.57 2.45 2.25 2.25 0 0 1 0 3.26 2.25 2.25 0 0 0-1.57 2.45 2.25 2.25 0 0 1-2.77 2 2.25 2.25 0 0 0-2.66 1.17A2.25 2.25 0 0 1 12 20.5a2.25 2.25 0 0 1-2.25-1.25 2.25 2.25 0 0 0-2.66-1.17 2.25 2.25 0 0 1-2.77-2 2.25 2.25 0 0 0-1.57-2.45 2.25 2.25 0 0 1 0-3.26 2.25 2.25 0 0 0 1.57-2.45 2.25 2.25 0 0 1 2.77-2 2.25 2.25 0 0 0 2.66-1.17Z",
  })
);
const clipboardListIcon = createIcon(
  createElement("path", { d: "M9 4.75h6" }),
  createElement("path", {
    d: "M9.75 3.75h4.5a1.5 1.5 0 0 1 1.5 1.5v.5h1a1.75 1.75 0 0 1 1.75 1.75v10.75A1.75 1.75 0 0 1 16.75 20H7.25A1.75 1.75 0 0 1 5.5 18.25V7.5A1.75 1.75 0 0 1 7.25 5.75h1v-.5a1.5 1.5 0 0 1 1.5-1.5Z",
  }),
  createElement("path", { d: "M9 10h6" }),
  createElement("path", { d: "M9 13.5h6" }),
  createElement("path", { d: "M9 17h3.5" })
);
const cogIcon = createIcon(
  createElement("circle", { cx: "12", cy: "12", r: "2.75" }),
  createElement("path", {
    d: "M19.25 12a7.24 7.24 0 0 0-.08-1.05l1.72-1.34-1.75-3.03-2.08.84a7.38 7.38 0 0 0-1.82-1.05l-.33-2.21H9.09l-.33 2.21c-.65.23-1.26.58-1.82 1.05l-2.08-.84-1.75 3.03 1.72 1.34A7.24 7.24 0 0 0 4.75 12c0 .36.03.71.08 1.05l-1.72 1.34 1.75 3.03 2.08-.84c.56.47 1.17.82 1.82 1.05l.33 2.21h3.82l.33-2.21c.65-.23 1.26-.58 1.82-1.05l2.08.84 1.75-3.03-1.72-1.34c.05-.34.08-.69.08-1.05Z",
  })
);

const ACTIONS = {
  dashboard: {
    to: "/",
    label: "Übersicht",
    description: "Ihr Einstieg in die Mitarbeiterprozesse.",
    feature: "dashboard",
    icon: homeIcon,
    end: true,
  },
  hrCreate: {
    to: "/create",
    label: "Neuer Vorgang",
    description: "Einen neuen Mitarbeiterprozess anlegen.",
    feature: "workflowCreate",
    icon: plusCircleIcon,
  },
  managerCreate: {
    to: "/create",
    label: "Änderung starten",
    description: "Einen Änderungsprozess für Mitarbeitende starten.",
    feature: "workflowCreate",
    icon: plusCircleIcon,
  },
  hrWorkflows: {
    to: "/workflows",
    label: "Laufende Vorgänge",
    description: "Laufende Vorgänge steuern und offene Arbeit priorisieren.",
    feature: "workflowOverview",
    icon: listBulletIcon,
  },
  workflowSearch: {
    to: "/search",
    label: "Vorgänge suchen",
    description: "Vorgänge gezielt über Namen, IDs und Filter finden.",
    feature: "workflowSearch",
    icon: magnifyingGlassIcon,
  },
  supervisorInbox: {
    to: "/supervisor",
    label: "Anforderungen der Abteilungsleitung",
    description: "Offene Anforderungen als Abteilungsleitung bearbeiten.",
    feature: "supervisorStep",
    icon: checkBadgeIcon,
  },
  departmentTasks: {
    to: "/tasks/my",
    label: "Meine Aufgaben",
    description: "Offene Aufgaben Ihrer Fachbereiche bearbeiten.",
    feature: "technicalTasks",
    icon: clipboardListIcon,
  },
  adminConfig: {
    to: "/admin/config",
    label: "Administration",
    description: "Organisation, Rechte und Systemeinstellungen pflegen.",
    feature: "adminConfig",
    icon: cogIcon,
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
    icon: action.icon,
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
  addKey("departmentTasks", canAccessFeature("technicalTasks"));
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
    // Bewusst leer: keine technische Neben-Navigation im Standard-Dashboard.
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
        description: "Hier pflegen Sie Organisation, Zuständigkeiten, Berechtigungen und Systemeinstellungen.",
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
