// Slice 5 (Admin-Gated-Automation, Approval-Runtime-UI): drei Service-Funktionen
// fuer die Backend-Endpoints aus Slice 1 + 3. Drift- und Already-Approved-Errors
// werden mit dem rohen Response-Body als ApiError.payload propagiert, damit der
// aufrufende Hook strukturiert reagieren kann.

import { requestJson } from "./api/client";
import type {
  AutomationApproveSuccess,
  AutomationPlanResponse,
  AutomationReauthIssueResponse,
} from "../types/automationApproval";

export async function fetchAutomationPlan(req: {
  workflowInstanceUid: string;
  nodeKey: string;
}): Promise<AutomationPlanResponse> {
  return requestJson<AutomationPlanResponse>("/admin/automation/plan", {
    method: "POST",
    body: { workflowInstanceUid: req.workflowInstanceUid, nodeKey: req.nodeKey },
  });
}

export async function issueAutomationReauthToken(): Promise<AutomationReauthIssueResponse> {
  // Default-Purpose "automation_approval" wird Backend-seitig ergaenzt, wenn nicht gesetzt.
  return requestJson<AutomationReauthIssueResponse>("/admin/automation/reauth", {
    method: "POST",
    body: { purpose: "automation_approval" },
  });
}

export async function approveAutomationPlan(req: {
  workflowInstanceUid: string;
  nodeKey: string;
  planHash: string;
  reauthToken: string;
}): Promise<AutomationApproveSuccess> {
  return requestJson<AutomationApproveSuccess>("/admin/automation/approve", {
    method: "POST",
    body: {
      workflowInstanceUid: req.workflowInstanceUid,
      nodeKey: req.nodeKey,
      planHash: req.planHash,
      reauthToken: req.reauthToken,
    },
  });
}
