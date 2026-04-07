import type {
  AdminDirectoryIdentity,
  AdminDirectoryMappingAuditEntry,
  AdminDirectorySyncStatus,
} from "../../types/auth";
import { useRef } from "react";
import EmptyState from "../feedback/EmptyState";
import LoadingState from "../feedback/LoadingState";
import { formatTimestamp } from "./adminConfigHelpers";

type AdminDirectorySyncSectionProps = {
  status: AdminDirectorySyncStatus | null;
  identities: AdminDirectoryIdentity[];
  auditEntries: AdminDirectoryMappingAuditEntry[];
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
  isLoading,
  isSyncing,
  onSync,
}: AdminDirectorySyncSectionProps) {
  const configuredGroupPrefix = status?.configuredGroupPrefix ?? "Onboarding";
  const groupPrefixInputRef = useRef<HTMLInputElement | null>(null);

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

      {!isLoading && identities.length > 0 ? (
        <>
          <div className="panel-head" style={{ marginTop: "1rem" }}>
            <h2>Zuletzt übernommene Identitäten</h2>
          </div>

          <table className="table">
            <thead>
              <tr>
                <th>Name</th>
                <th>UPN / Mail</th>
                <th>Konto</th>
                <th>Verknüpft mit</th>
                <th>Zuletzt synchronisiert</th>
              </tr>
            </thead>
            <tbody>
              {identities.map((identity) => (
                <tr key={identity.directoryIdentityId}>
                  <td>{identity.displayName}</td>
                  <td>
                    <div>{identity.userPrincipalName}</div>
                    {identity.mail ? <div className="panel-note">{identity.mail}</div> : null}
                  </td>
                  <td>
                    <span className={`badge badge--${identity.accountEnabled ? "success" : "error"}`}>
                      {identity.accountEnabled ? "Aktiv" : "Deaktiviert"}
                    </span>
                  </td>
                  <td>{identity.appUserDisplayName ?? "Noch nicht verknüpft"}</td>
                  <td>{formatTimestamp(identity.lastSyncedAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      ) : null}

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
        </>
      ) : null}
    </section>
  );
}
