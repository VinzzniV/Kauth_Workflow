import type { Dispatch, SetStateAction } from "react";
import type {
  AdminDepartmentAssignment,
  AdminDirectoryGroup,
  AdminDirectoryIdentity,
  AdminDirectoryMappingAuditEntry,
  AdminDirectorySyncStatus,
  DirectoryPendingImports,
  DirectoryResponsibilityGaps,
  AdminGraphApplicationConfiguration,
  AdminGroup,
  AdminNotificationEmailConfiguration,
  AdminNotificationTemplate,
  AdminNotificationTemplatePreviewResponse,
  AdminNotificationTemplateRotationPlanPreviewTarget,
  AdminNotificationTemplateWorkflowPreviewTarget,
  AdminPermission,
  AdminPermissionAuditEntry,
  AdminResponsibilityOwner,
  AdminRole,
  AdminUser,
} from "../../types/auth";
import type { DepartmentDraft, NewResponsibilityDraft, PositionDraft, ResponsibilityDraft } from "./adminOrganizationTypes";
import type {
  AdminOrganizationEntity,
  AdminWorkspaceSection,
  AdminWorkspaceWarning,
} from "./adminWorkspaceModel";
import type { AdminPermissionOverrideDraft } from "./AdminPermissionsSection";

// ─── Meta bundle: section selection, navigation, errors ──────────────────────

export type AdminConfigMetaBundle = {
  section: AdminWorkspaceSection;
  organizationEntity: AdminOrganizationEntity;
  selectedEntityId: number | null;
  warnings: AdminWorkspaceWarning[];
  onSelectSection: (section: AdminWorkspaceSection) => void;
  onOpenOrganization: (entity: AdminOrganizationEntity, id?: number | null) => void;
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
};

// ─── User bundle: user list + drafts + selection + CRUD ──────────────────────

export type AdminConfigUserBundle = {
  users: AdminUser[];
  sortedUsers: AdminUser[];
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
  onSelectUser: (user: AdminUser) => void;
  onCreateUser: () => void | Promise<void>;
  onSaveUserMasterData: () => void | Promise<void>;
  onRemoveUser: (user: AdminUser) => void | Promise<void>;
  onUserDisplayNameChange: (value: string) => void;
  onUserEmailChange: (value: string) => void;
  onUserNotificationEmailChange: (value: string) => void;
  onUserExternalKeyChange: (value: string) => void;
  onUserDepartmentIdChange: (value: string) => void;
  onUserIsActiveChange: (value: boolean) => void;
  onNewUserDisplayNameChange: (value: string) => void;
  onNewUserEmailChange: (value: string) => void;
  onNewUserNotificationEmailChange: (value: string) => void;
  onNewUserExternalKeyChange: (value: string) => void;
  onNewUserDepartmentIdChange: (value: string) => void;
  onNewUserIsActiveChange: (value: boolean) => void;
};

// ─── Organization bundle: departments, positions, responsibilities ───────────

export type AdminConfigOrganizationBundle = {
  departmentAssignments: AdminDepartmentAssignment[];
  departmentPositions: AdminRole[];
  responsibilityOwners: AdminResponsibilityOwner[];
  sortedDepartments: AdminDepartmentAssignment[];
  sortedDepartmentPositions: AdminRole[];
  sortedResponsibilities: AdminResponsibilityOwner[];
  newDepartmentNameDraft: string;
  newPositionNameDraft: string;
  newResponsibilityDraft: NewResponsibilityDraft;
  departmentDrafts: Record<number, DepartmentDraft>;
  positionDrafts: Record<number, PositionDraft>;
  responsibilityDrafts: Record<number, ResponsibilityDraft>;
  isCreatingDepartment: boolean;
  creatingPositionDepartmentId: number | null;
  isCreatingResponsibility: boolean;
  deletingDepartmentId: number | null;
  deletingPositionId: number | null;
  deletingResponsibilityId: number | null;
  savingDepartmentId: number | null;
  savingPositionId: number | null;
  savingResponsibilityId: number | null;
  onNewDepartmentNameChange: (value: string) => void;
  onNewPositionNameChange: (value: string) => void;
  onNewResponsibilityDraftChange: (draft: NewResponsibilityDraft) => void;
  onDepartmentDraftChange: (departmentId: number, draft: DepartmentDraft) => void;
  onPositionDraftChange: (positionId: number, draft: PositionDraft) => void;
  onResponsibilityDraftChange: (responsibilityId: number, draft: ResponsibilityDraft) => void;
  setDepartmentDrafts: Dispatch<SetStateAction<Record<number, DepartmentDraft>>>;
  setPositionDrafts: Dispatch<SetStateAction<Record<number, PositionDraft>>>;
  setResponsibilityDrafts: Dispatch<SetStateAction<Record<number, ResponsibilityDraft>>>;
  onCreateDepartment: () => void | Promise<void>;
  onCreateDepartmentPosition: (departmentId: number) => void | Promise<void>;
  onCreateResponsibility: () =>
    | void
    | Promise<AdminResponsibilityOwner | null>
    | AdminResponsibilityOwner
    | null;
  onSaveDepartmentAssignment: (departmentId: number) => void | Promise<void>;
  onRemoveDepartment: (department: AdminDepartmentAssignment) => void | Promise<void>;
  onSaveDepartmentPosition: (positionId: number) => void | Promise<void>;
  onRemoveDepartmentPosition: (position: AdminRole) => void | Promise<void>;
  onRemoveResponsibility: (
    responsibility: AdminResponsibilityOwner
  ) => void | Promise<boolean> | boolean;
  onSaveResponsibilityAssignment: (responsibilityId: number) => void | Promise<void>;
  onReloadOrganizationData: () => void | Promise<void>;
};

// ─── Access bundle: roles, groups, permissions ───────────────────────────────

export type AdminConfigAccessBundle = {
  sortedRoles: AdminRole[];
  groups: AdminGroup[];
  permissions: AdminPermission[];
  permissionAuditEntries: AdminPermissionAuditEntry[];
  hasMorePermissionAudit: boolean;
  isLoadingMorePermissionAudit: boolean;
  onLoadMorePermissionAudit: () => void | Promise<void>;
  selectedUserRoleIds: number[];
  selectedUserGroupIds: number[];
  selectedGroupId: number | null;
  selectedGroup: AdminGroup | null;
  selectedGroupRoleIds: number[];
  selectedRoleId: number | null;
  selectedRolePermissionIds: number[];
  userOverrideDrafts: AdminPermissionOverrideDraft[];
  hasLoadedTechnicalAccess: boolean;
  isLoadingTechnicalAccess: boolean;
  isSavingUserRoles: boolean;
  isSavingUserGroups: boolean;
  isSavingGroupRoles: boolean;
  isSavingRolePermissions: boolean;
  isSavingUserOverrides: boolean;
  onSelectRole: (roleId: number | null) => void;
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
};

// ─── Directory bundle: directory sync state + handlers ───────────────────────

export type AdminConfigDirectoryBundle = {
  directoryGroups: AdminDirectoryGroup[];
  directoryIdentities: AdminDirectoryIdentity[];
  directoryAuditEntries: AdminDirectoryMappingAuditEntry[];
  hasMoreDirectoryAudit: boolean;
  isLoadingMoreDirectoryAudit: boolean;
  onLoadMoreDirectoryAudit: () => void | Promise<void>;
  directoryStatus: AdminDirectorySyncStatus | null;
  directoryResponsibilityGaps: DirectoryResponsibilityGaps | null;
  directoryPendingImports: DirectoryPendingImports | null;
  isLoadingDirectory: boolean;
  isSyncingDirectory: boolean;
  isImportingDirectory: boolean;
  savingDirectoryGroupId: number | null;
  deletingDirectoryMappingId: number | null;
  onSyncDirectory: (groupPrefix: string | null) => void | Promise<void>;
  onImportDirectoryIdentities: (ids: number[]) => void | Promise<void>;
  onCreateDirectoryMapping: (
    directoryGroupId: number,
    appRoleId: number,
    scope: string,
    scopeDepartmentId: number | null
  ) => void | Promise<void>;
  onDeleteDirectoryMapping: (mappingId: number) => void | Promise<void>;
};

// ─── Notification bundle: email config + templates ───────────────────────────

export type AdminConfigNotificationBundle = {
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
  notificationTemplates: AdminNotificationTemplate[];
  selectedNotificationTemplate: AdminNotificationTemplate | null;
  selectedNotificationTemplateKey: string | null;
  selectedNotificationTemplateSubjectDraft: string;
  selectedNotificationTemplateBodyDraft: string;
  hasSelectedNotificationTemplateChanges: boolean;
  workflowPreviewSearch: string;
  rotationPlanPreviewSearch: string;
  workflowPreviewTargets: AdminNotificationTemplateWorkflowPreviewTarget[];
  rotationPlanPreviewTargets: AdminNotificationTemplateRotationPlanPreviewTarget[];
  selectedWorkflowPreviewUid: string | null;
  selectedRotationPlanPreviewId: number | null;
  notificationTemplatePreviewResponse: AdminNotificationTemplatePreviewResponse | null;
  selectedNotificationTemplatePreviewVariantIndex: number;
  isLoadingNotificationTemplates: boolean;
  isSavingNotificationTemplate: boolean;
  isLoadingNotificationTemplatePreviewTargets: boolean;
  isLoadingNotificationTemplatePreview: boolean;
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
  onSelectNotificationTemplate: (templateKey: string) => void;
  onSelectedNotificationTemplateSubjectChange: (value: string) => void;
  onSelectedNotificationTemplateBodyChange: (value: string) => void;
  onWorkflowPreviewSearchChange: (value: string) => void;
  onRotationPlanPreviewSearchChange: (value: string) => void;
  onSelectWorkflowPreviewTarget: (workflowUid: string | null) => void;
  onSelectRotationPlanPreviewTarget: (rotationPlanId: number | null) => void;
  onSaveSelectedNotificationTemplate: () => void | Promise<void>;
  onRenderNotificationTemplatePreview: () => void | Promise<void>;
  onSelectNotificationTemplatePreviewVariant: (index: number) => void;
};

// ─── System bundle: graph application + system-level config ──────────────────

export type AdminConfigSystemBundle = {
  graphApplicationConfiguration: AdminGraphApplicationConfiguration | null;
};

// ─── Composite props for the workspace content dispatcher ────────────────────

export type AdminConfigWorkspaceContentProps = {
  meta: AdminConfigMetaBundle;
  user: AdminConfigUserBundle;
  organization: AdminConfigOrganizationBundle;
  access: AdminConfigAccessBundle;
  directory: AdminConfigDirectoryBundle;
  notification: AdminConfigNotificationBundle;
  system: AdminConfigSystemBundle;
};
