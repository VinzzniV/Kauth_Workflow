import { screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import WorkflowLinksPanel from "../src/components/workflow-detail/WorkflowLinksPanel";
import * as workflowApi from "../src/services/workflowApi";
import type { RelatedWorkflowSummary } from "../src/types/workflow";
import { renderWithApp } from "./testUtils";

vi.mock("../src/services/workflowApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/workflowApi")>(
    "../src/services/workflowApi"
  );

  return {
    ...actual,
    getRelatedWorkflows: vi.fn(),
  };
});

const mockedGetRelatedWorkflows = vi.mocked(workflowApi.getRelatedWorkflows);

function createRelatedWorkflow(
  overrides: Partial<RelatedWorkflowSummary> = {}
): RelatedWorkflowSummary {
  return {
    uid: "wf-related-1",
    processType: {
      key: "offboarding",
      name: "Offboarding",
      requiresTargetPerson: true,
    },
    workflowStatus: "in_progress",
    createdAt: "2026-04-01T08:00:00.000Z",
    departmentId: 10,
    ...overrides,
  };
}

describe("WorkflowLinksPanel", () => {
  beforeEach(() => {
    mockedGetRelatedWorkflows.mockReset();
  });

  it("renders related workflows from the query layer", async () => {
    mockedGetRelatedWorkflows.mockResolvedValue([createRelatedWorkflow()]);

    renderWithApp(<WorkflowLinksPanel uid="wf-1" />);

    expect(await screen.findByRole("heading", { name: "Verknüpfte Vorgänge" })).toBeTruthy();
    expect(screen.getByText("Offboarding")).toBeTruthy();
    expect(screen.getByRole("link", { name: "Öffnen" }).getAttribute("href")).toBe("/workflows/wf-related-1");
  });

  it("stays hidden when no related workflows exist", async () => {
    mockedGetRelatedWorkflows.mockResolvedValue([]);

    renderWithApp(<WorkflowLinksPanel uid="wf-1" />);

    await waitFor(() => {
      expect(mockedGetRelatedWorkflows).toHaveBeenCalledWith("wf-1");
    });

    expect(screen.queryByRole("heading", { name: "Verknüpfte Vorgänge" })).toBeNull();
  });

  it("stays hidden when the related workflow query fails", async () => {
    const consoleErrorSpy = vi.spyOn(console, "error").mockImplementation(() => undefined);
    mockedGetRelatedWorkflows.mockRejectedValue(new Error("Backend nicht erreichbar"));

    renderWithApp(<WorkflowLinksPanel uid="wf-1" />);

    await waitFor(() => {
      expect(mockedGetRelatedWorkflows).toHaveBeenCalledWith("wf-1");
    });

    expect(screen.queryByRole("heading", { name: "Verknüpfte Vorgänge" })).toBeNull();
    consoleErrorSpy.mockRestore();
  });
});
