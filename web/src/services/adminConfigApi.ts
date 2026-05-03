import type {
  AdminAnswerDefinition,
  AdminDependencyGraph,
  AdminDirectoryGroup,
  AdminDirectoryGroupRoleMapping,
  AdminDirectoryIdentity,
  AdminDirectoryMappingAuditEntry,
  AdminDirectorySyncResult,
  AdminDirectorySyncStatus,
  AdminRoleAnswerDefault,
  AdminTaskSpec,
  AdminTaskSpecCondition,
  AdminTaskSpecDependency,
  AdminWorkflowActionDefinition,
  AdminWorkflowDefinitionSummary,
  AdminWorkflowDefinitionVersionDetail,
  AdminWorkflowDefinitionVersionSummary,
} from "../types/auth";
import { encodeId, requestJson } from "./api/client";
import {
  mapAdminDependencyGraph,
  mapAdminTaskSpec,
  mapAdminTaskSpecCondition,
  mapAdminTaskSpecDependency,
} from "./api/mappers";
import type {
  BackendAdminAnswerDefinitionDto,
  BackendAdminDependencyGraphDto,
  BackendAdminDirectoryGroupDto,
  BackendAdminDirectoryGroupRoleMappingDto,
  BackendAdminDirectoryIdentityDto,
  BackendAdminDirectoryMappingAuditEntryDto,
  BackendAdminDirectorySyncResultDto,
  BackendAdminDirectorySyncStatusDto,
  BackendAdminRoleAnswerDefaultDto,
  BackendAdminTaskTemplateConditionDto,
  BackendAdminTaskTemplateDependencyDto,
  BackendAdminTaskTemplateDto,
  BackendAdminWorkflowActionDefinitionDto,
  BackendAdminWorkflowDefinitionSummaryDto,
  BackendAdminWorkflowDefinitionVersionDetailDto,
  BackendAdminWorkflowDefinitionVersionSummaryDto,
} from "./api/backendDtos";

export async function getAdminDirectoryStatus(): Promise<AdminDirectorySyncStatus> {
  return requestJson<BackendAdminDirectorySyncStatusDto>("/admin/directory/status");
}

export async function syncAdminDirectory(groupPrefix?: string | null): Promise<AdminDirectorySyncResult> {
  return requestJson<BackendAdminDirectorySyncResultDto>("/admin/directory/sync", {
    method: "POST",
    body: {
      groupPrefix: groupPrefix ?? null,
    },
  });
}

export async function getAdminDirectoryGroups(): Promise<AdminDirectoryGroup[]> {
  return requestJson<BackendAdminDirectoryGroupDto[]>("/admin/directory/groups");
}

export async function getAdminDirectoryIdentities(
  limit = 100,
  offset = 0
): Promise<AdminDirectoryIdentity[]> {
  const params = new URLSearchParams({
    limit: String(limit),
    offset: String(offset),
  });
  return requestJson<BackendAdminDirectoryIdentityDto[]>(`/admin/directory/identities?${params.toString()}`);
}

export async function getAdminDirectoryAudit(limit = 50): Promise<AdminDirectoryMappingAuditEntry[]> {
  const params = new URLSearchParams({ limit: String(limit) });
  return requestJson<BackendAdminDirectoryMappingAuditEntryDto[]>(`/admin/directory/audit?${params.toString()}`);
}

export async function createAdminDirectoryGroupRoleMapping(payload: {
  directoryGroupId: number;
  appRoleId: number;
  scope?: string | null;
  scopeDepartmentId?: number | null;
  isActive?: boolean;
}): Promise<AdminDirectoryGroupRoleMapping> {
  return requestJson<BackendAdminDirectoryGroupRoleMappingDto>("/admin/directory/group-mappings", {
    method: "POST",
    body: payload,
  });
}

export async function deleteAdminDirectoryGroupRoleMapping(mappingId: number): Promise<void> {
  await requestJson<void>(`/admin/directory/group-mappings/${encodeId(mappingId)}`, {
    method: "DELETE",
  });
}

export async function getAdminTaskTemplates(workflowDefinitionId: number): Promise<AdminTaskSpec[]> {
  const params = new URLSearchParams({ workflowDefinitionId: String(workflowDefinitionId) });
  const dtos = await requestJson<BackendAdminTaskTemplateDto[]>(
    `/admin/config/task-templates?${params.toString()}`
  );
  return dtos.map(mapAdminTaskSpec);
}

export async function getAdminDependencyGraph(workflowDefinitionId: number): Promise<AdminDependencyGraph> {
  const dto = await requestJson<BackendAdminDependencyGraphDto>(
    `/admin/config/workflow-definitions/${encodeId(workflowDefinitionId)}/dependency-graph`
  );
  return mapAdminDependencyGraph(dto);
}

export async function createAdminTaskTemplate(payload: {
  workflowDefinitionId: number;
  specKey: string;
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
}): Promise<AdminTaskSpec> {
  // LA5: Backend erwartet weiter `templateKey` ueber die Wire — Mapping ist explizit.
  const { specKey, ...rest } = payload;
  const dto = await requestJson<BackendAdminTaskTemplateDto>("/admin/config/task-templates", {
    method: "POST",
    body: { ...rest, templateKey: specKey },
  });
  return mapAdminTaskSpec(dto);
}

export async function updateAdminTaskTemplate(
  templateId: number,
  payload: {
    workflowDefinitionId: number;
    specKey: string;
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
): Promise<AdminTaskSpec> {
  const { specKey, ...rest } = payload;
  const dto = await requestJson<BackendAdminTaskTemplateDto>(
    `/admin/config/task-templates/${encodeId(templateId)}`,
    {
      method: "PATCH",
      body: { ...rest, templateKey: specKey },
    }
  );
  return mapAdminTaskSpec(dto);
}

export async function deleteAdminTaskTemplate(templateId: number): Promise<void> {
  await requestJson<void>(`/admin/config/task-templates/${encodeId(templateId)}`, {
    method: "DELETE",
  });
}

export async function getAdminTaskTemplateConditions(templateId: number): Promise<AdminTaskSpecCondition[]> {
  const dtos = await requestJson<BackendAdminTaskTemplateConditionDto[]>(
    `/admin/config/task-templates/${encodeId(templateId)}/conditions`
  );
  return dtos.map(mapAdminTaskSpecCondition);
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
): Promise<AdminTaskSpecCondition> {
  const dto = await requestJson<BackendAdminTaskTemplateConditionDto>(
    `/admin/config/task-templates/${encodeId(templateId)}/conditions`,
    {
      method: "POST",
      body: payload,
    }
  );
  return mapAdminTaskSpecCondition(dto);
}

export async function deleteAdminTaskTemplateCondition(templateId: number, conditionId: number): Promise<void> {
  await requestJson<void>(
    `/admin/config/task-templates/${encodeId(templateId)}/conditions/${encodeId(conditionId)}`,
    {
      method: "DELETE",
    }
  );
}

export async function getAdminTaskTemplateDependencies(templateId: number): Promise<AdminTaskSpecDependency[]> {
  const dtos = await requestJson<BackendAdminTaskTemplateDependencyDto[]>(
    `/admin/config/task-templates/${encodeId(templateId)}/dependencies`
  );
  return dtos.map(mapAdminTaskSpecDependency);
}

export async function createAdminTaskTemplateDependency(
  templateId: number,
  payload: {
    dependsOnTaskSpecId: number;
    requiredStatus: "open" | "ready" | "in_progress" | "blocked" | "done";
  }
): Promise<AdminTaskSpecDependency> {
  // LA5: Backend erwartet weiter `dependsOnTaskTemplateId` ueber die Wire.
  const dto = await requestJson<BackendAdminTaskTemplateDependencyDto>(
    `/admin/config/task-templates/${encodeId(templateId)}/dependencies`,
    {
      method: "POST",
      body: {
        dependsOnTaskTemplateId: payload.dependsOnTaskSpecId,
        requiredStatus: payload.requiredStatus,
      },
    }
  );
  return mapAdminTaskSpecDependency(dto);
}

export async function deleteAdminTaskTemplateDependency(templateId: number, dependencyId: number): Promise<void> {
  await requestJson<void>(
    `/admin/config/task-templates/${encodeId(templateId)}/dependencies/${encodeId(dependencyId)}`,
    {
      method: "DELETE",
    }
  );
}

export async function getAdminAnswerDefinitions(workflowDefinitionId: number): Promise<AdminAnswerDefinition[]> {
  const params = new URLSearchParams({ workflowDefinitionId: String(workflowDefinitionId) });
  return requestJson<BackendAdminAnswerDefinitionDto[]>(`/admin/config/answer-definitions?${params.toString()}`);
}

export async function createAdminAnswerDefinition(payload: {
  workflowDefinitionId: number;
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
    workflowDefinitionId: number;
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
    `/admin/config/answer-definitions/${encodeId(definitionId)}`,
    {
      method: "PATCH",
      body: payload,
    }
  );
}

export async function deleteAdminAnswerDefinition(definitionId: number): Promise<void> {
  await requestJson<void>(`/admin/config/answer-definitions/${encodeId(definitionId)}`, {
    method: "DELETE",
  });
}

export async function getAdminRoleAnswerDefaults(workflowDefinitionId: number): Promise<AdminRoleAnswerDefault[]> {
  const params = new URLSearchParams({ workflowDefinitionId: String(workflowDefinitionId) });
  return requestJson<BackendAdminRoleAnswerDefaultDto[]>(`/admin/config/role-answer-defaults?${params.toString()}`);
}

export async function updateAdminRoleAnswerDefaults(payload: {
  workflowDefinitionId: number;
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

export async function getAdminWorkflowDefinitions(): Promise<AdminWorkflowDefinitionSummary[]> {
  return requestJson<BackendAdminWorkflowDefinitionSummaryDto[]>("/admin/config/workflow-definitions");
}

export async function createAdminWorkflowDefinition(payload: {
  key?: string | null;
  name: string;
  description: string | null;
}): Promise<AdminWorkflowDefinitionSummary> {
  return requestJson<BackendAdminWorkflowDefinitionSummaryDto>("/admin/config/workflow-definitions", {
    method: "POST",
    body: payload,
  });
}

export async function deleteAdminWorkflowDefinition(definitionId: number): Promise<void> {
  await requestJson<void>(
    `/admin/config/workflow-definitions/${encodeId(definitionId)}`,
    {
      method: "DELETE",
    }
  );
}

export async function updateAdminWorkflowDefinition(
  definitionId: number,
  payload: {
    name: string;
    description: string | null;
  }
): Promise<AdminWorkflowDefinitionSummary> {
  return requestJson<BackendAdminWorkflowDefinitionSummaryDto>(
    `/admin/config/workflow-definitions/${encodeId(definitionId)}`,
    {
      method: "PATCH",
      body: payload,
    }
  );
}

export async function createAdminWorkflowDefinitionVersion(
  definitionId: number,
  payload: {
    name: string | null;
    description: string | null;
  }
): Promise<AdminWorkflowDefinitionVersionSummary> {
  return requestJson<BackendAdminWorkflowDefinitionVersionSummaryDto>(
    `/admin/config/workflow-definitions/${encodeId(definitionId)}/versions`,
    {
      method: "POST",
      body: payload,
    }
  );
}

export async function ensureAdminWorkflowDefinitionWorkingDraft(
  definitionId: number
): Promise<AdminWorkflowDefinitionVersionDetail> {
  return requestJson<BackendAdminWorkflowDefinitionVersionDetailDto>(
    `/admin/config/workflow-definitions/${encodeId(definitionId)}/working-draft`,
    {
      method: "POST",
      body: {},
    }
  );
}

export async function getAdminWorkflowDefinitionVersion(
  versionId: number
): Promise<AdminWorkflowDefinitionVersionDetail> {
  return requestJson<BackendAdminWorkflowDefinitionVersionDetailDto>(
    `/admin/config/workflow-definition-versions/${encodeId(versionId)}`
  );
}

export async function replaceAdminWorkflowDefinitionVersion(
  versionId: number,
  payload: {
    name: string | null;
    description: string | null;
    nodes: Array<{
      nodeKey: string | null;
      nodeType: string | null;
      title: string | null;
      sortOrder: number;
      positionX: number | null;
      positionY: number | null;
      config: Record<string, unknown> | null;
      actions: Array<{
        actionKey: string | null;
        inputMapping: Record<string, unknown> | null;
        executionOrder: number;
        onErrorBehavior: string | null;
      }>;
      // FE-9: Specs reisen mit der Version-DTO. Builder serialisiert sie immer
      // (ggf. leeres Array). Ohne dieses Feld wuerden Specs beim Replace via
      // CASCADE-Delete verschwinden.
      specs: Array<{
        specKey: string;
        title: string;
        category: string;
        description: string;
        iconKey: string | null;
        defaultResponsibilityId: number | null;
        processAreaLabel: string | null;
        isDepartmentPhaseTask: boolean;
        isRequired: boolean;
        dueInDays: number | null;
        sortOrder: number;
        conditions: Array<{
          answerKey: string;
          operator: "eq" | "neq" | "is_true" | "is_false" | "is_null" | "is_not_null";
          expectedValueText: string | null;
          expectedValueBoolean: boolean | null;
          expectedValueNumber: number | null;
        }>;
        dependencies: Array<{
          dependsOnSpecKey: string;
        }>;
      }>;
    }>;
    edges: Array<{
      sourceNodeKey: string | null;
      targetNodeKey: string | null;
      priority: number;
      conditionExpression: string | null;
    }>;
  }
): Promise<AdminWorkflowDefinitionVersionDetail> {
  return requestJson<BackendAdminWorkflowDefinitionVersionDetailDto>(
    `/admin/config/workflow-definition-versions/${encodeId(versionId)}`,
    {
      method: "PUT",
      body: payload,
    }
  );
}

export async function publishAdminWorkflowDefinitionVersion(
  versionId: number
): Promise<AdminWorkflowDefinitionVersionDetail> {
  return requestJson<BackendAdminWorkflowDefinitionVersionDetailDto>(
    `/admin/config/workflow-definition-versions/${encodeId(versionId)}/publish`,
    {
      method: "POST",
      body: {},
    }
  );
}

export async function getAdminWorkflowActionDefinitions(): Promise<AdminWorkflowActionDefinition[]> {
  const definitions = await requestJson<BackendAdminWorkflowActionDefinitionDto[]>("/admin/config/action-definitions");
  return definitions.map((definition) => ({
    id: definition.id,
    actionKey: definition.key,
    displayName: definition.name,
    description: definition.description,
    handlerKey: definition.handlerType,
    isIdempotent: definition.isIdempotent,
    isActive: definition.isActive,
    requiresApproval: definition.requiresApproval,
    inputSchema: definition.parameterSchema,
  }));
}

export type AdminAutomationPropertyCatalogSource = {
  source: string;
  label: string;
  properties: string[];
};

export type AdminAutomationPropertyCatalog = {
  sources: AdminAutomationPropertyCatalogSource[];
};

export async function getAdminAutomationPropertyCatalog(): Promise<AdminAutomationPropertyCatalog> {
  return requestJson<AdminAutomationPropertyCatalog>("/admin/config/automation-property-catalog");
}
