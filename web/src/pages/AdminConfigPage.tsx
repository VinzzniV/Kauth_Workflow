import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { useCurrentUser } from "../auth/useCurrentUser";
import { AdminOrganizationWorkspaceSection } from "../components/admin-config/AdminOrganizationWorkspaceSection";
import { AdminOverviewWorkspaceSection } from "../components/admin-config/AdminOverviewWorkspaceSection";
import { AdminSystemWorkspaceSection } from "../components/admin-config/AdminSystemWorkspaceSection";
import { AdminTechnicalAccessSection } from "../components/admin-config/AdminTechnicalAccessSection";
import { AdminWorkspaceNavigation } from "../components/admin-config/AdminWorkspaceNavigation";
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
  getAdminDepartmentAssignments,
  getAdminGroups,
  getAdminNotificationEmailConfiguration,
  getAdminResponsibilityOwners,
  getAdminRoles,
  getAdminUsers,
  getAdminWorkflowConfig,
} from "../services/onboardingApi";
import type {
  AdminDepartmentAssignment,
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
  const [departmentAssignments, setDepartmentAssignments] = useState<AdminDepartmentAssignment[]>([]);
  const [responsibilityOwners, setResponsibilityOwners] = useState<AdminResponsibilityOwner[]>([]);
  const [workflowConfig, setWorkflowConfig] = useState<WorkflowConfig | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [isLoadingTechnicalAccess, setIsLoadingTechnicalAccess] = useState<boolean>(false);
  const [hasLoadedTechnicalAccess, setHasLoadedTechnicalAccess] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const hasLoadedTechnicalAccessRef = useRef<boolean>(false);

  useEffect(() => {
    hasLoadedTechnicalAccessRef.current = hasLoadedTechnicalAccess;
  }, [hasLoadedTechnicalAccess]);

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
    } catch (err) {
      const message = err instanceof Error ? err.message : "Stammdaten konnten nicht geladen werden.";
      setError(message);
      setUsers([]);
      setDepartmentAssignments([]);
      setResponsibilityOwners([]);
      setWorkflowConfig(null);
      setNotificationEmailConfiguration(null);
    } finally {
      setIsLoading(false);
    }
  }, [loadTechnicalAccess, setNotificationEmailConfiguration]);

  useEffect(() => {
    void reload();
  }, [reload]);

  useEffect(() => {
    if (section !== "access" || hasLoadedTechnicalAccess) {
      return;
    }

    void loadTechnicalAccess();
  }, [hasLoadedTechnicalAccess, loadTechnicalAccess, section]);

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
  const workspaceSelectedUserId =
    section === "organization" && organizationEntity === "user" ? selectedEntityId : null;

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

  const hasAnyData =
    users.length > 0 || departmentAssignments.length > 0 || responsibilityOwners.length > 0;

  return (
    <main className="onboarding-shell">
      <div className="page-container">
        <PageHeader
          title="Admin-Workspace"
          description="Verwalten Sie Organisation, Rechte und Systemkonfiguration in klar getrennten Arbeitsbereichen. Personen, Abteilungen und fachliche Zuständigkeiten werden gemeinsam modelliert, Rollen und Gruppen bleiben bewusst separat."
        />

        {!isLoading && notice ? (
          <section className="panel panel-success">
            <p className="panel-text">{notice}</p>
          </section>
        ) : null}

        {!isLoading && error && hasAnyData ? (
          <section className="panel">
            <p className="panel-text">{error}</p>
          </section>
        ) : null}

        {isLoading ? <LoadingState title="Stammdaten werden geladen..." /> : null}
        {!isLoading && error && !hasAnyData ? (
          <EmptyState title="Stammdaten konnten nicht geladen werden." description={error} />
        ) : null}

        {!isLoading && (!error || hasAnyData) ? (
          <>
            <AdminWorkspaceNavigation section={section} onSelectSection={handleSelectSection} />

            {section === "overview" ? (
              <AdminOverviewWorkspaceSection
                departmentCount={departmentAssignments.length}
                userCount={users.length}
                responsibilityCount={responsibilityOwners.length}
                warningCount={warnings.length}
                hasLoadedTechnicalAccess={hasLoadedTechnicalAccess}
                roleCount={roles.length}
                groupCount={groups.length}
                notificationEmailConfiguration={notificationEmailConfiguration}
                warnings={warnings}
                onOpenOrganization={handleOpenOrganization}
                onOpenSection={handleSelectSection}
              />
            ) : null}

            {section === "organization" ? (
              <AdminOrganizationWorkspaceSection
                organizationEntity={organizationEntity}
                selectedEntityId={selectedEntityId}
                sortedUsers={sortedUsers}
                sortedDepartments={sortedDepartments}
                sortedResponsibilities={sortedResponsibilities}
                eligibleSupervisorUsers={eligibleSupervisorUsers}
                selectedUser={workspaceSelectedUser}
                selectedUserId={workspaceSelectedUserId}
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
                onSelectOrganizationEntity={handleOpenOrganization}
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
              />
            ) : null}

            {section === "access" ? (
              <AdminTechnicalAccessSection
                isTechnicalAccessOpen={true}
                isLoadingTechnicalAccess={isLoadingTechnicalAccess}
                sortedUsers={sortedUsers}
                selectedUser={selectedUser}
                selectedUserRoleIds={selectedUserRoleIds}
                selectedUserGroupIds={selectedUserGroupIds}
                selectedGroupId={selectedGroupId}
                selectedGroup={selectedGroup}
                selectedGroupRoleIds={selectedGroupRoleIds}
                sortedRoles={sortedRoles}
                groups={groups}
                isSavingUserRoles={isSavingUserRoles}
                isSavingUserGroups={isSavingUserGroups}
                isSavingGroupRoles={isSavingGroupRoles}
                onSelectUser={selectUser}
                onToggleUserRole={toggleUserRole}
                onToggleUserGroup={toggleUserGroup}
                onSelectGroup={selectGroup}
                onToggleGroupRole={toggleGroupRole}
                onSaveUserRoles={saveUserRoles}
                onSaveUserGroups={saveUserGroups}
                onSaveGroupRoles={saveGroupRoles}
              />
            ) : null}

            {section === "system" ? (
              <AdminSystemWorkspaceSection
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
                isLoading={isLoading}
                hasNotificationEmailDraftChanges={hasNotificationEmailDraftChanges}
                workflowConfig={workflowConfig}
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
            ) : null}
          </>
        ) : null}
      </div>
    </main>
  );
}
