import { screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import WorkflowListPage from "../src/pages/WorkflowListPage";
import * as onboardingApi from "../src/services/onboardingApi";
import { createWorkflowSummary, renderWithApp } from "./testUtils";

vi.mock("../src/services/onboardingApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/onboardingApi")>("../src/services/onboardingApi");
  return {
    ...actual,
    getWorkflows: vi.fn(),
  };
});

const mockedGetWorkflows = vi.mocked(onboardingApi.getWorkflows);

describe("WorkflowListPage", () => {
  beforeEach(() => {
    mockedGetWorkflows.mockReset();
  });

  it("renders workflow cards from the workflow overview endpoint", async () => {
    mockedGetWorkflows.mockResolvedValue([
      createWorkflowSummary({
        uid: "wf-123",
        firstName: "Mila",
        lastName: "Muster",
        roleName: "Quality Engineer",
      }),
    ]);

    renderWithApp(<WorkflowListPage />, { roleKeys: ["auth_hr"] });

    expect(await screen.findByText("Mila Muster")).toBeTruthy();
    expect(screen.getByText("Quality Engineer")).toBeTruthy();
    expect(screen.getByText("wf-123")).toBeTruthy();
  });
});
