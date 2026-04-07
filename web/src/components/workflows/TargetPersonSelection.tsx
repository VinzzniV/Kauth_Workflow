import type { WorkflowTargetPersonSource } from "../../types/workflow";
import { formatDateTime } from "../../utils/dateFormat";

type Props = {
  processTypeName: string;
  searchValue: string;
  onSearchChange: (value: string) => void;
  targetPersonSources: WorkflowTargetPersonSource[];
  selectedWorkflowUid: string | null;
  selectedSource: WorkflowTargetPersonSource | null;
  isLoading: boolean;
  error: string | null;
  selectionError?: string | null;
  onSelectSource: (source: WorkflowTargetPersonSource) => void;
};

export default function TargetPersonSelection({
  processTypeName,
  searchValue,
  onSearchChange,
  targetPersonSources,
  selectedWorkflowUid,
  selectedSource,
  isLoading,
  error,
  selectionError,
  onSelectSource,
}: Props) {
  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Quellworkflow auswählen</h2>
        <p>Bestehenden Workflow für {processTypeName} auswählen.</p>
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
      {isLoading ? <p className="text-muted">Quellworkflows werden geladen...</p> : null}
      {!isLoading && targetPersonSources.length === 0 ? (
        <p className="text-muted">Kein passender Quellworkflow gefunden.</p>
      ) : null}

      {!isLoading && targetPersonSources.length > 0 ? (
        <div className="wizard-choice-list">
          {targetPersonSources.map((source) => (
            (() => {
              const isSelected = selectedWorkflowUid === source.workflowUid;

              return (
            <label
              key={source.workflowUid}
              className={`wizard-choice-card${isSelected ? " wizard-choice-card--selected" : ""}`}
            >
              <span className="wizard-choice-card__head">
                <input
                  type="radio"
                  name="targetPersonSource"
                  checked={isSelected}
                  onChange={() => onSelectSource(source)}
                />
                <strong>{source.displayName}</strong>
              </span>
              <span className="wizard-choice-card__meta">
                {source.departmentName ?? "Keine Abteilung"} | {source.roleName ?? "Keine Stelle"} |{" "}
                {formatDateTime(source.completedAt)}
              </span>
            </label>
              );
            })()
          ))}
        </div>
      ) : null}

      {selectedSource ? (
        <div style={{ marginTop: "1rem" }}>
          <h3>Übernommener Kontext</h3>
          <dl className="workflow-kv-grid">
            <div>
              <dt>Quellworkflow</dt>
              <dd>{selectedSource.workflowUid}</dd>
            </div>
            <div>
              <dt>Person</dt>
              <dd>{selectedSource.displayName}</dd>
            </div>
            <div>
              <dt>Abteilung</dt>
              <dd>{selectedSource.departmentName ?? "Nicht ableitbar"}</dd>
            </div>
            <div>
              <dt>Stelle</dt>
              <dd>{selectedSource.roleName ?? "Nicht ableitbar"}</dd>
            </div>
            <div>
              <dt>Personalnummer</dt>
              <dd>{selectedSource.employeeNumber}</dd>
            </div>
            <div>
              <dt>Kartennummer</dt>
              <dd>{selectedSource.badgeNumber}</dd>
            </div>
            <div>
              <dt>Abgeschlossen</dt>
              <dd>{formatDateTime(selectedSource.completedAt)}</dd>
            </div>
          </dl>
        </div>
      ) : null}
    </section>
  );
}
