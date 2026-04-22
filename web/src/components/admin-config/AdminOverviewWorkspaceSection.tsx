import type { AdminNotificationEmailConfiguration } from "../../types/auth";
import { notificationConfigurationStatusLabel, notificationModeLabel } from "./adminConfigHelpers";
import type {
  AdminOrganizationEntity,
  AdminWorkspaceSection,
  AdminWorkspaceWarning,
} from "./adminWorkspaceModel";
import { groupAdminOverviewWarnings } from "./adminWorkspaceModel";
import SectionHeader from "../ui/SectionHeader";

type AdminOverviewWorkspaceSectionProps = {
  departmentCount: number;
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
  warningCount,
  hasLoadedTechnicalAccess,
  roleCount,
  groupCount,
  notificationEmailConfiguration,
  warnings,
  onOpenOrganization,
  onOpenSection,
}: AdminOverviewWorkspaceSectionProps) {
  const warningGroups = groupAdminOverviewWarnings(warnings);
  const technicalAccessSummary = hasLoadedTechnicalAccess
    ? `${roleCount} Rollen | ${groupCount} Gruppen`
    : "Rollen und Gruppen bei Bedarf laden";
  const notificationSummary = `${notificationModeLabel(notificationEmailConfiguration)} | ${notificationConfigurationStatusLabel(notificationEmailConfiguration)} | ${warningCount} Warnungen`;
  const statusTitle = warningCount === 0 ? "System ist sauber konfiguriert" : `${warningCount} offene Warnungen`;
  const statusDescription = warningCount === 0
    ? "Aktuell gibt es keine Admin-Warnungen in Organisation, Zuständigkeiten oder Benachrichtigungen."
    : "Die wichtigsten Problemgruppen sind unten zusammengefasst, damit du gezielt in den richtigen Bereich springen kannst.";

  return (
    <div className="content-stack">
      <section className={`panel admin-health-panel ${warningCount === 0 ? "admin-health-panel--clear" : "admin-health-panel--warning"}`}>
        <SectionHeader
          title="Systemstatus"
          description="Kritische Konfigurationslücken und Admin-Aufgaben auf einen Blick."
          className="admin-health-head"
        />

        <div className="admin-health-summary">
          <div className="admin-health-status">
            <p className="admin-health-count">{statusTitle}</p>
            <p className="panel-text">{statusDescription}</p>
          </div>

          <div className="admin-health-meta">
            <div className="admin-health-metric">
              <span>Organisation</span>
              <strong>{departmentCount} Abteilungen</strong>
            </div>
            <div className="admin-health-metric">
              <span>Zugriff</span>
              <strong>{technicalAccessSummary}</strong>
            </div>
            <div className="admin-health-metric">
              <span>Benachrichtigungen</span>
              <strong>{notificationSummary}</strong>
            </div>
          </div>
        </div>

        {warningGroups.length > 0 ? (
          <div className="admin-warning-group-list" aria-label="Gruppierte Admin-Warnungen">
            {warningGroups.map((group) => (
              <article key={group.category} className="admin-warning-group-row">
                <div className="admin-warning-group-copy">
                  <div className="admin-warning-group-title-row">
                    <h3 className="admin-warning-group-title">{group.title}</h3>
                    <span className="admin-warning-group-count">{group.count}</span>
                  </div>
                  <p className="admin-warning-group-detail">{group.detail}</p>
                  <p className="admin-warning-group-preview">
                    Betroffen: {group.affectedLabels.join(", ")}
                    {group.count > group.affectedLabels.length ? " ..." : ""}
                  </p>
                </div>
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => {
                    if (group.targetSection) {
                      onOpenSection(group.targetSection);
                      return;
                    }

                    if (group.targetEntity) {
                      onOpenOrganization(group.targetEntity, group.targetId ?? null);
                    }
                  }}
                >
                  {group.actionLabel}
                </button>
              </article>
            ))}
          </div>
        ) : null}
      </section>
    </div>
  );
}
