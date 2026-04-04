import PageHeader from "../components/layout/PageHeader";
import { WorkflowListFilters } from "./WorkflowListFilters";
import { WorkflowListResults } from "./WorkflowListResults";
import { useWorkflowListPageView } from "./workflowListPageModel";

export default function WorkflowListPage() {
  const view = useWorkflowListPageView();

  return (
    <main className="app-shell">
      <div className="page-container">
        <PageHeader title="Laufende Vorgänge" />

        <WorkflowListFilters
          search={view.search}
          statusFilter={view.statusFilter}
          departmentFilter={view.departmentFilter}
          processTypeFilter={view.processTypeFilter}
          responsibilityFilter={view.responsibilityFilter}
          processTypeOptions={view.processTypeOptions}
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
          onProcessTypeChange={view.setProcessTypeFilter}
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
