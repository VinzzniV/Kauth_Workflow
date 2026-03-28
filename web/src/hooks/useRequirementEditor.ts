import { useCallback, useEffect, useMemo, useState } from "react";
import { useToast } from "../components/feedback/ToastProvider";
import { useUpdateSupervisorStep } from "../services/mutations/workflowMutations";
import type { RequirementSelectionState, WorkflowDetail } from "../types/workflow";
import {
  applyRequirementBooleanEditorSelection,
  applyRequirementSingleSelectEditorSelection,
} from "../utils/requirementEditor";
import {
  buildRequirementSelections,
  createEmptyRequirementSelection,
  toRequirementSelectionPayload,
} from "../utils/requirements";

type UseRequirementEditorOptions = {
  workflowUid: string;
  workflow: WorkflowDetail | null;
  canEditSupervisorRequirements: boolean;
};

export function useRequirementEditor({
  workflowUid,
  workflow,
  canEditSupervisorRequirements,
}: UseRequirementEditorOptions) {
  const [requirementSelections, setRequirementSelections] = useState<Record<number, RequirementSelectionState>>({});
  const [isSavingRequirements, setIsSavingRequirements] = useState<boolean>(false);
  const { showError, showSuccess } = useToast();
  const updateSupervisorStepMutation = useUpdateSupervisorStep(workflowUid);

  useEffect(() => {
    if (!workflow) {
      setRequirementSelections({});
      return;
    }

    setRequirementSelections(buildRequirementSelections(workflow.requirements));
  }, [workflow]);

  const canSaveSupervisorRequirements = useMemo(() => {
    if (!workflow || !canEditSupervisorRequirements || isSavingRequirements) {
      return false;
    }

    return true;
  }, [canEditSupervisorRequirements, isSavingRequirements, workflow]);

  const setRequirementBoolean = useCallback(
    (requirementId: number, value: boolean | null) => {
      if (!workflow) {
        return;
      }

      setRequirementSelections((current) =>
        applyRequirementBooleanEditorSelection(workflow.requirements, current, requirementId, value)
      );
    },
    [workflow]
  );

  const setRequirementText = useCallback((requirementId: number, value: string) => {
    setRequirementSelections((current) => ({
      ...current,
      [requirementId]: {
        ...(current[requirementId] ?? createEmptyRequirementSelection()),
        valueText: value,
      },
    }));
  }, []);

  const setRequirementSelectedOption = useCallback(
    (requirementId: number, optionId: number | null) => {
      if (!workflow) {
        return;
      }

      setRequirementSelections((current) =>
        applyRequirementSingleSelectEditorSelection(workflow.requirements, current, requirementId, optionId)
      );
    },
    [workflow]
  );

  const toggleRequirementSelectedOption = useCallback((requirementId: number, optionId: number) => {
    setRequirementSelections((current) => {
      const existing = current[requirementId] ?? createEmptyRequirementSelection();
      const isActive = existing.selectedOptionIds.includes(optionId);

      return {
        ...current,
        [requirementId]: {
          ...existing,
          selectedOptionIds: isActive
            ? existing.selectedOptionIds.filter((id) => id !== optionId)
            : [...existing.selectedOptionIds, optionId],
        },
      };
    });
  }, []);

  const handleRequirementSave = useCallback(async () => {
    if (!workflow || !canEditSupervisorRequirements) {
      return;
    }

    setIsSavingRequirements(true);

    try {
      await updateSupervisorStepMutation.mutateAsync(
        toRequirementSelectionPayload(workflow.requirements, requirementSelections)
      );
      showSuccess("Anforderungen wurden per Admin-Override gespeichert.");
    } catch (err) {
      const message = err instanceof Error ? err.message : "Anforderungen konnten nicht gespeichert werden.";
      showError(message);
    } finally {
      setIsSavingRequirements(false);
    }
  }, [
    canEditSupervisorRequirements,
    requirementSelections,
    showError,
    showSuccess,
    updateSupervisorStepMutation,
    workflow,
  ]);

  return {
    requirementSelections,
    isSavingRequirements,
    canSaveSupervisorRequirements,
    setRequirementBoolean,
    setRequirementText,
    setRequirementSelectedOption,
    toggleRequirementSelectedOption,
    handleRequirementSave,
  };
}
