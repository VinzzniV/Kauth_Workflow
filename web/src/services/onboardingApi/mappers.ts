import type {
  Department,
  RequirementBehavior,
  RequirementOption,
  RequirementResetTarget,
  RequirementSingleSelectReset,
  RequirementValidation,
  RequirementVisibilityDependency,
  Role,
  RoleRequirement,
  RoleRecommendations,
  WorkflowConfig,
  WorkflowDetail,
  WorkflowNotification,
  WorkflowRequirementOptionSnapshot,
  WorkflowRequirementSelectedOption,
  WorkflowRequirementSummary,
  WorkflowRequirementSnapshot,
  WorkflowRequirementValue,
  WorkflowRuntimeStatus,
  WorkflowSummary,
  WorkflowTaskAreaSummary,
  WorkflowTaskCountSummary,
  WorkflowTaskMetrics,
  WorkflowTask,
  WorkflowTaskArea,
  WorkflowTaskAssignment,
  WorkflowTaskDependency,
  WorkflowTaskStatus,
  TaskWithWorkflow,
  TaskWorkflowContext,
} from "../../types/workflow";
import type {
  AdminDepartmentAssignment,
  AdminGroup,
  AdminNotificationEmailConfiguration,
  AdminNotificationEmailTestResponse,
  AdminResponsibilityOwner,
  AdminRole,
  AdminUser,
  DemoLoginUserOption,
  Me,
} from "../../types/auth";
import { coerceIconKey } from "../../utils/iconRegistry";
import { toWorkflowLegacyStatus } from "../../utils/workflowStatus";

export type BackendDepartmentDto = Department;
export type BackendRoleDto = Role;

type BackendRequirementOptionDto = {
  id: number;
  key: string;
  value: string;
  label: string;
  sortOrder: number;
  isDefault: boolean;
};

type BackendRequirementVisibilityDependencyDto = {
  dependencyKey: string;
  kind: "boolean_true" | "selected_option_value";
  expectedValue: string | null;
  missingResult: boolean;
};

type BackendRequirementValidationDto = {
  kind: "text_required" | "single_select_required" | "multi_select_required";
  message: string;
};

type BackendRequirementResetTargetDto = {
  requirementKey: string;
  clearBoolean: boolean;
  clearText: boolean;
  clearNumber: boolean;
  clearSelectedOption: boolean;
  clearSelectedOptions: boolean;
};

type BackendRequirementSingleSelectResetDto = {
  keepSelectedOptionValues: string[];
  targets: BackendRequirementResetTargetDto[];
};

type BackendRequirementBehaviorDto = {
  visibilityDependencies: BackendRequirementVisibilityDependencyDto[];
  validation: BackendRequirementValidationDto | null;
  resetTargetsWhenNotTrue: BackendRequirementResetTargetDto[];
  singleSelectReset: BackendRequirementSingleSelectResetDto | null;
};

type BackendRequirementDto = {
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

type BackendRoleRecommendationDefaultValueDto = {
  requirementId: number;
  valueBoolean: boolean | null;
  valueText: string | null;
  valueNumber: number | null;
};

type BackendRoleRecommendationSelectedOptionsDto = {
  requirementId: number;
  selectedOptionId: number | null;
  selectedOptionIds: number[];
};

type BackendRoleRecommendationsDto = {
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

type BackendWorkflowRequirementSummaryDto = {
  totalCount: number;
  visibleCount: number;
  answeredVisibleCount: number;
  pendingVisibleCount: number;
};

type BackendWorkflowTaskCountSummaryDto = {
  totalCount: number;
  openCount: number;
  inProgressCount: number;
  doneCount: number;
  endedCount: number;
  completedCount: number;
  activeCount: number;
};

type BackendWorkflowTaskMetricsDto = {
  overall: BackendWorkflowTaskCountSummaryDto;
  departmentPhase: BackendWorkflowTaskCountSummaryDto;
};

type BackendWorkflowTaskAreaSummaryDto = {
  name: string;
  isCurrentArea: boolean;
  counts: BackendWorkflowTaskCountSummaryDto;
};

type BackendWorkflowRequirementOptionSnapshotDto = {
  id: number;
  sourceOptionId: number | null;
  key: string;
  value: string;
  label: string;
  sortOrder: number;
};

type BackendWorkflowRequirementSelectedOptionDto = {
  id: number;
  key: string;
  value: string;
  label: string;
  sortOrder: number;
};

type BackendWorkflowRequirementValueDto = {
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
  sortOrder: number;
  behavior: BackendRequirementBehaviorDto;
  options: BackendWorkflowRequirementOptionSnapshotDto[];
  value: BackendWorkflowRequirementValueDto;
};

type BackendWorkflowNotificationDto = {
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

type BackendWorkflowTaskAssignmentDto = {
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

type BackendWorkflowTaskDependencyDto = {
  workflowTaskId: number;
  dependsOnWorkflowTaskId: number;
  requiredStatus: string;
  dependsOnTaskKey: string;
  dependsOnTitle: string;
};

export type BackendWorkflowTaskDto = {
  id: number;
  taskTemplateId: number | null;
  taskKey: string;
  title: string;
  description: string;
  category: string;
  iconKey: string | null;
  status: string;
  isRequired: boolean;
  sortOrder: number;
  createdAt: string;
  readyAt: string | null;
  startedAt: string | null;
  completedAt: string | null;
  cancelledAt: string | null;
  processArea: string | null;
  isDepartmentPhaseTask: boolean;
  canUpdateStatus: boolean;
  assignments: BackendWorkflowTaskAssignmentDto[];
  dependencies: BackendWorkflowTaskDependencyDto[];
};

export type BackendWorkflowDetailDto = {
  uid: string;
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
  requirements: BackendWorkflowRequirementSnapshotDto[];
  requirementSummary: BackendWorkflowRequirementSummaryDto;
  tasks: BackendWorkflowTaskDto[];
  taskMetrics: BackendWorkflowTaskMetricsDto;
  taskAreas: BackendWorkflowTaskAreaSummaryDto[];
  notifications: BackendWorkflowNotificationDto[];
};

type BackendTaskWorkflowContextDto = {
  workflowId: number;
  workflowUid: string;
  workflowStatus: string;
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

export type BackendTaskWithWorkflowDto = {
  task: BackendWorkflowTaskDto;
  workflow: BackendTaskWorkflowContextDto;
};

export type BackendDemoLoginUserOptionDto = DemoLoginUserOption;
export type BackendMeDto = Me;
export type BackendAdminUserDto = AdminUser;
export type BackendAdminRoleDto = AdminRole;
export type BackendAdminGroupDto = AdminGroup;
export type BackendAdminDepartmentAssignmentDto = AdminDepartmentAssignment;
export type BackendAdminResponsibilityOwnerDto = AdminResponsibilityOwner;
export type BackendAdminNotificationEmailConfigurationDto = AdminNotificationEmailConfiguration;
export type BackendAdminNotificationEmailTestResponseDto = AdminNotificationEmailTestResponse;

function toIconKey(iconKey?: string | null): string {
  return coerceIconKey(iconKey);
}

function normalizeLegacyWorkflowStatus(status: string): string {
  const normalized = status.trim().toLowerCase();
  return normalized === "cancelled" ? "completed" : normalized;
}

function normalizeLegacyTaskStatus(status: string): string {
  const normalized = status.trim().toLowerCase();
  return normalized === "cancelled" ? "skipped" : normalized;
}

function toWorkflowRuntimeStatus(status: string): WorkflowRuntimeStatus {
  const normalized = normalizeLegacyWorkflowStatus(status);

  switch (normalized) {
    case "draft":
    case "in_progress":
    case "waiting_for_supervisor":
    case "waiting_for_department":
    case "completed":
      return normalized;
    default:
      return "in_progress";
  }
}

function toWorkflowTaskStatus(status: string): WorkflowTaskStatus {
  const normalized = normalizeLegacyTaskStatus(status);

  switch (normalized) {
    case "open":
    case "ready":
    case "in_progress":
    case "blocked":
    case "done":
    case "skipped":
      return normalized;
    default:
      return "open";
  }
}

function toWorkflowTaskArea(area: string | null): WorkflowTaskArea | null {
  if (!area || !area.trim()) {
    return null;
  }

  return area.trim();
}

function mapRequirementOption(dto: BackendRequirementOptionDto): RequirementOption {
  return dto;
}

function mapRequirementVisibilityDependency(
  dto: BackendRequirementVisibilityDependencyDto
): RequirementVisibilityDependency {
  return { ...dto };
}

function mapRequirementValidation(dto: BackendRequirementValidationDto): RequirementValidation {
  return { ...dto };
}

function mapRequirementResetTarget(dto: BackendRequirementResetTargetDto): RequirementResetTarget {
  return { ...dto };
}

function mapRequirementSingleSelectReset(dto: BackendRequirementSingleSelectResetDto): RequirementSingleSelectReset {
  return {
    keepSelectedOptionValues: [...dto.keepSelectedOptionValues],
    targets: dto.targets.map(mapRequirementResetTarget),
  };
}

function mapRequirementBehavior(dto: BackendRequirementBehaviorDto): RequirementBehavior {
  return {
    visibilityDependencies: dto.visibilityDependencies.map(mapRequirementVisibilityDependency),
    validation: dto.validation ? mapRequirementValidation(dto.validation) : null,
    resetTargetsWhenNotTrue: dto.resetTargetsWhenNotTrue.map(mapRequirementResetTarget),
    singleSelectReset: dto.singleSelectReset ? mapRequirementSingleSelectReset(dto.singleSelectReset) : null,
  };
}

function mapRequirement(dto: BackendRequirementDto): RoleRequirement {
  return {
    id: dto.id,
    key: dto.key,
    title: dto.title,
    description: dto.description,
    category: dto.category,
    iconKey: toIconKey(dto.iconKey),
    inputType: dto.inputType,
    isRecommended: false,
    isDefault: false,
    isRequired: dto.isRequired,
    sortOrder: dto.sortOrder,
    defaultValueBoolean: null,
    defaultValueText: null,
    defaultValueNumber: null,
    defaultSelectedOptionId: null,
    defaultSelectedOptionIds: [],
    behavior: mapRequirementBehavior(dto.behavior),
    options: dto.options.map(mapRequirementOption),
  };
}

function mapRoleRecommendations(dto: BackendRoleRecommendationsDto): RoleRecommendations {
  return {
    recommendedRequirementIds: [...dto.recommendedRequirementIds],
    defaultValues: dto.defaultValues.map((value) => ({ ...value })),
    defaultSelectedOptions: dto.defaultSelectedOptions.map((selectedOptions) => ({
      requirementId: selectedOptions.requirementId,
      selectedOptionId: selectedOptions.selectedOptionId,
      selectedOptionIds: [...selectedOptions.selectedOptionIds],
    })),
  };
}

function mapWorkflowRequirementOption(dto: BackendWorkflowRequirementOptionSnapshotDto): WorkflowRequirementOptionSnapshot {
  return dto;
}

function mapWorkflowRequirementSelectedOption(dto: BackendWorkflowRequirementSelectedOptionDto): WorkflowRequirementSelectedOption {
  return dto;
}

function mapWorkflowRequirementValue(dto: BackendWorkflowRequirementValueDto): WorkflowRequirementValue {
  return {
    ...dto,
    selectedOptions: dto.selectedOptions.map(mapWorkflowRequirementSelectedOption),
  };
}

function mapWorkflowRequirementSummary(dto: BackendWorkflowRequirementSummaryDto): WorkflowRequirementSummary {
  return { ...dto };
}

function mapWorkflowTaskCountSummary(dto: BackendWorkflowTaskCountSummaryDto): WorkflowTaskCountSummary {
  return { ...dto };
}

function mapWorkflowTaskMetrics(dto: BackendWorkflowTaskMetricsDto): WorkflowTaskMetrics {
  return {
    overall: mapWorkflowTaskCountSummary(dto.overall),
    departmentPhase: mapWorkflowTaskCountSummary(dto.departmentPhase),
  };
}

function mapWorkflowTaskAreaSummary(dto: BackendWorkflowTaskAreaSummaryDto): WorkflowTaskAreaSummary {
  return {
    name: dto.name,
    isCurrentArea: dto.isCurrentArea,
    counts: mapWorkflowTaskCountSummary(dto.counts),
  };
}

export function mapWorkflowRequirement(dto: BackendWorkflowRequirementSnapshotDto): WorkflowRequirementSnapshot {
  return {
    workflowRequirementId: dto.workflowRequirementId,
    id: dto.id,
    key: dto.key,
    title: dto.title,
    description: dto.description,
    category: dto.category,
    iconKey: toIconKey(dto.iconKey),
    inputType: dto.inputType,
    isRequired: dto.isRequired,
    sortOrder: dto.sortOrder,
    behavior: mapRequirementBehavior(dto.behavior),
    options: dto.options.map(mapWorkflowRequirementOption),
    value: mapWorkflowRequirementValue(dto.value),
  };
}

function mapWorkflowNotification(dto: BackendWorkflowNotificationDto): WorkflowNotification {
  return dto;
}

function mapWorkflowTaskAssignment(dto: BackendWorkflowTaskAssignmentDto): WorkflowTaskAssignment {
  return {
    ...dto,
    assignmentType: dto.assignmentType === "user" ? "user" : "responsibility",
  };
}

function mapWorkflowTaskDependency(dto: BackendWorkflowTaskDependencyDto): WorkflowTaskDependency {
  return {
    ...dto,
    requiredStatus: toWorkflowTaskStatus(dto.requiredStatus),
  };
}

export function mapWorkflowTask(dto: BackendWorkflowTaskDto): WorkflowTask {
  return {
    ...dto,
    iconKey: toIconKey(dto.iconKey),
    status: toWorkflowTaskStatus(dto.status),
    processArea: toWorkflowTaskArea(dto.processArea),
    assignments: dto.assignments.map(mapWorkflowTaskAssignment),
    dependencies: dto.dependencies.map(mapWorkflowTaskDependency),
  };
}

export function mapWorkflowSummary(dto: BackendWorkflowSummaryDto): WorkflowSummary {
  const workflowStatus = normalizeLegacyWorkflowStatus(dto.workflowStatus);

  return {
    uid: dto.uid,
    firstName: dto.firstName,
    lastName: dto.lastName,
    employeeNumber: dto.employeeNumber,
    badgeNumber: dto.badgeNumber,
    departmentId: dto.departmentId,
    departmentName: dto.departmentName,
    roleId: dto.roleId,
    roleName: dto.roleName,
    status: toWorkflowLegacyStatus(workflowStatus),
    workflowStatus: toWorkflowRuntimeStatus(workflowStatus),
    createdAt: dto.createdAt,
    pendingNotifications: dto.pendingNotifications,
    failedNotifications: dto.failedNotifications,
    requirementSummary: mapWorkflowRequirementSummary(dto.requirementSummary),
    taskMetrics: mapWorkflowTaskMetrics(dto.taskMetrics),
    taskSummary: dto.taskSummary,
    responsibilityOptions: dto.responsibilityOptions.map((option) => ({ ...option })),
  };
}

export function mapWorkflowDetail(dto: BackendWorkflowDetailDto): WorkflowDetail {
  const workflowStatus = normalizeLegacyWorkflowStatus(dto.workflowStatus);

  return {
    uid: dto.uid,
    firstName: dto.firstName,
    lastName: dto.lastName,
    employeeNumber: dto.employeeNumber,
    badgeNumber: dto.badgeNumber,
    departmentId: dto.departmentId,
    departmentName: dto.departmentName,
    roleId: dto.roleId,
    roleName: dto.roleName,
    status: toWorkflowLegacyStatus(workflowStatus),
    workflowStatus: toWorkflowRuntimeStatus(workflowStatus),
    createdAt: dto.createdAt,
    requirements: dto.requirements.map(mapWorkflowRequirement),
    requirementSummary: mapWorkflowRequirementSummary(dto.requirementSummary),
    tasks: dto.tasks.map(mapWorkflowTask),
    taskMetrics: mapWorkflowTaskMetrics(dto.taskMetrics),
    taskAreas: dto.taskAreas.map(mapWorkflowTaskAreaSummary),
    notifications: dto.notifications.map(mapWorkflowNotification),
  };
}

function mapTaskWorkflowContext(dto: BackendTaskWorkflowContextDto): TaskWorkflowContext {
  const workflowStatus = normalizeLegacyWorkflowStatus(dto.workflowStatus);

  return {
    workflowId: dto.workflowId,
    workflowUid: dto.workflowUid,
    workflowStatus: toWorkflowRuntimeStatus(workflowStatus),
    workflowLegacyStatus: toWorkflowLegacyStatus(workflowStatus),
    workflowCreatedAt: dto.workflowCreatedAt,
    firstName: dto.firstName,
    lastName: dto.lastName,
    employeeNumber: dto.employeeNumber,
    badgeNumber: dto.badgeNumber,
    departmentId: dto.departmentId,
    departmentName: dto.departmentName,
    roleId: dto.roleId,
    roleName: dto.roleName,
  };
}

export function mapTaskWithWorkflow(dto: BackendTaskWithWorkflowDto): TaskWithWorkflow {
  return {
    task: mapWorkflowTask(dto.task),
    workflow: mapTaskWorkflowContext(dto.workflow),
  };
}

export function mapWorkflowConfig(dto: BackendWorkflowConfigDto): WorkflowConfig {
  return {
    requirements: dto.requirements.map(mapRequirement),
    roleRecommendations: mapRoleRecommendations(dto.roleRecommendations),
  };
}
