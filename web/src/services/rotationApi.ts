import type {
  CreateRotationPlanPayload,
  DepartmentActionTemplate,
  DepartmentActionTemplateUpsertPayload,
  RotationAuditEntry,
  RotationEligiblePerson,
  RotationGeneratedTask,
  RotationNotification,
  RotationPlanDetail,
  RotationPlanListItem,
  RotationStation,
  RotationStationUpsertPayload,
  RotationTaskRegenerationResult,
} from "../types/rotation";
import { encodeId, requestJson } from "./api/client";
import { buildAdminListQuery, type AdminListPage } from "./api/adminList";

function buildSearchQuery(search?: string, limit = 20): string {
  const params = new URLSearchParams({ limit: String(limit) });
  if (search && search.trim()) {
    params.set("query", search.trim());
  }

  return params.toString();
}

export async function searchRotationEligiblePeople(
  search?: string,
  limit = 20
): Promise<RotationEligiblePerson[]> {
  return requestJson<RotationEligiblePerson[]>(`/people/rotation-eligible?${buildSearchQuery(search, limit)}`);
}

export async function getRotationPlans(personId?: number | null): Promise<RotationPlanListItem[]> {
  const params = new URLSearchParams();
  if (typeof personId === "number" && personId > 0) {
    params.set("personId", String(personId));
  }

  const query = params.toString();
  return requestJson<RotationPlanListItem[]>(`/rotation/plans${query ? `?${query}` : ""}`);
}

export async function getRotationPlan(planId: number): Promise<RotationPlanDetail> {
  return requestJson<RotationPlanDetail>(`/rotation/plans/${encodeId(planId)}`);
}

export async function createRotationPlan(payload: CreateRotationPlanPayload): Promise<RotationPlanDetail> {
  return requestJson<RotationPlanDetail>("/rotation/plans", {
    method: "POST",
    body: payload,
  });
}

export async function createRotationStation(
  planId: number,
  payload: RotationStationUpsertPayload
): Promise<RotationStation> {
  return requestJson<RotationStation>(`/rotation/plans/${encodeId(planId)}/stations`, {
    method: "POST",
    body: payload,
  });
}

export async function updateRotationStation(
  stationId: number,
  payload: RotationStationUpsertPayload
): Promise<RotationStation> {
  return requestJson<RotationStation>(`/rotation/stations/${encodeId(stationId)}`, {
    method: "PUT",
    body: payload,
  });
}

export async function deleteRotationStation(stationId: number): Promise<void> {
  await requestJson<void>(`/rotation/stations/${encodeId(stationId)}`, {
    method: "DELETE",
  });
}

export async function getRotationGeneratedTasks(planId: number): Promise<RotationGeneratedTask[]> {
  return requestJson<RotationGeneratedTask[]>(
    `/rotation/plans/${encodeId(planId)}/generated-tasks`
  );
}

export async function getRotationAuditLog(
  planId: number,
  limit = 50,
  offset = 0
): Promise<RotationAuditEntry[]> {
  const params = new URLSearchParams({
    limit: String(limit),
    offset: String(offset),
  });
  return requestJson<RotationAuditEntry[]>(
    `/rotation/plans/${encodeId(planId)}/audit?${params.toString()}`
  );
}

export async function getRotationNotifications(
  planId: number,
  limit = 50,
  offset = 0
): Promise<RotationNotification[]> {
  const params = new URLSearchParams({
    limit: String(limit),
    offset: String(offset),
  });
  return requestJson<RotationNotification[]>(
    `/rotation/plans/${encodeId(planId)}/notifications?${params.toString()}`
  );
}

export async function regenerateRotationGeneratedTasks(
  planId: number
): Promise<RotationTaskRegenerationResult> {
  return requestJson<RotationTaskRegenerationResult>(
    `/rotation/plans/${encodeId(planId)}/generated-tasks/regenerate`,
    {
      method: "POST",
    }
  );
}

export async function getAdminRotationTemplates(
  departmentId?: number | null,
  isActive?: boolean | null
): Promise<DepartmentActionTemplate[]> {
  const params = new URLSearchParams(buildAdminListQuery({ limit: 200 }).slice(1));
  if (typeof departmentId === "number") {
    params.set("departmentId", String(departmentId));
  }
  if (typeof isActive === "boolean") {
    params.set("isActive", String(isActive));
  }
  const query = params.toString();
  const page = await requestJson<AdminListPage<DepartmentActionTemplate>>(
    `/admin/rotation/action-templates${query ? `?${query}` : ""}`
  );
  return page.items;
}

export async function getAdminRotationTemplate(templateId: number): Promise<DepartmentActionTemplate> {
  return requestJson<DepartmentActionTemplate>(
    `/admin/rotation/action-templates/${encodeId(templateId)}`
  );
}

export async function createAdminRotationTemplate(
  payload: DepartmentActionTemplateUpsertPayload
): Promise<DepartmentActionTemplate> {
  return requestJson<DepartmentActionTemplate>("/admin/rotation/action-templates", {
    method: "POST",
    body: payload,
  });
}

export async function updateAdminRotationTemplate(
  templateId: number,
  payload: DepartmentActionTemplateUpsertPayload
): Promise<DepartmentActionTemplate> {
  return requestJson<DepartmentActionTemplate>(
    `/admin/rotation/action-templates/${encodeId(templateId)}`,
    {
      method: "PUT",
      body: payload,
    }
  );
}

export async function deleteAdminRotationTemplate(templateId: number): Promise<void> {
  await requestJson<void>(
    `/admin/rotation/action-templates/${encodeId(templateId)}`,
    { method: "DELETE" }
  );
}
