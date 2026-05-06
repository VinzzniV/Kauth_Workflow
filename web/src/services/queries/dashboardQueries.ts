import { useQuery } from "@tanstack/react-query";
import type { DashboardPersona } from "../../auth/roleModel";
import type { StartableWorkflowDefinition } from "../../types/workflow";
import { loadDashboardInsights } from "../../components/dashboard/dashboardInsights";
import { queryKeys } from "../queryKeys";

export function useDashboardInsightsQuery(
  dashboardPersona: DashboardPersona,
  workflowDefinitionKey?: string | null,
  selectedWorkflowDefinition?: StartableWorkflowDefinition | null
) {
  return useQuery({
    queryKey: queryKeys.dashboard.insights(dashboardPersona, workflowDefinitionKey),
    queryFn: () => loadDashboardInsights(dashboardPersona, { workflowDefinitionKey, selectedWorkflowDefinition }),
    placeholderData: (previousData) => previousData,
    staleTime: 30 * 1000,
  });
}
