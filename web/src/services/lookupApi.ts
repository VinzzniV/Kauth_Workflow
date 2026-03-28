import type { Department, ProcessType, Role } from "../types/workflow";
import { requestJson } from "./api/client";
import type { BackendDepartmentDto, BackendProcessTypeDto, BackendRoleDto } from "./api/backendDtos";

export async function getRoles(): Promise<Role[]> {
  return requestJson<BackendRoleDto[]>("/roles");
}

export function getProcessTypes(): Promise<ProcessType[]> {
  return requestJson<BackendProcessTypeDto[]>("/process-types");
}

export async function getDepartments(): Promise<Department[]> {
  return requestJson<BackendDepartmentDto[]>("/departments");
}
