import { useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import type { WorkflowQueryOptions } from "../services/workflowApi";
import { useProcessTypes } from "../services/queries/processTypeQueries";
import { useWorkflowList } from "../services/queries/workflowQueries";
import type { ProcessType, WorkflowResponsibilityOption, WorkflowRuntimeStatus, WorkflowSummary } from "../types/workflow";

const SEARCH_DEBOUNCE_MS = 400;
const PAGE_SIZE = 20;
const WORKFLOW_STATUS_FILTERS: Array<"all" | WorkflowRuntimeStatus> = [
  "all",
  "draft",
  "waiting_for_supervisor",
  "waiting_for_department",
  "in_progress",
  "completed",
];

export function parseWorkflowStatusFilter(value: string | null): "all" | WorkflowRuntimeStatus {
  return WORKFLOW_STATUS_FILTERS.includes(value as "all" | WorkflowRuntimeStatus)
    ? (value as "all" | WorkflowRuntimeStatus)
    : "all";
}

export function parsePageIndex(value: string | null): number {
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed < 2) {
    return 0;
  }

  return Math.floor(parsed) - 1;
}

export function useWorkflowListPageView() {
  const [searchParams, setSearchParams] = useSearchParams();
  const initialSearch = searchParams.get("q") ?? "";
  const initialStatusFilter = parseWorkflowStatusFilter(searchParams.get("status"));
  const initialDepartmentFilter = searchParams.get("dept") ?? "all";
  const initialProcessTypeFilter = searchParams.get("type") ?? "all";
  const initialResponsibilityFilter = searchParams.get("resp") ?? "all";
  const initialPageIndex = parsePageIndex(searchParams.get("page"));

  const [pageIndex, setPageIndex] = useState<number>(initialPageIndex);
  const [search, setSearch] = useState<string>(initialSearch);
  const [debouncedSearch, setDebouncedSearch] = useState<string>(initialSearch);
  const [statusFilter, setStatusFilter] = useState<"all" | WorkflowRuntimeStatus>(initialStatusFilter);
  const [departmentFilter, setDepartmentFilter] = useState<string>(initialDepartmentFilter);
  const [processTypeFilter, setProcessTypeFilter] = useState<string>(initialProcessTypeFilter);
  const [responsibilityFilter, setResponsibilityFilter] = useState<string>(initialResponsibilityFilter);

  useEffect(() => {
    const timer = setTimeout(() => setDebouncedSearch(search), SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    const nextParams = new URLSearchParams();

    if (search.trim().length > 0) {
      nextParams.set("q", search);
    }

    if (statusFilter !== "all") {
      nextParams.set("status", statusFilter);
    }

    if (departmentFilter !== "all") {
      nextParams.set("dept", departmentFilter);
    }

    if (processTypeFilter !== "all") {
      nextParams.set("type", processTypeFilter);
    }

    if (responsibilityFilter !== "all") {
      nextParams.set("resp", responsibilityFilter);
    }

    if (pageIndex > 0) {
      nextParams.set("page", String(pageIndex + 1));
    }

    if (nextParams.toString() !== searchParams.toString()) {
      setSearchParams(nextParams, { replace: true });
    }
  }, [
    departmentFilter,
    pageIndex,
    processTypeFilter,
    responsibilityFilter,
    search,
    searchParams,
    setSearchParams,
    statusFilter,
  ]);

  const pageQuery = useMemo<WorkflowQueryOptions>(
    () => ({
      status: statusFilter === "all" ? null : statusFilter,
      departmentId: departmentFilter === "all" ? null : Number(departmentFilter),
      processTypeKey: processTypeFilter === "all" ? null : processTypeFilter,
      search: debouncedSearch,
      responsibilityValue: responsibilityFilter === "all" ? null : responsibilityFilter,
    }),
    [departmentFilter, debouncedSearch, processTypeFilter, responsibilityFilter, statusFilter]
  );
  const processTypesQuery = useProcessTypes();
  const workflowListQuery = useWorkflowList(pageQuery, pageIndex, PAGE_SIZE);
  const processTypeOptions: ProcessType[] = processTypesQuery.data ?? [];
  const rows: WorkflowSummary[] = workflowListQuery.data?.items ?? [];
  const totalCount = workflowListQuery.data?.count ?? 0;
  const departmentOptions: Array<[number, string]> =
    workflowListQuery.data?.departmentOptions.map((option) => [option.id, option.name]) ?? [];
  const responsibilityOptions: WorkflowResponsibilityOption[] =
    workflowListQuery.data?.responsibilityOptions ?? [];
  const isLoading = workflowListQuery.isLoading;
  const isRefreshing = workflowListQuery.isFetching || processTypesQuery.isFetching;
  const error =
    workflowListQuery.error instanceof Error
      ? workflowListQuery.error.message
      : processTypesQuery.error instanceof Error
        ? processTypesQuery.error.message
        : workflowListQuery.error || processTypesQuery.error
          ? "Vorgänge konnten nicht geladen werden."
          : null;

  const totalPages = useMemo(() => {
    if (totalCount <= 0) {
      return 1;
    }

    return Math.ceil(totalCount / PAGE_SIZE);
  }, [totalCount]);

  const hasActiveFilters = useMemo(
    () =>
      search.trim().length > 0 ||
      statusFilter !== "all" ||
      departmentFilter !== "all" ||
      processTypeFilter !== "all" ||
      responsibilityFilter !== "all",
    [departmentFilter, processTypeFilter, responsibilityFilter, search, statusFilter]
  );
  const hasAdvancedFilters =
    departmentFilter !== "all" || processTypeFilter !== "all" || responsibilityFilter !== "all";
  const advancedFilterCount = [departmentFilter, processTypeFilter, responsibilityFilter].filter(
    (value) => value !== "all"
  ).length;

  const refresh = async () => {
    await Promise.all([workflowListQuery.refetch(), processTypesQuery.refetch()]);
  };

  return {
    search,
    statusFilter,
    departmentFilter,
    processTypeFilter,
    responsibilityFilter,
    pageIndex,
    processTypeOptions,
    departmentOptions,
    responsibilityOptions,
    rows,
    totalCount,
    totalPages,
    isLoading,
    isRefreshing,
    error,
    hasActiveFilters,
    hasAdvancedFilters,
    advancedFilterCount,
    setSearch: (value: string) => {
      setSearch(value);
      setPageIndex(0);
    },
    setStatusFilter: (value: "all" | WorkflowRuntimeStatus) => {
      setStatusFilter(value);
      setPageIndex(0);
    },
    setDepartmentFilter: (value: string) => {
      setDepartmentFilter(value);
      setPageIndex(0);
    },
    setProcessTypeFilter: (value: string) => {
      setProcessTypeFilter(value);
      setPageIndex(0);
    },
    setResponsibilityFilter: (value: string) => {
      setResponsibilityFilter(value);
      setPageIndex(0);
    },
    goToPreviousPage: () => setPageIndex((current) => Math.max(0, current - 1)),
    goToNextPage: () => setPageIndex((current) => current + 1),
    refresh,
  };
}
