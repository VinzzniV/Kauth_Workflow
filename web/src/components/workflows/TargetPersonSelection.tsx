import type { CompletedOnboardingSearchResult } from "../../types/workflow";
import { formatDateTime } from "../../utils/dateFormat";

type Props = {
  processTypeName: string;
  searchValue: string;
  onSearchChange: (value: string) => void;
  completedOnboardings: CompletedOnboardingSearchResult[];
  selectedWorkflowUid: string | null;
  selectedOnboarding: CompletedOnboardingSearchResult | null;
  isLoading: boolean;
  error: string | null;
  selectionError?: string | null;
  onSelectOnboarding: (onboarding: CompletedOnboardingSearchResult) => void;
};

export default function TargetPersonSelection({
  processTypeName,
  searchValue,
  onSearchChange,
  completedOnboardings,
  selectedWorkflowUid,
  selectedOnboarding,
  isLoading,
  error,
  selectionError,
  onSelectOnboarding,
}: Props) {
  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Abgeschlossenes Onboarding auswählen</h2>
        <p>
          Wählen Sie das abgeschlossene Onboarding für {processTypeName}. Person, Kontext und Quell-Vorgang werden dabei
          gemeinsam übernommen.
        </p>
      </div>

      <label className="field">
        <span>Mitarbeiter suchen</span>
        <input
          type="search"
          value={searchValue}
          onChange={(event) => onSearchChange(event.target.value)}
          placeholder="Name, Personalnummer, Abteilung oder Stelle"
        />
      </label>

      {error ? <p className="text-error">{error}</p> : null}
      {selectionError ? <p className="field-error">{selectionError}</p> : null}
      {isLoading ? <p className="text-muted">Abgeschlossene Onboardings werden geladen...</p> : null}
      {!isLoading && completedOnboardings.length === 0 ? (
        <p className="text-muted">Kein passendes abgeschlossenes Onboarding gefunden.</p>
      ) : null}

      {!isLoading && completedOnboardings.length > 0 ? (
        <div style={{ marginTop: "1rem", display: "grid", gap: "0.75rem" }}>
          {completedOnboardings.map((onboarding) => (
            <label
              key={onboarding.workflowUid}
              className="panel panel-muted"
              style={{ cursor: "pointer", display: "grid", gap: "0.35rem" }}
            >
              <span style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
                <input
                  type="radio"
                  name="completedOnboarding"
                  checked={selectedWorkflowUid === onboarding.workflowUid}
                  onChange={() => onSelectOnboarding(onboarding)}
                />
                <strong>{onboarding.displayName}</strong>
              </span>
              <span className="text-muted">
                {onboarding.departmentName ?? "Keine Abteilung"} | {onboarding.roleName ?? "Keine Stelle"} |
                Personalnummer: {onboarding.employeeNumber}
              </span>
              <span className="text-muted">
                Onboarding abgeschlossen am {formatDateTime(onboarding.completedAt)}
              </span>
            </label>
          ))}
        </div>
      ) : null}

      {selectedOnboarding ? (
        <div style={{ marginTop: "1rem" }}>
          <h3>Übernommener Kontext</h3>
          <dl className="workflow-kv-grid">
            <div>
              <dt>Quell-Onboarding</dt>
              <dd>{selectedOnboarding.workflowUid}</dd>
            </div>
            <div>
              <dt>Person</dt>
              <dd>{selectedOnboarding.displayName}</dd>
            </div>
            <div>
              <dt>Abteilung</dt>
              <dd>{selectedOnboarding.departmentName ?? "Nicht ableitbar"}</dd>
            </div>
            <div>
              <dt>Stelle</dt>
              <dd>{selectedOnboarding.roleName ?? "Nicht ableitbar"}</dd>
            </div>
            <div>
              <dt>Personalnummer</dt>
              <dd>{selectedOnboarding.employeeNumber}</dd>
            </div>
            <div>
              <dt>Kartennummer</dt>
              <dd>{selectedOnboarding.badgeNumber}</dd>
            </div>
            <div>
              <dt>Abgeschlossen</dt>
              <dd>{formatDateTime(selectedOnboarding.completedAt)}</dd>
            </div>
          </dl>
        </div>
      ) : null}
    </section>
  );
}
