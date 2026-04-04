import { useCallback, useEffect, useMemo, useState } from "react";
import type { AdminGroup, AdminUser } from "../types/auth";
import { toggleId } from "../components/admin-config/adminConfigHelpers";
import {
  buildEditDraft,
  buildSelectedGroupSignature,
  buildSelectedUserSignature,
  EMPTY_USER_EDIT_DRAFT,
  EMPTY_USER_NEW_DRAFT,
  type UserEditDraft,
  type UserNewDraft,
} from "./adminUserManagementModel";

function scheduleStateSync(sync: () => void) {
  let cancelled = false;
  queueMicrotask(() => {
    if (!cancelled) {
      sync();
    }
  });

  return () => {
    cancelled = true;
  };
}

type UseAdminUserSelectionStateOptions = {
  users: AdminUser[];
  groups: AdminGroup[];
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
};

export function useAdminUserSelectionState({
  users,
  groups,
  onNotice,
  onError,
}: UseAdminUserSelectionStateOptions) {
  const [userFormError, setUserFormError] = useState<string | null>(null);
  const [userFormNotice, setUserFormNotice] = useState<string | null>(null);
  const [selectedUserId, setSelectedUserId] = useState<number | null>(null);
  const [selectedGroupId, setSelectedGroupId] = useState<number | null>(null);
  const [selectedUserRoleIds, setSelectedUserRoleIds] = useState<number[]>([]);
  const [selectedUserGroupIds, setSelectedUserGroupIds] = useState<number[]>([]);
  const [selectedGroupRoleIds, setSelectedGroupRoleIds] = useState<number[]>([]);
  const [editDraft, setEditDraft] = useState<UserEditDraft>(EMPTY_USER_EDIT_DRAFT);
  const [newUserDraft, setNewUserDraft] = useState<UserNewDraft>(EMPTY_USER_NEW_DRAFT);
  const [deletingUserId, setDeletingUserId] = useState<number | null>(null);
  const [lastSyncedSelectedUserSignature, setLastSyncedSelectedUserSignature] = useState<string | null>(null);
  const [lastSyncedSelectedGroupSignature, setLastSyncedSelectedGroupSignature] = useState<string | null>(null);

  const selectedUser = useMemo(() => users.find((user) => user.userId === selectedUserId) ?? null, [users, selectedUserId]);
  const selectedGroup = useMemo(() => groups.find((group) => group.groupId === selectedGroupId) ?? null, [groups, selectedGroupId]);
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
      return scheduleStateSync(() => {
        setSelectedUserRoleIds([]);
        setSelectedUserGroupIds([]);
        setEditDraft(EMPTY_USER_EDIT_DRAFT);
        setLastSyncedSelectedUserSignature(null);
      });
    }

    if (selectedUserSignature === lastSyncedSelectedUserSignature) {
      return;
    }

    return scheduleStateSync(() => {
      setSelectedUserRoleIds(selectedUser.roles.map((role) => role.roleId));
      setSelectedUserGroupIds(selectedUser.groups.map((group) => group.groupId));
      setEditDraft(buildEditDraft(selectedUser));
      setLastSyncedSelectedUserSignature(selectedUserSignature);
    });
  }, [lastSyncedSelectedUserSignature, selectedUser, selectedUserSignature]);

  useEffect(() => {
    if (!selectedGroup) {
      return scheduleStateSync(() => {
        setSelectedGroupRoleIds([]);
        setLastSyncedSelectedGroupSignature(null);
      });
    }

    if (selectedGroupSignature === lastSyncedSelectedGroupSignature) {
      return;
    }

    return scheduleStateSync(() => {
      setSelectedGroupRoleIds(selectedGroup.roles.map((role) => role.roleId));
      setLastSyncedSelectedGroupSignature(selectedGroupSignature);
    });
  }, [lastSyncedSelectedGroupSignature, selectedGroup, selectedGroupSignature]);

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
    setUserFormError,
    userFormNotice,
    setUserFormNotice,
    selectedUserId,
    setSelectedUserId,
    selectedGroupId,
    selectedUserRoleIds,
    setSelectedUserRoleIds,
    selectedUserGroupIds,
    setSelectedUserGroupIds,
    selectedGroupRoleIds,
    setSelectedGroupRoleIds,
    editDraft,
    setEditDraft,
    newUserDraft,
    setNewUserDraft,
    deletingUserId,
    setDeletingUserId,
    selectedUser,
    selectedGroup,
    setLastSyncedSelectedUserSignature,
    setLastSyncedSelectedGroupSignature,
    selectUser,
    selectGroup,
    toggleUserRole,
    toggleUserGroup,
    toggleGroupRole,
    updateEditDraft,
    updateNewUserDraft,
  };
}
