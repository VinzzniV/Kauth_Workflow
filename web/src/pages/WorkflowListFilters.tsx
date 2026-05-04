import { useId } from "react";
import { Link } from "react-router-dom";
import type { StartableWorkflowDefinition, WorkflowResponsibilityOption, WorkflowRuntimeStatus } from "../types/workflow";

function FilterChip({ label, value }: { label: string; value: string }) {
  return (
    <span className="filter-chip">
      <strong>{label}</strong>
      <span>{value}</span>
    </span>
  );
}

type WorkflowListFiltersProps = {
  search: string;
  statusFilter: "all" | WorkflowRuntimeStatus;
  departmentFilter: string;
  workflowDefinitionFilter: string;
  responsibilityFilter: string;
  workflowDefinitionOptions: StartableWorkflowDefinition[];
  departmentOptions: Array<[number, string]>;
  responsibilityOptions: WorkflowResponsibilityOption[];
  hasAdvancedFilters: boolean;
  advancedFilterCount: number;
  pageIndex: number;
  totalPages: number;
  totalCount: number;
  isRefreshing: boolean;
  onSearchChange: (value: string) => void;
  onStatusChange: (value: "all" | WorkflowRuntimeStatus) => void;
  onDepartmentChange: (value: string) => void;
  onWorkflowDefinitionChange: (value: string) => void;
  onResponsibilityChange: (value: string) => void;
  onResetFilters: () => void;
  onRefresh: () => void;
  onPreviousPage: () => void;
  onNextPage: () => void;
  onPageIndexChange: (next: number) => void;
};

export function WorkflowListFilters({
  search,
  statusFilter,
  departmentFilter,
  workflowDefinitionFilter,
  responsibilityFilter,
  workflowDefinitionOptions,
  departmentOptions,
  responsibilityOptions,
  hasAdvancedFilters,
  advancedFilterCount,
  pageIndex,
  totalPages,
  totalCount,
  isRefreshing,
  onSearchChange,
  onStatusChange,
  onDepartmentChange,
  onWorkflowDefinitionChange,
  onResponsibilityChange,
  onResetFilters,
  onRefresh,
  onPreviousPage,
  onNextPage,
  onPageIndexChange,
}: WorkflowListFiltersProps) {
  // FE-5: explicit htmlFor/id paaren Label und Input — robuster fuer Screen-Reader
  // als die implicit-Association via verschachteltem <label>.
  const searchId = useId();
  const statusId = useId();
  const definitionId = useId();
  const departmentId = useId();
  const responsibilityId = useId();
  const pageSelectId = useId();
  const selectedDefinitionLabel =
    workflowDefinitionOptions.find((definition) => definition.definitionKey === workflowDefinitionFilter)?.name ?? null;
  const selectedDepartmentLabel =
    departmentOptions.find(([id]) => String(id) === departmentFilter)?.[1] ?? null;
  const selectedResponsibilityLabel =
    responsibilityOptions.find((option) => option.value === responsibilityFilter)?.label ?? null;
  const statusLabelMap: Record<WorkflowListFiltersProps["statusFilter"], string> = {
    all: "Alle",
    draft: "HR startet",
    waiting_for_supervisor: "Wartet auf Abteilungsleitung",
    waiting_for_department: "Fachbereiche offen",
    in_progress: "Fachbereiche in Bearbeitung",
    completed: "Abgeschlossen",
  };
  const hasActiveFilters =
    search.trim().length > 0 ||
    statusFilter !== "all" ||
    workflowDefinitionFilter !== "all" ||
    departmentFilter !== "all" ||
    responsibilityFilter !== "all";

  return (
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
        <div className="field compact grow">
          <label htmlFor={searchId}>Suche im Überblick</label>
          <input
            id={searchId}
            type="text"
            value={search}
            onChange={(event) => onSearchChange(event.target.value)}
            placeholder="z. B. Name, Stelle oder ID in den laufenden Vorgängen"
          />
        </div>

        <div className="field compact">
          <label htmlFor={statusId}>Status</label>
          <select
            id={statusId}
            value={statusFilter}
            onChange={(event) => onStatusChange(event.target.value as "all" | WorkflowRuntimeStatus)}
          >
            <option value="all">Alle</option>
            <option value="draft">HR startet</option>
            <option value="waiting_for_supervisor">Wartet auf Abteilungsleitung</option>
            <option value="waiting_for_department">Fachbereiche offen</option>
            <option value="in_progress">Fachbereiche in Bearbeitung</option>
            <option value="completed">Abgeschlossen</option>
          </select>
        </div>

        <button type="button" className="btn btn-secondary" onClick={onRefresh} disabled={isRefreshing}>
          {isRefreshing ? "Aktualisiere..." : "Aktualisieren"}
        </button>
      </div>

      <details className="workflow-filter-details" open={hasAdvancedFilters}>
        <summary>Weitere Filter{advancedFilterCount > 0 ? ` (${advancedFilterCount})` : ""}</summary>
        <div className="toolbar-row toolbar-row-filters workflow-filter-bar workflow-filter-bar--details">
          <div className="field compact">
            <label htmlFor={definitionId}>Prozesstyp</label>
            <select
              id={definitionId}
              value={workflowDefinitionFilter}
              onChange={(event) => onWorkflowDefinitionChange(event.target.value)}
            >
              <option value="all">Alle</option>
              {workflowDefinitionOptions.map((definition) => (
                <option key={definition.definitionKey} value={definition.definitionKey}>
                  {definition.name}
                </option>
              ))}
            </select>
          </div>

          <div className="field compact">
            <label htmlFor={departmentId}>Abteilung</label>
            <select
              id={departmentId}
              value={departmentFilter}
              onChange={(event) => onDepartmentChange(event.target.value)}
            >
              <option value="all">Alle</option>
              {departmentOptions.map(([id, name]) => (
                <option key={id} value={id}>
                  {name}
                </option>
              ))}
            </select>
          </div>

          <div className="field compact">
            <label htmlFor={responsibilityId}>Zuständiger Bereich</label>
            <select
              id={responsibilityId}
              value={responsibilityFilter}
              onChange={(event) => onResponsibilityChange(event.target.value)}
            >
              <option value="all">Alle</option>
              {responsibilityOptions.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </div>
        </div>
      </details>

      <div className="filter-panel-actions" aria-live="polite">
        <div className="filter-chip-row" aria-label="Aktive Filter">
          {search.trim().length > 0 ? <FilterChip label="Suche" value={search.trim()} /> : null}
          {statusFilter !== "all" ? <FilterChip label="Status" value={statusLabelMap[statusFilter]} /> : null}
          {selectedDefinitionLabel ? <FilterChip label="Prozesstyp" value={selectedDefinitionLabel} /> : null}
          {selectedDepartmentLabel ? <FilterChip label="Abteilung" value={selectedDepartmentLabel} /> : null}
          {selectedResponsibilityLabel ? <FilterChip label="Bereich" value={selectedResponsibilityLabel} /> : null}
          {!hasActiveFilters ? <span className="filter-chip-empty">Keine aktiven Filter.</span> : null}
        </div>
        <button type="button" className="btn btn-ghost" onClick={onResetFilters} disabled={!hasActiveFilters}>
          Filter zurücksetzen
        </button>
      </div>

      <div className="toolbar-row workflow-filter-pagination">
        <p className="panel-note">
          {totalCount} Einträge insgesamt
        </p>
        <button
          type="button"
          className="btn btn-secondary"
          onClick={onPreviousPage}
          disabled={pageIndex === 0 || isRefreshing}
          aria-label="Vorherige Seite"
        >
          ← Zurück
        </button>
        <div className="field compact">
          <label htmlFor={pageSelectId}>Seite</label>
          <select
            id={pageSelectId}
            value={pageIndex}
            onChange={(event) => onPageIndexChange(Number(event.target.value))}
            disabled={isRefreshing || totalPages <= 1}
          >
            {Array.from({ length: Math.max(totalPages, 1) }, (_, idx) => (
              <option key={idx} value={idx}>
                {idx + 1} von {totalPages || 1}
              </option>
            ))}
          </select>
        </div>
        <button
          type="button"
          className="btn btn-secondary"
          onClick={onNextPage}
          disabled={isRefreshing || pageIndex + 1 >= totalPages}
          aria-label="Nächste Seite"
        >
          Weiter →
        </button>
      </div>
    </section>
  );
}
