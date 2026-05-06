import type {
  AdminDirectoryIdentity,
  AdminDirectoryMappingAuditEntry,
  AdminDirectorySyncStatus,
  DirectoryResponsibilityGaps,
} from "../../types/auth";
import { useRef, useState } from "react";
import EmptyState from "../feedback/EmptyState";
import LoadingState from "../feedback/LoadingState";
import { formatTimestamp } from "./adminConfigHelpers";

type AdminDirectorySyncSectionProps = {
  status: AdminDirectorySyncStatus | null;
  identities: AdminDirectoryIdentity[];
  auditEntries: AdminDirectoryMappingAuditEntry[];
  hasMoreAudit: boolean;
  isLoadingMoreAudit: boolean;
  onLoadMoreAudit: () => void | Promise<void>;
  responsibilityGaps: DirectoryResponsibilityGaps | null;
  isLoading: boolean;
  isSyncing: boolean;
  onSync: (groupPrefix: string | null) => void | Promise<void>;
};

function statusBadgeClass(status: string | null): string {
  switch ((status ?? "").trim().toLowerCase()) {
    case "success":
      return "badge badge--success";
    case "partial":
      return "badge badge--default";
    case "failed":
      return "badge badge--error";
    default:
      return "badge badge--default";
  }
}

function statusLabel(status: string | null): string {
  switch ((status ?? "").trim().toLowerCase()) {
    case "success":
      return "Erfolgreich";
    case "partial":
      return "Teilweise erfolgreich";
    case "failed":
      return "Fehlgeschlagen";
    default:
      return "Noch nicht ausgeführt";
  }
}

export function AdminDirectorySyncSection({
  status,
  identities,
  auditEntries,
  hasMoreAudit,
  isLoadingMoreAudit,
  onLoadMoreAudit,
  responsibilityGaps,
  isLoading,
  isSyncing,
  onSync,
}: AdminDirectorySyncSectionProps) {
  const configuredGroupPrefix = status?.configuredGroupPrefix ?? "Onboarding";
  const groupPrefixInputRef = useRef<HTMLInputElement | null>(null);
  const [gapsExpanded, setGapsExpanded] = useState(false);

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Verzeichnis abgleichen</h2>
      </div>

      <div className="dashboard-grid" aria-label="Verzeichnisstatus">
        <article className="dashboard-card card-list">
          <h2>Letzter Lauf</h2>
          <p>{formatTimestamp(status?.lastSyncAt ?? null)}</p>
          <span className={statusBadgeClass(status?.lastSyncStatus ?? null)}>
            {statusLabel(status?.lastSyncStatus ?? null)}
          </span>
        </article>

        <article className="dashboard-card card-list">
          <h2>Gruppen</h2>
          <p>{status?.totalGroups ?? 0} synchronisiert</p>
        </article>

        <article className="dashboard-card card-list">
          <h2>Identitäten</h2>
          <p>{status?.totalIdentities ?? 0} übernommen</p>
        </article>

        <article className="dashboard-card card-list">
          <h2>Gruppen-Rollen-Zuordnungen</h2>
          <p>{status?.totalMappings ?? 0} aktiv</p>
        </article>
      </div>

      {status?.lastError ? (
        <p className="panel-note">Letzter Fehler: {status.lastError}</p>
      ) : null}

      {!isLoading && responsibilityGaps && responsibilityGaps.totalUnassignedDepartments > 0 ? (
        <div className="panel-note" style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
          <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
            <span>
              {responsibilityGaps.totalCandidatesNotYetAssigned}{" "}
              {responsibilityGaps.totalCandidatesNotYetAssigned === 1 ? "Person" : "Personen"} in Entra-Gruppen
              ohne Zuständigkeitszuweisung in{" "}
              {responsibilityGaps.totalUnassignedDepartments}{" "}
              {responsibilityGaps.totalUnassignedDepartments === 1 ? "Abteilung" : "Abteilungen"}.
            </span>
            <button
              type="button"
              className="btn btn-link"
              onClick={() => setGapsExpanded((prev) => !prev)}
            >
              {gapsExpanded ? "Ausblenden" : "Details"}
            </button>
          </div>

          {gapsExpanded ? (
            <table className="table" style={{ marginTop: "0.5rem" }}>
              <thead>
                <tr>
                  <th>Abteilung</th>
                  <th>Entra-Gruppe</th>
                  <th>Kandidaten</th>
                  <th>Aktuelle Zuweisung</th>
                </tr>
              </thead>
              <tbody>
                {responsibilityGaps.gaps
                  .filter((g) => g.assignedLeadPersonId === null)
                  .map((gap) => (
                    <tr key={gap.departmentId}>
                      <td>{gap.departmentName}</td>
                      <td>{gap.entraGroupName ?? "–"}</td>
                      <td>
                        {gap.candidates.map((c) => (
                          <div key={c.appUserId}>
                            {c.displayName}
                            {c.mail ? <span className="panel-note"> ({c.mail})</span> : null}
                          </div>
                        ))}
                      </td>
                      <td>Nicht zugewiesen</td>
                    </tr>
                  ))}
              </tbody>
            </table>
          ) : null}
        </div>
      ) : null}

      <div className="toolbar-row">
        <label className="field compact grow">
          <span>Gruppenfilter</span>
          <input
            key={configuredGroupPrefix}
            type="text"
            ref={groupPrefixInputRef}
            defaultValue={configuredGroupPrefix}
            placeholder="z. B. Onboarding"
            disabled={isLoading || isSyncing}
          />
        </label>
      </div>

      <p className="panel-note">
        Standard ist der konfigurierte Filter
        {status?.configuredGroupPrefix ? ` "${status.configuredGroupPrefix}"` : " ohne Einschränkung"}.
        Für diesen Lauf können Sie hier einen anderen Suchtext eingeben. Es werden alle Gruppen berücksichtigt,
        deren Name diesen Text enthält. Leeres Feld bedeutet: alle Gruppen synchronisieren.
      </p>

      <div className="action-row">
        <button
          type="button"
          className="btn btn-primary"
          onClick={() => {
            const groupPrefixDraft = groupPrefixInputRef.current?.value ?? configuredGroupPrefix;
            void onSync(groupPrefixDraft.trim().length > 0 ? groupPrefixDraft.trim() : "");
          }}
          disabled={isLoading || isSyncing}
        >
          {isSyncing ? "Synchronisiert..." : "Jetzt synchronisieren"}
        </button>
      </div>

      {isLoading ? <LoadingState title="Verzeichnisdaten werden geladen..." /> : null}

      {!isLoading && identities.length === 0 ? (
        <EmptyState
          title="Noch keine Verzeichnisdaten"
          description="Nach der ersten Synchronisierung erscheinen hier die projizierten Identitäten."
        />
      ) : null}

      {!isLoading && identities.length > 0 ? (() => {
        const sharedSyncTimestamp = identities.every(
          (item) => item.lastSyncedAt === identities[0]?.lastSyncedAt
        )
          ? identities[0]?.lastSyncedAt ?? null
          : null;

        return (
          <>
            <div className="panel-head" style={{ marginTop: "1rem" }}>
              <h2>Zuletzt übernommene Identitäten</h2>
              {sharedSyncTimestamp ? (
                <p className="panel-note">Stand: {formatTimestamp(sharedSyncTimestamp)}</p>
              ) : null}
            </div>

            <table className="table">
              <thead>
                <tr>
                  <th>Name</th>
                  <th>UPN / Mail</th>
                  <th>Konto</th>
                  {sharedSyncTimestamp ? null : <th>Zuletzt synchronisiert</th>}
                </tr>
              </thead>
              <tbody>
                {identities.map((identity) => {
                  const linkedNameDiffers =
                    identity.appUserDisplayName !== null
                    && identity.appUserDisplayName !== identity.displayName;
                  const mailDiffers =
                    !!identity.mail && identity.mail !== identity.userPrincipalName;

                  return (
                    <tr key={identity.directoryIdentityId}>
                      <td>
                        <div>{identity.displayName}</div>
                        {identity.appUserDisplayName === null ? (
                          <div className="panel-note">Noch nicht verknüpft</div>
                        ) : linkedNameDiffers ? (
                          <div className="panel-note">→ {identity.appUserDisplayName}</div>
                        ) : null}
                      </td>
                      <td>
                        <div>{identity.userPrincipalName}</div>
                        {mailDiffers ? <div className="panel-note">{identity.mail}</div> : null}
                      </td>
                      <td>
                        <span className={`badge badge--${identity.accountEnabled ? "success" : "error"}`}>
                          {identity.accountEnabled ? "Aktiv" : "Deaktiviert"}
                        </span>
                      </td>
                      {sharedSyncTimestamp ? null : (
                        <td>{formatTimestamp(identity.lastSyncedAt)}</td>
                      )}
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </>
        );
      })() : null}

      {!isLoading && auditEntries.length > 0 ? (
        <>
          <div className="panel-head" style={{ marginTop: "1rem" }}>
            <h2>Änderungsprotokoll</h2>
          </div>

          <table className="table">
            <thead>
              <tr>
                <th>Zeitpunkt</th>
                <th>Aktion</th>
                <th>Akteur</th>
                <th>Detail</th>
              </tr>
            </thead>
            <tbody>
              {auditEntries.map((entry) => (
                <tr key={entry.auditEntryId}>
                  <td>{formatTimestamp(entry.createdAt)}</td>
                  <td>{entry.eventType}</td>
                  <td>{entry.actorDisplayName ?? "System"}</td>
                  <td>{entry.detail ?? "-"}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {hasMoreAudit ? (
            <div style={{ padding: "0.75rem 0" }}>
              <button
                className="btn btn-secondary btn-sm"
                onClick={() => void onLoadMoreAudit()}
                disabled={isLoadingMoreAudit}
              >
                {isLoadingMoreAudit ? "Wird geladen…" : "Mehr laden"}
              </button>
            </div>
          ) : null}
        </>
      ) : null}
    </section>
  );
}
