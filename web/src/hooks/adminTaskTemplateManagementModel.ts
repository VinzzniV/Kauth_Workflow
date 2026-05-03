import type {
  AdminTaskSpec,
  AdminTaskSpecCondition,
  AdminTaskSpecDependency,
} from "../types/auth";

export type TemplateDraft = {
  specKey: string;
  title: string;
  category: string;
  description: string;
  iconKey: string;
  owningDepartmentId: string;
  defaultResponsibilityId: string;
  processAreaLabel: string;
  dueInDays: string;
  sortOrder: string;
  isDepartmentPhaseTask: boolean;
  isRequired: boolean;
  isActive: boolean;
};

export type ConditionDraft = {
  conditionGroup: string;
  answerKey: string;
  operator: "eq" | "neq" | "is_true" | "is_false" | "is_null" | "is_not_null";
  expectedValueText: string;
  expectedValueBoolean: "true" | "false" | "";
  expectedValueNumber: string;
};

export type DependencyDraft = {
  dependsOnTaskSpecId: string;
  requiredStatus: "open" | "ready" | "in_progress" | "blocked" | "done";
};

export type DependencyStatus = DependencyDraft["requiredStatus"];

export type OperationState = {
  isLoadingProcessTypes: boolean;
  isLoadingTemplates: boolean;
  isLoadingDependencyGraph: boolean;
  isLoadingConditions: boolean;
  isLoadingDependencies: boolean;
  isSaving: boolean;
  isDeleting: boolean;
  isSavingCondition: boolean;
  deletingConditionId: number | null;
  isSavingDependency: boolean;
  deletingDependencyId: number | null;
};

export const EMPTY_DRAFT: TemplateDraft = {
  specKey: "",
  title: "",
  category: "general",
  description: "",
  iconKey: "berechtigungen",
  owningDepartmentId: "",
  defaultResponsibilityId: "",
  processAreaLabel: "",
  dueInDays: "",
  sortOrder: "0",
  isDepartmentPhaseTask: true,
  isRequired: true,
  isActive: true,
};

export const EMPTY_CONDITION_DRAFT: ConditionDraft = {
  conditionGroup: "1",
  answerKey: "",
  operator: "eq",
  expectedValueText: "",
  expectedValueBoolean: "",
  expectedValueNumber: "",
};

export const EMPTY_DEPENDENCY_DRAFT: DependencyDraft = {
  dependsOnTaskSpecId: "",
  requiredStatus: "done",
};

export const INITIAL_OPERATION_STATE: OperationState = {
  isLoadingProcessTypes: true,
  isLoadingTemplates: false,
  isLoadingDependencyGraph: false,
  isLoadingConditions: false,
  isLoadingDependencies: false,
  isSaving: false,
  isDeleting: false,
  isSavingCondition: false,
  deletingConditionId: null,
  isSavingDependency: false,
  deletingDependencyId: null,
};

export function toDraft(template: AdminTaskSpec): TemplateDraft {
  return {
    specKey: template.specKey,
    title: template.title,
    category: template.category,
    description: template.description,
    iconKey: template.iconKey ?? "",
    owningDepartmentId: template.owningDepartmentId ? String(template.owningDepartmentId) : "",
    defaultResponsibilityId: template.defaultResponsibilityId ? String(template.defaultResponsibilityId) : "",
    processAreaLabel: template.processAreaLabel ?? "",
    dueInDays: template.dueInDays === null ? "" : String(template.dueInDays),
    sortOrder: String(template.sortOrder),
    isDepartmentPhaseTask: template.isDepartmentPhaseTask,
    isRequired: template.isRequired,
    isActive: template.isActive,
  };
}

export function groupConditions(conditions: AdminTaskSpecCondition[]) {
  const groups = new Map<number, AdminTaskSpecCondition[]>();
  for (const condition of conditions) {
    const current = groups.get(condition.conditionGroup) ?? [];
    current.push(condition);
    groups.set(condition.conditionGroup, current);
  }

  return Array.from(groups.entries())
    .sort((left, right) => left[0] - right[0])
    .map(([group, items]) => ({
      group,
      items: items.slice().sort((left, right) => left.id - right.id),
    }));
}

export function sortTemplates(templates: AdminTaskSpec[]) {
  return templates.slice().sort((left, right) =>
    left.sortOrder === right.sortOrder
      ? left.title.localeCompare(right.title, "de")
      : left.sortOrder - right.sortOrder
  );
}

export function sortConditions(conditions: AdminTaskSpecCondition[]) {
  return conditions.slice().sort((left, right) =>
    left.conditionGroup === right.conditionGroup
      ? left.id - right.id
      : left.conditionGroup - right.conditionGroup
  );
}

export function sortDependencies(dependencies: AdminTaskSpecDependency[]) {
  return dependencies
    .slice()
    .sort((left, right) => left.dependsOnSpecTitle.localeCompare(right.dependsOnSpecTitle, "de"));
}
