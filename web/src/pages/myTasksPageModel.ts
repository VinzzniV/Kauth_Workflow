import { useCallback, useMemo, useState } from "react";
import { useTaskInteraction } from "../hooks/useTaskInteraction";
import { useMyTasks } from "../services/queries/workflowQueries";
import type { TaskWithWorkflow } from "../types/workflow";
import { getResponsibleResponsibilityFilterOption } from "../utils/taskAssignment";
import {
  getVisibleTaskStatus,
  TASK_STATUS_ORDER,
  VISIBLE_TASK_STATUS_ORDER,
  type VisibleTaskStatus,
} from "../utils/taskStatus";

export function toTaskStateKey(workflowUid: string, taskId: number): string {
  return `${workflowUid}:${taskId}`;
}

export type WorkflowTaskSummary = {
  workflowUid: string;
  personName: string;
  departmentName: string;
  departmentId: number;
  roleName: string;
  counts: Record<VisibleTaskStatus, number>;
  totalCount: number;
};

export type TaskGroup = {
  status: VisibleTaskStatus;
  items: TaskWithWorkflow[];
};

export function useMyTasksPageView() {
  const myTasksQuery = useMyTasks();
  const {
    savingTaskIds,
    savingApprovalTaskIds,
    commentDrafts,
    savingCommentTaskIds,
    handleStatusChange,
    handleApprovalDecision,
    handleCommentDraftChange,
    handleTaskCommentSubmit,
  } = useTaskInteraction();

  const [search, setSearch] = useState<string>("");
  const [departmentFilter, setDepartmentFilter] = useState<string>("all");
  const [selectedWorkflowUid, setSelectedWorkflowUid] = useState<string | null>(null);
  const [expandedStatuses, setExpandedStatuses] = useState<Set<VisibleTaskStatus>>(
    new Set(["open", "in_progress", "blocked"])
  );

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

  // Alle Tasks nach Workflow-UID gruppiert
  const tasksByWorkflow = useMemo(() => {
    const byUid = new Map<string, TaskWithWorkflow[]>();
    for (const row of rows) {
      const uid = row.workflow.workflowUid;
      if (!byUid.has(uid)) {
        byUid.set(uid, []);
      }
      byUid.get(uid)!.push(row);
    }
    return byUid;
  }, [rows]);

  // Workflow-Übersichten (Zusammenfassungen)
  const allWorkflowSummaries = useMemo<WorkflowTaskSummary[]>(() => {
    return Array.from(tasksByWorkflow.entries()).map(([workflowUid, tasks]) => {
      const first = tasks[0]!;
      const personName = `${first.workflow.firstName} ${first.workflow.lastName}`.trim() || "Unbekannter Mitarbeitender";
      const counts: Record<VisibleTaskStatus, number> = { open: 0, in_progress: 0, blocked: 0, done: 0 };
      for (const t of tasks) {
        counts[getVisibleTaskStatus(t.task.status)] += 1;
      }
      return {
        workflowUid,
        personName,
        departmentName: first.workflow.departmentName,
        departmentId: first.workflow.departmentId,
        roleName: first.workflow.roleName,
        counts,
        totalCount: tasks.length,
      };
    });
  }, [tasksByWorkflow]);

  // Gefilterte Workflow-Übersichten
  const filteredWorkflowSummaries = useMemo(() => {
    const normalizedSearch = search.trim().toLowerCase();
    return allWorkflowSummaries
      .filter((summary) => {
        const matchesDepartment = departmentFilter === "all" || String(summary.departmentId) === departmentFilter;
        if (!matchesDepartment) return false;
        if (!normalizedSearch) return true;
        return (
          summary.personName.toLowerCase().includes(normalizedSearch) ||
          summary.workflowUid.toLowerCase().includes(normalizedSearch) ||
          summary.departmentName.toLowerCase().includes(normalizedSearch)
        );
      })
      .sort((left, right) => {
        const leftOpen = left.counts.open + left.counts.in_progress + left.counts.blocked;
        const rightOpen = right.counts.open + right.counts.in_progress + right.counts.blocked;
        // Workflows mit offenen Aufgaben zuerst
        if (leftOpen !== rightOpen) return rightOpen - leftOpen;
        return left.personName.localeCompare(right.personName, "de");
      });
  }, [allWorkflowSummaries, search, departmentFilter]);

  // Aufgaben des ausgewählten Workflows, sortiert und nach Status gruppiert
  const selectedWorkflowTasks = useMemo<TaskWithWorkflow[]>(() => {
    if (!selectedWorkflowUid) return [];
    const tasks = tasksByWorkflow.get(selectedWorkflowUid) ?? [];
    return [...tasks].sort((left, right) => {
      const leftStatus = left.task.status;
      const rightStatus = right.task.status;
      const statusDelta = TASK_STATUS_ORDER.indexOf(leftStatus) - TASK_STATUS_ORDER.indexOf(rightStatus);
      if (statusDelta !== 0) return statusDelta;
      return left.task.sortOrder - right.task.sortOrder || left.task.id - right.task.id;
    });
  }, [selectedWorkflowUid, tasksByWorkflow]);

  const selectedWorkflowGroups = useMemo<TaskGroup[]>(() => {
    const groups = VISIBLE_TASK_STATUS_ORDER.map((status) => ({
      status,
      items: [] as TaskWithWorkflow[],
    }));
    const itemsByStatus = new Map(groups.map((group) => [group.status, group.items]));
    for (const row of selectedWorkflowTasks) {
      itemsByStatus.get(getVisibleTaskStatus(row.task.status))?.push(row);
    }
    return groups.filter((group) => group.items.length > 0);
  }, [selectedWorkflowTasks]);

  // Legacy-Kompatibilität für bestehende responsibility-Filter-Hilfen (werden nicht mehr im UI verwendet, aber Interaktionshooks brauchen sie ggf.)
  const responsibilityOptions = useMemo(() => {
    const optionsByValue = new Map(
      rows.map((row) => {
        const option = getResponsibleResponsibilityFilterOption(row.task);
        return [option.value, option] as const;
      })
    );
    return Array.from(optionsByValue.values()).sort((left, right) => left.label.localeCompare(right.label, "de"));
  }, [rows]);

  const toggleStatus = useCallback((status: VisibleTaskStatus) => {
    setExpandedStatuses((current) => {
      const next = new Set(current);
      if (next.has(status)) {
        next.delete(status);
      } else {
        next.add(status);
      }
      return next;
    });
  }, []);

  const selectedWorkflowSummary = useMemo(() => {
    if (!selectedWorkflowUid) return null;
    return allWorkflowSummaries.find((s) => s.workflowUid === selectedWorkflowUid) ?? null;
  }, [selectedWorkflowUid, allWorkflowSummaries]);

  // Für Abwärtskompatibilität – wird von MyTaskGroups noch genutzt
  const filteredRows = useMemo(() => {
    const normalizedSearch = search.trim().toLowerCase();
    return rows
      .filter((row) => {
        const workflowUid = row.workflow.workflowUid;
        const workflowDisplayName =
          `${row.workflow.firstName} ${row.workflow.lastName}`.trim() || "Unbekannter Mitarbeitender";
        const matchesDepartment = departmentFilter === "all" || String(row.workflow.departmentId) === departmentFilter;
        if (!matchesDepartment) return false;
        if (!normalizedSearch) return true;
        return (
          workflowDisplayName.toLowerCase().includes(normalizedSearch) ||
          String(row.workflow.employeeNumber).includes(normalizedSearch) ||
          workflowUid.toLowerCase().includes(normalizedSearch) ||
          row.task.title.toLowerCase().includes(normalizedSearch) ||
          row.task.taskKey.toLowerCase().includes(normalizedSearch)
        );
      })
      .sort((left, right) => {
        const statusDelta = TASK_STATUS_ORDER.indexOf(left.task.status) - TASK_STATUS_ORDER.indexOf(right.task.status);
        if (statusDelta !== 0) return statusDelta;
        return left.task.sortOrder - right.task.sortOrder || left.task.id - right.task.id;
      });
  }, [rows, search, departmentFilter]);

  return {
    // Filter
    search,
    departmentFilter,
    departmentOptions,
    responsibilityOptions,
    setSearch,
    setDepartmentFilter,
    // Daten
    rows,
    filteredRows,
    // Übersichts-Ebene
    filteredWorkflowSummaries,
    // Detail-Ebene
    selectedWorkflowUid,
    setSelectedWorkflowUid,
    selectedWorkflowSummary,
    selectedWorkflowGroups,
    expandedStatuses,
    toggleStatus,
    // Zustand
    isLoading,
    isRefreshing,
    error,
    // Aufgaben-Interaktion
    savingTaskIds,
    savingApprovalTaskIds,
    commentDrafts,
    savingCommentTaskIds,
    handleStatusChange,
    handleApprovalDecision,
    handleCommentDraftChange,
    handleTaskCommentSubmit,
    reload,
  };
}
