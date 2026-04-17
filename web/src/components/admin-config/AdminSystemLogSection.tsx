import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import EmptyState from "../feedback/EmptyState";
import LoadingState from "../feedback/LoadingState";
import { getAdminSystemLogs, getAdminSystemLogSummary, type AdminSystemLogQueryOptions } from "../../services/adminApi";
import type { AdminSystemLogEntry, AdminSystemLogSummary } from "../../types/auth";
import { formatDateTime } from "../../utils/dateFormat";

const SYSTEM_LOG_SOURCES = [
  "frontend",
  "api",
  "system",
  "mail",
  "entra",
  "directory",
  "automation",
  "workflow",
  "task",
  "rotation",
  "admin",
] as const;

function getDefaultSinceDate(): string {
  const date = new Date();
  date.setDate(date.getDate() - 7);
  return date.toISOString().slice(0, 10);
}

function getReferenceLink(entry: AdminSystemLogEntry): { to: string; label: string } | null {
  if (entry.workflowUid) {
    return {
      to: `/workflows/${entry.workflowUid}`,
      label: `Workflow ${entry.workflowUid}`,
    };
  }

  if (entry.rotationPlanId) {
    return {
      to: `/rotation/plans/${entry.rotationPlanId}`,
      label: `Plan ${entry.rotationPlanId}`,
    };
  }

  if (entry.taskRef?.startsWith("rot:")) {
    return {
      to: `/rotation/tasks/${encodeURIComponent(entry.taskRef)}`,
      label: `Task ${entry.taskRef}`,
    };
  }

  return null;
}

function buildQuery(options: {
  severities: string[];
  source: string;
  since: string;
  until: string;
  search: string;
  actorUserId: string;
  workflowUid: string;
  rotationPlanId: string;
  taskRef: string;
  limit: number;
  offset: number;
}): AdminSystemLogQueryOptions {
  return {
    severity: options.severities,
    source: options.source || null,
    since: options.since ? `${options.since}T00:00:00` : null,
    until: options.until ? `${options.until}T23:59:59` : null,
    search: options.search || null,
    actorUserId: options.actorUserId ? Number(options.actorUserId) : null,
    workflowUid: options.workflowUid || null,
    rotationPlanId: options.rotationPlanId ? Number(options.rotationPlanId) : null,
    taskRef: options.taskRef || null,
    limit: options.limit,
    offset: options.offset,
  };
}

export function AdminSystemLogSection() {
  const [severities, setSeverities] = useState<string[]>(["warning", "error"]);
  const [source, setSource] = useState("");
  const [since, setSince] = useState(getDefaultSinceDate);
  const [until, setUntil] = useState("");
  const [search, setSearch] = useState("");
  const [actorUserId, setActorUserId] = useState("");
  const [workflowUid, setWorkflowUid] = useState("");
  const [rotationPlanId, setRotationPlanId] = useState("");
  const [taskRef, setTaskRef] = useState("");
  const [limit, setLimit] = useState(50);
  const [offset, setOffset] = useState(0);
  const [entries, setEntries] = useState<AdminSystemLogEntry[]>([]);
  const [summary, setSummary] = useState<AdminSystemLogSummary | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const currentPage = Math.floor(offset / limit) + 1;
  const query = useMemo(
    () =>
      buildQuery({
        severities,
        source,
        since,
        until,
        search,
        actorUserId,
        workflowUid,
        rotationPlanId,
        taskRef,
        limit,
        offset,
      }),
    [actorUserId, limit, offset, rotationPlanId, search, severities, since, source, taskRef, until, workflowUid]
  );

  useEffect(() => {
    let isCancelled = false;

    async function load() {
      setIsLoading(true);
      setError(null);

      try {
        const [nextEntries, nextSummary] = await Promise.all([
          getAdminSystemLogs(query),
          getAdminSystemLogSummary(query),
        ]);

        if (isCancelled) {
          return;
        }

        setEntries(nextEntries);
        setSummary(nextSummary);
      } catch (loadError) {
        if (isCancelled) {
          return;
        }

        setError(loadError instanceof Error ? loadError.message : "System-Logs konnten nicht geladen werden.");
      } finally {
        if (!isCancelled) {
          setIsLoading(false);
        }
      }
    }

    void load();
    return () => {
      isCancelled = true;
    };
  }, [query]);

  function toggleSeverity(severity: string) {
    setOffset(0);
    setSeverities((current) => {
      if (current.includes(severity)) {
        return current.length === 1 ? current : current.filter((item) => item !== severity);
      }

      return [...current, severity];
    });
  }

  return (
    <section className="panel admin-system-log-panel">
      <div className="panel-head">
        <h2>Zentrale System-Logs</h2>
        <p>Warnings und Fehler der letzten sieben Tage sind standardmäßig aktiv. Info kann bei Bedarf zugeschaltet werden.</p>
      </div>

      <div className="admin-system-log-summary-grid" aria-label="Zusammenfassung">
        <article className="admin-system-log-summary-card">
          <span>Einträge</span>
          <strong>{summary?.totalCount ?? 0}</strong>
        </article>
        <article className="admin-system-log-summary-card">
          <span>Warnungen</span>
          <strong>{summary?.warningCount ?? 0}</strong>
        </article>
        <article className="admin-system-log-summary-card">
          <span>Fehler</span>
          <strong>{summary?.errorCount ?? 0}</strong>
        </article>
        <article className="admin-system-log-summary-card">
          <span>Info</span>
          <strong>{summary?.infoCount ?? 0}</strong>
        </article>
      </div>

      {summary && summary.sources.length > 0 ? (
        <div className="admin-system-log-source-list" aria-label="Quellen">
          {summary.sources.map((item) => (
            <span key={item.source} className="admin-system-log-source-chip">
              {item.source}: {item.count}
            </span>
          ))}
        </div>
      ) : null}

      <div className="admin-system-log-filter-grid">
        <div className="admin-system-log-severity-group">
          {["warning", "error", "info"].map((severity) => (
            <label key={severity} className="admin-system-log-checkbox">
              <input
                type="checkbox"
                checked={severities.includes(severity)}
                onChange={() => toggleSeverity(severity)}
              />
              <span>{severity}</span>
            </label>
          ))}
        </div>

        <label className="field compact">
          <span>Quelle</span>
          <select
            value={source}
            onChange={(event) => {
              setOffset(0);
              setSource(event.target.value);
            }}
          >
            <option value="">Alle Quellen</option>
            {SYSTEM_LOG_SOURCES.map((item) => (
              <option key={item} value={item}>
                {item}
              </option>
            ))}
          </select>
        </label>

        <label className="field compact">
          <span>Seit</span>
          <input
            type="date"
            value={since}
            onChange={(event) => {
              setOffset(0);
              setSince(event.target.value);
            }}
          />
        </label>

        <label className="field compact">
          <span>Bis</span>
          <input
            type="date"
            value={until}
            onChange={(event) => {
              setOffset(0);
              setUntil(event.target.value);
            }}
          />
        </label>

        <label className="field compact">
          <span>Suche</span>
          <input
            type="text"
            value={search}
            onChange={(event) => {
              setOffset(0);
              setSearch(event.target.value);
            }}
            placeholder="Meldung, Route, Funktion"
          />
        </label>

        <label className="field compact">
          <span>User-ID</span>
          <input
            type="number"
            value={actorUserId}
            onChange={(event) => {
              setOffset(0);
              setActorUserId(event.target.value);
            }}
            placeholder="optional"
          />
        </label>

        <label className="field compact">
          <span>Workflow</span>
          <input
            type="text"
            value={workflowUid}
            onChange={(event) => {
              setOffset(0);
              setWorkflowUid(event.target.value);
            }}
            placeholder="Workflow-UID"
          />
        </label>

        <label className="field compact">
          <span>Plan-ID</span>
          <input
            type="number"
            value={rotationPlanId}
            onChange={(event) => {
              setOffset(0);
              setRotationPlanId(event.target.value);
            }}
            placeholder="optional"
          />
        </label>

        <label className="field compact">
          <span>Task-Ref</span>
          <input
            type="text"
            value={taskRef}
            onChange={(event) => {
              setOffset(0);
              setTaskRef(event.target.value);
            }}
            placeholder="z. B. rot:123"
          />
        </label>

        <label className="field compact">
          <span>Seite</span>
          <select
            value={String(limit)}
            onChange={(event) => {
              setOffset(0);
              setLimit(Number(event.target.value));
            }}
          >
            <option value="25">25</option>
            <option value="50">50</option>
            <option value="100">100</option>
          </select>
        </label>
      </div>

      {isLoading ? <LoadingState title="System-Logs werden geladen..." /> : null}
      {!isLoading && error ? <EmptyState title="System-Logs konnten nicht geladen werden." description={error} /> : null}

      {!isLoading && !error && entries.length === 0 ? (
        <EmptyState
          title="Keine System-Logs gefunden"
          description="Mit den aktuellen Filtern gibt es keine passenden Einträge."
        />
      ) : null}

      {!isLoading && !error && entries.length > 0 ? (
        <>
          <div className="table-scroll">
            <table className="table admin-system-log-table">
              <thead>
                <tr>
                  <th>Zeitpunkt</th>
                  <th>Severity</th>
                  <th>Quelle</th>
                  <th>Bereich / Funktion</th>
                  <th>Nutzermeldung</th>
                  <th>Technische Meldung</th>
                  <th>Nutzer</th>
                  <th>Bezug</th>
                  <th>Status / HTTP</th>
                </tr>
              </thead>
              <tbody>
                {entries.map((entry) => {
                  const referenceLink = getReferenceLink(entry);
                  return (
                    <tr key={entry.id}>
                      <td>{formatDateTime(entry.createdAt)}</td>
                      <td>
                        <span className={`admin-system-log-pill admin-system-log-pill--${entry.severity}`}>
                          {entry.severity}
                        </span>
                      </td>
                      <td>{entry.source}</td>
                      <td>
                        <div className="admin-system-log-cell-stack">
                          <strong>{entry.clientFunction ?? entry.category}</strong>
                          <span>{entry.clientRoute ?? entry.httpPath ?? "-"}</span>
                        </div>
                      </td>
                      <td>{entry.userMessage ?? "-"}</td>
                      <td>
                        <div className="admin-system-log-cell-stack">
                          <span>{entry.message}</span>
                          <span className="text-muted">{entry.eventKey}</span>
                        </div>
                      </td>
                      <td>{entry.actorDisplayName ?? (entry.actorUserId ? `User ${entry.actorUserId}` : "-")}</td>
                      <td>
                        <div className="admin-system-log-cell-stack">
                          {referenceLink ? <Link to={referenceLink.to}>{referenceLink.label}</Link> : null}
                          {!referenceLink && entry.taskRef ? <span>{entry.taskRef}</span> : null}
                          {!referenceLink && !entry.taskRef && !entry.workflowUid && !entry.rotationPlanId ? <span>-</span> : null}
                        </div>
                      </td>
                      <td>
                        <div className="admin-system-log-cell-stack">
                          <span>{entry.httpStatus ?? "-"}</span>
                          <span>{entry.httpMethod ? `${entry.httpMethod} ${entry.httpPath ?? ""}`.trim() : "-"}</span>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          <div className="action-row admin-system-log-pagination">
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => setOffset((current) => Math.max(0, current - limit))}
              disabled={offset === 0}
            >
              Vorherige Seite
            </button>
            <span className="panel-note">Seite {currentPage}</span>
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => setOffset((current) => current + limit)}
              disabled={entries.length < limit}
            >
              Nächste Seite
            </button>
          </div>
        </>
      ) : null}
    </section>
  );
}
