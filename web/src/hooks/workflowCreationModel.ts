import type {
  Department,
  EmployeeFormData,
  Role,
  StartableWorkflowDefinition,
  WorkflowTargetPersonSource,
  WorkflowCreationPayload,
} from "../types/workflow";

export type SubmitState = "idle" | "loading" | "success" | "error";
export type WorkflowCreationStep = "process" | "context" | "review";

export type WorkflowStartFormState = {
  workflowDefinitionKey: string | null;
  employee: EmployeeFormData;
  departmentId: number | null;
  roleId: number | null;
  targetPersonSourceSearch: string;
};

export const EMPTY_EMPLOYEE: EmployeeFormData = {
  firstName: "",
  lastName: "",
  employeeNumber: 0,
  badgeNumber: 0,
  deadlineDate: "",
};

export function createInitialWorkflowStartFormState(): WorkflowStartFormState {
  return {
    workflowDefinitionKey: null,
    employee: EMPTY_EMPLOYEE,
    departmentId: null,
    roleId: null,
    targetPersonSourceSearch: "",
  };
}

export function hasValidEmployeeData(employee: EmployeeFormData): boolean {
  return (
    employee.firstName.trim().length > 0 &&
    employee.lastName.trim().length > 0 &&
    employee.employeeNumber > 0 &&
    employee.badgeNumber > 0
  );
}

export function resolveSelectedWorkflowDefinitionKey(
  workflowDefinitionKey: string | null,
  workflowDefinitions: StartableWorkflowDefinition[]
): string | null {
  if (
    workflowDefinitionKey &&
    workflowDefinitions.some((definition) => definition.definitionKey === workflowDefinitionKey)
  ) {
    return workflowDefinitionKey;
  }

  return workflowDefinitions.length === 1 ? workflowDefinitions[0].definitionKey : null;
}

export function resolveSelectedDepartment(
  departmentId: number | null,
  departments: Department[]
): Department | null {
  if (departmentId === null) {
    return null;
  }

  return departments.find((department) => department.id === departmentId) ?? null;
}

export function resolveSelectedRoleId(
  roleId: number | null,
  selectedDepartmentId: number | null,
  roles: Role[]
): number | null {
  if (roleId === null || selectedDepartmentId === null) {
    return null;
  }

  return (
    roles.find((role) => role.id === roleId && role.departmentId === selectedDepartmentId)?.id ?? null
  );
}

export function getAvailableRoles(selectedDepartmentId: number | null, roles: Role[]): Role[] {
  if (selectedDepartmentId === null) {
    return [];
  }

  return roles.filter((role) => role.departmentId === selectedDepartmentId);
}

export function resolveSelectedTargetPersonSource(
  targetPersonSources: WorkflowTargetPersonSource[],
  selectedTargetPersonSourceSnapshot: WorkflowTargetPersonSource | null
): WorkflowTargetPersonSource | null {
  if (!selectedTargetPersonSourceSnapshot) {
    return null;
  }

  return (
    targetPersonSources.find(
      (result) => result.workflowUid === selectedTargetPersonSourceSnapshot.workflowUid
    ) ?? selectedTargetPersonSourceSnapshot
  );
}

type WorkflowCreationContextCompletionArgs = {
  selectedWorkflowDefinitionKey: string | null;
  requiresTargetPerson: boolean;
  selectedTargetPersonSource: WorkflowTargetPersonSource | null;
  targetPersonSourcesLoading: boolean;
  employee: EmployeeFormData;
  selectedDepartmentId: number | null;
  selectedRoleId: number | null;
  roles: Role[];
  rolesLoading: boolean;
  rolesError: string | null;
};

export function isWorkflowCreationContextComplete({
  selectedWorkflowDefinitionKey,
  requiresTargetPerson,
  selectedTargetPersonSource,
  targetPersonSourcesLoading,
  employee,
  selectedDepartmentId,
  selectedRoleId,
  roles,
  rolesLoading,
  rolesError,
}: WorkflowCreationContextCompletionArgs): boolean {
  if (!selectedWorkflowDefinitionKey) {
    return false;
  }

  if (requiresTargetPerson) {
    return Boolean(
      selectedTargetPersonSource &&
        selectedTargetPersonSource.departmentId &&
        selectedTargetPersonSource.roleId &&
        selectedTargetPersonSource.employeeNumber > 0 &&
        selectedTargetPersonSource.badgeNumber > 0 &&
        !targetPersonSourcesLoading
    );
  }

  const metadataIsValid =
    selectedDepartmentId !== null &&
    selectedRoleId !== null &&
    roles.some((role) => role.id === selectedRoleId && role.departmentId === selectedDepartmentId);

  return hasValidEmployeeData(employee) && !rolesLoading && !rolesError && metadataIsValid;
}

type WorkflowCreationPayloadArgs = {
  selectedWorkflowDefinitionKey: string;
  selectedLegacyProcessTypeKey: string;
  requiresTargetPerson: boolean;
  selectedTargetPersonSource: WorkflowTargetPersonSource | null;
  employee: EmployeeFormData;
  selectedDepartmentId: number | null;
  selectedRoleId: number | null;
};

export function buildWorkflowCreationPayload({
  selectedWorkflowDefinitionKey,
  selectedLegacyProcessTypeKey,
  requiresTargetPerson,
  selectedTargetPersonSource,
  employee,
  selectedDepartmentId,
  selectedRoleId,
}: WorkflowCreationPayloadArgs): WorkflowCreationPayload {
  if (requiresTargetPerson && selectedTargetPersonSource) {
    return {
      workflowDefinitionKey: selectedWorkflowDefinitionKey,
      processTypeKey: selectedLegacyProcessTypeKey,
      targetPersonId: selectedTargetPersonSource.personId,
      sourceWorkflowUid: selectedTargetPersonSource.workflowUid,
      firstName: selectedTargetPersonSource.firstName,
      lastName: selectedTargetPersonSource.lastName,
      employeeNumber: selectedTargetPersonSource.employeeNumber,
      badgeNumber: selectedTargetPersonSource.badgeNumber,
      deadlineDate: employee.deadlineDate.trim() || null,
      departmentId: null,
      roleId: null,
    };
  }

  return {
    workflowDefinitionKey: selectedWorkflowDefinitionKey,
    processTypeKey: selectedLegacyProcessTypeKey,
    firstName: employee.firstName.trim(),
    lastName: employee.lastName.trim(),
    employeeNumber: employee.employeeNumber,
    badgeNumber: employee.badgeNumber,
    deadlineDate: employee.deadlineDate.trim() || null,
    departmentId: selectedDepartmentId,
    roleId: selectedRoleId,
  };
}

type WorkflowCreationSuccessMessageArgs = {
  workflowName: string;
  createdWorkflowUid: string;
  requiresTargetPerson: boolean;
  selectedTargetPersonSource: WorkflowTargetPersonSource | null;
};

export function buildWorkflowCreationSuccessMessage({
  workflowName,
  createdWorkflowUid,
  requiresTargetPerson,
  selectedTargetPersonSource,
}: WorkflowCreationSuccessMessageArgs): string {
  return `${workflowName} ${createdWorkflowUid} angelegt.${
    requiresTargetPerson && selectedTargetPersonSource
      ? " Automatisch mit bestehendem Quellworkflow verknüpft."
      : ""
  } Nächster Schritt: Der zuständige Prozessschritt kann jetzt im Tool weiterbearbeitet werden.`;
}
