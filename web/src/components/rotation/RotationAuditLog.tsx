import { Fragment } from "react";
import type { RotationAuditEntry } from "../../types/rotation";

type RotationAuditLogProps = {
  title?: string;
  description?: string;
  entries: RotationAuditEntry[];
  isLoading: boolean;
  error: string | null;
};

function toEventLabel(eventType: string): string {
  switch (eventType) {
    case "rotation_plan_created":
      return "Durchlaufplan erstellt";
    case "rotation_station_created":
      return "Station erstellt";
    case "rotation_station_updated":
      return "Station aktualisiert";
    case "rotation_station_deleted":
      return "Station gelöscht";
    case "rotation_task_generated":
      return "Maßnahme erzeugt";
    case "rotation_task_updated":
      return "Maßnahme aktualisiert";
    case "rotation_task_cancelled":
      return "Maßnahme storniert";
    case "rotation_task_status_changed":
      return "Status geändert";
    case "rotation_task_assigned":
      return "Zuweisung geändert";
    case "rotation_task_comment_added":
      return "Kommentar hinzugefügt";
    case "rotation_task_sync_completed":
      return "Task-Synchronisierung abgeschlossen";
    case "rotation_notification_sent":
      return "Benachrichtigung versendet";
    case "rotation_notification_failed":
      return "Benachrichtigung fehlgeschlagen";
    case "rotation_notification_disabled":
      return "Benachrichtigung deaktiviert";
    default:
      return eventType;
  }
}

function formatDateHeading(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleDateString("de-DE", { dateStyle: "full" });
}

function formatTimestamp(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString("de-DE", {
    dateStyle: "medium",
    timeStyle: "short",
  });
}

function tryReadStatusLabel(value: unknown): string | null {
  if (typeof value !== "string") {
    return null;
  }

  switch (value) {
    case "draft":
      return "Entwurf";
    case "active":
      return "Aktiv";
    case "planned":
      return "Geplant";
    case "open":
      return "Offen";
    case "in_progress":
      return "In Bearbeitung";
    case "completed":
      return "Abgeschlossen";
    case "failed":
      return "Fehlgeschlagen";
    case "cancelled":
      return "Storniert";
    case "sent":
      return "Gesendet";
    case "disabled":
      return "Deaktiviert";
    default:
      return value;
  }
}

function formatChange(entry: RotationAuditEntry): string | null {
  const oldStatus = tryReadStatusLabel(entry.oldValue?.status);
  const newStatus = tryReadStatusLabel(entry.newValue?.status);
  if (oldStatus || newStatus) {
    if (oldStatus && newStatus) {
      return `${oldStatus} -> ${newStatus}`;
    }

    return oldStatus ? `Vorher: ${oldStatus}` : `Neu: ${newStatus}`;
  }

  const oldAssignee = typeof entry.oldValue?.assignee === "string" ? entry.oldValue.assignee : null;
  const newAssignee = typeof entry.newValue?.assignee === "string" ? entry.newValue.assignee : null;
  if (oldAssignee || newAssignee) {
    if (oldAssignee && newAssignee) {
      return `${oldAssignee} -> ${newAssignee}`;
    }

    return oldAssignee ? `Vorher: ${oldAssignee}` : `Neu: ${newAssignee}`;
  }

  return null;
}

export default function RotationAuditLog({
  title = "Verlauf / Audit",
  description = "Chronologischer Verlauf der wichtigsten Plan-, Stations-, Task- und Versandereignisse.",
  entries,
  isLoading,
  error,
}: RotationAuditLogProps) {
  return (
    <details className="panel" name="rotation-audit-log" open>
      <summary className="panel-head" style={{ cursor: "pointer", listStyle: "none" }}>
        <div>
          <h2>{title}</h2>
          <p>{description}</p>
        </div>
      </summary>

      {isLoading ? <p className="panel-note">Historie wird geladen...</p> : null}
      {!isLoading && error ? <p className="panel-note">{error}</p> : null}
      {!isLoading && !error && entries.length === 0 ? (
        <p className="panel-note">Noch keine Historieneinträge vorhanden.</p>
      ) : null}

      {!isLoading && !error && entries.length > 0 ? (
        <div className="task-list">
          {entries.map((entry, index) => {
            const currentDate = formatDateHeading(entry.createdAt);
            const previousDate = index > 0 ? formatDateHeading(entries[index - 1]!.createdAt) : null;
            const showDateHeader = index === 0 || currentDate !== previousDate;
            const change = formatChange(entry);

            return (
              <Fragment key={entry.id}>
                {showDateHeader ? (
                  <p className="panel-note" style={{ fontWeight: "bold", marginTop: "0.5rem" }}>
                    {currentDate}
                  </p>
                ) : null}

                <article className="task-card">
                  <div className="task-card-top">
                    <div>
                      <h3>{toEventLabel(entry.eventType)}</h3>
                      <p className="panel-text">{entry.detail ?? "Ohne zusätzliche Details"}</p>
                    </div>
                    <span className="chip">{formatTimestamp(entry.createdAt)}</span>
                  </div>
                  <p className="panel-note">
                    Akteur: {entry.actorUserName ?? "System"}
                    {entry.generatedTaskId ? ` | Task: rot:${entry.generatedTaskId}` : ""}
                    {change ? ` | ${change}` : ""}
                  </p>
                </article>
              </Fragment>
            );
          })}
        </div>
      ) : null}
    </details>
  );
}
