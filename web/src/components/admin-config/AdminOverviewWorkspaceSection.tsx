import type {
  AdminDirectorySyncStatus,
  AdminNotificationEmailConfiguration,
  DirectoryPendingImports,
} from "../../types/auth";
import { formatTimestamp, notificationConfigurationStatusLabel, notificationModeLabel } from "./adminConfigHelpers";
import type {
  AdminOrganizationEntity,
  AdminWorkspaceSection,
  AdminWorkspaceWarning,
} from "./adminWorkspaceModel";
import {
  clusterAdminOverviewWarnings,
  groupAdminOverviewWarnings,
} from "./adminWorkspaceModel";
import SectionHeader from "../ui/SectionHeader";

type HealthTone = "neutral" | "success" | "warning" | "danger";

type AdminOverviewWorkspaceSectionProps = {
  warningCount: number;
  notificationEmailConfiguration: AdminNotificationEmailConfiguration | null;
  directoryStatus: AdminDirectorySyncStatus | null;
  directoryPendingImports: DirectoryPendingImports | null;
  isSyncingDirectory: boolean;
  warnings: AdminWorkspaceWarning[];
  onOpenOrganization: (entity: AdminOrganizationEntity, id?: number | null) => void;
  onOpenSection: (section: AdminWorkspaceSection) => void;
};

function getDirectorySyncTone(status: AdminDirectorySyncStatus | null): HealthTone {
  if (!status) return "neutral";
  if (status.lastError) return "danger";
  switch ((status.lastSyncStatus ?? "").trim().toLowerCase()) {
    case "success":
      return "success";
    case "partial":
      return "warning";
    case "failed":
      return "danger";
    default:
      return "neutral";
  }
}

function getDirectorySyncStatusLabel(status: AdminDirectorySyncStatus | null): string {
  switch ((status?.lastSyncStatus ?? "").trim().toLowerCase()) {
    case "success":
      return "Erfolgreich";
    case "partial":
      return "Teilweise";
    case "failed":
      return "Fehlgeschlagen";
    default:
      return "Noch nicht ausgeführt";
  }
}

function getMailTone(config: AdminNotificationEmailConfiguration | null): HealthTone {
  if (!config) return "neutral";
  if (config.configurationStatus === "incomplete") return "danger";
  switch (config.mode) {
    case "enabled":
      return "success";
    case "sandbox":
      return "warning";
    default:
      return "neutral";
  }
}

function getWarningTone(warningCount: number): HealthTone {
  if (warningCount === 0) return "success";
  if (warningCount >= 10) return "danger";
  return "warning";
}

export function AdminOverviewWorkspaceSection({
  warningCount,
  notificationEmailConfiguration,
  directoryStatus,
  directoryPendingImports,
  isSyncingDirectory,
  warnings,
  onOpenOrganization,
  onOpenSection,
}: AdminOverviewWorkspaceSectionProps) {
  const warningGroups = groupAdminOverviewWarnings(warnings);
  const warningClusters = clusterAdminOverviewWarnings(warningGroups);
  const pendingImportCount = directoryPendingImports?.totalCount ?? 0;

  const directoryTone = getDirectorySyncTone(directoryStatus);
  const directoryLabel = isSyncingDirectory
    ? "Synchronisiert..."
    : getDirectorySyncStatusLabel(directoryStatus);
  const directoryTimestamp = directoryStatus?.lastSyncAt
    ? formatTimestamp(directoryStatus.lastSyncAt)
    : "Noch nicht ausgeführt";

  const mailTone = getMailTone(notificationEmailConfiguration);
  const mailMode = notificationModeLabel(notificationEmailConfiguration);
  const mailConfigStatus = notificationConfigurationStatusLabel(notificationEmailConfiguration);

  const warningTone = getWarningTone(warningCount);
  const statusTitle = warningCount === 0
    ? "System ist sauber konfiguriert"
    : `${warningCount} offene ${warningCount === 1 ? "Warnung" : "Warnungen"}`;
  const statusDescription = warningCount === 0
    ? "Aktuell gibt es keine Admin-Warnungen in Stammdaten, Zuständigkeiten oder Mail-Konfiguration."
    : "Die Problemgruppen sind unten nach Bereichen gebündelt.";

  return (
    <div className="content-stack">
      <section className={`panel admin-health-panel ${warningCount === 0 ? "admin-health-panel--clear" : "admin-health-panel--warning"}`}>
        <SectionHeader
          title="Systemstatus"
          description="Direkte Health-Signale für Verzeichnis-Sync, Mailversand und offene Admin-Warnungen."
          className="admin-health-head"
        />

        <div className="admin-health-summary">
          <div className="admin-health-status">
            <p className="admin-health-count">{statusTitle}</p>
            <p className="panel-text">{statusDescription}</p>
          </div>

          <div className="admin-health-meta">
            <button
              type="button"
              className={`admin-health-metric admin-health-metric--${directoryTone} admin-health-metric--clickable`}
              onClick={() => onOpenSection("directory")}
              aria-label="Verzeichnis & Gruppen öffnen"
            >
              <span>Verzeichnis-Sync</span>
              <strong>{directoryLabel}</strong>
              <p className="admin-health-metric-detail">Letzter Lauf: {directoryTimestamp}</p>
              {directoryStatus?.lastError ? (
                <p className="admin-health-metric-detail admin-health-metric-detail--danger">
                  {directoryStatus.lastError}
                </p>
              ) : null}
              {pendingImportCount > 0 ? (
                <p className="admin-health-metric-detail">
                  {pendingImportCount} {pendingImportCount === 1 ? "ausstehender Import" : "ausstehende Importe"}
                </p>
              ) : null}
            </button>

            <button
              type="button"
              className={`admin-health-metric admin-health-metric--${mailTone} admin-health-metric--clickable`}
              onClick={() => onOpenSection("system_configuration")}
              aria-label="Mail-Konfiguration öffnen"
            >
              <span>Mail-Versand</span>
              <strong>{mailMode}</strong>
              <p className="admin-health-metric-detail">{mailConfigStatus}</p>
            </button>

            <div className={`admin-health-metric admin-health-metric--${warningTone}`}>
              <span>Offene Warnungen</span>
              <strong>{warningCount}</strong>
              <p className="admin-health-metric-detail">
                {warningCount === 0
                  ? "Keine offenen Warnungen."
                  : `${warningClusters.length} ${warningClusters.length === 1 ? "Bereich" : "Bereiche"} betroffen.`}
              </p>
            </div>
          </div>
        </div>

        {warningClusters.length > 0 ? (
          <div className="admin-warning-cluster-list" aria-label="Warnungen nach Bereich">
            {warningClusters.map((bucket) => (
              <div key={bucket.cluster} className="admin-warning-cluster">
                <div className="admin-warning-cluster-head">
                  <h3 className="admin-warning-cluster-title">{bucket.title}</h3>
                  <span className="admin-warning-cluster-count">{bucket.totalCount}</span>
                </div>

                <div className="admin-warning-group-list">
                  {bucket.groups.map((group) => (
                    <article key={group.category} className="admin-warning-group-row">
                      <div className="admin-warning-group-copy">
                        <div className="admin-warning-group-title-row">
                          <h4 className="admin-warning-group-title">{group.title}</h4>
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
              </div>
            ))}
          </div>
        ) : null}
      </section>
    </div>
  );
}
