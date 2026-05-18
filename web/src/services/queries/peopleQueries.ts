import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { getPerson360View, getPersonWorkflowHistory, getPeopleDirectory, searchPeople, searchRotationEligiblePeople } from "../peopleApi";
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

// Slice 4: 360°-Karte. Lazy-Fetch on Tab-Activate via enabled-Param —
// kein Polling, staleTime 30s reicht fuer Read-only-Detail.
export function usePerson360View(personId: number | null, enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.people.view360(personId ?? 0),
    queryFn: () => getPerson360View(personId as number),
    enabled: enabled && typeof personId === "number" && personId > 0,
    staleTime: 30 * 1000,
  });
}

export function usePeopleDirectory(search: string, offset = 0, pageSize = 50) {
  return useQuery({
    queryKey: queryKeys.people.directory(search, offset),
    queryFn: () => getPeopleDirectory({ search, limit: pageSize, offset }),
    staleTime: 30 * 1000,
    placeholderData: keepPreviousData,
  });
}
