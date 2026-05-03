import { useQuery } from "@tanstack/react-query";
import { getStartableWorkflowDefinitions } from "../lookupApi";
import { queryKeys } from "../queryKeys";

export function useStartableWorkflowDefinitions() {
  return useQuery({
    queryKey: queryKeys.workflowDefinitions.startable(),
    queryFn: getStartableWorkflowDefinitions,
    staleTime: 30 * 1000,
  });
}
