import type { AdminGraphApplicationConfiguration } from "../../types/auth";

type AdminGraphApplicationSectionProps = {
  graphApplicationConfiguration: AdminGraphApplicationConfiguration | null;
};

export function AdminGraphApplicationSection({
  graphApplicationConfiguration,
}: AdminGraphApplicationSectionProps) {
  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Konfiguration: Graph-Anwendung</h2>
        <p>Read-only Status der Entra-Laufzeitkonfiguration für Directory-Sync und Graph-basierten Mailversand.</p>
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
            Das Secret wird ausschließlich aus der Runtime gelesen und nie über die Admin-Oberfläche ausgegeben.
          </p>
        </article>

        <article className="dashboard-stat-card card-stat">
          <div>
            <h2>Konfigurationsquelle</h2>
            <p>{graphApplicationConfiguration?.configurationSource === "runtime" ? "Runtime" : "Unbekannt"}</p>
          </div>
          <p className="panel-note">Erwartet werden `ENTRA_TENANT_ID`, `ENTRA_CLIENT_ID` und ein Runtime-Secret.</p>
        </article>
      </div>

      <div className="dashboard-card card-primary">
        <div>
          <h2>Runtime-Werte</h2>
          <p>
            Graph, Directory-Sync und Mailversand verwenden dieselbe Entra-App wie die API-Auth. Änderungen erfolgen
            über Environment oder Secret-Store und werden nach Neustart wirksam.
          </p>
        </div>

        <div className="field compact">
          <span>Tenant ID</span>
          <p className="panel-note">{graphApplicationConfiguration?.tenantId ?? "Nicht gesetzt"}</p>
        </div>

        <div className="field compact">
          <span>Client ID</span>
          <p className="panel-note">{graphApplicationConfiguration?.clientId ?? "Nicht gesetzt"}</p>
        </div>

        <div className="field compact">
          <span>Secret-Rotation</span>
          <p className="panel-note">
            Setzen Sie `ENTRA_CLIENT_SECRET` oder optional `GRAPH_CLIENT_SECRET` außerhalb der UI und starten Sie die
            API anschließend neu.
          </p>
        </div>

        <p className="panel-note">
          Diese Ansicht ist bewusst read-only. Persistierte Graph-Secrets oder lokale Admin-Edits werden nicht mehr
          unterstützt.
        </p>
      </div>
    </section>
  );
}
