import type { AdminOrganizationEntity } from "./adminWorkspaceModel";

export type DepartmentDraft = {
  departmentLeadUserId: string;
  requirementOwnerUserId: string;
};

export type PositionDraft = {
  roleName: string;
  isActive: boolean;
};

export type ResponsibilityDraft = {
  appUserId: string;
  departmentId: string;
};

export type NewResponsibilityDraft = {
  responsibilityName: string;
  departmentId: string;
};

export const ADMIN_ORGANIZATION_ENTITY_LABELS: Record<AdminOrganizationEntity, string> = {
  user: "Personen",
  department: "Abteilungen",
  responsibility: "Fachbereiche / Zuständigkeiten",
};
