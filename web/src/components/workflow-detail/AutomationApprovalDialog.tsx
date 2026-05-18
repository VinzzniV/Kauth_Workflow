// Slice 5 (Admin-Gated-Automation, Approval-Runtime-UI): Modal-Dialog mit
// State-Machine idle -> loading-plan -> reviewing -> submitting -> running ->
// (succeeded | background). Drift (409) und unsupported-Errors (4xx) werden
// inline behandelt.
//
// Slice 6 (Action-Buendelung im Task-UI): Plan wird als Bundle gerendert
// (Stepper mit Connector, fachliche Action-Labels, Plan-Failure-Guard).
//
// Slice 7 (Live-Log waehrend Ausfuehrung): running-State zeigt jetzt per-Action-
// Status (Status-Pill, Versuch N/M, Logs); permanente Failures kippen die Phase
// auf "failed" statt nur den 60s-background-Fallback abzuwarten.

import { useCallback, useEffect, useMemo, useState } from "react";
import type { ApiError } from "../../services/api/client";
import { useToast } from "../feedback/useToast";
import {
  useApproveAutomationPlanMutation,
  useAutomationApprovalStatusQuery,
  useAutomationPlanQuery,
} from "../../services/mutations/automationApprovalMutations";
import { useWorkflowTasks } from "../../services/queries/workflowQueries";
import { getAutomationActionLabel } from "../../utils/automationActionLabels";
import type {
  AdUserPlanDetail,
  AutomationApprovalStatusStep,
  AutomationApproveAlreadyApprovedError,
  AutomationApproveDriftError,
  AutomationPlanResponse,
  AutomationPlanStep,
  GroupAssignmentPlanDetail,
  MailboxPlanDetail,
  WelcomeMailPlanDetail,
} from "../../types/automationApproval";

const RUNNING_TIMEOUT_MS = 60_000;
const POLL_INTERVAL_MS = 2_000;

type DialogPhase =
  | "loading-plan"
  | "load-error"
  | "reviewing"
  | "submitting"
  | "running"
  | "succeeded"
  | "failed"
  | "background"
  | "error-already-approved"
  | "error-plan-unavailable"
  | "error-no-role"
  | "error-config";

type AutomationApprovalDialogProps = {
  open: boolean;
  onClose: () => void;
  workflowInstanceUid: string;
  nodeKey: string;
  taskId: number;
};

export function AutomationApprovalDialog(props: AutomationApprovalDialogProps) {
  const { open, onClose, workflowInstanceUid, nodeKey, taskId } = props;
  const { showError, showSuccess } = useToast();

  const [phase, setPhase] = useState<DialogPhase>("loading-plan");
  const [driftNotice, setDriftNotice] = useState<string | null>(null);
  const [errorDetail, setErrorDetail] = useState<string | null>(null);
  const [runningStartedAt, setRunningStartedAt] = useState<number | null>(null);
  // Slice 7: Approval-ID aus dem Approve-Erfolg, wird im running-State fuer den
  // Status-Poll genutzt.
  const [approvalId, setApprovalId] = useState<number | null>(null);

  const planQuery = useAutomationPlanQuery({
    workflowInstanceUid,
    nodeKey,
    enabled: open,
  });
  const approveMutation = useApproveAutomationPlanMutation(workflowInstanceUid);

  // Polling auf workflow_tasks im running-Phase.
  const tasksQuery = useWorkflowTasks(
    workflowInstanceUid,
    { refetchInterval: phase === "running" ? POLL_INTERVAL_MS : false },
  );

  // Slice 7: Live-Status pro Action im running-State.
  const statusQuery = useAutomationApprovalStatusQuery({
    approvalId,
    enabled: phase === "running",
  });

  // Phase-Transition: Plan geladen -> reviewing. setState-in-effect ist hier die
  // einzige Moeglichkeit, weil React-Query v5 die onSuccess/onError-Callbacks
  // entfernt hat — Status-Flags muessen ueber Effects gespiegelt werden.
  useEffect(() => {
    if (!open) return;
    if (phase === "loading-plan") {
      if (planQuery.isSuccess) {
        // eslint-disable-next-line react-hooks/set-state-in-effect -- React-Query-v5-Pattern
        setPhase("reviewing");
      } else if (planQuery.isError) {
        setPhase("load-error");
      }
    }
  }, [open, phase, planQuery.isSuccess, planQuery.isError]);

  // Reset beim Schliessen.
  useEffect(() => {
    if (!open) {
      // eslint-disable-next-line react-hooks/set-state-in-effect -- Modal-Reset
      setPhase("loading-plan");
      setDriftNotice(null);
      setErrorDetail(null);
      setRunningStartedAt(null);
      setApprovalId(null);
    }
  }, [open]);

  // Running-Phase: Task-Status checken (success) bzw. Timeout (background).
  useEffect(() => {
    if (phase !== "running") return;
    const targetTask = tasksQuery.data?.find((t) => t.id === taskId);
    if (targetTask?.status === "done") {
      // eslint-disable-next-line react-hooks/set-state-in-effect -- Poll-Erfolg
      setPhase("succeeded");
      showSuccess("Aktionen wurden erfolgreich ausgeführt.");
      return;
    }
    if (runningStartedAt && Date.now() - runningStartedAt > RUNNING_TIMEOUT_MS) {
      setPhase("background");
    }
  }, [phase, tasksQuery.data, taskId, runningStartedAt, showSuccess]);

  // Slice 7: Permanent-Failure / Retries-Erschöpft → "failed"-Endphase.
  useEffect(() => {
    if (phase !== "running") return;
    if (statusQuery.data?.overallStatus === "failed") {
      // eslint-disable-next-line react-hooks/set-state-in-effect -- Poll-Failure
      setPhase("failed");
      setErrorDetail(buildFailureSummary(statusQuery.data.steps));
    }
  }, [phase, statusQuery.data]);

  // Watchdog-Tick fuer Timeout (force-rerender alle 1s im running-State).
  useEffect(() => {
    if (phase !== "running") return;
    const timer = setInterval(() => {
      if (runningStartedAt && Date.now() - runningStartedAt > RUNNING_TIMEOUT_MS) {
        setPhase("background");
      }
    }, 1_000);
    return () => clearInterval(timer);
  }, [phase, runningStartedAt]);

  const handleApprove = useCallback(async () => {
    if (!planQuery.data) return;
    setPhase("submitting");
    setDriftNotice(null);
    setErrorDetail(null);
    try {
      const result = await approveMutation.mutateAsync({
        workflowInstanceUid,
        nodeKey,
        planHash: planQuery.data.planHash,
      });
      setApprovalId(result.approvalId);
      setRunningStartedAt(Date.now());
      setPhase("running");
    } catch (err) {
      const apiError = err as ApiError;
      const status = apiError.status ?? 0;
      const payload = apiError.payload as
        | AutomationApproveDriftError
        | AutomationApproveAlreadyApprovedError
        | { error?: string; failedActions?: string[]; message?: string }
        | undefined;

      if (status === 409 && payload && (payload as AutomationApproveDriftError).error === "plan_drift") {
        // Plan-Drift: neuen Plan in den Cache spielen, State zurueck auf reviewing.
        const drift = payload as AutomationApproveDriftError;
        // queryClient.setQueryData wuerde direkter sein, aber invalidate triggert
        // den Refetch — Drift-Body enthaelt den neuen Plan; wir setzen ihn lokal:
        planQuery.refetch();
        setDriftNotice(
          `Der Plan hat sich geändert. Neuer Hash: ${drift.currentPlanHash.slice(0, 12)}… Bitte erneut prüfen und bestätigen.`,
        );
        setPhase("reviewing");
        return;
      }
      if (status === 409 && payload && (payload as AutomationApproveAlreadyApprovedError).error === "already_approved") {
        setPhase("error-already-approved");
        return;
      }
      if (status === 401) {
        setErrorDetail("Re-Auth fehlgeschlagen. Bitte erneut versuchen.");
        setPhase("reviewing");
        return;
      }
      if (status === 422) {
        const failedActions =
          (payload as { failedActions?: string[] } | undefined)?.failedActions ?? [];
        setErrorDetail(
          failedActions.length > 0
            ? `Plan-Berechnung fehlgeschlagen für: ${failedActions.join(", ")}.`
            : "Plan-Berechnung fehlgeschlagen.",
        );
        setPhase("error-plan-unavailable");
        return;
      }
      if (status === 403) {
        setPhase("error-no-role");
        return;
      }
      if (status === 400) {
        setPhase("error-config");
        return;
      }
      showError(apiError.message || "Genehmigung fehlgeschlagen.");
      setPhase("reviewing");
    }
  }, [planQuery, approveMutation, workflowInstanceUid, nodeKey, showError]);

  if (!open) return null;

  return (
    <>
      <div className="admin-drawer-backdrop" onClick={onClose} aria-hidden="true" />
      <aside
        className="admin-drawer wf-automation-drawer"
        role="dialog"
        aria-modal="true"
        aria-label="Plan anzeigen und freigeben"
      >
        <header className="admin-drawer-head">
          <div className="admin-drawer-title">
            <h2>Automatisierung beim Abschluss</h2>
          </div>
          <button
            type="button"
            className="admin-drawer-close"
            onClick={onClose}
            aria-label="Schließen"
          >
            ×
          </button>
        </header>
        <div className="admin-drawer-body content-stack">
          {phase === "loading-plan" && <p>Plan wird geladen…</p>}

          {phase === "load-error" && (
            <p className="wf-step-card-hint wf-step-card-hint--error">
              Plan konnte nicht geladen werden. Bitte das Modal schließen und erneut versuchen.
            </p>
          )}

          {(phase === "reviewing" || phase === "submitting") && planQuery.data && (
            <PlanReview
              plan={planQuery.data}
              driftNotice={driftNotice}
              errorDetail={errorDetail}
              submitting={phase === "submitting"}
              onApprove={handleApprove}
            />
          )}

          {phase === "running" && (
            <div className="content-stack">
              <p>
                Aktionen werden ausgeführt. Die Aufgabe aktualisiert sich automatisch — Du kannst
                dieses Fenster offen lassen.
              </p>
              {statusQuery.data ? (
                <LiveStatusList steps={statusQuery.data.steps} />
              ) : null}
            </div>
          )}

          {phase === "succeeded" && (
            <div className="content-stack">
              <p className="wf-step-card-hint wf-step-card-hint--success">
                Aktionen wurden erfolgreich ausgeführt. Die Aufgabe wurde abgeschlossen.
              </p>
              {statusQuery.data ? (
                <LiveStatusList steps={statusQuery.data.steps} />
              ) : null}
              <button type="button" className="btn btn-primary" onClick={onClose}>
                Schließen
              </button>
            </div>
          )}

          {phase === "failed" && (
            <div className="content-stack">
              <p className="wf-step-card-hint wf-step-card-hint--error">
                Eine Aktion ist fehlgeschlagen — die Aufgabe bleibt offen. Bitte im Workflow-Detail
                manuell weiterarbeiten.
              </p>
              {errorDetail ? (
                <p className="wf-step-card-hint wf-step-card-hint--error">{errorDetail}</p>
              ) : null}
              {statusQuery.data ? (
                <LiveStatusList steps={statusQuery.data.steps} />
              ) : null}
              <button type="button" className="btn btn-secondary" onClick={onClose}>
                Schließen
              </button>
            </div>
          )}

          {phase === "background" && (
            <div className="content-stack">
              <p>
                Aktionen werden ausgeführt. Die Aufgabe aktualisiert sich automatisch — du kannst
                dieses Fenster jetzt schließen.
              </p>
              <button type="button" className="btn btn-secondary" onClick={onClose}>
                Schließen
              </button>
            </div>
          )}

          {phase === "error-already-approved" && (
            <div className="content-stack">
              <p className="wf-step-card-hint wf-step-card-hint--warn">
                Diese Aufgabe wurde bereits von einer anderen Admin-Person freigegeben.
              </p>
              <button type="button" className="btn btn-secondary" onClick={onClose}>
                Schließen
              </button>
            </div>
          )}

          {phase === "error-plan-unavailable" && (
            <div className="content-stack">
              <p className="wf-step-card-hint wf-step-card-hint--error">
                {errorDetail ?? "Plan-Berechnung fehlgeschlagen."}
              </p>
              <button type="button" className="btn btn-secondary" onClick={onClose}>
                Schließen
              </button>
            </div>
          )}

          {phase === "error-no-role" && (
            <div className="content-stack">
              <p className="wf-step-card-hint wf-step-card-hint--error">
                Du hast keine Berechtigung, diese Aufgabe freizugeben.
              </p>
              <button type="button" className="btn btn-secondary" onClick={onClose}>
                Schließen
              </button>
            </div>
          )}

          {phase === "error-config" && (
            <div className="content-stack">
              <p className="wf-step-card-hint wf-step-card-hint--error">
                Diese Aufgabe ist für die Freigabe nicht konfiguriert. Bitte einen Admin verständigen.
              </p>
              <button type="button" className="btn btn-secondary" onClick={onClose}>
                Schließen
              </button>
            </div>
          )}
        </div>
      </aside>
    </>
  );
}

// ---------- Plan-Vorschau + Action-Renderer ----------

function PlanReview(props: {
  plan: AutomationPlanResponse;
  driftNotice: string | null;
  errorDetail: string | null;
  submitting: boolean;
  onApprove: () => void;
}) {
  const { plan, driftNotice, errorDetail, submitting, onApprove } = props;
  const totalSteps = plan.steps.length;
  const unplannableCount = useMemo(
    () => plan.steps.filter((s) => !s.isSuccess).length,
    [plan.steps],
  );
  const hasUnplannableStep = unplannableCount > 0;

  return (
    <div className="content-stack">
      <section className="wfa-bundle-head">
        <p className="wfa-bundle-head-count">
          {totalSteps === 1
            ? "1 Aktion wird bei Bestätigung ausgeführt."
            : `${totalSteps} Aktionen werden bei Bestätigung in dieser Reihenfolge ausgeführt.`}
        </p>
        <p className="wfa-bundle-head-note">
          Schlägt ein Schritt fehl, bleibt die Aufgabe offen — bereits ausgeführte Schritte
          werden nicht zurückgerollt und müssen ggf. manuell nachgepflegt werden.
        </p>
      </section>

      {driftNotice ? (
        <p className="wf-step-card-hint wf-step-card-hint--warn">{driftNotice}</p>
      ) : null}
      {errorDetail ? (
        <p className="wf-step-card-hint wf-step-card-hint--error">{errorDetail}</p>
      ) : null}
      {hasUnplannableStep ? (
        <p className="wf-step-card-hint wf-step-card-hint--error">
          {unplannableCount === 1
            ? "Ein Schritt kann derzeit nicht geplant werden — bitte die Aufgabe später erneut öffnen oder die Konfiguration prüfen."
            : `${unplannableCount} Schritte können derzeit nicht geplant werden — bitte die Aufgabe später erneut öffnen oder die Konfiguration prüfen.`}
        </p>
      ) : null}

      <ol className="wfa-bundle-steps">
        {plan.steps.map((step, idx) => (
          <li
            key={`${step.actionKey}-${idx}`}
            className={
              step.isSuccess
                ? "wfa-bundle-step"
                : "wfa-bundle-step wfa-bundle-step--error"
            }
          >
            <span className="wfa-bundle-step-num" aria-hidden="true">
              {idx + 1}
            </span>
            <div className="wfa-bundle-step-body">
              <PlanStepDetail step={step} />
            </div>
          </li>
        ))}
      </ol>

      <button
        type="button"
        className="btn btn-primary"
        onClick={onApprove}
        disabled={submitting || hasUnplannableStep}
        title={hasUnplannableStep ? "Mindestens ein Schritt ist nicht planbar." : undefined}
      >
        {submitting ? "Wird genehmigt…" : "Genehmigen & ausführen"}
      </button>
    </div>
  );
}

function PlanStepDetail({ step }: { step: AutomationPlanStep }) {
  const label = getAutomationActionLabel(step.actionKey);
  if (!step.isSuccess) {
    return (
      <div className="content-stack">
        <div className="wfa-bundle-step-title">
          <strong>{label}</strong>
          <span className="wfa-bundle-step-key">{step.actionKey}</span>
        </div>
        <p className="wf-step-card-hint wf-step-card-hint--error">
          Plan nicht verfügbar: {step.errorMessage ?? "unbekannter Fehler"}
        </p>
      </div>
    );
  }

  return (
    <div className="content-stack">
      <div className="wfa-bundle-step-title">
        <strong>{label}</strong>
        <span className="wfa-bundle-step-key">{step.actionKey}</span>
      </div>
      <PlanStepBody actionKey={step.actionKey} plan={step.plan} />
    </div>
  );
}

function PlanStepBody({ actionKey, plan }: { actionKey: string; plan: unknown }) {
  if (plan === null || plan === undefined) {
    return <p>Keine Plan-Details verfügbar.</p>;
  }

  switch (actionKey) {
    case "CreateAdUserLdaps":
      return <AdUserPlanCard detail={plan as AdUserPlanDetail} />;
    case "AssignGroupsLdaps":
      return <GroupAssignmentPlanCard detail={plan as GroupAssignmentPlanDetail} />;
    case "CreateMailboxGraph":
      return <MailboxPlanCard detail={plan as MailboxPlanDetail} />;
    case "SendWelcomeMailGraph":
      return <WelcomeMailPlanCard detail={plan as WelcomeMailPlanDetail} />;
    default:
      return (
        <pre className="wf-condition-drawer-route">{JSON.stringify(plan, null, 2)}</pre>
      );
  }
}

function AdUserPlanCard({ detail }: { detail: AdUserPlanDetail }) {
  if (detail.alreadyExists) {
    return (
      <ul>
        <li>AD-User existiert bereits.</li>
        {detail.existingDn ? <li>DN: {detail.existingDn}</li> : null}
        <li>Passwort: {detail.passwordNote}</li>
      </ul>
    );
  }
  return (
    <ul>
      <li>AD-User wird angelegt.</li>
      {detail.targetDn ? <li>Ziel-DN: {detail.targetDn}</li> : null}
      {detail.userPrincipalName ? <li>UPN: {detail.userPrincipalName}</li> : null}
      {detail.samAccountName ? <li>SAM: {detail.samAccountName}</li> : null}
      {detail.displayName ? <li>Anzeigename: {detail.displayName}</li> : null}
      <li>Passwort: {detail.passwordNote}</li>
    </ul>
  );
}

function GroupAssignmentPlanCard({ detail }: { detail: GroupAssignmentPlanDetail }) {
  return (
    <div>
      {detail.note ? <p className="wf-step-card-hint">{detail.note}</p> : null}
      {detail.userDistinguishedName ? (
        <p>User-DN: {detail.userDistinguishedName}</p>
      ) : null}
      <ul>
        {detail.groups.map((group, idx) => (
          <li key={`group-${idx}`}>
            + {group.groupDn}
            {group.alreadyMember === true ? " (bereits Mitglied)" : null}
            {group.alreadyMember === false ? " (wird hinzugefügt)" : null}
            {group.source ? ` — von ${group.source}` : null}
          </li>
        ))}
      </ul>
    </div>
  );
}

function MailboxPlanCard({ detail }: { detail: MailboxPlanDetail }) {
  return (
    <ul>
      {detail.upnKnown ? (
        <li>UPN: {detail.userPrincipalName}</li>
      ) : (
        <li>UPN noch nicht bekannt — {detail.upnNote}</li>
      )}
      <li>
        Lizenz-SKU: {detail.skuDisplayName ?? detail.skuId}
      </li>
      <li>SMTP-Adresse: {detail.smtpNote}</li>
    </ul>
  );
}

function WelcomeMailPlanCard({ detail }: { detail: WelcomeMailPlanDetail }) {
  return (
    <div>
      {detail.recipientKnown ? (
        <p>Empfänger: {detail.recipient}</p>
      ) : (
        <p>Empfänger noch nicht bekannt — {detail.note}</p>
      )}
      {detail.subject ? <p>Betreff: {detail.subject}</p> : null}
      <p>Initial-Passwort: {renderPasswordAvailability(detail.passwordAvailability)}</p>
      {detail.bodyPreview ? (
        <details>
          <summary>Body-Vorschau</summary>
          <pre className="wf-condition-drawer-route">{detail.bodyPreview}</pre>
        </details>
      ) : null}
    </div>
  );
}

function renderPasswordAvailability(state: WelcomeMailPlanDetail["passwordAvailability"]): string {
  switch (state) {
    case "PresentInVault":
      return "im Vault verfügbar";
    case "WillBeGenerated":
      return "wird beim Ausführen erzeugt";
    case "Unknown":
      return "Status unbekannt (AD-Vorgänger noch nicht geplant)";
    case "NotAvailable":
      return "kein neues Passwort (User existiert bereits)";
  }
}

// ---------- Slice 7: Live-Status-Rendering ----------

function LiveStatusList({ steps }: { steps: AutomationApprovalStatusStep[] }) {
  if (steps.length === 0) return null;
  return (
    <ol className="wfa-bundle-steps">
      {steps.map((step) => (
        <li
          key={`status-${step.actionKey}-${step.executionOrder}`}
          className={
            step.jobStatus === "failed"
              ? "wfa-bundle-step wfa-bundle-step--error"
              : "wfa-bundle-step"
          }
        >
          <span className="wfa-bundle-step-num" aria-hidden="true">
            {step.executionOrder}
          </span>
          <div className="wfa-bundle-step-body">
            <LiveStatusStep step={step} />
          </div>
        </li>
      ))}
    </ol>
  );
}

function LiveStatusStep({ step }: { step: AutomationApprovalStatusStep }) {
  const label = getAutomationActionLabel(step.actionKey);
  const showAttempt = step.currentAttempt > 1
    && (step.jobStatus === "running" || step.jobStatus === "failed");
  return (
    <div className="content-stack">
      <div className="wfa-bundle-step-title">
        <strong>{label}</strong>
        <span className="wfa-bundle-step-key">{step.actionKey}</span>
        <StatusPill status={step.jobStatus} />
        {showAttempt ? (
          <span className="wfa-status-attempt">
            Versuch {step.currentAttempt}/{step.maxAttempts}
          </span>
        ) : null}
      </div>
      {step.jobStatus === "failed" && step.latestErrorMessage ? (
        <p className="wf-step-card-hint wf-step-card-hint--error">
          {step.latestErrorMessage}
          {step.latestFailureKind ? (
            <> — {renderFailureKind(step.latestFailureKind)}</>
          ) : null}
        </p>
      ) : null}
      {step.logs.length > 0 ? (
        <details className="wfa-status-logs">
          <summary>Details ({step.logs.length})</summary>
          <ul>
            {step.logs.map((log, idx) => (
              <li key={`log-${idx}`}>
                <span className="wfa-status-logs-time">
                  {new Date(log.createdAt).toLocaleTimeString()}
                </span>
                <span className="wfa-status-logs-level">[{log.level}]</span>
                <span>{log.message}</span>
              </li>
            ))}
          </ul>
        </details>
      ) : null}
    </div>
  );
}

function StatusPill({ status }: { status: AutomationApprovalStatusStep["jobStatus"] }) {
  return (
    <span className={`wfa-status-pill wfa-status-pill--${status}`}>
      {renderStatusLabel(status)}
    </span>
  );
}

function renderStatusLabel(status: AutomationApprovalStatusStep["jobStatus"]): string {
  switch (status) {
    case "pending":   return "wartet";
    case "running":   return "läuft";
    case "succeeded": return "erfolgreich";
    case "failed":    return "fehlgeschlagen";
    case "cancelled": return "abgebrochen";
  }
}

function renderFailureKind(kind: "permanent" | "transient"): string {
  return kind === "permanent" ? "permanenter Fehler" : "vorübergehender Fehler";
}

function buildFailureSummary(steps: AutomationApprovalStatusStep[]): string {
  const failed = steps.find((s) => s.jobStatus === "failed");
  if (!failed) return "Eine Aktion ist fehlgeschlagen.";
  const label = getAutomationActionLabel(failed.actionKey);
  if (failed.latestErrorMessage) {
    return `${label} (Schritt ${failed.executionOrder}): ${failed.latestErrorMessage}`;
  }
  return `${label} (Schritt ${failed.executionOrder}) fehlgeschlagen.`;
}


