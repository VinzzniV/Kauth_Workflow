// Gemeinsame Frontend-Typen fuer Workflow-Konfiguration, Laufzeitdaten und Aufgabenansichten.
export type ProcessType = {
  key: string;
  name: string;
  description?: string | null;
  requiresTargetPerson: boolean;
};

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

// Konfiguration fuer Anforderungen, wie sie bei der Erstellung eines Vorgangs verwendet wird.
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
  processTypeKey: string;
  departmentId?: number | null;
  roleId?: number | null;
  targetPersonId?: number | null;
  sourceWorkflowUid?: string | null;
  firstName?: string | null;
  lastName?: string | null;
  employeeNumber?: number | null;
  badgeNumber?: number | null;
  deadlineDate?: string | null;
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
  | "completed";

export type WorkflowTaskStatus =
  | "open"
  | "ready"
  | "in_progress"
  | "blocked"
  | "done";
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
  processType: ProcessType;
  firstName: string;
  lastName: string;
  employeeNumber: number;
  badgeNumber: number;
  departmentId: number;
  departmentName: string;
  roleId: number;
  roleName: string;
  // Legacy backend field. Prefer workflowStatus for UI filters and labels.
  status: WorkflowStatus;
  workflowStatus: WorkflowRuntimeStatus;
  createdAt: string;
  deadlineDate: string | null;
  archivedAt: string | null;
  pendingNotifications: number;
  failedNotifications: number;
  requirementSummary: WorkflowRequirementSummary;
  taskMetrics: WorkflowTaskMetrics;
  taskSummary: string;
  responsibilityOptions: WorkflowResponsibilityOption[];
};

export type WorkflowPage = {
  items: WorkflowSummary[];
  count: number;
  offset: number;
  limit: number | null;
  departmentOptions: Department[];
  responsibilityOptions: WorkflowResponsibilityOption[];
};

// Detailansicht eines Workflows inklusive Antworten, Aufgaben und Benachrichtigungen.
export type WorkflowDetail = {
  uid: string;
  processType: ProcessType;
  firstName: string;
  lastName: string;
  employeeNumber: number;
  badgeNumber: number;
  departmentId: number;
  departmentName: string;
  roleId: number;
  roleName: string;
  // Legacy backend field. Prefer workflowStatus for UI filters and labels.
  status: WorkflowStatus;
  workflowStatus: WorkflowRuntimeStatus;
  createdAt: string;
  deadlineDate: string | null;
  archivedAt: string | null;
  targetPersonId: number | null;
  requirements: WorkflowRequirementSnapshot[];
  requirementSummary: WorkflowRequirementSummary;
  tasks: WorkflowTask[];
  taskMetrics: WorkflowTaskMetrics;
  taskAreas: WorkflowTaskAreaSummary[];
  notifications: WorkflowNotification[];
};

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
  isApprovalTask: boolean;
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

export type ApiErrorState = {
  message: string;
  status?: number;
};

export type WorkflowLink = {
  id: number;
  sourceWorkflowUid: string;
  targetWorkflowUid: string;
  linkType: string;
  linkedWorkflowFirstName: string;
  linkedWorkflowLastName: string;
  linkedWorkflowProcessType: ProcessType;
  linkedWorkflowStatus: string;
  linkedWorkflowCreatedAt: string;
  notes: string | null;
  createdByUserId: number | null;
  createdByUserName: string | null;
  createdAt: string;
};

export type LinkableWorkflow = {
  uid: string;
  processType: ProcessType;
  firstName: string;
  lastName: string;
  employeeNumber: number;
  departmentName: string;
  status: string;
  workflowStatus: string;
  createdAt: string;
};

export type WorkflowTargetPerson = {
  personId: number;
  displayName: string;
  departmentId: number | null;
  departmentName: string | null;
  roleId: number | null;
  roleName: string | null;
  employeeNumber: number | null;
  badgeNumber: number | null;
  firstName: string | null;
  lastName: string | null;
};

export type CompletedOnboardingSearchResult = {
  workflowUid: string;
  personId: number;
  displayName: string;
  firstName: string;
  lastName: string;
  employeeNumber: number;
  badgeNumber: number;
  departmentId: number | null;
  departmentName: string | null;
  roleId: number | null;
  roleName: string | null;
  completedAt: string;
  archivedAt: string | null;
};

export type DerivedAnswer = {
  targetAnswerKey: string;
  sourceAnswerKey: string;
  valueBoolean: boolean | null;
  valueText: string | null;
  valueNumber: number | null;
  selectedOptionValue: string | null;
};

export type BulkDepartmentChangePayload = {
  sourceDepartmentId: number;
  targetDepartmentId: number;
  targetRoleId: number;
  deadlineDate?: string | null;
  dryRun: boolean;
};

export type BulkOperationResult = {
  totalEmployees: number;
  createdWorkflows: number;
  skippedEmployees: number;
  failedEmployees: number;
  isDryRun: boolean;
  items: BulkOperationItem[];
};

export type BulkOperationItem = {
  personId: number;
  displayName: string;
  status: string;
  workflowUid: string | null;
  errorMessage: string | null;
};

// Mitarbeiter-Lifecycle: Alle Vorgänge einer Person in chronologischer Reihenfolge.
export type PersonWorkflowSummary = {
  uid: string;
  processType: ProcessType;
  firstName: string;
  lastName: string;
  roleName: string;
  departmentName: string;
  status: WorkflowStatus;
  workflowStatus: WorkflowRuntimeStatus;
  createdAt: string;
  completedAt: string | null;
  archivedAt: string | null;
};

export type PersonWorkflowHistory = {
  personId: number;
  displayName: string;
  departmentId: number | null;
  departmentName: string | null;
  employeeNumber: number | null;
  badgeNumber: number | null;
  firstName: string | null;
  lastName: string | null;
  workflows: PersonWorkflowSummary[];
};
