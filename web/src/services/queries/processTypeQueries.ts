import { useQuery } from "@tanstack/react-query";
import { getProcessTypes, getStartableWorkflowDefinitions } from "../lookupApi";
import { queryKeys } from "../queryKeys";

export function useProcessTypes() {
  return useQuery({
    queryKey: queryKeys.processTypes(),
    queryFn: getProcessTypes,
    staleTime: 5 * 60 * 1000,
  });
}

export function useStartableWorkflowDefinitions() {
  return useQuery({
    queryKey: queryKeys.workflowDefinitions.startable(),
    queryFn: getStartableWorkflowDefinitions,
    staleTime: 30 * 1000,
  });
}
