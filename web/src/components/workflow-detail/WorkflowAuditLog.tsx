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
    default:
      return eventType;
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

function toChangeLabel(entry: WorkflowAuditEntry): string | null {
  if (!entry.oldValue && !entry.newValue) {
    return null;
  }

  if (!entry.oldValue) {
    return `Neu: ${entry.newValue}`;
  }

  if (!entry.newValue) {
    return `Vorher: ${entry.oldValue}`;
  }

  return `${entry.oldValue} -> ${entry.newValue}`;
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
          {entries.map((entry) => {
            const changeLabel = toChangeLabel(entry);
            const displayDetail = entry.detail && entry.detail !== `Task: ${entry.taskTitle ?? ""}` ? entry.detail : null;

            return (
              <article key={entry.id} className="task-card">
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
            );
          })}
        </div>
      ) : null}
    </details>
  );
}
