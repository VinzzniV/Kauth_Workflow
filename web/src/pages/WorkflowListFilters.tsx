import { Link } from "react-router-dom";
import type { StartableWorkflowDefinition, WorkflowResponsibilityOption, WorkflowRuntimeStatus } from "../types/workflow";

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
  onRefresh: () => void;
  onPreviousPage: () => void;
  onNextPage: () => void;
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
  onRefresh,
  onPreviousPage,
  onNextPage,
}: WorkflowListFiltersProps) {
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
        <label className="field compact grow">
          <span>Suche im Überblick</span>
          <input
            type="text"
            value={search}
            onChange={(event) => onSearchChange(event.target.value)}
            placeholder="z. B. Name, Stelle oder ID in den laufenden Vorgängen"
          />
        </label>

        <label className="field compact">
          <span>Status</span>
          <select value={statusFilter} onChange={(event) => onStatusChange(event.target.value as "all" | WorkflowRuntimeStatus)}>
            <option value="all">Alle</option>
            <option value="draft">HR startet</option>
            <option value="waiting_for_supervisor">Wartet auf Abteilungsleitung</option>
            <option value="waiting_for_department">Fachbereiche offen</option>
            <option value="in_progress">Fachbereiche in Bearbeitung</option>
            <option value="completed">Abgeschlossen</option>
          </select>
        </label>

        <button type="button" className="btn btn-secondary" onClick={onRefresh} disabled={isRefreshing}>
          {isRefreshing ? "Aktualisiere..." : "Aktualisieren"}
        </button>
      </div>

      <details className="workflow-filter-details" open={hasAdvancedFilters}>
        <summary>Weitere Filter{advancedFilterCount > 0 ? ` (${advancedFilterCount})` : ""}</summary>
        <div className="toolbar-row toolbar-row-filters workflow-filter-bar workflow-filter-bar--details">
          <label className="field compact">
            <span>Prozesstyp</span>
            <select value={workflowDefinitionFilter} onChange={(event) => onWorkflowDefinitionChange(event.target.value)}>
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
            <select value={departmentFilter} onChange={(event) => onDepartmentChange(event.target.value)}>
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
            <select value={responsibilityFilter} onChange={(event) => onResponsibilityChange(event.target.value)}>
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
          onClick={onPreviousPage}
          disabled={pageIndex === 0 || isRefreshing}
        >
          Vorherige Seite
        </button>
        <button
          type="button"
          className="btn btn-secondary"
          onClick={onNextPage}
          disabled={isRefreshing || pageIndex + 1 >= totalPages}
        >
          Nächste Seite
        </button>
      </div>
    </section>
  );
}
