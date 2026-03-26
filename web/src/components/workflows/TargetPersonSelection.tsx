import type { WorkflowTargetPerson } from "../../types/workflow";

type Props = {
  processTypeName: string;
  searchValue: string;
  onSearchChange: (value: string) => void;
  people: WorkflowTargetPerson[];
  selectedPersonId: number | null;
  selectedPerson: WorkflowTargetPerson | null;
  isLoading: boolean;
  error: string | null;
  onSelectPerson: (person: WorkflowTargetPerson) => void;
};

export default function TargetPersonSelection({
  processTypeName,
  searchValue,
  onSearchChange,
  people,
  selectedPersonId,
  selectedPerson,
  isLoading,
  error,
  onSelectPerson,
}: Props) {
  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Bestehende Person auswählen</h2>
        <p>Wählen Sie die Zielperson für {processTypeName} und übernehmen Sie den aktuellen Kontext aus bestehenden Daten.</p>
      </div>

      <label className="field">
        <span>Person suchen</span>
        <input
          type="search"
          value={searchValue}
          onChange={(event) => onSearchChange(event.target.value)}
          placeholder="Name, Abteilung, Stelle oder Personalnummer"
        />
      </label>

      {error ? <p className="text-error">{error}</p> : null}
      {isLoading ? <p className="text-muted">Personen werden geladen...</p> : null}
      {!isLoading && people.length === 0 ? <p className="text-muted">Keine passende Person gefunden.</p> : null}

      {!isLoading && people.length > 0 ? (
        <div style={{ marginTop: "1rem", display: "grid", gap: "0.75rem" }}>
          {people.map((person) => (
            <label
              key={person.personId}
              className="panel panel-muted"
              style={{ cursor: "pointer", display: "grid", gap: "0.35rem" }}
            >
              <span style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
                <input
                  type="radio"
                  name="targetPerson"
                  checked={selectedPersonId === person.personId}
                  onChange={() => onSelectPerson(person)}
                />
                <strong>{person.displayName}</strong>
              </span>
              <span className="text-muted">
                {person.departmentName ?? "Keine Abteilung"} | {person.roleName ?? "Keine Stelle"} | Personalnummer:{" "}
                {person.employeeNumber ?? "unbekannt"}
              </span>
            </label>
          ))}
        </div>
      ) : null}

      {selectedPerson ? (
        <div style={{ marginTop: "1rem" }}>
          <h3>Übernommener Kontext</h3>
          <dl className="workflow-kv-grid">
            <div>
              <dt>Person</dt>
              <dd>{selectedPerson.displayName}</dd>
            </div>
            <div>
              <dt>Abteilung</dt>
              <dd>{selectedPerson.departmentName ?? "Nicht ableitbar"}</dd>
            </div>
            <div>
              <dt>Stelle</dt>
              <dd>{selectedPerson.roleName ?? "Nicht ableitbar"}</dd>
            </div>
            <div>
              <dt>Personalnummer</dt>
              <dd>{selectedPerson.employeeNumber ?? "Nicht ableitbar"}</dd>
            </div>
            <div>
              <dt>Kartennummer</dt>
              <dd>{selectedPerson.badgeNumber ?? "Nicht ableitbar"}</dd>
            </div>
          </dl>
        </div>
      ) : null}
    </section>
  );
}
