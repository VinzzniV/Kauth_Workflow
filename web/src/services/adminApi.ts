import type {
  AdminDepartmentAssignment,
  AdminGraphApplicationConfiguration,
  AdminGroup,
  AdminNotificationEmailConfiguration,
  AdminNotificationEmailTestResponse,
  AdminNotificationTemplate,
  AdminNotificationTemplatePreviewResponse,
  AdminNotificationTemplateRotationPlanPreviewTarget,
  AdminNotificationTemplateWorkflowPreviewTarget,
  AdminPermission,
  AdminPermissionAuditEntry,
  AdminResponsibilityOwner,
  AdminRole,
  AdminRuntimeHealth,
  AdminSystemLogEntry,
  AdminSystemLogSummary,
  AdminUser,
} from "../types/auth";
import type { WorkflowConfig } from "../types/workflow";
import type { SystemConfigSnapshot } from "../types/systemConfigSnapshot";
import { encodeId, requestJson } from "./api/client";
import { buildAdminListQuery, type AdminListPage, type AdminListQueryOptions } from "./api/adminList";
import { buildCursorPageQuery, type CursorPage, type CursorPageQueryOptions } from "./api/cursorPage";
import { buildLookupQuery } from "./api/lookupPage";
import type {
  BackendAdminDepartmentAssignmentDto,
  BackendAdminGraphApplicationConfigurationDto,
  BackendAdminGroupDto,
  BackendAdminNotificationEmailConfigurationDto,
  BackendAdminNotificationEmailTestResponseDto,
  BackendAdminNotificationTemplateDto,
  BackendAdminNotificationTemplatePreviewResponseDto,
  BackendAdminNotificationTemplateRotationPlanPreviewTargetDto,
  BackendAdminNotificationTemplateWorkflowPreviewTargetDto,
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
  cursor?: string | null;
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

  if (options.cursor?.trim()) {
    params.set("cursor", options.cursor.trim());
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

export async function getAdminNotificationTemplates(): Promise<AdminNotificationTemplate[]> {
  const page = await requestJson<AdminListPage<BackendAdminNotificationTemplateDto>>(
    `/admin/notification-templates${buildAdminListQuery({ limit: 200 })}`
  );
  return page.items;
}

export async function updateAdminNotificationTemplate(
  templateKey: string,
  payload: {
    subjectTemplate: string;
    bodyTemplate: string;
  }
): Promise<AdminNotificationTemplate> {
  return requestJson<BackendAdminNotificationTemplateDto>(`/admin/notification-templates/${encodeURIComponent(templateKey)}`, {
    method: "PUT",
    body: payload,
  });
}

export async function searchAdminNotificationTemplateWorkflows(
  search: string,
  limit = 20
): Promise<AdminNotificationTemplateWorkflowPreviewTarget[]> {
  return requestJson<BackendAdminNotificationTemplateWorkflowPreviewTargetDto[]>(
    `/admin/notification-templates/preview-targets/workflows${buildLookupQuery({ search, limit })}`
  );
}

export async function searchAdminNotificationTemplateRotationPlans(
  search: string,
  limit = 20
): Promise<AdminNotificationTemplateRotationPlanPreviewTarget[]> {
  return requestJson<BackendAdminNotificationTemplateRotationPlanPreviewTargetDto[]>(
    `/admin/notification-templates/preview-targets/rotation-plans${buildLookupQuery({ search, limit })}`
  );
}

export async function previewAdminNotificationTemplate(
  templateKey: string,
  payload: {
    workflowUid?: string | null;
    rotationPlanId?: number | null;
  }
): Promise<AdminNotificationTemplatePreviewResponse> {
  return requestJson<BackendAdminNotificationTemplatePreviewResponseDto>(
    `/admin/notification-templates/${encodeURIComponent(templateKey)}/preview`,
    {
      method: "POST",
      body: payload,
    }
  );
}

export async function getAdminSystemLogs(options: AdminSystemLogQueryOptions): Promise<CursorPage<AdminSystemLogEntry>> {
  return requestJson<CursorPage<BackendAdminSystemLogEntryDto>>(`/admin/system/logs${buildAdminSystemLogQuery(options)}`);
}

export async function getAdminSystemLogSummary(options: Omit<AdminSystemLogQueryOptions, "limit" | "cursor">): Promise<AdminSystemLogSummary> {
  return requestJson<BackendAdminSystemLogSummaryDto>(
    `/admin/system/logs/summary${buildAdminSystemLogQuery(options)}`
  );
}

export async function getAdminSystemConfigSnapshot(): Promise<SystemConfigSnapshot> {
  return requestJson<SystemConfigSnapshot>("/admin/system/config");
}

export async function getAdminUsers(): Promise<AdminUser[]> {
  const page = await requestJson<AdminListPage<BackendAdminUserDto>>(
    `/admin/auth/users${buildAdminListQuery({ limit: 200 })}`
  );
  return page.items;
}

export async function getAdminRoles(): Promise<AdminRole[]> {
  const page = await requestJson<AdminListPage<BackendAdminRoleDto>>(
    `/admin/auth/roles${buildAdminListQuery({ limit: 200 })}`
  );
  return page.items;
}

export async function getAdminGroups(): Promise<AdminGroup[]> {
  const page = await requestJson<AdminListPage<BackendAdminGroupDto>>(
    `/admin/auth/groups${buildAdminListQuery({ limit: 200 })}`
  );
  return page.items;
}

export async function getAdminPermissions(): Promise<AdminPermission[]> {
  const page = await requestJson<AdminListPage<BackendAdminPermissionDto>>(
    `/admin/auth/permissions${buildAdminListQuery({ limit: 200 })}`
  );
  return page.items;
}

export async function getAdminPermissionAudit(options: CursorPageQueryOptions = {}): Promise<CursorPage<AdminPermissionAuditEntry>> {
  const qs = buildCursorPageQuery(options);
  return requestJson<CursorPage<AdminPermissionAuditEntry>>(`/admin/auth/audit${qs}`);
}

export async function getAdminDepartmentAssignments(
  options: AdminListQueryOptions = {}
): Promise<AdminListPage<AdminDepartmentAssignment>> {
  return requestJson<AdminListPage<BackendAdminDepartmentAssignmentDto>>(
    `/admin/master-data/departments${buildAdminListQuery(options)}`
  );
}

export async function getAdminDepartmentPositions(
  options: AdminListQueryOptions = {}
): Promise<AdminListPage<AdminRole>> {
  return requestJson<AdminListPage<BackendAdminRoleDto>>(
    `/admin/master-data/positions${buildAdminListQuery(options)}`
  );
}

export async function getAdminResponsibilityOwners(
  options: AdminListQueryOptions = {}
): Promise<AdminListPage<AdminResponsibilityOwner>> {
  return requestJson<AdminListPage<BackendAdminResponsibilityOwnerDto>>(
    `/admin/master-data/responsibilities${buildAdminListQuery(options)}`
  );
}

export async function createAdminDepartment(departmentName: string): Promise<AdminDepartmentAssignment> {
  return requestJson<BackendAdminDepartmentAssignmentDto>("/admin/master-data/departments", {
    method: "POST",
    body: { departmentName },
  });
}

export async function deleteAdminDepartment(departmentId: number): Promise<void> {
  await requestJson<void>(`/admin/master-data/departments/${encodeId(departmentId)}`, {
    method: "DELETE",
  });
}

export async function getDepartmentEntraJobTitles(departmentId: number): Promise<string[]> {
  const result = await requestJson<{ jobTitle: string }[]>(
    `/admin/master-data/departments/${encodeId(departmentId)}/entra-job-titles`
  );
  return result.map((r) => r.jobTitle);
}

export async function importDepartmentPositionsFromEntra(
  departmentId: number,
  jobTitles: string[]
): Promise<{ created: number; skipped: number }> {
  return requestJson<{ created: number; skipped: number }>(
    `/admin/master-data/departments/${encodeId(departmentId)}/positions/import-from-entra`,
    {
      method: "POST",
      body: { jobTitles },
    }
  );
}

export async function createAdminDepartmentPosition(
  departmentId: number,
  positionName: string
): Promise<AdminRole> {
  return requestJson<BackendAdminRoleDto>(
    `/admin/master-data/departments/${encodeId(departmentId)}/positions`,
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
  return requestJson<BackendAdminRoleDto>(`/admin/master-data/positions/${encodeId(positionId)}`, {
    method: "PATCH",
    body: { positionName, isActive },
  });
}

export async function deleteAdminDepartmentPosition(positionId: number): Promise<void> {
  await requestJson<void>(`/admin/master-data/positions/${encodeId(positionId)}`, {
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
  await requestJson<void>(`/admin/master-data/responsibilities/${encodeId(responsibilityId)}`, {
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
  await requestJson<void>(`/admin/master-data/users/${encodeId(userId)}`, {
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
  return requestJson<BackendAdminUserDto>(`/admin/master-data/users/${encodeId(userId)}`, {
    method: "PATCH",
    body: { externalKey, displayName, email, notificationEmail, departmentId, isActive },
  });
}

export async function updateAdminUserRoles(userId: number, roleIds: number[]): Promise<AdminUser> {
  return requestJson<BackendAdminUserDto>(`/admin/auth/users/${encodeId(userId)}/roles`, {
    method: "PATCH",
    body: { roleIds },
  });
}

export async function updateAdminUserGroups(userId: number, groupIds: number[]): Promise<AdminUser> {
  return requestJson<BackendAdminUserDto>(`/admin/auth/users/${encodeId(userId)}/groups`, {
    method: "PATCH",
    body: { groupIds },
  });
}

export async function updateAdminGroupRoles(groupId: number, roleIds: number[]): Promise<AdminGroup> {
  return requestJson<BackendAdminGroupDto>(`/admin/auth/groups/${encodeId(groupId)}/roles`, {
    method: "PATCH",
    body: { roleIds },
  });
}

export async function updateAdminRolePermissions(roleId: number, permissionIds: number[]): Promise<AdminRole> {
  return requestJson<BackendAdminRoleDto>(`/admin/auth/roles/${encodeId(roleId)}/permissions`, {
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
    `/admin/auth/users/${encodeId(userId)}/permission-overrides`,
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
    `/admin/master-data/departments/${encodeId(departmentId)}`,
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
    `/admin/master-data/responsibilities/${encodeId(responsibilityId)}`,
    {
      method: "PATCH",
      body: { appUserId, departmentId },
    }
  );
}

export async function getAdminRuntimeHealth(): Promise<AdminRuntimeHealth> {
  return requestJson<AdminRuntimeHealth>("/admin/runtime-health");
}
