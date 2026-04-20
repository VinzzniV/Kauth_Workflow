import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { getPersonWorkflowHistory, searchPeople, searchRotationEligiblePeople } from "../peopleApi";
import { queryKeys } from "../queryKeys";

export function usePeopleSearch(search: string, enabled = true) {
  return useQuery({
    queryKey: queryKeys.people.search(search),
    queryFn: () => searchPeople(search),
    enabled,
    staleTime: 30 * 1000,
    placeholderData: keepPreviousData,
  });
}

export function useRotationEligiblePeople(search: string, enabled = true) {
  return useQuery({
    queryKey: queryKeys.people.rotationEligible(search),
    queryFn: () => searchRotationEligiblePeople(search),
    enabled,
    staleTime: 30 * 1000,
    placeholderData: keepPreviousData,
  });
}

export function usePersonWorkflowHistory(personId: number | null, enabled = true) {
  return useQuery({
    queryKey: queryKeys.people.history(personId ?? 0),
    queryFn: () => getPersonWorkflowHistory(personId as number),
    enabled: enabled && typeof personId === "number" && personId > 0,
    staleTime: 15 * 1000,
  });
}
