import { useCallback, useState } from "react";
import { createPerson } from "../services/peopleApi";
import { createWorkflow } from "../services/workflowApi";
import type {
  EmployeeFormData,
  StartableWorkflowDefinition,
  WorkflowTargetPerson,
} from "../types/workflow";
import {
  buildWorkflowCreationPayload,
  buildWorkflowCreationSuccessMessage,
  type SubmitState,
} from "./workflowCreationModel";

type UseWorkflowCreationSubmissionArgs = {
  selectedWorkflowDefinitionKey: string | null;
  selectedWorkflowDefinition: StartableWorkflowDefinition | null;
  requiresTargetPerson: boolean;
  selectedDepartmentId: number | null;
  selectedRoleId: number | null;
  selectedTargetPerson: WorkflowTargetPerson | null;
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
  selectedWorkflowDefinitionKey,
  selectedWorkflowDefinition,
  requiresTargetPerson,
  selectedDepartmentId,
  selectedRoleId,
  selectedTargetPerson,
  employee,
}: UseWorkflowCreationSubmissionArgs): UseWorkflowCreationSubmissionResult {
  const [submitState, setSubmitState] = useState<SubmitState>("idle");
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitSuccessMessage, setSubmitSuccessMessage] = useState<string | null>(null);
  const [createdWorkflowUid, setCreatedWorkflowUid] = useState<string | null>(null);
  const [stagedTargetPerson, setStagedTargetPerson] = useState<WorkflowTargetPerson | null>(null);

  const resetSubmissionState = useCallback(() => {
    setSubmitError(null);
    setSubmitSuccessMessage(null);
    setCreatedWorkflowUid(null);
    setStagedTargetPerson(null);
    setSubmitState((previous) => (previous === "loading" ? previous : "idle"));
  }, []);

  const submitWorkflow = useCallback(async () => {
    if (!selectedWorkflowDefinitionKey) {
      setSubmitState("error");
      setSubmitError("Bitte zuerst einen Workflow wählen.");
      return;
    }

    if (!requiresTargetPerson && (selectedDepartmentId === null || selectedRoleId === null)) {
      setSubmitState("error");
      setSubmitError("Bitte zuerst Abteilung und Stelle auswählen.");
      return;
    }

    if (requiresTargetPerson && !selectedTargetPerson) {
      setSubmitState("error");
      setSubmitError("Bitte zuerst eine bestehende Person auswählen.");
      return;
    }

    setSubmitState("loading");
    setSubmitError(null);
    setSubmitSuccessMessage(null);
    setCreatedWorkflowUid(null);

    const workflowName = selectedWorkflowDefinition?.name ?? selectedWorkflowDefinitionKey;

    try {
      const targetPerson = requiresTargetPerson
        ? selectedTargetPerson
        : stagedTargetPerson
          ?? await createPerson({
            firstName: employee.firstName.trim(),
            lastName: employee.lastName.trim(),
            employeeNumber: employee.employeeNumber,
            badgeNumber: employee.badgeNumber,
            departmentId: selectedDepartmentId as number,
            roleId: selectedRoleId as number,
          });

      if (!targetPerson) {
        throw new Error("Die Zielperson konnte nicht aufgelöst werden.");
      }

      if (!requiresTargetPerson && stagedTargetPerson === null) {
        setStagedTargetPerson(targetPerson);
      }

      const payload = buildWorkflowCreationPayload({
        selectedWorkflowDefinitionKey,
        requiresTargetPerson,
        targetPersonId: targetPerson.personId,
        employee,
        selectedDepartmentId,
        selectedRoleId,
      });

      const response = await createWorkflow(payload);
      setCreatedWorkflowUid(response.uid);
      setSubmitState("success");
      setSubmitSuccessMessage(
        buildWorkflowCreationSuccessMessage({
          workflowName,
          createdWorkflowUid: response.uid,
          requiresTargetPerson,
          selectedTargetPerson: requiresTargetPerson ? targetPerson : null,
        })
      );
    } catch (err) {
      setSubmitState("error");
      setSubmitError(err instanceof Error ? err.message : "Vorgang konnte nicht gestartet werden.");
    }
  }, [
    employee,
    requiresTargetPerson,
    selectedDepartmentId,
    selectedRoleId,
    selectedTargetPerson,
    selectedWorkflowDefinition,
    selectedWorkflowDefinitionKey,
    stagedTargetPerson,
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
