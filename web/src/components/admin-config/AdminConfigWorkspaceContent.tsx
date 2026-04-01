import { AdminOrganizationWorkspaceSection } from "./AdminOrganizationWorkspaceSection";
import { AdminAnswerDefinitionSection } from "./AdminAnswerDefinitionSection";
import { AdminOverviewWorkspaceSection } from "./AdminOverviewWorkspaceSection";
import { AdminRoleAnswerDefaultsSection } from "./AdminRoleAnswerDefaultsSection";
import { AdminSystemWorkspaceSection } from "./AdminSystemWorkspaceSection";
import { AdminTaskTemplateSection } from "./AdminTaskTemplateSection";
import { AdminTechnicalAccessSection } from "./AdminTechnicalAccessSection";
import { AdminBulkOperationsSection } from "./AdminBulkOperationsSection";
import { AdminDirectorySyncSection } from "./AdminDirectorySyncSection";
import { AdminGroupMappingSection } from "./AdminGroupMappingSection";
import { AdminPermissionsSection, type AdminPermissionOverrideDraft } from "./AdminPermissionsSection";
import type {
  AdminDepartmentAssignment,
  AdminDirectoryGroup,
  AdminDirectoryIdentity,
  AdminDirectoryMappingAuditEntry,
  AdminDirectorySyncStatus,
  AdminGraphApplicationConfiguration,
  AdminGroup,
  AdminNotificationEmailConfiguration,
  AdminPermission,
  AdminPermissionAuditEntry,
  AdminResponsibilityOwner,
  AdminRole,
  AdminUser,
} from "../../types/auth";
import type { WorkflowConfig } from "../../types/workflow";
import type {
  AdminOrganizationEntity,
  AdminWorkspaceSection,
  AdminWorkspaceWarning,
} from "./adminWorkspaceModel";
import type { DepartmentDraft, ResponsibilityDraft } from "./adminOrganizationTypes";

type AdminConfigWorkspaceContentProps = {
  section: AdminWorkspaceSection;
  organizationEntity: AdminOrganizationEntity;
  selectedEntityId: number | null;
  users: AdminUser[];
  departmentAssignments: AdminDepartmentAssignment[];
  responsibilityOwners: AdminResponsibilityOwner[];
  sortedUsers: AdminUser[];
  sortedDepartments: AdminDepartmentAssignment[];
  sortedResponsibilities: AdminResponsibilityOwner[];
  eligibleSupervisorUsers: AdminUser[];
  selectedUser: AdminUser | null;
  workspaceSelectedUser: AdminUser | null;
  userDisplayNameDraft: string;
  userEmailDraft: string;
  userNotificationEmailDraft: string;
  userExternalKeyDraft: string;
  userDepartmentIdDraft: string;
  userIsActiveDraft: boolean;
  userFormError: string | null;
  userFormNotice: string | null;
  newUserDisplayNameDraft: string;
  newUserEmailDraft: string;
  newUserNotificationEmailDraft: string;
  newUserExternalKeyDraft: string;
  newUserDepartmentIdDraft: string;
  newUserIsActiveDraft: boolean;
  isCreatingUser: boolean;
  isSavingUserMasterData: boolean;
  deletingUserId: number | null;
  newDepartmentNameDraft: string;
  departmentDrafts: Record<number, DepartmentDraft>;
  responsibilityDrafts: Record<number, ResponsibilityDraft>;
  isCreatingDepartment: boolean;
  deletingDepartmentId: number | null;
  savingDepartmentId: number | null;
  savingResponsibilityId: number | null;
  hasLoadedTechnicalAccess: boolean;
  isLoadingTechnicalAccess: boolean;
  isLoadingDirectory: boolean;
  isSyncingDirectory: boolean;
  savingDirectoryGroupId: number | null;
  deletingDirectoryMappingId: number | null;
  selectedUserRoleIds: number[];
  selectedUserGroupIds: number[];
  selectedGroupId: number | null;
  selectedGroup: AdminGroup | null;
  selectedGroupRoleIds: number[];
  sortedRoles: AdminRole[];
  permissions: AdminPermission[];
  permissionAuditEntries: AdminPermissionAuditEntry[];
  selectedRoleId: number | null;
  selectedRolePermissionIds: number[];
  userOverrideDrafts: AdminPermissionOverrideDraft[];
  groups: AdminGroup[];
  directoryGroups: AdminDirectoryGroup[];
  directoryIdentities: AdminDirectoryIdentity[];
  directoryAuditEntries: AdminDirectoryMappingAuditEntry[];
  directoryStatus: AdminDirectorySyncStatus | null;
  graphApplicationConfiguration: AdminGraphApplicationConfiguration | null;
  notificationEmailConfiguration: AdminNotificationEmailConfiguration | null;
  notificationEnabledDraft: boolean;
  notificationSenderEmailDraft: string;
  notificationFrontendBaseUrlDraft: string;
  notificationTestRecipientDraft: string;
  notificationSandboxRedirectDraft: string;
  notificationNotifyOnWorkflowCreatedDraft: boolean;
  notificationNotifyOnTaskReadyDraft: boolean;
  notificationNotifyOnWorkflowCompletedDraft: boolean;
  isSavingNotificationEmailConfiguration: boolean;
  isSendingNotificationEmailTest: boolean;
  hasNotificationEmailDraftChanges: boolean;
  workflowConfig: WorkflowConfig | null;
  warnings: AdminWorkspaceWarning[];
  isSavingUserRoles: boolean;
  isSavingUserGroups: boolean;
  isSavingGroupRoles: boolean;
  isSavingRolePermissions: boolean;
  isSavingUserOverrides: boolean;
  onOpenOrganization: (entity: AdminOrganizationEntity, id?: number | null) => void;
  onSelectSection: (section: AdminWorkspaceSection) => void;
  onSelectUser: (user: AdminUser) => void;
  onSelectRole: (roleId: number | null) => void;
  onNewUserDisplayNameChange: (value: string) => void;
  onNewUserEmailChange: (value: string) => void;
  onNewUserNotificationEmailChange: (value: string) => void;
  onNewUserExternalKeyChange: (value: string) => void;
  onNewUserDepartmentIdChange: (value: string) => void;
  onNewUserIsActiveChange: (value: boolean) => void;
  onUserDisplayNameChange: (value: string) => void;
  onUserEmailChange: (value: string) => void;
  onUserNotificationEmailChange: (value: string) => void;
  onUserExternalKeyChange: (value: string) => void;
  onUserDepartmentIdChange: (value: string) => void;
  onUserIsActiveChange: (value: boolean) => void;
  onCreateUser: () => void | Promise<void>;
  onSaveUserMasterData: () => void | Promise<void>;
  onRemoveUser: (user: AdminUser) => void | Promise<void>;
  onNewDepartmentNameChange: (value: string) => void;
  onDepartmentDraftChange: (departmentId: number, draft: DepartmentDraft) => void;
  onCreateDepartment: () => void | Promise<void>;
  onSaveDepartmentAssignment: (departmentId: number) => void | Promise<void>;
  onRemoveDepartment: (department: AdminDepartmentAssignment) => void | Promise<void>;
  onResponsibilityDraftChange: (responsibilityId: number, draft: ResponsibilityDraft) => void;
  onSaveResponsibilityAssignment: (responsibilityId: number) => void | Promise<void>;
  onToggleUserRole: (roleId: number) => void;
  onToggleUserGroup: (groupId: number) => void;
  onSelectGroup: (groupId: number | null) => void;
  onToggleGroupRole: (roleId: number) => void;
  onSaveUserRoles: () => void | Promise<void>;
  onSaveUserGroups: () => void | Promise<void>;
  onSaveGroupRoles: () => void | Promise<void>;
  onToggleRolePermission: (permissionId: number) => void;
  onSaveRolePermissions: () => void | Promise<void>;
  onUserOverrideDraftsChange: (drafts: AdminPermissionOverrideDraft[]) => void;
  onSaveUserOverrides: () => void | Promise<void>;
  onSyncDirectory: (groupPrefix: string | null) => void | Promise<void>;
  onCreateDirectoryMapping: (
    directoryGroupId: number,
    appRoleId: number,
    scope: string,
    scopeDepartmentId: number | null
  ) => void | Promise<void>;
  onDeleteDirectoryMapping: (mappingId: number) => void | Promise<void>;
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
  onNotificationEnabledChange: (enabled: boolean) => void;
  onNotificationSenderEmailChange: (value: string) => void;
  onNotificationFrontendBaseUrlChange: (value: string) => void;
  onNotificationTestRecipientChange: (value: string) => void;
  onNotificationSandboxRedirectChange: (value: string) => void;
  onNotificationNotifyOnWorkflowCreatedChange: (value: boolean) => void;
  onNotificationNotifyOnTaskReadyChange: (value: boolean) => void;
  onNotificationNotifyOnWorkflowCompletedChange: (value: boolean) => void;
  onSaveNotificationEmailConfiguration: () => void | Promise<void>;
  onSendNotificationEmailTest: () => void | Promise<void>;
};

export function AdminConfigWorkspaceContent(props: AdminConfigWorkspaceContentProps) {
  switch (props.section) {
    case "overview":
      return (
        <AdminOverviewWorkspaceSection
          departmentCount={props.departmentAssignments.length}
          userCount={props.users.length}
          responsibilityCount={props.responsibilityOwners.length}
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
    case "organization":
      return (
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
    case "access":
      return (
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
    case "directory":
      return (
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
    case "templates":
      return (
        <AdminTaskTemplateSection
          departments={props.departmentAssignments}
          responsibilities={props.responsibilityOwners}
          onNotice={props.onNotice}
          onError={props.onError}
        />
      );
    case "answers":
      return <AdminAnswerDefinitionSection onNotice={props.onNotice} onError={props.onError} />;
    case "defaults":
      return <AdminRoleAnswerDefaultsSection onNotice={props.onNotice} onError={props.onError} />;
    case "system":
      return (
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
    case "operations":
      return <AdminBulkOperationsSection departments={props.departmentAssignments} />;
    default:
      return null;
  }
}
