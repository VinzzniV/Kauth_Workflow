import type { DashboardPersona } from "../../auth/roleModel";
import type { StartableWorkflowDefinition } from "../../types/workflow";
import { useDashboardInsightsQuery } from "../../services/queries/dashboardQueries";

export function useDashboardInsights(
  dashboardPersona: DashboardPersona,
  workflowDefinitionKey?: string | null,
  selectedWorkflowDefinition?: StartableWorkflowDefinition | null
) {
  const query = useDashboardInsightsQuery(dashboardPersona, workflowDefinitionKey, selectedWorkflowDefinition);

  return {
    insights: query.data ?? null,
    insightsError: query.error instanceof Error ? query.error.message : query.error ? "Übersichtsdaten konnten nicht geladen werden." : null,
    isInsightsLoading: query.isLoading,
    reloadInsights: query.refetch,
  };
}
