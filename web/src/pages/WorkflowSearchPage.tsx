import { useCallback, useEffect, useMemo, useState } from "react";
import EmptyState from "../components/feedback/EmptyState";
import LoadingState from "../components/feedback/LoadingState";
import PageHeader from "../components/layout/PageHeader";
import WorkflowCard from "../components/workflows/WorkflowCard";
import { getWorkflows } from "../services/onboardingApi";
import type { WorkflowStatus, WorkflowSummary } from "../types/workflow";
import { matchesWorkflowLegacyStatusFilter } from "../utils/workflowStatus";

export default function WorkflowSearchPage() {
  const [rows, setRows] = useState<WorkflowSummary[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState<string>("");
  const [departmentFilter, setDepartmentFilter] = useState<string>("all");
  const [statusFilter, setStatusFilter] = useState<"all" | WorkflowStatus>("all");

  const reload = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const workflows = await getWorkflows();
      setRows(workflows);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Onboarding-Suche konnte nicht geladen werden.";
      setError(message);
      setRows([]);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void reload();
  }, [reload]);

  const departmentOptions = useMemo(() => {
    const entries = Array.from(
      new Map(rows.map((row) => [row.departmentId, row.departmentName])).entries()
    );

    return entries.sort((left, right) => left[1].localeCompare(right[1], "de"));
  }, [rows]);

  const filteredRows = useMemo(() => {
    const normalizedSearch = search.trim().toLowerCase();

    return rows.filter((row) => {
      const matchesDepartment =
        departmentFilter === "all" || String(row.departmentId) === departmentFilter;
      const matchesStatus = matchesWorkflowLegacyStatusFilter(row.status, statusFilter, row.workflowStatus);

      if (!matchesDepartment || !matchesStatus) {
        return false;
      }

      if (!normalizedSearch) {
        return true;
      }

      const fullName = `${row.firstName} ${row.lastName}`.trim().toLowerCase();
      return (
        fullName.includes(normalizedSearch) ||
        row.departmentName.toLowerCase().includes(normalizedSearch) ||
        row.roleName.toLowerCase().includes(normalizedSearch) ||
        String(row.employeeNumber).includes(normalizedSearch) ||
        row.uid.toLowerCase().includes(normalizedSearch)
      );
    });
  }, [rows, search, departmentFilter, statusFilter]);

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
                onChange={(event) => setStatusFilter(event.target.value as "all" | WorkflowStatus)}
              >
                <option value="all">Alle</option>
                <option value="open">Offen</option>
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

        {!isLoading && !error && rows.length === 0 ? (
          <EmptyState
            title="Keine Onboardings vorhanden"
            description="Aktuell sind keine Onboarding-Fälle vorhanden."
          />
        ) : null}

        {!isLoading && !error && rows.length > 0 && filteredRows.length === 0 ? (
          <EmptyState
            title="Keine Treffer"
            description="Die aktuelle Suche liefert keine passenden Onboarding-Fälle."
          />
        ) : null}

        {!isLoading && !error && filteredRows.length > 0 ? (
          <section className="workflow-grid" aria-label="Suchergebnisse Onboardings">
            {filteredRows.map((workflow) => (
              <WorkflowCard key={workflow.uid} workflow={workflow} />
            ))}
          </section>
        ) : null}
      </div>
    </main>
  );
}
