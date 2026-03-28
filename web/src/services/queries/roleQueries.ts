import { useQuery } from "@tanstack/react-query";
import { getDepartments, getRoles } from "../lookupApi";
import { queryKeys } from "../queryKeys";

export function useRoles() {
  return useQuery({
    queryKey: queryKeys.roles(),
    queryFn: getRoles,
    staleTime: 5 * 60 * 1000,
  });
}

export function useDepartments() {
  return useQuery({
    queryKey: queryKeys.departments(),
    queryFn: getDepartments,
    staleTime: 5 * 60 * 1000,
  });
}
