import { useCallback, useState } from "react";
import { useNavigate } from "react-router-dom";
import type { RoleCapabilities } from "../../auth/roleModel";
import { useConfirmationDialog } from "../feedback/useConfirmationDialog";
import {
  useArchiveWorkflow,
  useCancelWorkflow,
  useDeleteWorkflow,
} from "../../services/mutations/workflowMutations";
import type { WorkflowCancellationRequest, WorkflowDetail } from "../../types/workflow";
import { CancelWorkflowDialog } from "./CancelWorkflowDialog";
import { WORKFLOW_CANCELLATION_REASON_OPTIONS } from "./workflowCancellationReasons";

interface WorkflowManagementPanelProps {
  uid: string;
  workflow: WorkflowDetail;
  capabilities: Pick<
    RoleCapabilities,
    "canManageAdminConfiguration" | "canCreateWorkflow" | "hasHrRole" | "hasManagerRole" | "hasAdminRole"
  >;
}

const CANCELLABLE_STATUSES: ReadonlyArray<WorkflowDetail["workflowStatus"]> = [
  "in_progress",
  "waiting_for_supervisor",
  "waiting_for_department",
];

export default function WorkflowManagementPanel({
  uid,
  workflow,
  capabilities,
}: WorkflowManagementPanelProps) {
  const navigate = useNavigate();
  const confirm = useConfirmationDialog();
  const archiveMutation = useArchiveWorkflow();
  const deleteMutation = useDeleteWorkflow();
  const cancelMutation = useCancelWorkflow();
  const [isArchiving, setIsArchiving] = useState(false);
  const [archiveError, setArchiveError] = useState<string | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [isCancelDialogOpen, setIsCancelDialogOpen] = useState(false);
  const [isCancelling, setIsCancelling] = useState(false);
  const [cancelError, setCancelError] = useState<string | null>(null);

  const showArchive = capabilities.canManageAdminConfiguration && workflow.workflowStatus === "completed" && !workflow.archivedAt;
  const showDelete = (capabilities.canCreateWorkflow || capabilities.canManageAdminConfiguration) && workflow.workflowStatus === "draft";
  // Storno: aktive Statuses. Backend entscheidet das letzte AuthZ-Wort (Manager nur eigene Abteilung).
  // Hier zeigen wir den Button fuer Admin/HR/Manager. Worker/Reader sehen ihn nicht.
  const canTriggerCancel =
    capabilities.hasAdminRole || capabilities.hasHrRole || capabilities.hasManagerRole;
  const showCancel = canTriggerCancel && CANCELLABLE_STATUSES.includes(workflow.workflowStatus);
  const showCancellationInfo = workflow.workflowStatus === "cancelled" && workflow.cancelledAt;

  const handleArchive = useCallback(async () => {
    if (!uid.trim()) return;
    setIsArchiving(true);
    setArchiveError(null);
    try {
      await archiveMutation.mutateAsync(uid);
    } catch (err) {
      setArchiveError(err instanceof Error ? err.message : "Vorgang konnte nicht archiviert werden.");
    } finally {
      setIsArchiving(false);
    }
  }, [archiveMutation, uid]);

  const handleDelete = useCallback(async () => {
    if (!uid.trim()) return;
    const shouldDelete = await confirm({
      title: "Entwurf löschen?",
      description: "Der Entwurf wird dauerhaft entfernt. Diese Aktion kann nicht rückgängig gemacht werden.",
      confirmLabel: "Entwurf löschen",
      tone: "danger",
    });
    if (!shouldDelete) return;
    setIsDeleting(true);
    setDeleteError(null);
    try {
      await deleteMutation.mutateAsync(uid);
      navigate("/workflows");
    } catch (err) {
      setDeleteError(err instanceof Error ? err.message : "Vorgang konnte nicht gelöscht werden.");
      setIsDeleting(false);
    }
  }, [confirm, deleteMutation, uid, navigate]);

  const handleCancelConfirm = useCallback(
    async (request: WorkflowCancellationRequest) => {
      if (!uid.trim()) return;
      setIsCancelling(true);
      setCancelError(null);
      try {
        await cancelMutation.mutateAsync({ uid, request });
        setIsCancelDialogOpen(false);
      } catch (err) {
        setCancelError(err instanceof Error ? err.message : "Vorgang konnte nicht storniert werden.");
      } finally {
        setIsCancelling(false);
      }
    },
    [cancelMutation, uid],
  );

  const handleCancelDialogClose = useCallback(() => {
    if (isCancelling) return;
    setIsCancelDialogOpen(false);
    setCancelError(null);
  }, [isCancelling]);

  if (!showArchive && !showDelete && !showCancel && !showCancellationInfo) {
    return null;
  }

  const cancellationReasonLabel = workflow.cancellationReasonCode
    ? WORKFLOW_CANCELLATION_REASON_OPTIONS.find((option) => option.code === workflow.cancellationReasonCode)?.label ?? workflow.cancellationReasonCode
    : null;

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Verwaltung</h2>
        <p>Lifecycle-Aktionen für diesen Vorgang.</p>
      </div>
      <div className="panel-body">
        {showArchive ? (
          <div style={{ marginBottom: "1rem" }}>
            <p className="text-muted" style={{ marginBottom: "0.5rem" }}>
              Abgeschlossene Vorgänge können archiviert werden. Archivierte Vorgänge erscheinen nicht mehr in der Liste, sind aber über die direkte URL weiterhin einsehbar.
            </p>
            {archiveError ? <p className="text-error" style={{ marginBottom: "0.5rem" }}>{archiveError}</p> : null}
            <button
              className="btn btn-outline"
              onClick={() => void handleArchive()}
              disabled={isArchiving}
            >
              {isArchiving ? "Wird archiviert…" : "Vorgang archivieren"}
            </button>
          </div>
        ) : null}

        {showCancel ? (
          <div style={{ marginBottom: "1rem" }}>
            <p className="text-muted" style={{ marginBottom: "0.5rem" }}>
              Laufende Vorgänge können bei abgesagtem Eintritt, falscher Person oder versehentlichem Start storniert werden. Offene Aufgaben werden mit-storniert, ausstehende Benachrichtigungen werden gestoppt.
            </p>
            {cancelError ? <p className="text-error" style={{ marginBottom: "0.5rem" }}>{cancelError}</p> : null}
            <button
              className="btn btn-outline btn-danger"
              onClick={() => {
                setCancelError(null);
                setIsCancelDialogOpen(true);
              }}
              disabled={isCancelling}
            >
              Vorgang stornieren
            </button>
          </div>
        ) : null}

        {showCancellationInfo ? (
          <div className="workflow-cancellation-info" style={{ marginBottom: "1rem" }}>
            <h3 style={{ marginTop: 0 }}>Vorgang storniert</h3>
            <dl>
              <dt>Stornierungsgrund</dt>
              <dd>{cancellationReasonLabel ?? "—"}</dd>
              {workflow.cancellationReasonDetail ? (
                <>
                  <dt>Ergänzung</dt>
                  <dd>{workflow.cancellationReasonDetail}</dd>
                </>
              ) : null}
              {workflow.cancelledAt ? (
                <>
                  <dt>Storniert am</dt>
                  <dd>{new Date(workflow.cancelledAt).toLocaleString("de-DE")}</dd>
                </>
              ) : null}
            </dl>
          </div>
        ) : null}

        {showDelete ? (
          <div>
            <p className="text-muted" style={{ marginBottom: "0.5rem" }}>
              Entwürfe können gelöscht werden, solange der Prozess noch nicht gestartet wurde.
            </p>
            {deleteError ? <p className="text-error" style={{ marginBottom: "0.5rem" }}>{deleteError}</p> : null}
            <button
              className="btn btn-outline btn-danger"
              onClick={() => void handleDelete()}
              disabled={isDeleting}
            >
              {isDeleting ? "Wird gelöscht…" : "Entwurf löschen"}
            </button>
          </div>
        ) : null}
      </div>
      <CancelWorkflowDialog
        open={isCancelDialogOpen}
        isSubmitting={isCancelling}
        errorMessage={cancelError}
        onCancel={handleCancelDialogClose}
        onConfirm={handleCancelConfirm}
      />
    </section>
  );
}
