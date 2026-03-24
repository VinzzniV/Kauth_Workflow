import { fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import WorkflowListPage from "../src/pages/WorkflowListPage";
import * as onboardingApi from "../src/services/onboardingApi";
import { createWorkflowSummary, renderWithApp } from "./testUtils";

vi.mock("../src/services/onboardingApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/onboardingApi")>("../src/services/onboardingApi");
  return {
    ...actual,
    getWorkflowPage: vi.fn(),
  };
});

const mockedGetWorkflowPage = vi.mocked(onboardingApi.getWorkflowPage);

function createDeferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;

  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });

  return { promise, resolve, reject };
}

function createWorkflowPageResponse(overrides: Partial<Awaited<ReturnType<typeof onboardingApi.getWorkflowPage>>> = {}) {
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
    mockedGetWorkflowPage.mockReset();
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
    expect(screen.getByText("Quality Engineer")).toBeTruthy();
    expect(screen.getByText("wf-123")).toBeTruthy();
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

    fireEvent.change(screen.getByRole("textbox", { name: "Suche" }), {
      target: { value: "zzzzz" },
    });

    expect(await screen.findByText("Keine Treffer")).toBeTruthy();
    expect(screen.queryByText("Keine Onboarding-Fälle vorhanden")).toBeNull();
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
    const staleFilteredWorkflow = createWorkflowSummary({
      uid: "wf-126",
      firstName: "Stale",
      lastName: "Result",
      workflowStatus: "completed",
    });
    const staleFilteredPage = createDeferred<Awaited<ReturnType<typeof onboardingApi.getWorkflowPage>>>();

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
      .mockImplementationOnce(() => staleFilteredPage.promise)
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

    staleFilteredPage.resolve({
      items: [staleFilteredWorkflow],
      count: 99,
      offset: 20,
      limit: 20,
      departmentOptions: [{ id: 1, name: "IT" }],
      responsibilityOptions: [{ value: "it", label: "IT" }],
    });

    await waitFor(() => {
      expect(screen.queryByText("Stale Result")).toBeNull();
      expect(screen.getByText("Clara Completed")).toBeTruthy();
      expect(screen.getByText("Seite 1 von 1 · 1 Workflow")).toBeTruthy();
    });
  });
});
