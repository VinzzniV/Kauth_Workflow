import { Link } from "react-router-dom";
import { useCurrentUser } from "../auth/useCurrentUser";
import PageHeader from "../components/layout/PageHeader";
import { WorkflowListFilters } from "./WorkflowListFilters";
import { WorkflowListResults } from "./WorkflowListResults";
import { useWorkflowListPageView } from "./workflowListPageModel";

export default function WorkflowListPage() {
  const view = useWorkflowListPageView();
  const { capabilities } = useCurrentUser();

  return (
    <main className="app-shell">
      <div className="page-container">
        <PageHeader
          variant="workspace"
          title="Laufende Vorgänge"
          description="Alle aktiven Prozesse filtern, verfolgen und weiterbearbeiten."
          actions={
            capabilities.canCreateWorkflow ? (
              <Link to="/create" className="btn btn-primary">
                Neuer Vorgang
              </Link>
            ) : undefined
          }
        />

        <WorkflowListFilters
          search={view.search}
          statusFilter={view.statusFilter}
          departmentFilter={view.departmentFilter}
          workflowDefinitionFilter={view.workflowDefinitionFilter}
          responsibilityFilter={view.responsibilityFilter}
          workflowDefinitionOptions={view.workflowDefinitionOptions}
          departmentOptions={view.departmentOptions}
          responsibilityOptions={view.responsibilityOptions}
          hasAdvancedFilters={view.hasAdvancedFilters}
          advancedFilterCount={view.advancedFilterCount}
          pageIndex={view.pageIndex}
          totalPages={view.totalPages}
          totalCount={view.totalCount}
          isRefreshing={view.isRefreshing}
          onSearchChange={view.setSearch}
          onStatusChange={view.setStatusFilter}
          onDepartmentChange={view.setDepartmentFilter}
          onWorkflowDefinitionChange={view.setWorkflowDefinitionFilter}
          onResponsibilityChange={view.setResponsibilityFilter}
          onRefresh={() => void view.refresh()}
          onPreviousPage={view.goToPreviousPage}
          onNextPage={view.goToNextPage}
        />

        <WorkflowListResults
          rows={view.rows}
          isLoading={view.isLoading}
          error={view.error}
          hasActiveFilters={view.hasActiveFilters}
          onRetry={() => void view.refresh()}
        />
      </div>
    </main>
  );
}
