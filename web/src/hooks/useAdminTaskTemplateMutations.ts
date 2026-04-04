import { useAdminTaskTemplateConditionMutations } from "./useAdminTaskTemplateConditionMutations";
import { useAdminTaskTemplateDependencyMutations } from "./useAdminTaskTemplateDependencyMutations";
import type { AdminTaskTemplateDataController } from "./useAdminTaskTemplateData";
import type { OperationState } from "./adminTaskTemplateManagementModel";
import { useAdminTaskTemplateTemplateMutations } from "./useAdminTaskTemplateTemplateMutations";

type UseAdminTaskTemplateMutationsOptions = {
  confirm: ReturnType<typeof import("../components/feedback/useConfirmationDialog").useConfirmationDialog>;
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
  updateOperationState: (patch: Partial<OperationState>) => void;
  data: AdminTaskTemplateDataController;
};

export function useAdminTaskTemplateMutations(options: UseAdminTaskTemplateMutationsOptions) {
  const templateMutations = useAdminTaskTemplateTemplateMutations(options);
  const conditionMutations = useAdminTaskTemplateConditionMutations(options);
  const dependencyMutations = useAdminTaskTemplateDependencyMutations(options);

  return {
    ...templateMutations,
    ...conditionMutations,
    ...dependencyMutations,
  };
}
