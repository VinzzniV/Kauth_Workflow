import type {
  EmployeeFormData,
  Role,
  StartableWorkflowDefinition,
  WorkflowConfig,
  WorkflowTargetPerson,
} from "../types/workflow";
import type { WorkflowCreationStep } from "../hooks/useWorkflowCreation";
import { hasCompleteTargetPersonContext } from "../hooks/workflowCreationModel";

export type StepDefinition = {
  key: WorkflowCreationStep;
  title: string;
};

type CreateWorkflowPageViewModelArgs = {
  currentStep: WorkflowCreationStep;
  capabilities: { hasHrRole: boolean; hasAdminRole: boolean };
  selectedWorkflowDefinition: StartableWorkflowDefinition | null;
  selectedWorkflowDefinitionKey: string | null;
  workflowDefinitions: StartableWorkflowDefinition[];
  requiresTargetPerson: boolean;
  employee: EmployeeFormData;
  selectedDepartmentId: number | null;
  selectedRoleId: number | null;
  selectedTargetPerson: WorkflowTargetPerson | null;
  targetPeopleLoading: boolean;
  targetPeopleError: string | null;
  rolesLoading: boolean;
  rolesError: string | null;
  availableRoles: Role[];
  workflowConfig: WorkflowConfig | null;
};

export function buildCreateWorkflowPageViewModel({
  currentStep,
  capabilities,
  selectedWorkflowDefinition,
  selectedWorkflowDefinitionKey,
  workflowDefinitions,
  requiresTargetPerson,
  employee,
  selectedDepartmentId,
  selectedRoleId,
  selectedTargetPerson,
  targetPeopleLoading,
  targetPeopleError,
  rolesLoading,
  rolesError,
  availableRoles,
  workflowConfig,
}: CreateWorkflowPageViewModelArgs) {
  const isHrEntry = capabilities.hasHrRole || capabilities.hasAdminRole;
  const pageTitle = isHrEntry ? "Neuer Workflow" : "Änderung starten";
  const contextStepTitle = requiresTargetPerson ? "Bestehende Person wählen" : "Neue Person erfassen";
  const reviewPersonLabel = requiresTargetPerson ? "Zielperson" : "Neue Person";
  const reviewDepartmentLabel = requiresTargetPerson ? "Stamm-Abteilung" : "Abteilung";
  const reviewRoleLabel = requiresTargetPerson ? "Aktuelle Stelle" : "Stelle";
  const roleRecommendationCount =
    (workflowConfig?.roleRecommendations.defaultValues.length ?? 0) +
    (workflowConfig?.roleRecommendations.defaultSelectedOptions.length ?? 0);
  const hasDerivedContextGap = Boolean(
    requiresTargetPerson &&
      selectedTargetPerson &&
      !hasCompleteTargetPersonContext(selectedTargetPerson)
  );
  const processStepIssues = selectedWorkflowDefinition ? [] : ["Bitte einen Workflow wählen."];
  const employeeFieldErrors = requiresTargetPerson
    ? {}
    : {
      firstName: employee.firstName.trim() ? undefined : "Vorname ist erforderlich.",
      lastName: employee.lastName.trim() ? undefined : "Nachname ist erforderlich.",
      employeeNumber: employee.employeeNumber > 0 ? undefined : "Positive Personalnummer eingeben.",
      badgeNumber: employee.badgeNumber > 0 ? undefined : "Positive Kartennummer eingeben.",
    };
  const departmentError = !requiresTargetPerson && selectedDepartmentId === null ? "Bitte eine Abteilung auswählen." : null;
  const roleError =
    !requiresTargetPerson && selectedDepartmentId !== null && selectedRoleId === null
      ? !rolesLoading && !rolesError && availableRoles.length === 0
        ? "In der gewählten Abteilung ist keine aktive Stelle hinterlegt."
        : "Bitte eine Stelle auswählen."
      : null;
  const targetPersonSelectionError =
    requiresTargetPerson && !selectedTargetPerson && !targetPeopleLoading
      ? "Bitte eine bestehende Person auswählen."
      : null;
  const contextStepIssues = requiresTargetPerson
    ? [
      ...(targetPeopleError ? ["Die Personensuche ist fehlgeschlagen."] : []),
      ...(targetPersonSelectionError ? [targetPersonSelectionError] : []),
      ...(hasDerivedContextGap
        ? ["Für die gewählte Person fehlen vollständige Angaben zu Abteilung, Stelle, Personalnummer oder Kartennummer."]
        : []),
    ]
    : [
      ...(rolesError ? ["Stellen und Abteilungen konnten nicht geladen werden."] : []),
      ...Object.values(employeeFieldErrors).filter((value): value is string => Boolean(value)),
      ...(departmentError ? [departmentError] : []),
      ...(roleError ? [roleError] : []),
    ];

  const steps: StepDefinition[] = [
    { key: "process", title: "Workflow wählen" },
    { key: "context", title: contextStepTitle },
    { key: "review", title: "Prüfen und anlegen" },
  ];

  return {
    pageTitle,
    contextStepTitle,
    reviewPersonLabel,
    reviewDepartmentLabel,
    reviewRoleLabel,
    roleRecommendationCount,
    hasDerivedContextGap,
    processStepIssues,
    employeeFieldErrors,
    departmentError,
    roleError,
    targetPersonSelectionError,
    contextStepIssues,
    steps,
    currentStepIndex: steps.findIndex((step) => step.key === currentStep),
    hasWorkflowDefinitions: workflowDefinitions.length > 0,
    isWorkflowSelected: Boolean(selectedWorkflowDefinitionKey),
  };
}
