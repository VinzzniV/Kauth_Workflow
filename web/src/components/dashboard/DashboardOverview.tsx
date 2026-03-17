// Rollenspezifisches Dashboard mit Kennzahlen und dem naechsten sinnvollen Arbeitsschritt.
import { Link } from "react-router-dom";
import { useCurrentUser } from "../../auth/useCurrentUser";
import { useRoleAwareNavigation } from "../../navigation/useRoleAwareNavigation";
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
  const { roleLabels } = useCurrentUser();
  const { dashboardActions, secondaryDashboardActions, dashboardContext, dashboardPersona } = useRoleAwareNavigation();
  const { insights, insightsError, isInsightsLoading, reloadInsights } = useDashboardInsights(dashboardPersona);

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
      <section className="panel panel-intro">
        <div className="panel-head">
          <h2>{dashboardContext.title}</h2>
          <p>{dashboardContext.description}</p>
        </div>
        {roleLabels.length > 0 ? <p className="panel-note dashboard-user-summary">Rolle: {roleLabels.join(", ")}</p> : null}
        <div className="action-row">
          <button
            type="button"
            className="btn btn-secondary"
            onClick={() => void reloadInsights()}
            disabled={isInsightsLoading}
          >
            {isInsightsLoading ? "Aktualisiere..." : "Übersicht aktualisieren"}
          </button>
        </div>
      </section>

      <section className="dashboard-grid" aria-label="Hauptaktionen">
        {dashboardActions.map((action) => (
          <Link key={action.to} to={action.to} className="dashboard-card">
            <h2>{action.label}</h2>
            <p>{action.description}</p>
            <span className="card-link">Öffnen</span>
          </Link>
        ))}
      </section>

      {isInsightsLoading ? <LoadingState title="Übersicht wird geladen..." /> : null}

      {!isInsightsLoading && insightsError ? (
        <EmptyState
          title="Übersichtsdaten konnten nicht geladen werden."
          description={insightsError}
          actionLabel="Erneut laden"
          onAction={() => {
            void reloadInsights();
          }}
        />
      ) : null}

      {!isInsightsLoading && !insightsError && insights ? (
        <>
          <section className="panel">
            <div className="panel-head">
              <h2>{insights.heading}</h2>
              <p>{insights.summary}</p>
            </div>
            <div className="next-action-callout" role="status" aria-live="polite">
              <p className="next-action-label">Nächste nötige Aktion</p>
              <p className="next-action-text">{insights.nextStep}</p>
            </div>
            {insights.stats.length > 0 ? (
              <div className="dashboard-stats-grid" aria-label="Rollenspezifische Übersicht">
                {insights.stats.map((stat) => (
                  <article key={stat.label} className="dashboard-stat-card">
                    <p className="dashboard-stat-label">{stat.label}</p>
                    {stat.statusLabel ? (
                      <span className={getStatToneClassName(stat.tone)}>{stat.statusLabel}</span>
                    ) : null}
                    <p className="dashboard-stat-value">{stat.value}</p>
                    <p className="dashboard-stat-note">{stat.note}</p>
                  </article>
                ))}
              </div>
            ) : (
              <p className="panel-note">Für diese Rolle sind aktuell keine Kennzahlen verfügbar.</p>
            )}
          </section>

          <section className="panel panel-muted dashboard-queue">
            <div className="panel-head">
              <h2>{insights.queueTitle}</h2>
              <p>{insights.queueDescription}</p>
            </div>

            {insights.queueItems.length === 0 ? (
              <p className="panel-note">{insights.emptyQueueText}</p>
            ) : (
              <ul className="dashboard-queue-list">
                {insights.queueItems.map((item) => (
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
            )}
          </section>
        </>
      ) : null}

      {secondaryDashboardActions.length > 0 ? (
        <section className="panel panel-muted">
          <div className="panel-head">
            <h2>Sekundäre Navigation</h2>
            <p>Weitere freigegebene Bereiche mit geringer Priorität.</p>
          </div>
          <nav className="dashboard-secondary-nav" aria-label="Weitere Navigation">
            {secondaryDashboardActions.map((action) => (
              <Link key={`secondary-${action.to}`} to={action.to} className="btn btn-secondary">
                {action.label}
              </Link>
            ))}
          </nav>
        </section>
      ) : null}
    </div>
  );
}
