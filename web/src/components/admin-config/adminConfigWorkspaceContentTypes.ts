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
import type { DepartmentDraft, NewResponsibilityDraft, ResponsibilityDraft } from "./adminOrganizationTypes";
import type {
  AdminOrganizationEntity,
  AdminWorkspaceSection,
  AdminWorkspaceWarning,
} from "./adminWorkspaceModel";
import type { AdminPermissionOverrideDraft } from "./AdminPermissionsSection";

export type AdminConfigWorkspaceContentProps = {
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
  eligibleRequirementOwnerUsers: AdminUser[];
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
  newResponsibilityDraft: NewResponsibilityDraft;
  departmentDrafts: Record<number, DepartmentDraft>;
  responsibilityDrafts: Record<number, ResponsibilityDraft>;
  isCreatingDepartment: boolean;
  isCreatingResponsibility: boolean;
  deletingDepartmentId: number | null;
  deletingResponsibilityId: number | null;
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
  onNewResponsibilityDraftChange: (draft: NewResponsibilityDraft) => void;
  onDepartmentDraftChange: (departmentId: number, draft: DepartmentDraft) => void;
  onCreateDepartment: () => void | Promise<void>;
  onCreateResponsibility: () => void | Promise<AdminResponsibilityOwner | null> | AdminResponsibilityOwner | null;
  onSaveDepartmentAssignment: (departmentId: number) => void | Promise<void>;
  onRemoveDepartment: (department: AdminDepartmentAssignment) => void | Promise<void>;
  onResponsibilityDraftChange: (responsibilityId: number, draft: ResponsibilityDraft) => void;
  onRemoveResponsibility: (responsibility: AdminResponsibilityOwner) => void | Promise<boolean> | boolean;
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
