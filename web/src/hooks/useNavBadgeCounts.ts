import { useQuery } from "@tanstack/react-query";
import { useCurrentUser } from "../auth/useCurrentUser";
import { getMyTasks } from "../services/taskApi";
import { getSupervisorStepWorkflows } from "../services/workflowApi";
import { queryKeys } from "../services/queryKeys";
import { isOpenTask } from "../components/dashboard/dashboardInsights.shared";

export type NavBadgeCounts = Readonly<Record<string, number>>;

export function useNavBadgeCounts(): NavBadgeCounts {
  const { canAccessFeature } = useCurrentUser();

  const canSeeTasks = canAccessFeature("technicalTasks");
  const canSeeSupervisor = canAccessFeature("supervisorStep");

  const myTasksQuery = useQuery({
    queryKey: queryKeys.myTasks(),
    queryFn: getMyTasks,
    staleTime: 60 * 1000,
    enabled: canSeeTasks,
    select: (tasks) => tasks.filter((item) => isOpenTask(item.task)).length,
  });

  const supervisorQuery = useQuery({
    queryKey: queryKeys.supervisorWorkflows(),
    queryFn: getSupervisorStepWorkflows,
    staleTime: 60 * 1000,
    enabled: canSeeSupervisor,
    select: (workflows) => workflows.length,
  });

  const counts: Record<string, number> = {};

  if (canSeeTasks && (myTasksQuery.data ?? 0) > 0) {
    counts["/tasks/my"] = myTasksQuery.data!;
  }

  if (canSeeSupervisor && (supervisorQuery.data ?? 0) > 0) {
    counts["/supervisor"] = supervisorQuery.data!;
  }

  return counts;
}
