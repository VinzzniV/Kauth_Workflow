import type { CreatePersonPayload, PersonWorkflowHistory, WorkflowTargetPerson } from "../types/workflow";
import { requestJson } from "./api/client";
import type { BackendPersonWorkflowHistoryDto, BackendWorkflowTargetPersonDto } from "./api/backendDtos";
import { mapPersonWorkflowHistory } from "./api/mappers";

export async function getPersonWorkflowHistory(personId: number): Promise<PersonWorkflowHistory> {
  const data = await requestJson<BackendPersonWorkflowHistoryDto>(`/people/${encodeURIComponent(String(personId))}/workflows`);
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
