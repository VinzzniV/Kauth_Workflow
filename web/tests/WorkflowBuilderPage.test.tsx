import { screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import WorkflowBuilderPage from "../src/pages/WorkflowBuilderPage";
import * as adminConfigApi from "../src/services/adminConfigApi";
import { renderWithApp } from "./testUtils";

vi.mock("../src/services/adminConfigApi", () => ({
  createAdminWorkflowDefinition: vi.fn(),
  createAdminWorkflowDefinitionVersion: vi.fn(),
  deleteAdminWorkflowDefinition: vi.fn(),
  ensureAdminWorkflowDefinitionWorkingDraft: vi.fn(),
  getAdminWorkflowActionDefinitions: vi.fn(),
  getAdminWorkflowDefinitionVersion: vi.fn(),
  getAdminWorkflowDefinitions: vi.fn(),
  publishAdminWorkflowDefinitionVersion: vi.fn(),
  replaceAdminWorkflowDefinitionVersion: vi.fn(),
  updateAdminWorkflowDefinition: vi.fn(),
}));

const mockedGetAdminWorkflowActionDefinitions = vi.mocked(adminConfigApi.getAdminWorkflowActionDefinitions);
const mockedGetAdminWorkflowDefinitionVersion = vi.mocked(adminConfigApi.getAdminWorkflowDefinitionVersion);
const mockedEnsureAdminWorkflowDefinitionWorkingDraft = vi.mocked(adminConfigApi.ensureAdminWorkflowDefinitionWorkingDraft);
const mockedGetAdminWorkflowDefinitions = vi.mocked(adminConfigApi.getAdminWorkflowDefinitions);

const sampleDefinition = {
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
};

const sampleVersionDetail = {
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
    { nodeKey: "start", nodeType: "start", title: "Start", sortOrder: 1, positionX: null, positionY: null, config: null, actions: [] },
    { nodeKey: "end", nodeType: "end", title: "Ende", sortOrder: 2, positionX: null, positionY: null, config: null, actions: [] },
  ],
  edges: [{ sourceNodeKey: "start", targetNodeKey: "end", priority: 1, conditionExpression: null }],
};

describe("WorkflowBuilderPage (Form-Editor)", () => {
  beforeEach(() => {
    mockedGetAdminWorkflowActionDefinitions.mockReset();
    mockedGetAdminWorkflowDefinitionVersion.mockReset();
    mockedEnsureAdminWorkflowDefinitionWorkingDraft.mockReset();
    mockedGetAdminWorkflowDefinitions.mockReset();

    mockedGetAdminWorkflowDefinitions.mockResolvedValue({
      items: [sampleDefinition],
      total: 1,
      limit: 200,
      offset: 0,
    });
    mockedGetAdminWorkflowActionDefinitions.mockResolvedValue({
      items: [],
      total: 0,
      limit: 200,
      offset: 0,
    });
    mockedGetAdminWorkflowDefinitionVersion.mockResolvedValue(sampleVersionDetail);
    mockedEnsureAdminWorkflowDefinitionWorkingDraft.mockResolvedValue(sampleVersionDetail);
  });

  it("renders the form-editor sections in admin mode", async () => {
    renderWithApp(<WorkflowBuilderPage />, { roleKeys: ["auth_admin"] });

    expect(await screen.findByRole("heading", { name: /Stammdaten/i })).toBeTruthy();
    expect(screen.getByRole("heading", { name: /Schritte/i })).toBeTruthy();
    expect(screen.getByRole("heading", { name: /Übergänge/i })).toBeTruthy();
    expect(screen.getByRole("heading", { name: /Validierung/i })).toBeTruthy();
    // Admin-only actions
    expect(screen.getByRole("button", { name: /\+ Neuer Workflow/ })).toBeTruthy();
  });

  it("hides admin-only actions in limited builder mode", async () => {
    renderWithApp(<WorkflowBuilderPage />, { roleKeys: ["auth_manager"] });

    expect(await screen.findByRole("heading", { name: /Stammdaten/i })).toBeTruthy();
    expect(screen.queryByRole("button", { name: /\+ Neuer Workflow/ })).toBeNull();
    expect(screen.queryByRole("button", { name: /Workflow löschen/ })).toBeNull();
  });
});
