import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  getSupervisorStepWorkflows,
  getWorkflowSupervisorStep,
  updateWorkflowSupervisorStep,
} from "../services/workflowApi";
import { queryKeys } from "../services/queryKeys";
import type { RequirementSelectionPayload } from "../types/workflow";

export function useSupervisorWorkflows() {
  const queryClient = useQueryClient();

  const queueQuery = useQuery({
    queryKey: queryKeys.supervisorWorkflows(),
    queryFn: getSupervisorStepWorkflows,
    staleTime: 60 * 1000,
    select: (workflows) => workflows.filter((w) => w.workflowStatus === "waiting_for_supervisor"),
  });

  const saveMutation = useMutation({
    mutationFn: ({ uid, payload }: { uid: string; payload: RequirementSelectionPayload[] }) =>
      updateWorkflowSupervisorStep(uid, payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.supervisorWorkflows() });
    },
  });

  return {
    assignedWorkflows: queueQuery.data ?? [],
    isQueueLoading: queueQuery.isLoading,
    queueError: queueQuery.error instanceof Error ? queueQuery.error.message : null,
    refetchQueue: queueQuery.refetch,
    saveMutation,
  };
}

export function useSupervisorStep(uid: string | null) {
  return useQuery({
    queryKey: queryKeys.workflows.supervisorStep(uid ?? ""),
    queryFn: () => getWorkflowSupervisorStep(uid!),
    enabled: uid !== null,
    staleTime: 30 * 1000,
  });
}
