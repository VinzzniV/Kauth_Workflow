import { fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import WorkflowSearchPage from "../src/pages/WorkflowSearchPage";
import * as lifecycleApi from "../src/services/lifecycleApi";
import { createWorkflowSummary, renderWithApp } from "./testUtils";

vi.mock("../src/services/lifecycleApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/lifecycleApi")>("../src/services/lifecycleApi");
  return {
    ...actual,
    getProcessTypes: vi.fn(),
    getWorkflows: vi.fn(),
  };
});

const mockedGetProcessTypes = vi.mocked(lifecycleApi.getProcessTypes);
const mockedGetWorkflows = vi.mocked(lifecycleApi.getWorkflows);

function createDeferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;

  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });

  return { promise, resolve, reject };
}

describe("WorkflowSearchPage", () => {
  beforeEach(() => {
    mockedGetProcessTypes.mockReset();
    mockedGetWorkflows.mockReset();
    mockedGetProcessTypes.mockResolvedValue([
      { key: "onboarding", name: "Onboarding" },
      { key: "offboarding", name: "Offboarding" },
    ]);
  });

  it("keeps other departments selectable after a department filter is applied", async () => {
    mockedGetWorkflows
      .mockResolvedValueOnce([
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
      ])
      .mockResolvedValueOnce([
        createWorkflowSummary({
          uid: "wf-it",
          departmentId: 10,
          departmentName: "IT",
        }),
      ])
      .mockResolvedValueOnce([
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
      ]);

    renderWithApp(<WorkflowSearchPage />, { roleKeys: ["auth_hr"] });

    expect(await screen.findByRole("option", { name: "Finance" })).toBeTruthy();

    fireEvent.change(screen.getByRole("combobox", { name: "Abteilung" }), {
      target: { value: "10" },
    });

    expect(await screen.findByText("Alice Example")).toBeTruthy();
    expect(screen.getByRole("option", { name: "Finance" })).toBeTruthy();
  });

  it("passes the selected process type to the search endpoint", async () => {
    mockedGetWorkflows
      .mockResolvedValueOnce([createWorkflowSummary()])
      .mockResolvedValueOnce([
        createWorkflowSummary({
          uid: "wf-off",
          processType: { key: "offboarding", name: "Offboarding" },
        }),
      ])
      .mockResolvedValueOnce([
        createWorkflowSummary({
          uid: "wf-off",
          processType: { key: "offboarding", name: "Offboarding" },
        }),
      ]);

    renderWithApp(<WorkflowSearchPage />, { roleKeys: ["auth_hr"] });

    expect(await screen.findByText("Alice Example")).toBeTruthy();

    fireEvent.change(screen.getByRole("combobox", { name: "Prozesstyp" }), {
      target: { value: "offboarding" },
    });

    expect(await screen.findByText("Offboarding")).toBeTruthy();
    expect(mockedGetWorkflows).toHaveBeenCalledWith(
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
    const staleResponse = createDeferred<typeof latestWorkflow[]>();

    mockedGetWorkflows
      .mockResolvedValueOnce([initialWorkflow])
      .mockImplementationOnce(() => staleResponse.promise)
      .mockResolvedValueOnce([latestWorkflow]);

    renderWithApp(<WorkflowSearchPage />, { roleKeys: ["auth_hr"] });

    expect(await screen.findByText("Alice Example")).toBeTruthy();

    fireEvent.change(screen.getByRole("textbox", { name: "Suche" }), {
      target: { value: "al" },
    });
    fireEvent.change(screen.getByRole("textbox", { name: "Suche" }), {
      target: { value: "be" },
    });

    expect(await screen.findByText("Berta Latest")).toBeTruthy();

    staleResponse.resolve([
      createWorkflowSummary({
        uid: "wf-stale",
        firstName: "Stale",
        lastName: "Result",
      }),
    ]);

    await waitFor(() => {
      expect(screen.queryByText("Stale Result")).toBeNull();
      expect(screen.getByText("Berta Latest")).toBeTruthy();
    });
  });

  it("shows the no-results state when filters return no workflows", async () => {
    mockedGetWorkflows
      .mockResolvedValueOnce([
        createWorkflowSummary({
          uid: "wf-initial",
          firstName: "Alice",
          lastName: "Example",
        }),
      ])
      .mockResolvedValueOnce([]);

    renderWithApp(<WorkflowSearchPage />, { roleKeys: ["auth_hr"] });

    expect(await screen.findByText("Alice Example")).toBeTruthy();

    fireEvent.change(screen.getByRole("textbox", { name: "Suche" }), {
      target: { value: "zzzzz" },
    });

    expect(await screen.findByText("Keine Treffer")).toBeTruthy();
    expect(screen.queryByText("Keine Onboardings vorhanden")).toBeNull();
  });
});
