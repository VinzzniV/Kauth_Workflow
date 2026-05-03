import type {
  RotationGeneratedTask,
  RotationPlanStatus,
  RotationStationStatus,
} from "../../types/rotation";

export function getPlanStatusLabel(status: RotationPlanStatus): string {
  switch (status) {
    case "active":
      return "Aktiv";
    case "completed":
      return "Abgeschlossen";
    case "archived":
      return "Archiviert";
    default:
      return "Entwurf";
  }
}

export function getPlanStatusPillClass(status: RotationPlanStatus): string {
  switch (status) {
    case "active":
      return "running";
    case "completed":
    case "archived":
      return "completed";
    default:
      return "open";
  }
}

export function getStationStatusLabel(status: RotationStationStatus): string {
  switch (status) {
    case "active":
      return "Aktiv";
    case "completed":
      return "Abgeschlossen";
    case "cancelled":
      return "Abgebrochen";
    default:
      return "Geplant";
  }
}

export function getGeneratedTaskStatusLabel(task: RotationGeneratedTask): string {
  switch (task.status) {
    case "in_progress":
      return "In Bearbeitung";
    case "completed":
      return "Erledigt";
    case "failed":
      return "Fehlgeschlagen";
    case "cancelled":
      return "Storniert";
    default:
      return "Offen";
  }
}
