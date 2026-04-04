import type { DashboardPersona } from "../../auth/roleModel";
import {
  loadAdminInsights,
  loadGenericInsights,
  loadHrInsights,
  loadManagerInsights,
  loadViewerInsights,
  loadWorkerInsights,
} from "./dashboardInsightsLoaders";
import type { DashboardInsights, DashboardInsightsOptions } from "./dashboardInsights.shared";

export type {
  DashboardEmployeeItem,
  DashboardInsights,
  DashboardInsightsOptions,
  DashboardQueueItem,
  DashboardStat,
} from "./dashboardInsights.shared";

export async function loadDashboardInsights(
  dashboardPersona: DashboardPersona,
  options: DashboardInsightsOptions = {}
): Promise<DashboardInsights> {
  switch (dashboardPersona) {
    case "admin":
      return loadAdminInsights(options);
    case "hr":
      return loadHrInsights(options);
    case "manager":
      return loadManagerInsights(options);
    case "worker":
      return loadWorkerInsights();
    case "reader":
      return loadViewerInsights(options);
    default:
      return loadGenericInsights();
  }
}
