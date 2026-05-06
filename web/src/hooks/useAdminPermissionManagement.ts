import { useCallback, useEffect, useState, type Dispatch, type SetStateAction } from "react";
import type { AdminPermissionOverrideDraft } from "../components/admin-config/AdminPermissionsSection";
import { getAdminPermissionAudit, getAdminUsers, updateAdminRolePermissions, updateAdminUserPermissionOverrides } from "../services/adminApi";
import type { AdminPermissionAuditEntry, AdminRole, AdminUser } from "../types/auth";

type UseAdminPermissionManagementOptions = {
  roles: AdminRole[];
  selectedUser: AdminUser | null;
  refreshCurrentUser: () => Promise<void>;
  setRoles: Dispatch<SetStateAction<AdminRole[]>>;
  setUsers: Dispatch<SetStateAction<AdminUser[]>>;
  setPermissionAuditEntries: Dispatch<SetStateAction<AdminPermissionAuditEntry[]>>;
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
};

export function useAdminPermissionManagement({
  roles,
  selectedUser,
  refreshCurrentUser,
  setRoles,
  setUsers,
  setPermissionAuditEntries,
  onNotice,
  onError,
}: UseAdminPermissionManagementOptions) {
  const [selectedRoleId, setSelectedRoleId] = useState<number | null>(null);
  const [selectedRolePermissionIds, setSelectedRolePermissionIds] = useState<number[]>([]);
  const [userOverrideDrafts, setUserOverrideDrafts] = useState<AdminPermissionOverrideDraft[]>([]);
  const [isSavingRolePermissions, setIsSavingRolePermissions] = useState(false);
  const [isSavingUserOverrides, setIsSavingUserOverrides] = useState(false);

  useEffect(() => {
    if (!selectedRoleId) {
      setSelectedRolePermissionIds([]);
      return;
    }

    const selectedRole = roles.find((role) => role.roleId === selectedRoleId) ?? null;
    if (!selectedRole) {
      setSelectedRoleId(null);
      setSelectedRolePermissionIds([]);
      return;
    }

    setSelectedRolePermissionIds(selectedRole.permissions.map((permission) => permission.permissionId));
  }, [roles, selectedRoleId]);

  useEffect(() => {
    if (!selectedUser) {
      setUserOverrideDrafts([]);
      return;
    }

    setUserOverrideDrafts(
      selectedUser.permissionOverrides.map((override) => ({
        permissionId: override.permissionId,
        effect: override.effect,
        scope: override.scope,
        scopeDepartmentId: override.scopeDepartmentId,
      }))
    );
  }, [selectedUser]);

  const handleSelectRole = useCallback((roleId: number | null) => {
    setSelectedRoleId(roleId);
  }, []);

  const handleToggleRolePermission = useCallback((permissionId: number) => {
    setSelectedRolePermissionIds((current) =>
      current.includes(permissionId)
        ? current.filter((item) => item !== permissionId)
        : current.concat(permissionId)
    );
  }, []);

  const handleSaveRolePermissions = useCallback(async () => {
    if (!selectedRoleId) {
      return;
    }

    setIsSavingRolePermissions(true);
    onNotice(null);
    onError(null);

    try {
      const updatedRole = await updateAdminRolePermissions(selectedRoleId, selectedRolePermissionIds);
      const [freshUsers, auditPage] = await Promise.all([getAdminUsers(), getAdminPermissionAudit({ limit: 50 })]);
      setRoles((current) => current.map((role) => (role.roleId === updatedRole.roleId ? updatedRole : role)));
      setUsers(freshUsers);
      setPermissionAuditEntries(auditPage.items);
      await refreshCurrentUser();
      onNotice(`Permission-Bundle für ${updatedRole.roleName} wurde aktualisiert.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Rollen-Permissions konnten nicht gespeichert werden.";
      onError(message);
    } finally {
      setIsSavingRolePermissions(false);
    }
  }, [onError, onNotice, refreshCurrentUser, selectedRoleId, selectedRolePermissionIds, setPermissionAuditEntries, setRoles, setUsers]);

  const handleSaveUserOverrides = useCallback(async () => {
    if (!selectedUser) {
      return;
    }

    setIsSavingUserOverrides(true);
    onNotice(null);
    onError(null);

    try {
      const updatedUser = await updateAdminUserPermissionOverrides(selectedUser.userId, userOverrideDrafts);
      const auditPage = await getAdminPermissionAudit({ limit: 50 });
      setUsers((current) => current.map((user) => (user.userId === updatedUser.userId ? updatedUser : user)));
      setPermissionAuditEntries(auditPage.items);
      await refreshCurrentUser();
      onNotice(`Lokale Permission-Overrides für ${updatedUser.displayName} wurden gespeichert.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Permission-Overrides konnten nicht gespeichert werden.";
      onError(message);
    } finally {
      setIsSavingUserOverrides(false);
    }
  }, [onError, onNotice, refreshCurrentUser, selectedUser, setPermissionAuditEntries, setUsers, userOverrideDrafts]);

  return {
    selectedRoleId,
    selectedRolePermissionIds,
    userOverrideDrafts,
    isSavingRolePermissions,
    isSavingUserOverrides,
    setUserOverrideDrafts,
    handleSelectRole,
    handleToggleRolePermission,
    handleSaveRolePermissions,
    handleSaveUserOverrides,
  };
}
