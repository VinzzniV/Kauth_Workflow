// Zentrale Frontend-Schnittstelle zur Lifecycle-API inklusive Mapping zwischen Backend-DTOs und UI-Typen.
import type {
  ProcessType,
  RequirementSelectionPayload,
  WorkflowConfig,
  WorkflowCreationPayload,
  WorkflowCreationResponse,
  WorkflowAuditEntry,
  WorkflowDetail,
  WorkflowPage,
  WorkflowRequirementSnapshot,
  WorkflowRuntimeStatus,
  WorkflowSummary,
  WorkflowTask,
  WorkflowTaskStatus,
  Department,
  Role,
  TaskWithWorkflow,
  WorkflowLink,
  LinkableWorkflow,
  WorkflowTargetPerson,
  DerivedAnswer,
  BulkDepartmentChangePayload,
  BulkOperationResult,
} from "../types/workflow";
import type {
  AdminDepartmentAssignment,
  AdminGroup,
  AdminNotificationEmailConfiguration,
  AdminNotificationEmailTestResponse,
  AdminAnswerDefinition,
  AdminDependencyGraph,
  AdminRoleAnswerDefault,
  AdminProcessType,
  AdminTaskTemplateCondition,
  AdminTaskTemplateDependency,
  AdminTaskTemplate,
  AdminResponsibilityOwner,
  AdminRole,
  AdminUser,
  DemoLoginResponse,
  DemoLoginUserOption,
  Me,
} from "../types/auth";
import { getDemoAuthToken, requestJson, setDemoAuthToken } from "./api/client";
import {
  mapTaskWithWorkflow,
  mapWorkflowAuditEntry,
  mapWorkflowConfig,
  mapWorkflowDetail,
  mapWorkflowPage,
  mapWorkflowRequirement,
  mapWorkflowSummary,
  mapWorkflowTask,
} from "./api/mappers";
import type {
  BackendAdminDepartmentAssignmentDto,
  BackendAdminGroupDto,
  BackendAdminNotificationEmailConfigurationDto,
  BackendAdminNotificationEmailTestResponseDto,
  BackendAdminAnswerDefinitionDto,
  BackendAdminDependencyGraphDto,
  BackendAdminRoleAnswerDefaultDto,
  BackendAdminProcessTypeDto,
  BackendAdminTaskTemplateConditionDto,
  BackendAdminTaskTemplateDependencyDto,
  BackendAdminTaskTemplateDto,
  BackendAdminResponsibilityOwnerDto,
  BackendAdminRoleDto,
  BackendAdminUserDto,
  BackendDemoLoginUserOptionDto,
  BackendDepartmentDto,
  BackendMeDto,
  BackendProcessTypeDto,
  BackendRoleDto,
  BackendTaskWithWorkflowDto,
  BackendWorkflowAuditEntryDto,
  BackendWorkflowConfigDto,
  BackendWorkflowDetailDto,
  BackendWorkflowPageDto,
  BackendWorkflowRequirementSnapshotDto,
  BackendWorkflowSummaryDto,
  BackendWorkflowTaskDto,
  BackendWorkflowLinkDto,
  BackendLinkableWorkflowDto,
  BackendWorkflowTargetPersonDto,
  BackendDerivedAnswerDto,
} from "./api/backendDtos";

type BackendDemoLoginResponseDto = {
  token: string;
  expiresAtUtc: string;
  user: BackendMeDto;
};

export { getDemoAuthToken, setDemoAuthToken };

export type WorkflowQueryOptions = {
  status?: WorkflowRuntimeStatus | null;
  departmentId?: number | null;
  processTypeKey?: string | null;
  search?: string | null;
  responsibilityValue?: string | null;
};

function buildWorkflowQuery(options: WorkflowQueryOptions & { limit?: number | null; offset?: number | null }): string {
  const params = new URLSearchParams();

  if (options.status) {
    params.set("status", options.status);
  }

  if (typeof options.departmentId === "number") {
    params.set("department", String(options.departmentId));
  }

  if (options.processTypeKey && options.processTypeKey.trim()) {
    params.set("processTypeKey", options.processTypeKey.trim());
  }

  if (options.search && options.search.trim()) {
    params.set("search", options.search.trim());
  }

  if (options.responsibilityValue && options.responsibilityValue.trim()) {
    params.set("responsibility", options.responsibilityValue.trim());
  }

  if (typeof options.limit === "number") {
    params.set("limit", String(options.limit));
  }

  if (typeof options.offset === "number") {
    params.set("offset", String(options.offset));
  }

  const query = params.toString();
  return query ? `?${query}` : "";
}

export async function getRoles(): Promise<Role[]> {
  return requestJson<BackendRoleDto[]>("/roles");
}

export async function getProcessTypes(): Promise<ProcessType[]> {
  return requestJson<BackendProcessTypeDto[]>("/process-types");
}

export async function getDepartments(): Promise<Department[]> {
  return requestJson<BackendDepartmentDto[]>("/departments");
}

export async function getWorkflowConfig(roleId?: number | null, processTypeKey?: string | null): Promise<WorkflowConfig> {
  const params = new URLSearchParams();
  if (typeof roleId === "number") {
    params.set("roleId", String(roleId));
  }
  if (processTypeKey && processTypeKey.trim()) {
    params.set("processTypeKey", processTypeKey.trim());
  }
  const query = params.toString();
  const data = await requestJson<BackendWorkflowConfigDto>(`/workflow-config${query ? `?${query}` : ""}`);
  return mapWorkflowConfig(data);
}

export async function getAdminWorkflowConfig(): Promise<WorkflowConfig> {
  const data = await requestJson<BackendWorkflowConfigDto>("/admin/config/workflow");
  return mapWorkflowConfig(data);
}

export async function getAdminNotificationEmailConfiguration(): Promise<AdminNotificationEmailConfiguration> {
  return requestJson<BackendAdminNotificationEmailConfigurationDto>("/admin/config/notification-email");
}

export async function updateAdminNotificationEmailConfiguration(payload: {
  enabled: boolean;
  tenantId: string | null;
  clientId: string | null;
  clientSecret?: string | null;
  senderEmail: string | null;
  frontendBaseUrl: string;
  testRecipientEmail: string | null;
  sandboxRedirectEmail: string | null;
  notifyOnWorkflowCreated: boolean;
  notifyOnTaskReady: boolean;
  notifyOnWorkflowCompleted: boolean;
}): Promise<AdminNotificationEmailConfiguration> {
  return requestJson<BackendAdminNotificationEmailConfigurationDto>("/admin/config/notification-email", {
    method: "PATCH",
    body: payload,
  });
}

export async function sendAdminNotificationEmailTest(recipientEmail: string | null): Promise<AdminNotificationEmailTestResponse> {
  return requestJson<BackendAdminNotificationEmailTestResponseDto>("/admin/config/notification-email/test", {
    method: "POST",
    body: { recipientEmail },
  });
}

export async function getAdminUsers(): Promise<AdminUser[]> {
  return requestJson<BackendAdminUserDto[]>("/admin/auth/users");
}

export async function getAdminRoles(): Promise<AdminRole[]> {
  return requestJson<BackendAdminRoleDto[]>("/admin/auth/roles");
}

export async function getAdminGroups(): Promise<AdminGroup[]> {
  return requestJson<BackendAdminGroupDto[]>("/admin/auth/groups");
}

export async function getAdminDepartmentAssignments(): Promise<AdminDepartmentAssignment[]> {
  return requestJson<BackendAdminDepartmentAssignmentDto[]>("/admin/master-data/departments");
}

export async function getAdminResponsibilityOwners(): Promise<AdminResponsibilityOwner[]> {
  return requestJson<BackendAdminResponsibilityOwnerDto[]>("/admin/master-data/responsibilities");
}

export async function createAdminDepartment(departmentName: string): Promise<AdminDepartmentAssignment> {
  return requestJson<BackendAdminDepartmentAssignmentDto>("/admin/master-data/departments", {
    method: "POST",
    body: { departmentName },
  });
}

export async function deleteAdminDepartment(departmentId: number): Promise<void> {
  await requestJson<unknown>(`/admin/master-data/departments/${encodeURIComponent(String(departmentId))}`, {
    method: "DELETE",
  });
}

export async function createAdminUser(payload: {
  externalKey: string | null;
  displayName: string;
  email: string;
  notificationEmail: string | null;
  departmentId: number | null;
  isActive: boolean;
}): Promise<AdminUser> {
  return requestJson<BackendAdminUserDto>("/admin/master-data/users", {
    method: "POST",
    body: payload,
  });
}

export async function deleteAdminUser(userId: number): Promise<void> {
  await requestJson<unknown>(`/admin/master-data/users/${encodeURIComponent(String(userId))}`, {
    method: "DELETE",
  });
}

export async function updateAdminUserMasterData(
  userId: number,
  externalKey: string | null,
  displayName: string,
  email: string,
  notificationEmail: string | null,
  departmentId: number | null,
  isActive: boolean
): Promise<AdminUser> {
  return requestJson<BackendAdminUserDto>(`/admin/master-data/users/${encodeURIComponent(String(userId))}`, {
    method: "PATCH",
    body: { externalKey, displayName, email, notificationEmail, departmentId, isActive },
  });
}

export async function updateAdminUserRoles(userId: number, roleIds: number[]): Promise<AdminUser> {
  return requestJson<BackendAdminUserDto>(`/admin/auth/users/${encodeURIComponent(String(userId))}/roles`, {
    method: "PATCH",
    body: { roleIds },
  });
}

export async function updateAdminUserGroups(userId: number, groupIds: number[]): Promise<AdminUser> {
  return requestJson<BackendAdminUserDto>(`/admin/auth/users/${encodeURIComponent(String(userId))}/groups`, {
    method: "PATCH",
    body: { groupIds },
  });
}

export async function updateAdminGroupRoles(groupId: number, roleIds: number[]): Promise<AdminGroup> {
  return requestJson<BackendAdminGroupDto>(`/admin/auth/groups/${encodeURIComponent(String(groupId))}/roles`, {
    method: "PATCH",
    body: { roleIds },
  });
}

export async function updateAdminDepartmentAssignment(
  departmentId: number,
  departmentLeadUserId: number | null,
  requirementOwnerUserId: number | null
): Promise<AdminDepartmentAssignment> {
  return requestJson<BackendAdminDepartmentAssignmentDto>(
    `/admin/master-data/departments/${encodeURIComponent(String(departmentId))}`,
    {
      method: "PATCH",
      body: { departmentLeadUserId, requirementOwnerUserId },
    }
  );
}

export async function updateAdminResponsibilityOwner(
  responsibilityId: number,
  appUserId: number | null,
  departmentId: number | null
): Promise<AdminResponsibilityOwner> {
  return requestJson<BackendAdminResponsibilityOwnerDto>(
    `/admin/master-data/responsibilities/${encodeURIComponent(String(responsibilityId))}`,
    {
      method: "PATCH",
      body: { appUserId, departmentId },
    }
  );
}

export async function createWorkflow(payload: WorkflowCreationPayload): Promise<WorkflowCreationResponse> {
  return requestJson<WorkflowCreationResponse>("/workflows", { method: "POST", body: payload });
}

export async function getWorkflowByUid(uid: string): Promise<WorkflowDetail> {
  const data = await requestJson<BackendWorkflowDetailDto>(`/workflows/${encodeURIComponent(uid)}`);
  return mapWorkflowDetail(data);
}

export async function getWorkflowAuditLog(
  uid: string,
  limit = 200,
  offset = 0
): Promise<WorkflowAuditEntry[]> {
  const params = new URLSearchParams({
    limit: String(limit),
    offset: String(offset),
  });
  const data = await requestJson<BackendWorkflowAuditEntryDto[]>(
    `/workflows/${encodeURIComponent(uid)}/audit-log?${params.toString()}`
  );
  return data.map(mapWorkflowAuditEntry);
}

export async function getWorkflows(options: WorkflowQueryOptions = {}): Promise<WorkflowSummary[]> {
  const data = await requestJson<BackendWorkflowSummaryDto[]>(`/workflows${buildWorkflowQuery(options)}`);
  return data.map(mapWorkflowSummary);
}

export async function getWorkflowPage(
  limit: number,
  offset: number,
  options: WorkflowQueryOptions = {}
): Promise<WorkflowPage> {
  const data = await requestJson<BackendWorkflowPageDto>(
    `/workflows${buildWorkflowQuery({ ...options, limit, offset })}`
  );
  return mapWorkflowPage(data);
}

export async function getWorkflowTasks(uid: string): Promise<WorkflowTask[]> {
  const data = await requestJson<BackendWorkflowTaskDto[]>(`/workflows/${encodeURIComponent(uid)}/tasks`);
  return data.map(mapWorkflowTask);
}

export async function getMyTasks(): Promise<TaskWithWorkflow[]> {
  const data = await requestJson<BackendTaskWithWorkflowDto[]>("/tasks");
  return data.map(mapTaskWithWorkflow);
}

export async function updateTaskStatus(taskId: number, status: WorkflowTaskStatus): Promise<TaskWithWorkflow> {
  const data = await requestJson<BackendTaskWithWorkflowDto>(`/tasks/${encodeURIComponent(String(taskId))}/status`, {
    method: "PATCH",
    body: { status },
  });
  return mapTaskWithWorkflow(data);
}

export async function addTaskComment(taskId: number, commentText: string): Promise<TaskWithWorkflow> {
  const data = await requestJson<BackendTaskWithWorkflowDto>(`/tasks/${encodeURIComponent(String(taskId))}/comments`, {
    method: "POST",
    body: { commentText },
  });
  return mapTaskWithWorkflow(data);
}

export async function getSupervisorStepWorkflows(): Promise<WorkflowSummary[]> {
  const data = await requestJson<BackendWorkflowSummaryDto[]>("/workflows/supervisor-step");
  return data.map(mapWorkflowSummary);
}

export async function getWorkflowSupervisorStep(uid: string): Promise<WorkflowRequirementSnapshot[]> {
  const data = await requestJson<BackendWorkflowRequirementSnapshotDto[]>(
    `/workflows/${encodeURIComponent(uid)}/supervisor-step`
  );
  return data.map(mapWorkflowRequirement);
}

export async function updateWorkflowSupervisorStep(
  uid: string,
  requirementSelections: RequirementSelectionPayload[]
): Promise<WorkflowDetail> {
  const data = await requestJson<BackendWorkflowDetailDto>(`/workflows/${encodeURIComponent(uid)}/supervisor-step`, {
    method: "PATCH",
    body: { requirementSelections },
  });
  return mapWorkflowDetail(data);
}

export async function getDemoLoginUsers(): Promise<DemoLoginUserOption[]> {
  return requestJson<BackendDemoLoginUserOptionDto[]>("/auth/demo-users");
}

export async function demoLogin(username: string): Promise<DemoLoginResponse> {
  const data = await requestJson<BackendDemoLoginResponseDto>("/auth/demo-login", {
    method: "POST",
    body: { username },
  });

  return {
    token: data.token,
    expiresAtUtc: data.expiresAtUtc,
    user: data.user,
  };
}

export async function demoLogout(): Promise<void> {
  await requestJson<unknown>("/auth/demo-logout", { method: "POST" });
}

export async function getMe(): Promise<Me> {
  return requestJson<BackendMeDto>("/me");
}

export async function getWorkflowLinks(uid: string): Promise<WorkflowLink[]> {
  return requestJson<BackendWorkflowLinkDto[]>(`/workflows/${encodeURIComponent(uid)}/links`);
}

export async function createWorkflowLink(
  uid: string,
  payload: { sourceWorkflowUid: string; linkType: string; notes?: string }
): Promise<WorkflowLink> {
  return requestJson<BackendWorkflowLinkDto>(`/workflows/${encodeURIComponent(uid)}/links`, {
    method: "POST",
    body: payload,
  });
}

export async function deleteWorkflowLink(uid: string, linkId: number): Promise<void> {
  await requestJson<unknown>(`/workflows/${encodeURIComponent(uid)}/links/${encodeURIComponent(String(linkId))}`, {
    method: "DELETE",
  });
}

export async function findLinkableWorkflows(
  employeeNumber: number,
  excludeUid?: string
): Promise<LinkableWorkflow[]> {
  const params = new URLSearchParams({ employeeNumber: String(employeeNumber) });
  if (excludeUid) {
    params.set("excludeUid", excludeUid);
  }
  return requestJson<BackendLinkableWorkflowDto[]>(`/workflows/linkable?${params.toString()}`);
}

export async function searchWorkflowTargetPeople(
  query?: string,
  limit = 20
): Promise<WorkflowTargetPerson[]> {
  const params = new URLSearchParams({ limit: String(limit) });
  if (query && query.trim()) {
    params.set("query", query.trim());
  }
  return requestJson<BackendWorkflowTargetPersonDto[]>(`/workflow-target-people?${params.toString()}`);
}

export async function getDerivedAnswers(
  sourceUid: string,
  targetProcessTypeKey: string
): Promise<DerivedAnswer[]> {
  const params = new URLSearchParams({
    sourceUid,
    targetProcessTypeKey,
  });
  return requestJson<BackendDerivedAnswerDto[]>(`/workflows/derive-answers?${params.toString()}`);
}

export async function bulkCreateDepartmentChange(
  payload: BulkDepartmentChangePayload
): Promise<BulkOperationResult> {
  return requestJson<BulkOperationResult>("/admin/bulk/department-change", {
    method: "POST",
    body: payload,
  });
}

export async function getAdminProcessTypes(): Promise<AdminProcessType[]> {
  return requestJson<BackendAdminProcessTypeDto[]>("/admin/config/process-types");
}

export async function updateAdminProcessType(
  processTypeId: number,
  payload: {
    name?: string;
    description?: string | null;
    iconKey?: string | null;
    isActive?: boolean;
    sortOrder?: number;
  }
): Promise<AdminProcessType> {
  return requestJson<BackendAdminProcessTypeDto>(
    `/admin/config/process-types/${encodeURIComponent(String(processTypeId))}`,
    {
      method: "PATCH",
      body: payload,
    }
  );
}

export async function getAdminTaskTemplates(processTypeId: number): Promise<AdminTaskTemplate[]> {
  const params = new URLSearchParams({ processTypeId: String(processTypeId) });
  return requestJson<BackendAdminTaskTemplateDto[]>(`/admin/config/task-templates?${params.toString()}`);
}

export async function getAdminDependencyGraph(processTypeId: number): Promise<AdminDependencyGraph> {
  return requestJson<BackendAdminDependencyGraphDto>(
    `/admin/config/process-types/${encodeURIComponent(String(processTypeId))}/dependency-graph`
  );
}

export async function createAdminTaskTemplate(payload: {
  processTypeId: number;
  templateKey: string;
  title: string;
  category: string;
  description: string;
  iconKey: string | null;
  owningDepartmentId: number | null;
  defaultResponsibilityId: number | null;
  processAreaLabel: string | null;
  isDepartmentPhaseTask: boolean;
  isRequired: boolean;
  dueInDays: number | null;
  sortOrder: number;
  isActive: boolean;
}): Promise<AdminTaskTemplate> {
  return requestJson<BackendAdminTaskTemplateDto>("/admin/config/task-templates", {
    method: "POST",
    body: payload,
  });
}

export async function updateAdminTaskTemplate(
  templateId: number,
  payload: {
    processTypeId: number;
    templateKey: string;
    title: string;
    category: string;
    description: string;
    iconKey: string | null;
    owningDepartmentId: number | null;
    defaultResponsibilityId: number | null;
    processAreaLabel: string | null;
    isDepartmentPhaseTask: boolean;
    isRequired: boolean;
    dueInDays: number | null;
    sortOrder: number;
    isActive: boolean;
  }
): Promise<AdminTaskTemplate> {
  return requestJson<BackendAdminTaskTemplateDto>(
    `/admin/config/task-templates/${encodeURIComponent(String(templateId))}`,
    {
      method: "PATCH",
      body: payload,
    }
  );
}

export async function deleteAdminTaskTemplate(templateId: number): Promise<void> {
  await requestJson<unknown>(`/admin/config/task-templates/${encodeURIComponent(String(templateId))}`, {
    method: "DELETE",
  });
}

export async function getAdminTaskTemplateConditions(templateId: number): Promise<AdminTaskTemplateCondition[]> {
  return requestJson<BackendAdminTaskTemplateConditionDto[]>(
    `/admin/config/task-templates/${encodeURIComponent(String(templateId))}/conditions`
  );
}

export async function createAdminTaskTemplateCondition(
  templateId: number,
  payload: {
    conditionGroup: number;
    answerKey: string;
    operator: "eq" | "neq" | "is_true" | "is_false" | "is_null" | "is_not_null";
    expectedValueText: string | null;
    expectedValueBoolean: boolean | null;
    expectedValueNumber: number | null;
  }
): Promise<AdminTaskTemplateCondition> {
  return requestJson<BackendAdminTaskTemplateConditionDto>(
    `/admin/config/task-templates/${encodeURIComponent(String(templateId))}/conditions`,
    {
      method: "POST",
      body: payload,
    }
  );
}

export async function deleteAdminTaskTemplateCondition(templateId: number, conditionId: number): Promise<void> {
  await requestJson<unknown>(
    `/admin/config/task-templates/${encodeURIComponent(String(templateId))}/conditions/${encodeURIComponent(String(conditionId))}`,
    {
      method: "DELETE",
    }
  );
}

export async function getAdminTaskTemplateDependencies(templateId: number): Promise<AdminTaskTemplateDependency[]> {
  return requestJson<BackendAdminTaskTemplateDependencyDto[]>(
    `/admin/config/task-templates/${encodeURIComponent(String(templateId))}/dependencies`
  );
}

export async function createAdminTaskTemplateDependency(
  templateId: number,
  payload: {
    dependsOnTaskTemplateId: number;
    requiredStatus: "open" | "ready" | "in_progress" | "blocked" | "done";
  }
): Promise<AdminTaskTemplateDependency> {
  return requestJson<BackendAdminTaskTemplateDependencyDto>(
    `/admin/config/task-templates/${encodeURIComponent(String(templateId))}/dependencies`,
    {
      method: "POST",
      body: payload,
    }
  );
}

export async function deleteAdminTaskTemplateDependency(templateId: number, dependencyId: number): Promise<void> {
  await requestJson<unknown>(
    `/admin/config/task-templates/${encodeURIComponent(String(templateId))}/dependencies/${encodeURIComponent(String(dependencyId))}`,
    {
      method: "DELETE",
    }
  );
}

export async function getAdminAnswerDefinitions(processTypeId: number): Promise<AdminAnswerDefinition[]> {
  const params = new URLSearchParams({ processTypeId: String(processTypeId) });
  return requestJson<BackendAdminAnswerDefinitionDto[]>(`/admin/config/answer-definitions?${params.toString()}`);
}

export async function createAdminAnswerDefinition(payload: {
  processTypeId: number;
  answerKey: string;
  title: string;
  category: string;
  description: string;
  iconKey: string | null;
  inputType: "boolean" | "text" | "select" | "multi_select";
  isRequired: boolean;
  sortOrder: number;
  isActive: boolean;
}): Promise<AdminAnswerDefinition> {
  return requestJson<BackendAdminAnswerDefinitionDto>("/admin/config/answer-definitions", {
    method: "POST",
    body: payload,
  });
}

export async function updateAdminAnswerDefinition(
  definitionId: number,
  payload: {
    processTypeId: number;
    answerKey: string;
    title: string;
    category: string;
    description: string;
    iconKey: string | null;
    inputType: "boolean" | "text" | "select" | "multi_select";
    isRequired: boolean;
    sortOrder: number;
    isActive: boolean;
  }
): Promise<AdminAnswerDefinition> {
  return requestJson<BackendAdminAnswerDefinitionDto>(
    `/admin/config/answer-definitions/${encodeURIComponent(String(definitionId))}`,
    {
      method: "PATCH",
      body: payload,
    }
  );
}

export async function deleteAdminAnswerDefinition(definitionId: number): Promise<void> {
  await requestJson<unknown>(`/admin/config/answer-definitions/${encodeURIComponent(String(definitionId))}`, {
    method: "DELETE",
  });
}

export async function getAdminRoleAnswerDefaults(processTypeId: number): Promise<AdminRoleAnswerDefault[]> {
  const params = new URLSearchParams({ processTypeId: String(processTypeId) });
  return requestJson<BackendAdminRoleAnswerDefaultDto[]>(`/admin/config/role-answer-defaults?${params.toString()}`);
}

export async function updateAdminRoleAnswerDefaults(payload: {
  processTypeId: number;
  items: Array<{
    appRoleId: number;
    answerKey: string;
    defaultValueText: string | null;
    defaultValueBoolean: boolean | null;
  }>;
}): Promise<AdminRoleAnswerDefault[]> {
  return requestJson<BackendAdminRoleAnswerDefaultDto[]>("/admin/config/role-answer-defaults", {
    method: "PATCH",
    body: payload,
  });
}
