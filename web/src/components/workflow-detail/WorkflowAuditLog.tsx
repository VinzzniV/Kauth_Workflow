import { Fragment } from "react";
import type { WorkflowAuditEntry } from "../../types/workflow";

type WorkflowAuditLogProps = {
  entries: WorkflowAuditEntry[];
  isLoading: boolean;
  error: string | null;
};

function toEventLabel(eventType: string): string {
  switch (eventType) {
    case "workflow_created":
      return "Workflow erstellt";
    case "workflow_status_changed":
      return "Workflow-Status geändert";
    case "task_status_changed":
      return "Task-Status geändert";
    case "task_assigned":
      return "Task neu zugewiesen";
    case "supervisor_step_completed":
      return "Abteilungsleitungs-Schritt abgeschlossen";
    case "task_comment_added":
      return "Kommentar hinzugefügt";
    case "tasks_generated":
      return "Aufgaben generiert";
    default:
      return eventType;
  }
}

function toStatusLabel(status: string | null | undefined): string {
  switch (status) {
    case "open":
      return "Offen";
    case "ready":
      return "Bereit";
    case "in_progress":
      return "In Bearbeitung";
    case "blocked":
      return "Blockiert";
    case "done":
      return "Erledigt";
    case "draft":
      return "Entwurf";
    case "waiting_for_supervisor":
      return "Wartet auf Abteilungsleitung";
    case "waiting_for_department":
      return "Wartet auf Abteilung";
    case "completed":
      return "Abgeschlossen";
    default:
      return status ?? "";
  }
}

function formatAuditTimestamp(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString("de-DE", {
    dateStyle: "medium",
    timeStyle: "short",
  });
}

function formatAuditDate(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleDateString("de-DE", {
    dateStyle: "full",
  });
}

function toChangeLabel(entry: WorkflowAuditEntry): string | null {
  const isStatusChange =
    entry.eventType === "task_status_changed" ||
    entry.eventType === "workflow_status_changed";

  const oldLabel = isStatusChange ? toStatusLabel(entry.oldValue) : entry.oldValue;
  const newLabel = isStatusChange ? toStatusLabel(entry.newValue) : entry.newValue;

  if (!oldLabel && !newLabel) {
    return null;
  }

  if (!oldLabel) {
    return `Neu: ${newLabel}`;
  }

  if (!newLabel) {
    return `Vorher: ${oldLabel}`;
  }

  return `${oldLabel} -> ${newLabel}`;
}

export default function WorkflowAuditLog({
  entries,
  isLoading,
  error,
}: WorkflowAuditLogProps) {
  return (
    <details className="panel" name="workflow-audit-log">
      <summary className="panel-head" style={{ cursor: "pointer", listStyle: "none" }}>
        <div>
          <h2>Verlauf / Audit-Log</h2>
          <p>Append-only Verlauf der wichtigsten Workflow- und Task-Änderungen.</p>
        </div>
      </summary>

      {isLoading ? <p className="panel-note">Audit-Log wird geladen...</p> : null}
      {!isLoading && error ? <p className="panel-note">{error}</p> : null}

      {!isLoading && !error && entries.length === 0 ? (
        <p className="panel-note">Noch keine Audit-Einträge vorhanden.</p>
      ) : null}

      {!isLoading && !error && entries.length > 0 ? (
        <div className="task-list">
          {entries.map((entry, index) => {
            const changeLabel = toChangeLabel(entry);
            const displayDetail = entry.taskTitle && entry.detail ? entry.detail : null;
            const entryDate = formatAuditDate(entry.createdAt);
            const previousEntryDate = index > 0 ? formatAuditDate(entries[index - 1].createdAt) : null;
            const showDateHeader = index === 0 || entryDate !== previousEntryDate;

            return (
              <Fragment key={entry.id}>
                {showDateHeader ? (
                  <p className="panel-note" style={{ fontWeight: "bold", marginTop: "0.5rem" }}>
                    {entryDate}
                  </p>
                ) : null}

                <article className="task-card">
                  <div className="task-card-top">
                    <div>
                      <h3>{toEventLabel(entry.eventType)}</h3>
                      <p className="panel-text">
                        {entry.taskTitle ?? entry.detail ?? "Ohne zusätzliche Details"}
                      </p>
                    </div>
                    <span className="chip">{formatAuditTimestamp(entry.createdAt)}</span>
                  </div>

                  <p className="panel-note">
                    Akteur: {entry.actorUserName ?? "System"}
                    {entry.taskKey ? ` | Task-Key: ${entry.taskKey}` : ""}
                    {changeLabel ? ` | ${changeLabel}` : ""}
                  </p>

                  {displayDetail ? (
                    <p className="panel-note">{displayDetail}</p>
                  ) : null}
                </article>
              </Fragment>
            );
          })}
        </div>
      ) : null}
    </details>
  );
}
