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
        <p>Onboarding für {processTypeName} wählen.</p>
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
        <div className="wizard-choice-list">
          {completedOnboardings.map((onboarding) => (
            (() => {
              const isSelected = selectedWorkflowUid === onboarding.workflowUid;

              return (
            <label
              key={onboarding.workflowUid}
              className={`wizard-choice-card${isSelected ? " wizard-choice-card--selected" : ""}`}
            >
              <span className="wizard-choice-card__head">
                <input
                  type="radio"
                  name="completedOnboarding"
                  checked={isSelected}
                  onChange={() => onSelectOnboarding(onboarding)}
                />
                <strong>{onboarding.displayName}</strong>
              </span>
              <span className="wizard-choice-card__meta">
                {onboarding.departmentName ?? "Keine Abteilung"} | {onboarding.roleName ?? "Keine Stelle"} |{" "}
                {formatDateTime(onboarding.completedAt)}
              </span>
            </label>
              );
            })()
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
