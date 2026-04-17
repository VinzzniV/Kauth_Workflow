import { keepPreviousData, useQuery } from "@tanstack/react-query";
import {
  getRotationAuditLog,
  getRotationGeneratedTasks,
  getRotationNotifications,
  getRotationPlan,
  getRotationPlans,
  searchCompletedRotationOnboardings,
} from "../rotationApi";
import { queryKeys } from "../queryKeys";

export function useRotationCompletedOnboardings(search: string, enabled = true) {
  return useQuery({
    queryKey: queryKeys.rotation.completedOnboardings(search),
    queryFn: () => searchCompletedRotationOnboardings(search),
    enabled,
    staleTime: 30 * 1000,
    placeholderData: keepPreviousData,
  });
}

export function useRotationPlans(personId: number | null, enabled = true) {
  return useQuery({
    queryKey: queryKeys.rotation.plans(personId),
    queryFn: () => getRotationPlans(personId as number),
    enabled: enabled && typeof personId === "number" && personId > 0,
    staleTime: 15 * 1000,
    placeholderData: keepPreviousData,
  });
}

export function useRotationPlanDetail(planId: number | null, enabled = true) {
  return useQuery({
    queryKey: queryKeys.rotation.planDetail(planId),
    queryFn: () => getRotationPlan(planId as number),
    enabled: enabled && typeof planId === "number" && planId > 0,
    staleTime: 0,
    refetchOnWindowFocus: true,
  });
}

export function useRotationGeneratedTasks(planId: number | null, enabled = true) {
  return useQuery({
    queryKey: queryKeys.rotation.generatedTasks(planId),
    queryFn: () => getRotationGeneratedTasks(planId as number),
    enabled: enabled && typeof planId === "number" && planId > 0,
    staleTime: 15 * 1000,
    placeholderData: keepPreviousData,
  });
}

export function useRotationAuditLog(
  planId: number | null,
  limit = 50,
  offset = 0,
  enabled = true
) {
  return useQuery({
    queryKey: queryKeys.rotation.auditLog(planId, limit, offset),
    queryFn: () => getRotationAuditLog(planId as number, limit, offset),
    enabled: enabled && typeof planId === "number" && planId > 0,
    staleTime: 0,
    refetchOnWindowFocus: true,
  });
}

export function useRotationNotifications(
  planId: number | null,
  limit = 50,
  offset = 0,
  enabled = true
) {
  return useQuery({
    queryKey: queryKeys.rotation.notifications(planId, limit, offset),
    queryFn: () => getRotationNotifications(planId as number, limit, offset),
    enabled: enabled && typeof planId === "number" && planId > 0,
    staleTime: 15 * 1000,
    placeholderData: keepPreviousData,
  });
}
