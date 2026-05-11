import { fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import CreateWorkflowPage from "../src/pages/CreateWorkflowPage";
import * as lookupApi from "../src/services/lookupApi";
import * as peopleApi from "../src/services/peopleApi";
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

vi.mock("../src/services/peopleApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/peopleApi")>("../src/services/peopleApi");
  return {
    ...actual,
    searchPeople: vi.fn(),
    createPerson: vi.fn(),
  };
});

vi.mock("../src/services/workflowApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/workflowApi")>("../src/services/workflowApi");
  return {
    ...actual,
    getWorkflowConfig: vi.fn(),
    createWorkflow: vi.fn(),
  };
});

const mockedGetStartableWorkflowDefinitions = vi.mocked(lookupApi.getStartableWorkflowDefinitions);
const mockedSearchPeople = vi.mocked(peopleApi.searchPeople);
const mockedCreatePerson = vi.mocked(peopleApi.createPerson);
const mockedGetWorkflowConfig = vi.mocked(workflowApi.getWorkflowConfig);
const mockedCreateWorkflow = vi.mocked(workflowApi.createWorkflow);
const mockedGetRoles = vi.mocked(lookupApi.getRoles);
const mockedGetDepartments = vi.mocked(lookupApi.getDepartments);

describe("CreateWorkflowPage", () => {
  beforeEach(() => {
    vi.useRealTimers();
    mockedGetStartableWorkflowDefinitions.mockReset();
    mockedSearchPeople.mockReset();
    mockedCreatePerson.mockReset();
    mockedGetWorkflowConfig.mockReset();
    mockedCreateWorkflow.mockReset();
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
    mockedSearchPeople.mockResolvedValue([
      {
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
        employmentStatus: "active",
        appUserId: null,
        directoryIdentityId: null,
        directoryLinkStatus: "unlinked",
        directoryDisplayName: null,
        directoryUserPrincipalName: null,
        directoryMail: null,
        directoryEmployeeNumber: null,
        latestSourceWorkflowUid: "wf-onboarding-1",
        latestSourceWorkflowCompletedAt: "2026-03-20T10:00:00.000Z",
      },
    ]);
    mockedCreatePerson.mockResolvedValue({
      personId: 77,
      displayName: "Ada Lovelace",
      firstName: "Ada",
      lastName: "Lovelace",
      employeeNumber: 1001,
      badgeNumber: 2002,
      departmentId: 10,
      departmentName: "IT",
      roleId: 7,
      roleName: "Engineer",
      employmentStatus: "planned",
      appUserId: null,
      directoryIdentityId: null,
      directoryLinkStatus: "unlinked",
      directoryDisplayName: null,
      directoryUserPrincipalName: null,
      directoryMail: null,
      directoryEmployeeNumber: null,
      latestSourceWorkflowUid: null,
      latestSourceWorkflowCompletedAt: null,
    });
    mockedCreateWorkflow.mockResolvedValue({
      uid: "wf-created-1",
      notificationTargets: 0,
      failedNotifications: 0,
      summary: {
        workflowStatus: "draft",
        taskCount: 0,
        readyTaskCount: 0,
        blockedTaskCount: 0,
        doneTaskCount: 0,
        assignmentCount: 0,
        pendingNotifications: 0,
      },
    });
    mockedGetDepartments.mockResolvedValue({
      items: [
        { id: 10, name: "IT" },
        { id: 20, name: "HR" },
      ],
      total: 2,
      limit: 200,
      offset: 0,
    });
    mockedGetRoles.mockResolvedValue({
      items: [
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
      ],
      total: 2,
      limit: 200,
      offset: 0,
    });
  });

  it("starts with an explicit workflow step even when only one definition is available", async () => {
    mockedGetStartableWorkflowDefinitions.mockResolvedValue([
      {
        definitionKey: "department_change",
        name: "Abteilungswechsel",
        description: "Mitarbeiter in eine neue Abteilung versetzen.",
        requiresTargetPerson: true,

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

        latestPublishedVersionNumber: 1,
      },
      {
        definitionKey: "offboarding",
        name: "Offboarding",
        description: "Bestehende Person austreten lassen.",
        requiresTargetPerson: true,

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

        latestPublishedVersionNumber: 1,
      },
    ]);

    renderWithApp(<CreateWorkflowPage />, { roleKeys: ["auth_manager"] });

    expect(await screen.findByText("Änderung starten")).toBeTruthy();
    expect(await screen.findByText("Abteilungswechsel")).toBeTruthy();

    fireEvent.click(await screen.findByRole("button", { name: "Weiter zur Personenauswahl" }));

    expect(screen.getByRole("heading", { name: "Bestehende Person auswählen" })).toBeTruthy();
    expect(screen.queryByText("Daten der neuen Person")).toBeNull();
  });

  it("does not refetch people endlessly after selecting a person", async () => {
    mockedGetStartableWorkflowDefinitions.mockResolvedValue([
      {
        definitionKey: "department_change",
        name: "Abteilungswechsel",
        description: "Bestehende Person in eine neue Abteilung verschieben.",
        requiresTargetPerson: true,

        latestPublishedVersionNumber: 1,
      },
    ]);

    renderWithApp(<CreateWorkflowPage />, { roleKeys: ["auth_manager"] });

    expect(await screen.findByText("Änderung starten")).toBeTruthy();
    expect(await screen.findByText("Abteilungswechsel")).toBeTruthy();
    fireEvent.click(await screen.findByRole("button", { name: "Weiter zur Personenauswahl" }));
    expect(await screen.findByRole("heading", { name: "Bestehende Person auswählen" })).toBeTruthy();

    await waitFor(() => expect(mockedSearchPeople).toHaveBeenCalledTimes(1));

    fireEvent.click(screen.getByRole("radio"));

    await new Promise((resolve) => window.setTimeout(resolve, 400));
    await waitFor(() => expect(mockedSearchPeople).toHaveBeenCalledTimes(1));
  });

  it("resets stale context when the workflow changes", async () => {
    mockedGetStartableWorkflowDefinitions.mockResolvedValue([
      {
        definitionKey: "onboarding",
        name: "Onboarding",
        description: "Neue Person anlegen.",
        requiresTargetPerson: false,

        latestPublishedVersionNumber: 1,
      },
      {
        definitionKey: "offboarding",
        name: "Offboarding",
        description: "Bestehende Person auswählen.",
        requiresTargetPerson: true,

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

    expect(await screen.findByRole("heading", { name: "Bestehende Person auswählen" })).toBeTruthy();

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

  it("creates a canonical person first and then starts onboarding with targetPersonId", async () => {
    mockedGetStartableWorkflowDefinitions.mockResolvedValue([
      {
        definitionKey: "onboarding",
        name: "Onboarding",
        description: "Neue Person anlegen.",
        requiresTargetPerson: false,

        latestPublishedVersionNumber: 1,
      },
    ]);

    renderWithApp(<CreateWorkflowPage />, { roleKeys: ["auth_hr"] });

    fireEvent.click(await screen.findByRole("button", { name: "Weiter zur Person" }));
    fireEvent.change(screen.getByPlaceholderText("Max"), { target: { value: "Ada" } });
    fireEvent.change(screen.getByPlaceholderText("Mustermann"), { target: { value: "Lovelace" } });
    fireEvent.change(screen.getByPlaceholderText("10001"), { target: { value: "1001" } });
    fireEvent.change(screen.getByPlaceholderText("60001"), { target: { value: "2002" } });
    fireEvent.change(screen.getAllByRole("combobox")[0]!, { target: { value: "10" } });
    fireEvent.change(screen.getAllByRole("combobox")[1]!, { target: { value: "7" } });
    fireEvent.click(screen.getByRole("button", { name: "Zur Prüfung" }));
    fireEvent.click(await screen.findByRole("button", { name: "Workflow anlegen" }));

    await waitFor(() => {
      expect(mockedCreatePerson).toHaveBeenCalledWith({
        firstName: "Ada",
        lastName: "Lovelace",
        employeeNumber: 1001,
        badgeNumber: 2002,
        departmentId: 10,
        roleId: 7,
      });
    });

    await waitFor(() => {
      expect(mockedCreateWorkflow).toHaveBeenCalledWith({
        workflowDefinitionKey: "onboarding",
        targetPersonId: 77,
        firstName: "Ada",
        lastName: "Lovelace",
        employeeNumber: 1001,
        badgeNumber: 2002,
        deadlineDate: null,
        departmentId: 10,
        roleId: 7,
      });
    });
  });

  it("starts change workflows directly against the selected person without source workflow", async () => {
    mockedGetStartableWorkflowDefinitions.mockResolvedValue([
      {
        definitionKey: "department_change",
        name: "Abteilungswechsel",
        description: "Bestehende Person in eine neue Abteilung verschieben.",
        requiresTargetPerson: true,

        latestPublishedVersionNumber: 1,
      },
    ]);

    renderWithApp(<CreateWorkflowPage />, { roleKeys: ["auth_manager"] });

    fireEvent.click(await screen.findByRole("button", { name: "Weiter zur Personenauswahl" }));
    fireEvent.click(await screen.findByRole("radio"));
    fireEvent.click(screen.getByRole("button", { name: "Zur Prüfung" }));
    fireEvent.click(await screen.findByRole("button", { name: "Änderung anlegen" }));

    await waitFor(() => {
      expect(mockedCreatePerson).not.toHaveBeenCalled();
      expect(mockedCreateWorkflow).toHaveBeenCalledWith({
        workflowDefinitionKey: "department_change",
        targetPersonId: 22,
        deadlineDate: null,
      });
    });
  });
});
