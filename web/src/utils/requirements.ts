import type {
  RequirementResetTarget,
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

function findRequirementByKey(requirements: RequirementLike[], key: string): RequirementLike | undefined {
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
  requirements: RequirementLike[],
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

function areVisibilityDependenciesSatisfied(
  requirement: RequirementLike,
  requirements: RequirementLike[],
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

function hasMeaningfulSelection(
  requirement: RequirementLike,
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

export function isRequirementVisible(
  requirement: RequirementLike,
  requirements: RequirementLike[],
  selections?: Record<number, RequirementSelectionState>
): boolean {
  return areVisibilityDependenciesSatisfied(requirement, requirements, selections);
}

export function getVisibleRequirements<TRequirement extends RequirementLike>(
  requirements: TRequirement[],
  selections?: Record<number, RequirementSelectionState>
): TRequirement[] {
  return requirements.filter((requirement) => isRequirementVisible(requirement, requirements, selections));
}

export function hasRequirementAnswer(requirement: WorkflowRequirementSnapshot): boolean {
  if (requirement.inputType === "boolean") {
    return requirement.value.valueBoolean !== null;
  }

  if (requirement.inputType === "text") {
    return Boolean(requirement.value.valueText?.trim());
  }

  if (requirement.inputType === "select") {
    return requirement.value.selectedOptionId !== null;
  }

  return requirement.value.selectedOptions.length > 0;
}

export function validateRequirementSelections(
  requirements: WorkflowRequirementSnapshot[],
  selections: Record<number, RequirementSelectionState>
): string | null {
  for (const requirement of requirements) {
    if (!isRequirementVisible(requirement, requirements, selections)) {
      continue;
    }

    const selection = selections[requirement.id] ?? createEmptyRequirementSelection();
    const validation = requirement.behavior.validation;

    if (requirement.inputType === "boolean" && selection.valueBoolean === null) {
      return `Bitte für "${requirement.title}" Ja oder Nein auswählen.`;
    }

    if (requirement.inputType === "select" && selection.selectedOptionId === null) {
      if (validation?.kind === "single_select_required") {
        return validation.message;
      }

      return `Bitte für "${requirement.title}" eine Auswahl treffen.`;
    }

    if (!validation) {
      continue;
    }

    if (validation.kind === "text_required" && !selection.valueText.trim()) {
      return validation.message;
    }

    if (validation.kind === "multi_select_required" && selection.selectedOptionIds.length === 0) {
      return validation.message;
    }

    if (validation.kind === "single_select_required" && selection.selectedOptionId === null) {
      return validation.message;
    }
  }

  return null;
}

export function applyRequirementBooleanSelection(
  requirements: RequirementLike[],
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

export function applyRequirementSingleSelectSelection(
  requirements: RequirementLike[],
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
