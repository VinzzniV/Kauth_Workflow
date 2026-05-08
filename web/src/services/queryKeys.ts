import type { DashboardPersona } from "../auth/roleModel";
import type { WorkflowQueryOptions } from "./workflowApi";

export const queryKeys = {
  workflowDefinitions: {
    startable: () => ["workflow-definitions", "startable"] as const,
  },
  roles: () => ["roles"] as const,
  departments: () => ["departments"] as const,
  people: {
    search: (search: string) => ["people", "search", search] as const,
    rotationEligible: (search: string) => ["people", "rotation-eligible", search] as const,
    history: (personId: number) => ["people", personId, "history"] as const,
    directory: (search: string, offset: number) => ["people", "directory", search, offset] as const,
  },

  workflows: {
    all: () => ["workflows"] as const,
    list: (options: WorkflowQueryOptions, page: number, pageSize: number) =>
      ["workflows", "list", options, page, pageSize] as const,
    detail: (uid: string) => ["workflows", uid] as const,
    config: (roleId: number | null, workflowDefinitionKey: string | null) =>
      ["workflows", "config", roleId ?? null, workflowDefinitionKey ?? null] as const,
    targetPersonSources: (search: string) => ["workflows", "target-person-sources", search] as const,
    tasks: (uid: string) => ["workflows", uid, "tasks"] as const,
    related: (uid: string) => ["workflows", uid, "related"] as const,
    auditLog: (uid: string, limit: number, offset: number) =>
      ["workflows", uid, "audit", limit, offset] as const,
    supervisorStep: (uid: string) => ["workflows", uid, "supervisor-step"] as const,
  },

  myTasks: () => ["my-tasks"] as const,
  supervisorWorkflows: () => ["supervisor-workflows"] as const,

  tasks: {
    byRef: (taskRef: string) => ["tasks", "ref", taskRef] as const,
  },

  rotation: {
    plans: (personId: number | null) => ["rotation", "plans", personId ?? null] as const,
    planDetail: (planId: number | null) => ["rotation", "plans", "detail", planId ?? null] as const,
    generatedTasks: (planId: number | null) => ["rotation", "plans", planId ?? null, "generated-tasks"] as const,
    auditLog: (planId: number | null, limit: number, offset: number) =>
      ["rotation", "plans", planId ?? null, "audit", limit, offset] as const,
    notifications: (planId: number | null, limit: number, offset: number) =>
      ["rotation", "plans", planId ?? null, "notifications", limit, offset] as const,
    adminTemplates: (departmentId: number | null, isActive: boolean | null) =>
      ["rotation", "admin-templates", departmentId ?? null, isActive ?? null] as const,
  },

  dashboard: {
    all: () => ["dashboard"] as const,
    insights: (dashboardPersona: DashboardPersona, workflowDefinitionKey: string | null | undefined) =>
      ["dashboard", "insights", dashboardPersona, workflowDefinitionKey ?? null] as const,
  },

  admin: {
    users: () => ["admin", "users"] as const,
    roles: () => ["admin", "roles"] as const,
    groups: () => ["admin", "groups"] as const,
    departmentAssignments: () => ["admin", "department-assignments"] as const,
    responsibilityOwners: () => ["admin", "responsibility-owners"] as const,
    notificationEmail: () => ["admin", "notification-email"] as const,
    workflowConfig: () => ["admin", "workflow-config"] as const,
    runtimeHealth: () => ["admin", "runtime-health"] as const,
  },
};
