import type { CreatePersonPayload, PersonDirectoryItem, PersonWorkflowHistory, WorkflowTargetPerson } from "../types/workflow";
import { encodeId, requestJson } from "./api/client";
import type { BackendPersonWorkflowHistoryDto, BackendWorkflowTargetPersonDto } from "./api/backendDtos";
import { mapPersonWorkflowHistory } from "./api/mappers";
import { buildAdminListQuery, type AdminListPage, type AdminListQueryOptions } from "./api/adminList";

export async function getPersonWorkflowHistory(personId: number): Promise<PersonWorkflowHistory> {
  const data = await requestJson<BackendPersonWorkflowHistoryDto>(`/people/${encodeId(personId)}/workflows`);
  return mapPersonWorkflowHistory(data);
}

export async function searchPeople(
  query?: string,
  limit = 20
): Promise<WorkflowTargetPerson[]> {
  const params = new URLSearchParams({ limit: String(limit) });
  if (query && query.trim()) {
    params.set("query", query.trim());
  }

  return requestJson<BackendWorkflowTargetPersonDto[]>(`/people/search?${params.toString()}`);
}

export async function searchRotationEligiblePeople(
  query?: string,
  limit = 20
): Promise<WorkflowTargetPerson[]> {
  const params = new URLSearchParams({ limit: String(limit) });
  if (query && query.trim()) {
    params.set("query", query.trim());
  }

  return requestJson<BackendWorkflowTargetPersonDto[]>(`/people/rotation-eligible?${params.toString()}`);
}

export async function createPerson(payload: CreatePersonPayload): Promise<WorkflowTargetPerson> {
  return requestJson<BackendWorkflowTargetPersonDto>("/people", {
    method: "POST",
    body: payload,
  });
}

export async function getPeopleDirectory(
  options: AdminListQueryOptions = {}
): Promise<AdminListPage<PersonDirectoryItem>> {
  return requestJson<AdminListPage<PersonDirectoryItem>>(
    `/admin/people${buildAdminListQuery(options)}`
  );
}
