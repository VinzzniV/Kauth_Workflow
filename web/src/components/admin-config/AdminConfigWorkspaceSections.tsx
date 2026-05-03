import type { ReactNode } from "react";
import { AdminDirectorySyncSection } from "./AdminDirectorySyncSection";
import { AdminFieldConfigurationWorkspaceSection } from "./AdminFieldConfigurationWorkspaceSection";
import { AdminGroupMappingSection } from "./AdminGroupMappingSection";
import { AdminOrganizationWorkspaceSection } from "./AdminOrganizationWorkspaceSection";
import { AdminOverviewWorkspaceSection } from "./AdminOverviewWorkspaceSection";
import { AdminResponsibilitiesAndRequirementsSection } from "./AdminResponsibilitiesAndRequirementsSection";
import { AdminPermissionsSection } from "./AdminPermissionsSection";
import { AdminNotificationTemplateSection } from "./AdminNotificationTemplateSection";
import { AdminSystemConfigurationSection } from "./AdminSystemConfigurationSection";
import { AdminSystemWorkspaceSection } from "./AdminSystemWorkspaceSection";
import { AdminTaskTemplateSection } from "./AdminTaskTemplateSection";
import { AdminTechnicalAccessSection } from "./AdminTechnicalAccessSection";
import { AdminWorkflowBuilderFormSection } from "./AdminWorkflowBuilderFormSection";
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
  const { meta, organization, access, notification } = props;
  return (
    <AdminOverviewWorkspaceSection
      departmentCount={organization.departmentAssignments.length}
      warningCount={meta.warnings.length}
      hasLoadedTechnicalAccess={access.hasLoadedTechnicalAccess}
      roleCount={access.sortedRoles.length}
      groupCount={access.groups.length}
      notificationEmailConfiguration={notification.notificationEmailConfiguration}
      warnings={meta.warnings}
      onOpenOrganization={meta.onOpenOrganization}
      onOpenSection={meta.onSelectSection}
    />
  );
}

export function renderOrganizationWorkspace(props: AdminConfigWorkspaceContentProps) {
  const { meta, user, organization } = props;
  return renderWorkspaceWithIntro(
    "organization",
    <AdminOrganizationWorkspaceSection
      organizationEntity={meta.organizationEntity}
      selectedEntityId={meta.selectedEntityId}
      sortedUsers={user.sortedUsers}
      sortedDepartmentPositions={organization.sortedDepartmentPositions}
      sortedDepartments={organization.sortedDepartments}
      eligibleSupervisorUsers={user.eligibleSupervisorUsers}
      eligibleRequirementOwnerUsers={user.eligibleRequirementOwnerUsers}
      selectedUser={user.workspaceSelectedUser}
      userDisplayNameDraft={user.userDisplayNameDraft}
      userEmailDraft={user.userEmailDraft}
      userNotificationEmailDraft={user.userNotificationEmailDraft}
      userExternalKeyDraft={user.userExternalKeyDraft}
      userDepartmentIdDraft={user.userDepartmentIdDraft}
      userIsActiveDraft={user.userIsActiveDraft}
      userFormError={user.userFormError}
      userFormNotice={user.userFormNotice}
      newUserDisplayNameDraft={user.newUserDisplayNameDraft}
      newUserEmailDraft={user.newUserEmailDraft}
      newUserNotificationEmailDraft={user.newUserNotificationEmailDraft}
      newUserExternalKeyDraft={user.newUserExternalKeyDraft}
      newUserDepartmentIdDraft={user.newUserDepartmentIdDraft}
      newUserIsActiveDraft={user.newUserIsActiveDraft}
      isCreatingUser={user.isCreatingUser}
      isSavingUserMasterData={user.isSavingUserMasterData}
      deletingUserId={user.deletingUserId}
      newDepartmentNameDraft={organization.newDepartmentNameDraft}
      newPositionNameDraft={organization.newPositionNameDraft}
      departmentDrafts={organization.departmentDrafts}
      positionDrafts={organization.positionDrafts}
      isCreatingDepartment={organization.isCreatingDepartment}
      creatingPositionDepartmentId={organization.creatingPositionDepartmentId}
      deletingDepartmentId={organization.deletingDepartmentId}
      deletingPositionId={organization.deletingPositionId}
      savingDepartmentId={organization.savingDepartmentId}
      savingPositionId={organization.savingPositionId}
      onSelectOrganizationEntity={meta.onOpenOrganization}
      onSelectUser={user.onSelectUser}
      onNewUserDisplayNameChange={user.onNewUserDisplayNameChange}
      onNewUserEmailChange={user.onNewUserEmailChange}
      onNewUserNotificationEmailChange={user.onNewUserNotificationEmailChange}
      onNewUserExternalKeyChange={user.onNewUserExternalKeyChange}
      onNewUserDepartmentIdChange={user.onNewUserDepartmentIdChange}
      onNewUserIsActiveChange={user.onNewUserIsActiveChange}
      onUserDisplayNameChange={user.onUserDisplayNameChange}
      onUserEmailChange={user.onUserEmailChange}
      onUserNotificationEmailChange={user.onUserNotificationEmailChange}
      onUserExternalKeyChange={user.onUserExternalKeyChange}
      onUserDepartmentIdChange={user.onUserDepartmentIdChange}
      onUserIsActiveChange={user.onUserIsActiveChange}
      onCreateUser={user.onCreateUser}
      onSaveUserMasterData={user.onSaveUserMasterData}
      onRemoveUser={user.onRemoveUser}
      onNewDepartmentNameChange={organization.onNewDepartmentNameChange}
      onNewPositionNameChange={organization.onNewPositionNameChange}
      onDepartmentDraftChange={organization.onDepartmentDraftChange}
      onCreateDepartment={organization.onCreateDepartment}
      onCreateDepartmentPosition={organization.onCreateDepartmentPosition}
      onSaveDepartmentAssignment={organization.onSaveDepartmentAssignment}
      onRemoveDepartment={organization.onRemoveDepartment}
      onPositionDraftChange={organization.onPositionDraftChange}
      onSaveDepartmentPosition={organization.onSaveDepartmentPosition}
      onRemoveDepartmentPosition={organization.onRemoveDepartmentPosition}
    />
  );
}

export function renderRotationRequirementsWorkspace() {
  return renderWorkspaceWithIntro(
    "rotation_requirements",
    <AdminResponsibilitiesAndRequirementsSection />
  );
}

export function renderAccessWorkspace(props: AdminConfigWorkspaceContentProps) {
  const { user, access, organization } = props;
  return renderWorkspaceWithIntro(
    "access",
    <div className="content-stack">
      <AdminTechnicalAccessSection
        isTechnicalAccessOpen={true}
        isLoadingTechnicalAccess={access.isLoadingTechnicalAccess}
        sortedUsers={user.sortedUsers}
        selectedUser={user.selectedUser}
        selectedUserRoleIds={access.selectedUserRoleIds}
        selectedUserGroupIds={access.selectedUserGroupIds}
        selectedGroupId={access.selectedGroupId}
        selectedGroup={access.selectedGroup}
        selectedGroupRoleIds={access.selectedGroupRoleIds}
        sortedRoles={access.sortedRoles}
        groups={access.groups}
        isSavingUserRoles={access.isSavingUserRoles}
        isSavingUserGroups={access.isSavingUserGroups}
        isSavingGroupRoles={access.isSavingGroupRoles}
        onSelectUser={user.onSelectUser}
        onToggleUserRole={access.onToggleUserRole}
        onToggleUserGroup={access.onToggleUserGroup}
        onSelectGroup={access.onSelectGroup}
        onToggleGroupRole={access.onToggleGroupRole}
        onSaveUserRoles={access.onSaveUserRoles}
        onSaveUserGroups={access.onSaveUserGroups}
        onSaveGroupRoles={access.onSaveGroupRoles}
      />
      <AdminPermissionsSection
        roles={access.sortedRoles}
        permissions={access.permissions}
        auditEntries={access.permissionAuditEntries}
        departments={organization.sortedDepartments}
        selectedRoleId={access.selectedRoleId}
        selectedRolePermissionIds={access.selectedRolePermissionIds}
        selectedUser={user.selectedUser}
        userOverrideDrafts={access.userOverrideDrafts}
        isLoading={access.isLoadingTechnicalAccess}
        isSavingRolePermissions={access.isSavingRolePermissions}
        isSavingUserOverrides={access.isSavingUserOverrides}
        onSelectRole={access.onSelectRole}
        onToggleRolePermission={access.onToggleRolePermission}
        onSaveRolePermissions={access.onSaveRolePermissions}
        onUserOverrideDraftsChange={access.onUserOverrideDraftsChange}
        onSaveUserOverrides={access.onSaveUserOverrides}
      />
    </div>
  );
}

export function renderDirectoryWorkspace(props: AdminConfigWorkspaceContentProps) {
  const { access, directory, organization } = props;
  return renderWorkspaceWithIntro(
    "directory",
    <div className="content-stack">
      <AdminDirectorySyncSection
        status={directory.directoryStatus}
        identities={directory.directoryIdentities}
        auditEntries={directory.directoryAuditEntries}
        isLoading={directory.isLoadingDirectory}
        isSyncing={directory.isSyncingDirectory}
        onSync={directory.onSyncDirectory}
      />
      <AdminGroupMappingSection
        groups={directory.directoryGroups}
        roles={access.sortedRoles}
        departments={organization.sortedDepartments}
        isLoading={directory.isLoadingDirectory}
        savingGroupId={directory.savingDirectoryGroupId}
        deletingMappingId={directory.deletingDirectoryMappingId}
        onCreateMapping={directory.onCreateDirectoryMapping}
        onDeleteMapping={directory.onDeleteDirectoryMapping}
      />
    </div>
  );
}

export function renderSystemLogsWorkspace() {
  return renderWorkspaceWithIntro("system_logs", <AdminSystemWorkspaceSection />);
}

export function renderSystemMailTemplatesWorkspace(props: AdminConfigWorkspaceContentProps) {
  const { notification } = props;
  return renderWorkspaceWithIntro(
    "system_mail_templates",
    <AdminNotificationTemplateSection
      notificationTemplates={notification.notificationTemplates}
      selectedTemplate={notification.selectedNotificationTemplate}
      selectedTemplateKey={notification.selectedNotificationTemplateKey}
      selectedTemplateSubjectDraft={notification.selectedNotificationTemplateSubjectDraft}
      selectedTemplateBodyDraft={notification.selectedNotificationTemplateBodyDraft}
      hasSelectedTemplateChanges={notification.hasSelectedNotificationTemplateChanges}
      workflowPreviewSearch={notification.workflowPreviewSearch}
      rotationPlanPreviewSearch={notification.rotationPlanPreviewSearch}
      workflowPreviewTargets={notification.workflowPreviewTargets}
      rotationPlanPreviewTargets={notification.rotationPlanPreviewTargets}
      selectedWorkflowPreviewUid={notification.selectedWorkflowPreviewUid}
      selectedRotationPlanPreviewId={notification.selectedRotationPlanPreviewId}
      previewResponse={notification.notificationTemplatePreviewResponse}
      selectedPreviewVariantIndex={notification.selectedNotificationTemplatePreviewVariantIndex}
      isLoadingNotificationTemplates={notification.isLoadingNotificationTemplates}
      isSavingNotificationTemplate={notification.isSavingNotificationTemplate}
      isLoadingPreviewTargets={notification.isLoadingNotificationTemplatePreviewTargets}
      isLoadingPreview={notification.isLoadingNotificationTemplatePreview}
      onSelectTemplate={notification.onSelectNotificationTemplate}
      onSelectedTemplateSubjectChange={notification.onSelectedNotificationTemplateSubjectChange}
      onSelectedTemplateBodyChange={notification.onSelectedNotificationTemplateBodyChange}
      onWorkflowPreviewSearchChange={notification.onWorkflowPreviewSearchChange}
      onRotationPlanPreviewSearchChange={notification.onRotationPlanPreviewSearchChange}
      onSelectWorkflowPreviewTarget={notification.onSelectWorkflowPreviewTarget}
      onSelectRotationPlanPreviewTarget={notification.onSelectRotationPlanPreviewTarget}
      onSaveSelectedTemplate={notification.onSaveSelectedNotificationTemplate}
      onRenderPreview={notification.onRenderNotificationTemplatePreview}
      onSelectPreviewVariant={notification.onSelectNotificationTemplatePreviewVariant}
    />
  );
}

export function renderSystemConfigurationWorkspace(props: AdminConfigWorkspaceContentProps) {
  const { notification, system } = props;
  return renderWorkspaceWithIntro(
    "system_configuration",
    <AdminSystemConfigurationSection
      graphApplicationConfiguration={system.graphApplicationConfiguration}
      notificationEmailConfiguration={notification.notificationEmailConfiguration}
      notificationEnabledDraft={notification.notificationEnabledDraft}
      notificationSenderEmailDraft={notification.notificationSenderEmailDraft}
      notificationFrontendBaseUrlDraft={notification.notificationFrontendBaseUrlDraft}
      notificationTestRecipientDraft={notification.notificationTestRecipientDraft}
      notificationSandboxRedirectDraft={notification.notificationSandboxRedirectDraft}
      notificationNotifyOnWorkflowCreatedDraft={notification.notificationNotifyOnWorkflowCreatedDraft}
      notificationNotifyOnTaskReadyDraft={notification.notificationNotifyOnTaskReadyDraft}
      notificationNotifyOnWorkflowCompletedDraft={notification.notificationNotifyOnWorkflowCompletedDraft}
      isSavingNotificationEmailConfiguration={notification.isSavingNotificationEmailConfiguration}
      isSendingNotificationEmailTest={notification.isSendingNotificationEmailTest}
      isLoading={false}
      hasNotificationEmailDraftChanges={notification.hasNotificationEmailDraftChanges}
      onNotificationEnabledChange={notification.onNotificationEnabledChange}
      onNotificationSenderEmailChange={notification.onNotificationSenderEmailChange}
      onNotificationFrontendBaseUrlChange={notification.onNotificationFrontendBaseUrlChange}
      onNotificationTestRecipientChange={notification.onNotificationTestRecipientChange}
      onNotificationSandboxRedirectChange={notification.onNotificationSandboxRedirectChange}
      onNotificationNotifyOnWorkflowCreatedChange={notification.onNotificationNotifyOnWorkflowCreatedChange}
      onNotificationNotifyOnTaskReadyChange={notification.onNotificationNotifyOnTaskReadyChange}
      onNotificationNotifyOnWorkflowCompletedChange={notification.onNotificationNotifyOnWorkflowCompletedChange}
      onSaveNotificationEmailConfiguration={notification.onSaveNotificationEmailConfiguration}
      onSendNotificationEmailTest={notification.onSendNotificationEmailTest}
    />
  );
}

export function renderTemplateWorkspace(props: AdminConfigWorkspaceContentProps) {
  const { meta, organization } = props;
  return renderWorkspaceWithIntro(
    "templates",
    <AdminTaskTemplateSection
      departments={organization.departmentAssignments}
      responsibilities={organization.responsibilityOwners}
      onNotice={meta.onNotice}
      onError={meta.onError}
    />
  );
}

export function renderBuilderWorkspace(props: AdminConfigWorkspaceContentProps) {
  const { meta } = props;
  return renderWorkspaceWithIntro(
    "builder",
    <AdminWorkflowBuilderFormSection onNotice={meta.onNotice} onError={meta.onError} />
  );
}

export function renderAnswerWorkspace(props: AdminConfigWorkspaceContentProps) {
  const { meta } = props;
  return renderWorkspaceWithIntro(
    "answers",
    <AdminFieldConfigurationWorkspaceSection
      section="answers"
      onSelectSection={meta.onSelectSection}
      onNotice={meta.onNotice}
      onError={meta.onError}
    />
  );
}

export function renderDefaultWorkspace(props: AdminConfigWorkspaceContentProps) {
  const { meta } = props;
  return renderWorkspaceWithIntro(
    "defaults",
    <AdminFieldConfigurationWorkspaceSection
      section="defaults"
      onSelectSection={meta.onSelectSection}
      onNotice={meta.onNotice}
      onError={meta.onError}
    />
  );
}
