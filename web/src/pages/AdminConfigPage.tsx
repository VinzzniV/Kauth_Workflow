import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { useCurrentUser } from "../auth/useCurrentUser";
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
  getAdminGroups,
  getAdminNotificationEmailConfiguration,
  getAdminResponsibilityOwners,
  getAdminRoles,
  getAdminUsers,
  getAdminWorkflowConfig,
} from "../services/adminApi";
import type {
  AdminDepartmentAssignment,
  AdminDirectoryGroup,
  AdminDirectoryIdentity,
  AdminDirectoryMappingAuditEntry,
  AdminDirectorySyncStatus,
  AdminGroup,
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
    notificationTenantIdDraft,
    notificationClientIdDraft,
    notificationClientSecretDraft,
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
    setNotificationTenantIdDraft,
    setNotificationClientIdDraft,
    setNotificationClientSecretDraft,
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

  const loadTechnicalAccess = useCallback(async () => {
    setIsLoadingTechnicalAccess(true);
    setError(null);

    try {
      const [rolesData, groupsData] = await Promise.all([getAdminRoles(), getAdminGroups()]);
      setRoles(rolesData);
      setGroups(groupsData);
      setHasLoadedTechnicalAccess(true);
    } catch (err) {
      const message =
        err instanceof Error ? err.message : "Benutzerrechte und Gruppen konnten nicht geladen werden.";
      setError(message);
      setRoles([]);
      setGroups([]);
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
      const [usersData, departmentsData, responsibilitiesData, notificationEmailConfigurationData] = await Promise.all([
        getAdminUsers(),
        getAdminDepartmentAssignments(),
        getAdminResponsibilityOwners(),
        getAdminNotificationEmailConfiguration(),
      ]);

      setUsers(usersData);
      setDepartmentAssignments(departmentsData);
      setResponsibilityOwners(responsibilitiesData);
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
      setNotificationEmailConfiguration(null);
      setDirectoryStatus(null);
      setDirectoryGroups([]);
      setDirectoryIdentities([]);
      setDirectoryAuditEntries([]);
    } finally {
      setIsLoading(false);
    }
  }, [loadDirectoryData, loadTechnicalAccess, setNotificationEmailConfiguration]);

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
    async (directoryGroupId: number, appRoleId: number) => {
      setSavingDirectoryGroupId(directoryGroupId);
      setNotice(null);
      setError(null);

      try {
        const mapping = await createAdminDirectoryGroupRoleMapping({
          directoryGroupId,
          appRoleId,
          scope: "global",
          isActive: true,
        });
        await loadDirectoryData();
        setNotice(`Mapping gespeichert: ${mapping.appRoleName} wurde der Verzeichnisgruppe zugeordnet.`);
      } catch (err) {
        const message = err instanceof Error ? err.message : "Mapping konnte nicht gespeichert werden.";
        setError(message);
      } finally {
        setSavingDirectoryGroupId(null);
      }
    },
    [loadDirectoryData]
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
    || directoryGroups.length > 0
    || directoryIdentities.length > 0;

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
                groups={groups}
                directoryGroups={directoryGroups}
                directoryIdentities={directoryIdentities}
                directoryAuditEntries={directoryAuditEntries}
                directoryStatus={directoryStatus}
                notificationEmailConfiguration={notificationEmailConfiguration}
                notificationEnabledDraft={notificationEnabledDraft}
                notificationTenantIdDraft={notificationTenantIdDraft}
                notificationClientIdDraft={notificationClientIdDraft}
                notificationClientSecretDraft={notificationClientSecretDraft}
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
                onOpenOrganization={handleOpenOrganization}
                onSelectSection={handleSelectSection}
                onSelectUser={selectUser}
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
                onSyncDirectory={handleSyncDirectory}
                onCreateDirectoryMapping={handleCreateDirectoryMapping}
                onDeleteDirectoryMapping={handleDeleteDirectoryMapping}
                onNotice={setNotice}
                onError={setError}
                onNotificationEnabledChange={setNotificationEnabledDraft}
                onNotificationTenantIdChange={setNotificationTenantIdDraft}
                onNotificationClientIdChange={setNotificationClientIdDraft}
                onNotificationClientSecretChange={setNotificationClientSecretDraft}
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
