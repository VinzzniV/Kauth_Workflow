// Rollenspezifisches Dashboard mit Kennzahlen und dem naechsten sinnvollen Arbeitsschritt.
import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { useRoleAwareNavigation } from "../../navigation/useRoleAwareNavigation";
import { useProcessTypes } from "../../services/queries/processTypeQueries";
import {
  getWorkflowRuntimeStatusLabel,
  getWorkflowRuntimeStatusPillClass,
} from "../../utils/workflowStatus";
import EmptyState from "../feedback/EmptyState";
import LoadingState from "../feedback/LoadingState";
import Card from "../ui/Card";
import SectionHeader from "../ui/SectionHeader";
import { useDashboardInsights } from "./useDashboardInsights";

export default function DashboardOverview() {
  const { dashboardActions, dashboardContext, dashboardPersona } = useRoleAwareNavigation();
  const supportsProcessTypeFilter =
    dashboardPersona === "admin" ||
    dashboardPersona === "hr" ||
    dashboardPersona === "manager" ||
    dashboardPersona === "reader";
  const [selectedProcessTypeKey, setSelectedProcessTypeKey] = useState<string>("all");
  const processTypesQuery = useProcessTypes();
  const processTypes = useMemo(
    () => (supportsProcessTypeFilter ? processTypesQuery.data ?? [] : []),
    [processTypesQuery.data, supportsProcessTypeFilter]
  );
  const isProcessTypeLoading = supportsProcessTypeFilter ? processTypesQuery.isLoading : false;
  const effectiveProcessTypeKey =
    supportsProcessTypeFilter &&
    (selectedProcessTypeKey === "all" ||
      processTypes.some((processType) => processType.key === selectedProcessTypeKey))
      ? selectedProcessTypeKey
      : "all";
  const processTypeKey = effectiveProcessTypeKey === "all" ? null : effectiveProcessTypeKey;
  const selectedProcessType = processTypes.find((processType) => processType.key === processTypeKey) ?? null;
  const { insights, insightsError, isInsightsLoading, reloadInsights } = useDashboardInsights(
    dashboardPersona,
    processTypeKey,
    selectedProcessType
  );
  const priorityItem = insights?.queueItems[0] ?? null;
  const secondaryQueueItems = insights?.queueItems.slice(1) ?? [];
  const employeeItems = insights?.employeeItems ?? [];

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
          <section className="dashboard-priority-panel">
            <div className="dashboard-top-row">
              <SectionHeader title={dashboardContext.title} />
              {supportsProcessTypeFilter && processTypes.length > 1 ? (
                <label className="field compact dashboard-filter-field">
                  <span>Prozesstyp</span>
                  <select
                    value={effectiveProcessTypeKey}
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
              <Link to={priorityItem.to} className="dashboard-priority-card dashboard-priority-card--action card-primary">
                <div>
                  <p className="dashboard-priority-kicker">Jetzt prüfen</p>
                  <p className="dashboard-priority-title">{insights.nextStep}</p>
                  <p className="dashboard-priority-detail">{priorityItem.title}</p>
                </div>
                <span className="dashboard-priority-action">{priorityItem.actionLabel}</span>
              </Link>
            ) : (
              <article className="dashboard-priority-card card-primary">
                <div>
                  <p className="dashboard-priority-kicker">Jetzt prüfen</p>
                  <p className="dashboard-priority-title">{insights.nextStep}</p>
                  <p className="dashboard-priority-detail">{insights.emptyQueueText}</p>
                </div>
              </article>
            )}
          </section>

          {insights.stats.length > 0 ? (
            <section className="section-stack">
              <SectionHeader title="Kennzahlen" />
                <div className="dashboard-stats-grid" aria-label="Rollenspezifische Übersicht">
                  {insights.stats.map((stat) => (
                    <Card
                      key={stat.label}
                      variant="stat"
                      className={`dashboard-stat-card${stat.tone ? ` dashboard-stat-card--${stat.tone}` : ""}`}
                    >
                      <p className="dashboard-stat-label">{stat.label}</p>
                      <p className="dashboard-stat-value">{stat.value}</p>
                    </Card>
                  ))}
                </div>
              </section>
          ) : null}

          {secondaryQueueItems.length > 0 ? (
            <section className="section-stack dashboard-queue">
              <SectionHeader title={insights.queueTitle} />
              <ul className="dashboard-queue-list">
                {secondaryQueueItems.map((item) => (
                  <Card key={item.key} as="li" variant="list" className="dashboard-queue-item">
                    <div>
                      <p className="dashboard-queue-title">{item.title}</p>
                      <p className="dashboard-queue-detail">{item.detail}</p>
                    </div>
                    <Link to={item.to} className="btn btn-secondary">
                      {item.actionLabel}
                    </Link>
                  </Card>
                ))}
              </ul>
            </section>
          ) : null}

          {dashboardPersona === "manager" && employeeItems.length > 0 ? (
            <section className="section-stack dashboard-queue">
              <SectionHeader
                title={insights.employeeListTitle ?? "Mitarbeitende"}
                description={insights.employeeListDescription}
              />
              <ul className="dashboard-employee-list" aria-label="Mitarbeitende mit Vorgängen">
                {employeeItems.map((item) => (
                  <Card key={item.key} as="li" variant="list" className="dashboard-employee-item">
                    <div className="dashboard-employee-main">
                      <div className="dashboard-employee-head">
                        <div className="dashboard-employee-copy">
                          <p className="dashboard-employee-name">{item.name}</p>
                          <p className="dashboard-employee-role">{item.roleName}</p>
                        </div>
                        <span className={`status-pill ${getWorkflowRuntimeStatusPillClass(item.workflowStatus)}`}>
                          {getWorkflowRuntimeStatusLabel(item.workflowStatus)}
                        </span>
                      </div>

                      <div className="chips-row dashboard-employee-chips" aria-label="Vorgangskontext">
                        <span className="chip">{item.processTypeName}</span>
                        {item.departmentName ? <span className="chip">{item.departmentName}</span> : null}
                      </div>

                      <p className="dashboard-employee-context">{item.contextText}</p>
                      <p className="dashboard-employee-date">
                        {item.dateLabel}: {item.dateValue}
                      </p>
                    </div>

                    <Link to={item.to} className="btn btn-secondary">
                      {item.actionLabel}
                    </Link>
                  </Card>
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
