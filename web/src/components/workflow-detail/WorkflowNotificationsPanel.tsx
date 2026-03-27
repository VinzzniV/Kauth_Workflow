import type { WorkflowNotification } from "../../types/workflow";

interface WorkflowNotificationsPanelProps {
  notifications: WorkflowNotification[];
}

function toNotificationTypeLabel(notificationType: string): string {
  switch (notificationType) {
    case "workflow_created":
      return "Workflow gestartet";
    case "task_ready":
      return "Aufgabe bereit";
    case "workflow_completed":
      return "Workflow abgeschlossen";
    default:
      return notificationType;
  }
}

function toStatusLabel(status: WorkflowNotification["status"]): string {
  switch (status) {
    case "pending":
      return "Ausstehend";
    case "sent":
      return "Gesendet";
    case "failed":
      return "Fehlgeschlagen";
    case "disabled":
      return "Deaktiviert";
    default:
      return status;
  }
}

function toStatusBadgeClass(status: WorkflowNotification["status"]): string {
  switch (status) {
    case "sent":
      return "badge badge--success";
    case "failed":
      return "badge badge--error";
    case "disabled":
      return "badge badge--default";
    default:
      return "badge badge--default";
  }
}

export default function WorkflowNotificationsPanel({ notifications }: WorkflowNotificationsPanelProps) {
  if (notifications.length === 0) {
    return (
      <section className="panel">
        <div className="panel-head">
          <h2>Benachrichtigungen</h2>
          <p>Admin-Ansicht: Versandstatus aller Benachrichtigungen dieses Vorgangs.</p>
        </div>
        <div className="panel-body">
          <p className="text-muted">Keine Benachrichtigungen für diesen Vorgang.</p>
        </div>
      </section>
    );
  }

  const failedCount = notifications.filter((n) => n.status === "failed").length;

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Benachrichtigungen</h2>
        <p>Admin-Ansicht: Versandstatus aller Benachrichtigungen dieses Vorgangs.</p>
      </div>
      <div className="panel-body">
        {failedCount > 0 ? (
          <p className="text-error" style={{ marginBottom: "0.75rem" }}>
            {failedCount} Benachrichtigung{failedCount > 1 ? "en" : ""} fehlgeschlagen.
          </p>
        ) : null}

        <table className="table">
          <thead>
            <tr>
              <th>Typ</th>
              <th>Empfänger</th>
              <th>Status</th>
              <th>Versuche</th>
              <th>Gesendet</th>
              <th>Fehler</th>
            </tr>
          </thead>
          <tbody>
            {notifications.map((notification) => (
              <tr key={notification.id}>
                <td>{toNotificationTypeLabel(notification.notificationType)}</td>
                <td>
                  <span>{notification.targetName}</span>
                  <br />
                  <span className="text-muted" style={{ fontSize: "0.85em" }}>{notification.targetEmail}</span>
                </td>
                <td>
                  <span className={toStatusBadgeClass(notification.status)}>
                    {toStatusLabel(notification.status)}
                  </span>
                </td>
                <td>{notification.attempts}</td>
                <td>
                  {notification.sentAt
                    ? new Date(notification.sentAt).toLocaleString("de-DE")
                    : "—"}
                </td>
                <td>
                  {notification.lastError ? (
                    <span className="text-error" style={{ fontSize: "0.85em" }}>{notification.lastError}</span>
                  ) : "—"}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
