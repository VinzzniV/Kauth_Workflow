import { useState } from "react";
import type { Dispatch, SetStateAction } from "react";
import { useConfirmationDialog } from "../components/feedback/useConfirmationDialog";
import type { AdminGroup, AdminUser } from "../types/auth";
import {
  buildEditDraft,
  type SavingOperation,
  type UserEditDraft,
} from "./adminUserManagementModel";
import { useAdminUserMutations } from "./useAdminUserMutations";
import { useAdminUserSelectionState } from "./useAdminUserSelectionState";

type UseAdminUserManagementOptions = {
  users: AdminUser[];
  groups: AdminGroup[];
  setUsers: Dispatch<SetStateAction<AdminUser[]>>;
  setGroups: Dispatch<SetStateAction<AdminGroup[]>>;
  refreshCurrentUser: () => Promise<void>;
  reload: () => Promise<void>;
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
};

export { buildEditDraft };
export type { UserEditDraft };

export function useAdminUserManagement({
  users,
  groups,
  setUsers,
  setGroups,
  refreshCurrentUser,
  reload,
  onNotice,
  onError,
}: UseAdminUserManagementOptions) {
  const confirm = useConfirmationDialog();
  const [savingOperation, setSavingOperation] = useState<SavingOperation>(null);
  const selection = useAdminUserSelectionState({
    users,
    groups,
    onNotice,
    onError,
  });
  const mutations = useAdminUserMutations({
    confirm,
    setUsers,
    setGroups,
    refreshCurrentUser,
    reload,
    onNotice,
    onError,
    selectedUserId: selection.selectedUserId,
    selectedUser: selection.selectedUser,
    selectedGroup: selection.selectedGroup,
    selectedUserRoleIds: selection.selectedUserRoleIds,
    selectedUserGroupIds: selection.selectedUserGroupIds,
    selectedGroupRoleIds: selection.selectedGroupRoleIds,
    editDraft: selection.editDraft,
    newUserDraft: selection.newUserDraft,
    setSelectedUserId: selection.setSelectedUserId,
    setEditDraft: selection.setEditDraft,
    setNewUserDraft: selection.setNewUserDraft,
    setUsersFormNotice: selection.setUserFormNotice,
    setUsersFormError: selection.setUserFormError,
    setDeletingUserId: selection.setDeletingUserId,
    setSavingOperation,
    setLastSyncedSelectedUserSignature: selection.setLastSyncedSelectedUserSignature,
    setLastSyncedSelectedGroupSignature: selection.setLastSyncedSelectedGroupSignature,
  });

  return {
    userFormError: selection.userFormError,
    userFormNotice: selection.userFormNotice,
    selectedUserId: selection.selectedUserId,
    selectedGroupId: selection.selectedGroupId,
    selectedUserRoleIds: selection.selectedUserRoleIds,
    selectedUserGroupIds: selection.selectedUserGroupIds,
    selectedGroupRoleIds: selection.selectedGroupRoleIds,
    userExternalKeyDraft: selection.editDraft.externalKey,
    userDisplayNameDraft: selection.editDraft.displayName,
    userEmailDraft: selection.editDraft.email,
    userNotificationEmailDraft: selection.editDraft.notificationEmail,
    userDepartmentIdDraft: selection.editDraft.departmentId,
    userIsActiveDraft: selection.editDraft.isActive,
    newUserExternalKeyDraft: selection.newUserDraft.externalKey,
    newUserDisplayNameDraft: selection.newUserDraft.displayName,
    newUserEmailDraft: selection.newUserDraft.email,
    newUserNotificationEmailDraft: selection.newUserDraft.notificationEmail,
    newUserDepartmentIdDraft: selection.newUserDraft.departmentId,
    newUserIsActiveDraft: selection.newUserDraft.isActive,
    isSavingUserMasterData: savingOperation === "masterData",
    isSavingUserRoles: savingOperation === "userRoles",
    isSavingUserGroups: savingOperation === "userGroups",
    isSavingGroupRoles: savingOperation === "groupRoles",
    isCreatingUser: savingOperation === "creating",
    deletingUserId: selection.deletingUserId,
    selectedUser: selection.selectedUser,
    selectedGroup: selection.selectedGroup,
    setUserExternalKeyDraft: (value: string) => selection.updateEditDraft("externalKey", value),
    setUserDisplayNameDraft: (value: string) => selection.updateEditDraft("displayName", value),
    setUserEmailDraft: (value: string) => selection.updateEditDraft("email", value),
    setUserNotificationEmailDraft: (value: string) => selection.updateEditDraft("notificationEmail", value),
    setUserDepartmentIdDraft: (value: string) => selection.updateEditDraft("departmentId", value),
    setUserIsActiveDraft: (value: boolean) => selection.updateEditDraft("isActive", value),
    setNewUserExternalKeyDraft: (value: string) => selection.updateNewUserDraft("externalKey", value),
    setNewUserDisplayNameDraft: (value: string) => selection.updateNewUserDraft("displayName", value),
    setNewUserEmailDraft: (value: string) => selection.updateNewUserDraft("email", value),
    setNewUserNotificationEmailDraft: (value: string) => selection.updateNewUserDraft("notificationEmail", value),
    setNewUserDepartmentIdDraft: (value: string) => selection.updateNewUserDraft("departmentId", value),
    setNewUserIsActiveDraft: (value: boolean) => selection.updateNewUserDraft("isActive", value),
    selectUser: selection.selectUser,
    selectGroup: selection.selectGroup,
    saveUserMasterData: mutations.saveUserMasterData,
    createUser: mutations.createUser,
    removeUser: mutations.removeUser,
    saveUserRoles: mutations.saveUserRoles,
    saveUserGroups: mutations.saveUserGroups,
    saveGroupRoles: mutations.saveGroupRoles,
    toggleUserRole: selection.toggleUserRole,
    toggleUserGroup: selection.toggleUserGroup,
    toggleGroupRole: selection.toggleGroupRole,
  };
}
