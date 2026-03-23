// Zentrale Frontend-Schnittstelle zur Onboarding-API inklusive Mapping zwischen Backend-DTOs und UI-Typen.
import type {
  RequirementSelectionPayload,
  WorkflowConfig,
  WorkflowCreationPayload,
  WorkflowCreationResponse,
  WorkflowAuditEntry,
  WorkflowDetail,
  WorkflowRequirementSnapshot,
  WorkflowSummary,
  WorkflowTask,
  WorkflowTaskStatus,
  Department,
  Role,
  TaskWithWorkflow,
} from "../types/workflow";
import type {
  AdminDepartmentAssignment,
  AdminGroup,
  AdminNotificationEmailConfiguration,
  AdminNotificationEmailTestResponse,
  AdminResponsibilityOwner,
  AdminRole,
  AdminUser,
  DemoLoginResponse,
  DemoLoginUserOption,
  Me,
} from "../types/auth";
import { getDemoAuthToken, requestJson, setDemoAuthToken } from "./onboardingApi/client";
import {
  mapTaskWithWorkflow,
  mapWorkflowAuditEntry,
  mapWorkflowConfig,
  mapWorkflowDetail,
  mapWorkflowRequirement,
  mapWorkflowSummary,
  mapWorkflowTask,
  type BackendAdminDepartmentAssignmentDto,
  type BackendAdminGroupDto,
  type BackendAdminNotificationEmailConfigurationDto,
  type BackendAdminNotificationEmailTestResponseDto,
  type BackendAdminResponsibilityOwnerDto,
  type BackendAdminRoleDto,
  type BackendAdminUserDto,
  type BackendDemoLoginUserOptionDto,
  type BackendDepartmentDto,
  type BackendMeDto,
  type BackendRoleDto,
  type BackendTaskWithWorkflowDto,
  type BackendWorkflowAuditEntryDto,
  type BackendWorkflowConfigDto,
  type BackendWorkflowDetailDto,
  type BackendWorkflowRequirementSnapshotDto,
  type BackendWorkflowSummaryDto,
  type BackendWorkflowTaskDto,
} from "./onboardingApi/mappers";

type BackendDemoLoginResponseDto = {
  token: string;
  expiresAtUtc: string;
  user: BackendMeDto;
};

export { getDemoAuthToken, setDemoAuthToken };

export async function getRoles(): Promise<Role[]> {
  return requestJson<BackendRoleDto[]>("/roles");
}

export async function getDepartments(): Promise<Department[]> {
  return requestJson<BackendDepartmentDto[]>("/departments");
}

export async function getWorkflowConfig(roleId?: number | null): Promise<WorkflowConfig> {
  const query = typeof roleId === "number" ? `?roleId=${encodeURIComponent(roleId)}` : "";
  const data = await requestJson<BackendWorkflowConfigDto>(`/workflow-config${query}`);
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

export async function getWorkflowAuditLog(uid: string): Promise<WorkflowAuditEntry[]> {
  const data = await requestJson<BackendWorkflowAuditEntryDto[]>(`/workflows/${encodeURIComponent(uid)}/audit-log`);
  return data.map(mapWorkflowAuditEntry);
}

export async function getWorkflows(): Promise<WorkflowSummary[]> {
  const data = await requestJson<BackendWorkflowSummaryDto[]>("/workflows");
  return data.map(mapWorkflowSummary);
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
