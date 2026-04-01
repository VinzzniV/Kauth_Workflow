import { useCallback, useEffect, useMemo, useState } from "react";
import { createWorkflow } from "../services/workflowApi";
import { useDepartments, useRoles } from "../services/queries/roleQueries";
import { useProcessTypes } from "../services/queries/processTypeQueries";
import {
  useCompletedOnboardingsSearch,
  useWorkflowConfig,
} from "../services/queries/workflowQueries";
import type {
  CompletedOnboardingSearchResult,
  Department,
  EmployeeFormData,
  ProcessType,
  Role,
  WorkflowConfig,
} from "../types/workflow";

type SubmitState = "idle" | "loading" | "success" | "error";
export type WorkflowCreationStep = "process" | "context" | "review";

type WorkflowStartFormState = {
  processTypeKey: string | null;
  employee: EmployeeFormData;
  departmentId: number | null;
  roleId: number | null;
  completedOnboardingSearch: string;
};

const EMPTY_EMPLOYEE: EmployeeFormData = {
  firstName: "",
  lastName: "",
  employeeNumber: 0,
  badgeNumber: 0,
  deadlineDate: "",
};

type UseWorkflowCreationResult = {
  currentStep: WorkflowCreationStep;
  processTypes: ProcessType[];
  processTypesLoading: boolean;
  selectedProcessTypeKey: string | null;
  selectedProcessType: ProcessType | null;
  requiresTargetPerson: boolean;
  employee: EmployeeFormData;
  selectedDepartmentId: number | null;
  selectedRoleId: number | null;
  selectedDepartment: Department | null;
  selectedRole: Role | null;
  selectedCompletedOnboarding: CompletedOnboardingSearchResult | null;
  completedOnboardingSearch: string;
  completedOnboardings: CompletedOnboardingSearchResult[];
  completedOnboardingsLoading: boolean;
  completedOnboardingsError: string | null;
  availableRoles: Role[];
  roles: Role[];
  departments: Department[];
  rolesLoading: boolean;
  rolesError: string | null;
  workflowConfig: WorkflowConfig | null;
  workflowConfigLoading: boolean;
  workflowConfigError: string | null;
  submitState: SubmitState;
  submitError: string | null;
  submitSuccessMessage: string | null;
  createdWorkflowUid: string | null;
  setProcessType: (key: string) => void;
  goToProcessStep: () => void;
  goToContextStep: () => void;
  goToReviewStep: () => void;
  setEmployeeField: (field: keyof EmployeeFormData, value: string | number) => void;
  setSelectedDepartment: (departmentId: number | null) => void;
  setSelectedRole: (roleId: number | null) => void;
  setCompletedOnboardingSearch: (value: string) => void;
  setSelectedCompletedOnboarding: (onboarding: CompletedOnboardingSearchResult | null) => void;
  submitWorkflow: () => Promise<void>;
  canGoToContextStep: boolean;
  canGoToReviewStep: boolean;
  canSubmit: boolean;
  reloadRoles: () => Promise<void>;
};

function hasValidEmployeeData(employee: EmployeeFormData): boolean {
  return (
    employee.firstName.trim().length > 0 &&
    employee.lastName.trim().length > 0 &&
    employee.employeeNumber > 0 &&
    employee.badgeNumber > 0
  );
}

export function useWorkflowCreation(): UseWorkflowCreationResult {
  const [currentStep, setCurrentStep] = useState<WorkflowCreationStep>("process");
  const [formState, setFormState] = useState<WorkflowStartFormState>({
    processTypeKey: null,
    employee: EMPTY_EMPLOYEE,
    departmentId: null,
    roleId: null,
    completedOnboardingSearch: "",
  });
  const [selectedCompletedOnboardingSnapshot, setSelectedCompletedOnboardingSnapshot] =
    useState<CompletedOnboardingSearchResult | null>(null);
  const [debouncedCompletedOnboardingSearch, setDebouncedCompletedOnboardingSearch] = useState("");
  const [submitState, setSubmitState] = useState<SubmitState>("idle");
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitSuccessMessage, setSubmitSuccessMessage] = useState<string | null>(null);
  const [createdWorkflowUid, setCreatedWorkflowUid] = useState<string | null>(null);
  const processTypesQuery = useProcessTypes();
  const processTypes = useMemo(() => processTypesQuery.data ?? [], [processTypesQuery.data]);
  const processTypesLoading = processTypesQuery.isLoading;

  const resetSubmissionState = useCallback(() => {
    setSubmitError(null);
    setSubmitSuccessMessage(null);
    setCreatedWorkflowUid(null);
    setSubmitState((previous) => (previous === "loading" ? previous : "idle"));
  }, []);

  const rolesQuery = useRoles();
  const departmentsQuery = useDepartments();
  const roles = useMemo(
    () => (rolesQuery.data ?? []).filter((role) => role.isActive),
    [rolesQuery.data]
  );
  const departments = useMemo(() => departmentsQuery.data ?? [], [departmentsQuery.data]);
  const rolesLoading = rolesQuery.isLoading || departmentsQuery.isLoading;
  const rolesError =
    rolesQuery.error instanceof Error
      ? rolesQuery.error.message
      : departmentsQuery.error instanceof Error
        ? departmentsQuery.error.message
        : rolesQuery.error || departmentsQuery.error
          ? "Die Rollen konnten nicht geladen werden."
          : null;
  const reloadRoles = useCallback(async () => {
    await Promise.all([rolesQuery.refetch(), departmentsQuery.refetch()]);
  }, [departmentsQuery, rolesQuery]);

  const selectedProcessTypeKey = useMemo(() => {
    if (formState.processTypeKey && processTypes.some((type) => type.key === formState.processTypeKey)) {
      return formState.processTypeKey;
    }

    return processTypes.length === 1 ? processTypes[0].key : null;
  }, [formState.processTypeKey, processTypes]);

  const selectedProcessType = useMemo(
    () => processTypes.find((pt) => pt.key === selectedProcessTypeKey) ?? null,
    [processTypes, selectedProcessTypeKey]
  );

  const requiresTargetPerson = selectedProcessType?.requiresTargetPerson ?? false;
  const completedOnboardingSearchValue = requiresTargetPerson
    ? formState.completedOnboardingSearch
    : "";

  useEffect(() => {
    const timeoutHandle = window.setTimeout(() => {
      setDebouncedCompletedOnboardingSearch(completedOnboardingSearchValue);
    }, 250);

    return () => window.clearTimeout(timeoutHandle);
  }, [completedOnboardingSearchValue]);

  const selectedDepartment = useMemo(
    () =>
      formState.departmentId !== null && departments.some((department) => department.id === formState.departmentId)
        ? departments.find((department) => department.id === formState.departmentId) ?? null
        : null,
    [departments, formState.departmentId]
  );

  const selectedDepartmentId = selectedDepartment?.id ?? null;

  const selectedRoleId = useMemo(() => {
    if (formState.roleId === null || selectedDepartmentId === null) {
      return null;
    }

    return roles.some((role) => role.id === formState.roleId && role.departmentId === selectedDepartmentId)
      ? formState.roleId
      : null;
  }, [formState.roleId, roles, selectedDepartmentId]);

  const selectedRole = useMemo(
    () => roles.find((role) => role.id === selectedRoleId) ?? null,
    [roles, selectedRoleId]
  );

  const availableRoles = useMemo(() => {
    if (selectedDepartmentId === null) {
      return [];
    }

    return roles.filter((role) => role.departmentId === selectedDepartmentId);
  }, [roles, selectedDepartmentId]);

  const completedOnboardingsQuery = useCompletedOnboardingsSearch(
    debouncedCompletedOnboardingSearch,
    requiresTargetPerson
  );
  const completedOnboardings = useMemo(
    () => completedOnboardingsQuery.data ?? [],
    [completedOnboardingsQuery.data]
  );
  const completedOnboardingsLoading = requiresTargetPerson && completedOnboardingsQuery.isFetching;
  const completedOnboardingsError =
    requiresTargetPerson && completedOnboardingsQuery.error instanceof Error
      ? completedOnboardingsQuery.error.message
      : requiresTargetPerson && completedOnboardingsQuery.error
        ? "Abgeschlossene Onboardings konnten nicht geladen werden."
        : null;

  const selectedCompletedOnboarding = useMemo(() => {
    if (!selectedCompletedOnboardingSnapshot) {
      return null;
    }

    return (
      completedOnboardings.find(
        (result) => result.workflowUid === selectedCompletedOnboardingSnapshot.workflowUid
      ) ?? selectedCompletedOnboardingSnapshot
    );
  }, [completedOnboardings, selectedCompletedOnboardingSnapshot]);
  const effectiveRoleIdForConfig = requiresTargetPerson
    ? selectedCompletedOnboarding?.roleId ?? null
    : selectedRoleId;

  const workflowConfigQuery = useWorkflowConfig(
    effectiveRoleIdForConfig,
    selectedProcessTypeKey,
    Boolean(selectedProcessTypeKey)
  );
  const workflowConfig = useMemo<WorkflowConfig | null>(
    () => (selectedProcessTypeKey ? workflowConfigQuery.data ?? null : null),
    [selectedProcessTypeKey, workflowConfigQuery.data]
  );
  const workflowConfigLoading = Boolean(selectedProcessTypeKey) && workflowConfigQuery.isFetching;
  const workflowConfigError =
    selectedProcessTypeKey && workflowConfigQuery.error instanceof Error
      ? workflowConfigQuery.error.message
      : selectedProcessTypeKey && workflowConfigQuery.error
        ? "Workflow-Konfiguration konnte nicht geladen werden."
        : null;

  const setProcessType = (key: string) => {
    resetSubmissionState();
    setFormState((previous) => ({
      ...previous,
      processTypeKey: key,
      departmentId: null,
      roleId: null,
      completedOnboardingSearch: "",
      employee: {
        ...EMPTY_EMPLOYEE,
        deadlineDate: previous.employee.deadlineDate,
      },
    }));
    setSelectedCompletedOnboardingSnapshot(null);
  };

  const setEmployeeField = (field: keyof EmployeeFormData, value: string | number) => {
    resetSubmissionState();
    setFormState((previous) => ({
      ...previous,
      employee: {
        ...previous.employee,
        [field]: typeof value === "string" ? value : Number(value),
      },
    }));
  };

  const setSelectedDepartment = (departmentId: number | null) => {
    resetSubmissionState();
    setFormState((previous) => {
      const roleStillMatchesDepartment =
        previous.roleId !== null &&
        departmentId !== null &&
        roles.some((role) => role.id === previous.roleId && role.departmentId === departmentId);

      return {
        ...previous,
        departmentId,
        roleId: roleStillMatchesDepartment ? previous.roleId : null,
      };
    });
  };

  const setSelectedRole = (roleId: number | null) => {
    resetSubmissionState();
    if (roleId === null) {
      setFormState((previous) => ({
        ...previous,
        roleId: null,
      }));
      return;
    }

    const role = roles.find((item) => item.id === roleId);
    if (!role) {
      return;
    }

    setFormState((previous) => ({
      ...previous,
      departmentId: role.departmentId,
      roleId: role.id,
    }));
  };

  const setCompletedOnboardingSearch = (value: string) => {
    resetSubmissionState();
    setFormState((previous) => ({
      ...previous,
      completedOnboardingSearch: value,
    }));
  };

  const setSelectedCompletedOnboarding = (onboarding: CompletedOnboardingSearchResult | null) => {
    resetSubmissionState();
    setSelectedCompletedOnboardingSnapshot(onboarding);
    setFormState((previous) => ({
      ...previous,
      departmentId: onboarding?.departmentId ?? null,
      roleId: onboarding?.roleId ?? null,
      employee: {
        ...previous.employee,
        firstName: onboarding?.firstName ?? "",
        lastName: onboarding?.lastName ?? "",
        employeeNumber: onboarding?.employeeNumber ?? 0,
        badgeNumber: onboarding?.badgeNumber ?? 0,
      },
    }));
  };

  const isContextComplete = useMemo(() => {
    if (!selectedProcessTypeKey) {
      return false;
    }

    if (requiresTargetPerson) {
      return Boolean(
        selectedCompletedOnboarding &&
          selectedCompletedOnboarding.departmentId &&
          selectedCompletedOnboarding.roleId &&
          selectedCompletedOnboarding.employeeNumber > 0 &&
          selectedCompletedOnboarding.badgeNumber > 0 &&
          !completedOnboardingsLoading
      );
    }

    const metadataIsValid =
      selectedDepartmentId !== null &&
      selectedRoleId !== null &&
      roles.some((role) => role.id === selectedRoleId && role.departmentId === selectedDepartmentId);

    return hasValidEmployeeData(formState.employee) && !rolesLoading && !rolesError && metadataIsValid;
  }, [
    completedOnboardingsLoading,
    formState.employee,
    requiresTargetPerson,
    roles,
    rolesError,
    rolesLoading,
    selectedCompletedOnboarding,
    selectedDepartmentId,
    selectedRoleId,
    selectedProcessTypeKey,
  ]);

  const canGoToContextStep = selectedProcessTypeKey !== null;
  const canGoToReviewStep = isContextComplete;
  const canSubmit = currentStep === "review" && isContextComplete;

  const goToProcessStep = useCallback(() => {
    resetSubmissionState();
    setCurrentStep("process");
  }, [resetSubmissionState]);

  const goToContextStep = useCallback(() => {
    if (!selectedProcessTypeKey) {
      return;
    }

    resetSubmissionState();
    setCurrentStep("context");
  }, [resetSubmissionState, selectedProcessTypeKey]);

  const goToReviewStep = useCallback(() => {
    if (!isContextComplete) {
      return;
    }

    resetSubmissionState();
    setCurrentStep("review");
  }, [isContextComplete, resetSubmissionState]);

  const submitWorkflow = async () => {
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

    const payload =
      requiresTargetPerson && selectedCompletedOnboarding
        ? {
            processTypeKey: selectedProcessTypeKey,
            targetPersonId: selectedCompletedOnboarding.personId,
            sourceWorkflowUid: selectedCompletedOnboarding.workflowUid,
            firstName: selectedCompletedOnboarding.firstName,
            lastName: selectedCompletedOnboarding.lastName,
            employeeNumber: selectedCompletedOnboarding.employeeNumber,
            badgeNumber: selectedCompletedOnboarding.badgeNumber,
            deadlineDate: formState.employee.deadlineDate.trim() || null,
            departmentId: null,
            roleId: null,
          }
        : {
            processTypeKey: selectedProcessTypeKey,
            firstName: formState.employee.firstName.trim(),
            lastName: formState.employee.lastName.trim(),
            employeeNumber: formState.employee.employeeNumber,
            badgeNumber: formState.employee.badgeNumber,
            deadlineDate: formState.employee.deadlineDate.trim() || null,
            departmentId: selectedDepartmentId,
            roleId: selectedRoleId,
          };

    try {
      const response = await createWorkflow(payload);
      setCreatedWorkflowUid(response.uid);

      setSubmitState("success");
      setSubmitSuccessMessage(
        `${processTypeName} ${response.uid} angelegt.${
          requiresTargetPerson && selectedCompletedOnboarding
            ? " Automatisch mit abgeschlossenem Onboarding verknüpft."
            : ""
        } Nächster Schritt: Der zuständige Prozessschritt kann jetzt im Tool weiterbearbeitet werden.`
      );
    } catch (err) {
      const message = err instanceof Error ? err.message : "Vorgang konnte nicht gestartet werden.";
      setSubmitState("error");
      setSubmitError(message);
    }
  };

  return {
    currentStep,
    processTypes,
    processTypesLoading,
    selectedProcessTypeKey,
    selectedProcessType,
    requiresTargetPerson,
    employee: formState.employee,
    selectedDepartmentId,
    selectedRoleId,
    selectedDepartment,
    selectedRole,
    selectedCompletedOnboarding,
    completedOnboardingSearch: formState.completedOnboardingSearch,
    completedOnboardings: requiresTargetPerson ? completedOnboardings : [],
    completedOnboardingsLoading: requiresTargetPerson ? completedOnboardingsLoading : false,
    completedOnboardingsError: requiresTargetPerson ? completedOnboardingsError : null,
    availableRoles,
    roles,
    departments,
    rolesLoading,
    rolesError,
    workflowConfig,
    workflowConfigLoading,
    workflowConfigError,
    submitState,
    submitError,
    submitSuccessMessage,
    createdWorkflowUid,
    setProcessType,
    goToProcessStep,
    goToContextStep,
    goToReviewStep,
    setEmployeeField,
    setSelectedDepartment,
    setSelectedRole,
    setCompletedOnboardingSearch,
    setSelectedCompletedOnboarding,
    submitWorkflow,
    canGoToContextStep,
    canGoToReviewStep,
    canSubmit,
    reloadRoles,
  };
}
