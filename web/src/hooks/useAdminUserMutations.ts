import { useCallback } from "react";
import type { Dispatch, SetStateAction } from "react";
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
import { toNullableNumber, toNullableText } from "../components/admin-config/adminConfigHelpers";
import {
  buildEditDraft,
  buildSelectedGroupSignature,
  buildSelectedUserSignature,
  EMPTY_USER_NEW_DRAFT,
  type SavingOperation,
} from "./adminUserManagementModel";

export const SIMULATION_USERS_REFRESH_EVENT = "sim-users-refresh";

type UseAdminUserMutationsOptions = {
  confirm: ReturnType<typeof import("../components/feedback/useConfirmationDialog").useConfirmationDialog>;
  setUsers: Dispatch<SetStateAction<AdminUser[]>>;
  setGroups: Dispatch<SetStateAction<AdminGroup[]>>;
  refreshCurrentUser: () => Promise<void>;
  reload: () => Promise<void>;
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
  selectedUserId: number | null;
  selectedUser: AdminUser | null;
  selectedGroup: AdminGroup | null;
  selectedUserRoleIds: number[];
  selectedUserGroupIds: number[];
  selectedGroupRoleIds: number[];
  editDraft: ReturnType<typeof buildEditDraft>;
  newUserDraft: ReturnType<typeof buildEditDraft>;
  setSelectedUserId: Dispatch<SetStateAction<number | null>>;
  setEditDraft: Dispatch<SetStateAction<ReturnType<typeof buildEditDraft>>>;
  setNewUserDraft: Dispatch<SetStateAction<ReturnType<typeof buildEditDraft>>>;
  setUsersFormNotice: (message: string | null) => void;
  setUsersFormError: (message: string | null) => void;
  setDeletingUserId: Dispatch<SetStateAction<number | null>>;
  setSavingOperation: Dispatch<SetStateAction<SavingOperation>>;
  setLastSyncedSelectedUserSignature: (signature: string | null) => void;
  setLastSyncedSelectedGroupSignature: (signature: string | null) => void;
};

export function useAdminUserMutations({
  confirm,
  setUsers,
  setGroups,
  refreshCurrentUser,
  reload,
  onNotice,
  onError,
  selectedUserId,
  selectedUser,
  selectedGroup,
  selectedUserRoleIds,
  selectedUserGroupIds,
  selectedGroupRoleIds,
  editDraft,
  newUserDraft,
  setSelectedUserId,
  setEditDraft,
  setNewUserDraft,
  setUsersFormNotice,
  setUsersFormError,
  setDeletingUserId,
  setSavingOperation,
  setLastSyncedSelectedUserSignature,
  setLastSyncedSelectedGroupSignature,
}: UseAdminUserMutationsOptions) {
  const notifySimulationUsersChanged = useCallback(() => {
    if (typeof window === "undefined") {
      return;
    }

    window.dispatchEvent(new Event(SIMULATION_USERS_REFRESH_EVENT));
  }, []);

  const saveUserMasterData = useCallback(async () => {
    if (!selectedUser) {
      return;
    }

    setSavingOperation("masterData");
    setUsersFormError(null);
    setUsersFormNotice(null);
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
      setUsersFormNotice(`Personenstammdaten für ${updatedUser.displayName} wurden gespeichert.`);
      onNotice(`Personenstammdaten für ${updatedUser.displayName} wurden gespeichert.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Personenstammdaten konnten nicht gespeichert werden.";
      setUsersFormError(message);
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
    setEditDraft,
    setLastSyncedSelectedUserSignature,
    setSavingOperation,
    setSelectedUserId,
    setUsers,
    setUsersFormError,
    setUsersFormNotice,
  ]);

  const createUserMutation = useCallback(async () => {
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
      setUsers((current) => current.concat(createdUser).sort((left, right) => left.displayName.localeCompare(right.displayName, "de")));
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
  }, [
    newUserDraft,
    notifySimulationUsersChanged,
    onError,
    onNotice,
    setEditDraft,
    setLastSyncedSelectedUserSignature,
    setNewUserDraft,
    setSavingOperation,
    setSelectedUserId,
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
      notifySimulationUsersChanged();
      onNotice(`Person ${user.displayName} wurde gelöscht.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Person konnte nicht gelöscht werden.";
      onError(message);
    } finally {
      setDeletingUserId(null);
    }
  }, [confirm, notifySimulationUsersChanged, onError, onNotice, reload, selectedUserId, setDeletingUserId, setSelectedUserId]);

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
  }, [notifySimulationUsersChanged, onError, onNotice, selectedUser, selectedUserRoleIds, setLastSyncedSelectedUserSignature, setSavingOperation, setUsers]);

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
  }, [notifySimulationUsersChanged, onError, onNotice, selectedUser, selectedUserGroupIds, setLastSyncedSelectedUserSignature, setSavingOperation, setUsers]);

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
  }, [onError, onNotice, selectedGroup, selectedGroupRoleIds, setGroups, setLastSyncedSelectedGroupSignature, setSavingOperation]);

  return {
    saveUserMasterData,
    createUser: createUserMutation,
    removeUser,
    saveUserRoles,
    saveUserGroups,
    saveGroupRoles,
  };
}
