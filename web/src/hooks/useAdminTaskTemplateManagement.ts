import { useCallback, useState } from "react";
import { useConfirmationDialog } from "../components/feedback/useConfirmationDialog";
import { INITIAL_OPERATION_STATE, type OperationState } from "./adminTaskTemplateManagementModel";
import { useAdminTaskTemplateData } from "./useAdminTaskTemplateData";
import { useAdminTaskTemplateMutations } from "./useAdminTaskTemplateMutations";

type UseAdminTaskTemplateManagementOptions = {
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
};

export function useAdminTaskTemplateManagement({
  onNotice,
  onError,
}: UseAdminTaskTemplateManagementOptions) {
  const confirm = useConfirmationDialog();
  const [operationState, setOperationState] = useState<OperationState>(INITIAL_OPERATION_STATE);

  const updateOperationState = useCallback((patch: Partial<OperationState>) => {
    setOperationState((current) => ({ ...current, ...patch }));
  }, []);

  const data = useAdminTaskTemplateData({
    onError,
    updateOperationState,
  });

  const mutations = useAdminTaskTemplateMutations({
    confirm,
    onNotice,
    onError,
    updateOperationState,
    data,
  });

  return {
    workflowDefinitions: data.workflowDefinitions,
    selectedWorkflowDefinitionId: data.selectedWorkflowDefinitionId,
    templates: data.templates,
    dependencyGraph: data.dependencyGraph,
    selectedTemplate: data.selectedTemplate,
    answerDefinitions: data.answerDefinitions,
    draft: data.draft,
    conditions: data.conditions,
    dependencies: data.dependencies,
    groupedConditions: data.groupedConditions,
    conditionDraft: data.conditionDraft,
    dependencyDraft: data.dependencyDraft,
    isCreatingNew: data.isCreatingNew,
    isLoadingProcessTypes: operationState.isLoadingProcessTypes,
    isLoadingTemplates: operationState.isLoadingTemplates,
    isLoadingDependencyGraph: operationState.isLoadingDependencyGraph,
    isLoadingConditions: operationState.isLoadingConditions,
    isLoadingDependencies: operationState.isLoadingDependencies,
    isSaving: operationState.isSaving,
    isDeleting: operationState.isDeleting,
    isSavingCondition: operationState.isSavingCondition,
    deletingConditionId: operationState.deletingConditionId,
    isSavingDependency: operationState.isSavingDependency,
    deletingDependencyId: operationState.deletingDependencyId,
    availableDependencyTemplates: mutations.availableDependencyTemplates,
    selectWorkflowDefinition: data.selectWorkflowDefinition,
    selectTemplate: data.selectTemplate,
    startCreatingTemplate: data.startCreatingTemplate,
    updateDraft: data.updateDraft,
    updateConditionDraft: data.updateConditionDraft,
    updateDependencyDraft: data.updateDependencyDraft,
    createTemplate: mutations.createTemplate,
    saveTemplate: mutations.saveTemplate,
    removeTemplate: mutations.removeTemplate,
    addCondition: mutations.addCondition,
    removeCondition: mutations.removeCondition,
    addConditionGroup: mutations.addConditionGroup,
    addDependency: mutations.addDependency,
    createDependencyFromGraph: mutations.createDependencyFromGraph,
    removeDependency: mutations.removeDependency,
    removeDependencyFromGraph: mutations.removeDependencyFromGraph,
  };
}
