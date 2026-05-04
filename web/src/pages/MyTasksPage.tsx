import { useMemo, useState } from "react";
import EmptyState from "../components/feedback/EmptyState";
import SkeletonCard from "../components/feedback/SkeletonCard";
import PageHeader from "../components/layout/PageHeader";
import ViewModeToggle, { type ViewMode } from "../components/layout/ViewModeToggle";
import { getVisibleTaskStatusLabel } from "../utils/taskStatus";
import { MyTaskGroups } from "./MyTaskGroups";
import type { WorkflowTaskSummary } from "./myTasksPageModel";
import { useMyTasksPageView } from "./myTasksPageModel";

function TaskCountBadge({ label, count, highlight }: { label: string; count: number; highlight?: boolean }) {
  return (
    <span className={`task-count-badge${highlight && count > 0 ? " task-count-badge--active" : ""}`}>
      <span className="task-count-badge-value">{count}</span>
      <span className="task-count-badge-label">{label}</span>
    </span>
  );
}

function FilterChip({ label, value }: { label: string; value: string }) {
  return (
    <span className="filter-chip">
      <strong>{label}</strong>
      <span>{value}</span>
    </span>
  );
}

type MyTasksSortKey = "person" | "department" | "open" | "in_progress" | "blocked" | "total";
type SortDirection = "asc" | "desc";

function compareText(left: string, right: string): number {
  return left.localeCompare(right, "de", { sensitivity: "base" });
}

function WorkflowSummaryCard({
  summary,
  onClick,
  isSelected = false,
}: {
  summary: WorkflowTaskSummary;
  onClick: (workflowUid: string) => void;
  isSelected?: boolean;
}) {
  const openTotal = summary.counts.open + summary.counts.in_progress + summary.counts.blocked;
  return (
    <button
      type="button"
      className={`workflow-summary-card${isSelected ? " workflow-summary-card--active" : ""}`}
      aria-pressed={isSelected}
      onClick={() => onClick(summary.workflowUid)}
    >
      <div className="workflow-summary-card-body">
        <div className="workflow-summary-card-info">
          <p className="workflow-summary-card-name">{summary.personName}</p>
          <p className="workflow-summary-card-meta">{summary.departmentName} · {summary.roleName}</p>
        </div>
        <div className="workflow-summary-card-counts">
          <TaskCountBadge label="Offen" count={summary.counts.open} highlight />
          <TaskCountBadge label="In Bearb." count={summary.counts.in_progress} highlight />
          <TaskCountBadge label="Blockiert" count={summary.counts.blocked} highlight />
          <TaskCountBadge label="Erledigt" count={summary.counts.done} />
        </div>
      </div>
      {openTotal > 0 && (
        <p className="workflow-summary-card-hint">
          {openTotal} ausstehende Aufgabe{openTotal === 1 ? "" : "n"} – Klicken zum Bearbeiten
        </p>
      )}
    </button>
  );
}

function WorkflowTaskDetailPane({
  summary,
  selectedWorkflowGroups,
  expandedStatuses,
  savingTaskIds,
  savingApprovalTaskIds,
  commentDrafts,
  savingCommentTaskIds,
  onClose,
  onToggleStatus,
  onStatusChange,
  onApprovalDecision,
  onCommentDraftChange,
  onCommentSubmit,
}: {
  summary: WorkflowTaskSummary | null;
  selectedWorkflowGroups: ReturnType<typeof useMyTasksPageView>["selectedWorkflowGroups"];
  expandedStatuses: ReturnType<typeof useMyTasksPageView>["expandedStatuses"];
  savingTaskIds: ReturnType<typeof useMyTasksPageView>["savingTaskIds"];
  savingApprovalTaskIds: ReturnType<typeof useMyTasksPageView>["savingApprovalTaskIds"];
  commentDrafts: ReturnType<typeof useMyTasksPageView>["commentDrafts"];
  savingCommentTaskIds: ReturnType<typeof useMyTasksPageView>["savingCommentTaskIds"];
  onClose: () => void;
  onToggleStatus: ReturnType<typeof useMyTasksPageView>["toggleStatus"];
  onStatusChange: ReturnType<typeof useMyTasksPageView>["handleStatusChange"];
  onApprovalDecision: ReturnType<typeof useMyTasksPageView>["handleApprovalDecision"];
  onCommentDraftChange: ReturnType<typeof useMyTasksPageView>["handleCommentDraftChange"];
  onCommentSubmit: ReturnType<typeof useMyTasksPageView>["handleTaskCommentSubmit"];
}) {
  if (!summary) {
    return (
      <section className="panel panel-muted split-detail-panel" aria-label="Aufgaben-Vorschau">
        <div className="split-detail-title">
          <h2>Vorgang auswählen</h2>
          <p className="split-detail-empty">Wählen Sie links einen Vorgang aus, um die Aufgaben hier zu bearbeiten.</p>
        </div>
      </section>
    );
  }

  return (
    <section className="panel split-detail-panel" aria-label={`Aufgaben für ${summary.personName}`}>
      <div className="split-detail-head">
        <div className="split-detail-title">
          <h2>{summary.personName}</h2>
          <p className="split-detail-meta">{summary.departmentName} · {summary.roleName}</p>
        </div>
        <button type="button" className="btn btn-ghost" onClick={onClose}>
          Schließen
        </button>
      </div>

      <div className="split-detail-counts" aria-label="Aufgabenstatus">
        <TaskCountBadge label="Offen" count={summary.counts.open} highlight />
        <TaskCountBadge label="In Bearb." count={summary.counts.in_progress} highlight />
        <TaskCountBadge label="Blockiert" count={summary.counts.blocked} highlight />
        <TaskCountBadge label="Erledigt" count={summary.counts.done} />
      </div>

      {selectedWorkflowGroups.length === 0 ? (
        <EmptyState title="Keine Aufgaben" description="Für diesen Vorgang gibt es keine Aufgaben." />
      ) : (
        <div className="task-groups" aria-label="Aufgaben nach Status">
          {selectedWorkflowGroups.map((group) => {
            const isExpanded = expandedStatuses.has(group.status);
            return (
              <section key={group.status} className="task-group">
                <button
                  type="button"
                  className="task-group-head task-group-head--collapsible"
                  aria-expanded={isExpanded}
                  onClick={() => onToggleStatus(group.status)}
                >
                  <span className={`task-group-chevron${isExpanded ? " task-group-chevron--open" : ""}`}>
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" className="icon-inline">
                      <path d="M6 9l6 6 6-6" />
                    </svg>
                  </span>
                  <div className="task-group-head-copy">
                    <h3>{getVisibleTaskStatusLabel(group.status)}</h3>
                    <p>{group.items.length} Aufgabe{group.items.length === 1 ? "" : "n"}</p>
                  </div>
                </button>
                {isExpanded && (
                  <MyTaskGroups
                    groups={[group]}
                    savingTaskIds={savingTaskIds}
                    savingApprovalTaskIds={savingApprovalTaskIds}
                    commentDrafts={commentDrafts}
                    savingCommentTaskIds={savingCommentTaskIds}
                    onStatusChange={onStatusChange}
                    onApprovalDecision={onApprovalDecision}
                    onCommentDraftChange={onCommentDraftChange}
                    onCommentSubmit={onCommentSubmit}
                  />
                )}
              </section>
            );
          })}
        </div>
      )}
    </section>
  );
}

export default function MyTasksPage() {
  const view = useMyTasksPageView();
  const [viewMode, setViewMode] = useState<ViewMode>("cards");
  const [sortKey, setSortKey] = useState<MyTasksSortKey>("open");
  const [sortDirection, setSortDirection] = useState<SortDirection>("desc");
  const hasActiveFilters = view.search.trim().length > 0 || view.departmentFilter !== "all";
  const selectedDepartmentLabel =
    view.departmentOptions.find(([departmentId]) => String(departmentId) === view.departmentFilter)?.[1] ?? null;
  const resetFilters = () => {
    view.setSearch("");
    view.setDepartmentFilter("all");
  };
  const sortedWorkflowSummaries = useMemo(() => {
    const next = [...view.filteredWorkflowSummaries].sort((left, right) => {
      switch (sortKey) {
        case "person":
          return compareText(left.personName, right.personName);
        case "department":
          return compareText(left.departmentName, right.departmentName);
        case "in_progress":
          return left.counts.in_progress - right.counts.in_progress;
        case "blocked":
          return left.counts.blocked - right.counts.blocked;
        case "total":
          return left.totalCount - right.totalCount;
        default:
          return left.counts.open - right.counts.open;
      }
    });
    return sortDirection === "asc" ? next : next.reverse();
  }, [sortDirection, sortKey, view.filteredWorkflowSummaries]);
  const updateSort = (nextKey: MyTasksSortKey) => {
    if (nextKey === sortKey) {
      setSortDirection((current) => (current === "asc" ? "desc" : "asc"));
      return;
    }

    setSortKey(nextKey);
    setSortDirection(nextKey === "person" || nextKey === "department" ? "asc" : "desc");
  };
  const getSortValue = (key: MyTasksSortKey) =>
    sortKey === key ? (sortDirection === "asc" ? "ascending" : "descending") : "none";

  // Übersichts-Ansicht
  return (
    <main className="app-shell">
      <div className="page-container">
        <PageHeader
          variant="workspace"
          title="Meine Aufgaben"
          description="Aufgaben, die dir direkt oder über deinen Fachbereich zugewiesen sind."
        />

        <section className="panel panel-muted">
          <div className="panel-head">
            <h2>Filter</h2>
          </div>
          <div className="toolbar-row toolbar-row-filters">
            <label className="field compact grow">
              <span>Suche</span>
              <input
                type="text"
                value={view.search}
                onChange={(event) => view.setSearch(event.target.value)}
                placeholder="z. B. Name, Vorgangs-ID"
              />
            </label>

            <label className="field compact">
              <span>Abteilung</span>
              <select value={view.departmentFilter} onChange={(event) => view.setDepartmentFilter(event.target.value)}>
                <option value="all">Alle</option>
                {view.departmentOptions.map(([departmentId, departmentName]) => (
                  <option key={departmentId} value={departmentId}>
                    {departmentName}
                  </option>
                ))}
              </select>
            </label>

            <button type="button" className="btn btn-secondary" onClick={() => void view.reload()} disabled={view.isRefreshing}>
              {view.isRefreshing ? "Aktualisiere..." : "Aktualisieren"}
            </button>
          </div>

          <div className="filter-panel-actions" aria-live="polite">
            <div className="filter-chip-row" aria-label="Aktive Filter">
              {view.search.trim().length > 0 ? <FilterChip label="Suche" value={view.search.trim()} /> : null}
              {selectedDepartmentLabel ? <FilterChip label="Abteilung" value={selectedDepartmentLabel} /> : null}
              {!hasActiveFilters ? <span className="filter-chip-empty">Keine aktiven Filter.</span> : null}
            </div>
            <button type="button" className="btn btn-ghost" onClick={resetFilters} disabled={!hasActiveFilters}>
              Filter zurücksetzen
            </button>
          </div>
        </section>

        {view.isLoading ? (
          <section className="skeleton-stack-list" aria-label="Aufgaben werden geladen">
            {Array.from({ length: 4 }, (_, index) => (
              <SkeletonCard key={`task-skeleton-${index}`} variant="task" />
            ))}
          </section>
        ) : null}

        {!view.isLoading && view.error ? (
          <EmptyState
            title="Aufgaben konnten nicht geladen werden."
            description={view.error}
            actionLabel="Erneut versuchen"
            onAction={() => void view.reload()}
          />
        ) : null}

        {!view.isLoading && !view.error && view.rows.length === 0 ? (
          <EmptyState title="Keine Aufgaben vorhanden" description="Aktuell sind keine Aufgaben zugeordnet." />
        ) : null}

        {!view.isLoading && !view.error && view.rows.length > 0 && view.filteredWorkflowSummaries.length === 0 ? (
          <EmptyState
            title="Keine Treffer"
            description="Die aktuelle Filterkombination liefert keine Vorgänge."
          />
        ) : null}

        {!view.isLoading && !view.error && view.filteredWorkflowSummaries.length > 0 ? (
          <>
            <div className="list-view-toolbar">
              <ViewModeToggle value={viewMode} onChange={setViewMode} />
            </div>
            <div className="split-workspace">
              <section className="split-workspace-list" aria-label="Vorgangs-Auswahl">
                {viewMode === "table" ? (
                  <>
                    <div className="operational-table-wrap">
                      <table className="operational-table" aria-label="Tabellenansicht Vorgänge mit Aufgaben">
                        <thead>
                          <tr>
                            <th scope="col" aria-sort={getSortValue("person")}>
                              <button type="button" className="operational-table-sort" onClick={() => updateSort("person")}>
                                Person
                              </button>
                            </th>
                            <th scope="col" aria-sort={getSortValue("department")}>
                              <button type="button" className="operational-table-sort" onClick={() => updateSort("department")}>
                                Abteilung
                              </button>
                            </th>
                            <th scope="col">Stelle</th>
                            <th scope="col" aria-sort={getSortValue("open")}>
                              <button type="button" className="operational-table-sort" onClick={() => updateSort("open")}>
                                Offen
                              </button>
                            </th>
                            <th scope="col" aria-sort={getSortValue("in_progress")}>
                              <button type="button" className="operational-table-sort" onClick={() => updateSort("in_progress")}>
                                In Bearb.
                              </button>
                            </th>
                            <th scope="col" aria-sort={getSortValue("blocked")}>
                              <button type="button" className="operational-table-sort" onClick={() => updateSort("blocked")}>
                                Blockiert
                              </button>
                            </th>
                            <th scope="col" aria-sort={getSortValue("total")}>
                              <button type="button" className="operational-table-sort" onClick={() => updateSort("total")}>
                                Gesamt
                              </button>
                            </th>
                            <th scope="col" aria-label="Aktionen" />
                          </tr>
                        </thead>
                        <tbody>
                          {sortedWorkflowSummaries.map((summary) => (
                            <tr
                              key={summary.workflowUid}
                              className={summary.workflowUid === view.selectedWorkflowUid ? "operational-row--active" : undefined}
                            >
                              <td>
                                <div className="operational-cell-primary">
                                  <span className="operational-cell-title">{summary.personName}</span>
                                  <span className="operational-cell-meta">{summary.workflowUid}</span>
                                </div>
                              </td>
                              <td>{summary.departmentName}</td>
                              <td>{summary.roleName}</td>
                              <td className="operational-cell-number">{summary.counts.open}</td>
                              <td className="operational-cell-number">{summary.counts.in_progress}</td>
                              <td className="operational-cell-number">{summary.counts.blocked}</td>
                              <td className="operational-cell-number">{summary.totalCount}</td>
                              <td>
                                <div className="operational-table-actions">
                                  <button type="button" className="btn btn-secondary" onClick={() => view.setSelectedWorkflowUid(summary.workflowUid)}>
                                    Vorschau
                                  </button>
                                </div>
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                    <div className="workflow-summary-list operational-card-fallback" aria-label="Vorgänge mit Aufgaben">
                      {sortedWorkflowSummaries.map((summary) => (
                        <WorkflowSummaryCard
                          key={summary.workflowUid}
                          summary={summary}
                          onClick={view.setSelectedWorkflowUid}
                          isSelected={summary.workflowUid === view.selectedWorkflowUid}
                        />
                      ))}
                    </div>
                  </>
                ) : (
                  <div className="workflow-summary-list" aria-label="Vorgänge mit Aufgaben">
                    {sortedWorkflowSummaries.map((summary) => (
                      <WorkflowSummaryCard
                        key={summary.workflowUid}
                        summary={summary}
                        onClick={view.setSelectedWorkflowUid}
                        isSelected={summary.workflowUid === view.selectedWorkflowUid}
                      />
                    ))}
                  </div>
                )}
              </section>

              <div className="split-workspace-detail">
                <WorkflowTaskDetailPane
                  summary={view.selectedWorkflowSummary}
                  selectedWorkflowGroups={view.selectedWorkflowGroups}
                  expandedStatuses={view.expandedStatuses}
                  savingTaskIds={view.savingTaskIds}
                  savingApprovalTaskIds={view.savingApprovalTaskIds}
                  commentDrafts={view.commentDrafts}
                  savingCommentTaskIds={view.savingCommentTaskIds}
                  onClose={() => view.setSelectedWorkflowUid(null)}
                  onToggleStatus={view.toggleStatus}
                  onStatusChange={view.handleStatusChange}
                  onApprovalDecision={view.handleApprovalDecision}
                  onCommentDraftChange={view.handleCommentDraftChange}
                  onCommentSubmit={view.handleTaskCommentSubmit}
                />
              </div>
            </div>
          </>
        ) : null}
      </div>
    </main>
  );
}
