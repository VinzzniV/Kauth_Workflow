// Uebersicht ueber alle sichtbaren Onboarding-Faelle inklusive Filter und abgeleitetem Prozessstand.
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useCurrentUser } from "../auth/useCurrentUser";
import EmptyState from "../components/feedback/EmptyState";
import LoadingState from "../components/feedback/LoadingState";
import PageHeader from "../components/layout/PageHeader";
import { getWorkflowPage } from "../services/onboardingApi";
import type {
  WorkflowResponsibilityOption,
  WorkflowRuntimeStatus,
  WorkflowSummary,
} from "../types/workflow";
import { formatDate, formatDateTime } from "../utils/dateFormat";
import {
  getWorkflowRuntimeStatusLabel,
  getWorkflowRuntimeStatusPillClass,
} from "../utils/workflowStatus";

const PAGE_SIZE = 20;

export default function WorkflowListPage() {
  const { capabilities } = useCurrentUser();
  const isReaderOnlyView =
    capabilities.hasReaderRole && !capabilities.hasProcessActorRole && !capabilities.canManageAdminConfiguration;
  const defaultStatusFilter: "all" | WorkflowRuntimeStatus = "all";

  const [rows, setRows] = useState<WorkflowSummary[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [totalCount, setTotalCount] = useState<number>(0);
  const [pageIndex, setPageIndex] = useState<number>(0);
  const [departmentOptions, setDepartmentOptions] = useState<Array<[number, string]>>([]);
  const [responsibilityOptions, setResponsibilityOptions] = useState<WorkflowResponsibilityOption[]>([]);
  const latestReloadId = useRef(0);

  const [search, setSearch] = useState<string>("");
  const [statusFilter, setStatusFilter] = useState<"all" | WorkflowRuntimeStatus>(defaultStatusFilter);
  const [departmentFilter, setDepartmentFilter] = useState<string>("all");
  const [responsibilityFilter, setResponsibilityFilter] = useState<string>("all");

  // Die Liste laedt alle benoetigten Kartendaten direkt ueber den Listen-Endpoint.
  const reload = useCallback(async () => {
    const reloadId = latestReloadId.current + 1;
    latestReloadId.current = reloadId;
    setIsLoading(true);
    setError(null);

    try {
      const pageQuery = {
        status: statusFilter === "all" ? null : statusFilter,
        departmentId: departmentFilter === "all" ? null : Number(departmentFilter),
        search,
        responsibilityValue: responsibilityFilter === "all" ? null : responsibilityFilter,
      };
      const page = await getWorkflowPage(PAGE_SIZE, pageIndex * PAGE_SIZE, pageQuery);
      if (latestReloadId.current !== reloadId) {
        return;
      }
      setRows(page.items);
      setTotalCount(page.count);
      setDepartmentOptions(page.departmentOptions.map((option) => [option.id, option.name]));
      setResponsibilityOptions(page.responsibilityOptions);
    } catch (err) {
      if (latestReloadId.current !== reloadId) {
        return;
      }
      const message = err instanceof Error ? err.message : "Onboarding-Fälle konnten nicht geladen werden.";
      setError(message);
      setRows([]);
      setTotalCount(0);
      setDepartmentOptions([]);
      setResponsibilityOptions([]);
    } finally {
      if (latestReloadId.current === reloadId) {
        setIsLoading(false);
      }
    }
  }, [departmentFilter, pageIndex, responsibilityFilter, search, statusFilter]);

  useEffect(() => {
    void reload();
  }, [reload]);

  useEffect(() => {
    setPageIndex(0);
  }, [departmentFilter, search, statusFilter]);

  const totalPages = useMemo(() => {
    if (totalCount <= 0) {
      return 1;
    }

    return Math.ceil(totalCount / PAGE_SIZE);
  }, [totalCount]);

  const hasActiveFilters = useMemo(() => {
    return (
      search.trim().length > 0 ||
      statusFilter !== "all" ||
      departmentFilter !== "all" ||
      responsibilityFilter !== "all"
    );
  }, [departmentFilter, responsibilityFilter, search, statusFilter]);

  return (
    <main className="onboarding-shell">
      <div className="page-container">
        <PageHeader
          title="Onboardings im Überblick"
          description="Zentrale Übersicht über alle für Sie sichtbaren Onboardings."
        />

        <section className="panel">
          <div className="panel-head">
            <h2>Filter</h2>
            <p>Filtern Sie nach Abteilung, Stand und zuständigem Bereich.</p>
          </div>
          <div className="next-action-callout">
            <p className="next-action-label">Nächste nötige Aktion</p>
            <p className="next-action-text">
              {isReaderOnlyView
                ? "Im Lesemodus sehen Sie freigegebene Workflow-Stände auf Basis des Backend-Runtime-Status."
                : "Prüfen Sie zuerst Fälle, die auf Abteilungsleitung oder Fachbereiche warten."}
            </p>
          </div>

          <div className="toolbar-row">
            <label className="field compact grow">
              <span>Suche</span>
              <input
                type="text"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="z. B. Name, Stelle oder ID"
              />
            </label>

            <label className="field compact">
              <span>Abteilung</span>
              <select value={departmentFilter} onChange={(event) => setDepartmentFilter(event.target.value)}>
                <option value="all">Alle</option>
                {departmentOptions.map(([id, name]) => (
                  <option key={id} value={id}>
                    {name}
                  </option>
                ))}
              </select>
            </label>

            <label className="field compact">
              <span>Status</span>
              <select
                value={statusFilter}
                onChange={(event) => setStatusFilter(event.target.value as "all" | WorkflowRuntimeStatus)}
              >
                <option value="all">Alle</option>
                <option value="draft">HR startet</option>
                <option value="waiting_for_supervisor">Wartet auf Abteilungsleitung</option>
                <option value="waiting_for_department">Fachbereiche offen</option>
                <option value="in_progress">Fachbereiche in Bearbeitung</option>
                <option value="completed">Abgeschlossen</option>
              </select>
            </label>

            <label className="field compact">
              <span>Zuständiger Bereich</span>
              <select
                value={responsibilityFilter}
                onChange={(event) => {
                  setResponsibilityFilter(event.target.value);
                  setPageIndex(0);
                }}
              >
                <option value="all">Alle</option>
                {responsibilityOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </label>

            <button type="button" className="btn btn-secondary" onClick={reload}>
              Aktualisieren
            </button>
          </div>
          <div className="toolbar-row">
            <p className="panel-note">
              Seite {pageIndex + 1} von {totalPages} · {totalCount} Workflow{totalCount === 1 ? "" : "s"}
            </p>
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => setPageIndex((current) => Math.max(0, current - 1))}
              disabled={pageIndex === 0 || isLoading}
            >
              Vorherige Seite
            </button>
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => setPageIndex((current) => current + 1)}
              disabled={isLoading || pageIndex + 1 >= totalPages}
            >
              Nächste Seite
            </button>
          </div>
        </section>

        {isLoading ? <LoadingState title="Onboarding-Fälle werden geladen..." /> : null}

        {!isLoading && error ? (
          <EmptyState
            title="Onboarding-Fälle konnten nicht geladen werden."
            description={error}
            actionLabel="Erneut versuchen"
            onAction={reload}
          />
        ) : null}

        {!isLoading && !error && rows.length === 0 && !hasActiveFilters ? (
          <EmptyState
            title="Keine Onboarding-Fälle vorhanden"
            description="Aktuell sind keine Vorgänge vorhanden. Starten Sie ein neues Onboarding."
          />
        ) : null}

        {!isLoading && !error && rows.length === 0 && hasActiveFilters ? (
          <EmptyState
            title="Keine Treffer"
            description="Die aktuelle Filterkombination liefert keine Onboarding-Fälle."
          />
        ) : null}

        {!isLoading && !error && rows.length > 0 ? (
          <section className="workflow-grid" aria-label="Liste Onboarding-Fälle">
            {rows.map((workflow) => {
              const workflowDisplayName = `${workflow.firstName} ${workflow.lastName}`.trim();
              const responsibilities =
                workflow.responsibilityOptions.map((option) => option.label).join(", ") || "Keine Aufgaben";

              return (
                <article key={workflow.uid} className="workflow-card workflow-card-extended">
                  <div className="workflow-card-top">
                    <h3>{workflowDisplayName || "Unbekannter Name"}</h3>
                    <span className={`status-pill ${getWorkflowRuntimeStatusPillClass(workflow.workflowStatus)}`}>
                      {getWorkflowRuntimeStatusLabel(workflow.workflowStatus)}
                    </span>
                  </div>

                  <dl className="workflow-meta">
                    <div>
                      <dt>Personalnummer</dt>
                      <dd>{workflow.employeeNumber}</dd>
                    </div>
                    <div>
                      <dt>Stelle</dt>
                      <dd>{workflow.roleName}</dd>
                    </div>
                    <div>
                      <dt>Abteilung</dt>
                      <dd>{workflow.departmentName}</dd>
                    </div>
                    <div>
                      <dt>Onboarding-ID</dt>
                      <dd className="uid-value">{workflow.uid}</dd>
                    </div>
                    <div>
                      <dt>Aktueller Stand</dt>
                      <dd>{getWorkflowRuntimeStatusLabel(workflow.workflowStatus)}</dd>
                    </div>
                    <div>
                      <dt>Erstellt</dt>
                      <dd>{formatDateTime(workflow.createdAt)}</dd>
                    </div>
                    <div>
                      <dt>Deadline</dt>
                      <dd>{formatDate(workflow.deadlineDate)}</dd>
                    </div>
                  </dl>

                  <p className="panel-note">Aufgaben: {workflow.taskSummary}</p>
                  <p className="panel-note">Zuständigkeiten: {responsibilities}</p>

                  <div className="action-row">
                    <Link className="btn btn-secondary" to={`/workflows/${workflow.uid}`}>
                      Öffnen
                    </Link>
                  </div>
                </article>
              );
            })}
          </section>
        ) : null}
      </div>
    </main>
  );
}
