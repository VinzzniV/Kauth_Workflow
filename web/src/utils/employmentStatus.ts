export function formatEmploymentStatus(status: string | null): string {
  switch (status) {
    case "planned":
      return "Geplant";
    case "active":
      return "Aktiv";
    case "inactive":
      return "Inaktiv";
    case "exited":
      return "Ausgetreten";
    default:
      return "–";
  }
}

export function formatDirectoryLinkStatus(status: string | null): string {
  switch (status) {
    case "linked":
      return "Mit Verzeichnis verknüpft";
    case "user_only":
      return "Nur App-Benutzer verknüpft";
    case "unlinked":
      return "Noch nicht verknüpft";
    default:
      return "–";
  }
}

export function formatDirectoryLinkStatusShort(status: string | null): string {
  switch (status) {
    case "linked":
      return "Verknüpft";
    case "user_only":
      return "Nur App";
    case "unlinked":
      return "Nicht verknüpft";
    default:
      return "–";
  }
}

export function getEmploymentStatusClass(status: string | null): string {
  switch (status) {
    case "active":
      return "status-pill running";
    case "planned":
      return "status-pill open";
    case "inactive":
    case "exited":
      return "status-pill completed";
    default:
      return "status-pill";
  }
}
