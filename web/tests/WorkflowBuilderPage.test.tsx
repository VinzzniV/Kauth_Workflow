import { screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import WorkflowBuilderPage from "../src/pages/WorkflowBuilderPage";
import * as adminConfigApi from "../src/services/adminConfigApi";
import { renderWithApp } from "./testUtils";

vi.mock("@xyflow/react", () => ({
  Background: () => null,
  Controls: () => null,
  MiniMap: () => null,
  Handle: () => null,
  Position: { Left: "left", Right: "right" },
  MarkerType: { ArrowClosed: "arrowclosed" },
  ReactFlow: ({ children }: { children?: any }) => <div data-testid="mock-react-flow-page">{children}</div>,
}));

vi.mock("../src/services/adminConfigApi", () => ({
  createAdminWorkflowDefinition: vi.fn(),
  createAdminWorkflowDefinitionVersion: vi.fn(),
  getAdminWorkflowActionDefinitions: vi.fn(),
  getAdminWorkflowDefinitionVersion: vi.fn(),
  getAdminWorkflowDefinitions: vi.fn(),
  publishAdminWorkflowDefinitionVersion: vi.fn(),
  replaceAdminWorkflowDefinitionVersion: vi.fn(),
  updateAdminWorkflowDefinition: vi.fn(),
}));

const mockedGetAdminWorkflowActionDefinitions = vi.mocked(adminConfigApi.getAdminWorkflowActionDefinitions);
const mockedGetAdminWorkflowDefinitionVersion = vi.mocked(adminConfigApi.getAdminWorkflowDefinitionVersion);
const mockedGetAdminWorkflowDefinitions = vi.mocked(adminConfigApi.getAdminWorkflowDefinitions);

describe("WorkflowBuilderPage", () => {
  beforeEach(() => {
    mockedGetAdminWorkflowActionDefinitions.mockReset();
    mockedGetAdminWorkflowDefinitionVersion.mockReset();
    mockedGetAdminWorkflowDefinitions.mockReset();

    mockedGetAdminWorkflowDefinitions.mockResolvedValue([
      {
        id: 1,
        key: "onboarding",
        name: "Onboarding",
        description: "Definition",
        versions: [
          {
            id: 10,
            workflowDefinitionId: 1,
            versionNumber: 1,
            status: "draft",
            name: "Draft 1",
            description: "Initial draft",
            primaryLegacyProcessTypeKey: "onboarding",
            createdAt: "2026-04-08T10:00:00Z",
            updatedAt: "2026-04-08T10:00:00Z",
            publishedAt: null,
            canPublish: true,
            validationIssues: [],
          },
        ],
      },
    ]);
    mockedGetAdminWorkflowActionDefinitions.mockResolvedValue([]);
    mockedGetAdminWorkflowDefinitionVersion.mockResolvedValue({
      id: 10,
      workflowDefinitionId: 1,
      definitionKey: "onboarding",
      definitionName: "Onboarding",
      definitionDescription: "Definition",
      versionNumber: 1,
      status: "draft",
      name: "Draft 1",
      description: "Initial draft",
      primaryLegacyProcessTypeKey: "onboarding",
      createdAt: "2026-04-08T10:00:00Z",
      updatedAt: "2026-04-08T10:00:00Z",
      publishedAt: null,
      canPublish: true,
      validationIssues: [],
      nodes: [
        { nodeKey: "start", nodeType: "start", title: "Start", sortOrder: 1, positionX: 80, positionY: 60, config: null, actions: [] },
        { nodeKey: "end", nodeType: "end", title: "Ende", sortOrder: 2, positionX: 420, positionY: 60, config: null, actions: [] },
      ],
      edges: [{ sourceNodeKey: "start", targetNodeKey: "end", priority: 1, conditionExpression: null }],
    });
  });

  it("renders the standalone builder page in admin mode", async () => {
    renderWithApp(<WorkflowBuilderPage />, { roleKeys: ["auth_admin"] });

    expect((await screen.findAllByRole("heading", { name: "Workflow Builder" })).length).toBeGreaterThan(0);
    expect(screen.getAllByText("Admin Builder").length).toBeGreaterThan(0);
    expect(await screen.findByRole("button", { name: "Definition anlegen" })).toBeTruthy();
  });

  it("renders the standalone builder page in limited builder mode", async () => {
    renderWithApp(<WorkflowBuilderPage />, { roleKeys: ["auth_manager"] });

    expect((await screen.findAllByRole("heading", { name: "Workflow Builder" })).length).toBeGreaterThan(0);
    expect(screen.getAllByText("Builder").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Automation gesperrt").length).toBeGreaterThan(0);
    await waitFor(() => {
      expect(mockedGetAdminWorkflowActionDefinitions).not.toHaveBeenCalled();
    });
    expect(screen.queryByRole("button", { name: "Definition anlegen" })).toBeNull();
  });
});
