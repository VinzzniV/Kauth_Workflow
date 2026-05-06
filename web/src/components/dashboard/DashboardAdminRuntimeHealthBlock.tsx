import { useQuery } from "@tanstack/react-query";
import type { AuthDependencyRuntimeHealth, DependenciesRuntimeHealth, HostRuntimeHealth } from "../../types/auth";
import { getAdminRuntimeHealth } from "../../services/adminApi";
import { queryKeys } from "../../services/queryKeys";

type HealthTone = "neutral" | "success" | "warning" | "danger";

function severityToTone(severity: string): HealthTone {
  switch (severity) {
    case "ok": return "success";
    case "warning": return "warning";
    case "critical": return "danger";
    default: return "neutral";
  }
}

function severityLabel(severity: string): string {
  switch (severity) {
    case "ok": return "OK";
    case "warning": return "Warnung";
    case "critical": return "Kritisch";
    default: return "Unbekannt";
  }
}

function formatUptime(seconds: number): string {
  if (seconds < 3600) {
    const m = Math.floor(seconds / 60);
    return m > 0 ? `${m}min` : "<1min";
  }
  if (seconds < 86400) {
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    return m > 0 ? `${h}h ${m}min` : `${h}h`;
  }
  const d = Math.floor(seconds / 86400);
  const h = Math.floor((seconds % 86400) / 3600);
  return h > 0 ? `${d}d ${h}h` : `${d}d`;
}

function formatBytes(bytes: number): string {
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${Math.round(bytes / 1024 / 1024)} MB`;
  return `${(bytes / 1024 / 1024 / 1024).toFixed(1)} GB`;
}

function hostSummaryLabel(host: HostRuntimeHealth): string {
  switch (host.severity) {
    case "ok": return "OK";
    case "warning": return "Warnung";
    case "critical": return "Kritisch";
    default: return "Unbekannt";
  }
}

function dependenciesSummaryLabel(deps: DependenciesRuntimeHealth): string {
  switch (deps.severity) {
    case "ok": return "Alles erreichbar";
    case "warning": return "Teilweise verfügbar";
    case "critical": return "Kritisch";
    default: return "Unbekannt";
  }
}

function authShortLabel(auth: AuthDependencyRuntimeHealth): string {
  if (auth.mode === "dev-sim") return "dev-sim";
  if (auth.mode === "none") return "deaktiviert";
  if (auth.reachability === "reachable") {
    return auth.latencyMs != null ? `${auth.latencyMs}ms` : "OK";
  }
  if (auth.reachability === "unreachable") return "Fehler";
  return auth.reachability;
}

export default function DashboardAdminRuntimeHealthBlock() {
  const query = useQuery({
    queryKey: queryKeys.admin.runtimeHealth(),
    queryFn: getAdminRuntimeHealth,
    staleTime: 60 * 1000,
    refetchInterval: 2 * 60 * 1000,
  });

  const health = query.data ?? null;
  const overallSeverity = health?.overallSeverity ?? null;

  const panelModifier = overallSeverity === "critical"
    ? "admin-health-panel--critical"
    : overallSeverity === "warning"
      ? "admin-health-panel--warning"
      : overallSeverity === "ok"
        ? "admin-health-panel--clear"
        : "";

  return (
    <section className={`panel admin-health-panel ${panelModifier}`} aria-label="Betriebsstatus">
      <div className="admin-health-head-row">
        <div>
          <h2 className="panel-title">Betriebsstatus</h2>
          <p className="panel-text">API-Prozess, Abhängigkeiten und Schreibpfade der Anwendung.</p>
        </div>
        {overallSeverity ? (
          <span className={`admin-health-severity-badge admin-health-severity-badge--${severityToTone(overallSeverity)}`}>
            {severityLabel(overallSeverity)}
          </span>
        ) : null}
      </div>

      {query.isError ? (
        <p className="panel-note admin-health-load-notice">Betriebsstatus konnte nicht geladen werden.</p>
      ) : null}

      {health ? (
        <div className="admin-health-meta admin-health-meta--runtime">
          <div className={`admin-health-metric admin-health-metric--${severityToTone(health.application.severity)}`}>
            <span>API-Prozess</span>
            <strong>{formatUptime(health.application.uptimeSeconds)}</strong>
            <p className="admin-health-metric-detail">
              Managed Heap: {formatBytes(health.application.managedHeapBytes)}
              {health.application.managedHeapHighThresholdBytes
                ? ` / ${formatBytes(health.application.managedHeapHighThresholdBytes)}`
                : ""}
            </p>
          </div>

          <div className={`admin-health-metric admin-health-metric--${severityToTone(health.dependencies.severity)}`}>
            <span>Abhängigkeiten</span>
            <strong>{dependenciesSummaryLabel(health.dependencies)}</strong>
            <p className="admin-health-metric-detail">
              DB:{" "}
              {health.dependencies.database.reachable
                ? health.dependencies.database.latencyMs != null
                  ? `${health.dependencies.database.latencyMs}ms`
                  : "OK"
                : "Fehler"}
              {" · "}
              Auth: {authShortLabel(health.dependencies.auth)}
            </p>
          </div>

          {health.storage.map((s) => (
            <div key={s.path} className={`admin-health-metric admin-health-metric--${severityToTone(s.severity)}`}>
              <span>Schreibpfad: {s.label}</span>
              <strong>{s.usedPercent.toFixed(0)}% belegt</strong>
              <p className="admin-health-metric-detail">
                {formatBytes(s.totalBytes - s.freeBytes)} / {formatBytes(s.totalBytes)}
              </p>
            </div>
          ))}

          {health.host ? (
            <div className={`admin-health-metric admin-health-metric--${severityToTone(health.host.severity)}`}>
              <span>Host / VM</span>
              <strong>{hostSummaryLabel(health.host)}</strong>
              <p className="admin-health-metric-detail">
                Uptime: {formatUptime(health.host.uptimeSeconds)}
                {" · "}
                RAM: {health.host.memUsedPercent.toFixed(0)}% belegt
                {" · "}
                Root-FS: {health.host.rootFsUsedPercent.toFixed(0)}% belegt
                {" · "}
                Last: {health.host.loadAverage1m.toFixed(2)}
              </p>
            </div>
          ) : null}
        </div>
      ) : null}
    </section>
  );
}
