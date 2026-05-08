import type { ReactNode } from "react";
import { AdminDirectoryPendingImportsSection } from "./AdminDirectoryPendingImportsSection";
import { AdminDirectorySyncSection } from "./AdminDirectorySyncSection";
import { AdminEntraImportSection } from "./AdminEntraImportSection";
import { AdminFieldConfigurationWorkspaceSection } from "./AdminFieldConfigurationWorkspaceSection";
import { AdminGroupMappingSection } from "./AdminGroupMappingSection";
import { AdminAbteilungenSection } from "./AdminAbteilungenSection";
import { AdminAccessWorkspaceContent } from "./AdminAccessWorkspaceContent";
import { AdminPersonenSection } from "./AdminPersonenSection";
import { AdminOverviewWorkspaceSection } from "./AdminOverviewWorkspaceSection";
import {
  AbteilungsanforderungenPanel,
  FachlicheZustaendigkeitenPanel,
} from "./AdminResponsibilitiesAndRequirementsSection";
import { AdminNotificationTemplateSection } from "./AdminNotificationTemplateSection";
import { AdminSystemConfigurationSection } from "./AdminSystemConfigurationSection";
import { AdminSystemWorkspaceSection } from "./AdminSystemWorkspaceSection";
import { AdminTaskTemplateSection } from "./AdminTaskTemplateSection";
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
  const { meta, directory, notification } = props;
  return (
    <AdminOverviewWorkspaceSection
      warningCount={meta.warnings.length}
      notificationEmailConfiguration={notification.notificationEmailConfiguration}
      directoryStatus={directory.directoryStatus}
      directoryPendingImports={directory.directoryPendingImports}
      isSyncingDirectory={directory.isSyncingDirectory}
      warnings={meta.warnings}
      onOpenOrganization={meta.onOpenOrganization}
      onOpenSection={meta.onSelectSection}
    />
  );
}

export function renderPersonenWorkspace(props: AdminConfigWorkspaceContentProps) {
  const { meta, user, organization } = props;
  return renderWorkspaceWithIntro(
    "personen",
    <AdminPersonenSection
      selectedEntityId={meta.selectedEntityId}
      sortedUsers={user.sortedUsers}
      sortedDepartments={organization.sortedDepartments}
      sortedResponsibilities={organization.sortedResponsibilities}
      sortedDepartmentPositions={organization.sortedDepartmentPositions}
      eligibleSupervisorUsers={user.eligibleSupervisorUsers}
      eligibleRequirementOwnerUsers={user.eligibleRequirementOwnerUsers}
      selectedUser={user.selectedUser}
      workspaceSelectedUser={user.workspaceSelectedUser}
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
      departmentDrafts={organization.departmentDrafts}
      positionDrafts={organization.positionDrafts}
      savingDepartmentId={organization.savingDepartmentId}
      onSelectOrganizationEntity={meta.onOpenOrganization}
      onSelectUser={user.onSelectUser}
      onCreateUser={user.onCreateUser}
      onSaveUserMasterData={user.onSaveUserMasterData}
      onRemoveUser={user.onRemoveUser}
      onUserDisplayNameChange={user.onUserDisplayNameChange}
      onUserEmailChange={user.onUserEmailChange}
      onUserNotificationEmailChange={user.onUserNotificationEmailChange}
      onUserExternalKeyChange={user.onUserExternalKeyChange}
      onUserDepartmentIdChange={user.onUserDepartmentIdChange}
      onUserIsActiveChange={user.onUserIsActiveChange}
      onNewUserDisplayNameChange={user.onNewUserDisplayNameChange}
      onNewUserEmailChange={user.onNewUserEmailChange}
      onNewUserNotificationEmailChange={user.onNewUserNotificationEmailChange}
      onNewUserExternalKeyChange={user.onNewUserExternalKeyChange}
      onNewUserDepartmentIdChange={user.onNewUserDepartmentIdChange}
      onNewUserIsActiveChange={user.onNewUserIsActiveChange}
    />
  );
}

export function renderAbteilungenWorkspace(props: AdminConfigWorkspaceContentProps) {
  const { meta, user, organization } = props;
  return renderWorkspaceWithIntro(
    "abteilungen",
    <AdminAbteilungenSection
      selectedEntityId={meta.selectedEntityId}
      sortedUsers={user.sortedUsers}
      sortedDepartments={organization.sortedDepartments}
      sortedDepartmentPositions={organization.sortedDepartmentPositions}
      sortedResponsibilities={organization.sortedResponsibilities}
      eligibleSupervisorUsers={user.eligibleSupervisorUsers}
      eligibleRequirementOwnerUsers={user.eligibleRequirementOwnerUsers}
      selectedUser={user.selectedUser}
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
      onReloadOrganizationData={organization.onReloadOrganizationData}
      onSelectOrganizationEntity={meta.onOpenOrganization}
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

export function renderZustaendigkeitenWorkspace(props: AdminConfigWorkspaceContentProps) {
  const { organization } = props;
  return renderWorkspaceWithIntro(
    "zustaendigkeiten",
    <FachlicheZustaendigkeitenPanel onAfterChange={organization.onReloadOrganizationData} />
  );
}

export function renderMassnahmenvorlagenWorkspace() {
  return renderWorkspaceWithIntro("massnahmenvorlagen", <AbteilungsanforderungenPanel />);
}

export function renderAccessWorkspace(props: AdminConfigWorkspaceContentProps) {
  return renderWorkspaceWithIntro("access", <AdminAccessWorkspaceContent props={props} />);
}

export function renderDirectoryWorkspace(props: AdminConfigWorkspaceContentProps) {
  const { access, directory, organization } = props;
  return renderWorkspaceWithIntro(
    "directory",
    <div className="content-stack">
      <AdminDirectoryPendingImportsSection
        pendingImports={directory.directoryPendingImports}
        isLoading={directory.isLoadingDirectory}
        isImporting={directory.isImportingDirectory}
        onImport={directory.onImportDirectoryIdentities}
      />
      <AdminDirectorySyncSection
        status={directory.directoryStatus}
        identities={directory.directoryIdentities}
        auditEntries={directory.directoryAuditEntries}
        hasMoreAudit={directory.hasMoreDirectoryAudit}
        isLoadingMoreAudit={directory.isLoadingMoreDirectoryAudit}
        onLoadMoreAudit={directory.onLoadMoreDirectoryAudit}
        responsibilityGaps={directory.directoryResponsibilityGaps}
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

export function renderEntraImportWorkspace() {
  return renderWorkspaceWithIntro("entra_import", <AdminEntraImportSection />);
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
