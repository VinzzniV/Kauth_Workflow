import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import WorkflowRequirementsPanel from "../src/components/workflow-detail/WorkflowRequirementsPanel";
import type { WorkflowDetail } from "../src/types/workflow";
import { buildRequirementSelections } from "../src/utils/requirements";
import { createRequirementSnapshot } from "./testUtils";

function createWorkflowDetail(requirements: WorkflowDetail["requirements"]): WorkflowDetail {
  return {
    uid: "wf-1",
    workflowDefinition: {
      key: "onboarding",
      name: "Onboarding",
      description: null,
      requiresTargetPerson: true,
    },
    firstName: "Peter",
    lastName: "Lustig",
    employeeNumber: 1001,
    badgeNumber: 2001,
    departmentId: 1,
    departmentName: "IT",
    roleId: 1,
    roleName: "Mitarbeiter",
    workflowStatus: "waiting_for_supervisor",
    createdAt: "2026-05-06T10:00:00.000Z",
    deadlineDate: null,
    archivedAt: null,
    targetPersonId: 1,
    requirements,
    requirementSummary: {
      totalCount: requirements.length,
      visibleCount: requirements.filter((requirement) => requirement.isVisible).length,
      answeredVisibleCount: 0,
      pendingVisibleCount: requirements.filter((requirement) => requirement.isVisible).length,
    },
    tasks: [],
    taskMetrics: {
      overall: {
        totalCount: 0,
        openCount: 0,
        inProgressCount: 0,
        blockedCount: 0,
        doneCount: 0,
        completedCount: 0,
        activeCount: 0,
      },
      required: {
        totalCount: 0,
        openCount: 0,
        inProgressCount: 0,
        blockedCount: 0,
        doneCount: 0,
        completedCount: 0,
        activeCount: 0,
      },
      departmentPhase: {
        totalCount: 0,
        openCount: 0,
        inProgressCount: 0,
        blockedCount: 0,
        doneCount: 0,
        completedCount: 0,
        activeCount: 0,
      },
    },
    taskAreas: [],
    notifications: [],
  };
}

describe("WorkflowRequirementsPanel", () => {
  it("renders read-only workflow requirements with normalized boolean state and derived visibility", () => {
    const requirements = [
      createRequirementSnapshot({
        id: 1,
        workflowRequirementId: 1,
        key: "hardware_requested",
        title: "Hardware benötigt?",
        inputType: "boolean",
        isVisible: true,
        value: {
          valueBoolean: null,
          valueText: null,
          valueNumber: null,
          selectedOptionId: null,
          selectedOptionKey: null,
          selectedOptionValue: null,
          selectedOptionLabel: null,
          selectedOptions: [],
        },
      }),
      createRequirementSnapshot({
        id: 2,
        workflowRequirementId: 2,
        key: "hardware_type",
        title: "Hardware",
        description: "Welche Hardware soll bereitgestellt werden?",
        inputType: "select",
        isVisible: true,
        behavior: {
          visibilityDependencies: [
            {
              dependencyKey: "hardware_requested",
              kind: "boolean_true",
              expectedValue: null,
              missingResult: false,
            },
          ],
          validation: null,
          resetTargetsWhenNotTrue: [],
          singleSelectReset: null,
        },
      }),
      createRequirementSnapshot({
        id: 3,
        workflowRequirementId: 3,
        key: "phone_requested",
        title: "Tragbares Telefon",
        inputType: "boolean",
        isVisible: true,
        value: {
          valueBoolean: null,
          valueText: null,
          valueNumber: null,
          selectedOptionId: null,
          selectedOptionKey: null,
          selectedOptionValue: null,
          selectedOptionLabel: null,
          selectedOptions: [],
        },
      }),
    ];
    const workflow = createWorkflowDetail(requirements);

    render(
      <WorkflowRequirementsPanel
        workflow={workflow}
        canEditSupervisorRequirements={false}
        requirementSelections={buildRequirementSelections(requirements)}
        isSavingRequirements={false}
        canSaveSupervisorRequirements={false}
        onToggleBoolean={vi.fn()}
        onTextChange={vi.fn()}
        onSelectOption={vi.fn()}
        onToggleMultiOption={vi.fn()}
        onSave={vi.fn()}
      />
    );

    expect(screen.getByLabelText("Hardware benötigt?: Nein")).toBeTruthy();
    expect(screen.getByLabelText("Tragbares Telefon: Nein")).toBeTruthy();
    expect(screen.queryByText("Hardware")).toBeNull();
    expect(screen.queryByText(/Nicht ausgewählt/i)).toBeNull();
  });
});
