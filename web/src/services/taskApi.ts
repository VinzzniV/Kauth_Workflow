import type { TaskWithWorkflow, WorkflowTaskStatus } from "../types/workflow";
import { requestJson } from "./api/client";
import type { BackendTaskWithWorkflowDto } from "./api/backendDtos";
import { mapTaskWithWorkflow } from "./api/mappers";

export async function getMyTasks(): Promise<TaskWithWorkflow[]> {
  const data = await requestJson<BackendTaskWithWorkflowDto[]>("/tasks");
  return data.map(mapTaskWithWorkflow);
}

export async function updateTaskStatus(taskId: number, status: WorkflowTaskStatus): Promise<TaskWithWorkflow> {
  const data = await requestJson<BackendTaskWithWorkflowDto>(`/tasks/${encodeURIComponent(String(taskId))}/status`, {
    method: "PATCH",
    body: { status },
  });
  return mapTaskWithWorkflow(data);
}

export async function addTaskComment(taskId: number, commentText: string): Promise<TaskWithWorkflow> {
  const data = await requestJson<BackendTaskWithWorkflowDto>(`/tasks/${encodeURIComponent(String(taskId))}/comments`, {
    method: "POST",
    body: { commentText },
  });
  return mapTaskWithWorkflow(data);
}

export async function decideTaskApproval(taskId: number, approved: boolean, commentText?: string): Promise<TaskWithWorkflow> {
  const data = await requestJson<BackendTaskWithWorkflowDto>(`/tasks/${encodeURIComponent(String(taskId))}/approval-decision`, {
    method: "POST",
    body: {
      approved,
      commentText: commentText?.trim() ? commentText.trim() : undefined,
    },
  });
  return mapTaskWithWorkflow(data);
}
