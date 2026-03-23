import { fireEvent, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import SupervisorStepPage from "../src/pages/SupervisorStepPage";
import * as onboardingApi from "../src/services/onboardingApi";
import { createRequirementSnapshot, createWorkflowSummary, renderWithApp } from "./testUtils";

vi.mock("../src/services/onboardingApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/onboardingApi")>("../src/services/onboardingApi");
  return {
    ...actual,
    getSupervisorStepWorkflows: vi.fn(),
    getWorkflowSupervisorStep: vi.fn(),
    updateWorkflowSupervisorStep: vi.fn(),
  };
});

const mockedGetSupervisorStepWorkflows = vi.mocked(onboardingApi.getSupervisorStepWorkflows);
const mockedGetWorkflowSupervisorStep = vi.mocked(onboardingApi.getWorkflowSupervisorStep);

describe("SupervisorStepPage", () => {
  beforeEach(() => {
    mockedGetSupervisorStepWorkflows.mockReset();
    mockedGetWorkflowSupervisorStep.mockReset();
  });

  it("loads the selected workflow requirements for the supervisor step", async () => {
    mockedGetSupervisorStepWorkflows.mockResolvedValue([
      createWorkflowSummary({
        uid: "wf-supervisor",
        firstName: "Lea",
        lastName: "Leitung",
        workflowStatus: "waiting_for_supervisor",
      }),
    ]);
    mockedGetWorkflowSupervisorStep.mockResolvedValue([
      createRequirementSnapshot({
        title: "AD-Benutzer anlegen",
      }),
    ]);

    renderWithApp(<SupervisorStepPage />, { roleKeys: ["auth_manager"] });

    expect(await screen.findByText("Lea Leitung")).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Angaben öffnen" }));

    expect(await screen.findByText("Bedarf festlegen: Lea Leitung")).toBeTruthy();
    expect(screen.getByText("AD-Benutzer anlegen")).toBeTruthy();
  });
});
