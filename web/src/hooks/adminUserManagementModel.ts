import type { AdminGroup, AdminUser } from "../types/auth";

export type UserEditDraft = {
  externalKey: string;
  displayName: string;
  email: string;
  notificationEmail: string;
  departmentId: string;
  isActive: boolean;
};

export type UserNewDraft = UserEditDraft;

export type SavingOperation =
  | "masterData"
  | "userRoles"
  | "userGroups"
  | "groupRoles"
  | "creating"
  | null;

export const EMPTY_USER_EDIT_DRAFT: UserEditDraft = {
  externalKey: "",
  displayName: "",
  email: "",
  notificationEmail: "",
  departmentId: "",
  isActive: true,
};

export const EMPTY_USER_NEW_DRAFT: UserNewDraft = {
  externalKey: "",
  displayName: "",
  email: "",
  notificationEmail: "",
  departmentId: "",
  isActive: true,
};

export function buildEditDraft(user: AdminUser | null): UserEditDraft {
  if (!user) {
    return EMPTY_USER_EDIT_DRAFT;
  }

  return {
    externalKey: user.externalKey ?? "",
    displayName: user.displayName,
    email: user.email,
    notificationEmail: user.notificationEmail ?? "",
    departmentId: user.departmentId ? String(user.departmentId) : "",
    isActive: user.isActive,
  };
}

export function buildSelectedUserSignature(user: AdminUser | null): string | null {
  if (!user) {
    return null;
  }

  return [
    user.userId,
    user.externalKey ?? "",
    user.displayName,
    user.email,
    user.notificationEmail ?? "",
    user.departmentId ?? "",
    user.isActive ? "1" : "0",
    user.roles.map((role) => role.roleId).join(","),
    user.groups.map((group) => group.groupId).join(","),
  ].join("|");
}

export function buildSelectedGroupSignature(group: AdminGroup | null): string | null {
  if (!group) {
    return null;
  }

  return [group.groupId, group.roles.map((role) => role.roleId).join(",")].join("|");
}

