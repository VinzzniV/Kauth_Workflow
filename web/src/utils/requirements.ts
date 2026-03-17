import type {
  RequirementSelectionState,
  RoleRequirement,
  WorkflowRequirementSnapshot,
} from "../types/workflow";
import {
  requirementBooleanResetRules,
  requirementSingleSelectResetRules,
  requirementValidationRules,
  requirementVisibilityRules,
} from "./requirementRules";

export type RequirementEntry = RoleRequirement | WorkflowRequirementSnapshot;

function isWorkflowRequirementSnapshot(requirement: RequirementEntry): requirement is WorkflowRequirementSnapshot {
  return "value" in requirement;
}

function hasExplicitSelection(
  selections: Record<number, RequirementSelectionState> | undefined,
  requirementId: number
): boolean {
  return selections !== undefined && Object.prototype.hasOwnProperty.call(selections, requirementId);
}

function findRequirementByKey(
  requirements: RequirementEntry[],
  key: string
): RequirementEntry | undefined {
  return requirements.find((requirement) => requirement.key === key);
}

function applySelectionPatch(
  currentSelection: RequirementSelectionState | undefined,
  patch: Partial<RequirementSelectionState>
): RequirementSelectionState {
  return {
    ...(currentSelection ?? createEmptyRequirementSelection()),
    ...patch,
  };
}

function areVisibilityDependenciesSatisfied(
  requirement: RequirementEntry,
  requirements: RequirementEntry[],
  selections?: Record<number, RequirementSelectionState>
): boolean {
  const dependencies = requirementVisibilityRules[requirement.key];
  if (!dependencies || dependencies.length === 0) {
    return true;
  }

  return dependencies.every((dependency) => {
    const dependencyRequirement = findRequirementByKey(requirements, dependency.dependencyKey);
    if (!dependencyRequirement) {
      return dependency.missingResult ?? true;
    }

    if (dependency.kind === "boolean_true") {
      return getRequirementSelection(dependencyRequirement, selections).valueBoolean === true;
    }

    return (
      getRequirementSelectedOptionValue(dependencyRequirement, selections) ===
      (dependency.expectedValue ?? null)
    );
  });
}

function applyConfiguredResets(
  requirements: RequirementEntry[],
  currentSelections: Record<number, RequirementSelectionState>,
  targets: Array<{ requirementKey: string; patch: Partial<RequirementSelectionState> }>
): Record<number, RequirementSelectionState> {
  const nextSelections = { ...currentSelections };

  for (const target of targets) {
    const dependencyRequirement = findRequirementByKey(requirements, target.requirementKey);
    if (!dependencyRequirement) {
      continue;
    }

    nextSelections[dependencyRequirement.id] = applySelectionPatch(nextSelections[dependencyRequirement.id], target.patch);
  }

  return nextSelections;
}

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
  requirement: RequirementEntry,
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
  requirement: RequirementEntry | undefined,
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

export function isRequirementVisible(
  requirement: RequirementEntry,
  requirements: RequirementEntry[],
  selections?: Record<number, RequirementSelectionState>
): boolean {
  return areVisibilityDependenciesSatisfied(requirement, requirements, selections);
}

export function getVisibleRequirements<TRequirement extends RequirementEntry>(
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
    const validationRule = requirementValidationRules[requirement.key];

    if (requirement.inputType === "boolean" && selection.valueBoolean === null) {
      return `Bitte für "${requirement.title}" Ja oder Nein auswählen.`;
    }

    if (requirement.inputType === "select" && selection.selectedOptionId === null) {
      if (validationRule?.kind === "single_select_required") {
        return validationRule.message;
      }

      return `Bitte für "${requirement.title}" eine Auswahl treffen.`;
    }

    if (!validationRule) {
      continue;
    }

    if (validationRule.kind === "text_required" && !selection.valueText.trim()) {
      return validationRule.message;
    }

    if (validationRule.kind === "multi_select_required" && selection.selectedOptionIds.length === 0) {
      return validationRule.message;
    }

    if (validationRule.kind === "single_select_required" && selection.selectedOptionId === null) {
      return validationRule.message;
    }
  }

  return null;
}

export function applyRequirementBooleanSelection(
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

  const resetTargets = requirementBooleanResetRules[changedRequirement.key];
  if (value !== true && resetTargets) {
    return applyConfiguredResets(requirements, nextSelections, resetTargets);
  }

  return nextSelections;
}

export function applyRequirementSingleSelectSelection(
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
  if (!changedRequirement) {
    return nextSelections;
  }

  const resetRule = requirementSingleSelectResetRules[changedRequirement.key];
  if (!resetRule) {
    return nextSelections;
  }

  const selectedOptionValue = changedRequirement.options.find((option) => option.id === optionId)?.value;
  if (selectedOptionValue && resetRule.keepSelectedOptionValues.includes(selectedOptionValue)) {
    return nextSelections;
  }

  return applyConfiguredResets(requirements, nextSelections, resetRule.targets);
}
