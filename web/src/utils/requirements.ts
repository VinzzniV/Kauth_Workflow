import type {
  RequirementSelectionPayload,
  RequirementSelectionState,
  RoleRequirement,
  WorkflowRequirementSnapshot,
} from "../types/workflow";

type RequirementLike = RoleRequirement | WorkflowRequirementSnapshot;

function isWorkflowRequirementSnapshot(requirement: RequirementLike): requirement is WorkflowRequirementSnapshot {
  return "value" in requirement;
}

function hasExplicitSelection(
  selections: Record<number, RequirementSelectionState> | undefined,
  requirementId: number
): boolean {
  return selections !== undefined && Object.prototype.hasOwnProperty.call(selections, requirementId);
}

export type RequirementEntry = RequirementLike;

export function createEmptyRequirementSelection(): RequirementSelectionState {
  return {
    valueBoolean: null,
    valueText: "",
    valueNumber: null,
    selectedOptionId: null,
    selectedOptionIds: [],
  };
}

export function buildRequirementSelections(
  requirements: WorkflowRequirementSnapshot[]
): Record<number, RequirementSelectionState> {
  return requirements.reduce<Record<number, RequirementSelectionState>>((acc, requirement) => {
    acc[requirement.id] = {
      valueBoolean: requirement.value.valueBoolean,
      valueText: requirement.value.valueText ?? "",
      valueNumber: requirement.value.valueNumber,
      selectedOptionId: requirement.value.selectedOptionId,
      selectedOptionIds: requirement.value.selectedOptions.map((option) => option.id),
    };
    return acc;
  }, {});
}

export function hasRequirementSelectionChanges(
  requirements: WorkflowRequirementSnapshot[],
  selections: Record<number, RequirementSelectionState> | undefined
): boolean {
  if (!selections) {
    return false;
  }

  return requirements.some((requirement) => {
    const selection = selections[requirement.id];
    if (!selection) {
      return false;
    }

    const selectedOptionIds = requirement.value.selectedOptions.map((option) => option.id);

    return (
      selection.valueBoolean !== requirement.value.valueBoolean ||
      selection.valueText !== (requirement.value.valueText ?? "") ||
      selection.valueNumber !== requirement.value.valueNumber ||
      selection.selectedOptionId !== requirement.value.selectedOptionId ||
      selection.selectedOptionIds.length !== selectedOptionIds.length ||
      selection.selectedOptionIds.some((optionId, index) => optionId !== selectedOptionIds[index])
    );
  });
}

export function getRequirementSelection(
  requirement: RequirementLike,
  selections?: Record<number, RequirementSelectionState>
): RequirementSelectionState {
  if (hasExplicitSelection(selections, requirement.id)) {
    return selections?.[requirement.id] ?? createEmptyRequirementSelection();
  }

  if (isWorkflowRequirementSnapshot(requirement)) {
    return {
      valueBoolean: requirement.value.valueBoolean,
      valueText: requirement.value.valueText ?? "",
      valueNumber: requirement.value.valueNumber,
      selectedOptionId: requirement.value.selectedOptionId,
      selectedOptionIds: requirement.value.selectedOptions.map((option) => option.id),
    };
  }

  return createEmptyRequirementSelection();
}

export function getRequirementSelectedOptionValue(
  requirement: RequirementLike | undefined,
  selections?: Record<number, RequirementSelectionState>
): string | null {
  if (!requirement) {
    return null;
  }

  const selection = getRequirementSelection(requirement, selections);
  if (selection.selectedOptionId !== null) {
    return requirement.options.find((option) => option.id === selection.selectedOptionId)?.value ?? null;
  }

  if (isWorkflowRequirementSnapshot(requirement) && !hasExplicitSelection(selections, requirement.id)) {
    return requirement.value.selectedOptionValue ?? null;
  }

  return null;
}

export function toRequirementSelectionPayload(
  requirements: RequirementLike[],
  selections: Record<number, RequirementSelectionState>
): RequirementSelectionPayload[] {
  return requirements.map((requirement) => {
    const selection = selections[requirement.id] ?? createEmptyRequirementSelection();

    if (requirement.inputType === "boolean") {
      return {
        requirementId: requirement.id,
        valueBoolean: selection.valueBoolean,
      };
    }

    if (requirement.inputType === "text") {
      return {
        requirementId: requirement.id,
        valueText: selection.valueText,
      };
    }

    if (requirement.inputType === "select") {
      return {
        requirementId: requirement.id,
        selectedOptionId: selection.selectedOptionId,
      };
    }

    return {
      requirementId: requirement.id,
      selectedOptionIds: [...selection.selectedOptionIds],
    };
  });
}
