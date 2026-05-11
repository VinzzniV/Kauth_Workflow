import { screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import DashboardOverview from "../src/components/dashboard/DashboardOverview";
import type { DashboardInsights } from "../src/components/dashboard/dashboardInsights";
import { renderWithApp } from "./testUtils";

vi.mock("../src/navigation/useRoleAwareNavigation", () => ({
  useRoleAwareNavigation: vi.fn(),
}));

vi.mock("../src/components/dashboard/useDashboardInsights", () => ({
  useDashboardInsights: vi.fn(),
}));

vi.mock("../src/services/queries/workflowDefinitionQueries", () => ({
  useStartableWorkflowDefinitions: vi.fn(),
}));

vi.mock("../src/components/dashboard/DashboardAdminRuntimeHealthBlock", () => ({
  default: () => <div>Betriebsstatus Runtime Block</div>,
}));

const { useRoleAwareNavigation } = await import("../src/navigation/useRoleAwareNavigation");
const { useDashboardInsights } = await import("../src/components/dashboard/useDashboardInsights");
const { useStartableWorkflowDefinitions } = await import("../src/services/queries/workflowDefinitionQueries");

const mockedUseRoleAwareNavigation = vi.mocked(useRoleAwareNavigation);
const mockedUseDashboardInsights = vi.mocked(useDashboardInsights);
const mockedUseStartableWorkflowDefinitions = vi.mocked(useStartableWorkflowDefinitions);

function createAdminInsights(): DashboardInsights {
  return {
    heading: "Administration",
    nextStep: "3 offene Admin-Warnungen bereinigen.",
    stats: [],
    queueTitle: "Operative Risiken",
    queueItems: [],
    emptyQueueText: "Keine offenen Admin-Aufgaben.",
    adminSummary: {
      statusKicker: "Governance",
      statusTitle: "3 offene Governance-Lücken priorisieren.",
      statusDetail: "Stammdaten-Lücken zuerst prüfen. 3 offene Warnungen sind gruppiert sichtbar.",
      action: {
        to: "/admin/config?section=abteilungen",
        label: "Warnungen prüfen",
        description: "Öffnet die kritischsten Governance-Lücken in der Administration.",
      },
      stats: [
        { label: "Admin-Warnungen", value: 3, note: "Stammdaten, Zuständigkeiten, Mail", tone: "attention" },
        { label: "Festhängende Vorgänge", value: 1, note: "seit ≥ 7 Tagen", tone: "attention" },
        { label: "Wartet auf Freigabe / Fachbereich", value: 2, note: "aktuelle Engpässe", tone: "attention" },
        { label: "Aktive Vorgänge", value: 9, note: "laufend", tone: "progress" },
      ],
    },
    adminWarnings: [
      {
        key: "stammdaten",
        title: "Stammdaten-Lücken",
        totalCount: 2,
        groups: [
          {
            key: "stammdaten-department-lead",
            title: "Abteilungen ohne gültige Leitung",
            detail: "In diesen Bereichen fehlt eine aktive Person mit Supervisor-Berechtigung.",
            count: 2,
            affectedLabels: ["IT", "Einkauf"],
            actionLabel: "Abteilungen prüfen",
            to: "/admin/config?section=abteilungen",
          },
        ],
      },
    ],
    adminOperations: [
      {
        key: "stuck-wf-1",
        title: "Steckt fest: Alice Example",
        detail: "Wartet auf Fachbereich – seit 20.04.2026",
        to: "/workflows/wf-1",
        actionLabel: "Öffnen",
        tone: "attention",
      },
    ],
  };
}

function createManagerInsights(): DashboardInsights {
  return {
    heading: "Vorgänge meiner Mitarbeitenden",
    nextStep: "Offene Freigaben zuerst bearbeiten.",
    stats: [{ label: "Aktive Vorgänge", value: 2, note: "laufend", tone: "progress" }],
    queueTitle: "Weitere Themen",
    queueItems: [
      {
        key: "manager-priority",
        title: "1 Vorgang wartet auf Ihre Rückmeldung.",
        detail: "Öffnen Sie die offenen Fälle im Leitungs-Schritt.",
        to: "/supervisor",
        actionLabel: "Freigaben öffnen",
      },
      {
        key: "manager-secondary",
        title: "HR-Rücklauf prüfen",
        detail: "Ein Vorgang wartet auf den nächsten Schritt.",
        to: "/workflows/wf-2",
        actionLabel: "Öffnen",
      },
    ],
    emptyQueueText: "Keine Aufgaben offen.",
  };
}

describe("DashboardOverview", () => {
  it("renders the admin dashboard without a process-type filter and with grouped governance warnings", () => {
    mockedUseRoleAwareNavigation.mockReturnValue({
      dashboardActions: [{ to: "/admin/config", label: "Administration", description: "..." }],
      secondaryDashboardActions: [],
      dashboardContext: {
        title: "Offene Admin-Aufgaben",
        description: "Warnungen bereinigen, Engpässe prüfen und Systemstatus im Blick behalten.",
      },
      dashboardPersona: "admin",
      headerNavItems: [],
    });
    mockedUseDashboardInsights.mockReturnValue({
      insights: createAdminInsights(),
      insightsError: null,
      isInsightsLoading: false,
      reloadInsights: vi.fn(),
    });
    mockedUseStartableWorkflowDefinitions.mockReturnValue({
      data: [],
      isLoading: false,
    } as never);

    renderWithApp(<DashboardOverview />, { roleKeys: ["auth_admin"] });

    expect(screen.queryByText("Prozesstyp")).toBeNull();
    expect(screen.getByText("Offene Themen")).toBeTruthy();
    expect(screen.getByText("Stammdaten-Lücken")).toBeTruthy();
    expect(screen.getByText("Abteilungen ohne gültige Leitung")).toBeTruthy();
    expect(screen.getByText("Betriebsstatus Runtime Block")).toBeTruthy();
    expect(screen.getByText("Operative Risiken")).toBeTruthy();
  });

  it("keeps the generic dashboard flow for non-admin personas including the process filter", () => {
    mockedUseRoleAwareNavigation.mockReturnValue({
      dashboardActions: [{ to: "/supervisor", label: "Freigaben", description: "..." }],
      secondaryDashboardActions: [],
      dashboardContext: {
        title: "Vorgänge meiner Mitarbeitenden",
        description: "Bearbeiten Sie offene Freigaben.",
      },
      dashboardPersona: "manager",
      headerNavItems: [],
    });
    mockedUseDashboardInsights.mockReturnValue({
      insights: createManagerInsights(),
      insightsError: null,
      isInsightsLoading: false,
      reloadInsights: vi.fn(),
    });
    mockedUseStartableWorkflowDefinitions.mockReturnValue({
      data: [
        {
          definitionKey: "onboarding",
          name: "Onboarding",
          requiresTargetPerson: false,

          latestPublishedVersionNumber: 1,
        },
        {
          definitionKey: "offboarding",
          name: "Offboarding",
          requiresTargetPerson: true,

          latestPublishedVersionNumber: 1,
        },
      ],
      isLoading: false,
    } as never);

    renderWithApp(<DashboardOverview />, { roleKeys: ["auth_manager"] });

    expect(screen.getByText("Prozesstyp")).toBeTruthy();
    expect(screen.getByText("Empfohlene Aktion")).toBeTruthy();
    expect(screen.queryByText("Betriebsstatus Runtime Block")).toBeNull();
  });
});
