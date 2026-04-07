import type {
  DerivedAnswer,
  LinkableWorkflow,
  RelatedWorkflowSummary,
  RequirementSelectionPayload,
  WorkflowAuditEntry,
  WorkflowConfig,
  WorkflowCreationPayload,
  WorkflowCreationResponse,
  WorkflowDetail,
  WorkflowLink,
  WorkflowPage,
  WorkflowRequirementSnapshot,
  WorkflowRuntimeStatus,
  WorkflowSummary,
  WorkflowTargetPerson,
  WorkflowTargetPersonSource,
  WorkflowTask,
} from "../types/workflow";
import { requestJson } from "./api/client";
import type {
  BackendDerivedAnswerDto,
  BackendLinkableWorkflowDto,
  BackendRelatedWorkflowSummaryDto,
  BackendWorkflowTargetPersonSourceDto,
  BackendWorkflowAuditEntryDto,
  BackendWorkflowConfigDto,
  BackendWorkflowDetailDto,
  BackendWorkflowLinkDto,
  BackendWorkflowPageDto,
  BackendWorkflowRequirementSnapshotDto,
  BackendWorkflowSummaryDto,
  BackendWorkflowTargetPersonDto,
  BackendWorkflowTaskDto,
} from "./api/backendDtos";
import {
  mapWorkflowAuditEntry,
  mapWorkflowConfig,
  mapWorkflowDetail,
  mapWorkflowPage,
  mapWorkflowRequirement,
  mapWorkflowSummary,
  mapWorkflowTask,
} from "./api/mappers";

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

export async function createWorkflow(payload: WorkflowCreationPayload): Promise<WorkflowCreationResponse> {
  return requestJson<WorkflowCreationResponse>("/workflows", { method: "POST", body: payload });
}

export async function archiveWorkflow(uid: string): Promise<void> {
  await requestJson<unknown>(`/workflows/${encodeURIComponent(uid)}/archive`, { method: "POST" });
}

export async function deleteWorkflow(uid: string): Promise<void> {
  await requestJson<unknown>(`/workflows/${encodeURIComponent(uid)}`, { method: "DELETE" });
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
  const data = await requestJson<BackendWorkflowPageDto>(`/workflows${buildWorkflowQuery(options)}`);
  return data.items.map(mapWorkflowSummary);
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

export async function getWorkflowLinks(uid: string): Promise<WorkflowLink[]> {
  return requestJson<BackendWorkflowLinkDto[]>(`/workflows/${encodeURIComponent(uid)}/links`);
}

export async function getRelatedWorkflows(uid: string): Promise<RelatedWorkflowSummary[]> {
  return requestJson<BackendRelatedWorkflowSummaryDto[]>(`/workflows/${encodeURIComponent(uid)}/related`);
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

export async function searchCompletedOnboardings(
  search?: string,
  limit = 20
): Promise<WorkflowTargetPersonSource[]> {
  return searchWorkflowTargetPersonSources(search, limit);
}

export async function searchWorkflowTargetPersonSources(
  search?: string,
  limit = 20
): Promise<WorkflowTargetPersonSource[]> {
  const params = new URLSearchParams({ limit: String(limit) });
  if (search && search.trim()) {
    params.set("query", search.trim());
  }

  return requestJson<BackendWorkflowTargetPersonSourceDto[]>(
    `/workflow-target-person-sources?${params.toString()}`
  );
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
