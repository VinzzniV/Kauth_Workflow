import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { useCurrentUser } from "../auth/useCurrentUser";
import type { AdminPermissionOverrideDraft } from "../components/admin-config/AdminPermissionsSection";
import { AdminConfigWorkspaceContent } from "../components/admin-config/AdminConfigWorkspaceContent";
import { AdminWorkspaceNavigation, AdminWorkspaceSubNavigation } from "../components/admin-config/AdminWorkspaceNavigation";
import {
  buildAdminOverviewWarnings,
  normalizeAdminOrganizationEntity,
  normalizeAdminWorkspaceSection,
  parseAdminWorkspaceId,
  type AdminOrganizationEntity,
  type AdminWorkspaceSection,
} from "../components/admin-config/adminWorkspaceModel";
import EmptyState from "../components/feedback/EmptyState";
import LoadingState from "../components/feedback/LoadingState";
import PageHeader from "../components/layout/PageHeader";
import { useAdminGraphApplicationConfiguration } from "../hooks/useAdminGraphApplicationConfiguration";
import { useAdminNotificationEmailConfiguration } from "../hooks/useAdminNotificationEmailConfiguration";
import { useAdminOrganizationManagement } from "../hooks/useAdminOrganizationManagement";
import { useAdminUserManagement } from "../hooks/useAdminUserManagement";
import {
  createAdminDirectoryGroupRoleMapping,
  deleteAdminDirectoryGroupRoleMapping,
  getAdminDirectoryAudit,
  getAdminDirectoryGroups,
  getAdminDirectoryIdentities,
  getAdminDirectoryStatus,
  syncAdminDirectory,
} from "../services/adminConfigApi";
import {
  getAdminDepartmentAssignments,
  getAdminGraphApplicationConfiguration,
  getAdminGroups,
  getAdminPermissionAudit,
  getAdminPermissions,
  getAdminNotificationEmailConfiguration,
  getAdminResponsibilityOwners,
  getAdminRoles,
  getAdminUsers,
  getAdminWorkflowConfig,
  updateAdminRolePermissions,
  updateAdminUserPermissionOverrides,
} from "../services/adminApi";
import type {
  AdminDepartmentAssignment,
  AdminDirectoryGroup,
  AdminDirectoryIdentity,
  AdminDirectoryMappingAuditEntry,
  AdminDirectorySyncStatus,
  AdminGroup,
  AdminPermission,
  AdminPermissionAuditEntry,
  AdminResponsibilityOwner,
  AdminRole,
  AdminUser,
} from "../types/auth";
import type { WorkflowConfig } from "../types/workflow";

export default function AdminConfigPage() {
  const { refreshCurrentUser } = useCurrentUser();
  const [searchParams, setSearchParams] = useSearchParams();
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [roles, setRoles] = useState<AdminRole[]>([]);
  const [groups, setGroups] = useState<AdminGroup[]>([]);
  const [permissions, setPermissions] = useState<AdminPermission[]>([]);
  const [permissionAuditEntries, setPermissionAuditEntries] = useState<AdminPermissionAuditEntry[]>([]);
  const [directoryGroups, setDirectoryGroups] = useState<AdminDirectoryGroup[]>([]);
  const [directoryIdentities, setDirectoryIdentities] = useState<AdminDirectoryIdentity[]>([]);
  const [directoryAuditEntries, setDirectoryAuditEntries] = useState<AdminDirectoryMappingAuditEntry[]>([]);
  const [directoryStatus, setDirectoryStatus] = useState<AdminDirectorySyncStatus | null>(null);
  const [departmentAssignments, setDepartmentAssignments] = useState<AdminDepartmentAssignment[]>([]);
  const [responsibilityOwners, setResponsibilityOwners] = useState<AdminResponsibilityOwner[]>([]);
  const [workflowConfig, setWorkflowConfig] = useState<WorkflowConfig | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [isLoadingTechnicalAccess, setIsLoadingTechnicalAccess] = useState<boolean>(false);
  const [isLoadingDirectory, setIsLoadingDirectory] = useState<boolean>(false);
  const [isSyncingDirectory, setIsSyncingDirectory] = useState<boolean>(false);
  const [savingDirectoryGroupId, setSavingDirectoryGroupId] = useState<number | null>(null);
  const [deletingDirectoryMappingId, setDeletingDirectoryMappingId] = useState<number | null>(null);
  const [selectedRoleId, setSelectedRoleId] = useState<number | null>(null);
  const [selectedRolePermissionIds, setSelectedRolePermissionIds] = useState<number[]>([]);
  const [userOverrideDrafts, setUserOverrideDrafts] = useState<AdminPermissionOverrideDraft[]>([]);
  const [isSavingRolePermissions, setIsSavingRolePermissions] = useState<boolean>(false);
  const [isSavingUserOverrides, setIsSavingUserOverrides] = useState<boolean>(false);
  const [hasLoadedTechnicalAccess, setHasLoadedTechnicalAccess] = useState<boolean>(false);
  const [hasLoadedDirectory, setHasLoadedDirectory] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const hasLoadedTechnicalAccessRef = useRef<boolean>(false);
  const hasLoadedDirectoryRef = useRef<boolean>(false);

  useEffect(() => {
    hasLoadedTechnicalAccessRef.current = hasLoadedTechnicalAccess;
  }, [hasLoadedTechnicalAccess]);

  useEffect(() => {
    hasLoadedDirectoryRef.current = hasLoadedDirectory;
  }, [hasLoadedDirectory]);

  const section = normalizeAdminWorkspaceSection(searchParams.get("section"));
  const organizationEntity = normalizeAdminOrganizationEntity(searchParams.get("entity"));
  const selectedEntityId = parseAdminWorkspaceId(searchParams.get("id"));

  const {
    notificationEmailConfiguration,
    setNotificationEmailConfiguration,
    notificationEnabledDraft,
    notificationSenderEmailDraft,
    notificationFrontendBaseUrlDraft,
    notificationTestRecipientDraft,
    notificationSandboxRedirectDraft,
    notificationNotifyOnWorkflowCreatedDraft,
    notificationNotifyOnTaskReadyDraft,
    notificationNotifyOnWorkflowCompletedDraft,
    isSavingNotificationEmailConfiguration,
    isSendingNotificationEmailTest,
    hasNotificationEmailDraftChanges,
    setNotificationEnabledDraft,
    setNotificationSenderEmailDraft,
    setNotificationFrontendBaseUrlDraft,
    setNotificationTestRecipientDraft,
    setNotificationSandboxRedirectDraft,
    setNotificationNotifyOnWorkflowCreatedDraft,
    setNotificationNotifyOnTaskReadyDraft,
    setNotificationNotifyOnWorkflowCompletedDraft,
    saveNotificationEmailConfiguration,
    sendNotificationEmailTest,
  } = useAdminNotificationEmailConfiguration({
    onNotice: setNotice,
    onError: setError,
  });

  const {
    graphApplicationConfiguration,
    setGraphApplicationConfiguration,
  } = useAdminGraphApplicationConfiguration();

  const loadTechnicalAccess = useCallback(async () => {
    setIsLoadingTechnicalAccess(true);
    setError(null);

    try {
      const [rolesData, groupsData, permissionsData, permissionAuditData] = await Promise.all([
        getAdminRoles(),
        getAdminGroups(),
        getAdminPermissions(),
        getAdminPermissionAudit(50),
      ]);
      setRoles(rolesData);
      setGroups(groupsData);
      setPermissions(permissionsData);
      setPermissionAuditEntries(permissionAuditData);
      setHasLoadedTechnicalAccess(true);
    } catch (err) {
      const message =
        err instanceof Error ? err.message : "Benutzerrechte und Gruppen konnten nicht geladen werden.";
      setError(message);
      setRoles([]);
      setGroups([]);
      setPermissions([]);
      setPermissionAuditEntries([]);
    } finally {
      setIsLoadingTechnicalAccess(false);
    }
  }, []);

  const loadDirectoryData = useCallback(async () => {
    setIsLoadingDirectory(true);
    setError(null);

    try {
      const [statusData, groupsData, identitiesData, auditData, rolesData] = await Promise.all([
        getAdminDirectoryStatus(),
        getAdminDirectoryGroups(),
        getAdminDirectoryIdentities(25, 0),
        getAdminDirectoryAudit(20),
        getAdminRoles(),
      ]);

      setDirectoryStatus(statusData);
      setDirectoryGroups(groupsData);
      setDirectoryIdentities(identitiesData);
      setDirectoryAuditEntries(auditData);
      setRoles(rolesData);
      setHasLoadedDirectory(true);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Verzeichnisdaten konnten nicht geladen werden.";
      setError(message);
      setDirectoryStatus(null);
      setDirectoryGroups([]);
      setDirectoryIdentities([]);
      setDirectoryAuditEntries([]);
    } finally {
      setIsLoadingDirectory(false);
    }
  }, []);

  const reload = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const workflowConfigPromise = getAdminWorkflowConfig().catch(() => null);
      const [usersData, departmentsData, responsibilitiesData, graphApplicationConfigurationData, notificationEmailConfigurationData] = await Promise.all([
        getAdminUsers(),
        getAdminDepartmentAssignments(),
        getAdminResponsibilityOwners(),
        getAdminGraphApplicationConfiguration(),
        getAdminNotificationEmailConfiguration(),
      ]);

      setUsers(usersData);
      setDepartmentAssignments(departmentsData);
      setResponsibilityOwners(responsibilitiesData);
      setGraphApplicationConfiguration(graphApplicationConfigurationData);
      setNotificationEmailConfiguration(notificationEmailConfigurationData);
      setWorkflowConfig(await workflowConfigPromise);

      if (hasLoadedTechnicalAccessRef.current) {
        await loadTechnicalAccess();
      }

      if (hasLoadedDirectoryRef.current) {
        await loadDirectoryData();
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : "Stammdaten konnten nicht geladen werden.";
      setError(message);
      setUsers([]);
      setDepartmentAssignments([]);
      setResponsibilityOwners([]);
      setWorkflowConfig(null);
      setGraphApplicationConfiguration(null);
      setNotificationEmailConfiguration(null);
      setDirectoryStatus(null);
      setDirectoryGroups([]);
      setDirectoryIdentities([]);
      setDirectoryAuditEntries([]);
    } finally {
      setIsLoading(false);
    }
  }, [loadDirectoryData, loadTechnicalAccess, setGraphApplicationConfiguration, setNotificationEmailConfiguration]);

  useEffect(() => {
    void reload();
  }, [reload]);

  useEffect(() => {
    if (section !== "access" || hasLoadedTechnicalAccess) {
      return;
    }

    void loadTechnicalAccess();
  }, [hasLoadedTechnicalAccess, loadTechnicalAccess, section]);

  useEffect(() => {
    if (section !== "directory" || hasLoadedDirectory) {
      return;
    }

    void loadDirectoryData();
  }, [hasLoadedDirectory, loadDirectoryData, section]);

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
    departmentDrafts,
    responsibilityDrafts,
    isCreatingDepartment,
    deletingDepartmentId,
    savingDepartmentId,
    savingResponsibilityId,
    setNewDepartmentNameDraft,
    setDepartmentDrafts,
    setResponsibilityDrafts,
    createDepartment,
    removeDepartment,
    saveDepartmentAssignment,
    saveResponsibilityAssignment,
  } = useAdminOrganizationManagement({
    departmentAssignments,
    responsibilityOwners,
    setDepartmentAssignments,
    setResponsibilityOwners,
    reload,
    onNotice: setNotice,
    onError: setError,
  });

  const sortedUsers = useMemo(
    () => users.slice().sort((left, right) => left.displayName.localeCompare(right.displayName, "de")),
    [users]
  );
  const eligibleSupervisorUsers = useMemo(
    () => sortedUsers.filter((user) => user.isActive && user.hasManagerAccess),
    [sortedUsers]
  );
  const sortedRoles = useMemo(
    () =>
      roles.slice().sort((left, right) => {
        const leftKey = `${left.roleKind}|${left.departmentName ?? ""}|${left.roleName}`;
        const rightKey = `${right.roleKind}|${right.departmentName ?? ""}|${right.roleName}`;
        return leftKey.localeCompare(rightKey, "de");
      }),
    [roles]
  );
  const sortedDepartments = useMemo(
    () =>
      departmentAssignments
        .slice()
        .sort((left, right) => left.departmentName.localeCompare(right.departmentName, "de")),
    [departmentAssignments]
  );
  const sortedResponsibilities = useMemo(
    () =>
      responsibilityOwners.slice().sort((left, right) => {
        const leftKey = `${left.responsibilityType}|${left.departmentName ?? ""}|${left.responsibilityName}`;
        const rightKey = `${right.responsibilityType}|${right.departmentName ?? ""}|${right.responsibilityName}`;
        return leftKey.localeCompare(rightKey, "de");
      }),
    [responsibilityOwners]
  );

  const workspaceSelectedUser =
    section === "organization" && organizationEntity === "user" && selectedEntityId ? selectedUser : null;

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

  const warnings = useMemo(
    () =>
      buildAdminOverviewWarnings({
        departments: sortedDepartments,
        responsibilities: sortedResponsibilities,
        eligibleSupervisorUsers,
        notificationEmailConfiguration,
      }),
    [eligibleSupervisorUsers, notificationEmailConfiguration, sortedDepartments, sortedResponsibilities]
  );

  useEffect(() => {
    if (organizationEntity !== "user" || !selectedEntityId) {
      return;
    }

    const matchingUser = users.find((user) => user.userId === selectedEntityId);
    if (!matchingUser || selectedUserId === matchingUser.userId) {
      return;
    }

    selectUser(matchingUser);
  }, [organizationEntity, selectedEntityId, selectUser, selectedUserId, users]);

  const updateWorkspace = useCallback(
    (nextSection: AdminWorkspaceSection, nextEntity?: AdminOrganizationEntity, nextId?: number | null) => {
      const nextParams = new URLSearchParams(searchParams);
      nextParams.set("section", nextSection);

      if (nextSection === "organization") {
        nextParams.set("entity", nextEntity ?? organizationEntity);
        if (nextId) {
          nextParams.set("id", String(nextId));
        } else {
          nextParams.delete("id");
        }
      } else {
        nextParams.delete("entity");
        nextParams.delete("id");
      }

      setSearchParams(nextParams);
    },
    [organizationEntity, searchParams, setSearchParams]
  );

  const handleSelectSection = useCallback(
    (nextSection: AdminWorkspaceSection) => {
      updateWorkspace(nextSection);
    },
    [updateWorkspace]
  );

  const handleOpenOrganization = useCallback(
    (entity: AdminOrganizationEntity, id?: number | null) => {
      updateWorkspace("organization", entity, id ?? null);
    },
    [updateWorkspace]
  );

  const handleSyncDirectory = useCallback(async (groupPrefix: string | null) => {
    setIsSyncingDirectory(true);
    setNotice(null);
    setError(null);

    try {
      const result = await syncAdminDirectory(groupPrefix);
      await loadDirectoryData();
      if (result.status === "failed") {
        setError(result.errorMessage ?? "Verzeichnis-Sync fehlgeschlagen.");
      } else {
        setNotice(
          `Verzeichnis-Sync ${result.status === "partial" ? "teilweise" : "erfolgreich"}: ${result.groupsSynced} Gruppen, ${result.identitiesSynced} Identitäten, ${result.membershipsSynced} Mitgliedschaften.${result.appliedGroupPrefix ? ` Filter: ${result.appliedGroupPrefix}.` : " Ohne Filter."}`
        );
        if (result.errorMessage) {
          setError(result.errorMessage);
        }
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : "Verzeichnis-Sync konnte nicht gestartet werden.";
      setError(message);
    } finally {
      setIsSyncingDirectory(false);
    }
  }, [loadDirectoryData]);

  const handleCreateDirectoryMapping = useCallback(
    async (directoryGroupId: number, appRoleId: number, scope: string, scopeDepartmentId: number | null) => {
      setSavingDirectoryGroupId(directoryGroupId);
      setNotice(null);
      setError(null);

      try {
        const mapping = await createAdminDirectoryGroupRoleMapping({
          directoryGroupId,
          appRoleId,
          scope,
          scopeDepartmentId,
          isActive: true,
        });
        await loadDirectoryData();
        const scopeDepartmentName =
          sortedDepartments.find((department) => department.departmentId === scopeDepartmentId)?.departmentName ?? null;
        const scopeLabel = scope === "department" ? `Abteilung ${scopeDepartmentName ?? "unbekannt"}` : "global";
        setNotice(`Mapping gespeichert: ${mapping.appRoleName} wurde der Verzeichnisgruppe mit Scope ${scopeLabel} zugeordnet.`);
      } catch (err) {
        const message = err instanceof Error ? err.message : "Mapping konnte nicht gespeichert werden.";
        setError(message);
      } finally {
        setSavingDirectoryGroupId(null);
      }
    },
    [loadDirectoryData, sortedDepartments]
  );

  const handleDeleteDirectoryMapping = useCallback(
    async (mappingId: number) => {
      setDeletingDirectoryMappingId(mappingId);
      setNotice(null);
      setError(null);

      try {
        await deleteAdminDirectoryGroupRoleMapping(mappingId);
        await loadDirectoryData();
        setNotice("Mapping wurde entfernt.");
      } catch (err) {
        const message = err instanceof Error ? err.message : "Mapping konnte nicht entfernt werden.";
        setError(message);
      } finally {
        setDeletingDirectoryMappingId(null);
      }
    },
    [loadDirectoryData]
  );

  const hasAnyData =
    users.length > 0
    || departmentAssignments.length > 0
    || responsibilityOwners.length > 0
    || permissions.length > 0
    || directoryGroups.length > 0
    || directoryIdentities.length > 0;

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
    setNotice(null);
    setError(null);

    try {
      const updatedRole = await updateAdminRolePermissions(selectedRoleId, selectedRolePermissionIds);
      const [freshUsers, auditData] = await Promise.all([getAdminUsers(), getAdminPermissionAudit(50)]);
      setRoles((current) => current.map((role) => (role.roleId === updatedRole.roleId ? updatedRole : role)));
      setUsers(freshUsers);
      setPermissionAuditEntries(auditData);
      await refreshCurrentUser();
      setNotice(`Permission-Bundle für ${updatedRole.roleName} wurde aktualisiert.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Rollen-Permissions konnten nicht gespeichert werden.";
      setError(message);
    } finally {
      setIsSavingRolePermissions(false);
    }
  }, [refreshCurrentUser, selectedRoleId, selectedRolePermissionIds]);

  const handleSaveUserOverrides = useCallback(async () => {
    if (!selectedUser) {
      return;
    }

    setIsSavingUserOverrides(true);
    setNotice(null);
    setError(null);

    try {
      const updatedUser = await updateAdminUserPermissionOverrides(selectedUser.userId, userOverrideDrafts);
      const auditData = await getAdminPermissionAudit(50);
      setUsers((current) => current.map((user) => (user.userId === updatedUser.userId ? updatedUser : user)));
      setPermissionAuditEntries(auditData);
      await refreshCurrentUser();
      setNotice(`Lokale Permission-Overrides für ${updatedUser.displayName} wurden gespeichert.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Permission-Overrides konnten nicht gespeichert werden.";
      setError(message);
    } finally {
      setIsSavingUserOverrides(false);
    }
  }, [refreshCurrentUser, selectedUser, userOverrideDrafts]);

  return (
    <main className="app-shell">
      <div className="page-container">
        <PageHeader title="Administration" />

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
          <>
            {section !== "overview" ? (
              <>
                <AdminWorkspaceNavigation
                  section={section}
                  onSelectSection={handleSelectSection}
                />

                <AdminWorkspaceSubNavigation
                  section={section}
                  onSelectSection={handleSelectSection}
                />
              </>
            ) : null}

            <section className="content-stack">
              <AdminConfigWorkspaceContent
                section={section}
                organizationEntity={organizationEntity}
                selectedEntityId={selectedEntityId}
                users={users}
                departmentAssignments={departmentAssignments}
                responsibilityOwners={responsibilityOwners}
                sortedUsers={sortedUsers}
                sortedDepartments={sortedDepartments}
                sortedResponsibilities={sortedResponsibilities}
                eligibleSupervisorUsers={eligibleSupervisorUsers}
                selectedUser={selectedUser}
                workspaceSelectedUser={workspaceSelectedUser}
                userDisplayNameDraft={userDisplayNameDraft}
                userEmailDraft={userEmailDraft}
                userNotificationEmailDraft={userNotificationEmailDraft}
                userExternalKeyDraft={userExternalKeyDraft}
                userDepartmentIdDraft={userDepartmentIdDraft}
                userIsActiveDraft={userIsActiveDraft}
                userFormError={userFormError}
                userFormNotice={userFormNotice}
                newUserDisplayNameDraft={newUserDisplayNameDraft}
                newUserEmailDraft={newUserEmailDraft}
                newUserNotificationEmailDraft={newUserNotificationEmailDraft}
                newUserExternalKeyDraft={newUserExternalKeyDraft}
                newUserDepartmentIdDraft={newUserDepartmentIdDraft}
                newUserIsActiveDraft={newUserIsActiveDraft}
                isCreatingUser={isCreatingUser}
                isSavingUserMasterData={isSavingUserMasterData}
                deletingUserId={deletingUserId}
                newDepartmentNameDraft={newDepartmentNameDraft}
                departmentDrafts={departmentDrafts}
                responsibilityDrafts={responsibilityDrafts}
                isCreatingDepartment={isCreatingDepartment}
                deletingDepartmentId={deletingDepartmentId}
                savingDepartmentId={savingDepartmentId}
                savingResponsibilityId={savingResponsibilityId}
                hasLoadedTechnicalAccess={hasLoadedTechnicalAccess}
                isLoadingTechnicalAccess={isLoadingTechnicalAccess}
                isLoadingDirectory={isLoadingDirectory}
                isSyncingDirectory={isSyncingDirectory}
                savingDirectoryGroupId={savingDirectoryGroupId}
                deletingDirectoryMappingId={deletingDirectoryMappingId}
                selectedUserRoleIds={selectedUserRoleIds}
                selectedUserGroupIds={selectedUserGroupIds}
                selectedGroupId={selectedGroupId}
                selectedGroup={selectedGroup}
                selectedGroupRoleIds={selectedGroupRoleIds}
                sortedRoles={sortedRoles}
                permissions={permissions}
                permissionAuditEntries={permissionAuditEntries}
                selectedRoleId={selectedRoleId}
                selectedRolePermissionIds={selectedRolePermissionIds}
                userOverrideDrafts={userOverrideDrafts}
                groups={groups}
                directoryGroups={directoryGroups}
                directoryIdentities={directoryIdentities}
                directoryAuditEntries={directoryAuditEntries}
                directoryStatus={directoryStatus}
                graphApplicationConfiguration={graphApplicationConfiguration}
                notificationEmailConfiguration={notificationEmailConfiguration}
                notificationEnabledDraft={notificationEnabledDraft}
                notificationSenderEmailDraft={notificationSenderEmailDraft}
                notificationFrontendBaseUrlDraft={notificationFrontendBaseUrlDraft}
                notificationTestRecipientDraft={notificationTestRecipientDraft}
                notificationSandboxRedirectDraft={notificationSandboxRedirectDraft}
                notificationNotifyOnWorkflowCreatedDraft={notificationNotifyOnWorkflowCreatedDraft}
                notificationNotifyOnTaskReadyDraft={notificationNotifyOnTaskReadyDraft}
                notificationNotifyOnWorkflowCompletedDraft={notificationNotifyOnWorkflowCompletedDraft}
                isSavingNotificationEmailConfiguration={isSavingNotificationEmailConfiguration}
                isSendingNotificationEmailTest={isSendingNotificationEmailTest}
                hasNotificationEmailDraftChanges={hasNotificationEmailDraftChanges}
                workflowConfig={workflowConfig}
                warnings={warnings}
                isSavingUserRoles={isSavingUserRoles}
                isSavingUserGroups={isSavingUserGroups}
                isSavingGroupRoles={isSavingGroupRoles}
                isSavingRolePermissions={isSavingRolePermissions}
                isSavingUserOverrides={isSavingUserOverrides}
                onOpenOrganization={handleOpenOrganization}
                onSelectSection={handleSelectSection}
                onSelectUser={selectUser}
                onSelectRole={handleSelectRole}
                onNewUserDisplayNameChange={setNewUserDisplayNameDraft}
                onNewUserEmailChange={setNewUserEmailDraft}
                onNewUserNotificationEmailChange={setNewUserNotificationEmailDraft}
                onNewUserExternalKeyChange={setNewUserExternalKeyDraft}
                onNewUserDepartmentIdChange={setNewUserDepartmentIdDraft}
                onNewUserIsActiveChange={setNewUserIsActiveDraft}
                onUserDisplayNameChange={setUserDisplayNameDraft}
                onUserEmailChange={setUserEmailDraft}
                onUserNotificationEmailChange={setUserNotificationEmailDraft}
                onUserExternalKeyChange={setUserExternalKeyDraft}
                onUserDepartmentIdChange={setUserDepartmentIdDraft}
                onUserIsActiveChange={setUserIsActiveDraft}
                onCreateUser={createUser}
                onSaveUserMasterData={saveUserMasterData}
                onRemoveUser={removeUser}
                onNewDepartmentNameChange={setNewDepartmentNameDraft}
                onDepartmentDraftChange={(departmentId, draft) =>
                  setDepartmentDrafts((current) => ({ ...current, [departmentId]: draft }))
                }
                onCreateDepartment={createDepartment}
                onSaveDepartmentAssignment={saveDepartmentAssignment}
                onRemoveDepartment={removeDepartment}
                onResponsibilityDraftChange={(responsibilityId, draft) =>
                  setResponsibilityDrafts((current) => ({ ...current, [responsibilityId]: draft }))
                }
                onSaveResponsibilityAssignment={saveResponsibilityAssignment}
                onToggleUserRole={toggleUserRole}
                onToggleUserGroup={toggleUserGroup}
                onSelectGroup={selectGroup}
                onToggleGroupRole={toggleGroupRole}
                onSaveUserRoles={saveUserRoles}
                onSaveUserGroups={saveUserGroups}
                onSaveGroupRoles={saveGroupRoles}
                onToggleRolePermission={handleToggleRolePermission}
                onSaveRolePermissions={handleSaveRolePermissions}
                onUserOverrideDraftsChange={setUserOverrideDrafts}
                onSaveUserOverrides={handleSaveUserOverrides}
                onSyncDirectory={handleSyncDirectory}
                onCreateDirectoryMapping={handleCreateDirectoryMapping}
                onDeleteDirectoryMapping={handleDeleteDirectoryMapping}
                onNotice={setNotice}
                onError={setError}
                onNotificationEnabledChange={setNotificationEnabledDraft}
                onNotificationSenderEmailChange={setNotificationSenderEmailDraft}
                onNotificationFrontendBaseUrlChange={setNotificationFrontendBaseUrlDraft}
                onNotificationTestRecipientChange={setNotificationTestRecipientDraft}
                onNotificationSandboxRedirectChange={setNotificationSandboxRedirectDraft}
                onNotificationNotifyOnWorkflowCreatedChange={setNotificationNotifyOnWorkflowCreatedDraft}
                onNotificationNotifyOnTaskReadyChange={setNotificationNotifyOnTaskReadyDraft}
                onNotificationNotifyOnWorkflowCompletedChange={setNotificationNotifyOnWorkflowCompletedDraft}
                onSaveNotificationEmailConfiguration={saveNotificationEmailConfiguration}
                onSendNotificationEmailTest={sendNotificationEmailTest}
              />
            </section>
          </>
        ) : null}
      </div>
    </main>
  );
}
