import type { WorkflowTargetPerson } from "../../types/workflow";
import { formatDateTime } from "../../utils/dateFormat";
import {
  formatEmploymentStatus,
  formatDirectoryLinkStatus,
} from "../../utils/employmentStatus";

type Props = {
  processTypeName: string;
  searchValue: string;
  onSearchChange: (value: string) => void;
  targetPeople: WorkflowTargetPerson[];
  selectedPersonId: number | null;
  selectedPerson: WorkflowTargetPerson | null;
  isLoading: boolean;
  error: string | null;
  selectionError?: string | null;
  onSelectPerson: (person: WorkflowTargetPerson) => void;
};

export default function TargetPersonSelection({
  processTypeName,
  searchValue,
  onSearchChange,
  targetPeople,
  selectedPersonId,
  selectedPerson,
  isLoading,
  error,
  selectionError,
  onSelectPerson,
}: Props) {
  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Bestehende Person auswählen</h2>
        <p>Bestehende Person für {processTypeName} auswählen.</p>
      </div>

      <label className="field">
        <span>Mitarbeiter suchen</span>
        <input
          type="search"
          value={searchValue}
          onChange={(event) => onSearchChange(event.target.value)}
          placeholder="Name, Personalnummer, Abteilung oder Rolle"
        />
      </label>

      {error ? <p className="text-error">{error}</p> : null}
      {selectionError ? <p className="field-error">{selectionError}</p> : null}
      {isLoading ? <p className="text-muted">Personen werden geladen...</p> : null}
      {!isLoading && targetPeople.length === 0 ? (
        <p className="text-muted">Keine passende Person gefunden.</p>
      ) : null}

      {!isLoading && targetPeople.length > 0 ? (
        <div className="wizard-choice-list">
          {targetPeople.map((person) => {
            const isSelected = selectedPersonId === person.personId;
            const lastOnboardingLabel = person.latestSourceWorkflowCompletedAt
              ? `Letzter Quell-Workflow: ${formatDateTime(person.latestSourceWorkflowCompletedAt)}`
              : "Kein Quell-Workflow vorhanden";

            return (
              <label
                key={person.personId}
                className={`wizard-choice-card${isSelected ? " wizard-choice-card--selected" : ""}`}
              >
                <span className="wizard-choice-card__head">
                  <input
                    type="radio"
                    name="targetPerson"
                    checked={isSelected}
                    onChange={() => onSelectPerson(person)}
                  />
                  <strong>{person.displayName}</strong>
                </span>
                <span className="wizard-choice-card__meta">
                  {person.departmentName ?? "Keine Abteilung"} | {person.roleName ?? "Keine Stelle"} |{" "}
                  {formatEmploymentStatus(person.employmentStatus)}
                </span>
                <span className="wizard-choice-card__meta">{lastOnboardingLabel}</span>
              </label>
            );
          })}
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
              <dt>Stamm-Abteilung</dt>
              <dd>{selectedPerson.departmentName ?? "Nicht ableitbar"}</dd>
            </div>
            <div>
              <dt>Aktuelle Stelle</dt>
              <dd>{selectedPerson.roleName ?? "Nicht ableitbar"}</dd>
            </div>
            <div>
              <dt>Personalnummer</dt>
              <dd>{selectedPerson.employeeNumber ?? "-"}</dd>
            </div>
            <div>
              <dt>Kartennummer</dt>
              <dd>{selectedPerson.badgeNumber ?? "-"}</dd>
            </div>
            <div>
              <dt>Beschäftigungsstatus</dt>
              <dd>{formatEmploymentStatus(selectedPerson.employmentStatus)}</dd>
            </div>
            <div>
              <dt>Directory-Link</dt>
              <dd>{formatDirectoryLinkStatus(selectedPerson.directoryLinkStatus)}</dd>
            </div>
            <div>
              <dt>Letzter Quell-Workflow</dt>
              <dd>{selectedPerson.latestSourceWorkflowUid ?? "-"}</dd>
            </div>
          </dl>
        </div>
      ) : null}
    </section>
  );
}
