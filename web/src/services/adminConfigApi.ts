import type {
  AdminAnswerDefinition,
  AdminDependencyGraph,
  AdminProcessType,
  AdminRoleAnswerDefault,
  AdminTaskTemplate,
  AdminTaskTemplateCondition,
  AdminTaskTemplateDependency,
} from "../types/auth";
import type { BulkDepartmentChangePayload, BulkOperationResult } from "../types/workflow";
import { requestJson } from "./api/client";
import { getCachedRequest, invalidateCachedRequest } from "./cache";
import type {
  BackendAdminAnswerDefinitionDto,
  BackendAdminDependencyGraphDto,
  BackendAdminProcessTypeDto,
  BackendAdminRoleAnswerDefaultDto,
  BackendAdminTaskTemplateConditionDto,
  BackendAdminTaskTemplateDependencyDto,
  BackendAdminTaskTemplateDto,
} from "./api/backendDtos";

export async function bulkCreateDepartmentChange(
  payload: BulkDepartmentChangePayload
): Promise<BulkOperationResult> {
  return requestJson<BulkOperationResult>("/admin/bulk/department-change", {
    method: "POST",
    body: payload,
  });
}

export function getAdminProcessTypes(): Promise<AdminProcessType[]> {
  return getCachedRequest("admin-process-types", () =>
    requestJson<BackendAdminProcessTypeDto[]>("/admin/config/process-types")
  );
}

export async function updateAdminProcessType(
  processTypeId: number,
  payload: {
    name?: string;
    description?: string | null;
    iconKey?: string | null;
    isActive?: boolean;
    sortOrder?: number;
  }
): Promise<AdminProcessType> {
  const result = await requestJson<BackendAdminProcessTypeDto>(
    `/admin/config/process-types/${encodeURIComponent(String(processTypeId))}`,
    {
      method: "PATCH",
      body: payload,
    }
  );
  // Invalidate both caches so the next fetch reflects the updated process type.
  invalidateCachedRequest("admin-process-types");
  invalidateCachedRequest("process-types");
  return result;
}

export async function getAdminTaskTemplates(processTypeId: number): Promise<AdminTaskTemplate[]> {
  const params = new URLSearchParams({ processTypeId: String(processTypeId) });
  return requestJson<BackendAdminTaskTemplateDto[]>(`/admin/config/task-templates?${params.toString()}`);
}

export async function getAdminDependencyGraph(processTypeId: number): Promise<AdminDependencyGraph> {
  return requestJson<BackendAdminDependencyGraphDto>(
    `/admin/config/process-types/${encodeURIComponent(String(processTypeId))}/dependency-graph`
  );
}

export async function createAdminTaskTemplate(payload: {
  processTypeId: number;
  templateKey: string;
  title: string;
  category: string;
  description: string;
  iconKey: string | null;
  owningDepartmentId: number | null;
  defaultResponsibilityId: number | null;
  processAreaLabel: string | null;
  isDepartmentPhaseTask: boolean;
  isRequired: boolean;
  dueInDays: number | null;
  sortOrder: number;
  isActive: boolean;
}): Promise<AdminTaskTemplate> {
  return requestJson<BackendAdminTaskTemplateDto>("/admin/config/task-templates", {
    method: "POST",
    body: payload,
  });
}

export async function updateAdminTaskTemplate(
  templateId: number,
  payload: {
    processTypeId: number;
    templateKey: string;
    title: string;
    category: string;
    description: string;
    iconKey: string | null;
    owningDepartmentId: number | null;
    defaultResponsibilityId: number | null;
    processAreaLabel: string | null;
    isDepartmentPhaseTask: boolean;
    isRequired: boolean;
    dueInDays: number | null;
    sortOrder: number;
    isActive: boolean;
  }
): Promise<AdminTaskTemplate> {
  return requestJson<BackendAdminTaskTemplateDto>(
    `/admin/config/task-templates/${encodeURIComponent(String(templateId))}`,
    {
      method: "PATCH",
      body: payload,
    }
  );
}

export async function deleteAdminTaskTemplate(templateId: number): Promise<void> {
  await requestJson<unknown>(`/admin/config/task-templates/${encodeURIComponent(String(templateId))}`, {
    method: "DELETE",
  });
}

export async function getAdminTaskTemplateConditions(templateId: number): Promise<AdminTaskTemplateCondition[]> {
  return requestJson<BackendAdminTaskTemplateConditionDto[]>(
    `/admin/config/task-templates/${encodeURIComponent(String(templateId))}/conditions`
  );
}

export async function createAdminTaskTemplateCondition(
  templateId: number,
  payload: {
    conditionGroup: number;
    answerKey: string;
    operator: "eq" | "neq" | "is_true" | "is_false" | "is_null" | "is_not_null";
    expectedValueText: string | null;
    expectedValueBoolean: boolean | null;
    expectedValueNumber: number | null;
  }
): Promise<AdminTaskTemplateCondition> {
  return requestJson<BackendAdminTaskTemplateConditionDto>(
    `/admin/config/task-templates/${encodeURIComponent(String(templateId))}/conditions`,
    {
      method: "POST",
      body: payload,
    }
  );
}

export async function deleteAdminTaskTemplateCondition(templateId: number, conditionId: number): Promise<void> {
  await requestJson<unknown>(
    `/admin/config/task-templates/${encodeURIComponent(String(templateId))}/conditions/${encodeURIComponent(String(conditionId))}`,
    {
      method: "DELETE",
    }
  );
}

export async function getAdminTaskTemplateDependencies(templateId: number): Promise<AdminTaskTemplateDependency[]> {
  return requestJson<BackendAdminTaskTemplateDependencyDto[]>(
    `/admin/config/task-templates/${encodeURIComponent(String(templateId))}/dependencies`
  );
}

export async function createAdminTaskTemplateDependency(
  templateId: number,
  payload: {
    dependsOnTaskTemplateId: number;
    requiredStatus: "open" | "ready" | "in_progress" | "blocked" | "done";
  }
): Promise<AdminTaskTemplateDependency> {
  return requestJson<BackendAdminTaskTemplateDependencyDto>(
    `/admin/config/task-templates/${encodeURIComponent(String(templateId))}/dependencies`,
    {
      method: "POST",
      body: payload,
    }
  );
}

export async function deleteAdminTaskTemplateDependency(templateId: number, dependencyId: number): Promise<void> {
  await requestJson<unknown>(
    `/admin/config/task-templates/${encodeURIComponent(String(templateId))}/dependencies/${encodeURIComponent(String(dependencyId))}`,
    {
      method: "DELETE",
    }
  );
}

export async function getAdminAnswerDefinitions(processTypeId: number): Promise<AdminAnswerDefinition[]> {
  const params = new URLSearchParams({ processTypeId: String(processTypeId) });
  return requestJson<BackendAdminAnswerDefinitionDto[]>(`/admin/config/answer-definitions?${params.toString()}`);
}

export async function createAdminAnswerDefinition(payload: {
  processTypeId: number;
  answerKey: string;
  title: string;
  category: string;
  description: string;
  iconKey: string | null;
  inputType: "boolean" | "text" | "select" | "multi_select";
  isRequired: boolean;
  sortOrder: number;
  isActive: boolean;
}): Promise<AdminAnswerDefinition> {
  return requestJson<BackendAdminAnswerDefinitionDto>("/admin/config/answer-definitions", {
    method: "POST",
    body: payload,
  });
}

export async function updateAdminAnswerDefinition(
  definitionId: number,
  payload: {
    processTypeId: number;
    answerKey: string;
    title: string;
    category: string;
    description: string;
    iconKey: string | null;
    inputType: "boolean" | "text" | "select" | "multi_select";
    isRequired: boolean;
    sortOrder: number;
    isActive: boolean;
  }
): Promise<AdminAnswerDefinition> {
  return requestJson<BackendAdminAnswerDefinitionDto>(
    `/admin/config/answer-definitions/${encodeURIComponent(String(definitionId))}`,
    {
      method: "PATCH",
      body: payload,
    }
  );
}

export async function deleteAdminAnswerDefinition(definitionId: number): Promise<void> {
  await requestJson<unknown>(`/admin/config/answer-definitions/${encodeURIComponent(String(definitionId))}`, {
    method: "DELETE",
  });
}

export async function getAdminRoleAnswerDefaults(processTypeId: number): Promise<AdminRoleAnswerDefault[]> {
  const params = new URLSearchParams({ processTypeId: String(processTypeId) });
  return requestJson<BackendAdminRoleAnswerDefaultDto[]>(`/admin/config/role-answer-defaults?${params.toString()}`);
}

export async function updateAdminRoleAnswerDefaults(payload: {
  processTypeId: number;
  items: Array<{
    appRoleId: number;
    answerKey: string;
    defaultValueText: string | null;
    defaultValueBoolean: boolean | null;
  }>;
}): Promise<AdminRoleAnswerDefault[]> {
  return requestJson<BackendAdminRoleAnswerDefaultDto[]>("/admin/config/role-answer-defaults", {
    method: "PATCH",
    body: payload,
  });
}
