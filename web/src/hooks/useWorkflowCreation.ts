import { useCallback, useEffect, useMemo, useState } from "react";
import {
  createWorkflow,
  getProcessTypes,
  getWorkflowConfig,
  searchCompletedOnboardings,
} from "../services/lifecycleApi";
import { useRoles } from "./useRoles";
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
  const [processTypes, setProcessTypes] = useState<ProcessType[]>([]);
  const [processTypesLoading, setProcessTypesLoading] = useState(true);
  const [formState, setFormState] = useState<WorkflowStartFormState>({
    processTypeKey: null,
    employee: EMPTY_EMPLOYEE,
    departmentId: null,
    roleId: null,
    completedOnboardingSearch: "",
  });
  const [selectedCompletedOnboarding, setSelectedCompletedOnboardingState] =
    useState<CompletedOnboardingSearchResult | null>(null);
  const [completedOnboardings, setCompletedOnboardings] = useState<CompletedOnboardingSearchResult[]>([]);
  const [completedOnboardingsLoading, setCompletedOnboardingsLoading] = useState(false);
  const [completedOnboardingsError, setCompletedOnboardingsError] = useState<string | null>(null);
  const [debouncedCompletedOnboardingSearch, setDebouncedCompletedOnboardingSearch] = useState("");
  const [workflowConfig, setWorkflowConfig] = useState<WorkflowConfig | null>(null);
  const [workflowConfigLoading, setWorkflowConfigLoading] = useState(false);
  const [workflowConfigError, setWorkflowConfigError] = useState<string | null>(null);
  const [submitState, setSubmitState] = useState<SubmitState>("idle");
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitSuccessMessage, setSubmitSuccessMessage] = useState<string | null>(null);
  const [createdWorkflowUid, setCreatedWorkflowUid] = useState<string | null>(null);

  const resetSubmissionState = useCallback(() => {
    setSubmitError(null);
    setSubmitSuccessMessage(null);
    setCreatedWorkflowUid(null);
    setSubmitState((previous) => (previous === "loading" ? previous : "idle"));
  }, []);

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

  useEffect(() => {
    if (!requiresTargetPerson) {
      setDebouncedCompletedOnboardingSearch("");
      return;
    }

    const timeoutHandle = window.setTimeout(() => {
      setDebouncedCompletedOnboardingSearch(formState.completedOnboardingSearch);
    }, 250);

    return () => window.clearTimeout(timeoutHandle);
  }, [formState.completedOnboardingSearch, requiresTargetPerson]);

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
    if (!requiresTargetPerson) {
      setCompletedOnboardings([]);
      setCompletedOnboardingsError(null);
      return;
    }

    let cancelled = false;
    setCompletedOnboardingsLoading(true);
    setCompletedOnboardingsError(null);

    searchCompletedOnboardings(debouncedCompletedOnboardingSearch)
      .then((results) => {
        if (cancelled) {
          return;
        }

        setCompletedOnboardings(results);
        setSelectedCompletedOnboardingState((previous) => {
          if (!previous) {
            return previous;
          }

          const refreshed = results.find((result) => result.workflowUid === previous.workflowUid);
          return refreshed ?? previous;
        });
      })
      .catch((err) => {
        if (cancelled) {
          return;
        }

        setCompletedOnboardings([]);
        setCompletedOnboardingsError(
          err instanceof Error ? err.message : "Abgeschlossene Onboardings konnten nicht geladen werden."
        );
      })
      .finally(() => {
        if (!cancelled) {
          setCompletedOnboardingsLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [debouncedCompletedOnboardingSearch, requiresTargetPerson]);

  const effectiveRoleIdForConfig = requiresTargetPerson
    ? selectedCompletedOnboarding?.roleId ?? null
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
          setWorkflowConfigError(
            err instanceof Error ? err.message : "Workflow-Konfiguration konnte nicht geladen werden."
          );
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
    setSelectedCompletedOnboardingState(null);
    setCompletedOnboardings([]);
    setCompletedOnboardingsError(null);
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
    setSelectedCompletedOnboardingState(onboarding);
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
    if (!formState.processTypeKey) {
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
    formState.processTypeKey,
    requiresTargetPerson,
    roles,
    rolesError,
    rolesLoading,
    selectedCompletedOnboarding,
    selectedDepartmentId,
    selectedRoleId,
  ]);

  const canGoToContextStep = formState.processTypeKey !== null;
  const canGoToReviewStep = isContextComplete;
  const canSubmit = currentStep === "review" && isContextComplete;

  const goToProcessStep = useCallback(() => {
    resetSubmissionState();
    setCurrentStep("process");
  }, [resetSubmissionState]);

  const goToContextStep = useCallback(() => {
    if (!formState.processTypeKey) {
      return;
    }

    resetSubmissionState();
    setCurrentStep("context");
  }, [formState.processTypeKey, resetSubmissionState]);

  const goToReviewStep = useCallback(() => {
    if (!isContextComplete) {
      return;
    }

    resetSubmissionState();
    setCurrentStep("review");
  }, [isContextComplete, resetSubmissionState]);

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

    if (requiresTargetPerson && !selectedCompletedOnboarding) {
      setSubmitState("error");
      setSubmitError("Bitte zuerst ein abgeschlossenes Onboarding auswählen.");
      return;
    }

    setSubmitState("loading");
    setSubmitError(null);
    setSubmitSuccessMessage(null);
    setCreatedWorkflowUid(null);

    const processTypeName = selectedProcessType?.name ?? formState.processTypeKey;

    const payload =
      requiresTargetPerson && selectedCompletedOnboarding
        ? {
            processTypeKey: formState.processTypeKey,
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
            processTypeKey: formState.processTypeKey,
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
    selectedProcessTypeKey: formState.processTypeKey,
    selectedProcessType,
    requiresTargetPerson,
    employee: formState.employee,
    selectedDepartmentId,
    selectedRoleId,
    selectedDepartment,
    selectedRole,
    selectedCompletedOnboarding,
    completedOnboardingSearch: formState.completedOnboardingSearch,
    completedOnboardings,
    completedOnboardingsLoading,
    completedOnboardingsError,
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
