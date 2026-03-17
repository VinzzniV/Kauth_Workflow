// Gemeinsame Frontend-Typen fuer Workflow-Konfiguration, Laufzeitdaten und Aufgabenansichten.
export type Role = {
  id: number;
  departmentId: number;
  departmentName: string;
  name: string;
  isActive: boolean;
};

export type Department = {
  id: number;
  name: string;
};

export type RequirementOption = {
  id: number;
  key: string;
  value: string;
  label: string;
  sortOrder: number;
  isDefault: boolean;
};

export type RequirementInputType = "boolean" | "text" | "select" | "multi_select";

// Konfiguration fuer Anforderungen, wie sie bei der Erstellung eines Onboardings verwendet wird.
export type RoleRequirement = {
  id: number;
  key: string;
  title: string;
  description: string;
  category: string;
  iconKey: string;
  inputType: RequirementInputType;
  isRecommended: boolean;
  isDefault: boolean;
  isRequired: boolean;
  sortOrder: number;
  defaultValueBoolean: boolean | null;
  defaultValueText: string | null;
  defaultValueNumber: number | null;
  defaultSelectedOptionId: number | null;
  defaultSelectedOptionIds: number[];
  options: RequirementOption[];
};

export type RoleRecommendationDefaultValue = {
  requirementId: number;
  valueBoolean: boolean | null;
  valueText: string | null;
  valueNumber: number | null;
};

export type RoleRecommendationSelectedOptions = {
  requirementId: number;
  selectedOptionId: number | null;
  selectedOptionIds: number[];
};

export type RoleRecommendations = {
  recommendedRequirementIds: number[];
  defaultValues: RoleRecommendationDefaultValue[];
  defaultSelectedOptions: RoleRecommendationSelectedOptions[];
};

export type WorkflowConfig = {
  requirements: RoleRequirement[];
  roleRecommendations: RoleRecommendations;
};

// Lokaler Bearbeitungszustand fuer Anforderungen im Frontend.
export type RequirementSelectionState = {
  valueBoolean: boolean | null;
  valueText: string;
  valueNumber: number | null;
  selectedOptionId: number | null;
  selectedOptionIds: number[];
};

export type RequirementSelectionPayload = {
  requirementId: number;
  valueBoolean?: boolean | null;
  valueText?: string | null;
  valueNumber?: number | null;
  selectedOptionId?: number | null;
  selectedOptionIds?: number[];
};

export type EmployeeFormData = {
  firstName: string;
  lastName: string;
  employeeNumber: number;
  badgeNumber: number;
};

export type WorkflowCreationPayload = EmployeeFormData & {
  departmentId: number;
  roleId: number;
};

// Rueckgabe nach erfolgreichem Start eines neuen Workflows.
export type WorkflowCreationSummary = {
  workflowStatus: string;
  taskCount: number;
  readyTaskCount: number;
  blockedTaskCount: number;
  doneTaskCount: number;
  assignmentCount: number;
  pendingNotifications: number;
};

export type WorkflowCreationResponse = {
  uid: string;
  notificationTargets: number;
  failedNotifications: number;
  summary: WorkflowCreationSummary;
};

export type WorkflowStatus = "open" | "completed" | "cancelled";

export type WorkflowRuntimeStatus =
  | "draft"
  | "in_progress"
  | "waiting_for_supervisor"
  | "waiting_for_department"
  | "completed"
  | "cancelled";

export type WorkflowTaskStatus = "open" | "ready" | "in_progress" | "blocked" | "done" | "skipped" | "cancelled";
export type WorkflowTaskArea = "HR" | "Abteilungsleitung" | "IT" | "QS" | "AV" | "QMB";

export type WorkflowResponsibilityOption = {
  value: string;
  label: string;
};

// Kompakte Uebersicht fuer Listen und Dashboards.
export type WorkflowSummary = {
  uid: string;
  firstName: string;
  lastName: string;
  employeeNumber: number;
  badgeNumber: number;
  departmentId: number;
  departmentName: string;
  roleId: number;
  roleName: string;
  status: WorkflowStatus;
  workflowStatus: WorkflowRuntimeStatus;
  createdAt: string;
  pendingNotifications: number;
  failedNotifications: number;
  taskSummary: string;
  responsibilityOptions: WorkflowResponsibilityOption[];
};

// Detailansicht eines Workflows inklusive Antworten, Aufgaben und Benachrichtigungen.
export type WorkflowRequirementSnapshot = {
  workflowRequirementId: number;
  id: number;
  key: string;
  title: string;
  description: string;
  category: string;
  iconKey: string;
  inputType: RequirementInputType;
  isRequired: boolean;
  sortOrder: number;
  options: WorkflowRequirementOptionSnapshot[];
  value: WorkflowRequirementValue;
};

export type WorkflowRequirementOptionSnapshot = {
  id: number;
  sourceOptionId: number | null;
  key: string;
  value: string;
  label: string;
  sortOrder: number;
};

export type WorkflowRequirementSelectedOption = {
  id: number;
  key: string;
  value: string;
  label: string;
  sortOrder: number;
};

export type WorkflowRequirementValue = {
  valueBoolean: boolean | null;
  valueText: string | null;
  valueNumber: number | null;
  selectedOptionId: number | null;
  selectedOptionKey: string | null;
  selectedOptionValue: string | null;
  selectedOptionLabel: string | null;
  selectedOptions: WorkflowRequirementSelectedOption[];
};

export type WorkflowNotification = {
  id: number;
  targetName: string;
  targetEmail: string;
  notificationType: string;
  status: "pending" | "sent" | "failed" | "disabled";
  attempts: number;
  recipientUserId: number | null;
  lastError: string | null;
  createdAt: string;
  sentAt: string | null;
};

export type WorkflowTaskAssignment = {
  id: number;
  assignmentType: "responsibility" | "user";
  isPrimary: boolean;
  assigneeUserId: number | null;
  assigneeUserName: string | null;
  assigneeUserEmail: string | null;
  assigneeResponsibilityId: number | null;
  assigneeResponsibilityKey: string | null;
  assigneeResponsibilityName: string | null;
  assigneeResponsibilityType: string | null;
  assignedAt: string;
  completedAt: string | null;
};

export type WorkflowTaskDependency = {
  workflowTaskId: number;
  dependsOnWorkflowTaskId: number;
  requiredStatus: WorkflowTaskStatus;
  dependsOnTaskKey: string;
  dependsOnTitle: string;
};

export type WorkflowTask = {
  id: number;
  taskTemplateId: number | null;
  taskKey: string;
  title: string;
  description: string;
  category: string;
  iconKey: string;
  status: WorkflowTaskStatus;
  isRequired: boolean;
  sortOrder: number;
  createdAt: string;
  readyAt: string | null;
  startedAt: string | null;
  completedAt: string | null;
  cancelledAt: string | null;
  processArea: WorkflowTaskArea | null;
  canUpdateStatus: boolean;
  assignments: WorkflowTaskAssignment[];
  dependencies: WorkflowTaskDependency[];
};

export type TaskWorkflowContext = {
  workflowId: number;
  workflowUid: string;
  workflowStatus: WorkflowRuntimeStatus;
  workflowLegacyStatus: WorkflowStatus;
  workflowCreatedAt: string;
  firstName: string;
  lastName: string;
  employeeNumber: number;
  badgeNumber: number;
  departmentId: number;
  departmentName: string;
  roleId: number;
  roleName: string;
};

export type TaskWithWorkflow = {
  task: WorkflowTask;
  workflow: TaskWorkflowContext;
};

export type WorkflowDetail = {
  uid: string;
  firstName: string;
  lastName: string;
  employeeNumber: number;
  badgeNumber: number;
  departmentId: number;
  departmentName: string;
  roleId: number;
  roleName: string;
  status: WorkflowStatus;
  workflowStatus: WorkflowRuntimeStatus;
  createdAt: string;
  requirements: WorkflowRequirementSnapshot[];
  tasks: WorkflowTask[];
  notifications: WorkflowNotification[];
};

export type ApiErrorState = {
  message: string;
  status?: number;
};
