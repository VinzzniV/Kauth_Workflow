import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
  archiveWorkflow,
  cancelWorkflow,
  deleteWorkflow,
  updateWorkflowSupervisorStep,
} from "../workflowApi";
import {
  addTaskComment,
  addTaskCommentByRef,
  decideTaskApproval,
  decideTaskApprovalByRef,
  updateTaskStatus,
  updateTaskStatusByRef,
} from "../taskApi";
import { queryKeys } from "../queryKeys";
import type {
  RequirementSelectionPayload,
  TaskStatus,
  WorkflowCancellationRequest,
} from "../../types/workflow";

type TaskMutationVariables = {
  workflowUid?: string | null;
  taskId: number;
  taskRef?: string;
  rotationPlanId?: number | null;
};

type UpdateTaskStatusVariables = TaskMutationVariables & {
  status: TaskStatus;
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
  workflowUid?: string | null,
  rotationPlanId?: number | null,
  taskRef?: string
) {
  if (workflowUid?.trim()) {
    invalidateQueries({ queryKey: queryKeys.workflows.tasks(workflowUid) });
    invalidateQueries({ queryKey: queryKeys.workflows.detail(workflowUid) });
    invalidateQueries({ queryKey: queryKeys.workflows.auditLog(workflowUid, 50, 0) });
  }

  if (rotationPlanId && rotationPlanId > 0) {
    invalidateQueries({ queryKey: queryKeys.rotation.planDetail(rotationPlanId) });
    invalidateQueries({ queryKey: queryKeys.rotation.generatedTasks(rotationPlanId) });
  }

  if (taskRef?.trim()) {
    invalidateQueries({ queryKey: queryKeys.tasks.byRef(taskRef) });
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

type CancelWorkflowVariables = {
  uid: string;
  request: WorkflowCancellationRequest;
};

export function useCancelWorkflow() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ uid, request }: CancelWorkflowVariables) => cancelWorkflow(uid, request),
    onSuccess: (_, { uid }) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.workflows.detail(uid) });
      queryClient.invalidateQueries({ queryKey: queryKeys.workflows.all() });
      queryClient.invalidateQueries({ queryKey: queryKeys.workflows.tasks(uid) });
      queryClient.invalidateQueries({ queryKey: queryKeys.workflows.auditLog(uid, 50, 0) });
      queryClient.invalidateQueries({ queryKey: queryKeys.dashboard.all() });
      // Storno blendet offene Tasks aus Worker-Inbox + Supervisor-Workflows aus.
      queryClient.invalidateQueries({ queryKey: queryKeys.myTasks() });
      queryClient.invalidateQueries({ queryKey: queryKeys.supervisorWorkflows() });
    },
  });
}

export function useUpdateTaskStatus() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ taskId, taskRef, status }: UpdateTaskStatusVariables) =>
      taskRef ? updateTaskStatusByRef(taskRef, status) : updateTaskStatus(taskId, status),
    onSuccess: (_, { workflowUid, rotationPlanId, taskRef }) => {
      invalidateWorkflowTaskQueries(queryClient.invalidateQueries.bind(queryClient), workflowUid, rotationPlanId, taskRef);
      queryClient.invalidateQueries({ queryKey: queryKeys.workflows.all() });
      queryClient.invalidateQueries({ queryKey: queryKeys.myTasks() });
      queryClient.invalidateQueries({ queryKey: queryKeys.dashboard.all() });
    },
  });
}

export function useAddTaskComment() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ taskId, taskRef, text }: AddTaskCommentVariables) =>
      taskRef ? addTaskCommentByRef(taskRef, text) : addTaskComment(taskId, text),
    onSuccess: (_, { workflowUid, rotationPlanId, taskRef }) => {
      invalidateWorkflowTaskQueries(queryClient.invalidateQueries.bind(queryClient), workflowUid, rotationPlanId, taskRef);
      queryClient.invalidateQueries({ queryKey: queryKeys.myTasks() });
    },
  });
}

export function useDecideTaskApproval() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ taskId, taskRef, approved, commentText }: DecideTaskApprovalVariables) =>
      taskRef
        ? decideTaskApprovalByRef(taskRef, approved, commentText)
        : decideTaskApproval(taskId, approved, commentText),
    onSuccess: (_, { workflowUid, rotationPlanId, taskRef }) => {
      invalidateWorkflowTaskQueries(queryClient.invalidateQueries.bind(queryClient), workflowUid, rotationPlanId, taskRef);
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
