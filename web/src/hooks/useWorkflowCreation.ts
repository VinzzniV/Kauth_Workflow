import { useCallback, useEffect, useMemo, useState } from "react";
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
import { useWorkflowCreationSubmission } from "./useWorkflowCreationSubmission";
import {
  createInitialWorkflowStartFormState,
  EMPTY_EMPLOYEE,
  getAvailableRoles,
  isWorkflowCreationContextComplete,
  resolveSelectedCompletedOnboarding,
  resolveSelectedDepartment,
  resolveSelectedProcessTypeKey,
  resolveSelectedRoleId,
  type SubmitState,
  type WorkflowCreationStep,
  type WorkflowStartFormState,
} from "./workflowCreationModel";

export type { WorkflowCreationStep } from "./workflowCreationModel";

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

export function useWorkflowCreation(): UseWorkflowCreationResult {
  const [currentStep, setCurrentStep] = useState<WorkflowCreationStep>("process");
  const [formState, setFormState] = useState<WorkflowStartFormState>(createInitialWorkflowStartFormState);
  const [selectedCompletedOnboardingSnapshot, setSelectedCompletedOnboardingSnapshot] =
    useState<CompletedOnboardingSearchResult | null>(null);
  const [debouncedCompletedOnboardingSearch, setDebouncedCompletedOnboardingSearch] = useState("");
  const processTypesQuery = useProcessTypes();
  const processTypes = useMemo(() => processTypesQuery.data ?? [], [processTypesQuery.data]);
  const processTypesLoading = processTypesQuery.isLoading;

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
    return resolveSelectedProcessTypeKey(formState.processTypeKey, processTypes);
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
    () => resolveSelectedDepartment(formState.departmentId, departments),
    [departments, formState.departmentId]
  );

  const selectedDepartmentId = selectedDepartment?.id ?? null;

  const selectedRoleId = useMemo(() => {
    return resolveSelectedRoleId(formState.roleId, selectedDepartmentId, roles);
  }, [formState.roleId, roles, selectedDepartmentId]);

  const selectedRole = useMemo(
    () => roles.find((role) => role.id === selectedRoleId) ?? null,
    [roles, selectedRoleId]
  );

  const availableRoles = useMemo(() => {
    return getAvailableRoles(selectedDepartmentId, roles);
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
    return resolveSelectedCompletedOnboarding(
      completedOnboardings,
      selectedCompletedOnboardingSnapshot
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
  const {
    submitState,
    submitError,
    submitSuccessMessage,
    createdWorkflowUid,
    resetSubmissionState,
    submitWorkflow,
  } = useWorkflowCreationSubmission({
    selectedProcessTypeKey,
    selectedProcessType,
    requiresTargetPerson,
    selectedDepartmentId,
    selectedRoleId,
    selectedCompletedOnboarding,
    employee: formState.employee,
  });

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
    return isWorkflowCreationContextComplete({
      selectedProcessTypeKey,
      requiresTargetPerson,
      selectedCompletedOnboarding,
      completedOnboardingsLoading,
      employee: formState.employee,
      selectedDepartmentId,
      selectedRoleId,
      roles,
      rolesLoading,
      rolesError,
    });
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
