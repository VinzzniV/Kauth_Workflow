import { useCallback, useEffect, useMemo, useState } from "react";
import { getWorkflows } from "../services/lifecycleApi";
import type { WorkflowRuntimeStatus, WorkflowSummary } from "../types/workflow";
import { matchesWorkflowRuntimeStatusFilter } from "../utils/workflowStatus";

export type Row = WorkflowSummary;

export type UseRowsOptions = {
  autoLoad?: boolean;
};

export function useRows(options: UseRowsOptions = {}) {
  const { autoLoad = true } = options;
  const [rows, setRows] = useState<WorkflowSummary[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState<string>("");
  const [statusFilter, setStatusFilter] = useState<"all" | WorkflowRuntimeStatus>("all");

  const reload = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const workflows = await getWorkflows();
      setRows(workflows);
      console.info("[workflows] loaded", workflows.length);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Vorgänge konnten nicht geladen werden.";
      setError(message);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    if (autoLoad) {
      void reload();
    }
  }, [autoLoad, reload]);

  const filteredRows = useMemo(() => {
    return rows.filter((row) => {
      const matchesStatus = matchesWorkflowRuntimeStatusFilter(row.workflowStatus, statusFilter);
      const searchText = search.trim().toLowerCase();

      if (!searchText) {
        return matchesStatus;
      }

      const fullName = `${row.firstName} ${row.lastName}`.toLowerCase();
      return (
        matchesStatus &&
        (fullName.includes(searchText) ||
          String(row.employeeNumber).includes(searchText) ||
          row.roleName.toLowerCase().includes(searchText) ||
          row.uid.toLowerCase().includes(searchText))
      );
    });
  }, [rows, search, statusFilter]);

  return {
    rows,
    filteredRows,
    isLoading,
    error,
    search,
    statusFilter,
    setSearch,
    setStatusFilter,
    reload,
  };
}
