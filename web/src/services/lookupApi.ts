import type { Department, Role, StartableWorkflowDefinition } from "../types/workflow";
import { requestJson } from "./api/client";
import { buildAdminListQuery, type AdminListPage, type AdminListQueryOptions } from "./api/adminList";
import type {
  BackendDepartmentDto,
  BackendRoleDto,
  BackendWorkflowStartableDefinitionDto,
} from "./api/backendDtos";

export async function getRoles(options: AdminListQueryOptions = {}): Promise<AdminListPage<Role>> {
  return requestJson<AdminListPage<BackendRoleDto>>(`/roles${buildAdminListQuery(options)}`);
}

export function getStartableWorkflowDefinitions(): Promise<StartableWorkflowDefinition[]> {
  return requestJson<BackendWorkflowStartableDefinitionDto[]>("/workflow-definitions/startable");
}

export async function getDepartments(options: AdminListQueryOptions = {}): Promise<AdminListPage<Department>> {
  return requestJson<AdminListPage<BackendDepartmentDto>>(`/departments${buildAdminListQuery(options)}`);
}
