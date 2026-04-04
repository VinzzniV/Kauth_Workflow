import { useCallback, useMemo, useState } from "react";
import { useTaskInteraction } from "../hooks/useTaskInteraction";
import { useMyTasks } from "../services/queries/workflowQueries";
import type { TaskWithWorkflow } from "../types/workflow";
import {
  getResponsibleResponsibilityFilterOption,
  getResponsibleResponsibilityFilterValue,
} from "../utils/taskAssignment";
import {
  getVisibleTaskStatus,
  TASK_STATUS_ORDER,
  VISIBLE_TASK_STATUS_ORDER,
  type VisibleTaskStatus,
} from "../utils/taskStatus";

export function toTaskStateKey(workflowUid: string, taskId: number): string {
  return `${workflowUid}:${taskId}`;
}

export function useMyTasksPageView() {
  const myTasksQuery = useMyTasks();
  const {
    savingTaskIds,
    commentDrafts,
    savingCommentTaskIds,
    handleStatusChange,
    handleCommentDraftChange,
    handleTaskCommentSubmit,
  } = useTaskInteraction();
  const [search, setSearch] = useState<string>("");
  const [departmentFilter, setDepartmentFilter] = useState<string>("all");
  const [statusFilter, setStatusFilter] = useState<"all" | VisibleTaskStatus>("all");
  const [responsibilityFilter, setResponsibilityFilter] = useState<string>("all");
  const rows = useMemo<TaskWithWorkflow[]>(() => myTasksQuery.data ?? [], [myTasksQuery.data]);
  const isLoading = myTasksQuery.isLoading;
  const isRefreshing = myTasksQuery.isFetching;
  const error =
    myTasksQuery.error instanceof Error
      ? myTasksQuery.error.message
      : myTasksQuery.error
        ? "Aufgaben konnten nicht geladen werden."
        : null;

  const reload = useCallback(async () => {
    await myTasksQuery.refetch();
  }, [myTasksQuery]);

  const departmentOptions = useMemo(() => {
    const entries = Array.from(
      new Map(rows.map((row) => [row.workflow.departmentId, row.workflow.departmentName])).entries()
    );
    return entries.sort((left, right) => left[1].localeCompare(right[1], "de"));
  }, [rows]);

  const responsibilityOptions = useMemo(() => {
    const optionsByValue = new Map(
      rows.map((row) => {
        const option = getResponsibleResponsibilityFilterOption(row.task);
        return [option.value, option] as const;
      })
    );

    return Array.from(optionsByValue.values()).sort((left, right) => left.label.localeCompare(right.label, "de"));
  }, [rows]);

  const filteredRows = useMemo(() => {
    const normalizedSearch = search.trim().toLowerCase();

    return rows
      .filter((row) => {
        const workflowUid = row.workflow.workflowUid;
        const workflowDisplayName =
          `${row.workflow.firstName} ${row.workflow.lastName}`.trim() || "Unbekannter Mitarbeitender";
        const effectiveStatus = getVisibleTaskStatus(row.task.status);
        const matchesDepartment = departmentFilter === "all" || String(row.workflow.departmentId) === departmentFilter;
        const matchesStatus = statusFilter === "all" || effectiveStatus === statusFilter;
        const responsibilityFilterValue = getResponsibleResponsibilityFilterValue(row.task);
        const matchesResponsibility =
          responsibilityFilter === "all" || responsibilityFilter === responsibilityFilterValue;

        if (!matchesDepartment || !matchesStatus || !matchesResponsibility) {
          return false;
        }

        if (!normalizedSearch) {
          return true;
        }

        return (
          workflowDisplayName.toLowerCase().includes(normalizedSearch) ||
          String(row.workflow.employeeNumber).includes(normalizedSearch) ||
          workflowUid.toLowerCase().includes(normalizedSearch) ||
          row.task.title.toLowerCase().includes(normalizedSearch) ||
          row.task.taskKey.toLowerCase().includes(normalizedSearch)
        );
      })
      .sort((left, right) => {
        const leftStatus = left.task.status;
        const rightStatus = right.task.status;
        const leftName =
          `${left.workflow.firstName} ${left.workflow.lastName}`.trim() || "Unbekannter Mitarbeitender";
        const rightName =
          `${right.workflow.firstName} ${right.workflow.lastName}`.trim() || "Unbekannter Mitarbeitender";

        const statusDelta = TASK_STATUS_ORDER.indexOf(leftStatus) - TASK_STATUS_ORDER.indexOf(rightStatus);
        if (statusDelta !== 0) {
          return statusDelta;
        }

        return left.task.sortOrder - right.task.sortOrder || leftName.localeCompare(rightName, "de") || left.task.id - right.task.id;
      });
  }, [rows, search, departmentFilter, statusFilter, responsibilityFilter]);

  const groupedRows = useMemo(() => {
    const groups = VISIBLE_TASK_STATUS_ORDER.map((status) => ({
      status,
      items: [] as TaskWithWorkflow[],
    }));
    const itemsByStatus = new Map(groups.map((group) => [group.status, group.items]));

    for (const row of filteredRows) {
      itemsByStatus.get(getVisibleTaskStatus(row.task.status))?.push(row);
    }

    return groups;
  }, [filteredRows]);
  const visibleGroups = useMemo(() => groupedRows.filter((group) => group.items.length > 0), [groupedRows]);

  return {
    search,
    departmentFilter,
    statusFilter,
    responsibilityFilter,
    departmentOptions,
    responsibilityOptions,
    rows,
    filteredRows,
    visibleGroups,
    isLoading,
    isRefreshing,
    error,
    savingTaskIds,
    commentDrafts,
    savingCommentTaskIds,
    setSearch,
    setDepartmentFilter,
    setStatusFilter,
    setResponsibilityFilter,
    reload,
    handleStatusChange,
    handleCommentDraftChange,
    handleTaskCommentSubmit,
  };
}
