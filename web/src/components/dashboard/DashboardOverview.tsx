// Rollenspezifisches Dashboard mit Kennzahlen und dem naechsten sinnvollen Arbeitsschritt.
// Struktur: Zone 1 (Focus/Naechster Schritt), Zone 2 (Kennzahlen), Zone 3 (Offene Arbeit), Zone 4 (Admin: Betriebsstatus).
import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import DashboardAdminRuntimeHealthBlock from "./DashboardAdminRuntimeHealthBlock";
import { useRoleAwareNavigation } from "../../navigation/useRoleAwareNavigation";
import { useStartableWorkflowDefinitions } from "../../services/queries/workflowDefinitionQueries";
import {
  getWorkflowRuntimeStatusLabel,
  getWorkflowRuntimeStatusPillClass,
} from "../../utils/workflowStatus";
import EmptyState from "../feedback/EmptyState";
import LoadingState from "../feedback/LoadingState";
import { useDashboardInsights } from "./useDashboardInsights";

export default function DashboardOverview() {
  const { dashboardActions, dashboardContext, dashboardPersona } = useRoleAwareNavigation();
  const supportsProcessTypeFilter =
    dashboardPersona === "admin" ||
    dashboardPersona === "hr" ||
    dashboardPersona === "manager" ||
    dashboardPersona === "reader";
  const [selectedDefinitionKey, setSelectedDefinitionKey] = useState<string>("all");
  const workflowDefinitionsQuery = useStartableWorkflowDefinitions();
  const workflowDefinitions = useMemo(
    () => (supportsProcessTypeFilter ? workflowDefinitionsQuery.data ?? [] : []),
    [workflowDefinitionsQuery.data, supportsProcessTypeFilter]
  );
  const isDefinitionsLoading = supportsProcessTypeFilter ? workflowDefinitionsQuery.isLoading : false;
  const effectiveDefinitionKey =
    supportsProcessTypeFilter &&
    (selectedDefinitionKey === "all" ||
      workflowDefinitions.some((definition) => definition.definitionKey === selectedDefinitionKey))
      ? selectedDefinitionKey
      : "all";
  const workflowDefinitionKey = effectiveDefinitionKey === "all" ? null : effectiveDefinitionKey;
  const selectedWorkflowDefinition = workflowDefinitions.find((d) => d.definitionKey === workflowDefinitionKey) ?? null;
  const { insights, insightsError, isInsightsLoading, reloadInsights } = useDashboardInsights(
    dashboardPersona,
    workflowDefinitionKey,
    selectedWorkflowDefinition
  );

  const displayInsights = insights;
  const isInitialLoading = (isInsightsLoading || isDefinitionsLoading) && displayInsights === null;
  const isRefreshing = (isInsightsLoading || isDefinitionsLoading) && displayInsights !== null;

  const priorityItem = displayInsights?.queueItems[0] ?? null;
  const secondaryQueueItems = displayInsights?.queueItems.slice(1) ?? [];
  const employeeItems = displayInsights?.employeeItems ?? [];
  const meaningfulStats = useMemo(
    () => (displayInsights?.stats ?? []).filter((stat) => stat.value > 0),
    [displayInsights?.stats]
  );

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
      {isInitialLoading ? <LoadingState title="Übersicht wird geladen..." /> : null}

      {!isInitialLoading && insightsError ? (
        <EmptyState
          title="Übersichtsdaten konnten nicht geladen werden."
          description={insightsError}
          actionLabel="Erneut laden"
          onAction={() => {
            void reloadInsights();
          }}
        />
      ) : null}

      {!isInitialLoading && !insightsError && displayInsights ? (
        <div style={isRefreshing ? { opacity: 0.55, pointerEvents: "none", transition: "opacity 120ms ease" } : undefined}>
          {/* ─── Zone 1: Focus — nächster Schritt + Filter + Aktualisieren ─── */}
          <section className="panel dashboard-focus">
            <div className="dashboard-focus__head">
              <h2>{dashboardContext.title}</h2>
              <div className="dashboard-focus__controls">
                {supportsProcessTypeFilter && workflowDefinitions.length > 1 ? (
                  <label className="field compact">
                    <span>Prozesstyp</span>
                    <select
                      value={effectiveDefinitionKey}
                      onChange={(event) => setSelectedDefinitionKey(event.target.value)}
                      disabled={isInsightsLoading || isDefinitionsLoading}
                    >
                      <option value="all">Alle</option>
                      {workflowDefinitions.map((definition) => (
                        <option key={definition.definitionKey} value={definition.definitionKey}>
                          {definition.name}
                        </option>
                      ))}
                    </select>
                  </label>
                ) : null}
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => void reloadInsights()}
                  disabled={isInsightsLoading || isDefinitionsLoading}
                >
                  {isInsightsLoading ? "Aktualisiere..." : "Aktualisieren"}
                </button>
              </div>
            </div>

            {priorityItem ? (
              <Link to={priorityItem.to} className="dashboard-next-step dashboard-next-step--action">
                <div>
                  <p className="dashboard-next-step__kicker">Nächster Schritt</p>
                  <p className="dashboard-next-step__title">{displayInsights.nextStep}</p>
                  <p className="dashboard-next-step__detail">{priorityItem.title}</p>
                </div>
                <span className="dashboard-next-step__cta">{priorityItem.actionLabel}</span>
              </Link>
            ) : (
              <div className="dashboard-next-step">
                <p className="dashboard-next-step__kicker">Aktueller Stand</p>
                <p className="dashboard-next-step__title">{displayInsights.nextStep}</p>
                <p className="dashboard-next-step__detail">{displayInsights.emptyQueueText}</p>
              </div>
            )}
          </section>

          {/* ─── Zone 2: Kennzahlen — nur wenn mindestens ein Wert > 0 ─── */}
          {meaningfulStats.length > 0 ? (
            <div className="dashboard-metrics" role="region" aria-label="Kennzahlen">
              {meaningfulStats.map((stat) => (
                <article
                  key={stat.label}
                  className={`dashboard-metric${stat.tone ? ` dashboard-metric--${stat.tone}` : ""}`}
                >
                  <span className="dashboard-metric__value">{stat.value}</span>
                  <span className="dashboard-metric__label">{stat.label}</span>
                </article>
              ))}
            </div>
          ) : null}

          {/* ─── Zone 3: Offene Arbeit — Queue-Items ─── */}
          {secondaryQueueItems.length > 0 ? (
            <section className="panel panel-muted">
              <div className="panel-head">
                <h2>{displayInsights.queueTitle}</h2>
              </div>
              <ul className="dashboard-work-list">
                {secondaryQueueItems.map((item) => (
                  <li key={item.key} className="dashboard-work-item">
                    <div>
                      <p className="dashboard-work-item__title">{item.title}</p>
                      <p className="dashboard-work-item__detail">{item.detail}</p>
                    </div>
                    <Link to={item.to} className="btn btn-secondary">
                      {item.actionLabel}
                    </Link>
                  </li>
                ))}
              </ul>
            </section>
          ) : null}

          {/* ─── Zone 3: Offene Arbeit — Mitarbeitende (nur Manager) ─── */}
          {dashboardPersona === "manager" && employeeItems.length > 0 ? (
            <section className="panel panel-muted">
              <div className="panel-head">
                <h2>{displayInsights.employeeListTitle ?? "Mitarbeitende"}</h2>
                {displayInsights.employeeListDescription ? <p>{displayInsights.employeeListDescription}</p> : null}
              </div>
              <ul className="dashboard-employee-list">
                {employeeItems.map((item) => (
                  <li key={item.key} className="dashboard-employee-item">
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
                      <div className="chips-row" aria-label="Vorgangskontext">
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
                  </li>
                ))}
              </ul>
            </section>
          ) : null}

          {/* ─── Zone 4: Betriebsstatus — nur Admin ─── */}
          {dashboardPersona === "admin" ? <DashboardAdminRuntimeHealthBlock /> : null}
        </div>
      ) : null}
    </div>
  );
}
