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
  AdminSystemLogEntry,
  AdminSystemLogSummary,
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
  BackendAdminSystemLogEntryDto,
  BackendAdminSystemLogSummaryDto,
  BackendAdminUserDto,
  BackendWorkflowConfigDto,
} from "./api/backendDtos";
import { mapWorkflowConfig } from "./api/mappers";

export type AdminSystemLogQueryOptions = {
  severity?: string[];
  source?: string | null;
  since?: string | null;
  until?: string | null;
  search?: string | null;
  actorUserId?: number | null;
  workflowUid?: string | null;
  rotationPlanId?: number | null;
  taskRef?: string | null;
  limit?: number | null;
  offset?: number | null;
};

function buildAdminSystemLogQuery(options: AdminSystemLogQueryOptions): string {
  const params = new URLSearchParams();

  if ((options.severity ?? []).length > 0) {
    params.set("severity", options.severity!.join(","));
  }

  if (options.source?.trim()) {
    params.set("source", options.source.trim());
  }

  if (options.since?.trim()) {
    params.set("since", options.since.trim());
  }

  if (options.until?.trim()) {
    params.set("until", options.until.trim());
  }

  if (options.search?.trim()) {
    params.set("search", options.search.trim());
  }

  if (options.actorUserId) {
    params.set("actorUserId", String(options.actorUserId));
  }

  if (options.workflowUid?.trim()) {
    params.set("workflowUid", options.workflowUid.trim());
  }

  if (options.rotationPlanId) {
    params.set("rotationPlanId", String(options.rotationPlanId));
  }

  if (options.taskRef?.trim()) {
    params.set("taskRef", options.taskRef.trim());
  }

  if (options.limit !== null && options.limit !== undefined) {
    params.set("limit", String(options.limit));
  }

  if (options.offset !== null && options.offset !== undefined) {
    params.set("offset", String(options.offset));
  }

  const query = params.toString();
  return query ? `?${query}` : "";
}

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

export async function getAdminSystemLogs(options: AdminSystemLogQueryOptions): Promise<AdminSystemLogEntry[]> {
  return requestJson<BackendAdminSystemLogEntryDto[]>(`/admin/system/logs${buildAdminSystemLogQuery(options)}`);
}

export async function getAdminSystemLogSummary(options: Omit<AdminSystemLogQueryOptions, "limit" | "offset">): Promise<AdminSystemLogSummary> {
  return requestJson<BackendAdminSystemLogSummaryDto>(
    `/admin/system/logs/summary${buildAdminSystemLogQuery(options)}`
  );
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

export async function getAdminDepartmentPositions(): Promise<AdminRole[]> {
  return requestJson<BackendAdminRoleDto[]>("/admin/master-data/positions");
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

export async function createAdminDepartmentPosition(
  departmentId: number,
  positionName: string
): Promise<AdminRole> {
  return requestJson<BackendAdminRoleDto>(
    `/admin/master-data/departments/${encodeURIComponent(String(departmentId))}/positions`,
    {
      method: "POST",
      body: { positionName },
    }
  );
}

export async function updateAdminDepartmentPosition(
  positionId: number,
  positionName: string,
  isActive: boolean
): Promise<AdminRole> {
  return requestJson<BackendAdminRoleDto>(`/admin/master-data/positions/${encodeURIComponent(String(positionId))}`, {
    method: "PATCH",
    body: { positionName, isActive },
  });
}

export async function deleteAdminDepartmentPosition(positionId: number): Promise<void> {
  await requestJson<unknown>(`/admin/master-data/positions/${encodeURIComponent(String(positionId))}`, {
    method: "DELETE",
  });
}

export async function createAdminResponsibility(
  responsibilityName: string,
  departmentId: number | null
): Promise<AdminResponsibilityOwner> {
  return requestJson<BackendAdminResponsibilityOwnerDto>("/admin/master-data/responsibilities", {
    method: "POST",
    body: { responsibilityName, departmentId },
  });
}

export async function deleteAdminResponsibility(responsibilityId: number): Promise<void> {
  await requestJson<unknown>(`/admin/master-data/responsibilities/${encodeURIComponent(String(responsibilityId))}`, {
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
