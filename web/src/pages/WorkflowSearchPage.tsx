import { useCallback, useEffect, useRef, useState } from "react";
import EmptyState from "../components/feedback/EmptyState";
import LoadingState from "../components/feedback/LoadingState";
import PageHeader from "../components/layout/PageHeader";
import WorkflowCard from "../components/workflows/WorkflowCard";
import { getProcessTypes, getWorkflowPage } from "../services/lifecycleApi";
import type { Department, ProcessType, WorkflowRuntimeStatus, WorkflowSummary } from "../types/workflow";

const SEARCH_DEBOUNCE_MS = 400;
const SEARCH_PAGE_SIZE = 1000;

export default function WorkflowSearchPage() {
  const [rows, setRows] = useState<WorkflowSummary[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState<string>("");
  const [debouncedSearch, setDebouncedSearch] = useState<string>("");
  const [departmentFilter, setDepartmentFilter] = useState<string>("all");
  const [processTypeFilter, setProcessTypeFilter] = useState<string>("all");
  const [statusFilter, setStatusFilter] = useState<"all" | WorkflowRuntimeStatus>("all");
  const [departmentOptions, setDepartmentOptions] = useState<Department[]>([]);
  const [processTypeOptions, setProcessTypeOptions] = useState<ProcessType[]>([]);
  const latestReloadId = useRef(0);

  // Debounce search input to avoid a request on every keystroke.
  useEffect(() => {
    const timer = setTimeout(() => setDebouncedSearch(search), SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [search]);

  // Process types are static – fetch once on mount, not on every filter change.
  useEffect(() => {
    getProcessTypes()
      .then(setProcessTypeOptions)
      .catch(() => setProcessTypeOptions([]));
  }, []);

  const reload = useCallback(async () => {
    const reloadId = latestReloadId.current + 1;
    latestReloadId.current = reloadId;
    setIsLoading(true);
    setError(null);

    try {
      // Single call – backend returns departmentOptions (without dept filter applied) alongside items.
      const page = await getWorkflowPage(SEARCH_PAGE_SIZE, 0, {
        status: statusFilter === "all" ? null : statusFilter,
        departmentId: departmentFilter === "all" ? null : Number(departmentFilter),
        processTypeKey: processTypeFilter === "all" ? null : processTypeFilter,
        search: debouncedSearch,
      });
      if (latestReloadId.current !== reloadId) {
        return;
      }
      setRows(page.items);
      setDepartmentOptions(page.departmentOptions);
    } catch (err) {
      if (latestReloadId.current !== reloadId) {
        return;
      }
      const message = err instanceof Error ? err.message : "Vorgangssuche konnte nicht geladen werden.";
      setError(message);
      setRows([]);
      setDepartmentOptions([]);
    } finally {
      if (latestReloadId.current === reloadId) {
        setIsLoading(false);
      }
    }
  }, [departmentFilter, processTypeFilter, debouncedSearch, statusFilter]);

  useEffect(() => {
    void reload();
  }, [reload]);

  const hasActiveFilters =
    search.trim().length > 0 || departmentFilter !== "all" || processTypeFilter !== "all" || statusFilter !== "all";

  return (
    <main className="app-shell">
      <div className="page-container">
        <PageHeader
          title="Vorgänge suchen"
          description="Suchen Sie nach Name, Prozesstyp, Abteilung, Stelle, Personalnummer oder Workflow-ID."
        />

        <section className="panel">
          <div className="panel-head">
            <h2>Suche und Filter</h2>
            <p>Hier finden Sie laufende und abgeschlossene Mitarbeiterprozesse ohne die exakte ID kennen zu müssen.</p>
          </div>

          <div className="toolbar-row">
            <label className="field compact grow">
              <span>Suche</span>
              <input
                type="text"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="z. B. Name, Abteilung, Stelle oder ID"
              />
            </label>

            <label className="field compact">
              <span>Prozesstyp</span>
              <select value={processTypeFilter} onChange={(event) => setProcessTypeFilter(event.target.value)}>
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

            <button type="button" className="btn btn-secondary" onClick={reload}>
              Aktualisieren
            </button>
          </div>
        </section>

        {isLoading ? <LoadingState title="Vorgänge werden gesucht..." /> : null}

        {!isLoading && error ? (
          <EmptyState title="Suche konnte nicht geladen werden." description={error} onAction={reload} actionLabel="Erneut laden" />
        ) : null}

        {!isLoading && !error && rows.length === 0 && !hasActiveFilters ? (
          <EmptyState
            title="Keine Vorgänge vorhanden"
            description="Aktuell sind keine Mitarbeiterprozesse vorhanden."
          />
        ) : null}

        {!isLoading && !error && rows.length === 0 && hasActiveFilters ? (
          <EmptyState
            title="Keine Treffer"
            description="Die aktuelle Suche liefert keine passenden Vorgänge."
          />
        ) : null}

        {!isLoading && !error && rows.length > 0 ? (
          <section className="workflow-grid" aria-label="Suchergebnisse Mitarbeiterprozesse">
            {rows.map((workflow) => (
              <WorkflowCard key={workflow.uid} workflow={workflow} />
            ))}
          </section>
        ) : null}
      </div>
    </main>
  );
}
