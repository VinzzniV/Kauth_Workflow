import { useCallback, useEffect, useMemo, useState } from "react";
import type { Dispatch, SetStateAction } from "react";
import {
  toNullableNumber,
  toNullableText,
  toggleId,
} from "../components/admin-config/adminConfigHelpers";
import { useConfirmationDialog } from "../components/feedback/useConfirmationDialog";
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

const SIMULATION_USERS_REFRESH_EVENT = "sim-users-refresh";

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

export type UserEditDraft = {
  externalKey: string;
  displayName: string;
  email: string;
  notificationEmail: string;
  departmentId: string;
  isActive: boolean;
};

type UserNewDraft = UserEditDraft;

type SavingOperation =
  | "masterData"
  | "userRoles"
  | "userGroups"
  | "groupRoles"
  | "creating"
  | null;

const EMPTY_USER_EDIT_DRAFT: UserEditDraft = {
  externalKey: "",
  displayName: "",
  email: "",
  notificationEmail: "",
  departmentId: "",
  isActive: true,
};

const EMPTY_USER_NEW_DRAFT: UserNewDraft = {
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

function buildSelectedUserSignature(user: AdminUser | null): string | null {
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

function buildSelectedGroupSignature(group: AdminGroup | null): string | null {
  if (!group) {
    return null;
  }

  return [
    group.groupId,
    group.roles.map((role) => role.roleId).join(","),
  ].join("|");
}

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

  const notifySimulationUsersChanged = useCallback(() => {
    if (typeof window === "undefined") {
      return;
    }

    window.dispatchEvent(new Event(SIMULATION_USERS_REFRESH_EVENT));
  }, []);

  const [userFormError, setUserFormError] = useState<string | null>(null);
  const [userFormNotice, setUserFormNotice] = useState<string | null>(null);
  const [selectedUserId, setSelectedUserId] = useState<number | null>(null);
  const [selectedGroupId, setSelectedGroupId] = useState<number | null>(null);
  const [selectedUserRoleIds, setSelectedUserRoleIds] = useState<number[]>([]);
  const [selectedUserGroupIds, setSelectedUserGroupIds] = useState<number[]>([]);
  const [selectedGroupRoleIds, setSelectedGroupRoleIds] = useState<number[]>([]);
  const [editDraft, setEditDraft] = useState<UserEditDraft>(EMPTY_USER_EDIT_DRAFT);
  const [newUserDraft, setNewUserDraft] = useState<UserNewDraft>(EMPTY_USER_NEW_DRAFT);
  const [savingOperation, setSavingOperation] = useState<SavingOperation>(null);
  const [deletingUserId, setDeletingUserId] = useState<number | null>(null);
  const [lastSyncedSelectedUserSignature, setLastSyncedSelectedUserSignature] = useState<string | null>(null);
  const [lastSyncedSelectedGroupSignature, setLastSyncedSelectedGroupSignature] = useState<string | null>(null);

  const selectedUser = useMemo(
    () => users.find((user) => user.userId === selectedUserId) ?? null,
    [users, selectedUserId]
  );
  const selectedGroup = useMemo(
    () => groups.find((group) => group.groupId === selectedGroupId) ?? null,
    [groups, selectedGroupId]
  );
  const selectedUserSignature = useMemo(() => buildSelectedUserSignature(selectedUser), [selectedUser]);
  const selectedGroupSignature = useMemo(() => buildSelectedGroupSignature(selectedGroup), [selectedGroup]);

  const selectUser = useCallback((user: AdminUser) => {
    setSelectedUserId(user.userId);
    setSelectedUserRoleIds(user.roles.map((role) => role.roleId));
    setSelectedUserGroupIds(user.groups.map((group) => group.groupId));
    setEditDraft(buildEditDraft(user));
    setLastSyncedSelectedUserSignature(buildSelectedUserSignature(user));
    setUserFormError(null);
    setUserFormNotice(null);
    onError(null);
    onNotice(null);
  }, [onError, onNotice]);

  const selectGroup = useCallback((groupId: number | null) => {
    setSelectedGroupId(groupId);
    const group = groups.find((item) => item.groupId === groupId) ?? null;
    setSelectedGroupRoleIds(group ? group.roles.map((role) => role.roleId) : []);
    setLastSyncedSelectedGroupSignature(buildSelectedGroupSignature(group));
    onNotice(null);
  }, [groups, onNotice]);

  useEffect(() => {
    if (!selectedUser) {
      setSelectedUserRoleIds([]);
      setSelectedUserGroupIds([]);
      setEditDraft(EMPTY_USER_EDIT_DRAFT);
      setLastSyncedSelectedUserSignature(null);
      return;
    }

    if (selectedUserSignature === lastSyncedSelectedUserSignature) {
      return;
    }

    setSelectedUserRoleIds(selectedUser.roles.map((role) => role.roleId));
    setSelectedUserGroupIds(selectedUser.groups.map((group) => group.groupId));
    setEditDraft(buildEditDraft(selectedUser));
    setLastSyncedSelectedUserSignature(selectedUserSignature);
  }, [lastSyncedSelectedUserSignature, selectedUser, selectedUserSignature]);

  useEffect(() => {
    if (!selectedGroup) {
      setSelectedGroupRoleIds([]);
      setLastSyncedSelectedGroupSignature(null);
      return;
    }

    if (selectedGroupSignature === lastSyncedSelectedGroupSignature) {
      return;
    }

    setSelectedGroupRoleIds(selectedGroup.roles.map((role) => role.roleId));
    setLastSyncedSelectedGroupSignature(selectedGroupSignature);
  }, [lastSyncedSelectedGroupSignature, selectedGroup, selectedGroupSignature]);

  const saveUserMasterData = useCallback(async () => {
    if (!selectedUser) {
      return;
    }

    setSavingOperation("masterData");
    setUserFormError(null);
    setUserFormNotice(null);
    onNotice(null);
    onError(null);

    try {
      const updatedUser = await updateAdminUserMasterData(
        selectedUser.userId,
        editDraft.externalKey.trim() || null,
        editDraft.displayName,
        editDraft.email,
        toNullableText(editDraft.notificationEmail),
        toNullableNumber(editDraft.departmentId),
        editDraft.isActive
      );

      const freshUsers = await getAdminUsers();
      setUsers(freshUsers);
      setSelectedUserId(updatedUser.userId);
      setEditDraft(buildEditDraft(updatedUser));
      setLastSyncedSelectedUserSignature(buildSelectedUserSignature(updatedUser));
      await refreshCurrentUser();
      notifySimulationUsersChanged();
      setUserFormNotice(`Personenstammdaten für ${updatedUser.displayName} wurden gespeichert.`);
      onNotice(`Personenstammdaten für ${updatedUser.displayName} wurden gespeichert.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Personenstammdaten konnten nicht gespeichert werden.";
      setUserFormError(message);
      onError(message);
    } finally {
      setSavingOperation(null);
    }
  }, [
    editDraft,
    notifySimulationUsersChanged,
    onError,
    onNotice,
    refreshCurrentUser,
    selectedUser,
    setUsers,
  ]);

  const createUser = useCallback(async () => {
    setSavingOperation("creating");
    onNotice(null);
    onError(null);

    try {
      const createdUser = await createAdminUser({
        externalKey: newUserDraft.externalKey.trim() || null,
        displayName: newUserDraft.displayName,
        email: newUserDraft.email,
        notificationEmail: toNullableText(newUserDraft.notificationEmail),
        departmentId: toNullableNumber(newUserDraft.departmentId),
        isActive: newUserDraft.isActive,
      });
      setUsers((current) =>
        current.concat(createdUser).sort((left, right) => left.displayName.localeCompare(right.displayName, "de"))
      );
      setSelectedUserId(createdUser.userId);
      setEditDraft(buildEditDraft(createdUser));
      setLastSyncedSelectedUserSignature(buildSelectedUserSignature(createdUser));
      setNewUserDraft(EMPTY_USER_NEW_DRAFT);
      notifySimulationUsersChanged();
      onNotice(`Person ${createdUser.displayName} wurde angelegt.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Person konnte nicht angelegt werden.";
      onError(message);
    } finally {
      setSavingOperation(null);
    }
  }, [newUserDraft, notifySimulationUsersChanged, onError, onNotice, setUsers]);

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
      notifySimulationUsersChanged();
      onNotice(`Person ${user.displayName} wurde gelöscht.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Person konnte nicht gelöscht werden.";
      onError(message);
    } finally {
      setDeletingUserId(null);
    }
  }, [confirm, notifySimulationUsersChanged, onError, onNotice, reload, selectedUserId]);

  const saveUserRoles = useCallback(async () => {
    if (!selectedUser) {
      return;
    }

    setSavingOperation("userRoles");
    onNotice(null);
    onError(null);

    try {
      const updatedUser = await updateAdminUserRoles(selectedUser.userId, selectedUserRoleIds);
      setUsers((current) => current.map((user) => (user.userId === updatedUser.userId ? updatedUser : user)));
      setLastSyncedSelectedUserSignature(buildSelectedUserSignature(updatedUser));
      notifySimulationUsersChanged();
      onNotice(`Rollen für ${updatedUser.displayName} wurden aktualisiert.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Rollen konnten nicht aktualisiert werden.";
      onError(message);
    } finally {
      setSavingOperation(null);
    }
  }, [notifySimulationUsersChanged, onError, onNotice, selectedUser, selectedUserRoleIds, setUsers]);

  const saveUserGroups = useCallback(async () => {
    if (!selectedUser) {
      return;
    }

    setSavingOperation("userGroups");
    onNotice(null);
    onError(null);

    try {
      const updatedUser = await updateAdminUserGroups(selectedUser.userId, selectedUserGroupIds);
      setUsers((current) => current.map((user) => (user.userId === updatedUser.userId ? updatedUser : user)));
      setLastSyncedSelectedUserSignature(buildSelectedUserSignature(updatedUser));
      notifySimulationUsersChanged();
      onNotice(`Gruppen für ${updatedUser.displayName} wurden aktualisiert.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Gruppen konnten nicht aktualisiert werden.";
      onError(message);
    } finally {
      setSavingOperation(null);
    }
  }, [notifySimulationUsersChanged, onError, onNotice, selectedUser, selectedUserGroupIds, setUsers]);

  const saveGroupRoles = useCallback(async () => {
    if (!selectedGroup) {
      return;
    }

    setSavingOperation("groupRoles");
    onNotice(null);
    onError(null);

    try {
      const updatedGroup = await updateAdminGroupRoles(selectedGroup.groupId, selectedGroupRoleIds);
      setGroups((current) => current.map((group) => (group.groupId === updatedGroup.groupId ? updatedGroup : group)));
      setLastSyncedSelectedGroupSignature(buildSelectedGroupSignature(updatedGroup));
      onNotice(`Gruppenrollen für ${updatedGroup.groupName} wurden aktualisiert.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Gruppenrollen konnten nicht aktualisiert werden.";
      onError(message);
    } finally {
      setSavingOperation(null);
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

  const updateEditDraft = useCallback(<K extends keyof UserEditDraft>(key: K, value: UserEditDraft[K]) => {
    setEditDraft((current) => ({ ...current, [key]: value }));
  }, []);

  const updateNewUserDraft = useCallback(<K extends keyof UserNewDraft>(key: K, value: UserNewDraft[K]) => {
    setNewUserDraft((current) => ({ ...current, [key]: value }));
  }, []);

  return {
    userFormError,
    userFormNotice,
    selectedUserId,
    selectedGroupId,
    selectedUserRoleIds,
    selectedUserGroupIds,
    selectedGroupRoleIds,
    userExternalKeyDraft: editDraft.externalKey,
    userDisplayNameDraft: editDraft.displayName,
    userEmailDraft: editDraft.email,
    userNotificationEmailDraft: editDraft.notificationEmail,
    userDepartmentIdDraft: editDraft.departmentId,
    userIsActiveDraft: editDraft.isActive,
    newUserExternalKeyDraft: newUserDraft.externalKey,
    newUserDisplayNameDraft: newUserDraft.displayName,
    newUserEmailDraft: newUserDraft.email,
    newUserNotificationEmailDraft: newUserDraft.notificationEmail,
    newUserDepartmentIdDraft: newUserDraft.departmentId,
    newUserIsActiveDraft: newUserDraft.isActive,
    isSavingUserMasterData: savingOperation === "masterData",
    isSavingUserRoles: savingOperation === "userRoles",
    isSavingUserGroups: savingOperation === "userGroups",
    isSavingGroupRoles: savingOperation === "groupRoles",
    isCreatingUser: savingOperation === "creating",
    deletingUserId,
    selectedUser,
    selectedGroup,
    setUserExternalKeyDraft: (value: string) => updateEditDraft("externalKey", value),
    setUserDisplayNameDraft: (value: string) => updateEditDraft("displayName", value),
    setUserEmailDraft: (value: string) => updateEditDraft("email", value),
    setUserNotificationEmailDraft: (value: string) => updateEditDraft("notificationEmail", value),
    setUserDepartmentIdDraft: (value: string) => updateEditDraft("departmentId", value),
    setUserIsActiveDraft: (value: boolean) => updateEditDraft("isActive", value),
    setNewUserExternalKeyDraft: (value: string) => updateNewUserDraft("externalKey", value),
    setNewUserDisplayNameDraft: (value: string) => updateNewUserDraft("displayName", value),
    setNewUserEmailDraft: (value: string) => updateNewUserDraft("email", value),
    setNewUserNotificationEmailDraft: (value: string) => updateNewUserDraft("notificationEmail", value),
    setNewUserDepartmentIdDraft: (value: string) => updateNewUserDraft("departmentId", value),
    setNewUserIsActiveDraft: (value: boolean) => updateNewUserDraft("isActive", value),
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
