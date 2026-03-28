import type { DashboardPersona } from "../../auth/roleModel";
import type { ProcessType } from "../../types/workflow";
import { useDashboardInsightsQuery } from "../../services/queries/dashboardQueries";

export function useDashboardInsights(
  dashboardPersona: DashboardPersona,
  processTypeKey?: string | null,
  selectedProcessType?: ProcessType | null
) {
  const query = useDashboardInsightsQuery(dashboardPersona, processTypeKey, selectedProcessType);

  return {
    insights: query.data ?? null,
    insightsError: query.error instanceof Error ? query.error.message : query.error ? "Übersichtsdaten konnten nicht geladen werden." : null,
    isInsightsLoading: query.isLoading,
    reloadInsights: query.refetch,
  };
}
