import type {
  Department,
  DerivedAnswer,
  LinkableWorkflow,
  ProcessType,
  RelatedWorkflowSummary,
  Role,
  WorkflowLink,
  WorkflowTargetPerson,
  WorkflowTargetPersonSource,
} from "../../types/workflow";
import type {
  AdminDepartmentAssignment,
  AdminDirectoryGroup,
  AdminDirectoryGroupRoleMapping,
  AdminDirectoryIdentity,
  AdminDirectoryMappingAuditEntry,
  AdminDirectorySyncResult,
  AdminDirectorySyncStatus,
  AdminGraphApplicationConfiguration,
  AdminPermission,
  AdminPermissionAuditEntry,
  AdminGroup,
  AdminNotificationEmailConfiguration,
  AdminNotificationEmailTestResponse,
  AdminSystemLogEntry,
  AdminSystemLogSummary,
  AdminAnswerDefinition,
  ClientLogEventRequest,
  AdminDependencyGraph,
  AdminDependencyGraphEdge,
  AdminDependencyGraphNode,
  AdminRoleAnswerDefault,
  AdminProcessType,
  AdminTaskTemplateCondition,
  AdminTaskTemplateDependency,
  AdminTaskTemplate,
  AdminResponsibilityOwner,
  AdminRole,
  AdminUser,
  AdminWorkflowDefinitionSummary,
  AdminWorkflowDefinitionVersionDetail,
  AdminWorkflowDefinitionVersionSummary,
  AdminWorkflowDefinitionNode,
  AdminWorkflowDefinitionEdge,
  AdminWorkflowNodeAction,
  AdminWorkflowValidationIssue,
  SimulationLoginUserOption,
  Me,
} from "../../types/auth";

export type BackendDepartmentDto = Department;
export type BackendRoleDto = Role;
export type BackendProcessTypeDto = ProcessType;

export type BackendRequirementOptionDto = {
  id: number;
  key: string;
  value: string;
  label: string;
  sortOrder: number;
  isDefault: boolean;
};

export type BackendRequirementVisibilityDependencyDto = {
  dependencyKey: string;
  kind: "boolean_true" | "selected_option_value";
  expectedValue: string | null;
  missingResult: boolean;
};

export type BackendRequirementValidationDto = {
  kind: "text_required" | "single_select_required" | "multi_select_required";
  message: string;
};

export type BackendRequirementResetTargetDto = {
  requirementKey: string;
  clearBoolean: boolean;
  clearText: boolean;
  clearNumber: boolean;
  clearSelectedOption: boolean;
  clearSelectedOptions: boolean;
};

export type BackendRequirementSingleSelectResetDto = {
  keepSelectedOptionValues: string[];
  targets: BackendRequirementResetTargetDto[];
};

export type BackendRequirementBehaviorDto = {
  visibilityDependencies: BackendRequirementVisibilityDependencyDto[];
  validation: BackendRequirementValidationDto | null;
  resetTargetsWhenNotTrue: BackendRequirementResetTargetDto[];
  singleSelectReset: BackendRequirementSingleSelectResetDto | null;
};

export type BackendRequirementDto = {
  id: number;
  key: string;
  title: string;
  description: string;
  category: string;
  iconKey: string | null;
  inputType: "boolean" | "text" | "select" | "multi_select";
  isRequired: boolean;
  sortOrder: number;
  behavior: BackendRequirementBehaviorDto;
  options: BackendRequirementOptionDto[];
};

export type BackendRoleRecommendationDefaultValueDto = {
  requirementId: number;
  valueBoolean: boolean | null;
  valueText: string | null;
  valueNumber: number | null;
};

export type BackendRoleRecommendationSelectedOptionsDto = {
  requirementId: number;
  selectedOptionId: number | null;
  selectedOptionIds: number[];
};

export type BackendRoleRecommendationsDto = {
  recommendedRequirementIds: number[];
  defaultValues: BackendRoleRecommendationDefaultValueDto[];
  defaultSelectedOptions: BackendRoleRecommendationSelectedOptionsDto[];
};

export type BackendWorkflowConfigDto = {
  requirements: BackendRequirementDto[];
  roleRecommendations: BackendRoleRecommendationsDto;
};

export type BackendWorkflowSummaryDto = {
  uid: string;
  processType: BackendProcessTypeDto;
  firstName: string;
  lastName: string;
  employeeNumber: number;
  badgeNumber: number;
  departmentId: number;
  departmentName: string;
  roleId: number;
  roleName: string;
  status: string;
  workflowStatus: string;
  createdAt: string;
  completedAt: string | null;
  deadlineDate: string | null;
  archivedAt: string | null;
  pendingNotifications: number;
  failedNotifications: number;
  requirementSummary: BackendWorkflowRequirementSummaryDto;
  taskMetrics: BackendWorkflowTaskMetricsDto;
  taskSummary: string;
  responsibilityOptions: Array<{
    value: string;
    label: string;
  }>;
};

export type BackendWorkflowPageDto = {
  items: BackendWorkflowSummaryDto[];
  count: number;
  offset: number;
  limit: number | null;
  departmentOptions: BackendDepartmentDto[];
  responsibilityOptions: Array<{
    value: string;
    label: string;
  }>;
};

export type BackendWorkflowRequirementSummaryDto = {
  totalCount: number;
  visibleCount: number;
  answeredVisibleCount: number;
  pendingVisibleCount: number;
};

export type BackendWorkflowTaskCountSummaryDto = {
  totalCount: number;
  openCount: number;
  inProgressCount: number;
  blockedCount: number;
  doneCount: number;
  completedCount: number;
  activeCount: number;
};

export type BackendWorkflowTaskMetricsDto = {
  overall: BackendWorkflowTaskCountSummaryDto;
  required: BackendWorkflowTaskCountSummaryDto;
  departmentPhase: BackendWorkflowTaskCountSummaryDto;
};

export type BackendWorkflowTaskAreaSummaryDto = {
  name: string;
  isCurrentArea: boolean;
  counts: BackendWorkflowTaskCountSummaryDto;
};

export type BackendWorkflowRequirementOptionSnapshotDto = {
  id: number;
  sourceOptionId: number | null;
  key: string;
  value: string;
  label: string;
  sortOrder: number;
};

export type BackendWorkflowRequirementSelectedOptionDto = {
  id: number;
  key: string;
  value: string;
  label: string;
  sortOrder: number;
};

export type BackendWorkflowRequirementValueDto = {
  valueBoolean: boolean | null;
  valueText: string | null;
  valueNumber: number | null;
  selectedOptionId: number | null;
  selectedOptionKey: string | null;
  selectedOptionValue: string | null;
  selectedOptionLabel: string | null;
  selectedOptions: BackendWorkflowRequirementSelectedOptionDto[];
};

export type BackendWorkflowRequirementSnapshotDto = {
  workflowRequirementId: number;
  id: number;
  key: string;
  title: string;
  description: string;
  category: string;
  iconKey: string | null;
  inputType: "boolean" | "text" | "select" | "multi_select";
  isRequired: boolean;
  isVisible: boolean;
  sortOrder: number;
  behavior: BackendRequirementBehaviorDto;
  options: BackendWorkflowRequirementOptionSnapshotDto[];
  value: BackendWorkflowRequirementValueDto;
};

export type BackendWorkflowNotificationDto = {
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

export type BackendWorkflowAuditEntryDto = {
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

export type BackendWorkflowTaskCommentDto = {
  id: number;
  taskId: number;
  authorUserId: number | null;
  authorUserName: string | null;
  commentText: string;
  createdAt: string;
};

export type BackendWorkflowTaskAssignmentDto = {
  id: number;
  assignmentType: string;
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

export type BackendWorkflowTaskDependencyDto = {
  workflowTaskId: number;
  dependsOnWorkflowTaskId: number;
  requiredStatus: string;
  dependsOnTaskKey: string;
  dependsOnTitle: string;
};

export type BackendWorkflowTaskDto = {
  id: number;
  nodeInstanceId: number | null;
  taskTemplateId: number | null;
  taskKey: string;
  isRuntimeNodeTask: boolean;
  isApprovalTask: boolean;
  title: string;
  description: string;
  category: string;
  iconKey: string | null;
  status: string;
  isRequired: boolean;
  dueInDays: number | null;
  dueAt: string | null;
  slaStatus: string;
  sortOrder: number;
  createdAt: string;
  readyAt: string | null;
  startedAt: string | null;
  completedAt: string | null;
  processArea: string | null;
  isDepartmentPhaseTask: boolean;
  canUpdateStatus: boolean;
  canDecideApproval: boolean;
  canAddComment: boolean;
  assignments: BackendWorkflowTaskAssignmentDto[];
  dependencies: BackendWorkflowTaskDependencyDto[];
  comments: BackendWorkflowTaskCommentDto[];
};

export type BackendWorkflowDetailDto = {
  uid: string;
  processType: BackendProcessTypeDto;
  firstName: string;
  lastName: string;
  employeeNumber: number;
  badgeNumber: number;
  departmentId: number;
  departmentName: string;
  roleId: number;
  roleName: string;
  status: string;
  workflowStatus: string;
  createdAt: string;
  deadlineDate: string | null;
  archivedAt: string | null;
  targetPersonId: number | null;
  requirements: BackendWorkflowRequirementSnapshotDto[];
  requirementSummary: BackendWorkflowRequirementSummaryDto;
  tasks: BackendWorkflowTaskDto[];
  taskMetrics: BackendWorkflowTaskMetricsDto;
  taskAreas: BackendWorkflowTaskAreaSummaryDto[];
  notifications: BackendWorkflowNotificationDto[];
};

export type BackendTaskWorkflowContextDto = {
  workflowId: number;
  workflowUid: string;
  workflowStatus: string;
  // Legacy backend field. Frontend UI uses workflowStatus instead.
  workflowLegacyStatus: string;
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

export type BackendTaskRotationContextDto = {
  rotationPlanId: number;
  planStatus: string;
  planTitle: string;
  sourceWorkflowUid: string;
  personId: number;
  displayName: string;
  departmentId: number;
  departmentName: string;
  rotationStationId: number | null;
  triggerType: string | null;
  anchorDate: string | null;
};

export type BackendTaskWithWorkflowDto = {
  taskRef: string;
  taskFamily: "workflow" | "rotation";
  task: BackendWorkflowTaskDto;
  workflow: BackendTaskWorkflowContextDto | null;
  rotation: BackendTaskRotationContextDto | null;
};

export type BackendSimulationLoginUserOptionDto = SimulationLoginUserOption;
export type BackendMeDto = Me;
export type BackendAdminUserDto = AdminUser;
export type BackendAdminRoleDto = AdminRole;
export type BackendAdminGroupDto = AdminGroup;
export type BackendAdminDepartmentAssignmentDto = AdminDepartmentAssignment;
export type BackendAdminResponsibilityOwnerDto = AdminResponsibilityOwner;
export type BackendAdminPermissionDto = AdminPermission;
export type BackendAdminPermissionAuditEntryDto = AdminPermissionAuditEntry;
export type BackendAdminNotificationEmailConfigurationDto = AdminNotificationEmailConfiguration;
export type BackendAdminNotificationEmailTestResponseDto = AdminNotificationEmailTestResponse;
export type BackendAdminDirectoryGroupDto = AdminDirectoryGroup;
export type BackendAdminDirectoryGroupRoleMappingDto = AdminDirectoryGroupRoleMapping;
export type BackendAdminDirectoryIdentityDto = AdminDirectoryIdentity;
export type BackendAdminDirectoryMappingAuditEntryDto = AdminDirectoryMappingAuditEntry;
export type BackendAdminDirectorySyncStatusDto = AdminDirectorySyncStatus;
export type BackendAdminDirectorySyncResultDto = AdminDirectorySyncResult;
export type BackendAdminGraphApplicationConfigurationDto = AdminGraphApplicationConfiguration;
export type BackendAdminSystemLogEntryDto = AdminSystemLogEntry;
export type BackendAdminSystemLogSummaryDto = AdminSystemLogSummary;
export type BackendClientLogEventRequestDto = ClientLogEventRequest;

export type BackendWorkflowLinkDto = WorkflowLink;
export type BackendLinkableWorkflowDto = LinkableWorkflow;
export type BackendRelatedWorkflowSummaryDto = RelatedWorkflowSummary;
export type BackendWorkflowTargetPersonDto = WorkflowTargetPerson;
export type BackendWorkflowTargetPersonSourceDto = WorkflowTargetPersonSource;
export type BackendCompletedOnboardingSearchResultDto = WorkflowTargetPersonSource;
export type BackendWorkflowStartableDefinitionDto = import("../../types/workflow").StartableWorkflowDefinition;
export type BackendDerivedAnswerDto = DerivedAnswer;
export type BackendAdminProcessTypeDto = AdminProcessType;
export type BackendAdminTaskTemplateDto = AdminTaskTemplate;
export type BackendAdminTaskTemplateConditionDto = AdminTaskTemplateCondition;
export type BackendAdminTaskTemplateDependencyDto = AdminTaskTemplateDependency;
export type BackendAdminDependencyGraphNodeDto = AdminDependencyGraphNode;
export type BackendAdminDependencyGraphEdgeDto = AdminDependencyGraphEdge;
export type BackendAdminDependencyGraphDto = AdminDependencyGraph;
export type BackendAdminAnswerDefinitionDto = AdminAnswerDefinition;
export type BackendAdminRoleAnswerDefaultDto = AdminRoleAnswerDefault;
export type BackendAdminWorkflowValidationIssueDto = AdminWorkflowValidationIssue;
export type BackendAdminWorkflowNodeActionDto = AdminWorkflowNodeAction;
export type BackendAdminWorkflowDefinitionNodeDto = AdminWorkflowDefinitionNode;
export type BackendAdminWorkflowDefinitionEdgeDto = AdminWorkflowDefinitionEdge;
export type BackendAdminWorkflowDefinitionVersionSummaryDto = AdminWorkflowDefinitionVersionSummary;
export type BackendAdminWorkflowDefinitionSummaryDto = AdminWorkflowDefinitionSummary;
export type BackendAdminWorkflowDefinitionVersionDetailDto = AdminWorkflowDefinitionVersionDetail;
export type BackendAdminWorkflowActionDefinitionDto = {
  id: number;
  key: string;
  name: string;
  description: string | null;
  handlerType: string;
  parameterSchema: unknown | null;
  isActive: boolean;
  requiresApproval: boolean;
  isIdempotent: boolean;
  createdAt: string;
  updatedAt: string;
};

export type BackendPersonWorkflowSummaryDto = {
  uid: string;
  processType: BackendProcessTypeDto;
  firstName: string;
  lastName: string;
  roleName: string;
  departmentName: string;
  status: string;
  workflowStatus: string;
  createdAt: string;
  completedAt: string | null;
  archivedAt: string | null;
};

export type BackendPersonWorkflowHistoryDto = {
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
  employmentStatus: string | null;
  entryDate: string | null;
  exitDate: string | null;
  appUserId: number | null;
  directoryIdentityId: number | null;
  directoryLinkStatus: string | null;
  directoryDisplayName: string | null;
  directoryUserPrincipalName: string | null;
  directoryMail: string | null;
  directoryEmployeeNumber: number | null;
  latestCompletedOnboardingWorkflowUid: string | null;
  latestCompletedOnboardingAt: string | null;
  workflows: BackendPersonWorkflowSummaryDto[];
};
