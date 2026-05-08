import type { Department } from "../../types/workflow";
import type { RotationStationStatus } from "../../types/rotation";
import type { StationFormState } from "../../hooks/useRotationStationForm";

export type RotationStationFormCardProps = {
  stationForm: StationFormState;
  setStationForm: React.Dispatch<React.SetStateAction<StationFormState>>;
  departments: Department[];
  isDepartmentsLoading: boolean;
  isSavingStation: boolean;
  editingStationId: number | null;
  onSave: () => void;
  onReset: () => void;
};

export default function RotationStationFormCard({
  stationForm,
  setStationForm,
  departments,
  isDepartmentsLoading,
  isSavingStation,
  editingStationId,
  onSave,
  onReset,
}: RotationStationFormCardProps) {
  return (
    <section className="panel panel-muted">
      <div className="panel-head">
        <h2>Stationsformular</h2>
        <p>Listen-/Timeline-nahe Erfassung: Reihenfolge, Zeitraum und Status stehen im Vordergrund.</p>
      </div>

      <div className="workflow-grid" aria-label="Stationsformular">
        <div className="card-primary card-form">
          <label className="field compact">
            <span>Abteilung</span>
            <select
              value={stationForm.departmentId}
              onChange={(event) =>
                setStationForm((current) => ({ ...current, departmentId: event.target.value }))
              }
              disabled={isDepartmentsLoading}
            >
              <option value="">Abteilung wählen...</option>
              {departments.map((dept) => (
                <option key={dept.id} value={String(dept.id)}>
                  {dept.name}
                </option>
              ))}
            </select>
          </label>
          <label className="field compact">
            <span>Startdatum</span>
            <input
              type="date"
              value={stationForm.startDate}
              onChange={(event) =>
                setStationForm((current) => ({ ...current, startDate: event.target.value }))
              }
            />
          </label>
          <label className="field compact">
            <span>Enddatum</span>
            <input
              type="date"
              value={stationForm.endDate}
              onChange={(event) =>
                setStationForm((current) => ({ ...current, endDate: event.target.value }))
              }
            />
          </label>
          <label className="field compact">
            <span>Status</span>
            <select
              value={stationForm.status}
              onChange={(event) =>
                setStationForm((current) => ({
                  ...current,
                  status: event.target.value as RotationStationStatus,
                }))
              }
            >
              <option value="planned">Geplant</option>
              <option value="active">Aktiv</option>
              <option value="completed">Abgeschlossen</option>
              <option value="cancelled">Abgebrochen</option>
            </select>
          </label>
          <label className="field compact">
            <span>Ort</span>
            <input
              type="text"
              value={stationForm.location}
              onChange={(event) =>
                setStationForm((current) => ({ ...current, location: event.target.value }))
              }
              placeholder="optional"
            />
          </label>
          <label className="field compact">
            <span>Notizen</span>
            <textarea
              value={stationForm.notes}
              onChange={(event) =>
                setStationForm((current) => ({ ...current, notes: event.target.value }))
              }
              rows={4}
              placeholder="Hinweise für HR oder den Fachbereich"
            />
          </label>

          <div className="action-row">
            <button
              type="button"
              className="btn btn-primary"
              onClick={onSave}
              disabled={isSavingStation}
            >
              {isSavingStation
                ? "Speichere..."
                : editingStationId
                  ? "Station aktualisieren"
                  : "Station anlegen"}
            </button>
            <button
              type="button"
              className="btn btn-secondary"
              onClick={onReset}
              disabled={isSavingStation}
            >
              Zurücksetzen
            </button>
          </div>
        </div>
      </div>
    </section>
  );
}
