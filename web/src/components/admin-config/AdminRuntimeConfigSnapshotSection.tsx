import { useQuery } from "@tanstack/react-query";
import { getAdminSystemConfigSnapshot } from "../../services/adminApi";
import { queryKeys } from "../../services/queryKeys";
import type { SystemConfigSnapshot } from "../../types/systemConfigSnapshot";

export function AdminRuntimeConfigSnapshotSection() {
  const query = useQuery({
    queryKey: queryKeys.admin.systemConfigSnapshot(),
    queryFn: getAdminSystemConfigSnapshot,
    staleTime: 30 * 1000,
  });

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Aktive Laufzeit-Konfiguration</h2>
        <p>
          Read-only Spiegel der tatsächlich aktiven Konfigurationswerte des laufenden API-Containers. Geheimnisse
          (`ENTRA_CLIENT_SECRET`, `GRAPH_CLIENT_SECRET`, `KAUTH_VAULT_KEY`) werden nie als Klartext angezeigt — der
          Endpoint liefert nur „set"/„unset" bzw. „present"/„missing".
        </p>
      </div>

      {query.isPending ? (
        <p className="panel-note">Lade Konfigurationsdaten …</p>
      ) : query.isError ? (
        <p className="panel-note text-danger">
          Konfiguration konnte nicht geladen werden. Bitte erneut versuchen oder Administrator kontaktieren.
        </p>
      ) : (
        <SnapshotBody snapshot={query.data} />
      )}
    </section>
  );
}

function SnapshotBody({ snapshot }: { snapshot: SystemConfigSnapshot }) {
  return (
    <div className="content-stack">
      <SnapshotCard title="Allgemein">
        <KeyValue label="Environment" value={snapshot.environment} />
        <KeyValue label="Production" value={snapshot.isProduction ? "ja" : "nein"} />
        <KeyValue label="Auth-Mode (API)" value={snapshot.authMode} />
        <KeyValue label="Public Base-URL" value={snapshot.publicBaseUrl ?? "(unset)"} />
        <KeyValue label="Swagger aktiviert" value={snapshot.swaggerEnabled ? "ja" : "nein"} />
      </SnapshotCard>

      <SnapshotCard title="Entra (Authentifizierung + Graph)">
        <KeyValue label="Tenant-ID" value={snapshot.entra.tenantId ?? "(unset)"} />
        <KeyValue label="Client-ID" value={snapshot.entra.clientId ?? "(unset)"} />
        <KeyValue label="Audience" value={snapshot.entra.audience ?? "(unset)"} />
        <KeyValue label="ENTRA_CLIENT_SECRET" value={snapshot.entra.clientSecretStatus} />
        <KeyValue label="GRAPH_CLIENT_SECRET" value={snapshot.entra.graphClientSecretStatus} />
      </SnapshotCard>

      <SnapshotCard title="Directory-Sync">
        <KeyValue label="Geplanter Sync aktiv" value={snapshot.directory.syncScheduled ? "ja" : "nein"} />
        <KeyValue label="Sync-Intervall" value={`${snapshot.directory.syncIntervalMinutes} min`} />
        <KeyValue label="Gruppen-Prefix" value={snapshot.directory.groupPrefix ?? "(unset)"} />
        <KeyValue label="Explizite Gruppen-IDs" value={snapshot.directory.explicitGroupIds ?? "(unset)"} />
        <KeyValue label="Auto-Provision Default-Rolle" value={snapshot.directory.autoProvisionDefaultRoleKey ?? "(unset)"} />
      </SnapshotCard>

      <SnapshotCard title="Benachrichtigungs-Mail">
        <KeyValue label="Aktiv" value={snapshot.email.enabled ? "ja" : "nein"} />
        <KeyValue label="Provider" value={snapshot.email.provider} />
        <KeyValue label="Absender" value={snapshot.email.senderEmail ?? "(unset)"} />
        <KeyValue label="Frontend Base-URL" value={snapshot.email.frontendBaseUrl} />
        <KeyValue label="In gesendete Elemente speichern" value={snapshot.email.saveToSentItems ? "ja" : "nein"} />
      </SnapshotCard>

      <SnapshotCard title="Vault (Initial-Passwörter)">
        <KeyValue label="KAUTH_VAULT_KEY" value={snapshot.vault.keyStatus} />
      </SnapshotCard>

      <SnapshotCard title="Automation-Retry">
        <KeyValue label="Max. Versuche" value={String(snapshot.retry.maxAttempts)} />
        <KeyValue label="Erster Retry-Delay" value={`${snapshot.retry.firstRetryDelaySeconds}s`} />
        <KeyValue label="Folge-Retry-Delay" value={`${snapshot.retry.subsequentRetryDelaySeconds}s`} />
      </SnapshotCard>

      <SnapshotCard title="Worker-Lease (API-Sweeper)">
        <KeyValue
          label="Stale-Claim-Timeout"
          value={`${snapshot.workerLease.staleClaimTimeoutMinutes} min`}
        />
      </SnapshotCard>

      <SnapshotCard title="Host-Health (Linux-VM)">
        <KeyValue label="Aktiviert" value={snapshot.hostHealth.enabled ? "ja" : "nein"} />
        <KeyValue label="procfs-Pfad" value={snapshot.hostHealth.procfsPath ?? "(unset)"} />
        <KeyValue label="Root-Pfad" value={snapshot.hostHealth.rootPath ?? "(unset)"} />
        <KeyValue label="Storage-Pfade" value={snapshot.hostHealth.storagePaths ?? "(unset)"} />
      </SnapshotCard>
    </div>
  );
}

function SnapshotCard({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="dashboard-card admin-system-config-card">
      <div>
        <h2>{title}</h2>
      </div>
      <dl className="admin-runtime-config-snapshot__list">{children}</dl>
    </div>
  );
}

function KeyValue({ label, value }: { label: string; value: string }) {
  return (
    <div className="admin-runtime-config-snapshot__row">
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  );
}
