import type { RotationNotification } from "../../types/rotation";

type RotationNotificationsPanelProps = {
  title?: string;
  description?: string;
  notifications: RotationNotification[];
  isLoading: boolean;
  error: string | null;
};

function toNotificationTypeLabel(notificationType: string): string {
  switch (notificationType) {
    case "upcoming_change":
      return "Kommender Wechsel";
    case "reminder":
      return "Erinnerung";
    case "overdue":
      return "Überfällig";
    case "escalation":
      return "Eskalation";
    default:
      return notificationType;
  }
}

function toStatusLabel(status: RotationNotification["status"]): string {
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

function toStatusBadgeClass(status: RotationNotification["status"]): string {
  switch (status) {
    case "sent":
      return "badge badge--success";
    case "failed":
      return "badge badge--error";
    default:
      return "badge badge--default";
  }
}

function formatTimestamp(value: string | null): string {
  if (!value) {
    return "—";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString("de-DE", {
    dateStyle: "medium",
    timeStyle: "short",
  });
}

export default function RotationNotificationsPanel({
  title = "Benachrichtigungen",
  description = "Versandhistorie der Rotation-Benachrichtigungen dieses Durchlaufplans.",
  notifications,
  isLoading,
  error,
}: RotationNotificationsPanelProps) {
  return (
    <section className="panel">
      <div className="panel-head">
        <h2>{title}</h2>
        <p>{description}</p>
      </div>

      {isLoading ? <p className="panel-note">Benachrichtigungen werden geladen...</p> : null}
      {!isLoading && error ? <p className="panel-note">{error}</p> : null}
      {!isLoading && !error && notifications.length === 0 ? (
        <p className="panel-note">Noch keine Benachrichtigungen vorhanden.</p>
      ) : null}

      {!isLoading && !error && notifications.length > 0 ? (
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
                  <span>{notification.recipientName ?? notification.payload?.recipientName ?? "Unbekannt"}</span>
                  <br />
                  <span className="text-muted" style={{ fontSize: "0.85em" }}>
                    {notification.recipientEmail}
                  </span>
                </td>
                <td>
                  <span className={toStatusBadgeClass(notification.status)}>
                    {toStatusLabel(notification.status)}
                  </span>
                </td>
                <td>{notification.attempts}</td>
                <td>{formatTimestamp(notification.sentAt)}</td>
                <td>
                  {notification.lastError ? (
                    <span className="text-error" style={{ fontSize: "0.85em" }}>{notification.lastError}</span>
                  ) : "—"}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : null}
    </section>
  );
}
