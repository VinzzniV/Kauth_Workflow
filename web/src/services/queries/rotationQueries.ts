import { keepPreviousData, useQuery } from "@tanstack/react-query";
import {
  getAdminRotationTemplates,
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
    queryKey: queryKeys.people.rotationEligible(search),
    queryFn: () => searchCompletedRotationOnboardings(search),
    enabled,
    staleTime: 30 * 1000,
    placeholderData: keepPreviousData,
  });
}

export function useRotationPlans(personId: number | null, enabled = true) {
  return useQuery({
    queryKey: queryKeys.rotation.plans(personId),
    queryFn: () => getRotationPlans(personId),
    enabled,
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

export function useAdminRotationTemplates(
  departmentId: number | null = null,
  isActive: boolean | null = null,
  enabled = true
) {
  return useQuery({
    queryKey: queryKeys.rotation.adminTemplates(departmentId, isActive),
    queryFn: () => getAdminRotationTemplates(departmentId, isActive),
    enabled,
    staleTime: 30 * 1000,
    placeholderData: keepPreviousData,
  });
}
