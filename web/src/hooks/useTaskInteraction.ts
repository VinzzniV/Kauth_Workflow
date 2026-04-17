import { useCallback, useState } from "react";
import { useToast } from "../components/feedback/useToast";
import {
  useAddTaskComment,
  useDecideTaskApproval,
  useUpdateTaskStatus,
} from "../services/mutations/workflowMutations";
import {
  mapVisibleTaskStatusToWorkflowStatus,
  type VisibleTaskStatus,
} from "../utils/taskStatus";
import type { TaskFamily, TaskStatus } from "../types/workflow";

type TaskInteractionArgs = {
  taskId: number;
  taskRef?: string;
  workflowUid?: string | null;
  taskFamily?: TaskFamily;
  rotationPlanId?: number | null;
};

type TaskStatusChangeArgs = TaskInteractionArgs & {
  status: VisibleTaskStatus;
  currentStatus: TaskStatus;
};

export function useTaskInteraction() {
  const [savingTaskIds, setSavingTaskIds] = useState<Record<number, boolean>>({});
  const [savingApprovalTaskIds, setSavingApprovalTaskIds] = useState<Record<number, boolean>>({});
  const [commentDrafts, setCommentDrafts] = useState<Record<number, string>>({});
  const [savingCommentTaskIds, setSavingCommentTaskIds] = useState<Record<number, boolean>>({});
  const { showError, showSuccess } = useToast();
  const updateTaskStatusMutation = useUpdateTaskStatus();
  const addTaskCommentMutation = useAddTaskComment();
  const decideTaskApprovalMutation = useDecideTaskApproval();

  const handleStatusChange = useCallback(
    async ({ taskId, taskRef, workflowUid, taskFamily, rotationPlanId, status, currentStatus }: TaskStatusChangeArgs) => {
      const nextStatus = mapVisibleTaskStatusToWorkflowStatus(status, currentStatus, taskFamily ?? "workflow");
      if (nextStatus === currentStatus) {
        return;
      }

      setSavingTaskIds((current) => ({ ...current, [taskId]: true }));

      try {
        await updateTaskStatusMutation.mutateAsync({
          workflowUid: workflowUid ?? null,
          taskId,
          taskRef,
          rotationPlanId: rotationPlanId ?? null,
          status: nextStatus,
        });
        showSuccess("Aufgabenstatus wurde gespeichert.");
      } catch (err) {
        const message = err instanceof Error ? err.message : "Aufgabenstatus konnte nicht aktualisiert werden.";
        showError(message);
      } finally {
        setSavingTaskIds((current) => ({ ...current, [taskId]: false }));
      }
    },
    [showError, showSuccess, updateTaskStatusMutation]
  );

  const handleCommentDraftChange = useCallback((taskId: number, value: string) => {
    setCommentDrafts((current) => ({ ...current, [taskId]: value }));
  }, []);

  const handleTaskCommentSubmit = useCallback(
    async ({ taskId, taskRef, workflowUid, rotationPlanId }: TaskInteractionArgs) => {
      const draft = (commentDrafts[taskId] ?? "").trim();
      if (!draft) {
        return;
      }

      setSavingCommentTaskIds((current) => ({ ...current, [taskId]: true }));

      try {
        await addTaskCommentMutation.mutateAsync({
          workflowUid: workflowUid ?? null,
          taskId,
          taskRef,
          rotationPlanId: rotationPlanId ?? null,
          text: draft,
        });
        setCommentDrafts((current) => ({ ...current, [taskId]: "" }));
        showSuccess("Kommentar wurde gespeichert.");
      } catch (err) {
        const message = err instanceof Error ? err.message : "Kommentar konnte nicht gespeichert werden.";
        showError(message);
      } finally {
        setSavingCommentTaskIds((current) => ({ ...current, [taskId]: false }));
      }
    },
    [addTaskCommentMutation, commentDrafts, showError, showSuccess]
  );

  const handleApprovalDecision = useCallback(
    async ({ taskId, taskRef, workflowUid, rotationPlanId, approved }: TaskInteractionArgs & { approved: boolean }) => {
      const draft = (commentDrafts[taskId] ?? "").trim();
      setSavingApprovalTaskIds((current) => ({ ...current, [taskId]: true }));

      try {
        await decideTaskApprovalMutation.mutateAsync({
          workflowUid: workflowUid ?? null,
          taskId,
          taskRef,
          rotationPlanId: rotationPlanId ?? null,
          approved,
          commentText: draft || undefined,
        });
        setCommentDrafts((current) => ({ ...current, [taskId]: "" }));
        showSuccess(approved ? "Freigabe wurde gespeichert." : "Ablehnung wurde gespeichert.");
      } catch (err) {
        const message = err instanceof Error ? err.message : "Freigabeentscheidung konnte nicht gespeichert werden.";
        showError(message);
      } finally {
        setSavingApprovalTaskIds((current) => ({ ...current, [taskId]: false }));
      }
    },
    [commentDrafts, decideTaskApprovalMutation, showError, showSuccess]
  );

  return {
    savingTaskIds,
    savingApprovalTaskIds,
    commentDrafts,
    savingCommentTaskIds,
    handleStatusChange,
    handleApprovalDecision,
    handleCommentDraftChange,
    handleTaskCommentSubmit,
  };
}
