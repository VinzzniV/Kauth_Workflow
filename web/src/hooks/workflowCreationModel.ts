import type {
  CompletedOnboardingSearchResult,
  Department,
  EmployeeFormData,
  ProcessType,
  Role,
  WorkflowCreationPayload,
} from "../types/workflow";

export type SubmitState = "idle" | "loading" | "success" | "error";
export type WorkflowCreationStep = "process" | "context" | "review";

export type WorkflowStartFormState = {
  processTypeKey: string | null;
  employee: EmployeeFormData;
  departmentId: number | null;
  roleId: number | null;
  completedOnboardingSearch: string;
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
    processTypeKey: null,
    employee: EMPTY_EMPLOYEE,
    departmentId: null,
    roleId: null,
    completedOnboardingSearch: "",
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

export function resolveSelectedProcessTypeKey(
  processTypeKey: string | null,
  processTypes: ProcessType[]
): string | null {
  if (processTypeKey && processTypes.some((type) => type.key === processTypeKey)) {
    return processTypeKey;
  }

  return processTypes.length === 1 ? processTypes[0].key : null;
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

export function resolveSelectedCompletedOnboarding(
  completedOnboardings: CompletedOnboardingSearchResult[],
  selectedCompletedOnboardingSnapshot: CompletedOnboardingSearchResult | null
): CompletedOnboardingSearchResult | null {
  if (!selectedCompletedOnboardingSnapshot) {
    return null;
  }

  return (
    completedOnboardings.find(
      (result) => result.workflowUid === selectedCompletedOnboardingSnapshot.workflowUid
    ) ?? selectedCompletedOnboardingSnapshot
  );
}

type WorkflowCreationContextCompletionArgs = {
  selectedProcessTypeKey: string | null;
  requiresTargetPerson: boolean;
  selectedCompletedOnboarding: CompletedOnboardingSearchResult | null;
  completedOnboardingsLoading: boolean;
  employee: EmployeeFormData;
  selectedDepartmentId: number | null;
  selectedRoleId: number | null;
  roles: Role[];
  rolesLoading: boolean;
  rolesError: string | null;
};

export function isWorkflowCreationContextComplete({
  selectedProcessTypeKey,
  requiresTargetPerson,
  selectedCompletedOnboarding,
  completedOnboardingsLoading,
  employee,
  selectedDepartmentId,
  selectedRoleId,
  roles,
  rolesLoading,
  rolesError,
}: WorkflowCreationContextCompletionArgs): boolean {
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

  return hasValidEmployeeData(employee) && !rolesLoading && !rolesError && metadataIsValid;
}

type WorkflowCreationPayloadArgs = {
  selectedProcessTypeKey: string;
  requiresTargetPerson: boolean;
  selectedCompletedOnboarding: CompletedOnboardingSearchResult | null;
  employee: EmployeeFormData;
  selectedDepartmentId: number | null;
  selectedRoleId: number | null;
};

export function buildWorkflowCreationPayload({
  selectedProcessTypeKey,
  requiresTargetPerson,
  selectedCompletedOnboarding,
  employee,
  selectedDepartmentId,
  selectedRoleId,
}: WorkflowCreationPayloadArgs): WorkflowCreationPayload {
  if (requiresTargetPerson && selectedCompletedOnboarding) {
    return {
      processTypeKey: selectedProcessTypeKey,
      targetPersonId: selectedCompletedOnboarding.personId,
      sourceWorkflowUid: selectedCompletedOnboarding.workflowUid,
      firstName: selectedCompletedOnboarding.firstName,
      lastName: selectedCompletedOnboarding.lastName,
      employeeNumber: selectedCompletedOnboarding.employeeNumber,
      badgeNumber: selectedCompletedOnboarding.badgeNumber,
      deadlineDate: employee.deadlineDate.trim() || null,
      departmentId: null,
      roleId: null,
    };
  }

  return {
    processTypeKey: selectedProcessTypeKey,
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
  processTypeName: string;
  createdWorkflowUid: string;
  requiresTargetPerson: boolean;
  selectedCompletedOnboarding: CompletedOnboardingSearchResult | null;
};

export function buildWorkflowCreationSuccessMessage({
  processTypeName,
  createdWorkflowUid,
  requiresTargetPerson,
  selectedCompletedOnboarding,
}: WorkflowCreationSuccessMessageArgs): string {
  return `${processTypeName} ${createdWorkflowUid} angelegt.${
    requiresTargetPerson && selectedCompletedOnboarding
      ? " Automatisch mit abgeschlossenem Onboarding verknüpft."
      : ""
  } Nächster Schritt: Der zuständige Prozessschritt kann jetzt im Tool weiterbearbeitet werden.`;
}
