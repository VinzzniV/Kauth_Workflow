import { fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import CreateWorkflowPage from "../src/pages/CreateWorkflowPage";
import * as lifecycleApi from "../src/services/lifecycleApi";
import { renderWithApp } from "./testUtils";

vi.mock("../src/services/lifecycleApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/lifecycleApi")>("../src/services/lifecycleApi");
  return {
    ...actual,
    getProcessTypes: vi.fn(),
    getWorkflowConfig: vi.fn(),
    searchCompletedOnboardings: vi.fn(),
    getRoles: vi.fn(),
    getDepartments: vi.fn(),
    createWorkflow: vi.fn(),
  };
});

const mockedGetProcessTypes = vi.mocked(lifecycleApi.getProcessTypes);
const mockedGetWorkflowConfig = vi.mocked(lifecycleApi.getWorkflowConfig);
const mockedSearchCompletedOnboardings = vi.mocked(lifecycleApi.searchCompletedOnboardings);
const mockedGetRoles = vi.mocked(lifecycleApi.getRoles);
const mockedGetDepartments = vi.mocked(lifecycleApi.getDepartments);

describe("CreateWorkflowPage", () => {
  beforeEach(() => {
    vi.useRealTimers();
    mockedGetProcessTypes.mockReset();
    mockedGetWorkflowConfig.mockReset();
    mockedSearchCompletedOnboardings.mockReset();
    mockedGetRoles.mockReset();
    mockedGetDepartments.mockReset();

    mockedGetWorkflowConfig.mockResolvedValue({
      requirements: [],
      roleRecommendations: {
        recommendedRequirementIds: [],
        defaultValues: [],
        defaultSelectedOptions: [],
      },
    });
    mockedSearchCompletedOnboardings.mockResolvedValue([
      {
        workflowUid: "wf-completed-1",
        personId: 22,
        displayName: "Ada Lovelace",
        firstName: "Ada",
        lastName: "Lovelace",
        employeeNumber: 1001,
        badgeNumber: 2002,
        departmentId: 10,
        departmentName: "IT",
        roleId: 7,
        roleName: "Engineer",
        completedAt: "2026-03-20T10:00:00.000Z",
        archivedAt: null,
      },
    ]);
    mockedGetDepartments.mockResolvedValue([
      { id: 10, name: "IT" },
      { id: 20, name: "HR" },
    ]);
    mockedGetRoles.mockResolvedValue([
      {
        id: 7,
        departmentId: 10,
        departmentName: "IT",
        name: "Engineer",
        isActive: true,
      },
      {
        id: 8,
        departmentId: 20,
        departmentName: "HR",
        name: "Recruiter",
        isActive: true,
      },
    ]);
  });

  it("starts with an explicit process step even when only one process type is available", async () => {
    mockedGetProcessTypes.mockResolvedValue([
      {
        key: "department_change",
        name: "Abteilungswechsel",
        description: "Mitarbeiter in eine neue Abteilung versetzen.",
        requiresTargetPerson: true,
      },
    ]);

    renderWithApp(<CreateWorkflowPage />, { roleKeys: ["auth_manager"] });

    expect(await screen.findByText("Schritt 1: Vorgang wählen")).toBeTruthy();
    expect(screen.getByRole("button", { name: /Abteilungswechsel/i })).toBeTruthy();
    expect(screen.queryByText("Daten der neuen Person")).toBeNull();
  });

  it("shows the new-person form for HR onboarding after process selection", async () => {
    mockedGetProcessTypes.mockResolvedValue([
      {
        key: "onboarding",
        name: "Onboarding",
        description: "Neue Person anlegen.",
        requiresTargetPerson: false,
      },
      {
        key: "offboarding",
        name: "Offboarding",
        description: "Bestehende Person austreten lassen.",
        requiresTargetPerson: true,
      },
    ]);

    renderWithApp(<CreateWorkflowPage />, { roleKeys: ["auth_hr"] });

    expect(await screen.findByText("Schritt 1: Vorgang wählen")).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: /Onboarding/i }));
    fireEvent.click(screen.getByRole("button", { name: "Weiter zu Schritt 2" }));

    expect(await screen.findByText("Schritt 2: Neue Person erfassen")).toBeTruthy();
    expect(screen.getByText("Daten der neuen Person")).toBeTruthy();
    expect(screen.getByText("Stelle und Abteilung")).toBeTruthy();
  });

  it("shows the existing-person selection for managers and never the new-person form", async () => {
    mockedGetProcessTypes.mockResolvedValue([
      {
        key: "department_change",
        name: "Abteilungswechsel",
        description: "Bestehende Person in eine neue Abteilung verschieben.",
        requiresTargetPerson: true,
      },
    ]);

    renderWithApp(<CreateWorkflowPage />, { roleKeys: ["auth_manager"] });

    expect(await screen.findByText("Änderung starten")).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Weiter zu Schritt 2" }));

    expect(await screen.findByText("Schritt 2: Bestehende Person wählen")).toBeTruthy();
    expect(screen.getByText("Abgeschlossenes Onboarding auswählen")).toBeTruthy();
    expect(screen.queryByText("Daten der neuen Person")).toBeNull();
  });

  it("does not refetch completed onboardings endlessly after selecting a person", async () => {
    mockedGetProcessTypes.mockResolvedValue([
      {
        key: "department_change",
        name: "Abteilungswechsel",
        description: "Bestehende Person in eine neue Abteilung verschieben.",
        requiresTargetPerson: true,
      },
    ]);

    renderWithApp(<CreateWorkflowPage />, { roleKeys: ["auth_manager"] });

    expect(await screen.findByText("Änderung starten")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Weiter zu Schritt 2" }));
    expect(await screen.findByText("Schritt 2: Bestehende Person wählen")).toBeTruthy();

    await waitFor(() => expect(mockedSearchCompletedOnboardings).toHaveBeenCalledTimes(1));

    fireEvent.click(screen.getByRole("radio"));

    await new Promise((resolve) => window.setTimeout(resolve, 400));
    await waitFor(() => expect(mockedSearchCompletedOnboardings).toHaveBeenCalledTimes(1));
  });

  it("resets stale context when the process type changes", async () => {
    mockedGetProcessTypes.mockResolvedValue([
      {
        key: "onboarding",
        name: "Onboarding",
        description: "Neue Person anlegen.",
        requiresTargetPerson: false,
      },
      {
        key: "offboarding",
        name: "Offboarding",
        description: "Bestehende Person auswählen.",
        requiresTargetPerson: true,
      },
    ]);

    renderWithApp(<CreateWorkflowPage />, { roleKeys: ["auth_hr"] });

    expect(await screen.findByText("Schritt 1: Vorgang wählen")).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: /Onboarding/i }));
    fireEvent.click(screen.getByRole("button", { name: "Weiter zu Schritt 2" }));
    fireEvent.change(screen.getByLabelText("Vorname"), { target: { value: "Ada" } });

    fireEvent.click(screen.getByRole("button", { name: "Zurück zu Schritt 1" }));
    fireEvent.click(screen.getByRole("button", { name: /Offboarding/i }));
    fireEvent.click(screen.getByRole("button", { name: "Weiter zu Schritt 2" }));

    expect(await screen.findByText("Schritt 2: Bestehende Person wählen")).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Zurück zu Schritt 1" }));
    fireEvent.click(screen.getByRole("button", { name: /Onboarding/i }));
    fireEvent.click(screen.getByRole("button", { name: "Weiter zu Schritt 2" }));

    expect(await screen.findByText("Schritt 2: Neue Person erfassen")).toBeTruthy();
    expect((screen.getByLabelText("Vorname") as HTMLInputElement).value).toBe("");
  });

  it("shows a review step with the selected onboarding context", async () => {
    mockedGetProcessTypes.mockResolvedValue([
      {
        key: "onboarding",
        name: "Onboarding",
        description: "Neue Person anlegen.",
        requiresTargetPerson: false,
      },
    ]);

    renderWithApp(<CreateWorkflowPage />, { roleKeys: ["auth_hr"] });

    expect(await screen.findByText("Schritt 1: Vorgang wählen")).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Weiter zu Schritt 2" }));
    fireEvent.change(screen.getByLabelText("Vorname"), { target: { value: "Ada" } });
    fireEvent.change(screen.getByLabelText("Nachname"), { target: { value: "Lovelace" } });
    fireEvent.change(screen.getByLabelText("Personalnummer"), { target: { value: "1001" } });
    fireEvent.change(screen.getByLabelText("Kartennummer"), { target: { value: "2002" } });
    fireEvent.change(screen.getByLabelText("Abteilung *"), { target: { value: "10" } });
    fireEvent.change(screen.getByLabelText("Stelle *"), { target: { value: "7" } });
    fireEvent.click(screen.getByRole("button", { name: "Weiter zu Schritt 3" }));

    expect(await screen.findByText("Schritt 3: Prüfen und anlegen")).toBeTruthy();
    expect(screen.getAllByText("Ada Lovelace").length).toBeGreaterThan(0);
    expect(screen.getByText("Engineer")).toBeTruthy();
    expect(screen.getByText("IT")).toBeTruthy();
  });
});
