import { useCallback, useEffect, useRef, useState } from "react";
import EmptyState from "../components/feedback/EmptyState";
import LoadingState from "../components/feedback/LoadingState";
import PageHeader from "../components/layout/PageHeader";
import WorkflowCard from "../components/workflows/WorkflowCard";
import { getWorkflows } from "../services/onboardingApi";
import type { WorkflowRuntimeStatus, WorkflowSummary } from "../types/workflow";

function buildDepartmentOptions(workflows: WorkflowSummary[]): Array<[number, string]> {
  const entries = Array.from(
    new Map(workflows.map((row) => [row.departmentId, row.departmentName])).entries()
  );

  return entries.sort((left, right) => left[1].localeCompare(right[1], "de"));
}

export default function WorkflowSearchPage() {
  const [rows, setRows] = useState<WorkflowSummary[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState<string>("");
  const [departmentFilter, setDepartmentFilter] = useState<string>("all");
  const [statusFilter, setStatusFilter] = useState<"all" | WorkflowRuntimeStatus>("all");
  const [departmentOptions, setDepartmentOptions] = useState<Array<[number, string]>>([]);
  const latestReloadId = useRef(0);

  const reload = useCallback(async () => {
    const reloadId = latestReloadId.current + 1;
    latestReloadId.current = reloadId;
    setIsLoading(true);
    setError(null);

    try {
      const workflowsPromise = getWorkflows({
        status: statusFilter === "all" ? null : statusFilter,
        departmentId: departmentFilter === "all" ? null : Number(departmentFilter),
        search,
      });
      const workflowsForDepartmentsPromise =
        departmentFilter === "all"
          ? workflowsPromise
          : getWorkflows({
              status: statusFilter === "all" ? null : statusFilter,
              search,
            });
      const [workflows, workflowsForDepartments] = await Promise.all([
        workflowsPromise,
        workflowsForDepartmentsPromise,
      ]);
      if (latestReloadId.current !== reloadId) {
        return;
      }
      setRows(workflows);
      setDepartmentOptions(buildDepartmentOptions(workflowsForDepartments));
    } catch (err) {
      if (latestReloadId.current !== reloadId) {
        return;
      }
      const message = err instanceof Error ? err.message : "Onboarding-Suche konnte nicht geladen werden.";
      setError(message);
      setRows([]);
      setDepartmentOptions([]);
    } finally {
      if (latestReloadId.current === reloadId) {
        setIsLoading(false);
      }
    }
  }, [departmentFilter, search, statusFilter]);

  useEffect(() => {
    void reload();
  }, [reload]);

  const hasActiveFilters = search.trim().length > 0 || departmentFilter !== "all" || statusFilter !== "all";

  return (
    <main className="onboarding-shell">
      <div className="page-container">
        <PageHeader
          title="Onboarding suchen"
          description="Suchen Sie nach Name, Abteilung, Stelle, Personalnummer oder Onboarding-ID."
        />

        <section className="panel">
          <div className="panel-head">
            <h2>Suche und Filter</h2>
            <p>Hier finden Sie laufende und abgeschlossene Onboardings ohne die exakte ID kennen zu müssen.</p>
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
              <span>Abteilung</span>
              <select value={departmentFilter} onChange={(event) => setDepartmentFilter(event.target.value)}>
                <option value="all">Alle</option>
                {departmentOptions.map(([departmentId, departmentName]) => (
                  <option key={departmentId} value={departmentId}>
                    {departmentName}
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

        {isLoading ? <LoadingState title="Onboardings werden gesucht..." /> : null}

        {!isLoading && error ? (
          <EmptyState title="Onboarding-Suche konnte nicht geladen werden." description={error} onAction={reload} actionLabel="Erneut laden" />
        ) : null}

        {!isLoading && !error && rows.length === 0 && !hasActiveFilters ? (
          <EmptyState
            title="Keine Onboardings vorhanden"
            description="Aktuell sind keine Onboarding-Fälle vorhanden."
          />
        ) : null}

        {!isLoading && !error && rows.length === 0 && hasActiveFilters ? (
          <EmptyState
            title="Keine Treffer"
            description="Die aktuelle Suche liefert keine passenden Onboarding-Fälle."
          />
        ) : null}

        {!isLoading && !error && rows.length > 0 ? (
          <section className="workflow-grid" aria-label="Suchergebnisse Onboardings">
            {rows.map((workflow) => (
              <WorkflowCard key={workflow.uid} workflow={workflow} />
            ))}
          </section>
        ) : null}
      </div>
    </main>
  );
}
