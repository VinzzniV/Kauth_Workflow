import { useCallback, useEffect, useMemo, useState } from "react";
import { useDepartments, useRoles } from "../services/queries/roleQueries";
import { useStartableWorkflowDefinitions } from "../services/queries/processTypeQueries";
import { usePeopleSearch } from "../services/queries/peopleQueries";
import { useWorkflowConfig } from "../services/queries/workflowQueries";
import type {
  Department,
  EmployeeFormData,
  Role,
  StartableWorkflowDefinition,
  WorkflowConfig,
  WorkflowTargetPerson,
} from "../types/workflow";
import { useWorkflowCreationSubmission } from "./useWorkflowCreationSubmission";
import {
  createInitialWorkflowStartFormState,
  EMPTY_EMPLOYEE,
  getAvailableRoles,
  isWorkflowCreationContextComplete,
  resolveSelectedDepartment,
  resolveSelectedRoleId,
  resolveSelectedTargetPerson,
  resolveSelectedWorkflowDefinitionKey,
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
  selectedTargetPerson: WorkflowTargetPerson | null;
  targetPersonSearch: string;
  targetPeople: WorkflowTargetPerson[];
  targetPeopleLoading: boolean;
  targetPeopleError: string | null;
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
  setTargetPersonSearch: (value: string) => void;
  setSelectedTargetPerson: (person: WorkflowTargetPerson | null) => void;
  submitWorkflow: () => Promise<void>;
  canGoToContextStep: boolean;
  canGoToReviewStep: boolean;
  canSubmit: boolean;
  reloadRoles: () => Promise<void>;
};

export function useWorkflowCreation(): UseWorkflowCreationResult {
  const [currentStep, setCurrentStep] = useState<WorkflowCreationStep>("process");
  const [formState, setFormState] = useState<WorkflowStartFormState>(createInitialWorkflowStartFormState);
  const [selectedTargetPersonSnapshot, setSelectedTargetPersonSnapshot] =
    useState<WorkflowTargetPerson | null>(null);
  const [debouncedTargetPersonSearch, setDebouncedTargetPersonSearch] = useState("");
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
  const targetPersonSearchValue = requiresTargetPerson
    ? formState.targetPersonSearch
    : "";

  useEffect(() => {
    const timeoutHandle = window.setTimeout(() => {
      setDebouncedTargetPersonSearch(targetPersonSearchValue);
    }, 250);

    return () => window.clearTimeout(timeoutHandle);
  }, [targetPersonSearchValue]);

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

  const targetPeopleQuery = usePeopleSearch(
    debouncedTargetPersonSearch,
    requiresTargetPerson
  );
  const targetPeople = useMemo(
    () => targetPeopleQuery.data ?? [],
    [targetPeopleQuery.data]
  );
  const targetPeopleLoading = requiresTargetPerson && targetPeopleQuery.isFetching;
  const targetPeopleError =
    requiresTargetPerson && targetPeopleQuery.error instanceof Error
      ? targetPeopleQuery.error.message
      : requiresTargetPerson && targetPeopleQuery.error
        ? "Personen konnten nicht geladen werden."
        : null;

  const selectedTargetPerson = useMemo(() => {
    return resolveSelectedTargetPerson(
      targetPeople,
      selectedTargetPersonSnapshot
    );
  }, [selectedTargetPersonSnapshot, targetPeople]);
  const effectiveRoleIdForConfig = requiresTargetPerson
    ? selectedTargetPerson?.roleId ?? null
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
    selectedTargetPerson,
    employee: formState.employee,
  });

  const setWorkflowDefinition = (key: string) => {
    resetSubmissionState();
    setFormState((previous) => ({
      ...previous,
      workflowDefinitionKey: key,
      departmentId: null,
      roleId: null,
      targetPersonSearch: "",
      employee: {
        ...EMPTY_EMPLOYEE,
        deadlineDate: previous.employee.deadlineDate,
      },
    }));
    setSelectedTargetPersonSnapshot(null);
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

  const setTargetPersonSearch = (value: string) => {
    resetSubmissionState();
    setFormState((previous) => ({
      ...previous,
      targetPersonSearch: value,
    }));
  };

  const setSelectedTargetPerson = (person: WorkflowTargetPerson | null) => {
    resetSubmissionState();
    setSelectedTargetPersonSnapshot(person);
    setFormState((previous) => ({
      ...previous,
      departmentId: person?.departmentId ?? null,
      roleId: person?.roleId ?? null,
      employee: {
        ...previous.employee,
        firstName: person?.firstName ?? "",
        lastName: person?.lastName ?? "",
        employeeNumber: person?.employeeNumber ?? 0,
        badgeNumber: person?.badgeNumber ?? 0,
      },
    }));
  };

  const isContextComplete = useMemo(() => {
    return isWorkflowCreationContextComplete({
      selectedWorkflowDefinitionKey,
      requiresTargetPerson,
      selectedTargetPerson,
      targetPeopleLoading,
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
    selectedDepartmentId,
    selectedRoleId,
    selectedTargetPerson,
    selectedWorkflowDefinitionKey,
    targetPeopleLoading,
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
    selectedTargetPerson,
    targetPersonSearch: formState.targetPersonSearch,
    targetPeople: requiresTargetPerson ? targetPeople : [],
    targetPeopleLoading: requiresTargetPerson ? targetPeopleLoading : false,
    targetPeopleError: requiresTargetPerson ? targetPeopleError : null,
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
    setTargetPersonSearch,
    setSelectedTargetPerson,
    submitWorkflow,
    canGoToContextStep,
    canGoToReviewStep,
    canSubmit,
    reloadRoles,
  };
}
