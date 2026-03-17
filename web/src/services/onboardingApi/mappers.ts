import type {
  Department,
  RequirementOption,
  Role,
  RoleRequirement,
  RoleRecommendations,
  WorkflowConfig,
  WorkflowDetail,
  WorkflowNotification,
  WorkflowRequirementOptionSnapshot,
  WorkflowRequirementSelectedOption,
  WorkflowRequirementSnapshot,
  WorkflowRequirementValue,
  WorkflowRuntimeStatus,
  WorkflowSummary,
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
  taskSummary: string;
  responsibilityOptions: Array<{
    value: string;
    label: string;
  }>;
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
  tasks: BackendWorkflowTaskDto[];
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

function toWorkflowRuntimeStatus(status: string): WorkflowRuntimeStatus {
  switch (status) {
    case "draft":
    case "in_progress":
    case "waiting_for_supervisor":
    case "waiting_for_department":
    case "completed":
    case "cancelled":
      return status;
    default:
      return "in_progress";
  }
}

function toWorkflowTaskStatus(status: string): WorkflowTaskStatus {
  switch (status) {
    case "open":
    case "ready":
    case "in_progress":
    case "blocked":
    case "done":
    case "skipped":
    case "cancelled":
      return status;
    default:
      return "open";
  }
}

function toWorkflowTaskArea(area: string | null): WorkflowTaskArea | null {
  switch (area) {
    case "HR":
    case "Abteilungsleitung":
    case "IT":
    case "QS":
    case "AV":
    case "QMB":
      return area;
    default:
      return null;
  }
}

function mapRequirementOption(dto: BackendRequirementOptionDto): RequirementOption {
  return dto;
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
    status: toWorkflowLegacyStatus(dto.workflowStatus),
    workflowStatus: toWorkflowRuntimeStatus(dto.workflowStatus),
    createdAt: dto.createdAt,
    pendingNotifications: dto.pendingNotifications,
    failedNotifications: dto.failedNotifications,
    taskSummary: dto.taskSummary,
    responsibilityOptions: dto.responsibilityOptions.map((option) => ({ ...option })),
  };
}

export function mapWorkflowDetail(dto: BackendWorkflowDetailDto): WorkflowDetail {
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
    status: toWorkflowLegacyStatus(dto.workflowStatus),
    workflowStatus: toWorkflowRuntimeStatus(dto.workflowStatus),
    createdAt: dto.createdAt,
    requirements: dto.requirements.map(mapWorkflowRequirement),
    tasks: dto.tasks.map(mapWorkflowTask),
    notifications: dto.notifications.map(mapWorkflowNotification),
  };
}

function mapTaskWorkflowContext(dto: BackendTaskWorkflowContextDto): TaskWorkflowContext {
  return {
    workflowId: dto.workflowId,
    workflowUid: dto.workflowUid,
    workflowStatus: toWorkflowRuntimeStatus(dto.workflowStatus),
    workflowLegacyStatus: toWorkflowLegacyStatus(dto.workflowStatus),
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
