import EmptyState from "../components/feedback/EmptyState";
import SkeletonCard from "../components/feedback/SkeletonCard";
import PageHeader from "../components/layout/PageHeader";
import { getVisibleTaskStatusLabel, VISIBLE_TASK_STATUS_ORDER, type VisibleTaskStatus } from "../utils/taskStatus";
import { MyTaskGroups } from "./MyTaskGroups";
import { useMyTasksPageView } from "./myTasksPageModel";

export default function MyTasksPage() {
  const view = useMyTasksPageView();

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
                placeholder="z. B. Name, ID, Aufgabe"
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

            <label className="field compact">
              <span>Status</span>
              <select
                value={view.statusFilter}
                onChange={(event) => view.setStatusFilter(event.target.value as "all" | VisibleTaskStatus)}
              >
                <option value="all">Alle</option>
                {VISIBLE_TASK_STATUS_ORDER.map((status) => (
                  <option key={status} value={status}>
                    {getVisibleTaskStatusLabel(status)}
                  </option>
                ))}
              </select>
            </label>

            <label className="field compact">
              <span>Zuständiger Bereich</span>
              <select value={view.responsibilityFilter} onChange={(event) => view.setResponsibilityFilter(event.target.value)}>
                <option value="all">Alle</option>
                {view.responsibilityOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
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
            {Array.from({ length: 5 }, (_, index) => (
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

        {!view.isLoading && !view.error && view.rows.length > 0 && view.filteredRows.length === 0 ? (
          <EmptyState
            title="Keine Treffer"
            description="Die aktuelle Filterkombination liefert keine Aufgaben."
          />
        ) : null}

        {!view.isLoading && !view.error && view.filteredRows.length > 0 ? (
          <MyTaskGroups
            visibleGroups={view.visibleGroups}
            savingTaskIds={view.savingTaskIds}
            savingApprovalTaskIds={view.savingApprovalTaskIds}
            commentDrafts={view.commentDrafts}
            savingCommentTaskIds={view.savingCommentTaskIds}
            onStatusChange={view.handleStatusChange}
            onApprovalDecision={view.handleApprovalDecision}
            onCommentDraftChange={view.handleCommentDraftChange}
            onCommentSubmit={view.handleTaskCommentSubmit}
          />
        ) : null}
      </div>
    </main>
  );
}
