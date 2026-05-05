import { useEffect, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import EmptyState from "../components/feedback/EmptyState";
import LoadingState from "../components/feedback/LoadingState";
import PageHeader from "../components/layout/PageHeader";
import WorkflowCard from "../components/workflows/WorkflowCard";
import type { WorkflowQueryOptions } from "../services/workflowApi";
import { useDepartments } from "../services/queries/roleQueries";
import { useStartableWorkflowDefinitions } from "../services/queries/workflowDefinitionQueries";
import { useWorkflowList } from "../services/queries/workflowQueries";
import type { Department, StartableWorkflowDefinition, WorkflowRuntimeStatus, WorkflowSummary } from "../types/workflow";

const SEARCH_DEBOUNCE_MS = 400;
const SEARCH_PAGE_SIZE = 1000;
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

export default function WorkflowSearchPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const initialSearch = searchParams.get("q") ?? "";
  const initialDepartmentFilter = searchParams.get("dept") ?? "all";
  const initialWorkflowDefinitionFilter = searchParams.get("type") ?? "all";
  const initialStatusFilter = parseWorkflowStatusFilter(searchParams.get("status"));
  const [search, setSearch] = useState<string>(initialSearch);
  const [debouncedSearch, setDebouncedSearch] = useState<string>(initialSearch);
  const [departmentFilter, setDepartmentFilter] = useState<string>(initialDepartmentFilter);
  const [workflowDefinitionFilter, setWorkflowDefinitionFilter] = useState<string>(initialWorkflowDefinitionFilter);
  const [statusFilter, setStatusFilter] = useState<"all" | WorkflowRuntimeStatus>(initialStatusFilter);

  // Debounce search input to avoid a request on every keystroke.
  useEffect(() => {
    const timer = setTimeout(() => setDebouncedSearch(search), SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [search]);

  const hasActiveFilters =
    search.trim().length > 0 || departmentFilter !== "all" || workflowDefinitionFilter !== "all" || statusFilter !== "all";
  const searchQueryOptions = useMemo<WorkflowQueryOptions>(() => ({
    status: statusFilter === "all" ? null : statusFilter,
    departmentId: departmentFilter === "all" ? null : Number(departmentFilter),
    workflowDefinitionKey: workflowDefinitionFilter === "all" ? null : workflowDefinitionFilter,
    search: debouncedSearch,
  }), [departmentFilter, debouncedSearch, workflowDefinitionFilter, statusFilter]);
  const workflowDefinitionsQuery = useStartableWorkflowDefinitions();
  const departmentsQuery = useDepartments();
  const workflowSearchQuery = useWorkflowList(searchQueryOptions, 0, SEARCH_PAGE_SIZE, hasActiveFilters);
  const workflowDefinitionOptions: StartableWorkflowDefinition[] = workflowDefinitionsQuery.data ?? [];
  const departmentOptions: Department[] = departmentsQuery.data?.items ?? [];
  const rows: WorkflowSummary[] = workflowSearchQuery.data?.items ?? [];
  const isLoading = workflowSearchQuery.isLoading;
  const isRefreshing =
    workflowSearchQuery.isFetching || workflowDefinitionsQuery.isFetching || departmentsQuery.isFetching;
  const error =
    workflowSearchQuery.error instanceof Error
      ? workflowSearchQuery.error.message
      : workflowDefinitionsQuery.error instanceof Error
        ? workflowDefinitionsQuery.error.message
        : departmentsQuery.error instanceof Error
          ? departmentsQuery.error.message
          : workflowSearchQuery.error || workflowDefinitionsQuery.error || departmentsQuery.error
            ? "Vorgangssuche konnte nicht geladen werden."
            : null;
  const hasAdvancedFilters = departmentFilter !== "all" || workflowDefinitionFilter !== "all" || statusFilter !== "all";
  const advancedFilterCount = [departmentFilter, workflowDefinitionFilter, statusFilter].filter((value) => value !== "all").length;

  useEffect(() => {
    const nextParams = new URLSearchParams();

    if (search.trim().length > 0) {
      nextParams.set("q", search);
    }

    if (departmentFilter !== "all") {
      nextParams.set("dept", departmentFilter);
    }

    if (workflowDefinitionFilter !== "all") {
      nextParams.set("type", workflowDefinitionFilter);
    }

    if (statusFilter !== "all") {
      nextParams.set("status", statusFilter);
    }

    if (nextParams.toString() !== searchParams.toString()) {
      setSearchParams(nextParams, { replace: true });
    }
  }, [departmentFilter, workflowDefinitionFilter, search, searchParams, setSearchParams, statusFilter]);

  return (
    <main className="app-shell">
      <div className="page-container">
        <PageHeader
          variant="workspace"
          title="Vorgänge suchen"
          description="Bekannte Person, Personalnummer oder Workflow-ID gezielt finden."
        />

        <section className="panel">
          <div className="panel-head">
            <h2>Suche starten</h2>
          </div>
          <div className="action-row">
            <Link className="btn btn-secondary" to="/workflows">
              Zum Überblick laufender Vorgänge
            </Link>
          </div>

          <div className="toolbar-row workflow-filter-bar">
            <label className="field compact grow">
              <span>Suche</span>
              <input
                type="text"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="z. B. Name, Abteilung, Stelle oder ID"
              />
            </label>

            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => {
                void Promise.all([
                  workflowSearchQuery.refetch(),
                  workflowDefinitionsQuery.refetch(),
                  departmentsQuery.refetch(),
                ]);
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
                <select value={workflowDefinitionFilter} onChange={(event) => setWorkflowDefinitionFilter(event.target.value)}>
                  <option value="all">Alle</option>
                  {workflowDefinitionOptions.map((definition) => (
                    <option key={definition.definitionKey} value={definition.definitionKey}>
                      {definition.name}
                    </option>
                  ))}
                </select>
              </label>

              <label className="field compact">
                <span>Abteilung</span>
                <select value={departmentFilter} onChange={(event) => setDepartmentFilter(event.target.value)}>
                  <option value="all">Alle</option>
                  {departmentOptions.map((option) => (
                    <option key={option.id} value={option.id}>
                      {option.name}
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
            </div>
          </details>
        </section>

        {isLoading ? <LoadingState title="Vorgänge werden gesucht..." /> : null}

        {!isLoading && error ? (
          <EmptyState
            title="Suche konnte nicht geladen werden."
            description={error}
            onAction={() => {
              void Promise.all([
                workflowSearchQuery.refetch(),
                workflowDefinitionsQuery.refetch(),
                departmentsQuery.refetch(),
              ]);
            }}
            actionLabel="Erneut laden"
          />
        ) : null}

        {!isLoading && !error && !hasActiveFilters ? (
          <section className="panel panel-muted">
            <div className="panel-head">
              <h2>Suche starten</h2>
              <p>Suchbegriff oder Filter setzen, dann Ergebnisse laden.</p>
            </div>
          </section>
        ) : null}

        {!isLoading && !error && hasActiveFilters && rows.length === 0 ? (
          <EmptyState
            title="Keine Treffer"
            description="Die aktuelle Suche liefert keine passenden Vorgänge."
          />
        ) : null}

        {!isLoading && !error && hasActiveFilters && rows.length > 0 ? (
          <>
            <section className="panel panel-muted">
              <p className="panel-note">
                {rows.length} Treffer in der Suche
              </p>
            </section>
            <section className="workflow-grid" aria-label="Suchergebnisse Mitarbeiterprozesse">
            {rows.map((workflow) => (
              <WorkflowCard key={workflow.uid} workflow={workflow} />
            ))}
            </section>
          </>
        ) : null}
      </div>
    </main>
  );
}
