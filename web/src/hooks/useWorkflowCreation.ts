import { useMemo, useState } from "react";
import { createWorkflow } from "../services/onboardingApi";
import { useRoles } from "./useRoles";
import type { Department, EmployeeFormData, Role } from "../types/workflow";

type SubmitState = "idle" | "loading" | "success" | "error";

type WorkflowStartFormState = {
  employee: EmployeeFormData;
  departmentId: number | null;
  roleId: number | null;
};

const EMPTY_EMPLOYEE: EmployeeFormData = {
  firstName: "",
  lastName: "",
  employeeNumber: 0,
  badgeNumber: 0,
};

type UseWorkflowCreationResult = {
  employee: EmployeeFormData;
  selectedDepartmentId: number | null;
  selectedRoleId: number | null;
  selectedDepartment: Department | null;
  selectedRole: Role | null;
  availableRoles: Role[];
  roles: Role[];
  departments: Department[];
  rolesLoading: boolean;
  rolesError: string | null;
  submitState: SubmitState;
  submitError: string | null;
  submitSuccessMessage: string | null;
  createdWorkflowUid: string | null;
  setEmployeeField: (field: keyof EmployeeFormData, value: string | number) => void;
  setSelectedDepartment: (departmentId: number | null) => void;
  setSelectedRole: (roleId: number | null) => void;
  submitWorkflow: () => Promise<void>;
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
  const [formState, setFormState] = useState<WorkflowStartFormState>({
    employee: EMPTY_EMPLOYEE,
    departmentId: null,
    roleId: null,
  });
  const [submitState, setSubmitState] = useState<SubmitState>("idle");
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitSuccessMessage, setSubmitSuccessMessage] = useState<string | null>(null);
  const [createdWorkflowUid, setCreatedWorkflowUid] = useState<string | null>(null);

  const {
    roles,
    departments,
    isLoading: rolesLoading,
    error: rolesError,
    reload: reloadRoles,
  } = useRoles();

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

  const setEmployeeField = (field: keyof EmployeeFormData, value: string | number) => {
    setFormState((previous) => ({
      ...previous,
      employee: {
        ...previous.employee,
        [field]: typeof value === "string" ? value : Number(value),
      },
    }));
  };

  const setSelectedDepartment = (departmentId: number | null) => {
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

  const canSubmit = useMemo(() => {
    const metadataIsValid =
      selectedDepartmentId !== null &&
      selectedRoleId !== null &&
      roles.some((role) => role.id === selectedRoleId && role.departmentId === selectedDepartmentId);

    return hasValidEmployeeData(formState.employee) && !rolesLoading && !rolesError && metadataIsValid;
  }, [formState.employee, roles, rolesError, rolesLoading, selectedDepartmentId, selectedRoleId]);

  const submitWorkflow = async () => {
    if (selectedDepartmentId === null || selectedRoleId === null) {
      setSubmitState("error");
      setSubmitError("Bitte zuerst Abteilung und Stelle auswählen.");
      return;
    }

    setSubmitState("loading");
    setSubmitError(null);
    setSubmitSuccessMessage(null);
    setCreatedWorkflowUid(null);

    const payload = {
      firstName: formState.employee.firstName.trim(),
      lastName: formState.employee.lastName.trim(),
      employeeNumber: formState.employee.employeeNumber,
      badgeNumber: formState.employee.badgeNumber,
      departmentId: selectedDepartmentId,
      roleId: selectedRoleId,
    };

    try {
      const response = await createWorkflow(payload);
      setSubmitState("success");
      setCreatedWorkflowUid(response.uid);
      setSubmitSuccessMessage(
        `Onboarding ${response.uid} angelegt. Nächster Schritt: Die Abteilungsleitung wählt die Anforderungen direkt im Tool aus.`
      );
    } catch (err) {
      const message = err instanceof Error ? err.message : "Onboarding konnte nicht gestartet werden.";
      setSubmitState("error");
      setSubmitError(message);
    }
  };

  return {
    employee: formState.employee,
    selectedDepartmentId,
    selectedRoleId,
    selectedDepartment,
    selectedRole,
    availableRoles,
    roles,
    departments,
    rolesLoading,
    rolesError,
    submitState,
    submitError,
    submitSuccessMessage,
    createdWorkflowUid,
    setEmployeeField,
    setSelectedDepartment,
    setSelectedRole,
    submitWorkflow,
    canSubmit,
    reloadRoles,
  };
}
