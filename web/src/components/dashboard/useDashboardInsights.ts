import { useCallback, useEffect, useState } from "react";
import type { DashboardPersona } from "../../auth/roleModel";
import { loadDashboardInsights, type DashboardInsights } from "./dashboardInsights";

export function useDashboardInsights(dashboardPersona: DashboardPersona, processTypeKey?: string | null) {
  const [insights, setInsights] = useState<DashboardInsights | null>(null);
  const [isInsightsLoading, setIsInsightsLoading] = useState<boolean>(true);
  const [insightsError, setInsightsError] = useState<string | null>(null);

  const reloadInsights = useCallback(async () => {
    setIsInsightsLoading(true);
    setInsightsError(null);

    try {
      const nextInsights = await loadDashboardInsights(dashboardPersona, { processTypeKey });
      setInsights(nextInsights);
    } catch (error) {
      const message = error instanceof Error ? error.message : "Übersichtsdaten konnten nicht geladen werden.";
      setInsights(null);
      setInsightsError(message);
    } finally {
      setIsInsightsLoading(false);
    }
  }, [dashboardPersona, processTypeKey]);

  useEffect(() => {
    void reloadInsights();
  }, [reloadInsights]);

  return {
    insights,
    insightsError,
    isInsightsLoading,
    reloadInsights,
  };
}
