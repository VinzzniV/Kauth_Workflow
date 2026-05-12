import { fireEvent, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import WorkflowManagementPanel from "../src/components/workflow-detail/WorkflowManagementPanel";
import * as workflowApi from "../src/services/workflowApi";
import { deriveRoleCapabilities } from "../src/auth/roleModel";
import type { WorkflowDetail, WorkflowRuntimeStatus } from "../src/types/workflow";
import { renderWithApp } from "./testUtils";

vi.mock("../src/services/workflowApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/workflowApi")>(
    "../src/services/workflowApi",
  );
  return {
    ...actual,
    cancelWorkflow: vi.fn(),
    archiveWorkflow: vi.fn(),
    deleteWorkflow: vi.fn(),
  };
});

const mockedCancelWorkflow = vi.mocked(workflowApi.cancelWorkflow);

function createDetail(overrides: Partial<WorkflowDetail> = {}): WorkflowDetail {
  return {
    uid: "wf-cancel-1",
    workflowDefinition: { key: "onboarding", name: "Onboarding", requiresTargetPerson: false },
    firstName: "Alice",
    lastName: "Example",
    employeeNumber: 1,
    badgeNumber: 1,
    departmentId: 10,
    departmentName: "IT",
    roleId: 5,
    roleName: "Engineer",
    workflowStatus: "in_progress" as WorkflowRuntimeStatus,
    createdAt: "2026-05-10T08:00:00.000Z",
    deadlineDate: null,
    archivedAt: null,
    cancelledAt: null,
    cancelledByPersonId: null,
    cancellationReasonCode: null,
    cancellationReasonDetail: null,
    targetPersonId: null,
    requirements: [],
    requirementSummary: {
      totalCount: 0,
      visibleCount: 0,
      answeredVisibleCount: 0,
      pendingVisibleCount: 0,
    },
    tasks: [],
    taskMetrics: {
      overall: { totalCount: 0, openCount: 0, inProgressCount: 0, blockedCount: 0, doneCount: 0, completedCount: 0, activeCount: 0 },
      required: { totalCount: 0, openCount: 0, inProgressCount: 0, blockedCount: 0, doneCount: 0, completedCount: 0, activeCount: 0 },
      departmentPhase: { totalCount: 0, openCount: 0, inProgressCount: 0, blockedCount: 0, doneCount: 0, completedCount: 0, activeCount: 0 },
    },
    taskAreas: [],
    notifications: [],
    ...overrides,
  };
}

function renderPanel(overrides: Partial<WorkflowDetail> = {}, roleKeys: string[] = ["auth_hr"]) {
  const workflow = createDetail(overrides);
  const capabilities = deriveRoleCapabilities(roleKeys, []);
  renderWithApp(
    <WorkflowManagementPanel uid={workflow.uid} workflow={workflow} capabilities={capabilities} />,
    { roleKeys },
  );
  return workflow;
}

describe("WorkflowManagementPanel cancel button", () => {
  beforeEach(() => {
    mockedCancelWorkflow.mockReset();
  });

  it("shows the cancel button for HR when workflow is in_progress", () => {
    renderPanel({ workflowStatus: "in_progress" });
    expect(screen.getByRole("button", { name: /Vorgang stornieren/i })).toBeTruthy();
  });

  it("shows the cancel button for waiting_for_supervisor", () => {
    renderPanel({ workflowStatus: "waiting_for_supervisor" });
    expect(screen.getByRole("button", { name: /Vorgang stornieren/i })).toBeTruthy();
  });

  it("hides the cancel button when workflow is completed", () => {
    renderPanel({ workflowStatus: "completed" });
    expect(screen.queryByRole("button", { name: /Vorgang stornieren/i })).toBeNull();
  });

  it("hides the cancel button when workflow is already cancelled", () => {
    renderPanel({
      workflowStatus: "cancelled",
      cancelledAt: "2026-05-12T12:00:00.000Z",
      cancellationReasonCode: "entry_postponed",
    });
    expect(screen.queryByRole("button", { name: /Vorgang stornieren/i })).toBeNull();
  });

  it("renders the cancellation read-only block when workflow is cancelled", () => {
    renderPanel({
      workflowStatus: "cancelled",
      cancelledAt: "2026-05-12T12:00:00.000Z",
      cancellationReasonCode: "wrong_person",
      cancellationReasonDetail: "Personalnummer-Verwechslung",
    });
    expect(screen.getByText(/Vorgang storniert/i)).toBeTruthy();
    expect(screen.getByText(/Falsche Person/i)).toBeTruthy();
    expect(screen.getByText(/Personalnummer-Verwechslung/i)).toBeTruthy();
  });

  it("hides the cancel button for worker role even on active workflow", () => {
    renderPanel({ workflowStatus: "in_progress" }, ["auth_worker"]);
    expect(screen.queryByRole("button", { name: /Vorgang stornieren/i })).toBeNull();
  });

  it("opens the dialog and submits the cancellation with the chosen reason", async () => {
    mockedCancelWorkflow.mockResolvedValue({
      uid: "wf-cancel-1",
      previousStatus: "in_progress",
      cancelledTaskCount: 2,
      disabledNotificationCount: 1,
    });

    renderPanel({ workflowStatus: "in_progress" });

    fireEvent.click(screen.getByRole("button", { name: /Vorgang stornieren/i }));

    const dialog = await screen.findByRole("alertdialog", { name: /Workflow stornieren\?/i });
    expect(dialog).toBeTruthy();

    fireEvent.change(within(dialog).getByLabelText(/^Grund/i), { target: { value: "entry_cancelled" } });
    fireEvent.click(within(dialog).getByRole("button", { name: /^Vorgang stornieren$/i }));

    await waitFor(() => {
      expect(mockedCancelWorkflow).toHaveBeenCalledWith("wf-cancel-1", {
        reasonCode: "entry_cancelled",
        reasonDetail: undefined,
      });
    });
  });
});
