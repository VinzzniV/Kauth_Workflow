import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import EmptyState from "../components/feedback/EmptyState";
import SkeletonCard from "../components/feedback/SkeletonCard";
import ViewModeToggle, { type ViewMode } from "../components/layout/ViewModeToggle";
import type { WorkflowSummary } from "../types/workflow";
import { formatDate } from "../utils/dateFormat";
import { getWorkflowRuntimeStatusLabel, getWorkflowRuntimeStatusPillClass } from "../utils/workflowStatus";

type WorkflowListResultsProps = {
  rows: WorkflowSummary[];
  isLoading: boolean;
  error: string | null;
  hasActiveFilters: boolean;
  onRetry: () => void;
};

type WorkflowSortKey = "name" | "status" | "department" | "deadline" | "created";
type SortDirection = "asc" | "desc";

function getWorkflowDisplayName(workflow: WorkflowSummary): string {
  return `${workflow.firstName} ${workflow.lastName}`.trim() || "Unbekannter Name";
}

function compareText(left: string, right: string): number {
  return left.localeCompare(right, "de", { sensitivity: "base" });
}

function compareNullableDate(left: string | null, right: string | null): number {
  const leftTime = left ? new Date(left).getTime() : Number.MAX_SAFE_INTEGER;
  const rightTime = right ? new Date(right).getTime() : Number.MAX_SAFE_INTEGER;
  return leftTime - rightTime;
}

function WorkflowCard({
  workflow,
  isSelected,
  onPreview,
}: {
  workflow: WorkflowSummary;
  isSelected: boolean;
  onPreview: (workflowUid: string) => void;
}) {
  const workflowDisplayName = getWorkflowDisplayName(workflow);
  return (
    <article
      key={workflow.uid}
      className={`workflow-card workflow-card-extended card-list${isSelected ? " workflow-card--active" : ""}`}
    >
      <div className="workflow-card-top">
        <div>
          <h3>{workflowDisplayName}</h3>
          <div className="chips-row" aria-label="Prozesstyp">
            <span className="chip">{workflow.workflowDefinition.name}</span>
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
        <button
          type="button"
          className="btn btn-secondary"
          aria-pressed={isSelected}
          onClick={() => onPreview(workflow.uid)}
        >
          Vorschau
        </button>
        <Link className="btn btn-secondary" to={`/workflows/${workflow.uid}`}>
          Öffnen
        </Link>
      </div>
    </article>
  );
}

function WorkflowPreviewPane({ workflow }: { workflow: WorkflowSummary | null }) {
  if (!workflow) {
    return (
      <section className="panel panel-muted split-detail-panel" aria-label="Vorgangs-Vorschau">
        <div className="split-detail-title">
          <h2>Vorgang auswählen</h2>
          <p className="split-detail-empty">Wählen Sie links einen Vorgang aus, um die wichtigsten Details hier zu prüfen.</p>
        </div>
      </section>
    );
  }

  const workflowDisplayName = getWorkflowDisplayName(workflow);
  const activeTasks = workflow.taskMetrics.overall.activeCount;
  const pendingRequirements = workflow.requirementSummary.pendingVisibleCount;

  return (
    <section className="panel split-detail-panel" aria-label={`Vorschau für ${workflowDisplayName}`}>
      <div className="split-detail-head">
        <div className="split-detail-title">
          <h2>Vorgangs-Vorschau</h2>
          <p className="split-detail-meta">{workflowDisplayName} · {workflow.workflowDefinition.name} · PN {workflow.employeeNumber}</p>
        </div>
        <span className={`status-pill ${getWorkflowRuntimeStatusPillClass(workflow.workflowStatus)}`}>
          {getWorkflowRuntimeStatusLabel(workflow.workflowStatus)}
        </span>
      </div>

      <dl className="workflow-meta">
        <div>
          <dt>Abteilung</dt>
          <dd>{workflow.departmentName}</dd>
        </div>
        <div>
          <dt>Stelle</dt>
          <dd>{workflow.roleName}</dd>
        </div>
        <div>
          <dt>Deadline</dt>
          <dd>{formatDate(workflow.deadlineDate)}</dd>
        </div>
        <div>
          <dt>Erstellt</dt>
          <dd>{formatDate(workflow.createdAt)}</dd>
        </div>
      </dl>

      <div className="split-detail-counts" aria-label="Vorgangskennzahlen">
        <span className={`task-count-badge${activeTasks > 0 ? " task-count-badge--active" : ""}`}>
          <span className="task-count-badge-value">{activeTasks}</span>
          <span className="task-count-badge-label">Aktive Aufgaben</span>
        </span>
        <span className={`task-count-badge${pendingRequirements > 0 ? " task-count-badge--active" : ""}`}>
          <span className="task-count-badge-value">{pendingRequirements}</span>
          <span className="task-count-badge-label">Offene Pflichtfelder</span>
        </span>
      </div>

      {workflow.taskSummary ? <p className="split-detail-empty">{workflow.taskSummary}</p> : null}

      <div className="action-row">
        <Link className="btn btn-primary" to={`/workflows/${workflow.uid}`}>
          Detail öffnen
        </Link>
      </div>
    </section>
  );
}

export function WorkflowListResults({
  rows,
  isLoading,
  error,
  hasActiveFilters,
  onRetry,
}: WorkflowListResultsProps) {
  const [viewMode, setViewMode] = useState<ViewMode>("cards");
  const [sortKey, setSortKey] = useState<WorkflowSortKey>("created");
  const [sortDirection, setSortDirection] = useState<SortDirection>("desc");
  const [selectedWorkflowUid, setSelectedWorkflowUid] = useState<string | null>(null);

  // Reset selection when rows change (page or filter change) and the selected uid is no longer present
  useEffect(() => {
    if (selectedWorkflowUid !== null && !rows.some((w) => w.uid === selectedWorkflowUid)) {
      setSelectedWorkflowUid(null);
    }
  }, [rows, selectedWorkflowUid]);

  const sortedRows = useMemo(() => {
    const next = [...rows].sort((left, right) => {
      switch (sortKey) {
        case "name":
          return compareText(getWorkflowDisplayName(left), getWorkflowDisplayName(right));
        case "status":
          return compareText(
            getWorkflowRuntimeStatusLabel(left.workflowStatus),
            getWorkflowRuntimeStatusLabel(right.workflowStatus)
          );
        case "department":
          return compareText(left.departmentName, right.departmentName);
        case "deadline":
          return compareNullableDate(left.deadlineDate, right.deadlineDate);
        default:
          return compareNullableDate(left.createdAt, right.createdAt);
      }
    });
    return sortDirection === "asc" ? next : next.reverse();
  }, [rows, sortDirection, sortKey]);
  const updateSort = (nextKey: WorkflowSortKey) => {
    if (nextKey === sortKey) {
      setSortDirection((current) => (current === "asc" ? "desc" : "asc"));
      return;
    }

    setSortKey(nextKey);
    setSortDirection(nextKey === "created" ? "desc" : "asc");
  };
  const getSortValue = (key: WorkflowSortKey) =>
    sortKey === key ? (sortDirection === "asc" ? "ascending" : "descending") : "none";
  const selectedWorkflow = sortedRows.find((workflow) => workflow.uid === selectedWorkflowUid) ?? null;

  if (isLoading) {
    return (
      <section className="workflow-grid" aria-label="Vorgänge werden geladen">
        {Array.from({ length: 8 }, (_, index) => (
          <SkeletonCard key={`workflow-skeleton-${index}`} variant="workflow" />
        ))}
      </section>
    );
  }

  if (error) {
    return (
      <EmptyState
        title="Vorgänge konnten nicht geladen werden."
        description={error}
        actionLabel="Erneut versuchen"
        onAction={onRetry}
      />
    );
  }

  if (rows.length === 0 && !hasActiveFilters) {
    return (
      <EmptyState
        title="Keine laufenden Vorgänge vorhanden"
        description="Aktuell sind keine laufenden Vorgänge vorhanden."
      />
    );
  }

  if (rows.length === 0 && hasActiveFilters) {
    return (
      <EmptyState
        title="Keine Treffer"
        description="Die aktuelle Filterkombination liefert keine passenden Vorgänge."
      />
    );
  }

  const toolbar = (
    <div className="list-view-toolbar">
      <ViewModeToggle value={viewMode} onChange={setViewMode} />
    </div>
  );

  const cards = (
    <section className="workflow-grid" aria-label="Liste Mitarbeiterprozesse">
      {sortedRows.map((workflow) => (
        <WorkflowCard
          key={workflow.uid}
          workflow={workflow}
          isSelected={workflow.uid === selectedWorkflow?.uid}
          onPreview={setSelectedWorkflowUid}
        />
      ))}
    </section>
  );

  const splitPreview = (
    <div className="split-workspace">
      <section className="split-workspace-list" aria-label="Vorgangsliste">
        {cards}
      </section>
      <div className="split-workspace-detail">
        <WorkflowPreviewPane workflow={selectedWorkflow} />
      </div>
    </div>
  );

  if (viewMode === "table") {
    return (
      <>
        {toolbar}
        <div className="split-workspace">
          <section className="split-workspace-list" aria-label="Vorgangsliste">
            <div className="operational-table-wrap">
              <table className="operational-table" aria-label="Tabellenansicht Mitarbeiterprozesse">
                <thead>
                  <tr>
                    <th scope="col" aria-sort={getSortValue("name")}>
                      <button type="button" className="operational-table-sort" onClick={() => updateSort("name")}>
                        Vorgang
                      </button>
                    </th>
                    <th scope="col">Prozesstyp</th>
                    <th scope="col" aria-sort={getSortValue("status")}>
                      <button type="button" className="operational-table-sort" onClick={() => updateSort("status")}>
                        Status
                      </button>
                    </th>
                    <th scope="col" aria-sort={getSortValue("department")}>
                      <button type="button" className="operational-table-sort" onClick={() => updateSort("department")}>
                        Abteilung
                      </button>
                    </th>
                    <th scope="col">Stelle</th>
                    <th scope="col" aria-sort={getSortValue("deadline")}>
                      <button type="button" className="operational-table-sort" onClick={() => updateSort("deadline")}>
                        Deadline
                      </button>
                    </th>
                    <th scope="col" aria-sort={getSortValue("created")}>
                      <button type="button" className="operational-table-sort" onClick={() => updateSort("created")}>
                        Erstellt
                      </button>
                    </th>
                    <th scope="col" aria-label="Aktionen" />
                  </tr>
                </thead>
                <tbody>
                  {sortedRows.map((workflow) => (
                    <tr
                      key={workflow.uid}
                      className={workflow.uid === selectedWorkflow?.uid ? "operational-row--active" : undefined}
                    >
                      <td>
                        <div className="operational-cell-primary">
                          <span className="operational-cell-title">{getWorkflowDisplayName(workflow)}</span>
                          <span className="operational-cell-meta">PN {workflow.employeeNumber}</span>
                        </div>
                      </td>
                      <td>{workflow.workflowDefinition.name}</td>
                      <td>
                        <span className={`status-pill ${getWorkflowRuntimeStatusPillClass(workflow.workflowStatus)}`}>
                          {getWorkflowRuntimeStatusLabel(workflow.workflowStatus)}
                        </span>
                      </td>
                      <td>{workflow.departmentName}</td>
                      <td>{workflow.roleName}</td>
                      <td className="operational-cell-number">{formatDate(workflow.deadlineDate)}</td>
                      <td className="operational-cell-number">{formatDate(workflow.createdAt)}</td>
                      <td>
                        <div className="operational-table-actions">
                          <button type="button" className="btn btn-secondary" onClick={() => setSelectedWorkflowUid(workflow.uid)}>
                            Vorschau
                          </button>
                          <Link className="btn btn-secondary" to={`/workflows/${workflow.uid}`}>
                            Öffnen
                          </Link>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="operational-card-fallback">
              {cards}
            </div>
          </section>
          <div className="split-workspace-detail">
            <WorkflowPreviewPane workflow={selectedWorkflow} />
          </div>
        </div>
      </>
    );
  }

  return (
    <>
      {toolbar}
      {splitPreview}
    </>
  );
}
