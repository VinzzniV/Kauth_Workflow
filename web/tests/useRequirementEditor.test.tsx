import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { useRequirementEditor } from "../src/hooks/useRequirementEditor";
import type { WorkflowDetail } from "../src/types/workflow";
import { createRequirementSnapshot } from "./testUtils";

const requirementEditorMocks = vi.hoisted(() => ({
  mutateAsync: vi.fn(),
  showError: vi.fn(),
  showSuccess: vi.fn(),
}));

vi.mock("../src/services/mutations/workflowMutations", () => ({
  useUpdateSupervisorStep: vi.fn(() => ({
    mutateAsync: requirementEditorMocks.mutateAsync,
  })),
}));

vi.mock("../src/components/feedback/useToast", () => ({
  useToast: vi.fn(() => ({
    showError: requirementEditorMocks.showError,
    showSuccess: requirementEditorMocks.showSuccess,
  })),
}));

function createWorkflow(uid: string, requirementText: string): WorkflowDetail {
  return {
    uid,
    workflowDefinition: {
      key: "onboarding",
      name: "Onboarding",
      requiresTargetPerson: true,
    },
    firstName: "Alice",
    lastName: "Example",
    employeeNumber: 1001,
    badgeNumber: 2001,
    departmentId: 10,
    departmentName: "IT",
    roleId: 5,
    roleName: "Engineer",
    status: "open",
    workflowStatus: "waiting_for_supervisor",
    createdAt: "2026-04-01T08:00:00.000Z",
    deadlineDate: null,
    archivedAt: null,
    targetPersonId: 42,
    requirements: [
      createRequirementSnapshot({
        id: 1,
        workflowRequirementId: 1,
        key: "notes",
        title: "Notizen",
        inputType: "text",
        value: {
          valueBoolean: null,
          valueText: requirementText,
          valueNumber: null,
          selectedOptionId: null,
          selectedOptionKey: null,
          selectedOptionValue: null,
          selectedOptionLabel: null,
          selectedOptions: [],
        },
      }),
    ],
    requirementSummary: {
      totalCount: 1,
      visibleCount: 1,
      answeredVisibleCount: requirementText.length > 0 ? 1 : 0,
      pendingVisibleCount: requirementText.length > 0 ? 0 : 1,
    },
    tasks: [],
    taskMetrics: {
      overall: {
        totalCount: 0,
        openCount: 0,
        inProgressCount: 0,
        doneCount: 0,
        completedCount: 0,
        activeCount: 0,
      },
      departmentPhase: {
        totalCount: 0,
        openCount: 0,
        inProgressCount: 0,
        doneCount: 0,
        completedCount: 0,
        activeCount: 0,
      },
    },
    taskAreas: [],
    notifications: [],
  };
}

function RequirementEditorHarness({ workflow }: { workflow: WorkflowDetail | null }) {
  const editor = useRequirementEditor({
    workflowUid: workflow?.uid ?? "",
    workflow,
    canEditSupervisorRequirements: true,
  });

  return (
    <div>
      <output data-testid="requirement-text">
        {editor.requirementSelections[1]?.valueText ?? ""}
      </output>
      <button type="button" onClick={() => editor.setRequirementText(1, "lokaler Entwurf")}>
        Entwurf ändern
      </button>
    </div>
  );
}

describe("useRequirementEditor", () => {
  it("preserves local edits for the same workflow uid and resets on uid changes", () => {
    const { rerender } = render(<RequirementEditorHarness workflow={createWorkflow("wf-1", "Serverwert A")} />);

    expect(screen.getByTestId("requirement-text").textContent).toBe("Serverwert A");

    fireEvent.click(screen.getByRole("button", { name: "Entwurf ändern" }));
    expect(screen.getByTestId("requirement-text").textContent).toBe("lokaler Entwurf");

    rerender(<RequirementEditorHarness workflow={createWorkflow("wf-1", "Serverwert B")} />);
    expect(screen.getByTestId("requirement-text").textContent).toBe("lokaler Entwurf");

    rerender(<RequirementEditorHarness workflow={createWorkflow("wf-2", "Serverwert C")} />);
    expect(screen.getByTestId("requirement-text").textContent).toBe("Serverwert C");
  });
});
