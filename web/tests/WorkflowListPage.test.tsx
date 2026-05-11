import { fireEvent, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import WorkflowListPage from "../src/pages/WorkflowListPage";
import * as lookupApi from "../src/services/lookupApi";
import * as workflowApi from "../src/services/workflowApi";
import { createWorkflowSummary, renderWithApp } from "./testUtils";

vi.mock("../src/services/lookupApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/lookupApi")>("../src/services/lookupApi");
  return {
    ...actual,
    getStartableWorkflowDefinitions: vi.fn(),
  };
});

vi.mock("../src/services/workflowApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/workflowApi")>("../src/services/workflowApi");
  return {
    ...actual,
    getWorkflowPage: vi.fn(),
  };
});

const mockedGetStartableWorkflowDefinitions = vi.mocked(lookupApi.getStartableWorkflowDefinitions);
const mockedGetWorkflowPage = vi.mocked(workflowApi.getWorkflowPage);

function createWorkflowPageResponse(overrides: Partial<Awaited<ReturnType<typeof workflowApi.getWorkflowPage>>> = {}) {
  return {
    items: [createWorkflowSummary()],
    count: 1,
    offset: 0,
    limit: 20,
    departmentOptions: [{ id: 1, name: "IT" }],
    responsibilityOptions: [{ value: "it", label: "IT" }],
    ...overrides,
  };
}

describe("WorkflowListPage", () => {
  beforeEach(() => {
    mockedGetStartableWorkflowDefinitions.mockReset();
    mockedGetWorkflowPage.mockReset();
    mockedGetStartableWorkflowDefinitions.mockResolvedValue([
      { definitionKey: "onboarding", name: "Onboarding", requiresTargetPerson: false, latestPublishedVersionNumber: 1 },
      { definitionKey: "offboarding", name: "Offboarding", requiresTargetPerson: true, latestPublishedVersionNumber: 1 },
    ]);
  });

  it("renders workflow cards from the workflow overview endpoint", async () => {
    mockedGetWorkflowPage.mockResolvedValue(createWorkflowPageResponse({
      items: [
        createWorkflowSummary({
          uid: "wf-123",
          firstName: "Mila",
          lastName: "Muster",
          roleName: "Quality Engineer",
        }),
      ],
      count: 1,
      offset: 0,
      limit: 20,
    }));

    renderWithApp(<WorkflowListPage />, { roleKeys: ["auth_hr"] });

    expect(await screen.findByText("Mila Muster")).toBeTruthy();
    expect(screen.getAllByText("Quality Engineer").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Onboarding").length).toBeGreaterThan(0);
    expect(screen.getByRole("link", { name: "Öffnen" }).getAttribute("href")).toBe("/workflows/wf-123");
  });

  it("offers a table mode for workflow scanning", async () => {
    mockedGetWorkflowPage.mockResolvedValue(createWorkflowPageResponse({
      items: [
        createWorkflowSummary({
          uid: "wf-123",
          firstName: "Mila",
          lastName: "Muster",
        }),
      ],
    }));

    renderWithApp(<WorkflowListPage />, { roleKeys: ["auth_hr"] });

    expect(await screen.findByText("Mila Muster")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Tabelle" }));

    expect(screen.getByRole("table", { name: "Tabellenansicht Mitarbeiterprozesse" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Vorgang" })).toBeTruthy();
  });

  it("keeps the workflow list visible while showing a selected preview", async () => {
    mockedGetWorkflowPage.mockResolvedValue(createWorkflowPageResponse({
      items: [
        createWorkflowSummary({
          uid: "wf-mila",
          firstName: "Mila",
          lastName: "Muster",
          createdAt: "2026-03-22T10:00:00.000Z",
        }),
        createWorkflowSummary({
          uid: "wf-ben",
          firstName: "Ben",
          lastName: "Beispiel",
          createdAt: "2026-03-21T10:00:00.000Z",
        }),
      ],
      count: 2,
    }));

    renderWithApp(<WorkflowListPage />, { roleKeys: ["auth_hr"] });

    const list = await screen.findByLabelText("Vorgangsliste");
    expect(within(list).getByText("Mila Muster")).toBeTruthy();
    // No auto-selection: empty preview hint shown until user clicks
    expect(screen.getByLabelText("Vorgangs-Vorschau")).toBeTruthy();
    expect(screen.getByText("Vorgang auswählen")).toBeTruthy();

    fireEvent.click(within(list).getAllByRole("button", { name: "Vorschau" })[1]);

    expect(screen.getByLabelText("Vorgangsliste")).toBeTruthy();
    expect(screen.getByLabelText("Vorschau für Ben Beispiel")).toBeTruthy();
    expect(screen.getByRole("link", { name: "Detail öffnen" }).getAttribute("href")).toBe("/workflows/wf-ben");
  });

  it("passes the selected process type filter to the workflow overview endpoint", async () => {
    mockedGetWorkflowPage
      .mockResolvedValueOnce(createWorkflowPageResponse())
      .mockResolvedValueOnce(createWorkflowPageResponse({
        items: [createWorkflowSummary({
          uid: "wf-off",
          workflowDefinition: { key: "offboarding", name: "Offboarding", requiresTargetPerson: true },
        })],
      }));

    renderWithApp(<WorkflowListPage />, { roleKeys: ["auth_hr"] });

    expect(await screen.findByText("Alice Example")).toBeTruthy();

    fireEvent.change(screen.getByRole("combobox", { name: "Prozesstyp" }), {
      target: { value: "offboarding" },
    });

    await waitFor(() => {
      expect(mockedGetWorkflowPage).toHaveBeenLastCalledWith(
        20,
        0,
        expect.objectContaining({ workflowDefinitionKey: "offboarding" })
      );
    });
    expect(mockedGetWorkflowPage).toHaveBeenLastCalledWith(
      20,
      0,
      expect.objectContaining({ workflowDefinitionKey: "offboarding" })
    );
  });

  it("initializes filter and page state from the URL query parameters", async () => {
    mockedGetWorkflowPage.mockResolvedValue(
      createWorkflowPageResponse({
        items: [
          createWorkflowSummary({
            uid: "wf-completed",
            firstName: "Clara",
            lastName: "Completed",
            workflowStatus: "completed",
            workflowDefinition: { key: "offboarding", name: "Offboarding", requiresTargetPerson: true },
          }),
        ],
        count: 21,
        offset: 20,
        limit: 20,
      })
    );

    renderWithApp(<WorkflowListPage />, {
      roleKeys: ["auth_hr"],
      route: "/workflows?status=completed&type=offboarding&page=2",
    });

    expect(await screen.findByText("Clara Completed")).toBeTruthy();
    expect(mockedGetWorkflowPage).toHaveBeenLastCalledWith(
      20,
      20,
      expect.objectContaining({ status: "completed", workflowDefinitionKey: "offboarding" })
    );
  });

  it("shows departments that are not present on the current page", async () => {
    mockedGetWorkflowPage.mockResolvedValue(createWorkflowPageResponse({
      items: [
        createWorkflowSummary({
          uid: "wf-123",
          departmentId: 10,
          departmentName: "IT",
        }),
      ],
      count: 21,
      offset: 0,
      limit: 20,
      departmentOptions: [
        { id: 20, name: "Finance" },
        { id: 10, name: "IT" },
      ],
    }));

    renderWithApp(<WorkflowListPage />, { roleKeys: ["auth_hr"] });

    expect(await screen.findByRole("option", { name: "Finance" })).toBeTruthy();
  });

  it("shows responsibilities that are not present on the current page", async () => {
    mockedGetWorkflowPage.mockResolvedValue(createWorkflowPageResponse({
      items: [
        createWorkflowSummary({
          uid: "wf-123",
          responsibilityOptions: [{ value: "it", label: "IT" }],
        }),
      ],
      count: 21,
      offset: 0,
      limit: 20,
      responsibilityOptions: [
        { value: "finance", label: "Finance" },
        { value: "it", label: "IT" },
      ],
    }));

    renderWithApp(<WorkflowListPage />, { roleKeys: ["auth_hr"] });

    expect(await screen.findByRole("option", { name: "Finance" })).toBeTruthy();
  });

  it("applies the responsibility filter before paginating", async () => {
    const pageOneWorkflow = createWorkflowSummary({
      uid: "wf-123",
      firstName: "Mila",
      lastName: "Muster",
      responsibilityOptions: [{ value: "it", label: "IT" }],
    });
    const laterMatchingWorkflow = createWorkflowSummary({
      uid: "wf-124",
      firstName: "Lars",
      lastName: "Later",
      responsibilityOptions: [{ value: "it", label: "IT" }],
    });

    mockedGetWorkflowPage
      .mockResolvedValueOnce(createWorkflowPageResponse({
        items: [pageOneWorkflow],
        count: 21,
        offset: 0,
        limit: 20,
        responsibilityOptions: [{ value: "it", label: "IT" }],
      }))
      .mockResolvedValueOnce(createWorkflowPageResponse({
        items: [laterMatchingWorkflow],
        count: 1,
        offset: 0,
        limit: 20,
        responsibilityOptions: [{ value: "it", label: "IT" }],
      }));

    renderWithApp(<WorkflowListPage />, { roleKeys: ["auth_hr"] });

    expect(await screen.findByText("Mila Muster")).toBeTruthy();

    fireEvent.change(screen.getByRole("combobox", { name: "Zuständiger Bereich" }), {
      target: { value: "it" },
    });

    expect(await screen.findByText("Lars Later")).toBeTruthy();
  });

  it("shows the filtered empty state when backend filtering returns no rows", async () => {
    mockedGetWorkflowPage
      .mockResolvedValueOnce(createWorkflowPageResponse({
        items: [createWorkflowSummary()],
        count: 1,
        offset: 0,
        limit: 20,
      }))
      .mockResolvedValueOnce(createWorkflowPageResponse({
        items: [],
        count: 0,
        offset: 0,
        limit: 20,
      }));

    renderWithApp(<WorkflowListPage />, { roleKeys: ["auth_hr"] });

    expect(await screen.findByText("Alice Example")).toBeTruthy();

    fireEvent.change(screen.getByRole("textbox", { name: "Suche im Überblick" }), {
      target: { value: "zzzzz" },
    });

    expect(await screen.findByText("Keine Treffer")).toBeTruthy();
    expect(screen.queryByText("Keine laufenden Vorgänge vorhanden")).toBeNull();
  });

  it("ignores stale page responses after filters reset the page index", async () => {
    const initialWorkflow = createWorkflowSummary({
      uid: "wf-123",
      firstName: "Mila",
      lastName: "Muster",
      workflowStatus: "waiting_for_department",
    });
    const secondPageWorkflow = createWorkflowSummary({
      uid: "wf-124",
      firstName: "Paula",
      lastName: "PageTwo",
      workflowStatus: "waiting_for_department",
    });
    const latestFilteredWorkflow = createWorkflowSummary({
      uid: "wf-125",
      firstName: "Clara",
      lastName: "Completed",
      workflowStatus: "completed",
    });

    mockedGetWorkflowPage
      .mockResolvedValueOnce(createWorkflowPageResponse({
        items: [initialWorkflow],
        count: 21,
        offset: 0,
        limit: 20,
      }))
      .mockResolvedValueOnce(createWorkflowPageResponse({
        items: [secondPageWorkflow],
        count: 21,
        offset: 20,
        limit: 20,
      }))
      .mockResolvedValueOnce(createWorkflowPageResponse({
        items: [latestFilteredWorkflow],
        count: 1,
        offset: 0,
        limit: 20,
      }));

    renderWithApp(<WorkflowListPage />, { roleKeys: ["auth_hr"] });

    expect(await screen.findByText("Mila Muster")).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Nächste Seite" }));
    expect(await screen.findByText("Paula PageTwo")).toBeTruthy();

    fireEvent.change(screen.getByRole("combobox", { name: "Status" }), {
      target: { value: "completed" },
    });

    expect(await screen.findByText("Clara Completed")).toBeTruthy();

    await waitFor(() => {
      expect(screen.getByText("Clara Completed")).toBeTruthy();
      expect(mockedGetWorkflowPage).toHaveBeenLastCalledWith(
        20,
        0,
        expect.objectContaining({ status: "completed" })
      );
    });
  });
});
