import { useMemo, useState } from "react";
import { useCurrentUser } from "../auth/useCurrentUser";
import { useMyTasks } from "../services/queries/workflowQueries";
import type { TaskWithWorkflow } from "../types/workflow";
import {
  getResponsibleResponsibilityLabel,
} from "../utils/taskAssignment";
import {
  getVisibleTaskStatus,
  type VisibleTaskStatus,
} from "../utils/taskStatus";

export type RotationTaskRow = TaskWithWorkflow & {
  taskFamily: "rotation";
  rotation: NonNullable<TaskWithWorkflow["rotation"]>;
};

export type UpcomingChangeSummary = {
  key: string;
  rotationPlanId: number;
  personName: string;
  departmentName: string;
  triggerType: "enter" | "exit" | null;
  anchorDate: string | null;
  taskCount: number;
  taskRefs: string[];
};

export type DepartmentSummary = {
  departmentName: string;
  count: number;
  openCount: number;
};

export type PersonSummary = {
  planId: number;
  personName: string;
  departmentName: string;
  planTitle: string;
  taskCount: number;
  openCount: number;
  nextAnchorDate: string | null;
  firstTaskRef: string;
};

export type RotationOperationsFilters = {
  search: string;
  departmentFilter: string;
  statusFilter: "all" | VisibleTaskStatus;
  changeWindowDays: string;
  onlyOpen: boolean;
  onlyItTasks: boolean;
  onlyOwnDepartment: boolean;
};

export type RotationOperationsFilterSetters = {
  setSearch: (value: string) => void;
  setDepartmentFilter: (value: string) => void;
  setStatusFilter: (value: "all" | VisibleTaskStatus) => void;
  setChangeWindowDays: (value: string) => void;
  setOnlyOpen: (value: boolean) => void;
  setOnlyItTasks: (value: boolean) => void;
  setOnlyOwnDepartment: (value: boolean) => void;
};

export function isOperationallyOpen(row: RotationTaskRow): boolean {
  return row.task.status === "open" || row.task.status === "in_progress";
}

export function isUpcomingAnchorDate(anchorDate: string | null, maxDays: number): boolean {
  if (!anchorDate) {
    return false;
  }

  const today = new Date();
  const midnightToday = new Date(today.getFullYear(), today.getMonth(), today.getDate()).getTime();
  const anchor = new Date(anchorDate).getTime();
  if (Number.isNaN(anchor)) {
    return false;
  }

  const daysUntil = Math.floor((anchor - midnightToday) / (24 * 60 * 60 * 1000));
  return daysUntil >= 0 && daysUntil <= maxDays;
}

export function useRotationOperationsView() {
  const { currentUser } = useCurrentUser();
  const myTasksQuery = useMyTasks();

  const [search, setSearch] = useState("");
  const [departmentFilter, setDepartmentFilter] = useState("all");
  const [statusFilter, setStatusFilter] = useState<"all" | VisibleTaskStatus>("all");
  const [changeWindowDays, setChangeWindowDays] = useState("14");
  const [onlyOpen, setOnlyOpen] = useState(true);
  const [onlyItTasks, setOnlyItTasks] = useState(false);
  const [onlyOwnDepartment, setOnlyOwnDepartment] = useState(false);

  const rotationRows = useMemo<RotationTaskRow[]>(
    () =>
      (myTasksQuery.data ?? []).filter(
        (row): row is RotationTaskRow => row.taskFamily === "rotation" && row.rotation !== null
      ),
    [myTasksQuery.data]
  );

  const departmentOptions = useMemo(
    () =>
      Array.from(
        new Map(
          rotationRows.map((row) => [row.rotation.departmentId, row.rotation.departmentName] as const)
        ).entries()
      ).sort((left, right) => left[1].localeCompare(right[1], "de")),
    [rotationRows]
  );

  const inferredOwnDepartmentId = useMemo(() => {
    const scopedDepartmentId = currentUser?.permissionScopes.find(
      (scope) => typeof scope.scopeDepartmentId === "number"
    )?.scopeDepartmentId;
    return scopedDepartmentId ?? null;
  }, [currentUser]);

  const filteredRows = useMemo(() => {
    const normalizedSearch = search.trim().toLowerCase();

    return rotationRows
      .filter((row) => {
        const visibleStatus = getVisibleTaskStatus(row.task.status);
        const matchesDepartment =
          departmentFilter === "all" || String(row.rotation.departmentId) === departmentFilter;
        const matchesStatus = statusFilter === "all" || visibleStatus === statusFilter;
        const matchesOpen = !onlyOpen || isOperationallyOpen(row);
        const matchesIt =
          !onlyItTasks || getResponsibleResponsibilityLabel(row.task).toLowerCase().includes("it");
        const matchesOwnDepartment =
          !onlyOwnDepartment
          || inferredOwnDepartmentId === null
          || row.rotation.departmentId === inferredOwnDepartmentId;
        const matchesSearch =
          !normalizedSearch
          || row.rotation.displayName.toLowerCase().includes(normalizedSearch)
          || row.rotation.planTitle.toLowerCase().includes(normalizedSearch)
          || row.task.title.toLowerCase().includes(normalizedSearch)
          || row.taskRef.toLowerCase().includes(normalizedSearch)
          || row.rotation.departmentName.toLowerCase().includes(normalizedSearch);

        return (
          matchesDepartment
          && matchesStatus
          && matchesOpen
          && matchesIt
          && matchesOwnDepartment
          && matchesSearch
        );
      })
      .sort((left, right) => {
        const leftAnchor = left.rotation.anchorDate
          ? new Date(left.rotation.anchorDate).getTime()
          : Number.MAX_SAFE_INTEGER;
        const rightAnchor = right.rotation.anchorDate
          ? new Date(right.rotation.anchorDate).getTime()
          : Number.MAX_SAFE_INTEGER;
        if (leftAnchor !== rightAnchor) {
          return leftAnchor - rightAnchor;
        }

        return left.rotation.displayName.localeCompare(right.rotation.displayName, "de");
      });
  }, [
    departmentFilter,
    inferredOwnDepartmentId,
    onlyItTasks,
    onlyOpen,
    onlyOwnDepartment,
    rotationRows,
    search,
    statusFilter,
  ]);

  const upcomingChanges = useMemo<UpcomingChangeSummary[]>(() => {
    const grouped = new Map<string, UpcomingChangeSummary>();
    const daysWindow = Math.max(Number(changeWindowDays), 0);

    for (const row of filteredRows) {
      if (!isOperationallyOpen(row) || !isUpcomingAnchorDate(row.rotation.anchorDate, daysWindow)) {
        continue;
      }

      const key = `${row.rotation.rotationPlanId}:${row.rotation.anchorDate ?? "none"}:${row.rotation.triggerType ?? "none"}`;
      const existing = grouped.get(key);
      if (existing) {
        existing.taskCount += 1;
        existing.taskRefs.push(row.taskRef);
        continue;
      }

      grouped.set(key, {
        key,
        rotationPlanId: row.rotation.rotationPlanId,
        personName: row.rotation.displayName,
        departmentName: row.rotation.departmentName,
        triggerType: row.rotation.triggerType,
        anchorDate: row.rotation.anchorDate,
        taskCount: 1,
        taskRefs: [row.taskRef],
      });
    }

    return Array.from(grouped.values()).sort((left, right) => {
      const leftAnchor = left.anchorDate ? new Date(left.anchorDate).getTime() : Number.MAX_SAFE_INTEGER;
      const rightAnchor = right.anchorDate ? new Date(right.anchorDate).getTime() : Number.MAX_SAFE_INTEGER;
      return leftAnchor - rightAnchor;
    });
  }, [changeWindowDays, filteredRows]);

  const departmentSummaries = useMemo<DepartmentSummary[]>(() => {
    const grouped = new Map<string, DepartmentSummary>();
    for (const row of filteredRows) {
      const key = `${row.rotation.departmentId}`;
      const existing = grouped.get(key) ?? {
        departmentName: row.rotation.departmentName,
        count: 0,
        openCount: 0,
      };
      existing.count += 1;
      if (isOperationallyOpen(row)) {
        existing.openCount += 1;
      }
      grouped.set(key, existing);
    }

    return Array.from(grouped.values()).sort((left, right) =>
      left.departmentName.localeCompare(right.departmentName, "de")
    );
  }, [filteredRows]);

  const personSummaries = useMemo<PersonSummary[]>(() => {
    const grouped = new Map<string, PersonSummary>();

    for (const row of filteredRows) {
      const key = `${row.rotation.rotationPlanId}`;
      const existing = grouped.get(key) ?? {
        planId: row.rotation.rotationPlanId,
        personName: row.rotation.displayName,
        departmentName: row.rotation.departmentName,
        planTitle: row.rotation.planTitle,
        taskCount: 0,
        openCount: 0,
        nextAnchorDate: row.rotation.anchorDate,
        firstTaskRef: row.taskRef,
      };
      existing.taskCount += 1;
      if (isOperationallyOpen(row)) {
        existing.openCount += 1;
      }
      if (
        row.rotation.anchorDate
        && (!existing.nextAnchorDate
          || new Date(row.rotation.anchorDate).getTime() < new Date(existing.nextAnchorDate).getTime())
      ) {
        existing.nextAnchorDate = row.rotation.anchorDate;
      }
      grouped.set(key, existing);
    }

    return Array.from(grouped.values()).sort((left, right) => {
      if (left.openCount !== right.openCount) {
        return right.openCount - left.openCount;
      }

      return left.personName.localeCompare(right.personName, "de");
    });
  }, [filteredRows]);

  const filters: RotationOperationsFilters = {
    search,
    departmentFilter,
    statusFilter,
    changeWindowDays,
    onlyOpen,
    onlyItTasks,
    onlyOwnDepartment,
  };

  const setters: RotationOperationsFilterSetters = {
    setSearch,
    setDepartmentFilter,
    setStatusFilter,
    setChangeWindowDays,
    setOnlyOpen,
    setOnlyItTasks,
    setOnlyOwnDepartment,
  };

  return {
    myTasksQuery,
    rotationRows,
    departmentOptions,
    inferredOwnDepartmentId,
    filteredRows,
    upcomingChanges,
    departmentSummaries,
    personSummaries,
    filters,
    setters,
  };
}
