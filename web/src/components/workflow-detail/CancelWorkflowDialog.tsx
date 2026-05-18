import {
  useEffect,
  useMemo,
  useRef,
  useState,
  type FormEvent,
} from "react";
import type {
  WorkflowCancellationReasonCode,
  WorkflowCancellationRequest,
} from "../../types/workflow";
import { WORKFLOW_CANCELLATION_REASON_OPTIONS } from "./workflowCancellationReasons";

const DETAIL_MAX_LENGTH = 500;

export function CancelWorkflowDialog({
  open,
  isSubmitting,
  errorMessage,
  onCancel,
  onConfirm,
}: {
  open: boolean;
  isSubmitting: boolean;
  errorMessage?: string | null;
  onCancel: () => void;
  onConfirm: (request: WorkflowCancellationRequest) => void;
}) {
  if (!open) {
    return null;
  }

  return (
    <CancelWorkflowDialogContent
      isSubmitting={isSubmitting}
      errorMessage={errorMessage}
      onCancel={onCancel}
      onConfirm={onConfirm}
    />
  );
}

function CancelWorkflowDialogContent({
  isSubmitting,
  errorMessage,
  onCancel,
  onConfirm,
}: {
  isSubmitting: boolean;
  errorMessage?: string | null;
  onCancel: () => void;
  onConfirm: (request: WorkflowCancellationRequest) => void;
}) {
  const cancelButtonRef = useRef<HTMLButtonElement | null>(null);
  const [reasonCode, setReasonCode] = useState<WorkflowCancellationReasonCode | "">("");
  const [reasonDetail, setReasonDetail] = useState("");

  useEffect(() => {
    cancelButtonRef.current?.focus();

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape" && !isSubmitting) {
        onCancel();
      }
    };
    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [open, isSubmitting, onCancel]);

  const detailIsRequired = reasonCode === "other";

  const isValid = useMemo(() => {
    if (!reasonCode) {
      return false;
    }
    if (detailIsRequired && reasonDetail.trim().length === 0) {
      return false;
    }
    if (reasonDetail.length > DETAIL_MAX_LENGTH) {
      return false;
    }
    return true;
  }, [reasonCode, reasonDetail, detailIsRequired]);

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!isValid || isSubmitting || !reasonCode) {
      return;
    }
    onConfirm({
      reasonCode,
      reasonDetail: reasonDetail.trim().length > 0 ? reasonDetail.trim() : undefined,
    });
  };

  return (
    <div
      className="confirmation-dialog-backdrop"
      onClick={() => {
        if (!isSubmitting) {
          onCancel();
        }
      }}
    >
      <section
        className="confirmation-dialog confirmation-dialog--danger"
        role="alertdialog"
        aria-modal="true"
        aria-labelledby="cancel-workflow-dialog-title"
        aria-describedby="cancel-workflow-dialog-description"
        onClick={(event) => event.stopPropagation()}
      >
        <form onSubmit={handleSubmit}>
          <div className="panel-head">
            <h2 id="cancel-workflow-dialog-title">Workflow stornieren?</h2>
            <p id="cancel-workflow-dialog-description">
              Diese Aktion beendet den laufenden Vorgang. Offene Aufgaben werden mit-storniert,
              ausstehende Benachrichtigungen werden gestoppt. Diese Aktion kann nicht rückgängig
              gemacht werden.
            </p>
          </div>

          <div className="form-row">
            <label htmlFor="cancel-workflow-reason-code">
              Grund <span aria-hidden="true">*</span>
            </label>
            <select
              id="cancel-workflow-reason-code"
              value={reasonCode}
              onChange={(event) =>
                setReasonCode(event.target.value as WorkflowCancellationReasonCode | "")
              }
              disabled={isSubmitting}
              required
            >
              <option value="" disabled>
                Bitte Grund auswählen
              </option>
              {WORKFLOW_CANCELLATION_REASON_OPTIONS.map((option) => (
                <option key={option.code} value={option.code}>
                  {option.label}
                </option>
              ))}
            </select>
          </div>

          <div className="form-row">
            <label htmlFor="cancel-workflow-reason-detail">
              Ergänzung
              {detailIsRequired ? (
                <>
                  {" "}
                  <span aria-hidden="true">*</span>
                </>
              ) : null}
            </label>
            <textarea
              id="cancel-workflow-reason-detail"
              value={reasonDetail}
              onChange={(event) => setReasonDetail(event.target.value)}
              rows={3}
              maxLength={DETAIL_MAX_LENGTH}
              placeholder={
                detailIsRequired
                  ? "Bei 'Sonstiges' bitte Detail angeben."
                  : "Optionaler Hinweis (max. 500 Zeichen)."
              }
              disabled={isSubmitting}
              required={detailIsRequired}
            />
            <small className="form-hint">
              {reasonDetail.length}/{DETAIL_MAX_LENGTH} Zeichen
            </small>
          </div>

          {errorMessage ? (
            <p className="form-error" role="alert">
              {errorMessage}
            </p>
          ) : null}

          <div className="confirmation-dialog-actions">
            <button
              ref={cancelButtonRef}
              type="button"
              className="btn btn-secondary"
              onClick={onCancel}
              disabled={isSubmitting}
            >
              Abbrechen
            </button>
            <button
              type="submit"
              className="btn btn-danger"
              disabled={!isValid || isSubmitting}
            >
              {isSubmitting ? "Wird storniert…" : "Vorgang stornieren"}
            </button>
          </div>
        </form>
      </section>
    </div>
  );
}
