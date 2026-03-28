import { useCallback, useState } from "react";
import { useNavigate } from "react-router-dom";
import type { RoleCapabilities } from "../../auth/roleModel";
import { useConfirmationDialog } from "../feedback/ConfirmationDialogProvider";
import { useArchiveWorkflow, useDeleteWorkflow } from "../../services/mutations/workflowMutations";
import type { WorkflowDetail } from "../../types/workflow";

interface WorkflowManagementPanelProps {
  uid: string;
  workflow: WorkflowDetail;
  capabilities: Pick<RoleCapabilities, "canManageAdminConfiguration" | "canCreateWorkflow">;
}

export default function WorkflowManagementPanel({
  uid,
  workflow,
  capabilities,
}: WorkflowManagementPanelProps) {
  const navigate = useNavigate();
  const confirm = useConfirmationDialog();
  const archiveMutation = useArchiveWorkflow();
  const deleteMutation = useDeleteWorkflow();
  const [isArchiving, setIsArchiving] = useState(false);
  const [archiveError, setArchiveError] = useState<string | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  const showArchive = capabilities.canManageAdminConfiguration && workflow.workflowStatus === "completed" && !workflow.archivedAt;
  const showDelete = (capabilities.canCreateWorkflow || capabilities.canManageAdminConfiguration) && workflow.workflowStatus === "draft";

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

  if (!showArchive && !showDelete) {
    return null;
  }

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
    </section>
  );
}
