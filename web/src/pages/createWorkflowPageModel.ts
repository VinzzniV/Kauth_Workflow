import type { EmployeeFormData, ProcessType, Role, WorkflowConfig, WorkflowTargetPersonSource } from "../types/workflow";
import type { WorkflowCreationStep } from "../hooks/useWorkflowCreation";

export type StepDefinition = {
  key: WorkflowCreationStep;
  title: string;
};

type CreateWorkflowPageViewModelArgs = {
  currentStep: WorkflowCreationStep;
  capabilities: { hasHrRole: boolean; hasAdminRole: boolean };
  selectedProcessType: ProcessType | null;
  selectedProcessTypeKey: string | null;
  processTypes: ProcessType[];
  requiresTargetPerson: boolean;
  employee: EmployeeFormData;
  selectedDepartmentId: number | null;
  selectedRoleId: number | null;
  selectedTargetPersonSource: WorkflowTargetPersonSource | null;
  targetPersonSourcesLoading: boolean;
  targetPersonSourcesError: string | null;
  rolesLoading: boolean;
  rolesError: string | null;
  availableRoles: Role[];
  workflowConfig: WorkflowConfig | null;
};

export function buildCreateWorkflowPageViewModel({
  currentStep,
  capabilities,
  selectedProcessType,
  selectedProcessTypeKey,
  processTypes,
  requiresTargetPerson,
  employee,
  selectedDepartmentId,
  selectedRoleId,
  selectedTargetPersonSource,
  targetPersonSourcesLoading,
  targetPersonSourcesError,
  rolesLoading,
  rolesError,
  availableRoles,
  workflowConfig,
}: CreateWorkflowPageViewModelArgs) {
  const isHrEntry = capabilities.hasHrRole || capabilities.hasAdminRole;
  const pageTitle = isHrEntry ? "Neuer Vorgang" : "Änderung starten";
  const contextStepTitle = requiresTargetPerson ? "Bestehende Person wählen" : "Neue Person erfassen";
  const reviewPersonLabel = requiresTargetPerson ? "Zielperson" : "Neue Person";
  const reviewDepartmentLabel = requiresTargetPerson ? "Aktuelle Abteilung" : "Abteilung";
  const reviewRoleLabel = requiresTargetPerson ? "Aktuelle Stelle" : "Stelle";
  const roleRecommendationCount =
    (workflowConfig?.roleRecommendations.defaultValues.length ?? 0) +
    (workflowConfig?.roleRecommendations.defaultSelectedOptions.length ?? 0);
  const hasDerivedContextGap = Boolean(
    requiresTargetPerson &&
      selectedTargetPersonSource &&
      (!selectedTargetPersonSource.departmentId ||
        !selectedTargetPersonSource.roleId ||
        selectedTargetPersonSource.employeeNumber <= 0 ||
        selectedTargetPersonSource.badgeNumber <= 0)
  );
  const processStepIssues = selectedProcessType ? [] : ["Bitte einen Vorgang wählen."];
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
    requiresTargetPerson && !selectedTargetPersonSource && !targetPersonSourcesLoading
      ? "Bitte einen passenden Quellworkflow auswählen."
      : null;
  const contextStepIssues = requiresTargetPerson
    ? [
        ...(targetPersonSourcesError ? ["Die Suche nach Quellworkflows ist fehlgeschlagen."] : []),
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
    { key: "process", title: "Vorgang wählen" },
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
    hasProcessTypes: processTypes.length > 0,
    isProcessSelected: Boolean(selectedProcessTypeKey),
  };
}
