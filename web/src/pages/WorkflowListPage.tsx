// Uebersicht ueber alle sichtbaren Onboarding-Faelle inklusive Filter und abgeleitetem Prozessstand.
import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { useCurrentUser } from "../auth/useCurrentUser";
import EmptyState from "../components/feedback/EmptyState";
import LoadingState from "../components/feedback/LoadingState";
import PageHeader from "../components/layout/PageHeader";
import { getWorkflows } from "../services/onboardingApi";
import type { WorkflowStatus, WorkflowSummary } from "../types/workflow";
import {
  getWorkflowLegacyStatusLabel,
  getWorkflowLegacyStatusPillClass,
  getWorkflowRuntimeStatusLabel,
  matchesWorkflowLegacyStatusFilter,
} from "../utils/workflowStatus";

function formatDate(value: string): string {
  if (!value) {
    return "-";
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return "-";
  }

  return parsed.toLocaleString("de-DE");
}

export default function WorkflowListPage() {
  const { capabilities } = useCurrentUser();
  const isReaderOnlyView =
    capabilities.hasReaderRole && !capabilities.hasProcessActorRole && !capabilities.canManageAdminConfiguration;
  const defaultStatusFilter: "all" | WorkflowStatus = isReaderOnlyView ? "all" : "open";

  const [rows, setRows] = useState<WorkflowSummary[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  const [search, setSearch] = useState<string>("");
  const [statusFilter, setStatusFilter] = useState<"all" | WorkflowStatus>(defaultStatusFilter);
  const [departmentFilter, setDepartmentFilter] = useState<string>("all");
  const [responsibilityFilter, setResponsibilityFilter] = useState<string>("all");

  // Die Liste laedt alle benoetigten Kartendaten direkt ueber den Listen-Endpoint.
  const reload = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const workflows = await getWorkflows();
      setRows(workflows);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Onboarding-Fälle konnten nicht geladen werden.";
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
    const unique = Array.from(new Map(rows.map((row) => [row.departmentId, row.departmentName])).entries());
    return unique.sort((left, right) => left[1].localeCompare(right[1], "de"));
  }, [rows]);

  const responsibilityOptions = useMemo(() => {
    const optionsByValue = new Map<string, { value: string; label: string }>();

    for (const workflow of rows) {
      for (const option of workflow.responsibilityOptions) {
        optionsByValue.set(option.value, option);
      }
    }

    return Array.from(optionsByValue.values()).sort((left, right) => left.label.localeCompare(right.label, "de"));
  }, [rows]);

  // Suche und Filter laufen auf den bereits geladenen Kurz- und Detaildaten.
  const filteredRows = useMemo(() => {
    const normalizedSearch = search.trim().toLowerCase();

    return rows.filter((row) => {
      const matchesStatus = matchesWorkflowLegacyStatusFilter(row.workflowStatus, statusFilter);
      const matchesDepartment = departmentFilter === "all" || String(row.departmentId) === departmentFilter;

      const matchesResponsibility = (() => {
        if (responsibilityFilter === "all") {
          return true;
        }

        return row.responsibilityOptions.some((option) => option.value === responsibilityFilter);
      })();

      if (!matchesStatus || !matchesDepartment || !matchesResponsibility) {
        return false;
      }

      if (!normalizedSearch) {
        return true;
      }

      const fullName = `${row.firstName} ${row.lastName}`.toLowerCase();
      return (
        fullName.includes(normalizedSearch) ||
        String(row.employeeNumber).includes(normalizedSearch) ||
        row.roleName.toLowerCase().includes(normalizedSearch) ||
        row.uid.toLowerCase().includes(normalizedSearch)
      );
    });
  }, [rows, search, statusFilter, departmentFilter, responsibilityFilter]);

  const emptyFilterDescription = useMemo(() => {
    if (isReaderOnlyView && statusFilter === "open") {
      return "Im Lesemodus sehen Sie nur abgeschlossene oder abgebrochene Onboardings. Stellen Sie den Status auf Alle oder einen Endstatus.";
    }

    return "Die aktuelle Filterkombination liefert keine Onboarding-Fälle.";
  }, [isReaderOnlyView, statusFilter]);

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
                ? "Im Lesemodus sehen Sie nur abgeschlossene oder abgebrochene Fälle."
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
                onChange={(event) => setStatusFilter(event.target.value as "all" | WorkflowStatus)}
              >
                <option value="all">Alle</option>
                <option value="open">Offen</option>
                <option value="completed">Abgeschlossen</option>
                <option value="cancelled">Abgebrochen</option>
              </select>
            </label>

            <label className="field compact">
              <span>Zuständiger Bereich</span>
              <select value={responsibilityFilter} onChange={(event) => setResponsibilityFilter(event.target.value)}>
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

        {!isLoading && !error && rows.length === 0 ? (
          <EmptyState
            title="Keine Onboarding-Fälle vorhanden"
            description="Aktuell sind keine Vorgänge vorhanden. Starten Sie ein neues Onboarding."
          />
        ) : null}

        {!isLoading && !error && rows.length > 0 && filteredRows.length === 0 ? (
          <EmptyState
            title="Keine Treffer"
            description={emptyFilterDescription}
          />
        ) : null}

        {!isLoading && !error && filteredRows.length > 0 ? (
          <section className="workflow-grid" aria-label="Liste Onboarding-Fälle">
            {filteredRows.map((workflow) => {
              const workflowDisplayName = `${workflow.firstName} ${workflow.lastName}`.trim();
              const responsibilities =
                workflow.responsibilityOptions.map((option) => option.label).join(", ") || "Keine Aufgaben";

              return (
                <article key={workflow.uid} className="workflow-card workflow-card-extended">
                  <div className="workflow-card-top">
                    <h3>{workflowDisplayName || "Unbekannter Name"}</h3>
                    <span className={`status-pill ${getWorkflowLegacyStatusPillClass(workflow.workflowStatus)}`}>
                      {getWorkflowLegacyStatusLabel(workflow.workflowStatus)}
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
                      <dd>{formatDate(workflow.createdAt)}</dd>
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
