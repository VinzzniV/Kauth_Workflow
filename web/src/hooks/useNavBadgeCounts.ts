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
  });

  const supervisorQuery = useQuery({
    queryKey: queryKeys.supervisorWorkflows(),
    queryFn: getSupervisorStepWorkflows,
    staleTime: 60 * 1000,
    enabled: canSeeSupervisor,
    select: (workflows) => workflows.length,
  });

  const allTasks = myTasksQuery.data ?? [];
  const openWorkflowCount = allTasks.filter(
    (item) => item.taskFamily === "workflow" && isOpenTask(item.task)
  ).length;
  const openRotationCount = allTasks.filter(
    (item) => item.taskFamily === "rotation" && isOpenTask(item.task)
  ).length;

  const counts: Record<string, number> = {};

  if (canSeeTasks && openWorkflowCount > 0) {
    counts["/tasks/my"] = openWorkflowCount;
  }

  if (canSeeTasks && openRotationCount > 0) {
    counts["/rotation/operations"] = openRotationCount;
  }

  if (canSeeSupervisor && (supervisorQuery.data ?? 0) > 0) {
    counts["/supervisor"] = supervisorQuery.data!;
  }

  return counts;
}
