import EmptyState from "../components/feedback/EmptyState";
import SkeletonCard from "../components/feedback/SkeletonCard";
import PageHeader from "../components/layout/PageHeader";
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

function WorkflowSummaryCard({
  summary,
  onClick,
}: {
  summary: WorkflowTaskSummary;
  onClick: (workflowUid: string) => void;
}) {
  const openTotal = summary.counts.open + summary.counts.in_progress + summary.counts.blocked;
  return (
    <button
      type="button"
      className="workflow-summary-card"
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

export default function MyTasksPage() {
  const view = useMyTasksPageView();

  // Detail-Ansicht (Workflow ausgewählt)
  if (view.selectedWorkflowUid && view.selectedWorkflowSummary) {
    const summary = view.selectedWorkflowSummary;
    return (
      <main className="app-shell">
        <div className="page-container">
          <div className="page-back-row">
            <button
              type="button"
              className="btn btn-ghost"
              onClick={() => view.setSelectedWorkflowUid(null)}
            >
              ← Zurück zur Übersicht
            </button>
          </div>

          <PageHeader
            variant="workspace"
            title={summary.personName}
            description={`${summary.departmentName} · ${summary.roleName}`}
          />

          {view.selectedWorkflowGroups.length === 0 ? (
            <EmptyState title="Keine Aufgaben" description="Für diesen Vorgang gibt es keine Aufgaben." />
          ) : (
            <div className="task-groups" aria-label="Aufgaben nach Status">
              {view.selectedWorkflowGroups.map((group) => {
                const isExpanded = view.expandedStatuses.has(group.status);
                return (
                  <section key={group.status} className="task-group">
                    <button
                      type="button"
                      className="task-group-head task-group-head--collapsible"
                      aria-expanded={isExpanded}
                      onClick={() => view.toggleStatus(group.status)}
                    >
                      <span className={`task-group-chevron${isExpanded ? " task-group-chevron--open" : ""}`}>
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" style={{ width: "1em", height: "1em" }}>
                          <path d="M6 9l6 6 6-6" />
                        </svg>
                      </span>
                      <div className="task-group-head-copy">
                        <h2>{getVisibleTaskStatusLabel(group.status)}</h2>
                        <p>{group.items.length} Aufgabe{group.items.length === 1 ? "" : "n"}</p>
                      </div>
                    </button>
                    {isExpanded && (
                      <MyTaskGroups
                        groups={[group]}
                        savingTaskIds={view.savingTaskIds}
                        savingApprovalTaskIds={view.savingApprovalTaskIds}
                        commentDrafts={view.commentDrafts}
                        savingCommentTaskIds={view.savingCommentTaskIds}
                        onStatusChange={view.handleStatusChange}
                        onApprovalDecision={view.handleApprovalDecision}
                        onCommentDraftChange={view.handleCommentDraftChange}
                        onCommentSubmit={view.handleTaskCommentSubmit}
                      />
                    )}
                  </section>
                );
              })}
            </div>
          )}
        </div>
      </main>
    );
  }

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
          <div className="workflow-summary-list" aria-label="Vorgänge mit Aufgaben">
            {view.filteredWorkflowSummaries.map((summary) => (
              <WorkflowSummaryCard
                key={summary.workflowUid}
                summary={summary}
                onClick={view.setSelectedWorkflowUid}
              />
            ))}
          </div>
        ) : null}
      </div>
    </main>
  );
}
