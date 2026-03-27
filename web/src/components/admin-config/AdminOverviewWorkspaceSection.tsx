import type { AdminNotificationEmailConfiguration } from "../../types/auth";
import { notificationConfigurationStatusLabel, notificationModeLabel } from "./adminConfigHelpers";
import type {
  AdminOrganizationEntity,
  AdminWorkspaceSection,
  AdminWorkspaceWarning,
} from "./adminWorkspaceModel";

type AdminOverviewWorkspaceSectionProps = {
  departmentCount: number;
  userCount: number;
  responsibilityCount: number;
  warningCount: number;
  hasLoadedTechnicalAccess: boolean;
  roleCount: number;
  groupCount: number;
  notificationEmailConfiguration: AdminNotificationEmailConfiguration | null;
  warnings: AdminWorkspaceWarning[];
  onOpenOrganization: (entity: AdminOrganizationEntity, id?: number | null) => void;
  onOpenSection: (section: AdminWorkspaceSection) => void;
};

export function AdminOverviewWorkspaceSection({
  departmentCount,
  userCount,
  responsibilityCount,
  warningCount,
  hasLoadedTechnicalAccess,
  roleCount,
  groupCount,
  notificationEmailConfiguration,
  warnings,
  onOpenOrganization,
  onOpenSection,
}: AdminOverviewWorkspaceSectionProps) {
  return (
    <div className="content-stack">
      <section className="panel">
        <div className="panel-head">
          <h2>Admin-Übersicht</h2>
          <p>Hier sehen Sie zuerst den Gesamtzustand und springen dann gezielt in Organisation, Rechte oder System.</p>
        </div>

        <div className="dashboard-grid" aria-label="Admin-Übersicht Kennzahlen">
          <article className="dashboard-card">
            <h2>Personen</h2>
            <p>{userCount} gepflegte Personen</p>
          </article>

          <article className="dashboard-card">
            <h2>Abteilungen</h2>
            <p>{departmentCount} gepflegte Abteilungen</p>
          </article>

          <article className="dashboard-card">
            <h2>Fachliche Zuständigkeiten</h2>
            <p>{responsibilityCount} gepflegte Zuständigkeiten</p>
          </article>

          <article className="dashboard-card">
            <h2>Rechte & Gruppen</h2>
            <p>
              {hasLoadedTechnicalAccess
                ? `${roleCount} Rollen | ${groupCount} Gruppen`
                : "Wird bei Bedarf geladen"}
            </p>
          </article>

          <article className="dashboard-card">
            <h2>Mail-Konfiguration</h2>
            <p>{notificationModeLabel(notificationEmailConfiguration)}</p>
            <p className="panel-note">{notificationConfigurationStatusLabel(notificationEmailConfiguration)}</p>
          </article>

          <article className="dashboard-card">
            <h2>Warnungen</h2>
            <p>{warningCount === 0 ? "Keine offenen Warnungen" : `${warningCount} Warnhinweise`}</p>
          </article>
        </div>
      </section>

      <section className="panel panel-muted">
        <div className="panel-head">
          <h2>Schnellzugriffe</h2>
          <p>Springen Sie direkt in den passenden Arbeitsbereich.</p>
        </div>

        <div className="action-row admin-action-grid">
          <button type="button" className="btn btn-primary" onClick={() => onOpenOrganization("user", null)}>
            Neue Person
          </button>
          <button type="button" className="btn btn-secondary" onClick={() => onOpenOrganization("department", null)}>
            Neue Abteilung
          </button>
          <button type="button" className="btn btn-secondary" onClick={() => onOpenOrganization("user", null)}>
            Organisation öffnen
          </button>
          <button type="button" className="btn btn-secondary" onClick={() => onOpenSection("access")}>
            Rechte & Gruppen öffnen
          </button>
          <button type="button" className="btn btn-secondary" onClick={() => onOpenSection("directory")}>
            Verzeichnis öffnen
          </button>
          <button type="button" className="btn btn-secondary" onClick={() => onOpenSection("templates")}>
            Templates öffnen
          </button>
          <button type="button" className="btn btn-secondary" onClick={() => onOpenSection("answers")}>
            Answers öffnen
          </button>
          <button type="button" className="btn btn-secondary" onClick={() => onOpenSection("defaults")}>
            Defaults öffnen
          </button>
          <button type="button" className="btn btn-secondary" onClick={() => onOpenSection("system")}>
            Mail-Konfiguration öffnen
          </button>
        </div>
      </section>

      <section className="panel">
        <div className="panel-head">
          <h2>Warnhinweise</h2>
          <p>Leere oder ungültige Zuordnungen werden hier strukturell gesammelt.</p>
        </div>

        {warnings.length === 0 ? (
          <p className="panel-note">Aktuell sind keine strukturellen Warnhinweise vorhanden.</p>
        ) : (
          <div className="dashboard-grid" aria-label="Admin-Warnhinweise">
            {warnings.map((warning) => (
              <article key={warning.key} className="dashboard-card">
                <h2>{warning.title}</h2>
                <p>{warning.detail}</p>
                <div className="action-row">
                  <button
                    type="button"
                    className="btn btn-secondary"
                    onClick={() => {
                      if (warning.targetSection && warning.targetSection !== "organization") {
                        onOpenSection(warning.targetSection);
                        return;
                      }

                      if (warning.targetEntity) {
                        onOpenOrganization(warning.targetEntity, warning.targetId ?? null);
                      }
                    }}
                  >
                    {warning.actionLabel}
                  </button>
                </div>
              </article>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
