// Rollenspezifisches Dashboard mit Kennzahlen und dem naechsten sinnvollen Arbeitsschritt.
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useRoleAwareNavigation } from "../../navigation/useRoleAwareNavigation";
import { useProcessTypes } from "../../services/queries/processTypeQueries";
import EmptyState from "../feedback/EmptyState";
import LoadingState from "../feedback/LoadingState";
import type { DashboardStat } from "./dashboardInsights";
import { useDashboardInsights } from "./useDashboardInsights";

function getStatToneClassName(tone: DashboardStat["tone"]): string {
  switch (tone) {
    case "attention":
      return "dashboard-stat-chip attention";
    case "progress":
      return "dashboard-stat-chip progress";
    case "success":
      return "dashboard-stat-chip success";
    default:
      return "dashboard-stat-chip neutral";
  }
}

export default function DashboardOverview() {
  const { dashboardActions, dashboardContext, dashboardPersona } = useRoleAwareNavigation();
  const supportsProcessTypeFilter =
    dashboardPersona === "admin" ||
    dashboardPersona === "hr" ||
    dashboardPersona === "manager" ||
    dashboardPersona === "reader";
  const [selectedProcessTypeKey, setSelectedProcessTypeKey] = useState<string>("all");
  const processTypesQuery = useProcessTypes();
  const processTypes = supportsProcessTypeFilter ? processTypesQuery.data ?? [] : [];
  const isProcessTypeLoading = supportsProcessTypeFilter ? processTypesQuery.isLoading : false;
  const processTypeKey = selectedProcessTypeKey === "all" ? null : selectedProcessTypeKey;
  const selectedProcessType = processTypes.find((processType) => processType.key === processTypeKey) ?? null;
  const { insights, insightsError, isInsightsLoading, reloadInsights } = useDashboardInsights(
    dashboardPersona,
    processTypeKey,
    selectedProcessType
  );
  const priorityItem = insights?.queueItems[0] ?? null;
  const secondaryQueueItems = insights?.queueItems.slice(1) ?? [];

  useEffect(() => {
    if (!supportsProcessTypeFilter) {
      setSelectedProcessTypeKey("all");
      return;
    }

    if (selectedProcessTypeKey !== "all" && !processTypes.some((processType) => processType.key === selectedProcessTypeKey)) {
      setSelectedProcessTypeKey("all");
    }
  }, [processTypes, selectedProcessTypeKey, supportsProcessTypeFilter]);

  if (dashboardActions.length === 0) {
    return (
      <section className="panel panel-muted" role="status" aria-live="polite">
        <h2 className="panel-title">Keine verfügbaren Bereiche</h2>
        <p className="panel-text">Für die aktuelle Rolle sind hier keine Bereiche freigeschaltet.</p>
      </section>
    );
  }

  return (
    <div className="content-stack">
      {isInsightsLoading || isProcessTypeLoading ? <LoadingState title="Übersicht wird geladen..." /> : null}

      {!isInsightsLoading && !isProcessTypeLoading && insightsError ? (
        <EmptyState
          title="Übersichtsdaten konnten nicht geladen werden."
          description={insightsError}
          actionLabel="Erneut laden"
          onAction={() => {
            void reloadInsights();
          }}
        />
      ) : null}

      {!isInsightsLoading && !isProcessTypeLoading && !insightsError && insights ? (
        <>
          <section className="panel dashboard-priority-panel">
            <div className="dashboard-top-row">
              <div className="panel-head">
                <h2>{dashboardContext.title}</h2>
              </div>
              {supportsProcessTypeFilter && processTypes.length > 1 ? (
                <label className="field compact dashboard-filter-field">
                  <span>Prozesstyp</span>
                  <select
                    value={selectedProcessTypeKey}
                    onChange={(event) => setSelectedProcessTypeKey(event.target.value)}
                    disabled={isInsightsLoading || isProcessTypeLoading}
                  >
                    <option value="all">Alle</option>
                    {processTypes.map((processType) => (
                      <option key={processType.key} value={processType.key}>
                        {processType.name}
                      </option>
                    ))}
                  </select>
                </label>
              ) : null}
            </div>

            {priorityItem ? (
              <div className="next-action-callout">
                <p className="next-action-label">Jetzt prüfen</p>
                <Link to={priorityItem.to} className="next-action-text next-action-link">
                  {insights.nextStep}
                </Link>
              </div>
            ) : (
              <div className="next-action-callout">
                <p className="next-action-label">Jetzt prüfen</p>
                <p className="next-action-text">{insights.nextStep}</p>
              </div>
            )}

            {priorityItem ? (
              <article className="dashboard-priority-card">
                <div>
                  <p className="dashboard-priority-title">{priorityItem.title}</p>
                  <p className="dashboard-priority-detail">{priorityItem.detail}</p>
                </div>
                <Link to={priorityItem.to} className="btn btn-primary">
                  {priorityItem.actionLabel}
                </Link>
              </article>
            ) : (
              <p className="panel-note">{insights.emptyQueueText}</p>
            )}
          </section>

          {insights.stats.length > 0 ? (
            <section className="panel">
              <div className="panel-head">
                <h2>Kennzahlen</h2>
              </div>
              <div className="dashboard-stats-grid" aria-label="Rollenspezifische Übersicht">
                {insights.stats.map((stat) => (
                  <article key={stat.label} className="dashboard-stat-card">
                    <div className="dashboard-stat-card-head">
                      <p className="dashboard-stat-label">{stat.label}</p>
                      {stat.statusLabel ? (
                        <span className={getStatToneClassName(stat.tone)}>{stat.statusLabel}</span>
                      ) : null}
                    </div>
                    <p className="dashboard-stat-value">{stat.value}</p>
                    <p className="dashboard-stat-note">{stat.note}</p>
                  </article>
                ))}
              </div>
            </section>
          ) : null}

          {secondaryQueueItems.length > 0 ? (
            <section className="panel panel-muted dashboard-queue">
              <div className="panel-head">
                <h2>{insights.queueTitle}</h2>
              </div>
              <ul className="dashboard-queue-list">
                {secondaryQueueItems.map((item) => (
                  <li key={item.key} className="dashboard-queue-item">
                    <div>
                      <p className="dashboard-queue-title">{item.title}</p>
                      <p className="dashboard-queue-detail">{item.detail}</p>
                    </div>
                    <Link to={item.to} className="btn btn-secondary">
                      {item.actionLabel}
                    </Link>
                  </li>
                ))}
              </ul>
            </section>
          ) : null}

          <div className="dashboard-refresh-row">
            <button
              type="button"
              className="btn-text"
              onClick={() => void reloadInsights()}
              disabled={isInsightsLoading || isProcessTypeLoading}
            >
              {isInsightsLoading ? "Aktualisiere..." : "Übersicht aktualisieren"}
            </button>
          </div>
        </>
      ) : null}
    </div>
  );
}
