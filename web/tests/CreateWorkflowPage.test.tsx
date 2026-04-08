import { fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import CreateWorkflowPage from "../src/pages/CreateWorkflowPage";
import * as lookupApi from "../src/services/lookupApi";
import * as workflowApi from "../src/services/workflowApi";
import { renderWithApp } from "./testUtils";

vi.mock("../src/services/lookupApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/lookupApi")>("../src/services/lookupApi");
  return {
    ...actual,
    getStartableWorkflowDefinitions: vi.fn(),
    getRoles: vi.fn(),
    getDepartments: vi.fn(),
  };
});

vi.mock("../src/services/workflowApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/workflowApi")>("../src/services/workflowApi");
  return {
    ...actual,
    getWorkflowConfig: vi.fn(),
    searchWorkflowTargetPersonSources: vi.fn(),
    createWorkflow: vi.fn(),
  };
});

const mockedGetStartableWorkflowDefinitions = vi.mocked(lookupApi.getStartableWorkflowDefinitions);
const mockedGetWorkflowConfig = vi.mocked(workflowApi.getWorkflowConfig);
const mockedSearchWorkflowTargetPersonSources = vi.mocked(workflowApi.searchWorkflowTargetPersonSources);
const mockedGetRoles = vi.mocked(lookupApi.getRoles);
const mockedGetDepartments = vi.mocked(lookupApi.getDepartments);

describe("CreateWorkflowPage", () => {
  beforeEach(() => {
    vi.useRealTimers();
    mockedGetStartableWorkflowDefinitions.mockReset();
    mockedGetWorkflowConfig.mockReset();
    mockedSearchWorkflowTargetPersonSources.mockReset();
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
    mockedSearchWorkflowTargetPersonSources.mockResolvedValue([
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

  it("starts with an explicit workflow step even when only one definition is available", async () => {
    mockedGetStartableWorkflowDefinitions.mockResolvedValue([
      {
        definitionKey: "department_change",
        name: "Abteilungswechsel",
        description: "Mitarbeiter in eine neue Abteilung versetzen.",
        requiresTargetPerson: true,
        primaryLegacyProcessTypeKey: "department_change",
        latestPublishedVersionNumber: 1,
      },
    ]);

    renderWithApp(<CreateWorkflowPage />, { roleKeys: ["auth_manager"] });

    expect(await screen.findByRole("heading", { name: "Workflow wählen" })).toBeTruthy();
    expect(await screen.findByText("Abteilungswechsel")).toBeTruthy();
    expect(screen.queryByText("Daten der neuen Person")).toBeNull();
  });

  it("shows the new-person form for HR onboarding after workflow selection", async () => {
    mockedGetStartableWorkflowDefinitions.mockResolvedValue([
      {
        definitionKey: "onboarding",
        name: "Onboarding",
        description: "Neue Person anlegen.",
        requiresTargetPerson: false,
        primaryLegacyProcessTypeKey: "onboarding",
        latestPublishedVersionNumber: 1,
      },
      {
        definitionKey: "offboarding",
        name: "Offboarding",
        description: "Bestehende Person austreten lassen.",
        requiresTargetPerson: true,
        primaryLegacyProcessTypeKey: "offboarding",
        latestPublishedVersionNumber: 1,
      },
    ]);

    renderWithApp(<CreateWorkflowPage />, { roleKeys: ["auth_hr"] });

    expect(await screen.findByRole("heading", { name: "Workflow wählen" })).toBeTruthy();
    expect(await screen.findByText("Onboarding")).toBeTruthy();

    fireEvent.click(screen.getByText("Onboarding"));
    fireEvent.click(await screen.findByRole("button", { name: "Weiter zur Person" }));

    expect(await screen.findByRole("heading", { name: "Daten der neuen Person" })).toBeTruthy();
    expect(screen.getByText("Stelle und Abteilung")).toBeTruthy();
  });

  it("shows the existing-person selection for managers and never the new-person form", async () => {
    mockedGetStartableWorkflowDefinitions.mockResolvedValue([
      {
        definitionKey: "department_change",
        name: "Abteilungswechsel",
        description: "Bestehende Person in eine neue Abteilung verschieben.",
        requiresTargetPerson: true,
        primaryLegacyProcessTypeKey: "department_change",
        latestPublishedVersionNumber: 1,
      },
    ]);

    renderWithApp(<CreateWorkflowPage />, { roleKeys: ["auth_manager"] });

    expect(await screen.findByText("Änderung starten")).toBeTruthy();
    expect(await screen.findByText("Abteilungswechsel")).toBeTruthy();

    fireEvent.click(await screen.findByRole("button", { name: "Weiter zur Personenauswahl" }));

    expect(screen.getByRole("heading", { name: "Quellworkflow auswählen" })).toBeTruthy();
    expect(screen.queryByText("Daten der neuen Person")).toBeNull();
  });

  it("does not refetch target-person sources endlessly after selecting a person", async () => {
    mockedGetStartableWorkflowDefinitions.mockResolvedValue([
      {
        definitionKey: "department_change",
        name: "Abteilungswechsel",
        description: "Bestehende Person in eine neue Abteilung verschieben.",
        requiresTargetPerson: true,
        primaryLegacyProcessTypeKey: "department_change",
        latestPublishedVersionNumber: 1,
      },
    ]);

    renderWithApp(<CreateWorkflowPage />, { roleKeys: ["auth_manager"] });

    expect(await screen.findByText("Änderung starten")).toBeTruthy();
    expect(await screen.findByText("Abteilungswechsel")).toBeTruthy();
    fireEvent.click(await screen.findByRole("button", { name: "Weiter zur Personenauswahl" }));
    expect(await screen.findByRole("heading", { name: "Quellworkflow auswählen" })).toBeTruthy();

    await waitFor(() => expect(mockedSearchWorkflowTargetPersonSources).toHaveBeenCalledTimes(1));

    fireEvent.click(screen.getByRole("radio"));

    await new Promise((resolve) => window.setTimeout(resolve, 400));
    await waitFor(() => expect(mockedSearchWorkflowTargetPersonSources).toHaveBeenCalledTimes(1));
  });

  it("resets stale context when the workflow changes", async () => {
    mockedGetStartableWorkflowDefinitions.mockResolvedValue([
      {
        definitionKey: "onboarding",
        name: "Onboarding",
        description: "Neue Person anlegen.",
        requiresTargetPerson: false,
        primaryLegacyProcessTypeKey: "onboarding",
        latestPublishedVersionNumber: 1,
      },
      {
        definitionKey: "offboarding",
        name: "Offboarding",
        description: "Bestehende Person auswählen.",
        requiresTargetPerson: true,
        primaryLegacyProcessTypeKey: "offboarding",
        latestPublishedVersionNumber: 1,
      },
    ]);

    renderWithApp(<CreateWorkflowPage />, { roleKeys: ["auth_hr"] });

    expect(await screen.findByRole("heading", { name: "Workflow wählen" })).toBeTruthy();
    expect(await screen.findByText("Onboarding")).toBeTruthy();

    fireEvent.click(screen.getByText("Onboarding"));
    fireEvent.click(await screen.findByRole("button", { name: "Weiter zur Person" }));
    fireEvent.change(screen.getByPlaceholderText("Max"), { target: { value: "Ada" } });

    fireEvent.click(screen.getByRole("button", { name: "Zurück zur Workflow-Auswahl" }));
    expect(await screen.findByText("Offboarding")).toBeTruthy();
    fireEvent.click(screen.getByText("Offboarding"));
    fireEvent.click(await screen.findByRole("button", { name: "Weiter zur Personenauswahl" }));

    expect(await screen.findByRole("heading", { name: "Quellworkflow auswählen" })).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Zurück zur Workflow-Auswahl" }));
    expect(await screen.findByText("Onboarding")).toBeTruthy();
    fireEvent.click(screen.getByText("Onboarding"));
    fireEvent.click(await screen.findByRole("button", { name: "Weiter zur Person" }));

    expect(await screen.findByRole("heading", { name: "Daten der neuen Person" })).toBeTruthy();
    expect((screen.getByPlaceholderText("Max") as HTMLInputElement).value).toBe("");
  });

  it("shows a review step with the selected onboarding context", async () => {
    mockedGetStartableWorkflowDefinitions.mockResolvedValue([
      {
        definitionKey: "onboarding",
        name: "Onboarding",
        description: "Neue Person anlegen.",
        requiresTargetPerson: false,
        primaryLegacyProcessTypeKey: "onboarding",
        latestPublishedVersionNumber: 1,
      },
    ]);

    renderWithApp(<CreateWorkflowPage />, { roleKeys: ["auth_hr"] });

    expect(await screen.findByRole("heading", { name: "Workflow wählen" })).toBeTruthy();
    expect(await screen.findByText("Onboarding")).toBeTruthy();

    fireEvent.click(await screen.findByRole("button", { name: "Weiter zur Person" }));
    fireEvent.change(screen.getByPlaceholderText("Max"), { target: { value: "Ada" } });
    fireEvent.change(screen.getByPlaceholderText("Mustermann"), { target: { value: "Lovelace" } });
    fireEvent.change(screen.getByPlaceholderText("10001"), { target: { value: "1001" } });
    fireEvent.change(screen.getByPlaceholderText("60001"), { target: { value: "2002" } });
    fireEvent.change(screen.getAllByRole("combobox")[0]!, { target: { value: "10" } });
    fireEvent.change(screen.getAllByRole("combobox")[1]!, { target: { value: "7" } });
    fireEvent.click(screen.getByRole("button", { name: "Zur Prüfung" }));

    expect(await screen.findByRole("heading", { name: "Prüfen und anlegen" })).toBeTruthy();
    expect(screen.getAllByText("Ada Lovelace").length).toBeGreaterThan(0);
    expect(screen.getByText("Engineer")).toBeTruthy();
    expect(screen.getByText("IT")).toBeTruthy();
  });
});
