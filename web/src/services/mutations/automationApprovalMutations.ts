// Slice 5 (Admin-Gated-Automation, Approval-Runtime-UI): Plan-Query + Approve-
// Mutation.
//
// Slice AGA-N2: Approval triggert vor dem One-Shot-Token einen interaktiven
// Entra-Re-Auth (MSAL `prompt: 'login'`-Popup). Backend prueft den auth_time-
// Claim und liefert 401 reauth_required oder 422 reauth_unconfigured, wenn der
// Token nicht frisch genug ist; die Mutation propagiert das strukturiert an den
// Dialog.

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  approveAutomationPlan,
  fetchAutomationApprovalStatus,
  fetchAutomationPlan,
  issueAutomationReauthToken,
} from "../automationApprovalApi";
import { queryKeys } from "../queryKeys";
import { identityProvider, isEntraMode } from "../../auth/IdentityProvider";
import type { ApiError } from "../api/client";
import type {
  AutomationApprovalStatusResponse,
  AutomationApproveSuccess,
  AutomationPlanResponse,
  AutomationReauthRequiredError,
  AutomationReauthUnconfiguredError,
} from "../../types/automationApproval";

export type ApproveReauthFailure =
  | { kind: "cancelled" }
  | { kind: "popup_failed"; reason: string }
  | { kind: "unconfigured"; hint: string }
  | { kind: "still_stale"; maxAgeSeconds: number };

// Throwable, damit der Dialog im catch-Pfad strukturiert unterscheiden kann.
export class ApproveReauthError extends Error {
  public readonly detail: ApproveReauthFailure;
  constructor(detail: ApproveReauthFailure) {
    super(`approve_reauth_failed:${detail.kind}`);
    this.detail = detail;
  }
}

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

// Slice 7: Per-Action-Live-Status. Im running-State pollt der Dialog alle 2s,
// solange enabled=true ist. React-Query raeumt das Interval automatisch ab, sobald
// enabled umkippt (zB beim Wechsel auf succeeded/failed/background).
export function useAutomationApprovalStatusQuery(req: {
  approvalId: number | null;
  enabled: boolean;
}) {
  return useQuery<AutomationApprovalStatusResponse>({
    queryKey: ["automation-approval-status", req.approvalId],
    queryFn: () => fetchAutomationApprovalStatus(req.approvalId!),
    enabled: req.enabled && req.approvalId !== null,
    refetchInterval: 2_000,
    staleTime: 0,
    retry: false,
  });
}

// Holt das One-Shot-Token, fuehrt bei 401 reauth_required einmal einen
// interaktiven MSAL-Popup-Re-Auth durch und versucht es danach noch ein
// einziges Mal. Wirft ApproveReauthError, wenn der Re-Auth-Pfad scheitert.
async function acquireReauthToken(): Promise<string> {
  try {
    const issued = await issueAutomationReauthToken();
    return issued.token;
  } catch (err) {
    const apiError = err as ApiError;
    if (apiError.status === 422) {
      const payload = apiError.payload as AutomationReauthUnconfiguredError | undefined;
      throw new ApproveReauthError({
        kind: "unconfigured",
        hint: payload?.hint ?? "auth_time-Claim fehlt im Access-Token.",
      });
    }
    if (apiError.status !== 401) {
      throw err;
    }
    const payload = apiError.payload as AutomationReauthRequiredError | undefined;
    if (payload?.error !== "reauth_required") {
      throw err;
    }

    // Entra-Pfad: MSAL-Popup mit prompt:'login', danach forceRefresh.
    // Dev-sim sollte nie hier landen (Backend skipped den Check), aber falls
    // doch — der Provider liefert "skipped-dev-sim".
    if (!isEntraMode()) {
      throw new ApproveReauthError({
        kind: "popup_failed",
        reason: "dev_sim_unexpected_reauth_required",
      });
    }

    const outcome = await identityProvider.triggerInteractiveReauth();
    if (outcome.kind === "cancelled") {
      throw new ApproveReauthError({ kind: "cancelled" });
    }
    if (outcome.kind === "failed") {
      throw new ApproveReauthError({ kind: "popup_failed", reason: outcome.reason });
    }

    try {
      const issued = await issueAutomationReauthToken();
      return issued.token;
    } catch (secondErr) {
      const secondApiError = secondErr as ApiError;
      if (secondApiError.status === 401) {
        const stillStale = secondApiError.payload as AutomationReauthRequiredError | undefined;
        throw new ApproveReauthError({
          kind: "still_stale",
          maxAgeSeconds: stillStale?.maxAgeSeconds ?? 120,
        });
      }
      throw secondErr;
    }
  }
}

export function useApproveAutomationPlanMutation(workflowUid: string) {
  const queryClient = useQueryClient();

  return useMutation<
    AutomationApproveSuccess,
    Error,
    { workflowInstanceUid: string; nodeKey: string; planHash: string }
  >({
    mutationFn: async (input) => {
      const token = await acquireReauthToken();
      return approveAutomationPlan({
        workflowInstanceUid: input.workflowInstanceUid,
        nodeKey: input.nodeKey,
        planHash: input.planHash,
        reauthToken: token,
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
