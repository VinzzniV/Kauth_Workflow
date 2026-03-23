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

export type RequirementVisibilityDependency = {
  dependencyKey: string;
  kind: "boolean_true" | "selected_option_value";
  expectedValue: string | null;
  missingResult: boolean;
};

export type RequirementValidation = {
  kind: "text_required" | "single_select_required" | "multi_select_required";
  message: string;
};

export type RequirementResetTarget = {
  requirementKey: string;
  clearBoolean: boolean;
  clearText: boolean;
  clearNumber: boolean;
  clearSelectedOption: boolean;
  clearSelectedOptions: boolean;
};

export type RequirementSingleSelectReset = {
  keepSelectedOptionValues: string[];
  targets: RequirementResetTarget[];
};

export type RequirementBehavior = {
  visibilityDependencies: RequirementVisibilityDependency[];
  validation: RequirementValidation | null;
  resetTargetsWhenNotTrue: RequirementResetTarget[];
  singleSelectReset: RequirementSingleSelectReset | null;
};

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
  behavior: RequirementBehavior;
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
  deadlineDate: string;
};

export type WorkflowCreationPayload = {
  firstName: string;
  lastName: string;
  employeeNumber: number;
  badgeNumber: number;
  deadlineDate: string | null;
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

export type WorkflowStatus = "open" | "completed";

export type WorkflowRuntimeStatus =
  | "draft"
  | "in_progress"
  | "waiting_for_supervisor"
  | "waiting_for_department"
  | "completed"
  | "cancelled";

export type WorkflowTaskStatus =
  | "open"
  | "ready"
  | "in_progress"
  | "blocked"
  | "done"
  | "skipped"
  | "cancelled";
export type WorkflowTaskArea = string;
export type WorkflowTaskSlaStatus = "none" | "on_track" | "due_today" | "overdue";

export type WorkflowResponsibilityOption = {
  value: string;
  label: string;
};

export type WorkflowRequirementSummary = {
  totalCount: number;
  visibleCount: number;
  answeredVisibleCount: number;
  pendingVisibleCount: number;
};

export type WorkflowTaskCountSummary = {
  totalCount: number;
  openCount: number;
  inProgressCount: number;
  doneCount: number;
  endedCount: number;
  completedCount: number;
  activeCount: number;
};

export type WorkflowTaskMetrics = {
  overall: WorkflowTaskCountSummary;
  departmentPhase: WorkflowTaskCountSummary;
};

export type WorkflowTaskAreaSummary = {
  name: string;
  isCurrentArea: boolean;
  counts: WorkflowTaskCountSummary;
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
  deadlineDate: string | null;
  pendingNotifications: number;
  failedNotifications: number;
  requirementSummary: WorkflowRequirementSummary;
  taskMetrics: WorkflowTaskMetrics;
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
  isVisible: boolean;
  sortOrder: number;
  behavior: RequirementBehavior;
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

export type WorkflowAuditEntry = {
  id: number;
  eventType: string;
  createdAt: string;
  taskId: number | null;
  taskKey: string | null;
  taskTitle: string | null;
  actorUserId: number | null;
  actorUserName: string | null;
  oldValue: string | null;
  newValue: string | null;
  detail: string | null;
};

export type WorkflowTaskComment = {
  id: number;
  taskId: number;
  authorUserId: number | null;
  authorUserName: string | null;
  commentText: string;
  createdAt: string;
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
  dueInDays: number | null;
  dueAt: string | null;
  slaStatus: WorkflowTaskSlaStatus;
  readyAt: string | null;
  startedAt: string | null;
  completedAt: string | null;
  cancelledAt: string | null;
  processArea: WorkflowTaskArea | null;
  isDepartmentPhaseTask: boolean;
  canUpdateStatus: boolean;
  canAddComment: boolean;
  assignments: WorkflowTaskAssignment[];
  dependencies: WorkflowTaskDependency[];
  comments: WorkflowTaskComment[];
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
  deadlineDate: string | null;
  requirements: WorkflowRequirementSnapshot[];
  requirementSummary: WorkflowRequirementSummary;
  tasks: WorkflowTask[];
  taskMetrics: WorkflowTaskMetrics;
  taskAreas: WorkflowTaskAreaSummary[];
  notifications: WorkflowNotification[];
};

export type ApiErrorState = {
  message: string;
  status?: number;
};
