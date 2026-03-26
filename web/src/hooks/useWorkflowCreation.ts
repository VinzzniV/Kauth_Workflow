import { useEffect, useMemo, useState } from "react";
import {
  createWorkflow,
  findLinkableWorkflows,
  getProcessTypes,
  getWorkflowConfig,
  searchWorkflowTargetPeople,
} from "../services/lifecycleApi";
import { useRoles } from "./useRoles";
import type {
  Department,
  EmployeeFormData,
  LinkableWorkflow,
  ProcessType,
  Role,
  WorkflowConfig,
  WorkflowTargetPerson,
} from "../types/workflow";

type SubmitState = "idle" | "loading" | "success" | "error";

type WorkflowStartFormState = {
  processTypeKey: string | null;
  employee: EmployeeFormData;
  departmentId: number | null;
  roleId: number | null;
  targetPersonSearch: string;
};

const EMPTY_EMPLOYEE: EmployeeFormData = {
  firstName: "",
  lastName: "",
  employeeNumber: 0,
  badgeNumber: 0,
  deadlineDate: "",
};

type UseWorkflowCreationResult = {
  processTypes: ProcessType[];
  processTypesLoading: boolean;
  selectedProcessTypeKey: string | null;
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
  linkableWorkflows: LinkableWorkflow[];
  selectedSourceWorkflowUid: string | null;
  setProcessType: (key: string) => void;
  setEmployeeField: (field: keyof EmployeeFormData, value: string | number) => void;
  setSelectedDepartment: (departmentId: number | null) => void;
  setSelectedRole: (roleId: number | null) => void;
  setTargetPersonSearch: (value: string) => void;
  setSelectedTargetPerson: (person: WorkflowTargetPerson | null) => void;
  setSelectedSourceWorkflow: (uid: string | null) => void;
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
  const [processTypes, setProcessTypes] = useState<ProcessType[]>([]);
  const [processTypesLoading, setProcessTypesLoading] = useState(true);
  const [formState, setFormState] = useState<WorkflowStartFormState>({
    processTypeKey: null,
    employee: EMPTY_EMPLOYEE,
    departmentId: null,
    roleId: null,
    targetPersonSearch: "",
  });
  const [selectedTargetPerson, setSelectedTargetPersonState] = useState<WorkflowTargetPerson | null>(null);
  const [targetPeople, setTargetPeople] = useState<WorkflowTargetPerson[]>([]);
  const [targetPeopleLoading, setTargetPeopleLoading] = useState(false);
  const [targetPeopleError, setTargetPeopleError] = useState<string | null>(null);
  const [workflowConfig, setWorkflowConfig] = useState<WorkflowConfig | null>(null);
  const [workflowConfigLoading, setWorkflowConfigLoading] = useState(false);
  const [workflowConfigError, setWorkflowConfigError] = useState<string | null>(null);
  const [submitState, setSubmitState] = useState<SubmitState>("idle");
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitSuccessMessage, setSubmitSuccessMessage] = useState<string | null>(null);
  const [createdWorkflowUid, setCreatedWorkflowUid] = useState<string | null>(null);
  const [linkableWorkflows, setLinkableWorkflows] = useState<LinkableWorkflow[]>([]);
  const [selectedSourceWorkflowUid, setSelectedSourceWorkflowUid] = useState<string | null>(null);

  useEffect(() => {
    getProcessTypes()
      .then((types) => {
        setProcessTypes(types);
        if (types.length === 1) {
          setFormState((previous) => ({ ...previous, processTypeKey: types[0].key }));
        }
      })
      .catch(() => {
        setFormState((previous) => ({ ...previous, processTypeKey: null }));
      })
      .finally(() => setProcessTypesLoading(false));
  }, []);

  const {
    roles,
    departments,
    isLoading: rolesLoading,
    error: rolesError,
    reload: reloadRoles,
  } = useRoles();

  const selectedProcessType = useMemo(
    () => processTypes.find((pt) => pt.key === formState.processTypeKey) ?? null,
    [processTypes, formState.processTypeKey]
  );

  const requiresTargetPerson = selectedProcessType?.requiresTargetPerson ?? false;

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

  useEffect(() => {
    const employeeNumber = requiresTargetPerson
      ? selectedTargetPerson?.employeeNumber ?? 0
      : formState.employee.employeeNumber;
    const processTypeKey = formState.processTypeKey;
    if (!processTypeKey || !requiresTargetPerson || employeeNumber <= 0) {
      setLinkableWorkflows([]);
      setSelectedSourceWorkflowUid(null);
      return;
    }

    let cancelled = false;
    findLinkableWorkflows(employeeNumber)
      .then((workflows) => {
        if (cancelled) {
          return;
        }

        setLinkableWorkflows(workflows);
        if (workflows.length > 0) {
          setSelectedSourceWorkflowUid(workflows[0].uid);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setLinkableWorkflows([]);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [formState.employee.employeeNumber, formState.processTypeKey, requiresTargetPerson, selectedTargetPerson?.employeeNumber]);

  useEffect(() => {
    if (!requiresTargetPerson) {
      setTargetPeople([]);
      setTargetPeopleError(null);
      return;
    }

    let cancelled = false;
    setTargetPeopleLoading(true);
    setTargetPeopleError(null);

    searchWorkflowTargetPeople(formState.targetPersonSearch)
      .then((people) => {
        if (cancelled) {
          return;
        }

        setTargetPeople(people);
        if (selectedTargetPerson) {
          const refreshed = people.find((person) => person.personId === selectedTargetPerson.personId);
          if (refreshed) {
            setSelectedTargetPersonState(refreshed);
          }
        }
      })
      .catch((err) => {
        if (cancelled) {
          return;
        }

        setTargetPeople([]);
        setTargetPeopleError(err instanceof Error ? err.message : "Zielpersonen konnten nicht geladen werden.");
      })
      .finally(() => {
        if (!cancelled) {
          setTargetPeopleLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [formState.targetPersonSearch, requiresTargetPerson, selectedTargetPerson]);

  const effectiveRoleIdForConfig = requiresTargetPerson
    ? selectedTargetPerson?.roleId ?? null
    : selectedRoleId;

  useEffect(() => {
    if (!formState.processTypeKey) {
      setWorkflowConfig(null);
      setWorkflowConfigError(null);
      return;
    }

    let cancelled = false;
    setWorkflowConfigLoading(true);
    setWorkflowConfigError(null);

    getWorkflowConfig(effectiveRoleIdForConfig, formState.processTypeKey)
      .then((config) => {
        if (!cancelled) {
          setWorkflowConfig(config);
        }
      })
      .catch((err) => {
        if (!cancelled) {
          setWorkflowConfig(null);
          setWorkflowConfigError(err instanceof Error ? err.message : "Workflow-Konfiguration konnte nicht geladen werden.");
        }
      })
      .finally(() => {
        if (!cancelled) {
          setWorkflowConfigLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [effectiveRoleIdForConfig, formState.processTypeKey]);

  const setProcessType = (key: string) => {
    setFormState((previous) => ({
      ...previous,
      processTypeKey: key,
      departmentId: null,
      roleId: null,
      targetPersonSearch: "",
      employee: {
        ...EMPTY_EMPLOYEE,
        deadlineDate: previous.employee.deadlineDate,
      },
    }));
    setSelectedTargetPersonState(null);
    setLinkableWorkflows([]);
    setSelectedSourceWorkflowUid(null);
  };

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

  const setTargetPersonSearch = (value: string) => {
    setFormState((previous) => ({
      ...previous,
      targetPersonSearch: value,
    }));
  };

  const setSelectedTargetPerson = (person: WorkflowTargetPerson | null) => {
    setSelectedTargetPersonState(person);
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

  const canSubmit = useMemo(() => {
    if (!formState.processTypeKey) {
      return false;
    }

    if (requiresTargetPerson) {
      return Boolean(
        selectedTargetPerson &&
        selectedTargetPerson.departmentId &&
        selectedTargetPerson.roleId &&
        selectedTargetPerson.employeeNumber &&
        selectedTargetPerson.badgeNumber &&
        !targetPeopleLoading
      );
    }

    const metadataIsValid =
      selectedDepartmentId !== null &&
      selectedRoleId !== null &&
      roles.some((role) => role.id === selectedRoleId && role.departmentId === selectedDepartmentId);

    return hasValidEmployeeData(formState.employee) && !rolesLoading && !rolesError && metadataIsValid;
  }, [
    formState.employee,
    formState.processTypeKey,
    requiresTargetPerson,
    roles,
    rolesError,
    rolesLoading,
    selectedDepartmentId,
    selectedRoleId,
    selectedTargetPerson,
    targetPeopleLoading,
  ]);

  const submitWorkflow = async () => {
    if (!formState.processTypeKey) {
      setSubmitState("error");
      setSubmitError("Bitte zuerst einen Prozesstyp wählen.");
      return;
    }

    if (!requiresTargetPerson && (selectedDepartmentId === null || selectedRoleId === null)) {
      setSubmitState("error");
      setSubmitError("Bitte zuerst Abteilung und Stelle auswählen.");
      return;
    }

    if (requiresTargetPerson && !selectedTargetPerson) {
      setSubmitState("error");
      setSubmitError("Bitte zuerst eine bestehende Zielperson auswählen.");
      return;
    }

    setSubmitState("loading");
    setSubmitError(null);
    setSubmitSuccessMessage(null);
    setCreatedWorkflowUid(null);

    const processTypeName = selectedProcessType?.name ?? formState.processTypeKey;

    const payload =
      requiresTargetPerson && selectedTargetPerson
        ? {
            processTypeKey: formState.processTypeKey,
            targetPersonId: selectedTargetPerson.personId,
            sourceWorkflowUid: selectedSourceWorkflowUid,
            firstName: selectedTargetPerson.firstName,
            lastName: selectedTargetPerson.lastName,
            employeeNumber: selectedTargetPerson.employeeNumber,
            badgeNumber: selectedTargetPerson.badgeNumber,
            deadlineDate: formState.employee.deadlineDate.trim() || null,
            departmentId: null,
            roleId: null,
          }
        : {
            processTypeKey: formState.processTypeKey,
            sourceWorkflowUid: selectedSourceWorkflowUid,
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
        `${processTypeName} ${response.uid} angelegt.${selectedSourceWorkflowUid ? " Automatisch mit Quell-Vorgang verknüpft." : ""} Nächster Schritt: Der zuständige Prozessschritt kann jetzt im Tool weiterbearbeitet werden.`
      );
    } catch (err) {
      const message = err instanceof Error ? err.message : "Vorgang konnte nicht gestartet werden.";
      setSubmitState("error");
      setSubmitError(message);
    }
  };

  return {
    processTypes,
    processTypesLoading,
    selectedProcessTypeKey: formState.processTypeKey,
    employee: formState.employee,
    selectedDepartmentId,
    selectedRoleId,
    selectedDepartment,
    selectedRole,
    selectedTargetPerson,
    targetPersonSearch: formState.targetPersonSearch,
    targetPeople,
    targetPeopleLoading,
    targetPeopleError,
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
    linkableWorkflows,
    selectedSourceWorkflowUid,
    setProcessType,
    setEmployeeField,
    setSelectedDepartment,
    setSelectedRole,
    setTargetPersonSearch,
    setSelectedTargetPerson,
    setSelectedSourceWorkflow: setSelectedSourceWorkflowUid,
    submitWorkflow,
    canSubmit,
    reloadRoles,
  };
}
