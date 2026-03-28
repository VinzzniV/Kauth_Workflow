import type { PersonWorkflowHistory } from "../types/workflow";
import { requestJson } from "./api/client";
import type { BackendPersonWorkflowHistoryDto } from "./api/backendDtos";
import { mapPersonWorkflowHistory } from "./api/mappers";

export async function getPersonWorkflowHistory(personId: number): Promise<PersonWorkflowHistory> {
  const data = await requestJson<BackendPersonWorkflowHistoryDto>(`/people/${encodeURIComponent(String(personId))}/workflows`);
  return mapPersonWorkflowHistory(data);
}
