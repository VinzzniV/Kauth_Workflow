import { keepPreviousData, useQuery } from "@tanstack/react-query";
import {
  getWorkflowAuditLog,
  getWorkflowByUid,
  getWorkflowConfig,
  getWorkflowPage,
  getRelatedWorkflows,
  searchWorkflowTargetPersonSources,
  getWorkflowTasks,
  type WorkflowQueryOptions,
} from "../workflowApi";
import { getMyTasks } from "../taskApi";
import { queryKeys } from "../queryKeys";

export function useWorkflowList(
  options: WorkflowQueryOptions,
  page: number,
  pageSize: number,
  enabled = true
) {
  return useQuery({
    queryKey: queryKeys.workflows.list(options, page, pageSize),
    queryFn: () => getWorkflowPage(pageSize, page * pageSize, options),
    staleTime: 30 * 1000,
    placeholderData: keepPreviousData,
    enabled,
  });
}

export function useWorkflowDetail(uid: string) {
  return useQuery({
    queryKey: queryKeys.workflows.detail(uid),
    queryFn: () => getWorkflowByUid(uid),
    staleTime: 0,
    enabled: Boolean(uid),
    refetchOnWindowFocus: true,
  });
}

export function useWorkflowConfig(
  roleId: number | null,
  workflowDefinitionKey: string | null,
  enabled = true
) {
  return useQuery({
    queryKey: queryKeys.workflows.config(roleId, workflowDefinitionKey),
    queryFn: () => getWorkflowConfig(roleId, workflowDefinitionKey),
    enabled: enabled && Boolean(workflowDefinitionKey),
    staleTime: 30 * 1000,
  });
}

export function useWorkflowTargetPersonSourcesSearch(search: string, enabled = true) {
  return useQuery({
    queryKey: queryKeys.workflows.targetPersonSources(search),
    queryFn: () => searchWorkflowTargetPersonSources(search),
    enabled,
    staleTime: 30 * 1000,
    placeholderData: keepPreviousData,
  });
}

export function useWorkflowTasks(
  uid: string,
  options?: { refetchInterval?: number | false }
) {
  return useQuery({
    queryKey: queryKeys.workflows.tasks(uid),
    queryFn: () => getWorkflowTasks(uid),
    enabled: Boolean(uid),
    staleTime: 15 * 1000,
    // Slice 5: optionales Polling-Intervall fuer den Approval-Modal-running-State.
    // Default behavior unveraendert (false = kein Polling).
    refetchInterval: options?.refetchInterval ?? false,
  });
}

export function useWorkflowAuditLog(uid: string, limit: number, offset: number, enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.workflows.auditLog(uid, limit, offset),
    queryFn: () => getWorkflowAuditLog(uid, limit, offset),
    enabled: Boolean(uid) && enabled,
    staleTime: 60 * 1000,
  });
}

export function useRelatedWorkflows(uid: string) {
  return useQuery({
    queryKey: queryKeys.workflows.related(uid),
    queryFn: () => getRelatedWorkflows(uid),
    enabled: Boolean(uid),
    staleTime: 30 * 1000,
  });
}

export function useMyTasks() {
  return useQuery({
    queryKey: queryKeys.myTasks(),
    queryFn: getMyTasks,
    staleTime: 30 * 1000,
  });
}
