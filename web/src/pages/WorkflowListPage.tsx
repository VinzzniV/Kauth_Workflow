// Uebersicht ueber alle sichtbaren Vorgaenge inklusive Filter und abgeleitetem Prozessstand.
import { useEffect, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import EmptyState from "../components/feedback/EmptyState";
import SkeletonCard from "../components/feedback/SkeletonCard";
import PageHeader from "../components/layout/PageHeader";
import type { WorkflowQueryOptions } from "../services/workflowApi";
import { useProcessTypes } from "../services/queries/processTypeQueries";
import { useWorkflowList } from "../services/queries/workflowQueries";

const SEARCH_DEBOUNCE_MS = 400;
import type {
  ProcessType,
  WorkflowResponsibilityOption,
  WorkflowRuntimeStatus,
  WorkflowSummary,
} from "../types/workflow";
import { formatDate } from "../utils/dateFormat";
import {
  getWorkflowRuntimeStatusLabel,
  getWorkflowRuntimeStatusPillClass,
} from "../utils/workflowStatus";

const PAGE_SIZE = 20;
const WORKFLOW_STATUS_FILTERS: Array<"all" | WorkflowRuntimeStatus> = [
  "all",
  "draft",
  "waiting_for_supervisor",
  "waiting_for_department",
  "in_progress",
  "completed",
];

function parseWorkflowStatusFilter(value: string | null): "all" | WorkflowRuntimeStatus {
  return WORKFLOW_STATUS_FILTERS.includes(value as "all" | WorkflowRuntimeStatus)
    ? (value as "all" | WorkflowRuntimeStatus)
    : "all";
}

function parsePageIndex(value: string | null): number {
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed < 2) {
    return 0;
  }

  return Math.floor(parsed) - 1;
}

export default function WorkflowListPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const initialSearch = searchParams.get("q") ?? "";
  const initialStatusFilter = parseWorkflowStatusFilter(searchParams.get("status"));
  const initialDepartmentFilter = searchParams.get("dept") ?? "all";
  const initialProcessTypeFilter = searchParams.get("type") ?? "all";
  const initialResponsibilityFilter = searchParams.get("resp") ?? "all";
  const initialPageIndex = parsePageIndex(searchParams.get("page"));

  const [pageIndex, setPageIndex] = useState<number>(initialPageIndex);

  const [search, setSearch] = useState<string>(initialSearch);
  const [debouncedSearch, setDebouncedSearch] = useState<string>(initialSearch);
  const [statusFilter, setStatusFilter] = useState<"all" | WorkflowRuntimeStatus>(initialStatusFilter);
  const [departmentFilter, setDepartmentFilter] = useState<string>(initialDepartmentFilter);
  const [processTypeFilter, setProcessTypeFilter] = useState<string>(initialProcessTypeFilter);
  const [responsibilityFilter, setResponsibilityFilter] = useState<string>(initialResponsibilityFilter);

  // Debounce search input to avoid a request on every keystroke.
  useEffect(() => {
    const timer = setTimeout(() => setDebouncedSearch(search), SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    const nextParams = new URLSearchParams();

    if (search.trim().length > 0) {
      nextParams.set("q", search);
    }

    if (statusFilter !== "all") {
      nextParams.set("status", statusFilter);
    }

    if (departmentFilter !== "all") {
      nextParams.set("dept", departmentFilter);
    }

    if (processTypeFilter !== "all") {
      nextParams.set("type", processTypeFilter);
    }

    if (responsibilityFilter !== "all") {
      nextParams.set("resp", responsibilityFilter);
    }

    if (pageIndex > 0) {
      nextParams.set("page", String(pageIndex + 1));
    }

    if (nextParams.toString() !== searchParams.toString()) {
      setSearchParams(nextParams, { replace: true });
    }
  }, [
    departmentFilter,
    pageIndex,
    processTypeFilter,
    responsibilityFilter,
    search,
    searchParams,
    setSearchParams,
    statusFilter,
  ]);

  // Die Liste laedt alle benoetigten Kartendaten direkt ueber den Listen-Endpoint.
  const pageQuery = useMemo<WorkflowQueryOptions>(() => ({
    status: statusFilter === "all" ? null : statusFilter,
    departmentId: departmentFilter === "all" ? null : Number(departmentFilter),
    processTypeKey: processTypeFilter === "all" ? null : processTypeFilter,
    search: debouncedSearch,
    responsibilityValue: responsibilityFilter === "all" ? null : responsibilityFilter,
  }), [departmentFilter, debouncedSearch, processTypeFilter, responsibilityFilter, statusFilter]);
  const processTypesQuery = useProcessTypes();
  const workflowListQuery = useWorkflowList(pageQuery, pageIndex, PAGE_SIZE);
  const processTypeOptions: ProcessType[] = processTypesQuery.data ?? [];
  const rows: WorkflowSummary[] = workflowListQuery.data?.items ?? [];
  const totalCount = workflowListQuery.data?.count ?? 0;
  const departmentOptions: Array<[number, string]> =
    workflowListQuery.data?.departmentOptions.map((option) => [option.id, option.name]) ?? [];
  const responsibilityOptions: WorkflowResponsibilityOption[] =
    workflowListQuery.data?.responsibilityOptions ?? [];
  const isLoading = workflowListQuery.isLoading;
  const isRefreshing = workflowListQuery.isFetching || processTypesQuery.isFetching;
  const error =
    workflowListQuery.error instanceof Error
      ? workflowListQuery.error.message
      : processTypesQuery.error instanceof Error
        ? processTypesQuery.error.message
        : workflowListQuery.error || processTypesQuery.error
          ? "Vorgänge konnten nicht geladen werden."
          : null;

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
      processTypeFilter !== "all" ||
      responsibilityFilter !== "all"
    );
  }, [departmentFilter, processTypeFilter, responsibilityFilter, search, statusFilter]);
  const hasAdvancedFilters = departmentFilter !== "all" || processTypeFilter !== "all" || responsibilityFilter !== "all";
  const advancedFilterCount = [departmentFilter, processTypeFilter, responsibilityFilter].filter((value) => value !== "all").length;

  return (
    <main className="app-shell">
      <div className="page-container">
        <PageHeader title="Laufende Vorgänge" />

        <section className="panel">
          <div className="panel-head">
            <h2>Laufende Vorgänge filtern</h2>
          </div>
          <div className="action-row">
            <Link className="btn btn-secondary" to="/search">
              Zur gezielten Suche
            </Link>
          </div>

          <div className="toolbar-row toolbar-row-filters workflow-filter-bar">
            <label className="field compact grow">
              <span>Suche im Überblick</span>
              <input
                type="text"
                value={search}
                onChange={(event) => {
                  setSearch(event.target.value);
                  setPageIndex(0);
                }}
                placeholder="z. B. Name, Stelle oder ID in den laufenden Vorgängen"
              />
            </label>

            <label className="field compact">
              <span>Status</span>
              <select
                value={statusFilter}
                onChange={(event) => {
                  setStatusFilter(event.target.value as "all" | WorkflowRuntimeStatus);
                  setPageIndex(0);
                }}
              >
                <option value="all">Alle</option>
                <option value="draft">HR startet</option>
                <option value="waiting_for_supervisor">Wartet auf Abteilungsleitung</option>
                <option value="waiting_for_department">Fachbereiche offen</option>
                <option value="in_progress">Fachbereiche in Bearbeitung</option>
                <option value="completed">Abgeschlossen</option>
              </select>
            </label>

            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => {
                void Promise.all([workflowListQuery.refetch(), processTypesQuery.refetch()]);
              }}
              disabled={isRefreshing}
            >
              {isRefreshing ? "Aktualisiere..." : "Aktualisieren"}
            </button>
          </div>

          <details className="workflow-filter-details" open={hasAdvancedFilters}>
            <summary>
              Weitere Filter{advancedFilterCount > 0 ? ` (${advancedFilterCount})` : ""}
            </summary>
            <div className="toolbar-row toolbar-row-filters workflow-filter-bar workflow-filter-bar--details">
              <label className="field compact">
              <span>Prozesstyp</span>
              <select
                value={processTypeFilter}
                onChange={(event) => {
                  setProcessTypeFilter(event.target.value);
                  setPageIndex(0);
                }}
              >
                <option value="all">Alle</option>
                {processTypeOptions.map((processType) => (
                  <option key={processType.key} value={processType.key}>
                    {processType.name}
                  </option>
                ))}
              </select>
            </label>

            <label className="field compact">
              <span>Abteilung</span>
              <select
                value={departmentFilter}
                onChange={(event) => {
                  setDepartmentFilter(event.target.value);
                  setPageIndex(0);
                }}
              >
                <option value="all">Alle</option>
                {departmentOptions.map(([id, name]) => (
                  <option key={id} value={id}>
                    {name}
                  </option>
                ))}
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
            </div>
          </details>
          <div className="toolbar-row">
            <p className="panel-note">
              Seite {pageIndex + 1} von {totalPages} · {totalCount} Einträge
            </p>
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => setPageIndex((current) => Math.max(0, current - 1))}
              disabled={pageIndex === 0 || isRefreshing}
            >
              Vorherige Seite
            </button>
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => setPageIndex((current) => current + 1)}
              disabled={isRefreshing || pageIndex + 1 >= totalPages}
            >
              Nächste Seite
            </button>
          </div>
        </section>

        {isLoading ? (
          <section className="workflow-grid" aria-label="Vorgänge werden geladen">
            {Array.from({ length: 8 }, (_, index) => (
              <SkeletonCard key={`workflow-skeleton-${index}`} variant="workflow" />
            ))}
          </section>
        ) : null}

        {!isLoading && error ? (
          <EmptyState
            title="Vorgänge konnten nicht geladen werden."
            description={error}
            actionLabel="Erneut versuchen"
            onAction={() => {
              void Promise.all([workflowListQuery.refetch(), processTypesQuery.refetch()]);
            }}
          />
        ) : null}

        {!isLoading && !error && rows.length === 0 && !hasActiveFilters ? (
          <EmptyState
            title="Keine laufenden Vorgänge vorhanden"
            description="Aktuell sind keine laufenden Vorgänge vorhanden."
          />
        ) : null}

        {!isLoading && !error && rows.length === 0 && hasActiveFilters ? (
          <EmptyState
            title="Keine Treffer"
            description="Die aktuelle Filterkombination liefert keine passenden Vorgänge."
          />
        ) : null}

        {!isLoading && !error && rows.length > 0 ? (
          <section className="workflow-grid" aria-label="Liste Mitarbeiterprozesse">
            {rows.map((workflow) => {
              const workflowDisplayName = `${workflow.firstName} ${workflow.lastName}`.trim();

              return (
                <article key={workflow.uid} className="workflow-card workflow-card-extended card-list">
                  <div className="workflow-card-top">
                    <div>
                      <h3>{workflowDisplayName || "Unbekannter Name"}</h3>
                      <div className="chips-row" aria-label="Prozesstyp">
                        <span className="chip">{workflow.processType.name}</span>
                      </div>
                    </div>
                    <span className={`status-pill ${getWorkflowRuntimeStatusPillClass(workflow.workflowStatus)}`}>
                      {getWorkflowRuntimeStatusLabel(workflow.workflowStatus)}
                    </span>
                  </div>

                  <dl className="workflow-meta">
                    <div>
                      <dt>Stelle</dt>
                      <dd>{workflow.roleName}</dd>
                    </div>
                    <div>
                      <dt>Abteilung</dt>
                      <dd>{workflow.departmentName}</dd>
                    </div>
                    <div>
                      <dt>Personalnummer</dt>
                      <dd>{workflow.employeeNumber}</dd>
                    </div>
                    <div>
                      <dt>Deadline</dt>
                      <dd>{formatDate(workflow.deadlineDate)}</dd>
                    </div>
                  </dl>

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
