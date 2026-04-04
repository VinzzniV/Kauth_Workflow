import { useCallback } from "react";
import {
  createAdminTaskTemplateCondition,
  deleteAdminTaskTemplateCondition,
} from "../services/adminConfigApi";
import { toNullableText } from "../components/admin-config/adminConfigHelpers";
import type { AdminTaskTemplateDataController } from "./useAdminTaskTemplateData";
import { EMPTY_CONDITION_DRAFT, sortConditions, type OperationState } from "./adminTaskTemplateManagementModel";

type UseAdminTaskTemplateConditionMutationsOptions = {
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
  updateOperationState: (patch: Partial<OperationState>) => void;
  data: AdminTaskTemplateDataController;
};

export function useAdminTaskTemplateConditionMutations({
  onNotice,
  onError,
  updateOperationState,
  data,
}: UseAdminTaskTemplateConditionMutationsOptions) {
  const addCondition = useCallback(async () => {
    if (!data.selectedTemplate) {
      onNotice(null);
      onError("Bitte zuerst ein Task-Template auswählen.");
      return;
    }

    const parsedGroup = Number(data.conditionDraft.conditionGroup);
    if (!Number.isFinite(parsedGroup) || parsedGroup <= 0) {
      onNotice(null);
      onError("Condition Group muss eine positive Zahl sein.");
      return;
    }

    const numberValue = data.conditionDraft.expectedValueNumber.trim();
    const parsedNumberValue = numberValue ? Number(numberValue) : null;
    if (numberValue && Number.isNaN(parsedNumberValue)) {
      onNotice(null);
      onError("Expected Number muss leer oder eine Zahl sein.");
      return;
    }

    updateOperationState({ isSavingCondition: true });
    onNotice(null);
    onError(null);

    try {
      const createdCondition = await createAdminTaskTemplateCondition(data.selectedTemplate.id, {
        conditionGroup: parsedGroup,
        answerKey: data.conditionDraft.answerKey.trim(),
        operator: data.conditionDraft.operator,
        expectedValueText: toNullableText(data.conditionDraft.expectedValueText),
        expectedValueBoolean:
          data.conditionDraft.expectedValueBoolean === ""
            ? null
            : data.conditionDraft.expectedValueBoolean === "true",
        expectedValueNumber: parsedNumberValue,
      });

      data.setConditions((current) => sortConditions(current.concat(createdCondition)));
      data.setTemplates((current) =>
        current.map((item) =>
          item.id === data.selectedTemplate!.id ? { ...item, conditionCount: item.conditionCount + 1 } : item
        )
      );
      data.setConditionDraft((current) => ({
        ...EMPTY_CONDITION_DRAFT,
        conditionGroup: current.conditionGroup,
      }));
      onNotice("Bedingung wurde angelegt.");
    } catch (err) {
      const message = err instanceof Error ? err.message : "Bedingung konnte nicht angelegt werden.";
      onError(message);
    } finally {
      updateOperationState({ isSavingCondition: false });
    }
  }, [data, onError, onNotice, updateOperationState]);

  const removeCondition = useCallback(async (conditionId: number) => {
    if (!data.selectedTemplate) {
      return;
    }

    const selectedTemplate = data.selectedTemplate;
    updateOperationState({ deletingConditionId: conditionId });
    onNotice(null);
    onError(null);

    try {
      await deleteAdminTaskTemplateCondition(selectedTemplate.id, conditionId);
      data.setConditions((current) => current.filter((item) => item.id !== conditionId));
      data.setTemplates((current) =>
        current.map((item) =>
          item.id === selectedTemplate.id ? { ...item, conditionCount: Math.max(0, item.conditionCount - 1) } : item
        )
      );
      onNotice("Bedingung wurde gelöscht.");
    } catch (err) {
      const message = err instanceof Error ? err.message : "Bedingung konnte nicht gelöscht werden.";
      onError(message);
    } finally {
      updateOperationState({ deletingConditionId: null });
    }
  }, [data, onError, onNotice, updateOperationState]);

  const addConditionGroup = useCallback(() => {
    const nextGroup =
      data.groupedConditions.length === 0
        ? 1
        : Math.max(...data.groupedConditions.map((group) => group.group)) + 1;
    data.setConditionDraft((current) => ({ ...current, conditionGroup: String(nextGroup) }));
  }, [data]);

  return {
    addCondition,
    removeCondition,
    addConditionGroup,
  };
}
