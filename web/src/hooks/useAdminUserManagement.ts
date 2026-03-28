import { useCallback, useEffect, useMemo, useState } from "react";
import type { Dispatch, SetStateAction } from "react";
import {
  toNullableNumber,
  toNullableText,
  toggleId,
} from "../components/admin-config/adminConfigHelpers";
import { useConfirmationDialog } from "../components/feedback/ConfirmationDialogProvider";
import {
  createAdminUser,
  deleteAdminUser,
  getAdminUsers,
  updateAdminGroupRoles,
  updateAdminUserGroups,
  updateAdminUserMasterData,
  updateAdminUserRoles,
} from "../services/adminApi";
import type { AdminGroup, AdminUser } from "../types/auth";

const DEMO_USERS_REFRESH_EVENT = "demo-users-refresh";

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

  const notifyDemoUsersChanged = useCallback(() => {
    if (typeof window === "undefined") {
      return;
    }

    window.dispatchEvent(new Event(DEMO_USERS_REFRESH_EVENT));
  }, []);

  const [userFormError, setUserFormError] = useState<string | null>(null);
  const [userFormNotice, setUserFormNotice] = useState<string | null>(null);
  const [selectedUserId, setSelectedUserId] = useState<number | null>(null);
  const [selectedGroupId, setSelectedGroupId] = useState<number | null>(null);
  const [selectedUserRoleIds, setSelectedUserRoleIds] = useState<number[]>([]);
  const [selectedUserGroupIds, setSelectedUserGroupIds] = useState<number[]>([]);
  const [selectedGroupRoleIds, setSelectedGroupRoleIds] = useState<number[]>([]);
  const [userExternalKeyDraft, setUserExternalKeyDraft] = useState<string>("");
  const [userDisplayNameDraft, setUserDisplayNameDraft] = useState<string>("");
  const [userEmailDraft, setUserEmailDraft] = useState<string>("");
  const [userNotificationEmailDraft, setUserNotificationEmailDraft] = useState<string>("");
  const [userDepartmentIdDraft, setUserDepartmentIdDraft] = useState<string>("");
  const [userIsActiveDraft, setUserIsActiveDraft] = useState<boolean>(true);
  const [newUserExternalKeyDraft, setNewUserExternalKeyDraft] = useState<string>("");
  const [newUserDisplayNameDraft, setNewUserDisplayNameDraft] = useState<string>("");
  const [newUserEmailDraft, setNewUserEmailDraft] = useState<string>("");
  const [newUserNotificationEmailDraft, setNewUserNotificationEmailDraft] = useState<string>("");
  const [newUserDepartmentIdDraft, setNewUserDepartmentIdDraft] = useState<string>("");
  const [newUserIsActiveDraft, setNewUserIsActiveDraft] = useState<boolean>(true);
  const [isSavingUserMasterData, setIsSavingUserMasterData] = useState<boolean>(false);
  const [isSavingUserRoles, setIsSavingUserRoles] = useState<boolean>(false);
  const [isSavingUserGroups, setIsSavingUserGroups] = useState<boolean>(false);
  const [isSavingGroupRoles, setIsSavingGroupRoles] = useState<boolean>(false);
  const [isCreatingUser, setIsCreatingUser] = useState<boolean>(false);
  const [deletingUserId, setDeletingUserId] = useState<number | null>(null);

  const selectedUser = useMemo(() => users.find((user) => user.userId === selectedUserId) ?? null, [users, selectedUserId]);
  const selectedGroup = useMemo(() => groups.find((group) => group.groupId === selectedGroupId) ?? null, [groups, selectedGroupId]);

  const selectUser = useCallback((user: AdminUser) => {
    setSelectedUserId(user.userId);
    setSelectedUserRoleIds(user.roles.map((role) => role.roleId));
    setSelectedUserGroupIds(user.groups.map((group) => group.groupId));
    setUserExternalKeyDraft(user.externalKey ?? "");
    setUserDisplayNameDraft(user.displayName);
    setUserEmailDraft(user.email);
    setUserNotificationEmailDraft(user.notificationEmail ?? "");
    setUserDepartmentIdDraft(user.departmentId ? String(user.departmentId) : "");
    setUserIsActiveDraft(user.isActive);
    setUserFormError(null);
    setUserFormNotice(null);
    onError(null);
    onNotice(null);
  }, [onError, onNotice]);

  const selectGroup = useCallback((groupId: number | null) => {
    setSelectedGroupId(groupId);
    const group = groups.find((item) => item.groupId === groupId) ?? null;
    setSelectedGroupRoleIds(group ? group.roles.map((role) => role.roleId) : []);
    onNotice(null);
  }, [groups, onNotice]);

  useEffect(() => {
    if (!selectedUser) {
      setSelectedUserRoleIds([]);
      setSelectedUserGroupIds([]);
      setUserExternalKeyDraft("");
      setUserDisplayNameDraft("");
      setUserEmailDraft("");
      setUserNotificationEmailDraft("");
      setUserDepartmentIdDraft("");
      setUserIsActiveDraft(true);
      return;
    }

    setSelectedUserRoleIds(selectedUser.roles.map((role) => role.roleId));
    setSelectedUserGroupIds(selectedUser.groups.map((group) => group.groupId));
    setUserExternalKeyDraft(selectedUser.externalKey ?? "");
    setUserDisplayNameDraft(selectedUser.displayName);
    setUserEmailDraft(selectedUser.email);
    setUserNotificationEmailDraft(selectedUser.notificationEmail ?? "");
    setUserDepartmentIdDraft(selectedUser.departmentId ? String(selectedUser.departmentId) : "");
    setUserIsActiveDraft(selectedUser.isActive);
  }, [selectedUser]);

  useEffect(() => {
    if (!selectedGroup) {
      setSelectedGroupRoleIds([]);
      return;
    }

    setSelectedGroupRoleIds(selectedGroup.roles.map((role) => role.roleId));
  }, [selectedGroup]);

  const saveUserMasterData = useCallback(async () => {
    if (!selectedUser) {
      return;
    }

    setIsSavingUserMasterData(true);
    setUserFormError(null);
    setUserFormNotice(null);
    onNotice(null);
    onError(null);

    try {
      const updatedUser = await updateAdminUserMasterData(
        selectedUser.userId,
        userExternalKeyDraft.trim() || null,
        userDisplayNameDraft,
        userEmailDraft,
        toNullableText(userNotificationEmailDraft),
        toNullableNumber(userDepartmentIdDraft),
        userIsActiveDraft
      );

      const freshUsers = await getAdminUsers();
      setUsers(freshUsers);
      setSelectedUserId(updatedUser.userId);
      await refreshCurrentUser();
      notifyDemoUsersChanged();
      setUserFormNotice(`Personenstammdaten für ${updatedUser.displayName} wurden gespeichert.`);
      onNotice(`Personenstammdaten für ${updatedUser.displayName} wurden gespeichert.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Personenstammdaten konnten nicht gespeichert werden.";
      setUserFormError(message);
      onError(message);
    } finally {
      setIsSavingUserMasterData(false);
    }
  }, [
    onError,
    onNotice,
    refreshCurrentUser,
    selectedUser,
    setUsers,
    userDepartmentIdDraft,
    userDisplayNameDraft,
    userEmailDraft,
    userExternalKeyDraft,
    userIsActiveDraft,
    userNotificationEmailDraft,
    notifyDemoUsersChanged,
  ]);

  const createUser = useCallback(async () => {
    setIsCreatingUser(true);
    onNotice(null);
    onError(null);

    try {
      const createdUser = await createAdminUser({
        externalKey: newUserExternalKeyDraft.trim() || null,
        displayName: newUserDisplayNameDraft,
        email: newUserEmailDraft,
        notificationEmail: toNullableText(newUserNotificationEmailDraft),
        departmentId: toNullableNumber(newUserDepartmentIdDraft),
        isActive: newUserIsActiveDraft,
      });
      setUsers((current) =>
        current.concat(createdUser).sort((left, right) => left.displayName.localeCompare(right.displayName, "de"))
      );
      setSelectedUserId(createdUser.userId);
      setNewUserExternalKeyDraft("");
      setNewUserDisplayNameDraft("");
      setNewUserEmailDraft("");
      setNewUserNotificationEmailDraft("");
      setNewUserDepartmentIdDraft("");
      setNewUserIsActiveDraft(true);
      notifyDemoUsersChanged();
      onNotice(`Person ${createdUser.displayName} wurde angelegt.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Person konnte nicht angelegt werden.";
      onError(message);
    } finally {
      setIsCreatingUser(false);
    }
  }, [
    newUserDepartmentIdDraft,
    newUserDisplayNameDraft,
    newUserEmailDraft,
    newUserExternalKeyDraft,
    newUserIsActiveDraft,
    newUserNotificationEmailDraft,
    notifyDemoUsersChanged,
    onError,
    onNotice,
    setUsers,
  ]);

  const removeUser = useCallback(async (user: AdminUser) => {
    const shouldDelete = await confirm({
      title: "Person löschen?",
      description: `Die Person "${user.displayName}" wird dauerhaft entfernt. Rollen- und Gruppenzuordnungen gehen dabei verloren.`,
      confirmLabel: "Person löschen",
      tone: "danger",
    });
    if (!shouldDelete) {
      return;
    }

    setDeletingUserId(user.userId);
    onNotice(null);
    onError(null);

    try {
      await deleteAdminUser(user.userId);
      if (selectedUserId === user.userId) {
        setSelectedUserId(null);
      }
      await reload();
      notifyDemoUsersChanged();
      onNotice(`Person ${user.displayName} wurde gelöscht.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Person konnte nicht gelöscht werden.";
      onError(message);
    } finally {
      setDeletingUserId(null);
    }
  }, [confirm, notifyDemoUsersChanged, onError, onNotice, reload, selectedUserId]);

  const saveUserRoles = useCallback(async () => {
    if (!selectedUser) {
      return;
    }

    setIsSavingUserRoles(true);
    onNotice(null);
    onError(null);

    try {
      const updatedUser = await updateAdminUserRoles(selectedUser.userId, selectedUserRoleIds);
      setUsers((current) => current.map((user) => (user.userId === updatedUser.userId ? updatedUser : user)));
      notifyDemoUsersChanged();
      onNotice(`Rollen für ${updatedUser.displayName} wurden aktualisiert.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Rollen konnten nicht aktualisiert werden.";
      onError(message);
    } finally {
      setIsSavingUserRoles(false);
    }
  }, [notifyDemoUsersChanged, onError, onNotice, selectedUser, selectedUserRoleIds, setUsers]);

  const saveUserGroups = useCallback(async () => {
    if (!selectedUser) {
      return;
    }

    setIsSavingUserGroups(true);
    onNotice(null);
    onError(null);

    try {
      const updatedUser = await updateAdminUserGroups(selectedUser.userId, selectedUserGroupIds);
      setUsers((current) => current.map((user) => (user.userId === updatedUser.userId ? updatedUser : user)));
      notifyDemoUsersChanged();
      onNotice(`Gruppen für ${updatedUser.displayName} wurden aktualisiert.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Gruppen konnten nicht aktualisiert werden.";
      onError(message);
    } finally {
      setIsSavingUserGroups(false);
    }
  }, [notifyDemoUsersChanged, onError, onNotice, selectedUser, selectedUserGroupIds, setUsers]);

  const saveGroupRoles = useCallback(async () => {
    if (!selectedGroup) {
      return;
    }

    setIsSavingGroupRoles(true);
    onNotice(null);
    onError(null);

    try {
      const updatedGroup = await updateAdminGroupRoles(selectedGroup.groupId, selectedGroupRoleIds);
      setGroups((current) => current.map((group) => (group.groupId === updatedGroup.groupId ? updatedGroup : group)));
      onNotice(`Gruppenrollen für ${updatedGroup.groupName} wurden aktualisiert.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Gruppenrollen konnten nicht aktualisiert werden.";
      onError(message);
    } finally {
      setIsSavingGroupRoles(false);
    }
  }, [onError, onNotice, selectedGroup, selectedGroupRoleIds, setGroups]);

  const toggleUserRole = useCallback((roleId: number) => {
    setSelectedUserRoleIds((current) => toggleId(current, roleId));
  }, []);

  const toggleUserGroup = useCallback((groupId: number) => {
    setSelectedUserGroupIds((current) => toggleId(current, groupId));
  }, []);

  const toggleGroupRole = useCallback((roleId: number) => {
    setSelectedGroupRoleIds((current) => toggleId(current, roleId));
  }, []);

  return {
    userFormError,
    userFormNotice,
    selectedUserId,
    selectedGroupId,
    selectedUserRoleIds,
    selectedUserGroupIds,
    selectedGroupRoleIds,
    userExternalKeyDraft,
    userDisplayNameDraft,
    userEmailDraft,
    userNotificationEmailDraft,
    userDepartmentIdDraft,
    userIsActiveDraft,
    newUserExternalKeyDraft,
    newUserDisplayNameDraft,
    newUserEmailDraft,
    newUserNotificationEmailDraft,
    newUserDepartmentIdDraft,
    newUserIsActiveDraft,
    isSavingUserMasterData,
    isSavingUserRoles,
    isSavingUserGroups,
    isSavingGroupRoles,
    isCreatingUser,
    deletingUserId,
    selectedUser,
    selectedGroup,
    setUserExternalKeyDraft,
    setUserDisplayNameDraft,
    setUserEmailDraft,
    setUserNotificationEmailDraft,
    setUserDepartmentIdDraft,
    setUserIsActiveDraft,
    setNewUserExternalKeyDraft,
    setNewUserDisplayNameDraft,
    setNewUserEmailDraft,
    setNewUserNotificationEmailDraft,
    setNewUserDepartmentIdDraft,
    setNewUserIsActiveDraft,
    selectUser,
    selectGroup,
    saveUserMasterData,
    createUser,
    removeUser,
    saveUserRoles,
    saveUserGroups,
    saveGroupRoles,
    toggleUserRole,
    toggleUserGroup,
    toggleGroupRole,
  };
}
