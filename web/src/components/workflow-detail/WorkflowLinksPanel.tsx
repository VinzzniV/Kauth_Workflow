import { useCallback, useEffect, useState } from "react";
import {
  createWorkflowLink,
  deleteWorkflowLink,
  findLinkableWorkflows,
  getWorkflowLinks,
} from "../../services/workflowApi";
import type { LinkableWorkflow, WorkflowDetail, WorkflowLink } from "../../types/workflow";

interface WorkflowLinksPanelProps {
  uid: string;
  workflow: WorkflowDetail;
}

export default function WorkflowLinksPanel({ uid, workflow }: WorkflowLinksPanelProps) {
  const [workflowLinks, setWorkflowLinks] = useState<WorkflowLink[]>([]);
  const [linkableWorkflows, setLinkableWorkflows] = useState<LinkableWorkflow[]>([]);
  const [showLinkDialog, setShowLinkDialog] = useState(false);
  const [linkError, setLinkError] = useState<string | null>(null);

  useEffect(() => {
    void getWorkflowLinks(uid).then(setWorkflowLinks).catch(() => setWorkflowLinks([]));
  }, [uid]);

  const handleOpenLinkDialog = useCallback(async () => {
    setShowLinkDialog(true);
    setLinkError(null);
    try {
      const linkable = await findLinkableWorkflows(workflow.employeeNumber, workflow.uid);
      setLinkableWorkflows(linkable);
    } catch {
      setLinkableWorkflows([]);
    }
  }, [workflow.employeeNumber, workflow.uid]);

  const handleCreateLink = useCallback(async (sourceUid: string) => {
    if (!uid.trim()) return;
    setLinkError(null);
    try {
      await createWorkflowLink(uid, { sourceWorkflowUid: sourceUid, linkType: "derived_from" });
      const links = await getWorkflowLinks(uid);
      setWorkflowLinks(links);
      setShowLinkDialog(false);
    } catch (err) {
      setLinkError(err instanceof Error ? err.message : "Verknüpfung konnte nicht erstellt werden.");
    }
  }, [uid]);

  const handleDeleteLink = useCallback(async (linkId: number) => {
    if (!uid.trim()) return;
    try {
      await deleteWorkflowLink(uid, linkId);
      setWorkflowLinks((prev) => prev.filter((l) => l.id !== linkId));
    } catch {
      // silent
    }
  }, [uid]);

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Verknüpfte Vorgänge</h2>
        <p>Beziehungen zu anderen Workflows desselben Mitarbeiters.</p>
      </div>
      <div className="panel-body">
        {workflowLinks.length === 0 && !showLinkDialog ? (
          <p className="text-muted">Keine Verknüpfungen vorhanden.</p>
        ) : null}

        {workflowLinks.length > 0 ? (
          <table className="table">
            <thead>
              <tr>
                <th>Prozesstyp</th>
                <th>Person</th>
                <th>Status</th>
                <th>Beziehung</th>
                <th>Erstellt</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {workflowLinks.map((link) => (
                <tr key={link.id}>
                  <td>{link.linkedWorkflowProcessType.name}</td>
                  <td>{link.linkedWorkflowFirstName} {link.linkedWorkflowLastName}</td>
                  <td>
                    <span className={`badge badge--${link.linkedWorkflowStatus === "completed" ? "success" : "default"}`}>
                      {link.linkedWorkflowStatus}
                    </span>
                  </td>
                  <td>{link.linkType === "derived_from" ? "Abgeleitet von" : link.linkType === "supersedes" ? "Ersetzt" : "Verwandt"}</td>
                  <td>{new Date(link.createdAt).toLocaleDateString("de-DE")}</td>
                  <td>
                    <button className="btn btn-sm btn-outline" onClick={() => void handleDeleteLink(link.id)}>
                      Entfernen
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : null}

        {showLinkDialog ? (
          <div style={{ marginTop: "1rem" }}>
            {linkError ? <p className="text-error">{linkError}</p> : null}
            {linkableWorkflows.length === 0 ? (
              <p className="text-muted">Keine verknüpfbaren Vorgänge für diese Personalnummer gefunden.</p>
            ) : (
              <table className="table">
                <thead>
                  <tr>
                    <th>Prozesstyp</th>
                    <th>Person</th>
                    <th>Status</th>
                    <th>Erstellt</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {linkableWorkflows.map((lw) => (
                    <tr key={lw.uid}>
                      <td>{lw.processType.name}</td>
                      <td>{lw.firstName} {lw.lastName}</td>
                      <td>{lw.workflowStatus}</td>
                      <td>{new Date(lw.createdAt).toLocaleDateString("de-DE")}</td>
                      <td>
                        <button className="btn btn-sm btn-primary" onClick={() => void handleCreateLink(lw.uid)}>
                          Verknüpfen
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
            <button className="btn btn-sm btn-outline" style={{ marginTop: "0.5rem" }} onClick={() => setShowLinkDialog(false)}>
              Abbrechen
            </button>
          </div>
        ) : (
          <button className="btn btn-sm btn-outline" style={{ marginTop: "0.5rem" }} onClick={() => void handleOpenLinkDialog()}>
            Vorgang verknüpfen
          </button>
        )}
      </div>
    </section>
  );
}
