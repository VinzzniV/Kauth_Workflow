// Rollenspezifisches Dashboard mit Kennzahlen und dem naechsten sinnvollen Arbeitsschritt.
// Struktur: Zone 1 (Focus/Naechster Schritt), Zone 2 (Kennzahlen), Zone 3 (Offene Arbeit).
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
  const meaningfulStats = useMemo(
    () => (insights?.stats ?? []).filter((stat) => stat.value > 0),
    [insights?.stats]
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
          {/* ─── Zone 1: Focus — nächster Schritt + Filter + Aktualisieren ─── */}
          <section className="panel dashboard-focus">
            <div className="dashboard-focus__head">
              <h2>{dashboardContext.title}</h2>
              <div className="dashboard-focus__controls">
                {supportsProcessTypeFilter && processTypes.length > 1 ? (
                  <label className="field compact">
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
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => void reloadInsights()}
                  disabled={isInsightsLoading || isProcessTypeLoading}
                >
                  {isInsightsLoading ? "Aktualisiere..." : "Aktualisieren"}
                </button>
              </div>
            </div>

            {priorityItem ? (
              <Link to={priorityItem.to} className="dashboard-next-step dashboard-next-step--action">
                <div>
                  <p className="dashboard-next-step__kicker">Nächster Schritt</p>
                  <p className="dashboard-next-step__title">{insights.nextStep}</p>
                  <p className="dashboard-next-step__detail">{priorityItem.title}</p>
                </div>
                <span className="dashboard-next-step__cta">{priorityItem.actionLabel}</span>
              </Link>
            ) : (
              <div className="dashboard-next-step">
                <p className="dashboard-next-step__kicker">Aktueller Stand</p>
                <p className="dashboard-next-step__title">{insights.nextStep}</p>
                <p className="dashboard-next-step__detail">{insights.emptyQueueText}</p>
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
                <h2>{insights.queueTitle}</h2>
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
                <h2>{insights.employeeListTitle ?? "Mitarbeitende"}</h2>
                {insights.employeeListDescription ? <p>{insights.employeeListDescription}</p> : null}
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
        </>
      ) : null}
    </div>
  );
}
