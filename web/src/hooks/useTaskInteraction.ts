import { useCallback, useState } from "react";
import { useToast } from "../components/feedback/useToast";
import {
  useAddTaskComment,
  useUpdateTaskStatus,
} from "../services/mutations/workflowMutations";
import {
  mapVisibleTaskStatusToWorkflowStatus,
  type VisibleTaskStatus,
} from "../utils/taskStatus";
import type { WorkflowTaskStatus } from "../types/workflow";

type TaskInteractionArgs = {
  taskId: number;
  workflowUid: string;
};

type TaskStatusChangeArgs = TaskInteractionArgs & {
  status: VisibleTaskStatus;
  currentStatus: WorkflowTaskStatus;
};

export function useTaskInteraction() {
  const [savingTaskIds, setSavingTaskIds] = useState<Record<number, boolean>>({});
  const [commentDrafts, setCommentDrafts] = useState<Record<number, string>>({});
  const [savingCommentTaskIds, setSavingCommentTaskIds] = useState<Record<number, boolean>>({});
  const { showError, showSuccess } = useToast();
  const updateTaskStatusMutation = useUpdateTaskStatus();
  const addTaskCommentMutation = useAddTaskComment();

  const handleStatusChange = useCallback(
    async ({ taskId, workflowUid, status, currentStatus }: TaskStatusChangeArgs) => {
      const nextStatus = mapVisibleTaskStatusToWorkflowStatus(status, currentStatus);
      if (nextStatus === currentStatus) {
        return;
      }

      setSavingTaskIds((current) => ({ ...current, [taskId]: true }));

      try {
        await updateTaskStatusMutation.mutateAsync({ workflowUid, taskId, status: nextStatus });
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
    async ({ taskId, workflowUid }: TaskInteractionArgs) => {
      const draft = (commentDrafts[taskId] ?? "").trim();
      if (!draft) {
        return;
      }

      setSavingCommentTaskIds((current) => ({ ...current, [taskId]: true }));

      try {
        await addTaskCommentMutation.mutateAsync({ workflowUid, taskId, text: draft });
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

  return {
    savingTaskIds,
    commentDrafts,
    savingCommentTaskIds,
    handleStatusChange,
    handleCommentDraftChange,
    handleTaskCommentSubmit,
  };
}
