import type {
  AdminDepartmentAssignment,
  AdminGraphApplicationConfiguration,
  AdminGroup,
  AdminNotificationEmailConfiguration,
  AdminNotificationEmailTestResponse,
  AdminPermission,
  AdminPermissionAuditEntry,
  AdminResponsibilityOwner,
  AdminRole,
  AdminUser,
} from "../types/auth";
import type { WorkflowConfig } from "../types/workflow";
import { requestJson } from "./api/client";
import type {
  BackendAdminDepartmentAssignmentDto,
  BackendAdminGraphApplicationConfigurationDto,
  BackendAdminGroupDto,
  BackendAdminNotificationEmailConfigurationDto,
  BackendAdminNotificationEmailTestResponseDto,
  BackendAdminPermissionAuditEntryDto,
  BackendAdminPermissionDto,
  BackendAdminResponsibilityOwnerDto,
  BackendAdminRoleDto,
  BackendAdminUserDto,
  BackendWorkflowConfigDto,
} from "./api/backendDtos";
import { mapWorkflowConfig } from "./api/mappers";

export async function getAdminWorkflowConfig(): Promise<WorkflowConfig> {
  const data = await requestJson<BackendWorkflowConfigDto>("/admin/config/workflow");
  return mapWorkflowConfig(data);
}

export async function getAdminNotificationEmailConfiguration(): Promise<AdminNotificationEmailConfiguration> {
  return requestJson<BackendAdminNotificationEmailConfigurationDto>("/admin/config/notification-email");
}

export async function getAdminGraphApplicationConfiguration(): Promise<AdminGraphApplicationConfiguration> {
  return requestJson<BackendAdminGraphApplicationConfigurationDto>("/admin/config/graph-application");
}

export async function updateAdminNotificationEmailConfiguration(payload: {
  enabled: boolean;
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

export async function getAdminPermissions(): Promise<AdminPermission[]> {
  return requestJson<BackendAdminPermissionDto[]>("/admin/auth/permissions");
}

export async function getAdminPermissionAudit(limit = 100): Promise<AdminPermissionAuditEntry[]> {
  const params = new URLSearchParams({ limit: String(limit) });
  return requestJson<BackendAdminPermissionAuditEntryDto[]>(`/admin/auth/audit?${params.toString()}`);
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

export async function updateAdminRolePermissions(roleId: number, permissionIds: number[]): Promise<AdminRole> {
  return requestJson<BackendAdminRoleDto>(`/admin/auth/roles/${encodeURIComponent(String(roleId))}/permissions`, {
    method: "PATCH",
    body: { permissionIds },
  });
}

export async function updateAdminUserPermissionOverrides(
  userId: number,
  overrides: Array<{
    permissionId: number;
    effect: string;
    scope: string;
    scopeDepartmentId: number | null;
  }>
): Promise<AdminUser> {
  return requestJson<BackendAdminUserDto>(
    `/admin/auth/users/${encodeURIComponent(String(userId))}/permission-overrides`,
    {
      method: "PATCH",
      body: { overrides },
    }
  );
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
