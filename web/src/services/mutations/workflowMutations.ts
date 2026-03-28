import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
  archiveWorkflow,
  deleteWorkflow,
  updateWorkflowSupervisorStep,
} from "../workflowApi";
import {
  addTaskComment,
  updateTaskStatus,
} from "../taskApi";
import { queryKeys } from "../queryKeys";
import type { RequirementSelectionPayload, WorkflowTaskStatus } from "../../types/workflow";

export function useArchiveWorkflow() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (uid: string) => archiveWorkflow(uid),
    onSuccess: (_, uid) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.workflows.detail(uid) });
      queryClient.invalidateQueries({ queryKey: queryKeys.workflows.all() });
      queryClient.invalidateQueries({ queryKey: queryKeys.dashboard.all() });
    },
  });
}

export function useDeleteWorkflow() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (uid: string) => deleteWorkflow(uid),
    onSuccess: (_, uid) => {
      queryClient.removeQueries({ queryKey: queryKeys.workflows.detail(uid) });
      queryClient.invalidateQueries({ queryKey: queryKeys.workflows.all() });
      queryClient.invalidateQueries({ queryKey: queryKeys.dashboard.all() });
    },
  });
}

export function useUpdateTaskStatus(workflowUid: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ taskId, status }: { taskId: number; status: WorkflowTaskStatus }) =>
      updateTaskStatus(taskId, status),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.workflows.tasks(workflowUid) });
      queryClient.invalidateQueries({ queryKey: queryKeys.workflows.detail(workflowUid) });
      queryClient.invalidateQueries({ queryKey: queryKeys.workflows.auditLog(workflowUid, 50, 0) });
      queryClient.invalidateQueries({ queryKey: queryKeys.workflows.all() });
      queryClient.invalidateQueries({ queryKey: queryKeys.myTasks() });
      queryClient.invalidateQueries({ queryKey: queryKeys.dashboard.all() });
    },
  });
}

export function useAddTaskComment(workflowUid: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ taskId, text }: { taskId: number; text: string }) => addTaskComment(taskId, text),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.workflows.tasks(workflowUid) });
      queryClient.invalidateQueries({ queryKey: queryKeys.workflows.detail(workflowUid) });
      queryClient.invalidateQueries({ queryKey: queryKeys.workflows.auditLog(workflowUid, 50, 0) });
      queryClient.invalidateQueries({ queryKey: queryKeys.myTasks() });
    },
  });
}

export function useUpdateSupervisorStep(workflowUid: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (selections: RequirementSelectionPayload[]) =>
      updateWorkflowSupervisorStep(workflowUid, selections),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.workflows.detail(workflowUid) });
      queryClient.invalidateQueries({ queryKey: queryKeys.workflows.auditLog(workflowUid, 50, 0) });
      queryClient.invalidateQueries({ queryKey: queryKeys.workflows.all() });
      queryClient.invalidateQueries({ queryKey: queryKeys.dashboard.all() });
    },
  });
}
