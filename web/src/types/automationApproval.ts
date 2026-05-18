// Slice 5 (Admin-Gated-Automation, Approval-Runtime-UI): Frontend-Types fuer
// /admin/automation/plan + /reauth + /approve. Mirror Backend Slice 3 +
// erweiterte Slice-1-Plan-Response (planHash).

export type AutomationPlanStep = {
  actionKey: string;
  isSuccess: boolean;
  // plan ist die rohe JsonNode-Struktur des typisierten Plans (AdUserPlan, etc.).
  // Wird im PlanStepDetail-Renderer per actionKey discriminator-resolved.
  plan: unknown | null;
  errorMessage: string | null;
};

export type AutomationPlanResponse = {
  nodeKey: string;
  steps: AutomationPlanStep[];
  planHash: string;
};

export type AutomationReauthIssueResponse = {
  token: string;
  expiresAt: string;
};

export type AutomationApproveSuccess = {
  approvalId: number;
  firstJobId: number;
};

// 409-Body bei Plan-Drift; das Modal liest currentPlanHash + plan und resettet
// den State auf "reviewing" mit dem neuen Plan.
export type AutomationApproveDriftError = {
  error: "plan_drift";
  currentPlanHash: string;
  plan: AutomationPlanResponse;
};

// 409-Body bei Doppel-Approval.
export type AutomationApproveAlreadyApprovedError = {
  error: "already_approved";
  existingApprovalId: number | null;
  approverUserId: number | null;
  approvedAt: string | null;
};

// ---- Typisierte Plan-Detail-Shapes (mirror api/API/Services/AutomationPlanResults.cs) ----

export type AdUserPlanDetail = {
  alreadyExists: boolean;
  existingDn: string | null;
  targetDn: string | null;
  userPrincipalName: string | null;
  samAccountName: string | null;
  displayName: string | null;
  givenName: string | null;
  surname: string | null;
  mail: string | null;
  employeeNumber: string | null;
  passwordNote: string;
};

export type PlannedGroupDetail = {
  groupDn: string;
  alreadyMember: boolean | null;
  source: string | null;
};

export type GroupAssignmentPlanDetail = {
  userDistinguishedName: string | null;
  groups: PlannedGroupDetail[];
  note: string | null;
};

export type MailboxPlanDetail = {
  upnKnown: boolean;
  userPrincipalName: string | null;
  skuId: string;
  skuDisplayName: string | null;
  smtpAddressKnown: boolean;
  smtpNote: string;
  upnNote: string | null;
};

export type WelcomeMailPlanDetail = {
  recipientKnown: boolean;
  recipient: string | null;
  subject: string | null;
  bodyPreview: string | null;
  passwordAvailability:
    | "PresentInVault"
    | "WillBeGenerated"
    | "Unknown"
    | "NotAvailable";
  note: string;
};

// ---- Slice 7 (Live-Log waehrend Ausfuehrung): Per-Action-Status-Polling ----

export type AutomationApprovalStepStatus =
  | "pending" | "running" | "succeeded" | "failed" | "cancelled";

export type AutomationApprovalStatusLogEntry = {
  level: string;
  message: string;
  createdAt: string;
};

export type AutomationApprovalStatusStep = {
  actionKey: string;
  executionOrder: number;
  jobStatus: AutomationApprovalStepStatus;
  currentAttempt: number;
  maxAttempts: number;
  latestErrorMessage: string | null;
  latestFailureKind: "permanent" | "transient" | null;
  logs: AutomationApprovalStatusLogEntry[];
};

export type AutomationApprovalStatusResponse = {
  approvalId: number;
  overallStatus: "running" | "succeeded" | "failed";
  steps: AutomationApprovalStatusStep[];
};
