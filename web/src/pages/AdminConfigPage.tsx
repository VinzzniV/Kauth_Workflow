import { useState } from "react";
import { Navigate, useSearchParams } from "react-router-dom";
import { useCurrentUser } from "../auth/useCurrentUser";
import { AdminConfigWorkspaceContent } from "../components/admin-config/AdminConfigWorkspaceContent";
import { AdminWorkspaceNavigation } from "../components/admin-config/AdminWorkspaceNavigation";
import {
  normalizeAdminOrganizationEntity,
  normalizeAdminWorkspaceSection,
  parseAdminWorkspaceId,
} from "../components/admin-config/adminWorkspaceModel";
import EmptyState from "../components/feedback/EmptyState";
import LoadingState from "../components/feedback/LoadingState";
import PageHeader from "../components/layout/PageHeader";
import { useAdminConfigData } from "../hooks/useAdminConfigData";
import { useAdminConfigPageView } from "../hooks/useAdminConfigPageView";
import { useAdminGraphApplicationConfiguration } from "../hooks/useAdminGraphApplicationConfiguration";
import { useAdminNotificationEmailConfiguration } from "../hooks/useAdminNotificationEmailConfiguration";
import { useAdminOrganizationManagement } from "../hooks/useAdminOrganizationManagement";
import { useAdminPermissionManagement } from "../hooks/useAdminPermissionManagement";
import { useAdminUserManagement } from "../hooks/useAdminUserManagement";

export default function AdminConfigPage() {
  const { refreshCurrentUser } = useCurrentUser();
  const [searchParams, setSearchParams] = useSearchParams();
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const rawSection = (searchParams.get("section") ?? "").trim().toLowerCase();

  if (rawSection === "builder" || rawSection === "templates" || rawSection === "answers" || rawSection === "defaults") {
    return <Navigate to="/builder" replace />;
  }

  const section = normalizeAdminWorkspaceSection(searchParams.get("section"));
  const organizationEntity = normalizeAdminOrganizationEntity(searchParams.get("entity"));
  const selectedEntityId = parseAdminWorkspaceId(searchParams.get("id"));

  const {
    graphApplicationConfiguration,
    setGraphApplicationConfiguration,
  } = useAdminGraphApplicationConfiguration();

  const notificationConfig = useAdminNotificationEmailConfiguration({
    onNotice: setNotice,
    onError: setError,
  });

  const {
    users,
    setUsers,
    roles,
    setRoles,
    groups,
    setGroups,
    permissions,
    permissionAuditEntries,
    setPermissionAuditEntries,
    directoryGroups,
    directoryIdentities,
    directoryAuditEntries,
    directoryStatus,
    departmentAssignments,
    setDepartmentAssignments,
    departmentPositions,
    setDepartmentPositions,
    responsibilityOwners,
    setResponsibilityOwners,
    isLoading,
    isLoadingTechnicalAccess,
    isLoadingDirectory,
    isSyncingDirectory,
    savingDirectoryGroupId,
    deletingDirectoryMappingId,
    hasLoadedTechnicalAccess,
    reload,
    handleSyncDirectory,
    handleCreateDirectoryMapping,
    handleDeleteDirectoryMapping,
  } = useAdminConfigData({
    section,
    setError,
    setNotice,
    setGraphApplicationConfiguration,
    setNotificationEmailConfiguration: notificationConfig.setNotificationEmailConfiguration,
  });

  const {
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
  } = useAdminUserManagement({
    users,
    groups,
    setUsers,
    setGroups,
    refreshCurrentUser,
    reload,
    onNotice: setNotice,
    onError: setError,
  });

  const {
    newDepartmentNameDraft,
    newPositionNameDraft,
    newResponsibilityDraft,
    departmentDrafts,
    positionDrafts,
    responsibilityDrafts,
    isCreatingDepartment,
    creatingPositionDepartmentId,
    isCreatingResponsibility,
    deletingDepartmentId,
    deletingPositionId,
    deletingResponsibilityId,
    savingDepartmentId,
    savingPositionId,
    savingResponsibilityId,
    setNewDepartmentNameDraft,
    setNewPositionNameDraft,
    setNewResponsibilityDraft,
    setDepartmentDrafts,
    setPositionDrafts,
    setResponsibilityDrafts,
    createDepartment,
    createDepartmentPosition,
    createResponsibility,
    removeDepartment,
    removeDepartmentPosition,
    removeResponsibility,
    saveDepartmentAssignment,
    saveDepartmentPosition,
    saveResponsibilityAssignment,
  } = useAdminOrganizationManagement({
    departmentAssignments,
    departmentPositions,
    responsibilityOwners,
    setDepartmentAssignments,
    setDepartmentPositions,
    setResponsibilityOwners,
    reload,
    onNotice: setNotice,
    onError: setError,
  });

  const {
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
  } = useAdminPermissionManagement({
    roles,
    selectedUser,
    refreshCurrentUser,
    setRoles,
    setUsers,
    setPermissionAuditEntries,
    onNotice: setNotice,
    onError: setError,
  });

  const { hasAnyData, handleSelectSection, workspaceContentProps } = useAdminConfigPageView({
    searchParams,
    setSearchParams,
    section,
    organizationEntity,
    selectedEntityId,
    users,
    departmentPositions,
    roles,
    groups,
    permissions,
    permissionAuditEntries,
    directoryGroups,
    directoryIdentities,
    directoryAuditEntries,
    directoryStatus,
    departmentAssignments,
    responsibilityOwners,
    hasLoadedTechnicalAccess,
    isLoadingTechnicalAccess,
    isLoadingDirectory,
    isSyncingDirectory,
    savingDirectoryGroupId,
    deletingDirectoryMappingId,
    graphApplicationConfiguration,
    notificationConfig,
    selectedUserId,
    selectedUser,
    selectedGroupId,
    selectedGroup,
    selectedUserRoleIds,
    selectedUserGroupIds,
    selectedGroupRoleIds,
    selectedRoleId,
    selectedRolePermissionIds,
    userOverrideDrafts,
    userFormError,
    userFormNotice,
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
    newDepartmentNameDraft,
    newPositionNameDraft,
    newResponsibilityDraft,
    departmentDrafts,
    positionDrafts,
    responsibilityDrafts,
    isCreatingDepartment,
    creatingPositionDepartmentId,
    isCreatingResponsibility,
    deletingDepartmentId,
    deletingPositionId,
    deletingResponsibilityId,
    savingDepartmentId,
    savingPositionId,
    savingResponsibilityId,
    isSavingRolePermissions,
    isSavingUserOverrides,
    onSelectUser: selectUser,
    onSelectGroup: selectGroup,
    onSelectRole: handleSelectRole,
    onToggleUserRole: toggleUserRole,
    onToggleUserGroup: toggleUserGroup,
    onToggleGroupRole: toggleGroupRole,
    onSaveUserMasterData: saveUserMasterData,
    onCreateUser: createUser,
    onRemoveUser: removeUser,
    onSaveUserRoles: saveUserRoles,
    onSaveUserGroups: saveUserGroups,
    onSaveGroupRoles: saveGroupRoles,
    onSetUserExternalKeyDraft: setUserExternalKeyDraft,
    onSetUserDisplayNameDraft: setUserDisplayNameDraft,
    onSetUserEmailDraft: setUserEmailDraft,
    onSetUserNotificationEmailDraft: setUserNotificationEmailDraft,
    onSetUserDepartmentIdDraft: setUserDepartmentIdDraft,
    onSetUserIsActiveDraft: setUserIsActiveDraft,
    onSetNewUserExternalKeyDraft: setNewUserExternalKeyDraft,
    onSetNewUserDisplayNameDraft: setNewUserDisplayNameDraft,
    onSetNewUserEmailDraft: setNewUserEmailDraft,
    onSetNewUserNotificationEmailDraft: setNewUserNotificationEmailDraft,
    onSetNewUserDepartmentIdDraft: setNewUserDepartmentIdDraft,
    onSetNewUserIsActiveDraft: setNewUserIsActiveDraft,
    onSetNewDepartmentNameDraft: setNewDepartmentNameDraft,
    onSetNewPositionNameDraft: setNewPositionNameDraft,
    onSetNewResponsibilityDraft: setNewResponsibilityDraft,
    setDepartmentDrafts,
    setPositionDrafts,
    setResponsibilityDrafts,
    onCreateDepartment: createDepartment,
    onCreateDepartmentPosition: createDepartmentPosition,
    onCreateResponsibility: createResponsibility,
    onSaveDepartmentAssignment: saveDepartmentAssignment,
    onRemoveDepartment: removeDepartment,
    onSaveDepartmentPosition: saveDepartmentPosition,
    onRemoveDepartmentPosition: removeDepartmentPosition,
    onRemoveResponsibility: removeResponsibility,
    onSaveResponsibilityAssignment: saveResponsibilityAssignment,
    onToggleRolePermission: handleToggleRolePermission,
    onSaveRolePermissions: handleSaveRolePermissions,
    onUserOverrideDraftsChange: setUserOverrideDrafts,
    onSaveUserOverrides: handleSaveUserOverrides,
    onSyncDirectory: handleSyncDirectory,
    onCreateDirectoryMapping: handleCreateDirectoryMapping,
    onDeleteDirectoryMapping: handleDeleteDirectoryMapping,
    onNotice: setNotice,
    onError: setError,
  });

  return (
    <main className="app-shell">
      <div className="page-container admin-settings-page">
        <PageHeader
          variant="section"
          title="Administration"
          description={undefined}
        />

        <div className="admin-settings-shell">
          <aside className="admin-settings-sidebar">
            <AdminWorkspaceNavigation
              section={section}
              onSelectSection={handleSelectSection}
            />
          </aside>

          <section className="admin-settings-main" aria-label="Admin-Arbeitsbereich">
            {!isLoading && notice ? (
              <section className="panel panel-success">
                <p className="panel-text">{notice}</p>
              </section>
            ) : null}

            {!isLoading && error && hasAnyData ? (
              <section className="panel panel-error" role="alert">
                <p className="panel-text text-error">{error}</p>
              </section>
            ) : null}

            {isLoading ? <LoadingState title="Stammdaten werden geladen..." /> : null}
            {!isLoading && error && !hasAnyData ? (
              <EmptyState title="Stammdaten konnten nicht geladen werden." description={error} />
            ) : null}

            {!isLoading && (!error || hasAnyData) ? (
              <section className="content-stack">
                <AdminConfigWorkspaceContent {...workspaceContentProps} />
              </section>
            ) : null}
          </section>
        </div>
      </div>
    </main>
  );
}
