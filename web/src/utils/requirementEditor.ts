import type {
  RequirementResetTarget,
  RequirementSelectionState,
} from "../types/workflow";
import {
  createEmptyRequirementSelection,
  getRequirementSelectedOptionValue,
  getRequirementSelection,
  type RequirementEntry,
} from "./requirements";

function findRequirementByKey(requirements: RequirementEntry[], key: string): RequirementEntry | undefined {
  return requirements.find((requirement) => requirement.key === key);
}

function applyResetTarget(
  currentSelection: RequirementSelectionState | undefined,
  target: RequirementResetTarget
): RequirementSelectionState {
  const nextSelection = {
    ...(currentSelection ?? createEmptyRequirementSelection()),
  };

  if (target.clearBoolean) {
    nextSelection.valueBoolean = null;
  }

  if (target.clearText) {
    nextSelection.valueText = "";
  }

  if (target.clearNumber) {
    nextSelection.valueNumber = null;
  }

  if (target.clearSelectedOption) {
    nextSelection.selectedOptionId = null;
  }

  if (target.clearSelectedOptions) {
    nextSelection.selectedOptionIds = [];
  }

  return nextSelection;
}

function applyConfiguredResets(
  requirements: RequirementEntry[],
  currentSelections: Record<number, RequirementSelectionState>,
  targets: RequirementResetTarget[]
): Record<number, RequirementSelectionState> {
  const nextSelections = { ...currentSelections };

  for (const target of targets) {
    const targetRequirement = findRequirementByKey(requirements, target.requirementKey);
    if (!targetRequirement) {
      continue;
    }

    nextSelections[targetRequirement.id] = applyResetTarget(nextSelections[targetRequirement.id], target);
  }

  return nextSelections;
}

function hasMeaningfulSelection(
  requirement: RequirementEntry,
  selection: RequirementSelectionState
): boolean {
  if (requirement.inputType === "boolean") {
    return selection.valueBoolean !== null;
  }

  if (requirement.inputType === "text") {
    return selection.valueText.trim().length > 0;
  }

  if (requirement.inputType === "select") {
    return selection.selectedOptionId !== null;
  }

  return selection.selectedOptionIds.length > 0;
}

function isRequirementVisibleInEditor(
  requirement: RequirementEntry,
  requirements: RequirementEntry[],
  selections?: Record<number, RequirementSelectionState>
): boolean {
  if (requirement.behavior.visibilityDependencies.length === 0) {
    return true;
  }

  return requirement.behavior.visibilityDependencies.every((dependency) => {
    const dependencyRequirement = findRequirementByKey(requirements, dependency.dependencyKey);
    if (!dependencyRequirement) {
      return dependency.missingResult;
    }

    const dependencySelection = getRequirementSelection(dependencyRequirement, selections);
    if (!hasMeaningfulSelection(dependencyRequirement, dependencySelection)) {
      return dependency.missingResult;
    }

    if (dependency.kind === "boolean_true") {
      return dependencySelection.valueBoolean === true;
    }

    return getRequirementSelectedOptionValue(dependencyRequirement, selections) === (dependency.expectedValue ?? null);
  });
}

// Editor-only preview helpers. Backend validation and final visibility stay authoritative on save/load.
export function getRequirementEditorVisibleRequirements<TRequirement extends RequirementEntry>(
  requirements: TRequirement[],
  selections?: Record<number, RequirementSelectionState>
): TRequirement[] {
  return requirements.filter((requirement) => isRequirementVisibleInEditor(requirement, requirements, selections));
}

export function applyRequirementBooleanEditorSelection(
  requirements: RequirementEntry[],
  currentSelections: Record<number, RequirementSelectionState>,
  requirementId: number,
  value: boolean | null
): Record<number, RequirementSelectionState> {
  const nextSelections = {
    ...currentSelections,
    [requirementId]: {
      ...(currentSelections[requirementId] ?? createEmptyRequirementSelection()),
      valueBoolean: value,
    },
  };

  const changedRequirement = requirements.find((requirement) => requirement.id === requirementId);
  if (!changedRequirement) {
    return nextSelections;
  }

  if (value === true || changedRequirement.behavior.resetTargetsWhenNotTrue.length === 0) {
    return nextSelections;
  }

  return applyConfiguredResets(requirements, nextSelections, changedRequirement.behavior.resetTargetsWhenNotTrue);
}

export function applyRequirementSingleSelectEditorSelection(
  requirements: RequirementEntry[],
  currentSelections: Record<number, RequirementSelectionState>,
  requirementId: number,
  optionId: number | null
): Record<number, RequirementSelectionState> {
  const nextSelections = {
    ...currentSelections,
    [requirementId]: {
      ...(currentSelections[requirementId] ?? createEmptyRequirementSelection()),
      selectedOptionId: optionId,
    },
  };

  const changedRequirement = requirements.find((requirement) => requirement.id === requirementId);
  if (!changedRequirement || !changedRequirement.behavior.singleSelectReset) {
    return nextSelections;
  }

  const selectedOptionValue = changedRequirement.options.find((option) => option.id === optionId)?.value;
  if (
    selectedOptionValue &&
    changedRequirement.behavior.singleSelectReset.keepSelectedOptionValues.includes(selectedOptionValue)
  ) {
    return nextSelections;
  }

  return applyConfiguredResets(
    requirements,
    nextSelections,
    changedRequirement.behavior.singleSelectReset.targets
  );
}
