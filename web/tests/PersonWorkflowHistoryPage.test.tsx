import { fireEvent, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { Route, Routes } from "react-router-dom";
import PersonWorkflowHistoryPage from "../src/pages/PersonWorkflowHistoryPage";
import * as peopleApi from "../src/services/peopleApi";
import { renderWithApp } from "./testUtils";
import type { PersonWorkflowHistory } from "../src/types/workflow";

vi.mock("../src/services/peopleApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/peopleApi")>("../src/services/peopleApi");
  return {
    ...actual,
    getPersonWorkflowHistory: vi.fn(),
  };
});

const mockedGetPersonWorkflowHistory = vi.mocked(peopleApi.getPersonWorkflowHistory);

function createPersonWorkflowHistory(): PersonWorkflowHistory {
  return {
    personId: 11,
    displayName: "Anika Sattler",
    departmentId: 3,
    departmentName: "BS",
    roleId: 5,
    roleName: "Studentin",
    employeeNumber: 4711,
    badgeNumber: 98,
    firstName: "Anika",
    lastName: "Sattler",
    employmentStatus: "active",
    entryDate: "2026-06-01",
    exitDate: null,
    appUserId: null,
    directoryIdentityId: null,
    directoryLinkStatus: "unlinked",
    directoryDisplayName: null,
    directoryUserPrincipalName: null,
    directoryMail: null,
    directoryEmployeeNumber: null,
    latestSourceWorkflowUid: "wf-onboarding-1",
    latestSourceWorkflowCompletedAt: "2026-06-01T08:00:00.000Z",
    workflows: [
      {
        uid: "wf-onboarding-1",
        workflowDefinition: {
          key: "onboarding",
          name: "Onboarding",
          requiresTargetPerson: true,
        },
        firstName: "Anika",
        lastName: "Sattler",
        roleName: "Studentin",
        departmentName: "BS",
        workflowStatus: "completed",
        createdAt: "2026-05-20T08:00:00.000Z",
        completedAt: "2026-06-01T08:00:00.000Z",
        archivedAt: null,
      },
    ],
  };
}

describe("PersonWorkflowHistoryPage", () => {
  beforeEach(() => {
    mockedGetPersonWorkflowHistory.mockReset();
  });

  it("offers a table mode for the person workflow history", async () => {
    mockedGetPersonWorkflowHistory.mockResolvedValue(createPersonWorkflowHistory());

    renderWithApp(
      <Routes>
        <Route path="/people/:personId" element={<PersonWorkflowHistoryPage />} />
      </Routes>,
      { roleKeys: ["auth_hr"], route: "/people/11" }
    );

    fireEvent.click(await screen.findByRole("tab", { name: /Vorgänge/ }));

    expect(await screen.findByRole("heading", { name: "Vorgänge (1)" })).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Tabelle" }));

    expect(screen.getByRole("table", { name: "Tabellenansicht Vorgänge dieser Person" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Erstellt" })).toBeTruthy();
  });

  it("renders the 360-degree workspace tabs and overview metrics", async () => {
    mockedGetPersonWorkflowHistory.mockResolvedValue(createPersonWorkflowHistory());

    renderWithApp(
      <Routes>
        <Route path="/people/:personId" element={<PersonWorkflowHistoryPage />} />
      </Routes>,
      { roleKeys: ["auth_hr"], route: "/people/11" }
    );

    expect(await screen.findByRole("heading", { name: "Anika Sattler" })).toBeTruthy();
    expect(screen.getByRole("tab", { name: /Übersicht/ })).toBeTruthy();
    expect(screen.getByRole("tab", { name: /Offene Aufgaben/ })).toBeTruthy();
    expect(screen.getByRole("tab", { name: /Benachrichtigungen/ })).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Stammdaten" })).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Verzeichnis-Kontext" })).toBeTruthy();
  });
});
