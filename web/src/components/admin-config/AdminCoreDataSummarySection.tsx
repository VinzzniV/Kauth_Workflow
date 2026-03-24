type AdminCoreDataSummarySectionProps = {
  departmentCount: number;
  userCount: number;
  responsibilityCount: number;
  error: string | null;
  notice: string | null;
  isLoading: boolean;
  isLoadingTechnicalAccess: boolean;
  isTechnicalAccessOpen: boolean;
  onReload: () => void | Promise<void>;
  onToggleTechnicalAccess: () => void;
};

export function AdminCoreDataSummarySection({
  departmentCount,
  userCount,
  responsibilityCount,
  error,
  notice,
  isLoading,
  isLoadingTechnicalAccess,
  isTechnicalAccessOpen,
  onReload,
  onToggleTechnicalAccess,
}: AdminCoreDataSummarySectionProps) {
  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Kern-Stammdaten</h2>
        <p>
          Abteilungen: {departmentCount} | Personen: {userCount} | Fachliche Zuständigkeiten: {responsibilityCount}
        </p>
      </div>

      <div className="action-row">
        <button
          type="button"
          className="btn btn-secondary"
          onClick={() => {
            void onReload();
          }}
          disabled={isLoading || isLoadingTechnicalAccess}
        >
          Stammdaten aktualisieren
        </button>
        <button type="button" className="btn btn-secondary" onClick={onToggleTechnicalAccess}>
          {isTechnicalAccessOpen ? "Rechte ausblenden" : "Rechte einblenden"}
        </button>
      </div>

      {error ? <p className="panel-note">{error}</p> : null}
      {notice ? <p className="panel-note">{notice}</p> : null}
    </section>
  );
}
