import type { CreatePersonPayload, PersonDirectoryItem, PersonWorkflowHistory, WorkflowTargetPerson } from "../types/workflow";
import type { ImportPeopleFromDirectoryResult, UnlinkedDirectoryIdentity } from "../types/auth";
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

// A2: Entra-Identitaeten ohne people-Record — Basis fuer retroaktiven Import.
export async function getUnlinkedDirectoryIdentities(
  onlyEnabled: boolean,
  limit = 500
): Promise<AdminListPage<UnlinkedDirectoryIdentity>> {
  const params = new URLSearchParams({ onlyEnabled: String(onlyEnabled), limit: String(limit) });
  return requestJson<AdminListPage<UnlinkedDirectoryIdentity>>(
    `/admin/directory/unlinked-identities?${params.toString()}`
  );
}

// A2: Legt people-Records fuer die gewaehlten Entra-Identitaeten an.
export async function importPeopleFromDirectory(
  directoryIdentityIds: number[]
): Promise<ImportPeopleFromDirectoryResult> {
  return requestJson<ImportPeopleFromDirectoryResult>("/admin/people/import-from-directory", {
    method: "POST",
    body: { directoryIdentityIds },
  });
}
