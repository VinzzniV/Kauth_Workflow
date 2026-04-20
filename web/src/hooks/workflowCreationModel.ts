import type {
  Department,
  EmployeeFormData,
  Role,
  StartableWorkflowDefinition,
  WorkflowCreationPayload,
  WorkflowTargetPerson,
} from "../types/workflow";

export type SubmitState = "idle" | "loading" | "success" | "error";
export type WorkflowCreationStep = "process" | "context" | "review";

export type WorkflowStartFormState = {
  workflowDefinitionKey: string | null;
  employee: EmployeeFormData;
  departmentId: number | null;
  roleId: number | null;
  targetPersonSearch: string;
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
    targetPersonSearch: "",
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

export function hasCompleteTargetPersonContext(person: WorkflowTargetPerson): boolean {
  return Boolean(
    person.departmentId &&
      person.roleId &&
      person.employeeNumber &&
      person.employeeNumber > 0 &&
      person.badgeNumber &&
      person.badgeNumber > 0
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

export function resolveSelectedTargetPerson(
  targetPeople: WorkflowTargetPerson[],
  selectedTargetPersonSnapshot: WorkflowTargetPerson | null
): WorkflowTargetPerson | null {
  if (!selectedTargetPersonSnapshot) {
    return null;
  }

  return (
    targetPeople.find((result) => result.personId === selectedTargetPersonSnapshot.personId)
    ?? selectedTargetPersonSnapshot
  );
}

type WorkflowCreationContextCompletionArgs = {
  selectedWorkflowDefinitionKey: string | null;
  requiresTargetPerson: boolean;
  selectedTargetPerson: WorkflowTargetPerson | null;
  targetPeopleLoading: boolean;
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
  selectedTargetPerson,
  targetPeopleLoading,
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
      selectedTargetPerson &&
        hasCompleteTargetPersonContext(selectedTargetPerson) &&
        !targetPeopleLoading
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
  targetPersonId: number;
  employee: EmployeeFormData;
  selectedDepartmentId: number | null;
  selectedRoleId: number | null;
};

export function buildWorkflowCreationPayload({
  selectedWorkflowDefinitionKey,
  selectedLegacyProcessTypeKey,
  requiresTargetPerson,
  targetPersonId,
  employee,
  selectedDepartmentId,
  selectedRoleId,
}: WorkflowCreationPayloadArgs): WorkflowCreationPayload {
  if (requiresTargetPerson) {
    return {
      workflowDefinitionKey: selectedWorkflowDefinitionKey,
      processTypeKey: selectedLegacyProcessTypeKey,
      targetPersonId,
      deadlineDate: employee.deadlineDate.trim() || null,
    };
  }

  return {
    workflowDefinitionKey: selectedWorkflowDefinitionKey,
    processTypeKey: selectedLegacyProcessTypeKey,
    targetPersonId,
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
  selectedTargetPerson: WorkflowTargetPerson | null;
};

export function buildWorkflowCreationSuccessMessage({
  workflowName,
  createdWorkflowUid,
  requiresTargetPerson,
  selectedTargetPerson,
}: WorkflowCreationSuccessMessageArgs): string {
  const linkageMessage = requiresTargetPerson && selectedTargetPerson
    ? " Bestehende Person wurde direkt verknüpft."
    : " Person-Stammsatz wurde erstellt und direkt verknüpft.";

  return `${workflowName} ${createdWorkflowUid} angelegt.${linkageMessage} Nächster Schritt: Der zuständige Prozessschritt kann jetzt im Tool weiterbearbeitet werden.`;
}
