import { useQuery } from "@tanstack/react-query";
import type { DashboardPersona } from "../../auth/roleModel";
import type { ProcessType } from "../../types/workflow";
import { loadDashboardInsights } from "../../components/dashboard/dashboardInsights";
import { queryKeys } from "../queryKeys";

export function useDashboardInsightsQuery(
  dashboardPersona: DashboardPersona,
  processTypeKey?: string | null,
  selectedProcessType?: ProcessType | null
) {
  return useQuery({
    queryKey: queryKeys.dashboard.insights(dashboardPersona, processTypeKey),
    queryFn: () => loadDashboardInsights(dashboardPersona, { processTypeKey, selectedProcessType }),
    staleTime: 30 * 1000,
  });
}
