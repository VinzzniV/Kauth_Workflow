import { fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import WorkflowSearchPage from "../src/pages/WorkflowSearchPage";
import * as lookupApi from "../src/services/lookupApi";
import * as workflowApi from "../src/services/workflowApi";
import { createWorkflowSummary, renderWithApp } from "./testUtils";

vi.mock("../src/services/lookupApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/lookupApi")>("../src/services/lookupApi");
  return {
    ...actual,
    getProcessTypes: vi.fn(),
    getDepartments: vi.fn(),
  };
});

vi.mock("../src/services/workflowApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/workflowApi")>("../src/services/workflowApi");
  return {
    ...actual,
    getWorkflowPage: vi.fn(),
  };
});

const mockedGetProcessTypes = vi.mocked(lookupApi.getProcessTypes);
const mockedGetDepartments = vi.mocked(lookupApi.getDepartments);
const mockedGetWorkflowPage = vi.mocked(workflowApi.getWorkflowPage);

function createDeferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;

  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });

  return { promise, resolve, reject };
}

function createWorkflowPageResponse(
  overrides: Partial<Awaited<ReturnType<typeof workflowApi.getWorkflowPage>>> = {}
) {
  return {
    items: [createWorkflowSummary()],
    count: 1,
    offset: 0,
    limit: 1000,
    departmentOptions: [{ id: 10, name: "IT" }],
    responsibilityOptions: [{ value: "it", label: "IT" }],
    ...overrides,
  };
}

describe("WorkflowSearchPage", () => {
  beforeEach(() => {
    mockedGetProcessTypes.mockReset();
    mockedGetDepartments.mockReset();
    mockedGetWorkflowPage.mockReset();
    mockedGetProcessTypes.mockResolvedValue([
      { key: "onboarding", name: "Onboarding" },
      { key: "offboarding", name: "Offboarding" },
    ]);
    mockedGetDepartments.mockResolvedValue([
      { id: 10, name: "IT" },
      { id: 20, name: "Finance" },
    ]);
  });

  it("keeps other departments selectable after a department filter is applied", async () => {
    mockedGetWorkflowPage
      .mockResolvedValueOnce(createWorkflowPageResponse({
        items: [
          createWorkflowSummary({
            uid: "wf-it",
            departmentId: 10,
            departmentName: "IT",
          }),
          createWorkflowSummary({
            uid: "wf-finance",
            departmentId: 20,
            departmentName: "Finance",
          }),
        ],
        departmentOptions: [
          { id: 10, name: "IT" },
          { id: 20, name: "Finance" },
        ],
      }))
      .mockResolvedValueOnce(createWorkflowPageResponse({
        items: [
          createWorkflowSummary({
            uid: "wf-it",
            departmentId: 10,
            departmentName: "IT",
          }),
        ],
        departmentOptions: [
          { id: 10, name: "IT" },
          { id: 20, name: "Finance" },
        ],
      }));

    renderWithApp(<WorkflowSearchPage />, { roleKeys: ["auth_hr"] });

    expect(await screen.findByRole("option", { name: "Finance" })).toBeTruthy();

    fireEvent.change(screen.getByRole("textbox", { name: "Suche" }), {
      target: { value: "alice" },
    });

    expect(await screen.findByText("Alice Example")).toBeTruthy();

    fireEvent.change(screen.getByRole("combobox", { name: "Abteilung" }), {
      target: { value: "10" },
    });

    expect(await screen.findByText("Alice Example")).toBeTruthy();
    expect(screen.getByRole("option", { name: "Finance" })).toBeTruthy();
  });

  it("passes the selected process type to the search endpoint", async () => {
    mockedGetWorkflowPage
      .mockResolvedValueOnce(createWorkflowPageResponse())
      .mockResolvedValueOnce(createWorkflowPageResponse({
        items: [
          createWorkflowSummary({
            uid: "wf-off",
            processType: { key: "offboarding", name: "Offboarding" },
          }),
        ],
      }));

    renderWithApp(<WorkflowSearchPage />, { roleKeys: ["auth_hr"] });

    fireEvent.change(screen.getByRole("textbox", { name: "Suche" }), {
      target: { value: "alice" },
    });

    expect(await screen.findByText("Alice Example")).toBeTruthy();

    fireEvent.change(screen.getByRole("combobox", { name: "Prozesstyp" }), {
      target: { value: "offboarding" },
    });

    expect(await screen.findByText("Offboarding")).toBeTruthy();
    expect(mockedGetWorkflowPage).toHaveBeenLastCalledWith(
      1000,
      0,
      expect.objectContaining({ processTypeKey: "offboarding" })
    );
  });

  it("ignores stale responses when search requests resolve out of order", async () => {
    const initialWorkflow = createWorkflowSummary({
      uid: "wf-initial",
      firstName: "Alice",
      lastName: "Example",
    });
    const latestWorkflow = createWorkflowSummary({
      uid: "wf-latest",
      firstName: "Berta",
      lastName: "Latest",
    });
    const staleResponse = createDeferred<Awaited<ReturnType<typeof workflowApi.getWorkflowPage>>>();

    mockedGetWorkflowPage
      .mockResolvedValueOnce(createWorkflowPageResponse({ items: [initialWorkflow] }))
      .mockImplementationOnce(() => staleResponse.promise)
      .mockResolvedValueOnce(createWorkflowPageResponse({ items: [latestWorkflow] }));

    renderWithApp(<WorkflowSearchPage />, { roleKeys: ["auth_hr"] });

    fireEvent.change(screen.getByRole("textbox", { name: "Suche" }), {
      target: { value: "al" },
    });
    await new Promise((resolve) => window.setTimeout(resolve, 450));

    expect(await screen.findByText("Alice Example")).toBeTruthy();

    fireEvent.change(screen.getByRole("textbox", { name: "Suche" }), {
      target: { value: "be" },
    });
    await new Promise((resolve) => window.setTimeout(resolve, 450));

    expect(await screen.findByText("Berta Latest")).toBeTruthy();

    staleResponse.resolve(createWorkflowPageResponse({
      items: [
        createWorkflowSummary({
          uid: "wf-stale",
          firstName: "Stale",
          lastName: "Result",
        }),
      ],
    }));

    await waitFor(() => {
      expect(screen.queryByText("Stale Result")).toBeNull();
      expect(screen.getByText("Berta Latest")).toBeTruthy();
    });
  });

  it("shows the no-results state when filters return no workflows", async () => {
    mockedGetWorkflowPage
      .mockResolvedValueOnce(createWorkflowPageResponse({
        items: [
          createWorkflowSummary({
            uid: "wf-initial",
            firstName: "Alice",
            lastName: "Example",
          }),
        ],
      }))
      .mockResolvedValueOnce(createWorkflowPageResponse({ items: [], count: 0 }));

    renderWithApp(<WorkflowSearchPage />, { roleKeys: ["auth_hr"] });

    fireEvent.change(screen.getByRole("textbox", { name: "Suche" }), {
      target: { value: "alice" },
    });

    expect(await screen.findByText("Alice Example")).toBeTruthy();

    fireEvent.change(screen.getByRole("textbox", { name: "Suche" }), {
      target: { value: "zzzzz" },
    });

    expect(await screen.findByText("Keine Treffer")).toBeTruthy();
    expect(screen.queryByText("Keine Onboardings vorhanden")).toBeNull();
  });
});
