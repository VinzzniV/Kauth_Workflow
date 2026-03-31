import type { AdminGraphApplicationConfiguration } from "../../types/auth";
import { formatTimestamp } from "./adminConfigHelpers";

type AdminGraphApplicationSectionProps = {
  graphApplicationConfiguration: AdminGraphApplicationConfiguration | null;
  graphTenantIdDraft: string;
  graphClientIdDraft: string;
  graphClientSecretDraft: string;
  isSavingGraphApplicationConfiguration: boolean;
  hasGraphApplicationDraftChanges: boolean;
  onGraphTenantIdChange: (value: string) => void;
  onGraphClientIdChange: (value: string) => void;
  onGraphClientSecretChange: (value: string) => void;
  onSave: () => void | Promise<void>;
};

export function AdminGraphApplicationSection({
  graphApplicationConfiguration,
  graphTenantIdDraft,
  graphClientIdDraft,
  graphClientSecretDraft,
  isSavingGraphApplicationConfiguration,
  hasGraphApplicationDraftChanges,
  onGraphTenantIdChange,
  onGraphClientIdChange,
  onGraphClientSecretChange,
  onSave,
}: AdminGraphApplicationSectionProps) {
  const tenantMissing = graphTenantIdDraft.trim().length === 0;
  const clientIdMissing = graphClientIdDraft.trim().length === 0;
  const clientSecretMissing =
    !graphApplicationConfiguration?.hasClientSecret && graphClientSecretDraft.trim().length === 0;
  const canSave =
    !isSavingGraphApplicationConfiguration
    && hasGraphApplicationDraftChanges
    && !tenantMissing
    && !clientIdMissing
    && !clientSecretMissing;

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Konfiguration: Graph-Anwendung</h2>
        <p>Separater Zugriff für Entra-/Graph-Operationen wie Benutzer-, Gruppen- und Mailzugriff.</p>
      </div>

      <div className="dashboard-grid" aria-label="Graph-Anwendungsstatus">
        <article className="dashboard-stat-card card-stat">
          <div>
            <h2>Konfigurationsstatus</h2>
            <p>{graphApplicationConfiguration?.configurationStatus === "ready" ? "Vollständig" : "Unvollständig"}</p>
          </div>
          <p className="panel-note">
            {graphApplicationConfiguration?.configurationMessage ?? "Alle erforderlichen Felder sind hinterlegt."}
          </p>
        </article>

        <article className="dashboard-stat-card card-stat">
          <div>
            <h2>Secret-Status</h2>
            <p>{graphApplicationConfiguration?.hasClientSecret ? "Hinterlegt" : "Fehlt"}</p>
          </div>
          <p className="panel-note">
            Das Secret wird nicht im Klartext geladen. Hier kann nur ein neues Secret gesetzt oder ersetzt werden.
          </p>
        </article>

        <article className="dashboard-stat-card card-stat">
          <div>
            <h2>Letzte Änderung</h2>
            <p>{formatTimestamp(graphApplicationConfiguration?.updatedAt ?? null)}</p>
          </div>
        </article>
      </div>

      <div className="dashboard-card card-primary">
        <label className={`field compact ${tenantMissing ? "field-invalid" : ""}`}>
          <span>Tenant ID</span>
          <input
            type="text"
            value={graphTenantIdDraft}
            onChange={(event) => onGraphTenantIdChange(event.target.value)}
            placeholder="Microsoft Entra Tenant ID"
          />
        </label>

        <label className={`field compact ${clientIdMissing ? "field-invalid" : ""}`}>
          <span>Client ID</span>
          <input
            type="text"
            value={graphClientIdDraft}
            onChange={(event) => onGraphClientIdChange(event.target.value)}
            placeholder="App Registration Client ID"
          />
        </label>

        <label className={`field compact ${clientSecretMissing ? "field-invalid" : ""}`}>
          <span>Client Secret</span>
          <input
            type="password"
            value={graphClientSecretDraft}
            onChange={(event) => onGraphClientSecretChange(event.target.value)}
            placeholder={
              graphApplicationConfiguration?.hasClientSecret
                ? "Neues Client Secret zum Ersetzen eingeben"
                : "Graph Client Secret"
            }
          />
        </label>

        <p className="panel-note">
          Diese Zugangsdaten werden unabhängig von der Mail-Konfiguration gespeichert und von Graph-basierten Diensten wiederverwendet.
        </p>

        <div className="action-row">
          <button
            type="button"
            className="btn btn-primary"
            onClick={() => {
              void onSave();
            }}
            disabled={!canSave}
          >
            {isSavingGraphApplicationConfiguration ? "Speichern..." : "Graph-Konfiguration speichern"}
          </button>
        </div>
      </div>
    </section>
  );
}
