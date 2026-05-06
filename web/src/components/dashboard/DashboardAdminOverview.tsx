import { Link } from "react-router-dom";
import type { DashboardInsights } from "./dashboardInsights";
import DashboardAdminRuntimeHealthBlock from "./DashboardAdminRuntimeHealthBlock";

type DashboardAdminOverviewProps = {
  insights: DashboardInsights;
  isRefreshing: boolean;
  onRefresh: () => void | Promise<unknown>;
};

function pluralize(count: number, singular: string, plural: string): string {
  return count === 1 ? singular : plural;
}

export default function DashboardAdminOverview(props: DashboardAdminOverviewProps) {
  const { insights, isRefreshing, onRefresh } = props;
  const adminSummary = insights.adminSummary;
  const adminWarnings = insights.adminWarnings ?? [];
  const adminOperations = insights.adminOperations ?? [];

  if (!adminSummary) {
    return null;
  }

  return (
    <div className="dashboard-admin">
      <section className="panel dashboard-admin-hero">
        <div className="dashboard-admin-hero__content">
          <p className="dashboard-admin-hero__kicker">{adminSummary.statusKicker}</p>
          <h2 className="dashboard-admin-hero__title">{adminSummary.statusTitle}</h2>
          <p className="dashboard-admin-hero__detail">{adminSummary.statusDetail}</p>
        </div>
        <div className="dashboard-admin-hero__controls">
          {adminSummary.action ? (
            <Link to={adminSummary.action.to} className="btn btn-primary">
              {adminSummary.action.label}
            </Link>
          ) : null}
          <button
            type="button"
            className="btn btn-secondary"
            onClick={() => {
              void onRefresh();
            }}
            disabled={isRefreshing}
          >
            {isRefreshing ? "Aktualisiere..." : "Aktualisieren"}
          </button>
        </div>
      </section>

      <section className="dashboard-admin-kpis" aria-label="Admin-Kennzahlen">
        {adminSummary.stats.map((stat) => (
          <article
            key={stat.label}
            className={`dashboard-admin-kpi dashboard-admin-kpi--${stat.tone ?? "neutral"}`}
          >
            <span className="dashboard-admin-kpi__label">{stat.label}</span>
            <strong className="dashboard-admin-kpi__value">{stat.value}</strong>
            <p className="dashboard-admin-kpi__note">{stat.note}</p>
          </article>
        ))}
      </section>

      <div className="dashboard-admin-grid">
        <section
          className="panel dashboard-admin-panel dashboard-admin-panel--attention"
          aria-labelledby="dashboard-admin-attention"
        >
          <div className="dashboard-admin-panel__head">
            <div>
              <h2 id="dashboard-admin-attention">Aufmerksamkeit jetzt</h2>
              <p>Governance-Lücken sind nach Themen gruppiert und priorisiert.</p>
            </div>
          </div>

          {adminWarnings.length > 0 ? (
            <div className="dashboard-admin-warning-clusters">
              {adminWarnings.map((cluster) => (
                <section key={cluster.key} className="dashboard-admin-warning-cluster">
                  <div className="dashboard-admin-warning-cluster__head">
                    <div>
                      <h3>{cluster.title}</h3>
                      <p>
                        {cluster.totalCount}{" "}
                        {pluralize(cluster.totalCount, "offene Warnung", "offene Warnungen")}
                      </p>
                    </div>
                  </div>

                  <ul className="dashboard-admin-warning-list">
                    {cluster.groups.map((group) => (
                      <li key={group.key} className="dashboard-admin-warning-item">
                        <div className="dashboard-admin-warning-item__copy">
                          <div className="dashboard-admin-warning-item__head">
                            <h4>{group.title}</h4>
                            <span className="dashboard-admin-warning-item__count">
                              {group.count} {pluralize(group.count, "Fall", "Fälle")}
                            </span>
                          </div>
                          <p className="dashboard-admin-warning-item__detail">{group.detail}</p>
                          {group.affectedLabels.length > 0 ? (
                            <p className="dashboard-admin-warning-item__affected">
                              Betrifft: {group.affectedLabels.join(", ")}
                            </p>
                          ) : null}
                        </div>
                        <Link to={group.to} className="btn btn-secondary">
                          {group.actionLabel}
                        </Link>
                      </li>
                    ))}
                  </ul>
                </section>
              ))}
            </div>
          ) : (
            <div className="dashboard-admin-empty">
              <h3>Keine offenen Governance-Lücken</h3>
              <p>Abteilungen, Zuständigkeiten und Mail-Konfiguration wirken aktuell vollständig.</p>
            </div>
          )}
        </section>

        <div className="dashboard-admin-runtime">
          <DashboardAdminRuntimeHealthBlock />
        </div>

        <section
          className="panel dashboard-admin-panel dashboard-admin-panel--operations"
          aria-labelledby="dashboard-admin-operations"
        >
          <div className="dashboard-admin-panel__head">
            <div>
              <h2 id="dashboard-admin-operations">Operative Risiken</h2>
              <p>Festhängende Vorgänge und Rückstau in Freigaben oder Fachbereichen.</p>
            </div>
          </div>

          {adminOperations.length > 0 ? (
            <ul className="dashboard-admin-operations">
              {adminOperations.map((item) => (
                <li
                  key={item.key}
                  className={`dashboard-admin-operation dashboard-admin-operation--${item.tone ?? "neutral"}`}
                >
                  <div className="dashboard-admin-operation__copy">
                    <h3>{item.title}</h3>
                    <p>{item.detail}</p>
                  </div>
                  <Link to={item.to} className="btn btn-secondary">
                    {item.actionLabel}
                  </Link>
                </li>
              ))}
            </ul>
          ) : (
            <div className="dashboard-admin-empty">
              <h3>Keine operativen Risiken sichtbar</h3>
              <p>Es gibt aktuell weder festhängende Vorgänge noch einen erkennbaren Rückstau.</p>
            </div>
          )}
        </section>
      </div>
    </div>
  );
}
