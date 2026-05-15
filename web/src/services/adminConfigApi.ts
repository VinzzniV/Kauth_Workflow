import type {
  AdminAnswerDefinition,
  AdminDependencyGraph,
  AdminDirectoryGroup,
  AdminDirectoryGroupRoleMapping,
  AdminDirectoryIdentity,
  AdminDirectoryMappingAuditEntry,
  AdminDirectorySyncResult,
  AdminDirectorySyncStatus,
  DirectoryPendingImport,
  DirectoryImportResult,
  DirectoryPendingImports,
  DirectoryResponsibilityGapEntry,
  DirectoryResponsibilityGaps,
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
  buildAdminListQuery,
  type AdminListPage,
  type AdminListQueryOptions,
} from "./api/adminList";
import { buildCursorPageQuery, type CursorPage, type CursorPageQueryOptions } from "./api/cursorPage";
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
  BackendAdminDirectorySyncResultDto,
  BackendAdminDirectorySyncStatusDto,
  BackendDirectoryImportResultDto,
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
  const page = await requestJson<AdminListPage<BackendAdminDirectoryIdentityDto>>(
    `/admin/directory/identities${buildAdminListQuery({ limit, offset })}`
  );
  return page.items;
}

export async function getAdminDirectoryResponsibilityGaps(): Promise<DirectoryResponsibilityGaps> {
  const page = await requestJson<AdminListPage<DirectoryResponsibilityGapEntry>>(
    `/admin/directory/responsibility-gaps${buildAdminListQuery({ limit: 200 })}`
  );
  const unassigned = page.items.filter((gap) => gap.assignedLeadPersonId === null);
  return {
    gaps: page.items,
    totalUnassignedDepartments: unassigned.length,
    totalCandidatesNotYetAssigned: unassigned.reduce((sum, gap) => sum + gap.candidatesInEntra, 0),
  };
}

export async function getAdminDirectoryPendingImports(): Promise<DirectoryPendingImports> {
  const page = await requestJson<AdminListPage<DirectoryPendingImport>>(
    `/admin/directory/pending-imports${buildAdminListQuery({ limit: 200 })}`
  );
  return {
    pendingImports: page.items,
    totalCount: page.total,
  };
}

export async function postAdminDirectoryImport(directoryIdentityIds: number[]): Promise<DirectoryImportResult> {
  return requestJson<BackendDirectoryImportResultDto>("/admin/directory/import", {
    method: "POST",
    body: { directoryIdentityIds },
  });
}

export async function getAdminDirectoryAudit(options: CursorPageQueryOptions = {}): Promise<CursorPage<AdminDirectoryMappingAuditEntry>> {
  const qs = buildCursorPageQuery(options);
  return requestJson<CursorPage<AdminDirectoryMappingAuditEntry>>(`/admin/directory/audit${qs}`);
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

export async function getAdminTaskTemplates(
  workflowDefinitionId: number,
  options: AdminListQueryOptions = {}
): Promise<AdminListPage<AdminTaskSpec>> {
  const baseQuery = buildAdminListQuery(options);
  const separator = baseQuery ? "&" : "?";
  const url = `/admin/config/task-templates${baseQuery}${separator}workflowDefinitionId=${encodeURIComponent(String(workflowDefinitionId))}`;
  const page = await requestJson<AdminListPage<BackendAdminTaskTemplateDto>>(url);
  return {
    items: page.items.map(mapAdminTaskSpec),
    total: page.total,
    limit: page.limit,
    offset: page.offset,
  };
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

export async function getAdminTaskTemplateConditions(
  templateId: number,
  options: AdminListQueryOptions = {}
): Promise<AdminListPage<AdminTaskSpecCondition>> {
  const page = await requestJson<AdminListPage<BackendAdminTaskTemplateConditionDto>>(
    `/admin/config/task-templates/${encodeId(templateId)}/conditions${buildAdminListQuery(options)}`
  );
  return {
    items: page.items.map(mapAdminTaskSpecCondition),
    total: page.total,
    limit: page.limit,
    offset: page.offset,
  };
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

export async function getAdminTaskTemplateDependencies(
  templateId: number,
  options: AdminListQueryOptions = {}
): Promise<AdminListPage<AdminTaskSpecDependency>> {
  const page = await requestJson<AdminListPage<BackendAdminTaskTemplateDependencyDto>>(
    `/admin/config/task-templates/${encodeId(templateId)}/dependencies${buildAdminListQuery(options)}`
  );
  return {
    items: page.items.map(mapAdminTaskSpecDependency),
    total: page.total,
    limit: page.limit,
    offset: page.offset,
  };
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

export async function getAdminAnswerDefinitions(
  workflowDefinitionId: number,
  options: AdminListQueryOptions = {}
): Promise<AdminListPage<AdminAnswerDefinition>> {
  const baseQuery = buildAdminListQuery(options);
  const separator = baseQuery ? "&" : "?";
  const url = `/admin/config/answer-definitions${baseQuery}${separator}workflowDefinitionId=${encodeURIComponent(String(workflowDefinitionId))}`;
  return requestJson<AdminListPage<BackendAdminAnswerDefinitionDto>>(url);
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

export async function getAdminRoleAnswerDefaults(
  workflowDefinitionId: number,
  options: AdminListQueryOptions = {}
): Promise<AdminListPage<AdminRoleAnswerDefault>> {
  const baseQuery = buildAdminListQuery(options);
  const separator = baseQuery ? "&" : "?";
  const url = `/admin/config/role-answer-defaults${baseQuery}${separator}workflowDefinitionId=${encodeURIComponent(String(workflowDefinitionId))}`;
  return requestJson<AdminListPage<BackendAdminRoleAnswerDefaultDto>>(url);
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

export async function getAdminWorkflowDefinitions(
  options: AdminListQueryOptions = {}
): Promise<AdminListPage<AdminWorkflowDefinitionSummary>> {
  return requestJson<AdminListPage<BackendAdminWorkflowDefinitionSummaryDto>>(
    `/admin/config/workflow-definitions${buildAdminListQuery(options)}`
  );
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

export type ReplaceAdminWorkflowDefinitionVersionConflict = {
  message: string;
  currentUpdatedAt: string;
};

export async function replaceAdminWorkflowDefinitionVersion(
  versionId: number,
  payload: {
    name: string | null;
    description: string | null;
    // FE-13: Optimistic-Concurrency-Token. Server lehnt Replace mit 409 ab,
    // wenn der DB-Stand zwischenzeitlich von einem anderen Schreiber verändert
    // wurde. Wenn null/undefined, wird kein Stale-Check gemacht.
    expectedUpdatedAt?: string | null;
    nodes: Array<{
      nodeKey: string | null;
      nodeType: string | null;
      title: string | null;
      sortOrder: number;
      positionX: number | null;
      positionY: number | null;
      config: Record<string, unknown> | null;
      // Slice 4 (Admin-Gated-Automation, Builder-UI): Approval-Rolle fuer
      // task-Nodes mit Action-Bundle. Whitelist greift backend-seitig
      // (WorkflowDefinitionValidationCatalog.AllowedAutomationAdminRoles);
      // null fuer alle anderen Node-Typen.
      automationAdminRole: string | null;
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

export async function getAdminWorkflowActionDefinitions(
  options: AdminListQueryOptions = {}
): Promise<AdminListPage<AdminWorkflowActionDefinition>> {
  const page = await requestJson<AdminListPage<BackendAdminWorkflowActionDefinitionDto>>(
    `/admin/config/action-definitions${buildAdminListQuery(options)}`
  );
  return {
    items: page.items.map((definition) => ({
      id: definition.id,
      actionKey: definition.key,
      displayName: definition.name,
      description: definition.description,
      handlerKey: definition.handlerType,
      isSimulated: definition.isSimulated,
      isIdempotent: definition.isIdempotent,
      isActive: definition.isActive,
      requiresApproval: definition.requiresApproval,
      inputSchema: definition.parameterSchema,
    })),
    total: page.total,
    limit: page.limit,
    offset: page.offset,
  };
}

export type AdminAutomationPropertyCatalogPropertyKind = "business" | "technical";

export type AdminAutomationPropertyCatalogProperty = {
  key: string;
  label: string;
  kind: AdminAutomationPropertyCatalogPropertyKind;
};

export type AdminAutomationPropertyCatalogSource = {
  source: string;
  label: string;
  properties: AdminAutomationPropertyCatalogProperty[];
};

export type AdminAutomationPropertyCatalog = {
  sources: AdminAutomationPropertyCatalogSource[];
};

export async function getAdminAutomationPropertyCatalog(): Promise<AdminAutomationPropertyCatalog> {
  return requestJson<AdminAutomationPropertyCatalog>("/admin/config/automation-property-catalog");
}
