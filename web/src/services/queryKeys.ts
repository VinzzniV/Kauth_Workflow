import type { DashboardPersona } from "../auth/roleModel";
import type { WorkflowQueryOptions } from "./workflowApi";

export const queryKeys = {
  processTypes: () => ["process-types"] as const,
  roles: () => ["roles"] as const,
  departments: () => ["departments"] as const,

  workflows: {
    all: () => ["workflows"] as const,
    list: (options: WorkflowQueryOptions, page: number, pageSize: number) =>
      ["workflows", "list", options, page, pageSize] as const,
    detail: (uid: string) => ["workflows", uid] as const,
    config: (roleId: number | null, processTypeKey: string | null) =>
      ["workflows", "config", roleId ?? null, processTypeKey ?? null] as const,
    completedOnboardings: (search: string) => ["workflows", "completed-onboardings", search] as const,
    tasks: (uid: string) => ["workflows", uid, "tasks"] as const,
    related: (uid: string) => ["workflows", uid, "related"] as const,
    auditLog: (uid: string, limit: number, offset: number) =>
      ["workflows", uid, "audit", limit, offset] as const,
    supervisorStep: (uid: string) => ["workflows", uid, "supervisor-step"] as const,
  },

  myTasks: () => ["my-tasks"] as const,

  dashboard: {
    all: () => ["dashboard"] as const,
    insights: (dashboardPersona: DashboardPersona, processTypeKey: string | null | undefined) =>
      ["dashboard", "insights", dashboardPersona, processTypeKey ?? null] as const,
  },

  admin: {
    users: () => ["admin", "users"] as const,
    roles: () => ["admin", "roles"] as const,
    groups: () => ["admin", "groups"] as const,
    departmentAssignments: () => ["admin", "department-assignments"] as const,
    responsibilityOwners: () => ["admin", "responsibility-owners"] as const,
    notificationEmail: () => ["admin", "notification-email"] as const,
    workflowConfig: () => ["admin", "workflow-config"] as const,
  },
};
