import type { ReactNode } from "react";
import { AdminBulkOperationsSection } from "./AdminBulkOperationsSection";
import { AdminDirectorySyncSection } from "./AdminDirectorySyncSection";
import { AdminFieldConfigurationWorkspaceSection } from "./AdminFieldConfigurationWorkspaceSection";
import { AdminGroupMappingSection } from "./AdminGroupMappingSection";
import { AdminOrganizationWorkspaceSection } from "./AdminOrganizationWorkspaceSection";
import { AdminOverviewWorkspaceSection } from "./AdminOverviewWorkspaceSection";
import { AdminPermissionsSection } from "./AdminPermissionsSection";
import { AdminSystemWorkspaceSection } from "./AdminSystemWorkspaceSection";
import { AdminTaskTemplateSection } from "./AdminTaskTemplateSection";
import { AdminTechnicalAccessSection } from "./AdminTechnicalAccessSection";
import { AdminWorkspaceIntro } from "./AdminWorkspaceIntro";
import type { AdminConfigWorkspaceContentProps } from "./adminConfigWorkspaceContentTypes";
import {
  getAdminWorkspacePresentationSection,
  getAdminWorkspaceSectionMeta,
  type AdminWorkspaceSection,
} from "./adminWorkspaceModel";

function renderWorkspaceWithIntro(section: AdminWorkspaceSection, content: ReactNode) {
  const meta = getAdminWorkspaceSectionMeta(getAdminWorkspacePresentationSection(section));

  return (
    <div className="content-stack">
      <AdminWorkspaceIntro meta={meta} />
      {content}
    </div>
  );
}

export function renderOverviewWorkspace(props: AdminConfigWorkspaceContentProps) {
  return (
    <AdminOverviewWorkspaceSection
      departmentCount={props.departmentAssignments.length}
      warningCount={props.warnings.length}
      hasLoadedTechnicalAccess={props.hasLoadedTechnicalAccess}
      roleCount={props.sortedRoles.length}
      groupCount={props.groups.length}
      notificationEmailConfiguration={props.notificationEmailConfiguration}
      warnings={props.warnings}
      onOpenOrganization={props.onOpenOrganization}
      onOpenSection={props.onSelectSection}
    />
  );
}

export function renderOrganizationWorkspace(props: AdminConfigWorkspaceContentProps) {
  return renderWorkspaceWithIntro(
    "organization",
    <AdminOrganizationWorkspaceSection
      organizationEntity={props.organizationEntity}
      selectedEntityId={props.selectedEntityId}
      sortedUsers={props.sortedUsers}
      sortedDepartments={props.sortedDepartments}
      sortedResponsibilities={props.sortedResponsibilities}
      eligibleSupervisorUsers={props.eligibleSupervisorUsers}
      selectedUser={props.workspaceSelectedUser}
      userDisplayNameDraft={props.userDisplayNameDraft}
      userEmailDraft={props.userEmailDraft}
      userNotificationEmailDraft={props.userNotificationEmailDraft}
      userExternalKeyDraft={props.userExternalKeyDraft}
      userDepartmentIdDraft={props.userDepartmentIdDraft}
      userIsActiveDraft={props.userIsActiveDraft}
      userFormError={props.userFormError}
      userFormNotice={props.userFormNotice}
      newUserDisplayNameDraft={props.newUserDisplayNameDraft}
      newUserEmailDraft={props.newUserEmailDraft}
      newUserNotificationEmailDraft={props.newUserNotificationEmailDraft}
      newUserExternalKeyDraft={props.newUserExternalKeyDraft}
      newUserDepartmentIdDraft={props.newUserDepartmentIdDraft}
      newUserIsActiveDraft={props.newUserIsActiveDraft}
      isCreatingUser={props.isCreatingUser}
      isSavingUserMasterData={props.isSavingUserMasterData}
      deletingUserId={props.deletingUserId}
      newDepartmentNameDraft={props.newDepartmentNameDraft}
      departmentDrafts={props.departmentDrafts}
      responsibilityDrafts={props.responsibilityDrafts}
      isCreatingDepartment={props.isCreatingDepartment}
      deletingDepartmentId={props.deletingDepartmentId}
      savingDepartmentId={props.savingDepartmentId}
      savingResponsibilityId={props.savingResponsibilityId}
      onSelectOrganizationEntity={props.onOpenOrganization}
      onSelectUser={props.onSelectUser}
      onNewUserDisplayNameChange={props.onNewUserDisplayNameChange}
      onNewUserEmailChange={props.onNewUserEmailChange}
      onNewUserNotificationEmailChange={props.onNewUserNotificationEmailChange}
      onNewUserExternalKeyChange={props.onNewUserExternalKeyChange}
      onNewUserDepartmentIdChange={props.onNewUserDepartmentIdChange}
      onNewUserIsActiveChange={props.onNewUserIsActiveChange}
      onUserDisplayNameChange={props.onUserDisplayNameChange}
      onUserEmailChange={props.onUserEmailChange}
      onUserNotificationEmailChange={props.onUserNotificationEmailChange}
      onUserExternalKeyChange={props.onUserExternalKeyChange}
      onUserDepartmentIdChange={props.onUserDepartmentIdChange}
      onUserIsActiveChange={props.onUserIsActiveChange}
      onCreateUser={props.onCreateUser}
      onSaveUserMasterData={props.onSaveUserMasterData}
      onRemoveUser={props.onRemoveUser}
      onNewDepartmentNameChange={props.onNewDepartmentNameChange}
      onDepartmentDraftChange={props.onDepartmentDraftChange}
      onCreateDepartment={props.onCreateDepartment}
      onSaveDepartmentAssignment={props.onSaveDepartmentAssignment}
      onRemoveDepartment={props.onRemoveDepartment}
      onResponsibilityDraftChange={props.onResponsibilityDraftChange}
      onSaveResponsibilityAssignment={props.onSaveResponsibilityAssignment}
    />
  );
}

export function renderAccessWorkspace(props: AdminConfigWorkspaceContentProps) {
  return renderWorkspaceWithIntro(
    "access",
    <div className="content-stack">
      <AdminTechnicalAccessSection
        isTechnicalAccessOpen={true}
        isLoadingTechnicalAccess={props.isLoadingTechnicalAccess}
        sortedUsers={props.sortedUsers}
        selectedUser={props.selectedUser}
        selectedUserRoleIds={props.selectedUserRoleIds}
        selectedUserGroupIds={props.selectedUserGroupIds}
        selectedGroupId={props.selectedGroupId}
        selectedGroup={props.selectedGroup}
        selectedGroupRoleIds={props.selectedGroupRoleIds}
        sortedRoles={props.sortedRoles}
        groups={props.groups}
        isSavingUserRoles={props.isSavingUserRoles}
        isSavingUserGroups={props.isSavingUserGroups}
        isSavingGroupRoles={props.isSavingGroupRoles}
        onSelectUser={props.onSelectUser}
        onToggleUserRole={props.onToggleUserRole}
        onToggleUserGroup={props.onToggleUserGroup}
        onSelectGroup={props.onSelectGroup}
        onToggleGroupRole={props.onToggleGroupRole}
        onSaveUserRoles={props.onSaveUserRoles}
        onSaveUserGroups={props.onSaveUserGroups}
        onSaveGroupRoles={props.onSaveGroupRoles}
      />
      <AdminPermissionsSection
        roles={props.sortedRoles}
        permissions={props.permissions}
        auditEntries={props.permissionAuditEntries}
        departments={props.sortedDepartments}
        selectedRoleId={props.selectedRoleId}
        selectedRolePermissionIds={props.selectedRolePermissionIds}
        selectedUser={props.selectedUser}
        userOverrideDrafts={props.userOverrideDrafts}
        isLoading={props.isLoadingTechnicalAccess}
        isSavingRolePermissions={props.isSavingRolePermissions}
        isSavingUserOverrides={props.isSavingUserOverrides}
        onSelectRole={props.onSelectRole}
        onToggleRolePermission={props.onToggleRolePermission}
        onSaveRolePermissions={props.onSaveRolePermissions}
        onUserOverrideDraftsChange={props.onUserOverrideDraftsChange}
        onSaveUserOverrides={props.onSaveUserOverrides}
      />
    </div>
  );
}

export function renderDirectoryWorkspace(props: AdminConfigWorkspaceContentProps) {
  return renderWorkspaceWithIntro(
    "directory",
    <div className="content-stack">
      <AdminDirectorySyncSection
        status={props.directoryStatus}
        identities={props.directoryIdentities}
        auditEntries={props.directoryAuditEntries}
        isLoading={props.isLoadingDirectory}
        isSyncing={props.isSyncingDirectory}
        onSync={props.onSyncDirectory}
      />
      <AdminGroupMappingSection
        groups={props.directoryGroups}
        roles={props.sortedRoles}
        departments={props.sortedDepartments}
        isLoading={props.isLoadingDirectory}
        savingGroupId={props.savingDirectoryGroupId}
        deletingMappingId={props.deletingDirectoryMappingId}
        onCreateMapping={props.onCreateDirectoryMapping}
        onDeleteMapping={props.onDeleteDirectoryMapping}
      />
    </div>
  );
}

export function renderSystemWorkspace(props: AdminConfigWorkspaceContentProps) {
  return renderWorkspaceWithIntro(
    "system",
    <AdminSystemWorkspaceSection
      graphApplicationConfiguration={props.graphApplicationConfiguration}
      notificationEmailConfiguration={props.notificationEmailConfiguration}
      notificationEnabledDraft={props.notificationEnabledDraft}
      notificationSenderEmailDraft={props.notificationSenderEmailDraft}
      notificationFrontendBaseUrlDraft={props.notificationFrontendBaseUrlDraft}
      notificationTestRecipientDraft={props.notificationTestRecipientDraft}
      notificationSandboxRedirectDraft={props.notificationSandboxRedirectDraft}
      notificationNotifyOnWorkflowCreatedDraft={props.notificationNotifyOnWorkflowCreatedDraft}
      notificationNotifyOnTaskReadyDraft={props.notificationNotifyOnTaskReadyDraft}
      notificationNotifyOnWorkflowCompletedDraft={props.notificationNotifyOnWorkflowCompletedDraft}
      isSavingNotificationEmailConfiguration={props.isSavingNotificationEmailConfiguration}
      isSendingNotificationEmailTest={props.isSendingNotificationEmailTest}
      isLoading={false}
      hasNotificationEmailDraftChanges={props.hasNotificationEmailDraftChanges}
      workflowConfig={props.workflowConfig}
      onNotificationEnabledChange={props.onNotificationEnabledChange}
      onNotificationSenderEmailChange={props.onNotificationSenderEmailChange}
      onNotificationFrontendBaseUrlChange={props.onNotificationFrontendBaseUrlChange}
      onNotificationTestRecipientChange={props.onNotificationTestRecipientChange}
      onNotificationSandboxRedirectChange={props.onNotificationSandboxRedirectChange}
      onNotificationNotifyOnWorkflowCreatedChange={props.onNotificationNotifyOnWorkflowCreatedChange}
      onNotificationNotifyOnTaskReadyChange={props.onNotificationNotifyOnTaskReadyChange}
      onNotificationNotifyOnWorkflowCompletedChange={props.onNotificationNotifyOnWorkflowCompletedChange}
      onSaveNotificationEmailConfiguration={props.onSaveNotificationEmailConfiguration}
      onSendNotificationEmailTest={props.onSendNotificationEmailTest}
    />
  );
}

export function renderTemplateWorkspace(props: AdminConfigWorkspaceContentProps) {
  return renderWorkspaceWithIntro(
    "templates",
    <AdminTaskTemplateSection
      departments={props.departmentAssignments}
      responsibilities={props.responsibilityOwners}
      onNotice={props.onNotice}
      onError={props.onError}
    />
  );
}

export function renderAnswerWorkspace(props: AdminConfigWorkspaceContentProps) {
  return renderWorkspaceWithIntro(
    "answers",
    <AdminFieldConfigurationWorkspaceSection
      section="answers"
      onSelectSection={props.onSelectSection}
      onNotice={props.onNotice}
      onError={props.onError}
    />
  );
}

export function renderDefaultWorkspace(props: AdminConfigWorkspaceContentProps) {
  return renderWorkspaceWithIntro(
    "defaults",
    <AdminFieldConfigurationWorkspaceSection
      section="defaults"
      onSelectSection={props.onSelectSection}
      onNotice={props.onNotice}
      onError={props.onError}
    />
  );
}

export function renderOperationsWorkspace(props: AdminConfigWorkspaceContentProps) {
  return renderWorkspaceWithIntro(
    "operations",
    <AdminBulkOperationsSection departments={props.departmentAssignments} />
  );
}
