import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
  archiveWorkflow,
  deleteWorkflow,
  updateWorkflowSupervisorStep,
} from "../workflowApi";
import {
  addTaskComment,
  decideTaskApproval,
  updateTaskStatus,
} from "../taskApi";
import { queryKeys } from "../queryKeys";
import type { RequirementSelectionPayload, WorkflowTaskStatus } from "../../types/workflow";

type TaskMutationVariables = {
  workflowUid: string;
  taskId: number;
};

type UpdateTaskStatusVariables = TaskMutationVariables & {
  status: WorkflowTaskStatus;
};

type AddTaskCommentVariables = TaskMutationVariables & {
  text: string;
};

type DecideTaskApprovalVariables = TaskMutationVariables & {
  approved: boolean;
  commentText?: string;
};

function invalidateWorkflowTaskQueries(
  invalidateQueries: ReturnType<typeof useQueryClient>["invalidateQueries"],
  workflowUid: string
) {
  if (workflowUid.trim()) {
    invalidateQueries({ queryKey: queryKeys.workflows.tasks(workflowUid) });
    invalidateQueries({ queryKey: queryKeys.workflows.detail(workflowUid) });
    invalidateQueries({ queryKey: queryKeys.workflows.auditLog(workflowUid, 50, 0) });
  }
}

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

export function useUpdateTaskStatus() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ taskId, status }: UpdateTaskStatusVariables) =>
      updateTaskStatus(taskId, status),
    onSuccess: (_, { workflowUid }) => {
      invalidateWorkflowTaskQueries(queryClient.invalidateQueries.bind(queryClient), workflowUid);
      queryClient.invalidateQueries({ queryKey: queryKeys.workflows.all() });
      queryClient.invalidateQueries({ queryKey: queryKeys.myTasks() });
      queryClient.invalidateQueries({ queryKey: queryKeys.dashboard.all() });
    },
  });
}

export function useAddTaskComment() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ taskId, text }: AddTaskCommentVariables) => addTaskComment(taskId, text),
    onSuccess: (_, { workflowUid }) => {
      invalidateWorkflowTaskQueries(queryClient.invalidateQueries.bind(queryClient), workflowUid);
      queryClient.invalidateQueries({ queryKey: queryKeys.myTasks() });
    },
  });
}

export function useDecideTaskApproval() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ taskId, approved, commentText }: DecideTaskApprovalVariables) =>
      decideTaskApproval(taskId, approved, commentText),
    onSuccess: (_, { workflowUid }) => {
      invalidateWorkflowTaskQueries(queryClient.invalidateQueries.bind(queryClient), workflowUid);
      queryClient.invalidateQueries({ queryKey: queryKeys.workflows.all() });
      queryClient.invalidateQueries({ queryKey: queryKeys.myTasks() });
      queryClient.invalidateQueries({ queryKey: queryKeys.dashboard.all() });
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
