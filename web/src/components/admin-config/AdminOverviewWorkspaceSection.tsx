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
  const technicalAccessSummary = hasLoadedTechnicalAccess
    ? `${roleCount} Rollen | ${groupCount} Gruppen`
    : "Rollen und Gruppen bei Bedarf laden";
  const notificationSummary = `${notificationModeLabel(notificationEmailConfiguration)} | ${notificationConfigurationStatusLabel(notificationEmailConfiguration)}`;

  return (
    <div className="content-stack">
      <section className="panel">
        <div className="panel-head">
          <h2>Bereiche</h2>
        </div>

        <div className="admin-overview-grid" aria-label="Bereiche der Administration">
          <article className="admin-overview-card">
            <div className="admin-overview-card-head">
              <h3>Organisation</h3>
              <p>{userCount} Personen | {departmentCount} Abteilungen | {responsibilityCount} Zuständigkeiten</p>
            </div>
            <div className="action-row admin-overview-actions">
              <button type="button" className="btn btn-primary" onClick={() => onOpenOrganization("user", null)}>
                Personen
              </button>
              <button type="button" className="btn btn-secondary" onClick={() => onOpenOrganization("department", null)}>
                Abteilungen
              </button>
              <button type="button" className="btn btn-secondary" onClick={() => onOpenOrganization("responsibility", null)}>
                Zuständigkeiten
              </button>
            </div>
          </article>

          <article className="admin-overview-card">
            <div className="admin-overview-card-head">
              <h3>Vorlagen & Felder</h3>
              <p>Aufgabenvorlagen, Antwortfelder und Standardwerte</p>
            </div>
            <div className="action-row admin-overview-actions">
              <button type="button" className="btn btn-secondary" onClick={() => onOpenSection("templates")}>
                Aufgabenvorlagen
              </button>
              <button type="button" className="btn btn-secondary" onClick={() => onOpenSection("answers")}>
                Antwortfelder
              </button>
              <button type="button" className="btn btn-secondary" onClick={() => onOpenSection("defaults")}>
                Standardwerte
              </button>
            </div>
          </article>

          <article className="admin-overview-card">
            <div className="admin-overview-card-head">
              <h3>Rechte & Verzeichnis</h3>
              <p>{technicalAccessSummary}</p>
            </div>
            <div className="action-row admin-overview-actions">
              <button type="button" className="btn btn-secondary" onClick={() => onOpenSection("access")}>
                Zugriffe & Gruppen
              </button>
              <button type="button" className="btn btn-secondary" onClick={() => onOpenSection("directory")}>
                Entra-Verzeichnis
              </button>
            </div>
          </article>

          <article className="admin-overview-card">
            <div className="admin-overview-card-head">
              <h3>Benachrichtigungen & System</h3>
              <p>{notificationSummary}</p>
            </div>
            <div className="action-row admin-overview-actions">
              <button type="button" className="btn btn-secondary" onClick={() => onOpenSection("system")}>
                System öffnen
              </button>
            </div>
          </article>

          <article className="admin-overview-card admin-overview-card--caution">
            <div className="admin-overview-card-head">
              <h3>Massenänderungen</h3>
              <p>{warningCount === 0 ? "Keine offenen Warnungen" : `${warningCount} offene Warnungen`}</p>
            </div>
            <div className="action-row admin-overview-actions">
              <button type="button" className="btn btn-secondary" onClick={() => onOpenSection("operations")}>
                Massenänderungen
              </button>
            </div>
          </article>
        </div>
      </section>

      <section className="panel">
        <div className="panel-head">
          <h2>Warnungen</h2>
        </div>

        {warnings.length === 0 ? (
          <p className="panel-note">Keine offenen Warnungen.</p>
        ) : (
          <div className="dashboard-grid" aria-label="Warnhinweise der Administration">
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
