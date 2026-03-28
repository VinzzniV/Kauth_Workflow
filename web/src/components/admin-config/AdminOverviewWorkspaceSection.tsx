import type { AdminNotificationEmailConfiguration } from "../../types/auth";
import { notificationConfigurationStatusLabel, notificationModeLabel } from "./adminConfigHelpers";
import type {
  AdminOrganizationEntity,
  AdminWorkspaceSection,
  AdminWorkspaceWarning,
} from "./adminWorkspaceModel";
import Card from "../ui/Card";
import SectionHeader from "../ui/SectionHeader";

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
  const notificationSummary = `${notificationModeLabel(notificationEmailConfiguration)} | ${notificationConfigurationStatusLabel(notificationEmailConfiguration)} | ${warningCount} Warnungen`;

  return (
    <div className="content-stack">
      <section className="panel">
        <SectionHeader title="Arbeitsbereiche" />

        <div className="admin-overview-grid" aria-label="Bereiche der Administration">
          <Card variant="primary" className="admin-overview-card">
            <SectionHeader
              title="Organisation"
              description={`${userCount} Personen | ${departmentCount} Abteilungen | ${responsibilityCount} Zuständigkeiten`}
              className="admin-overview-card-head"
              headingTag="h3"
            />
            <div className="action-row admin-overview-actions">
              <button type="button" className="btn btn-primary" onClick={() => onOpenOrganization("user", null)}>
                Personen
              </button>
              <button type="button" className="btn btn-ghost" onClick={() => onOpenOrganization("department", null)}>
                Abteilungen
              </button>
              <button type="button" className="btn btn-ghost" onClick={() => onOpenOrganization("responsibility", null)}>
                Zuständigkeiten
              </button>
            </div>
          </Card>

          <Card variant="primary" className="admin-overview-card">
            <SectionHeader
              title="Vorlagen & Felder"
              description="Vorlagen | Felder | Standardwerte"
              className="admin-overview-card-head"
              headingTag="h3"
            />
            <div className="action-row admin-overview-actions">
              <button type="button" className="btn btn-primary" onClick={() => onOpenSection("templates")}>
                Aufgabenvorlagen
              </button>
              <button type="button" className="btn btn-ghost" onClick={() => onOpenSection("answers")}>
                Antwortfelder
              </button>
              <button type="button" className="btn btn-ghost" onClick={() => onOpenSection("defaults")}>
                Standardwerte
              </button>
            </div>
          </Card>

          <Card variant="primary" className="admin-overview-card">
            <SectionHeader
              title="Rechte & Zugriff"
              description={technicalAccessSummary}
              className="admin-overview-card-head"
              headingTag="h3"
            />
            <div className="action-row admin-overview-actions">
              <button type="button" className="btn btn-primary" onClick={() => onOpenSection("access")}>
                Zugriffe & Gruppen
              </button>
              <button type="button" className="btn btn-ghost" onClick={() => onOpenSection("directory")}>
                Entra-Verzeichnis
              </button>
            </div>
          </Card>

          <Card variant="primary" className="admin-overview-card">
            <SectionHeader
              title="System"
              description={notificationSummary}
              className="admin-overview-card-head"
              headingTag="h3"
            />
            <div className="action-row admin-overview-actions">
              <button type="button" className="btn btn-primary" onClick={() => onOpenSection("system")}>
                Benachrichtigungen
              </button>
              <button type="button" className="btn btn-ghost" onClick={() => onOpenSection("operations")}>
                Massenänderungen
              </button>
            </div>
          </Card>
        </div>
      </section>

      <section className="section-stack">
        <SectionHeader title="Warnungen" />

        {warnings.length === 0 ? (
          <p className="panel-note">Keine offenen Warnungen.</p>
        ) : (
          <div className="dashboard-grid" aria-label="Warnhinweise der Administration">
            {warnings.map((warning) => (
              <Card key={warning.key} variant="list" className="dashboard-card">
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
              </Card>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
