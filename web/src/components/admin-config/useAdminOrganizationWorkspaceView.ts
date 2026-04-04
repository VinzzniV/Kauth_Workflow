import { useMemo } from "react";
import type {
  AdminDepartmentAssignment,
  AdminResponsibilityOwner,
  AdminUser,
} from "../../types/auth";
import {
  getDepartmentRelations,
  getResponsibilityRelations,
  getUserRelations,
  hasDepartmentAssignmentChanges,
  hasResponsibilityAssignmentChanges,
  hasUserMasterDataChanges,
  type AdminOrganizationEntity,
} from "./adminWorkspaceModel";
import type { DepartmentDraft, ResponsibilityDraft } from "./adminOrganizationTypes";

type UseAdminOrganizationWorkspaceViewArgs = {
  organizationEntity: AdminOrganizationEntity;
  selectedEntityId: number | null;
  sortedUsers: AdminUser[];
  sortedDepartments: AdminDepartmentAssignment[];
  sortedResponsibilities: AdminResponsibilityOwner[];
  eligibleSupervisorUsers: AdminUser[];
  selectedUser: AdminUser | null;
  userDisplayNameDraft: string;
  userEmailDraft: string;
  userNotificationEmailDraft: string;
  userExternalKeyDraft: string;
  userDepartmentIdDraft: string;
  userIsActiveDraft: boolean;
  newUserDisplayNameDraft: string;
  newUserEmailDraft: string;
  isCreatingUser: boolean;
  isSavingUserMasterData: boolean;
  departmentDrafts: Record<number, DepartmentDraft>;
  responsibilityDrafts: Record<number, ResponsibilityDraft>;
  savingDepartmentId: number | null;
  savingResponsibilityId: number | null;
};

function buildSupervisorOptions(
  selectedUserId: string,
  sortedUsers: AdminUser[],
  eligibleSupervisorUsers: AdminUser[]
): AdminUser[] {
  if (!selectedUserId) {
    return eligibleSupervisorUsers;
  }

  const selectedUser = sortedUsers.find((user) => String(user.userId) === selectedUserId);
  if (!selectedUser) {
    return eligibleSupervisorUsers;
  }

  if (eligibleSupervisorUsers.some((user) => user.userId === selectedUser.userId)) {
    return eligibleSupervisorUsers;
  }

  return [...eligibleSupervisorUsers, selectedUser];
}

export function useAdminOrganizationWorkspaceView({
  organizationEntity,
  selectedEntityId,
  sortedUsers,
  sortedDepartments,
  sortedResponsibilities,
  eligibleSupervisorUsers,
  selectedUser,
  userDisplayNameDraft,
  userEmailDraft,
  userNotificationEmailDraft,
  userExternalKeyDraft,
  userDepartmentIdDraft,
  userIsActiveDraft,
  newUserDisplayNameDraft,
  newUserEmailDraft,
  isCreatingUser,
  isSavingUserMasterData,
  departmentDrafts,
  responsibilityDrafts,
  savingDepartmentId,
  savingResponsibilityId,
}: UseAdminOrganizationWorkspaceViewArgs) {
  const selectedDepartment = useMemo(
    () =>
      organizationEntity === "department"
        ? sortedDepartments.find((department) => department.departmentId === selectedEntityId) ?? null
        : null,
    [organizationEntity, selectedEntityId, sortedDepartments]
  );
  const selectedResponsibility = useMemo(
    () =>
      organizationEntity === "responsibility"
        ? sortedResponsibilities.find((responsibility) => responsibility.responsibilityId === selectedEntityId) ?? null
        : null,
    [organizationEntity, selectedEntityId, sortedResponsibilities]
  );

  const userRelations = useMemo(
    () =>
      selectedUser
        ? getUserRelations({
            user: selectedUser,
            departments: sortedDepartments,
            responsibilities: sortedResponsibilities,
          })
        : null,
    [selectedUser, sortedDepartments, sortedResponsibilities]
  );
  const departmentRelations = useMemo(
    () =>
      selectedDepartment
        ? getDepartmentRelations({
            department: selectedDepartment,
            users: sortedUsers,
            responsibilities: sortedResponsibilities,
          })
        : null,
    [selectedDepartment, sortedResponsibilities, sortedUsers]
  );
  const responsibilityRelations = useMemo(
    () =>
      selectedResponsibility
        ? getResponsibilityRelations({
            responsibility: selectedResponsibility,
            users: sortedUsers,
            departments: sortedDepartments,
          })
        : null,
    [selectedResponsibility, sortedDepartments, sortedUsers]
  );

  const canCreateUser =
    !isCreatingUser &&
    newUserDisplayNameDraft.trim().length > 0 &&
    newUserEmailDraft.trim().length > 0;
  const canSaveUser =
    selectedUser !== null &&
    !isSavingUserMasterData &&
    userDisplayNameDraft.trim().length > 0 &&
    userEmailDraft.trim().length > 0 &&
    hasUserMasterDataChanges({
      selectedUser,
      userExternalKeyDraft,
      userDisplayNameDraft,
      userEmailDraft,
      userNotificationEmailDraft,
      userDepartmentIdDraft,
      userIsActiveDraft,
    });

  const selectedDepartmentDraft = useMemo(
    () =>
      selectedDepartment
        ? departmentDrafts[selectedDepartment.departmentId] ?? {
            departmentLeadUserId: "",
            requirementOwnerUserId: "",
          }
        : null,
    [departmentDrafts, selectedDepartment]
  );
  const selectedDepartmentLeadOptions = useMemo(
    () =>
      selectedDepartmentDraft
        ? buildSupervisorOptions(
            selectedDepartmentDraft.departmentLeadUserId,
            sortedUsers,
            eligibleSupervisorUsers
          )
        : [],
    [eligibleSupervisorUsers, selectedDepartmentDraft, sortedUsers]
  );
  const selectedDepartmentOwnerOptions = useMemo(
    () =>
      selectedDepartmentDraft
        ? buildSupervisorOptions(
            selectedDepartmentDraft.requirementOwnerUserId,
            sortedUsers,
            eligibleSupervisorUsers
          )
        : [],
    [eligibleSupervisorUsers, selectedDepartmentDraft, sortedUsers]
  );
  const canSaveDepartment =
    selectedDepartment !== null &&
    selectedDepartmentDraft !== null &&
    hasDepartmentAssignmentChanges(selectedDepartment, selectedDepartmentDraft) &&
    savingDepartmentId !== selectedDepartment.departmentId;

  const selectedResponsibilityDraft = useMemo(
    () =>
      selectedResponsibility
        ? responsibilityDrafts[selectedResponsibility.responsibilityId] ?? {
            appUserId: "",
            departmentId: "",
          }
        : null,
    [responsibilityDrafts, selectedResponsibility]
  );
  const canSaveResponsibility =
    selectedResponsibility !== null &&
    selectedResponsibilityDraft !== null &&
    hasResponsibilityAssignmentChanges(selectedResponsibility, selectedResponsibilityDraft) &&
    savingResponsibilityId !== selectedResponsibility.responsibilityId;

  return {
    selectedDepartment,
    selectedResponsibility,
    userRelations,
    departmentRelations,
    responsibilityRelations,
    canCreateUser,
    canSaveUser,
    selectedDepartmentDraft,
    selectedDepartmentLeadOptions,
    selectedDepartmentOwnerOptions,
    canSaveDepartment,
    selectedResponsibilityDraft,
    canSaveResponsibility,
  };
}
