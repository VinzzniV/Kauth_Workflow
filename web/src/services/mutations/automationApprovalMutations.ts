// Slice 5 (Admin-Gated-Automation, Approval-Runtime-UI): Plan-Query + Approve-
// Mutation. Soft-Re-Auth-Flow (Token holen + Approve in einem useMutation).

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  approveAutomationPlan,
  fetchAutomationPlan,
  issueAutomationReauthToken,
} from "../automationApprovalApi";
import { queryKeys } from "../queryKeys";
import type {
  AutomationApproveSuccess,
  AutomationPlanResponse,
} from "../../types/automationApproval";

export function useAutomationPlanQuery(req: {
  workflowInstanceUid: string;
  nodeKey: string;
  enabled: boolean;
}) {
  return useQuery<AutomationPlanResponse>({
    queryKey: ["automation-plan", req.workflowInstanceUid, req.nodeKey],
    queryFn: () => fetchAutomationPlan({
      workflowInstanceUid: req.workflowInstanceUid,
      nodeKey: req.nodeKey,
    }),
    enabled: req.enabled && Boolean(req.workflowInstanceUid) && Boolean(req.nodeKey),
    // staleTime 0: bei jedem Modal-Open frisch laden, weil der Plan jederzeit
    // driften kann (Form-Antworten, Vorgaenger-Aktionen).
    staleTime: 0,
    retry: false,
  });
}

export function useApproveAutomationPlanMutation(workflowUid: string) {
  const queryClient = useQueryClient();

  return useMutation<
    AutomationApproveSuccess,
    Error,
    { workflowInstanceUid: string; nodeKey: string; planHash: string }
  >({
    mutationFn: async (input) => {
      // Soft-Re-Auth: Token holen und sofort verwenden.
      const tokenResponse = await issueAutomationReauthToken();
      return approveAutomationPlan({
        workflowInstanceUid: input.workflowInstanceUid,
        nodeKey: input.nodeKey,
        planHash: input.planHash,
        reauthToken: tokenResponse.token,
      });
    },
    onSuccess: () => {
      // Inline drei Query-Keys invalidieren — analog zu invalidateWorkflowTaskQueries
      // in workflowMutations.ts (Zeile 43-63), aber lokal weil der Helper
      // module-private + anderer Argument-Contract.
      if (workflowUid?.trim()) {
        queryClient.invalidateQueries({ queryKey: queryKeys.workflows.tasks(workflowUid) });
        queryClient.invalidateQueries({ queryKey: queryKeys.workflows.detail(workflowUid) });
        queryClient.invalidateQueries({ queryKey: queryKeys.workflows.auditLog(workflowUid, 50, 0) });
      }
    },
  });
}
