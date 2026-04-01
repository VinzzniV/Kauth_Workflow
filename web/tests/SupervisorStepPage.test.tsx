import { fireEvent, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import SupervisorStepPage from "../src/pages/SupervisorStepPage";
import * as workflowApi from "../src/services/workflowApi";
import { createRequirementSnapshot, createWorkflowSummary, renderWithApp } from "./testUtils";

vi.mock("../src/services/workflowApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/workflowApi")>("../src/services/workflowApi");
  return {
    ...actual,
    getSupervisorStepWorkflows: vi.fn(),
    getWorkflowSupervisorStep: vi.fn(),
    updateWorkflowSupervisorStep: vi.fn(),
  };
});

const mockedGetSupervisorStepWorkflows = vi.mocked(workflowApi.getSupervisorStepWorkflows);
const mockedGetWorkflowSupervisorStep = vi.mocked(workflowApi.getWorkflowSupervisorStep);

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
        processType: { key: "onboarding", name: "Onboarding" },
        workflowStatus: "waiting_for_supervisor",
      }),
      createWorkflowSummary({
        uid: "wf-department",
        firstName: "Nina",
        lastName: "Nebenlauf",
        processType: { key: "offboarding", name: "Offboarding" },
        workflowStatus: "waiting_for_department",
      }),
    ]);
    mockedGetWorkflowSupervisorStep.mockResolvedValue([
      createRequirementSnapshot({
        title: "AD-Benutzer anlegen",
      }),
    ]);

    renderWithApp(<SupervisorStepPage />, { roleKeys: ["auth_manager"] });

    expect(await screen.findByText("Lea Leitung")).toBeTruthy();
    expect(screen.getAllByText("Onboarding").length).toBeGreaterThan(0);
    expect(screen.queryByText("Nina Nebenlauf")).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Angaben öffnen" }));

    expect(await screen.findByText("Bedarf festlegen: Lea Leitung")).toBeTruthy();
    expect(screen.getByText("AD-Benutzer anlegen")).toBeTruthy();
  });
});
