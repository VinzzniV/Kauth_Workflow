import { useCallback, useEffect, useMemo, useState } from "react";
import { useDepartments, useRoles } from "../services/queries/roleQueries";
import { useStartableWorkflowDefinitions } from "../services/queries/processTypeQueries";
import {
  useWorkflowTargetPersonSourcesSearch,
  useWorkflowConfig,
} from "../services/queries/workflowQueries";
import type {
  Department,
  EmployeeFormData,
  Role,
  StartableWorkflowDefinition,
  WorkflowTargetPersonSource,
  WorkflowConfig,
} from "../types/workflow";
import { useWorkflowCreationSubmission } from "./useWorkflowCreationSubmission";
import {
  createInitialWorkflowStartFormState,
  EMPTY_EMPLOYEE,
  getAvailableRoles,
  isWorkflowCreationContextComplete,
  resolveSelectedTargetPersonSource,
  resolveSelectedDepartment,
  resolveSelectedWorkflowDefinitionKey,
  resolveSelectedRoleId,
  type SubmitState,
  type WorkflowCreationStep,
  type WorkflowStartFormState,
} from "./workflowCreationModel";

export type { WorkflowCreationStep } from "./workflowCreationModel";

type UseWorkflowCreationResult = {
  currentStep: WorkflowCreationStep;
  workflowDefinitions: StartableWorkflowDefinition[];
  workflowDefinitionsLoading: boolean;
  selectedWorkflowDefinitionKey: string | null;
  selectedWorkflowDefinition: StartableWorkflowDefinition | null;
  requiresTargetPerson: boolean;
  employee: EmployeeFormData;
  selectedDepartmentId: number | null;
  selectedRoleId: number | null;
  selectedDepartment: Department | null;
  selectedRole: Role | null;
  selectedTargetPersonSource: WorkflowTargetPersonSource | null;
  targetPersonSourceSearch: string;
  targetPersonSources: WorkflowTargetPersonSource[];
  targetPersonSourcesLoading: boolean;
  targetPersonSourcesError: string | null;
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
  setWorkflowDefinition: (key: string) => void;
  goToProcessStep: () => void;
  goToContextStep: () => void;
  goToReviewStep: () => void;
  setEmployeeField: (field: keyof EmployeeFormData, value: string | number) => void;
  setSelectedDepartment: (departmentId: number | null) => void;
  setSelectedRole: (roleId: number | null) => void;
  setTargetPersonSourceSearch: (value: string) => void;
  setSelectedTargetPersonSource: (source: WorkflowTargetPersonSource | null) => void;
  submitWorkflow: () => Promise<void>;
  canGoToContextStep: boolean;
  canGoToReviewStep: boolean;
  canSubmit: boolean;
  reloadRoles: () => Promise<void>;
};

export function useWorkflowCreation(): UseWorkflowCreationResult {
  const [currentStep, setCurrentStep] = useState<WorkflowCreationStep>("process");
  const [formState, setFormState] = useState<WorkflowStartFormState>(createInitialWorkflowStartFormState);
  const [selectedTargetPersonSourceSnapshot, setSelectedTargetPersonSourceSnapshot] =
    useState<WorkflowTargetPersonSource | null>(null);
  const [debouncedTargetPersonSourceSearch, setDebouncedTargetPersonSourceSearch] = useState("");
  const workflowDefinitionsQuery = useStartableWorkflowDefinitions();
  const workflowDefinitions = useMemo(
    () => workflowDefinitionsQuery.data ?? [],
    [workflowDefinitionsQuery.data]
  );
  const workflowDefinitionsLoading = workflowDefinitionsQuery.isLoading;

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

  const selectedWorkflowDefinitionKey = useMemo(() => {
    return resolveSelectedWorkflowDefinitionKey(
      formState.workflowDefinitionKey,
      workflowDefinitions
    );
  }, [formState.workflowDefinitionKey, workflowDefinitions]);

  const selectedWorkflowDefinition = useMemo(
    () =>
      workflowDefinitions.find(
        (definition) => definition.definitionKey === selectedWorkflowDefinitionKey
      ) ?? null,
    [workflowDefinitions, selectedWorkflowDefinitionKey]
  );

  const requiresTargetPerson = selectedWorkflowDefinition?.requiresTargetPerson ?? false;
  const targetPersonSourceSearchValue = requiresTargetPerson
    ? formState.targetPersonSourceSearch
    : "";

  useEffect(() => {
    const timeoutHandle = window.setTimeout(() => {
      setDebouncedTargetPersonSourceSearch(targetPersonSourceSearchValue);
    }, 250);

    return () => window.clearTimeout(timeoutHandle);
  }, [targetPersonSourceSearchValue]);

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

  const targetPersonSourcesQuery = useWorkflowTargetPersonSourcesSearch(
    debouncedTargetPersonSourceSearch,
    requiresTargetPerson
  );
  const targetPersonSources = useMemo(
    () => targetPersonSourcesQuery.data ?? [],
    [targetPersonSourcesQuery.data]
  );
  const targetPersonSourcesLoading = requiresTargetPerson && targetPersonSourcesQuery.isFetching;
  const targetPersonSourcesError =
    requiresTargetPerson && targetPersonSourcesQuery.error instanceof Error
      ? targetPersonSourcesQuery.error.message
      : requiresTargetPerson && targetPersonSourcesQuery.error
        ? "Quellworkflows konnten nicht geladen werden."
        : null;

  const selectedTargetPersonSource = useMemo(() => {
    return resolveSelectedTargetPersonSource(
      targetPersonSources,
      selectedTargetPersonSourceSnapshot
    );
  }, [selectedTargetPersonSourceSnapshot, targetPersonSources]);
  const effectiveRoleIdForConfig = requiresTargetPerson
    ? selectedTargetPersonSource?.roleId ?? null
    : selectedRoleId;

  const workflowConfigQuery = useWorkflowConfig(
    effectiveRoleIdForConfig,
    selectedWorkflowDefinition?.primaryLegacyProcessTypeKey ?? null,
    Boolean(selectedWorkflowDefinition?.primaryLegacyProcessTypeKey)
  );
  const workflowConfig = useMemo<WorkflowConfig | null>(
    () =>
      selectedWorkflowDefinition?.primaryLegacyProcessTypeKey
        ? workflowConfigQuery.data ?? null
        : null,
    [selectedWorkflowDefinition?.primaryLegacyProcessTypeKey, workflowConfigQuery.data]
  );
  const workflowConfigLoading =
    Boolean(selectedWorkflowDefinition?.primaryLegacyProcessTypeKey) && workflowConfigQuery.isFetching;
  const workflowConfigError =
    selectedWorkflowDefinition?.primaryLegacyProcessTypeKey && workflowConfigQuery.error instanceof Error
      ? workflowConfigQuery.error.message
      : selectedWorkflowDefinition?.primaryLegacyProcessTypeKey && workflowConfigQuery.error
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
    selectedWorkflowDefinitionKey,
    selectedLegacyProcessTypeKey: selectedWorkflowDefinition?.primaryLegacyProcessTypeKey ?? null,
    selectedWorkflowDefinition,
    requiresTargetPerson,
    selectedDepartmentId,
    selectedRoleId,
    selectedTargetPersonSource,
    employee: formState.employee,
  });

  const setWorkflowDefinition = (key: string) => {
    resetSubmissionState();
    setFormState((previous) => ({
      ...previous,
      workflowDefinitionKey: key,
      departmentId: null,
      roleId: null,
      targetPersonSourceSearch: "",
      employee: {
        ...EMPTY_EMPLOYEE,
        deadlineDate: previous.employee.deadlineDate,
      },
    }));
    setSelectedTargetPersonSourceSnapshot(null);
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

  const setTargetPersonSourceSearch = (value: string) => {
    resetSubmissionState();
    setFormState((previous) => ({
      ...previous,
      targetPersonSourceSearch: value,
    }));
  };

  const setSelectedTargetPersonSource = (source: WorkflowTargetPersonSource | null) => {
    resetSubmissionState();
    setSelectedTargetPersonSourceSnapshot(source);
    setFormState((previous) => ({
      ...previous,
      departmentId: source?.departmentId ?? null,
      roleId: source?.roleId ?? null,
      employee: {
        ...previous.employee,
        firstName: source?.firstName ?? "",
        lastName: source?.lastName ?? "",
        employeeNumber: source?.employeeNumber ?? 0,
        badgeNumber: source?.badgeNumber ?? 0,
      },
    }));
  };

  const isContextComplete = useMemo(() => {
    return isWorkflowCreationContextComplete({
      selectedWorkflowDefinitionKey,
      requiresTargetPerson,
      selectedTargetPersonSource,
      targetPersonSourcesLoading,
      employee: formState.employee,
      selectedDepartmentId,
      selectedRoleId,
      roles,
      rolesLoading,
      rolesError,
    });
  }, [
    formState.employee,
    requiresTargetPerson,
    roles,
    rolesError,
    rolesLoading,
    selectedTargetPersonSource,
    selectedDepartmentId,
    selectedRoleId,
    selectedWorkflowDefinitionKey,
    targetPersonSourcesLoading,
  ]);

  const canGoToContextStep = selectedWorkflowDefinitionKey !== null;
  const canGoToReviewStep = isContextComplete;
  const canSubmit = currentStep === "review" && isContextComplete;

  const goToProcessStep = useCallback(() => {
    resetSubmissionState();
    setCurrentStep("process");
  }, [resetSubmissionState]);

  const goToContextStep = useCallback(() => {
    if (!selectedWorkflowDefinitionKey) {
      return;
    }

    resetSubmissionState();
    setCurrentStep("context");
  }, [resetSubmissionState, selectedWorkflowDefinitionKey]);

  const goToReviewStep = useCallback(() => {
    if (!isContextComplete) {
      return;
    }

    resetSubmissionState();
    setCurrentStep("review");
  }, [isContextComplete, resetSubmissionState]);

  return {
    currentStep,
    workflowDefinitions,
    workflowDefinitionsLoading,
    selectedWorkflowDefinitionKey,
    selectedWorkflowDefinition,
    requiresTargetPerson,
    employee: formState.employee,
    selectedDepartmentId,
    selectedRoleId,
    selectedDepartment,
    selectedRole,
    selectedTargetPersonSource,
    targetPersonSourceSearch: formState.targetPersonSourceSearch,
    targetPersonSources: requiresTargetPerson ? targetPersonSources : [],
    targetPersonSourcesLoading: requiresTargetPerson ? targetPersonSourcesLoading : false,
    targetPersonSourcesError: requiresTargetPerson ? targetPersonSourcesError : null,
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
    setWorkflowDefinition,
    goToProcessStep,
    goToContextStep,
    goToReviewStep,
    setEmployeeField,
    setSelectedDepartment,
    setSelectedRole,
    setTargetPersonSourceSearch,
    setSelectedTargetPersonSource,
    submitWorkflow,
    canGoToContextStep,
    canGoToReviewStep,
    canSubmit,
    reloadRoles,
  };
}
