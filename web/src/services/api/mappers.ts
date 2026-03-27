import type {
  ProcessType,
  RequirementBehavior,
  RequirementOption,
  RequirementResetTarget,
  RequirementSingleSelectReset,
  RequirementValidation,
  RequirementVisibilityDependency,
  RoleRequirement,
  RoleRecommendations,
  TaskWithWorkflow,
  TaskWorkflowContext,
  WorkflowAuditEntry,
  WorkflowConfig,
  WorkflowDetail,
  WorkflowNotification,
  WorkflowPage,
  WorkflowRequirementOptionSnapshot,
  WorkflowRequirementSelectedOption,
  WorkflowRequirementSnapshot,
  WorkflowRequirementSummary,
  WorkflowRequirementValue,
  WorkflowRuntimeStatus,
  WorkflowSummary,
  WorkflowTask,
  WorkflowTaskArea,
  WorkflowTaskAreaSummary,
  WorkflowTaskAssignment,
  WorkflowTaskComment,
  WorkflowTaskCountSummary,
  WorkflowTaskDependency,
  WorkflowTaskMetrics,
  WorkflowTaskSlaStatus,
  WorkflowTaskStatus,
  PersonWorkflowHistory,
  PersonWorkflowSummary,
} from "../../types/workflow";
import { coerceIconKey } from "../../utils/iconRegistry";
import { toWorkflowLegacyStatus } from "../../utils/workflowStatus";
import type {
  BackendProcessTypeDto,
  BackendRequirementBehaviorDto,
  BackendRequirementDto,
  BackendRequirementOptionDto,
  BackendRequirementResetTargetDto,
  BackendRequirementSingleSelectResetDto,
  BackendRequirementValidationDto,
  BackendRequirementVisibilityDependencyDto,
  BackendRoleRecommendationsDto,
  BackendTaskWithWorkflowDto,
  BackendTaskWorkflowContextDto,
  BackendWorkflowAuditEntryDto,
  BackendWorkflowConfigDto,
  BackendWorkflowDetailDto,
  BackendWorkflowNotificationDto,
  BackendWorkflowPageDto,
  BackendWorkflowRequirementOptionSnapshotDto,
  BackendWorkflowRequirementSelectedOptionDto,
  BackendWorkflowRequirementSnapshotDto,
  BackendWorkflowRequirementSummaryDto,
  BackendWorkflowRequirementValueDto,
  BackendWorkflowSummaryDto,
  BackendWorkflowTaskAreaSummaryDto,
  BackendWorkflowTaskAssignmentDto,
  BackendWorkflowTaskCommentDto,
  BackendWorkflowTaskCountSummaryDto,
  BackendWorkflowTaskDependencyDto,
  BackendWorkflowTaskDto,
  BackendWorkflowTaskMetricsDto,
  BackendPersonWorkflowHistoryDto,
  BackendPersonWorkflowSummaryDto,
} from "./backendDtos";

export type * from "./backendDtos";

function toIconKey(iconKey?: string | null): string {
  return coerceIconKey(iconKey);
}

function normalizeStatus(status: string): string {
  return status.trim().toLowerCase();
}

function toWorkflowRuntimeStatus(status: string): WorkflowRuntimeStatus {
  const normalized = normalizeStatus(status);

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
  const normalized = normalizeStatus(status);

  switch (normalized) {
    case "open":
    case "ready":
    case "in_progress":
    case "blocked":
    case "done":
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

function toWorkflowTaskSlaStatus(status: string): WorkflowTaskSlaStatus {
  switch (normalizeStatus(status)) {
    case "on_track":
    case "due_today":
    case "overdue":
      return normalizeStatus(status) as WorkflowTaskSlaStatus;
    default:
      return "none";
  }
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

function mapWorkflowRequirementSelectedOption(
  dto: BackendWorkflowRequirementSelectedOptionDto
): WorkflowRequirementSelectedOption {
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
    isVisible: dto.isVisible,
    sortOrder: dto.sortOrder,
    behavior: mapRequirementBehavior(dto.behavior),
    options: dto.options.map(mapWorkflowRequirementOption),
    value: mapWorkflowRequirementValue(dto.value),
  };
}

function mapWorkflowNotification(dto: BackendWorkflowNotificationDto): WorkflowNotification {
  return dto;
}

export function mapWorkflowAuditEntry(dto: BackendWorkflowAuditEntryDto): WorkflowAuditEntry {
  return { ...dto };
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

function mapWorkflowTaskComment(dto: BackendWorkflowTaskCommentDto): WorkflowTaskComment {
  return { ...dto };
}

export function mapWorkflowTask(dto: BackendWorkflowTaskDto): WorkflowTask {
  return {
    ...dto,
    iconKey: toIconKey(dto.iconKey),
    status: toWorkflowTaskStatus(dto.status),
    slaStatus: toWorkflowTaskSlaStatus(dto.slaStatus),
    processArea: toWorkflowTaskArea(dto.processArea),
    assignments: dto.assignments.map(mapWorkflowTaskAssignment),
    dependencies: dto.dependencies.map(mapWorkflowTaskDependency),
    comments: dto.comments.map(mapWorkflowTaskComment),
  };
}

function mapProcessType(dto: BackendProcessTypeDto): ProcessType {
  return {
    key: dto.key,
    name: dto.name,
    description: dto.description ?? null,
    requiresTargetPerson: dto.requiresTargetPerson,
  };
}

export function mapWorkflowSummary(dto: BackendWorkflowSummaryDto): WorkflowSummary {
  const workflowStatus = normalizeStatus(dto.workflowStatus);

  return {
    uid: dto.uid,
    processType: mapProcessType(dto.processType),
    firstName: dto.firstName,
    lastName: dto.lastName,
    employeeNumber: dto.employeeNumber,
    badgeNumber: dto.badgeNumber,
    departmentId: dto.departmentId,
    departmentName: dto.departmentName,
    roleId: dto.roleId,
    roleName: dto.roleName,
    status: toWorkflowLegacyStatus(dto.status),
    workflowStatus: toWorkflowRuntimeStatus(workflowStatus),
    createdAt: dto.createdAt,
    deadlineDate: dto.deadlineDate,
    archivedAt: dto.archivedAt,
    pendingNotifications: dto.pendingNotifications,
    failedNotifications: dto.failedNotifications,
    requirementSummary: mapWorkflowRequirementSummary(dto.requirementSummary),
    taskMetrics: mapWorkflowTaskMetrics(dto.taskMetrics),
    taskSummary: dto.taskSummary,
    responsibilityOptions: dto.responsibilityOptions.map((option) => ({ ...option })),
  };
}

export function mapWorkflowPage(dto: BackendWorkflowPageDto): WorkflowPage {
  return {
    items: dto.items.map(mapWorkflowSummary),
    count: dto.count,
    offset: dto.offset,
    limit: dto.limit,
    departmentOptions: dto.departmentOptions,
    responsibilityOptions: dto.responsibilityOptions.map((option) => ({ ...option })),
  };
}

export function mapWorkflowDetail(dto: BackendWorkflowDetailDto): WorkflowDetail {
  const workflowStatus = normalizeStatus(dto.workflowStatus);

  return {
    uid: dto.uid,
    processType: mapProcessType(dto.processType),
    firstName: dto.firstName,
    lastName: dto.lastName,
    employeeNumber: dto.employeeNumber,
    badgeNumber: dto.badgeNumber,
    departmentId: dto.departmentId,
    departmentName: dto.departmentName,
    roleId: dto.roleId,
    roleName: dto.roleName,
    status: toWorkflowLegacyStatus(dto.status),
    workflowStatus: toWorkflowRuntimeStatus(workflowStatus),
    createdAt: dto.createdAt,
    deadlineDate: dto.deadlineDate,
    archivedAt: dto.archivedAt,
    targetPersonId: dto.targetPersonId,
    requirements: dto.requirements.map(mapWorkflowRequirement),
    requirementSummary: mapWorkflowRequirementSummary(dto.requirementSummary),
    tasks: dto.tasks.map(mapWorkflowTask),
    taskMetrics: mapWorkflowTaskMetrics(dto.taskMetrics),
    taskAreas: dto.taskAreas.map(mapWorkflowTaskAreaSummary),
    notifications: dto.notifications.map(mapWorkflowNotification),
  };
}

function mapTaskWorkflowContext(dto: BackendTaskWorkflowContextDto): TaskWorkflowContext {
  const workflowStatus = normalizeStatus(dto.workflowStatus);

  return {
    workflowId: dto.workflowId,
    workflowUid: dto.workflowUid,
    workflowStatus: toWorkflowRuntimeStatus(workflowStatus),
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

function mapPersonWorkflowSummary(dto: BackendPersonWorkflowSummaryDto): PersonWorkflowSummary {
  const workflowStatus = normalizeStatus(dto.workflowStatus);
  return {
    uid: dto.uid,
    processType: mapProcessType(dto.processType),
    firstName: dto.firstName,
    lastName: dto.lastName,
    roleName: dto.roleName,
    departmentName: dto.departmentName,
    status: toWorkflowLegacyStatus(dto.status),
    workflowStatus: toWorkflowRuntimeStatus(workflowStatus),
    createdAt: dto.createdAt,
    completedAt: dto.completedAt,
    archivedAt: dto.archivedAt,
  };
}

export function mapPersonWorkflowHistory(dto: BackendPersonWorkflowHistoryDto): PersonWorkflowHistory {
  return {
    personId: dto.personId,
    displayName: dto.displayName,
    departmentId: dto.departmentId,
    departmentName: dto.departmentName,
    employeeNumber: dto.employeeNumber,
    badgeNumber: dto.badgeNumber,
    firstName: dto.firstName,
    lastName: dto.lastName,
    workflows: dto.workflows.map(mapPersonWorkflowSummary),
  };
}
