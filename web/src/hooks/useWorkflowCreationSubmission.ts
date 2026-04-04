import { useCallback, useState } from "react";
import { createWorkflow } from "../services/workflowApi";
import type { CompletedOnboardingSearchResult, EmployeeFormData, ProcessType } from "../types/workflow";
import {
  buildWorkflowCreationPayload,
  buildWorkflowCreationSuccessMessage,
  type SubmitState,
} from "./workflowCreationModel";

type UseWorkflowCreationSubmissionArgs = {
  selectedProcessTypeKey: string | null;
  selectedProcessType: ProcessType | null;
  requiresTargetPerson: boolean;
  selectedDepartmentId: number | null;
  selectedRoleId: number | null;
  selectedCompletedOnboarding: CompletedOnboardingSearchResult | null;
  employee: EmployeeFormData;
};

type UseWorkflowCreationSubmissionResult = {
  submitState: SubmitState;
  submitError: string | null;
  submitSuccessMessage: string | null;
  createdWorkflowUid: string | null;
  resetSubmissionState: () => void;
  submitWorkflow: () => Promise<void>;
};

export function useWorkflowCreationSubmission({
  selectedProcessTypeKey,
  selectedProcessType,
  requiresTargetPerson,
  selectedDepartmentId,
  selectedRoleId,
  selectedCompletedOnboarding,
  employee,
}: UseWorkflowCreationSubmissionArgs): UseWorkflowCreationSubmissionResult {
  const [submitState, setSubmitState] = useState<SubmitState>("idle");
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitSuccessMessage, setSubmitSuccessMessage] = useState<string | null>(null);
  const [createdWorkflowUid, setCreatedWorkflowUid] = useState<string | null>(null);

  const resetSubmissionState = useCallback(() => {
    setSubmitError(null);
    setSubmitSuccessMessage(null);
    setCreatedWorkflowUid(null);
    setSubmitState((previous) => (previous === "loading" ? previous : "idle"));
  }, []);

  const submitWorkflow = useCallback(async () => {
    if (!selectedProcessTypeKey) {
      setSubmitState("error");
      setSubmitError("Bitte zuerst einen Prozesstyp wählen.");
      return;
    }

    if (!requiresTargetPerson && (selectedDepartmentId === null || selectedRoleId === null)) {
      setSubmitState("error");
      setSubmitError("Bitte zuerst Abteilung und Stelle auswählen.");
      return;
    }

    if (requiresTargetPerson && !selectedCompletedOnboarding) {
      setSubmitState("error");
      setSubmitError("Bitte zuerst ein abgeschlossenes Onboarding auswählen.");
      return;
    }

    setSubmitState("loading");
    setSubmitError(null);
    setSubmitSuccessMessage(null);
    setCreatedWorkflowUid(null);

    const processTypeName = selectedProcessType?.name ?? selectedProcessTypeKey;
    const payload = buildWorkflowCreationPayload({
      selectedProcessTypeKey,
      requiresTargetPerson,
      selectedCompletedOnboarding,
      employee,
      selectedDepartmentId,
      selectedRoleId,
    });

    try {
      const response = await createWorkflow(payload);
      setCreatedWorkflowUid(response.uid);
      setSubmitState("success");
      setSubmitSuccessMessage(
        buildWorkflowCreationSuccessMessage({
          processTypeName,
          createdWorkflowUid: response.uid,
          requiresTargetPerson,
          selectedCompletedOnboarding,
        })
      );
    } catch (err) {
      setSubmitState("error");
      setSubmitError(err instanceof Error ? err.message : "Vorgang konnte nicht gestartet werden.");
    }
  }, [
    employee,
    requiresTargetPerson,
    selectedCompletedOnboarding,
    selectedDepartmentId,
    selectedProcessType,
    selectedProcessTypeKey,
    selectedRoleId,
  ]);

  return {
    submitState,
    submitError,
    submitSuccessMessage,
    createdWorkflowUid,
    resetSubmissionState,
    submitWorkflow,
  };
}
