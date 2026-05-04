import type { RotationStation } from "../../types/rotation";
import { formatDate } from "../../utils/dateFormat";
import EmptyState from "../feedback/EmptyState";
import { getStationStatusLabel, getStationStatusPillClass } from "./rotationLabels";

type Props = {
  stations: RotationStation[];
  editingStationId: number | null;
  deletingStationId: number | null;
  onEdit: (station: RotationStation) => void;
  onDelete: (station: RotationStation) => void;
  onOpenCreate: () => void;
};

export default function RotationStationTimeline({
  stations,
  editingStationId,
  deletingStationId,
  onEdit,
  onDelete,
  onOpenCreate,
}: Props) {
  if (stations.length === 0) {
    return (
      <EmptyState
        title="Noch keine Stationen vorhanden"
        description="Legen Sie die erste Abteilungsphase über das Formular an."
        actionLabel="Stationsformular öffnen"
        onAction={onOpenCreate}
      />
    );
  }

  return (
    <ol className="rotation-station-timeline" aria-label="Stationen des Durchlaufplans">
      {stations.map((station) => {
        const isEditing = editingStationId === station.id;
        const isDeleting = deletingStationId === station.id;
        const statusClass = getStationStatusPillClass(station.status);

        return (
          <li
            key={station.id}
            className={`rotation-station-timeline__item rotation-station-timeline__item--${station.status}`}
          >
            <div className="rotation-station-timeline__dot" aria-hidden="true" />
            <div className={`rotation-station-timeline__content${isEditing ? " rotation-station-timeline__item--active" : ""}`}>
              <div className="rotation-station-timeline__head">
                <h3>
                  {station.orderIndex + 1}. {station.departmentName}
                </h3>
                <span className={`status-pill ${statusClass}`}>
                  {getStationStatusLabel(station.status)}
                </span>
              </div>

              <p className="rotation-station-timeline__dates">
                {formatDate(station.startDate)} – {formatDate(station.endDate)}
              </p>

              {(station.location || station.notes) ? (
                <dl className="rotation-station-timeline__meta">
                  {station.location ? (
                    <div>
                      <dt>Ort</dt>
                      <dd>{station.location}</dd>
                    </div>
                  ) : null}
                  {station.notes ? (
                    <div>
                      <dt>Notizen</dt>
                      <dd>{station.notes}</dd>
                    </div>
                  ) : null}
                </dl>
              ) : null}

              <div className="action-row">
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => onEdit(station)}
                  disabled={isDeleting}
                >
                  Bearbeiten
                </button>
                <button
                  type="button"
                  className="btn btn-ghost"
                  onClick={() => void onDelete(station)}
                  disabled={isDeleting}
                >
                  {isDeleting ? "Lösche..." : "Löschen"}
                </button>
              </div>
            </div>
          </li>
        );
      })}
    </ol>
  );
}
