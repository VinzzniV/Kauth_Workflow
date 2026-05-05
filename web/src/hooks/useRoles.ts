import { useCallback, useEffect, useMemo, useState } from "react";
import { getDepartments, getRoles } from "../services/lookupApi";
import type { Department, Role } from "../types/workflow";

type UseRolesResult = {
  roles: Role[];
  departments: Department[];
  isLoading: boolean;
  error: string | null;
  reload: () => Promise<void>;
  groupedByDepartment: Record<number, Role[]>;
};

export function useRoles(): UseRolesResult {
  const [roles, setRoles] = useState<Role[]>([]);
  const [departments, setDepartments] = useState<Department[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  const reload = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const [rolePage, departmentPage] = await Promise.all([
        getRoles({ limit: 200 }),
        getDepartments({ limit: 200 }),
      ]);
      const activeRoles = rolePage.items.filter((role) => role.isActive);

      setRoles(activeRoles);
      setDepartments(departmentPage.items);
      console.info("[roles] loaded", {
        roles: activeRoles.length,
        departments: departmentPage.items.length,
      });
    } catch (err) {
      const message = err instanceof Error ? err.message : "Die Rollen konnten nicht geladen werden.";
      setError(message);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void reload();
  }, [reload]);

  const groupedByDepartment = useMemo(() => {
    return roles.reduce<Record<number, Role[]>>((acc, role) => {
      const key = role.departmentId;
      const existing = acc[key] ?? [];
      acc[key] = [...existing, role];
      return acc;
    }, {});
  }, [roles]);

  return {
    roles,
    departments,
    isLoading,
    error,
    reload,
    groupedByDepartment,
  };
}
