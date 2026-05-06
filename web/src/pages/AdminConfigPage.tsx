import { useCallback, useEffect, useMemo, useState } from "react";
import { Navigate, useSearchParams } from "react-router-dom";
import { useCurrentUser } from "../auth/useCurrentUser";
import { AdminConfigWorkspaceContent } from "../components/admin-config/AdminConfigWorkspaceContent";
import { AdminWorkspaceNavigation } from "../components/admin-config/AdminWorkspaceNavigation";
import {
  getAdminWorkspaceAreaMeta,
  getAdminWorkspaceSectionMeta,
  normalizeAdminOrganizationEntity,
  normalizeAdminWorkspaceSection,
  parseAdminWorkspaceId,
} from "../components/admin-config/adminWorkspaceModel";
import type {
  AdminConfigAccessBundle,
  AdminConfigDirectoryBundle,
  AdminConfigMetaBundle,
  AdminConfigNotificationBundle,
  AdminConfigOrganizationBundle,
  AdminConfigSystemBundle,
  AdminConfigUserBundle,
} from "../components/admin-config/adminConfigWorkspaceContentTypes";
import type {
  DepartmentDraft,
  PositionDraft,
  ResponsibilityDraft,
} from "../components/admin-config/adminOrganizationTypes";
import { AppErrorBoundary } from "../components/feedback/AppErrorBoundary";
import EmptyState from "../components/feedback/EmptyState";
import LoadingState from "../components/feedback/LoadingState";
import PageHeader from "../components/layout/PageHeader";
import { useAdminConfigData } from "../hooks/useAdminConfigData";
import { useAdminConfigPageView } from "../hooks/useAdminConfigPageView";
import { useAdminGraphApplicationConfiguration } from "../hooks/useAdminGraphApplicationConfiguration";
import { useAdminNotificationEmailConfiguration } from "../hooks/useAdminNotificationEmailConfiguration";
import { useAdminNotificationTemplates } from "../hooks/useAdminNotificationTemplates";
import { useAdminOrganizationManagement } from "../hooks/useAdminOrganizationManagement";
import { useAdminPermissionManagement } from "../hooks/useAdminPermissionManagement";
import { useAdminUserManagement } from "../hooks/useAdminUserManagement";
import { reportUserVisibleError } from "../services/systemLogReporter";

export default function AdminConfigPage() {
  const { refreshCurrentUser } = useCurrentUser();
  const [searchParams, setSearchParams] = useSearchParams();
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  useEffect(() => {
    if (!notice) return;
    const timer = setTimeout(() => setNotice(null), 5000);
    return () => clearTimeout(timer);
  }, [notice]);
  const rawSection = (searchParams.get("section") ?? "").trim().toLowerCase();
  const rawEntity = (searchParams.get("entity") ?? "").trim().toLowerCase();
  const redirectToBuilder =
    rawSection === "builder" || rawSection === "templates" || rawSection === "answers" || rawSection === "defaults";
  const redirectToSystemLogs = rawSection === "operations";
  const redirectToSystemConfiguration = rawSection === "system";
  const legacyOrganizationRedirect =
    rawSection === "organization"
      ? rawEntity === "department"
        ? "abteilungen"
        : "personen"
      : null;
  const legacyRotationRedirect = rawSection === "rotation_requirements" ? "zustaendigkeiten" : null;

  const effectiveSectionValue = redirectToSystemConfiguration
    ? "system_configuration"
    : searchParams.get("section");
  const section = normalizeAdminWorkspaceSection(effectiveSectionValue);
  const organizationEntity = normalizeAdminOrganizationEntity(searchParams.get("entity"));
  const selectedEntityId = parseAdminWorkspaceId(searchParams.get("id"));

  const handleError = useCallback((message: string | null) => {
    setError(message);

    if (!message) {
      return;
    }

    reportUserVisibleError({
      message,
      clientFunction: "AdminConfigPage.setError",
      category: "ui",
      eventKey: "admin_workspace_error",
    });
  }, []);

  const { graphApplicationConfiguration, setGraphApplicationConfiguration } =
    useAdminGraphApplicationConfiguration();

  const notificationConfig = useAdminNotificationEmailConfiguration({
    onNotice: setNotice,
    onError: handleError,
  });
  const notificationTemplateConfig = useAdminNotificationTemplates({
    enabled: section === "system_mail_templates",
    onNotice: setNotice,
    onError: handleError,
  });

  const data = useAdminConfigData({
    section,
    setError: handleError,
    setNotice,
    setGraphApplicationConfiguration,
    setNotificationEmailConfiguration: notificationConfig.setNotificationEmailConfiguration,
  });

  const userMgmt = useAdminUserManagement({
    users: data.users,
    groups: data.groups,
    setUsers: data.setUsers,
    setGroups: data.setGroups,
    refreshCurrentUser,
    reload: data.reload,
    onNotice: setNotice,
    onError: handleError,
  });

  const orgMgmt = useAdminOrganizationManagement({
    departmentAssignments: data.departmentAssignments,
    departmentPositions: data.departmentPositions,
    responsibilityOwners: data.responsibilityOwners,
    setDepartmentAssignments: data.setDepartmentAssignments,
    setDepartmentPositions: data.setDepartmentPositions,
    setResponsibilityOwners: data.setResponsibilityOwners,
    reload: data.reload,
    onNotice: setNotice,
    onError: handleError,
  });

  const permMgmt = useAdminPermissionManagement({
    roles: data.roles,
    selectedUser: userMgmt.selectedUser,
    refreshCurrentUser,
    setRoles: data.setRoles,
    setUsers: data.setUsers,
    setPermissionAuditEntries: data.setPermissionAuditEntries,
    onNotice: setNotice,
    onError: handleError,
  });

  const view = useAdminConfigPageView({
    searchParams,
    setSearchParams,
    section,
    organizationEntity,
    selectedEntityId,
    users: data.users,
    departmentPositions: data.departmentPositions,
    roles: data.roles,
    groups: data.groups,
    permissions: data.permissions,
    directoryGroups: data.directoryGroups,
    directoryIdentities: data.directoryIdentities,
    departmentAssignments: data.departmentAssignments,
    responsibilityOwners: data.responsibilityOwners,
    notificationEmailConfiguration: notificationConfig.notificationEmailConfiguration,
    selectedUser: userMgmt.selectedUser,
    selectedUserId: userMgmt.selectedUserId,
    onSelectUser: userMgmt.selectUser,
  });

  const meta: AdminConfigMetaBundle = useMemo(
    () => ({
      section,
      organizationEntity,
      selectedEntityId,
      warnings: view.warnings,
      onSelectSection: view.handleSelectSection,
      onOpenOrganization: view.handleOpenOrganization,
      onNotice: setNotice,
      onError: handleError,
    }),
    [
      handleError,
      organizationEntity,
      section,
      selectedEntityId,
      view.handleOpenOrganization,
      view.handleSelectSection,
      view.warnings,
    ]
  );

  const userBundle: AdminConfigUserBundle = {
    users: data.users,
    sortedUsers: view.sortedUsers,
    eligibleSupervisorUsers: view.eligibleSupervisorUsers,
    eligibleRequirementOwnerUsers: view.eligibleRequirementOwnerUsers,
    selectedUser: userMgmt.selectedUser,
    workspaceSelectedUser: view.workspaceSelectedUser,
    userDisplayNameDraft: userMgmt.userDisplayNameDraft,
    userEmailDraft: userMgmt.userEmailDraft,
    userNotificationEmailDraft: userMgmt.userNotificationEmailDraft,
    userExternalKeyDraft: userMgmt.userExternalKeyDraft,
    userDepartmentIdDraft: userMgmt.userDepartmentIdDraft,
    userIsActiveDraft: userMgmt.userIsActiveDraft,
    userFormError: userMgmt.userFormError,
    userFormNotice: userMgmt.userFormNotice,
    newUserDisplayNameDraft: userMgmt.newUserDisplayNameDraft,
    newUserEmailDraft: userMgmt.newUserEmailDraft,
    newUserNotificationEmailDraft: userMgmt.newUserNotificationEmailDraft,
    newUserExternalKeyDraft: userMgmt.newUserExternalKeyDraft,
    newUserDepartmentIdDraft: userMgmt.newUserDepartmentIdDraft,
    newUserIsActiveDraft: userMgmt.newUserIsActiveDraft,
    isCreatingUser: userMgmt.isCreatingUser,
    isSavingUserMasterData: userMgmt.isSavingUserMasterData,
    deletingUserId: userMgmt.deletingUserId,
    onSelectUser: userMgmt.selectUser,
    onCreateUser: userMgmt.createUser,
    onSaveUserMasterData: userMgmt.saveUserMasterData,
    onRemoveUser: userMgmt.removeUser,
    onUserDisplayNameChange: userMgmt.setUserDisplayNameDraft,
    onUserEmailChange: userMgmt.setUserEmailDraft,
    onUserNotificationEmailChange: userMgmt.setUserNotificationEmailDraft,
    onUserExternalKeyChange: userMgmt.setUserExternalKeyDraft,
    onUserDepartmentIdChange: userMgmt.setUserDepartmentIdDraft,
    onUserIsActiveChange: userMgmt.setUserIsActiveDraft,
    onNewUserDisplayNameChange: userMgmt.setNewUserDisplayNameDraft,
    onNewUserEmailChange: userMgmt.setNewUserEmailDraft,
    onNewUserNotificationEmailChange: userMgmt.setNewUserNotificationEmailDraft,
    onNewUserExternalKeyChange: userMgmt.setNewUserExternalKeyDraft,
    onNewUserDepartmentIdChange: userMgmt.setNewUserDepartmentIdDraft,
    onNewUserIsActiveChange: userMgmt.setNewUserIsActiveDraft,
  };

  const onDepartmentDraftChange = useCallback(
    (departmentId: number, draft: DepartmentDraft) => {
      orgMgmt.setDepartmentDrafts((current) => ({ ...current, [departmentId]: draft }));
    },
    [orgMgmt]
  );
  const onPositionDraftChange = useCallback(
    (positionId: number, draft: PositionDraft) => {
      orgMgmt.setPositionDrafts((current) => ({ ...current, [positionId]: draft }));
    },
    [orgMgmt]
  );
  const onResponsibilityDraftChange = useCallback(
    (responsibilityId: number, draft: ResponsibilityDraft) => {
      orgMgmt.setResponsibilityDrafts((current) => ({ ...current, [responsibilityId]: draft }));
    },
    [orgMgmt]
  );

  const organizationBundle: AdminConfigOrganizationBundle = {
    departmentAssignments: data.departmentAssignments,
    departmentPositions: data.departmentPositions,
    responsibilityOwners: data.responsibilityOwners,
    sortedDepartments: view.sortedDepartments,
    sortedDepartmentPositions: view.sortedDepartmentPositions,
    sortedResponsibilities: view.sortedResponsibilities,
    newDepartmentNameDraft: orgMgmt.newDepartmentNameDraft,
    newPositionNameDraft: orgMgmt.newPositionNameDraft,
    newResponsibilityDraft: orgMgmt.newResponsibilityDraft,
    departmentDrafts: orgMgmt.departmentDrafts,
    positionDrafts: orgMgmt.positionDrafts,
    responsibilityDrafts: orgMgmt.responsibilityDrafts,
    isCreatingDepartment: orgMgmt.isCreatingDepartment,
    creatingPositionDepartmentId: orgMgmt.creatingPositionDepartmentId,
    isCreatingResponsibility: orgMgmt.isCreatingResponsibility,
    deletingDepartmentId: orgMgmt.deletingDepartmentId,
    deletingPositionId: orgMgmt.deletingPositionId,
    deletingResponsibilityId: orgMgmt.deletingResponsibilityId,
    savingDepartmentId: orgMgmt.savingDepartmentId,
    savingPositionId: orgMgmt.savingPositionId,
    savingResponsibilityId: orgMgmt.savingResponsibilityId,
    onNewDepartmentNameChange: orgMgmt.setNewDepartmentNameDraft,
    onNewPositionNameChange: orgMgmt.setNewPositionNameDraft,
    onNewResponsibilityDraftChange: orgMgmt.setNewResponsibilityDraft,
    onDepartmentDraftChange,
    onPositionDraftChange,
    onResponsibilityDraftChange,
    setDepartmentDrafts: orgMgmt.setDepartmentDrafts,
    setPositionDrafts: orgMgmt.setPositionDrafts,
    setResponsibilityDrafts: orgMgmt.setResponsibilityDrafts,
    onCreateDepartment: orgMgmt.createDepartment,
    onCreateDepartmentPosition: orgMgmt.createDepartmentPosition,
    onCreateResponsibility: orgMgmt.createResponsibility,
    onSaveDepartmentAssignment: orgMgmt.saveDepartmentAssignment,
    onRemoveDepartment: orgMgmt.removeDepartment,
    onSaveDepartmentPosition: orgMgmt.saveDepartmentPosition,
    onRemoveDepartmentPosition: orgMgmt.removeDepartmentPosition,
    onRemoveResponsibility: orgMgmt.removeResponsibility,
    onSaveResponsibilityAssignment: orgMgmt.saveResponsibilityAssignment,
  };

  const accessBundle: AdminConfigAccessBundle = {
    sortedRoles: view.sortedRoles,
    groups: data.groups,
    permissions: data.permissions,
    permissionAuditEntries: data.permissionAuditEntries,
    hasMorePermissionAudit: data.hasMorePermissionAudit,
    isLoadingMorePermissionAudit: data.isLoadingMorePermissionAudit,
    onLoadMorePermissionAudit: data.handleLoadMorePermissionAudit,
    selectedUserRoleIds: userMgmt.selectedUserRoleIds,
    selectedUserGroupIds: userMgmt.selectedUserGroupIds,
    selectedGroupId: userMgmt.selectedGroupId,
    selectedGroup: userMgmt.selectedGroup,
    selectedGroupRoleIds: userMgmt.selectedGroupRoleIds,
    selectedRoleId: permMgmt.selectedRoleId,
    selectedRolePermissionIds: permMgmt.selectedRolePermissionIds,
    userOverrideDrafts: permMgmt.userOverrideDrafts,
    hasLoadedTechnicalAccess: data.hasLoadedTechnicalAccess,
    isLoadingTechnicalAccess: data.isLoadingTechnicalAccess,
    isSavingUserRoles: userMgmt.isSavingUserRoles,
    isSavingUserGroups: userMgmt.isSavingUserGroups,
    isSavingGroupRoles: userMgmt.isSavingGroupRoles,
    isSavingRolePermissions: permMgmt.isSavingRolePermissions,
    isSavingUserOverrides: permMgmt.isSavingUserOverrides,
    onSelectRole: permMgmt.handleSelectRole,
    onToggleUserRole: userMgmt.toggleUserRole,
    onToggleUserGroup: userMgmt.toggleUserGroup,
    onSelectGroup: userMgmt.selectGroup,
    onToggleGroupRole: userMgmt.toggleGroupRole,
    onSaveUserRoles: userMgmt.saveUserRoles,
    onSaveUserGroups: userMgmt.saveUserGroups,
    onSaveGroupRoles: userMgmt.saveGroupRoles,
    onToggleRolePermission: permMgmt.handleToggleRolePermission,
    onSaveRolePermissions: permMgmt.handleSaveRolePermissions,
    onUserOverrideDraftsChange: permMgmt.setUserOverrideDrafts,
    onSaveUserOverrides: permMgmt.handleSaveUserOverrides,
  };

  const directoryBundle: AdminConfigDirectoryBundle = {
    directoryGroups: data.directoryGroups,
    directoryIdentities: data.directoryIdentities,
    directoryAuditEntries: data.directoryAuditEntries,
    hasMoreDirectoryAudit: data.hasMoreDirectoryAudit,
    isLoadingMoreDirectoryAudit: data.isLoadingMoreDirectoryAudit,
    onLoadMoreDirectoryAudit: data.handleLoadMoreDirectoryAudit,
    directoryStatus: data.directoryStatus,
    directoryResponsibilityGaps: data.directoryResponsibilityGaps,
    directoryPendingImports: data.directoryPendingImports,
    isLoadingDirectory: data.isLoadingDirectory,
    isSyncingDirectory: data.isSyncingDirectory,
    isImportingDirectory: data.isImportingDirectory,
    savingDirectoryGroupId: data.savingDirectoryGroupId,
    deletingDirectoryMappingId: data.deletingDirectoryMappingId,
    onSyncDirectory: data.handleSyncDirectory,
    onImportDirectoryIdentities: data.handleImportDirectoryIdentities,
    onCreateDirectoryMapping: data.handleCreateDirectoryMapping,
    onDeleteDirectoryMapping: data.handleDeleteDirectoryMapping,
  };

  const notificationBundle: AdminConfigNotificationBundle = {
    notificationEmailConfiguration: notificationConfig.notificationEmailConfiguration,
    notificationEnabledDraft: notificationConfig.notificationEnabledDraft,
    notificationSenderEmailDraft: notificationConfig.notificationSenderEmailDraft,
    notificationFrontendBaseUrlDraft: notificationConfig.notificationFrontendBaseUrlDraft,
    notificationTestRecipientDraft: notificationConfig.notificationTestRecipientDraft,
    notificationSandboxRedirectDraft: notificationConfig.notificationSandboxRedirectDraft,
    notificationNotifyOnWorkflowCreatedDraft: notificationConfig.notificationNotifyOnWorkflowCreatedDraft,
    notificationNotifyOnTaskReadyDraft: notificationConfig.notificationNotifyOnTaskReadyDraft,
    notificationNotifyOnWorkflowCompletedDraft: notificationConfig.notificationNotifyOnWorkflowCompletedDraft,
    isSavingNotificationEmailConfiguration: notificationConfig.isSavingNotificationEmailConfiguration,
    isSendingNotificationEmailTest: notificationConfig.isSendingNotificationEmailTest,
    hasNotificationEmailDraftChanges: notificationConfig.hasNotificationEmailDraftChanges,
    notificationTemplates: notificationTemplateConfig.notificationTemplates,
    selectedNotificationTemplate: notificationTemplateConfig.selectedTemplate,
    selectedNotificationTemplateKey: notificationTemplateConfig.selectedTemplateKey,
    selectedNotificationTemplateSubjectDraft: notificationTemplateConfig.selectedTemplateSubjectDraft,
    selectedNotificationTemplateBodyDraft: notificationTemplateConfig.selectedTemplateBodyDraft,
    hasSelectedNotificationTemplateChanges: notificationTemplateConfig.hasSelectedTemplateChanges,
    workflowPreviewSearch: notificationTemplateConfig.workflowPreviewSearch,
    rotationPlanPreviewSearch: notificationTemplateConfig.rotationPlanPreviewSearch,
    workflowPreviewTargets: notificationTemplateConfig.workflowPreviewTargets,
    rotationPlanPreviewTargets: notificationTemplateConfig.rotationPlanPreviewTargets,
    selectedWorkflowPreviewUid: notificationTemplateConfig.selectedWorkflowPreviewUid,
    selectedRotationPlanPreviewId: notificationTemplateConfig.selectedRotationPlanPreviewId,
    notificationTemplatePreviewResponse: notificationTemplateConfig.previewResponse,
    selectedNotificationTemplatePreviewVariantIndex: notificationTemplateConfig.selectedPreviewVariantIndex,
    isLoadingNotificationTemplates: notificationTemplateConfig.isLoadingNotificationTemplates,
    isSavingNotificationTemplate: notificationTemplateConfig.isSavingNotificationTemplate,
    isLoadingNotificationTemplatePreviewTargets: notificationTemplateConfig.isLoadingPreviewTargets,
    isLoadingNotificationTemplatePreview: notificationTemplateConfig.isLoadingPreview,
    onNotificationEnabledChange: notificationConfig.setNotificationEnabledDraft,
    onNotificationSenderEmailChange: notificationConfig.setNotificationSenderEmailDraft,
    onNotificationFrontendBaseUrlChange: notificationConfig.setNotificationFrontendBaseUrlDraft,
    onNotificationTestRecipientChange: notificationConfig.setNotificationTestRecipientDraft,
    onNotificationSandboxRedirectChange: notificationConfig.setNotificationSandboxRedirectDraft,
    onNotificationNotifyOnWorkflowCreatedChange: notificationConfig.setNotificationNotifyOnWorkflowCreatedDraft,
    onNotificationNotifyOnTaskReadyChange: notificationConfig.setNotificationNotifyOnTaskReadyDraft,
    onNotificationNotifyOnWorkflowCompletedChange: notificationConfig.setNotificationNotifyOnWorkflowCompletedDraft,
    onSaveNotificationEmailConfiguration: notificationConfig.saveNotificationEmailConfiguration,
    onSendNotificationEmailTest: notificationConfig.sendNotificationEmailTest,
    onSelectNotificationTemplate: notificationTemplateConfig.setSelectedTemplateKey,
    onSelectedNotificationTemplateSubjectChange: notificationTemplateConfig.setSelectedTemplateSubjectDraft,
    onSelectedNotificationTemplateBodyChange: notificationTemplateConfig.setSelectedTemplateBodyDraft,
    onWorkflowPreviewSearchChange: notificationTemplateConfig.setWorkflowPreviewSearch,
    onRotationPlanPreviewSearchChange: notificationTemplateConfig.setRotationPlanPreviewSearch,
    onSelectWorkflowPreviewTarget: notificationTemplateConfig.setSelectedWorkflowPreviewUid,
    onSelectRotationPlanPreviewTarget: notificationTemplateConfig.setSelectedRotationPlanPreviewId,
    onSaveSelectedNotificationTemplate: notificationTemplateConfig.saveSelectedTemplate,
    onRenderNotificationTemplatePreview: notificationTemplateConfig.renderPreview,
    onSelectNotificationTemplatePreviewVariant: notificationTemplateConfig.setSelectedPreviewVariantIndex,
  };

  const systemBundle: AdminConfigSystemBundle = {
    graphApplicationConfiguration,
  };

  if (redirectToBuilder) {
    return <Navigate to="/builder" replace />;
  }

  if (redirectToSystemLogs) {
    const nextParams = new URLSearchParams(searchParams);
    nextParams.set("section", "system_logs");
    return <Navigate to={`/admin/config?${nextParams.toString()}`} replace />;
  }

  if (redirectToSystemConfiguration) {
    const nextParams = new URLSearchParams(searchParams);
    nextParams.set("section", "system_configuration");
    return <Navigate to={`/admin/config?${nextParams.toString()}`} replace />;
  }

  if (legacyOrganizationRedirect) {
    const nextParams = new URLSearchParams(searchParams);
    nextParams.set("section", legacyOrganizationRedirect);
    nextParams.delete("entity");
    return <Navigate to={`/admin/config?${nextParams.toString()}`} replace />;
  }

  if (legacyRotationRedirect) {
    const nextParams = new URLSearchParams(searchParams);
    nextParams.set("section", legacyRotationRedirect);
    return <Navigate to={`/admin/config?${nextParams.toString()}`} replace />;
  }

  return (
    <main className="app-shell">
      <div className="page-container admin-settings-page">
        <PageHeader
          variant="section"
          title="Administration"
          description={(() => {
            if (section === "overview") return undefined;
            const sectionMeta = getAdminWorkspaceSectionMeta(section);
            const areaMeta = sectionMeta.area ? getAdminWorkspaceAreaMeta(sectionMeta.area) : null;
            return areaMeta ? `${areaMeta.label} › ${sectionMeta.label}` : sectionMeta.label;
          })()}
        />

        <div className="admin-settings-shell">
          <AppErrorBoundary scope="AdminConfigPage/sidebar" inline>
            <aside className="admin-settings-sidebar">
              <AdminWorkspaceNavigation section={section} onSelectSection={view.handleSelectSection} />
            </aside>
          </AppErrorBoundary>

          <section className="admin-settings-main" aria-label="Admin-Arbeitsbereich">
            {!data.isLoading && notice ? (
              <section className="panel panel-success panel-banner">
                <p className="panel-text">{notice}</p>
                <button
                  type="button"
                  className="toast-close"
                  aria-label="Hinweis schließen"
                  onClick={() => setNotice(null)}
                >
                  ×
                </button>
              </section>
            ) : null}

            {!data.isLoading && error && view.hasAnyData ? (
              <section className="panel panel-error panel-banner" role="alert">
                <p className="panel-text text-error">{error}</p>
                <button
                  type="button"
                  className="toast-close"
                  aria-label="Fehler schließen"
                  onClick={() => handleError(null)}
                >
                  ×
                </button>
              </section>
            ) : null}

            {data.isLoading ? <LoadingState title="Stammdaten werden geladen..." /> : null}
            {!data.isLoading && error && !view.hasAnyData ? (
              <EmptyState title="Stammdaten konnten nicht geladen werden." description={error} />
            ) : null}

            {!data.isLoading && (!error || view.hasAnyData) ? (
              <AppErrorBoundary scope="AdminConfigPage/workspace" inline>
                <section className="content-stack">
                  <AdminConfigWorkspaceContent
                    meta={meta}
                    user={userBundle}
                    organization={organizationBundle}
                    access={accessBundle}
                    directory={directoryBundle}
                    notification={notificationBundle}
                    system={systemBundle}
                  />
                </section>
              </AppErrorBoundary>
            ) : null}
          </section>
        </div>
      </div>
    </main>
  );
}
