import { useQuery } from "@tanstack/react-query";
import { getStartableWorkflowDefinitions } from "../lookupApi";
import { queryKeys } from "../queryKeys";

type StartableWorkflowDefinitionsOptions = {
  enabled?: boolean;
};

export function useStartableWorkflowDefinitions(options: StartableWorkflowDefinitionsOptions = {}) {
  return useQuery({
    queryKey: queryKeys.workflowDefinitions.startable(),
    queryFn: () => getStartableWorkflowDefinitions(),
    staleTime: 30 * 1000,
    enabled: options.enabled ?? true,
  });
}
