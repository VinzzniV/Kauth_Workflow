import { useCallback, useEffect, useMemo } from "react";
import type { Dispatch, SetStateAction } from "react";
import type { SetURLSearchParams } from "react-router-dom";
import {
  buildAdminOverviewWarnings,
  type AdminOrganizationEntity,
  type AdminWorkspaceSection,
} from "../components/admin-config/adminWorkspaceModel";
import type { AdminConfigWorkspaceContentProps } from "../components/admin-config/adminConfigWorkspaceContentTypes";
import type { AdminGraphApplicationConfiguration, AdminUser } from "../types/auth";
import type { useAdminNotificationEmailConfiguration } from "./useAdminNotificationEmailConfiguration";
import type { useAdminNotificationTemplates } from "./useAdminNotificationTemplates";

type UseAdminConfigPageViewArgs = {
  searchParams: URLSearchParams;
  setSearchParams: SetURLSearchParams;
  section: AdminWorkspaceSection;
  organizationEntity: AdminOrganizationEntity;
  selectedEntityId: number | null;
  users: AdminConfigWorkspaceContentProps["users"];
  departmentPositions: AdminConfigWorkspaceContentProps["departmentPositions"];
  roles: AdminConfigWorkspaceContentProps["sortedRoles"];
  groups: AdminConfigWorkspaceContentProps["groups"];
  permissions: AdminConfigWorkspaceContentProps["permissions"];
  permissionAuditEntries: AdminConfigWorkspaceContentProps["permissionAuditEntries"];
  directoryGroups: AdminConfigWorkspaceContentProps["directoryGroups"];
  directoryIdentities: AdminConfigWorkspaceContentProps["directoryIdentities"];
  directoryAuditEntries: AdminConfigWorkspaceContentProps["directoryAuditEntries"];
  directoryStatus: AdminConfigWorkspaceContentProps["directoryStatus"];
  departmentAssignments: AdminConfigWorkspaceContentProps["departmentAssignments"];
  responsibilityOwners: AdminConfigWorkspaceContentProps["responsibilityOwners"];
  hasLoadedTechnicalAccess: boolean;
  isLoadingTechnicalAccess: boolean;
  isLoadingDirectory: boolean;
  isSyncingDirectory: boolean;
  savingDirectoryGroupId: number | null;
  deletingDirectoryMappingId: number | null;
  graphApplicationConfiguration: AdminGraphApplicationConfiguration | null;
  notificationConfig: ReturnType<typeof useAdminNotificationEmailConfiguration>;
  notificationTemplateConfig: ReturnType<typeof useAdminNotificationTemplates>;
  selectedUserId: number | null;
  selectedUser: AdminConfigWorkspaceContentProps["selectedUser"];
  selectedGroupId: AdminConfigWorkspaceContentProps["selectedGroupId"];
  selectedGroup: AdminConfigWorkspaceContentProps["selectedGroup"];
  selectedUserRoleIds: AdminConfigWorkspaceContentProps["selectedUserRoleIds"];
  selectedUserGroupIds: AdminConfigWorkspaceContentProps["selectedUserGroupIds"];
  selectedGroupRoleIds: AdminConfigWorkspaceContentProps["selectedGroupRoleIds"];
  selectedRoleId: AdminConfigWorkspaceContentProps["selectedRoleId"];
  selectedRolePermissionIds: AdminConfigWorkspaceContentProps["selectedRolePermissionIds"];
  userOverrideDrafts: AdminConfigWorkspaceContentProps["userOverrideDrafts"];
  userFormError: string | null;
  userFormNotice: string | null;
  userExternalKeyDraft: string;
  userDisplayNameDraft: string;
  userEmailDraft: string;
  userNotificationEmailDraft: string;
  userDepartmentIdDraft: string;
  userIsActiveDraft: boolean;
  newUserExternalKeyDraft: string;
  newUserDisplayNameDraft: string;
  newUserEmailDraft: string;
  newUserNotificationEmailDraft: string;
  newUserDepartmentIdDraft: string;
  newUserIsActiveDraft: boolean;
  isSavingUserMasterData: boolean;
  isSavingUserRoles: boolean;
  isSavingUserGroups: boolean;
  isSavingGroupRoles: boolean;
  isCreatingUser: boolean;
  deletingUserId: number | null;
  newDepartmentNameDraft: string;
  newPositionNameDraft: string;
  newResponsibilityDraft: AdminConfigWorkspaceContentProps["newResponsibilityDraft"];
  departmentDrafts: AdminConfigWorkspaceContentProps["departmentDrafts"];
  positionDrafts: AdminConfigWorkspaceContentProps["positionDrafts"];
  responsibilityDrafts: AdminConfigWorkspaceContentProps["responsibilityDrafts"];
  isCreatingDepartment: boolean;
  creatingPositionDepartmentId: number | null;
  isCreatingResponsibility: boolean;
  deletingDepartmentId: number | null;
  deletingPositionId: number | null;
  deletingResponsibilityId: number | null;
  savingDepartmentId: number | null;
  savingPositionId: number | null;
  savingResponsibilityId: number | null;
  isSavingRolePermissions: boolean;
  isSavingUserOverrides: boolean;
  onSelectUser: (user: AdminUser) => void;
  onSelectGroup: AdminConfigWorkspaceContentProps["onSelectGroup"];
  onSelectRole: AdminConfigWorkspaceContentProps["onSelectRole"];
  onToggleUserRole: AdminConfigWorkspaceContentProps["onToggleUserRole"];
  onToggleUserGroup: AdminConfigWorkspaceContentProps["onToggleUserGroup"];
  onToggleGroupRole: AdminConfigWorkspaceContentProps["onToggleGroupRole"];
  onSaveUserMasterData: AdminConfigWorkspaceContentProps["onSaveUserMasterData"];
  onCreateUser: AdminConfigWorkspaceContentProps["onCreateUser"];
  onRemoveUser: AdminConfigWorkspaceContentProps["onRemoveUser"];
  onSaveUserRoles: AdminConfigWorkspaceContentProps["onSaveUserRoles"];
  onSaveUserGroups: AdminConfigWorkspaceContentProps["onSaveUserGroups"];
  onSaveGroupRoles: AdminConfigWorkspaceContentProps["onSaveGroupRoles"];
  onSetUserExternalKeyDraft: (value: string) => void;
  onSetUserDisplayNameDraft: (value: string) => void;
  onSetUserEmailDraft: (value: string) => void;
  onSetUserNotificationEmailDraft: (value: string) => void;
  onSetUserDepartmentIdDraft: (value: string) => void;
  onSetUserIsActiveDraft: (value: boolean) => void;
  onSetNewUserExternalKeyDraft: (value: string) => void;
  onSetNewUserDisplayNameDraft: (value: string) => void;
  onSetNewUserEmailDraft: (value: string) => void;
  onSetNewUserNotificationEmailDraft: (value: string) => void;
  onSetNewUserDepartmentIdDraft: (value: string) => void;
  onSetNewUserIsActiveDraft: (value: boolean) => void;
  onSetNewDepartmentNameDraft: (value: string) => void;
  onSetNewPositionNameDraft: (value: string) => void;
  onSetNewResponsibilityDraft: (draft: AdminConfigWorkspaceContentProps["newResponsibilityDraft"]) => void;
  setDepartmentDrafts: Dispatch<SetStateAction<AdminConfigWorkspaceContentProps["departmentDrafts"]>>;
  setPositionDrafts: Dispatch<SetStateAction<AdminConfigWorkspaceContentProps["positionDrafts"]>>;
  setResponsibilityDrafts: Dispatch<SetStateAction<AdminConfigWorkspaceContentProps["responsibilityDrafts"]>>;
  onCreateDepartment: AdminConfigWorkspaceContentProps["onCreateDepartment"];
  onCreateDepartmentPosition: AdminConfigWorkspaceContentProps["onCreateDepartmentPosition"];
  onCreateResponsibility: AdminConfigWorkspaceContentProps["onCreateResponsibility"];
  onSaveDepartmentAssignment: AdminConfigWorkspaceContentProps["onSaveDepartmentAssignment"];
  onRemoveDepartment: AdminConfigWorkspaceContentProps["onRemoveDepartment"];
  onSaveDepartmentPosition: AdminConfigWorkspaceContentProps["onSaveDepartmentPosition"];
  onRemoveDepartmentPosition: AdminConfigWorkspaceContentProps["onRemoveDepartmentPosition"];
  onRemoveResponsibility: AdminConfigWorkspaceContentProps["onRemoveResponsibility"];
  onSaveResponsibilityAssignment: AdminConfigWorkspaceContentProps["onSaveResponsibilityAssignment"];
  onToggleRolePermission: AdminConfigWorkspaceContentProps["onToggleRolePermission"];
  onSaveRolePermissions: AdminConfigWorkspaceContentProps["onSaveRolePermissions"];
  onUserOverrideDraftsChange: (drafts: AdminConfigWorkspaceContentProps["userOverrideDrafts"]) => void;
  onSaveUserOverrides: AdminConfigWorkspaceContentProps["onSaveUserOverrides"];
  onSyncDirectory: AdminConfigWorkspaceContentProps["onSyncDirectory"];
  onCreateDirectoryMapping: AdminConfigWorkspaceContentProps["onCreateDirectoryMapping"];
  onDeleteDirectoryMapping: AdminConfigWorkspaceContentProps["onDeleteDirectoryMapping"];
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
};

export function useAdminConfigPageView(args: UseAdminConfigPageViewArgs) {
  const {
    searchParams,
    setSearchParams,
    section,
    organizationEntity,
    selectedEntityId,
    users,
    departmentPositions,
    roles,
    groups,
    permissions,
    permissionAuditEntries,
    directoryGroups,
    directoryIdentities,
    directoryAuditEntries,
    directoryStatus,
    departmentAssignments,
    responsibilityOwners,
    hasLoadedTechnicalAccess,
    isLoadingTechnicalAccess,
    isLoadingDirectory,
    isSyncingDirectory,
    savingDirectoryGroupId,
    deletingDirectoryMappingId,
    graphApplicationConfiguration,
    notificationConfig,
    notificationTemplateConfig,
    selectedUserId,
    selectedUser,
    selectedGroupId,
    selectedGroup,
    selectedUserRoleIds,
    selectedUserGroupIds,
    selectedGroupRoleIds,
    selectedRoleId,
    selectedRolePermissionIds,
    userOverrideDrafts,
    userFormError,
    userFormNotice,
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
    newDepartmentNameDraft,
    newPositionNameDraft,
    newResponsibilityDraft,
    departmentDrafts,
    positionDrafts,
    responsibilityDrafts,
    isCreatingDepartment,
    creatingPositionDepartmentId,
    isCreatingResponsibility,
    deletingDepartmentId,
    deletingPositionId,
    deletingResponsibilityId,
    savingDepartmentId,
    savingPositionId,
    savingResponsibilityId,
    isSavingRolePermissions,
    isSavingUserOverrides,
    onSelectUser,
    onSelectGroup,
    onSelectRole,
    onToggleUserRole,
    onToggleUserGroup,
    onToggleGroupRole,
    onSaveUserMasterData,
    onCreateUser,
    onRemoveUser,
    onSaveUserRoles,
    onSaveUserGroups,
    onSaveGroupRoles,
    onSetUserExternalKeyDraft,
    onSetUserDisplayNameDraft,
    onSetUserEmailDraft,
    onSetUserNotificationEmailDraft,
    onSetUserDepartmentIdDraft,
    onSetUserIsActiveDraft,
    onSetNewUserExternalKeyDraft,
    onSetNewUserDisplayNameDraft,
    onSetNewUserEmailDraft,
    onSetNewUserNotificationEmailDraft,
    onSetNewUserDepartmentIdDraft,
    onSetNewUserIsActiveDraft,
    setDepartmentDrafts,
    setPositionDrafts,
    setResponsibilityDrafts,
    onSetNewDepartmentNameDraft,
    onSetNewPositionNameDraft,
    onSetNewResponsibilityDraft,
    onCreateDepartment,
    onCreateDepartmentPosition,
    onCreateResponsibility,
    onSaveDepartmentAssignment,
    onRemoveDepartment,
    onSaveDepartmentPosition,
    onRemoveDepartmentPosition,
    onRemoveResponsibility,
    onSaveResponsibilityAssignment,
    onToggleRolePermission,
    onSaveRolePermissions,
    onUserOverrideDraftsChange,
    onSaveUserOverrides,
    onSyncDirectory,
    onCreateDirectoryMapping,
    onDeleteDirectoryMapping,
    onNotice,
    onError,
  } = args;

  const sortedUsers = useMemo(
    () => users.slice().sort((left, right) => left.displayName.localeCompare(right.displayName, "de")),
    [users]
  );
  const sortedDepartmentPositions = useMemo(
    () =>
      departmentPositions.slice().sort((left, right) => {
        const leftKey = `${left.departmentName ?? ""}|${left.roleName}`;
        const rightKey = `${right.departmentName ?? ""}|${right.roleName}`;
        return leftKey.localeCompare(rightKey, "de");
      }),
    [departmentPositions]
  );
  const eligibleSupervisorUsers = useMemo(
    () =>
      sortedUsers.filter(
        (user) => user.isActive && Boolean(user.canAccessSupervisorStep ?? user.hasManagerAccess)
      ),
    [sortedUsers]
  );
  const eligibleRequirementOwnerUsers = useMemo(
    () => sortedUsers.filter((user) => user.isActive),
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
        eligibleRequirementOwnerUsers,
        notificationEmailConfiguration: notificationConfig.notificationEmailConfiguration,
      }),
    [
      eligibleRequirementOwnerUsers,
      eligibleSupervisorUsers,
      notificationConfig.notificationEmailConfiguration,
      sortedDepartments,
      sortedResponsibilities,
    ]
  );

  useEffect(() => {
    if (organizationEntity !== "user" || !selectedEntityId) {
      return;
    }

    const matchingUser = users.find((user) => user.userId === selectedEntityId);
    if (!matchingUser || selectedUserId === matchingUser.userId) {
      return;
    }

    onSelectUser(matchingUser);
  }, [onSelectUser, organizationEntity, selectedEntityId, selectedUserId, users]);

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
    users.length > 0 ||
    departmentAssignments.length > 0 ||
    responsibilityOwners.length > 0 ||
    permissions.length > 0 ||
    directoryGroups.length > 0 ||
    directoryIdentities.length > 0;

  const workspaceContentProps: AdminConfigWorkspaceContentProps = {
    section,
    organizationEntity,
    selectedEntityId,
    users,
    departmentPositions,
    departmentAssignments,
    responsibilityOwners,
    sortedUsers,
    sortedDepartmentPositions,
    sortedDepartments,
    sortedResponsibilities,
    eligibleSupervisorUsers,
    eligibleRequirementOwnerUsers,
    selectedUser,
    workspaceSelectedUser,
    userDisplayNameDraft,
    userEmailDraft,
    userNotificationEmailDraft,
    userExternalKeyDraft,
    userDepartmentIdDraft,
    userIsActiveDraft,
    userFormError,
    userFormNotice,
    newUserDisplayNameDraft,
    newUserEmailDraft,
    newUserNotificationEmailDraft,
    newUserExternalKeyDraft,
    newUserDepartmentIdDraft,
    newUserIsActiveDraft,
    isCreatingUser,
    isSavingUserMasterData,
    deletingUserId,
    newDepartmentNameDraft,
    newPositionNameDraft,
    newResponsibilityDraft,
    departmentDrafts,
    positionDrafts,
    responsibilityDrafts,
    isCreatingDepartment,
    creatingPositionDepartmentId,
    isCreatingResponsibility,
    deletingDepartmentId,
    deletingPositionId,
    deletingResponsibilityId,
    savingDepartmentId,
    savingPositionId,
    savingResponsibilityId,
    hasLoadedTechnicalAccess,
    isLoadingTechnicalAccess,
    isLoadingDirectory,
    isSyncingDirectory,
    savingDirectoryGroupId,
    deletingDirectoryMappingId,
    selectedUserRoleIds,
    selectedUserGroupIds,
    selectedGroupId,
    selectedGroup,
    selectedGroupRoleIds,
    sortedRoles,
    permissions,
    permissionAuditEntries,
    selectedRoleId,
    selectedRolePermissionIds,
    userOverrideDrafts,
    groups,
    directoryGroups,
    directoryIdentities,
    directoryAuditEntries,
    directoryStatus,
    graphApplicationConfiguration,
    notificationEmailConfiguration: notificationConfig.notificationEmailConfiguration,
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
    warnings,
    isSavingUserRoles,
    isSavingUserGroups,
    isSavingGroupRoles,
    isSavingRolePermissions,
    isSavingUserOverrides,
    onOpenOrganization: handleOpenOrganization,
    onSelectSection: handleSelectSection,
    onSelectUser,
    onSelectRole,
    onNewUserDisplayNameChange: onSetNewUserDisplayNameDraft,
    onNewUserEmailChange: onSetNewUserEmailDraft,
    onNewUserNotificationEmailChange: onSetNewUserNotificationEmailDraft,
    onNewUserExternalKeyChange: onSetNewUserExternalKeyDraft,
    onNewUserDepartmentIdChange: onSetNewUserDepartmentIdDraft,
    onNewUserIsActiveChange: onSetNewUserIsActiveDraft,
    onUserDisplayNameChange: onSetUserDisplayNameDraft,
    onUserEmailChange: onSetUserEmailDraft,
    onUserNotificationEmailChange: onSetUserNotificationEmailDraft,
    onUserExternalKeyChange: onSetUserExternalKeyDraft,
    onUserDepartmentIdChange: onSetUserDepartmentIdDraft,
    onUserIsActiveChange: onSetUserIsActiveDraft,
    onCreateUser,
    onSaveUserMasterData,
    onRemoveUser,
    onNewDepartmentNameChange: onSetNewDepartmentNameDraft,
    onNewResponsibilityDraftChange: onSetNewResponsibilityDraft,
    onDepartmentDraftChange: (departmentId, draft) =>
      setDepartmentDrafts((current) => ({ ...current, [departmentId]: draft })),
    onCreateDepartment,
    onCreateResponsibility,
    onSaveDepartmentAssignment,
    onRemoveDepartment,
    onNewPositionNameChange: onSetNewPositionNameDraft,
    onCreateDepartmentPosition,
    onPositionDraftChange: (positionId, draft) =>
      setPositionDrafts((current) => ({ ...current, [positionId]: draft })),
    onSaveDepartmentPosition,
    onRemoveDepartmentPosition,
    onResponsibilityDraftChange: (responsibilityId, draft) =>
      setResponsibilityDrafts((current) => ({ ...current, [responsibilityId]: draft })),
    onRemoveResponsibility,
    onSaveResponsibilityAssignment,
    onToggleUserRole,
    onToggleUserGroup,
    onSelectGroup,
    onToggleGroupRole,
    onSaveUserRoles,
    onSaveUserGroups,
    onSaveGroupRoles,
    onToggleRolePermission,
    onSaveRolePermissions,
    onUserOverrideDraftsChange,
    onSaveUserOverrides,
    onSyncDirectory,
    onCreateDirectoryMapping,
    onDeleteDirectoryMapping,
    onNotice,
    onError,
    onNotificationEnabledChange: notificationConfig.setNotificationEnabledDraft,
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
    onNotificationSenderEmailChange: notificationConfig.setNotificationSenderEmailDraft,
    onNotificationFrontendBaseUrlChange: notificationConfig.setNotificationFrontendBaseUrlDraft,
    onNotificationTestRecipientChange: notificationConfig.setNotificationTestRecipientDraft,
    onNotificationSandboxRedirectChange: notificationConfig.setNotificationSandboxRedirectDraft,
    onNotificationNotifyOnWorkflowCreatedChange: notificationConfig.setNotificationNotifyOnWorkflowCreatedDraft,
    onNotificationNotifyOnTaskReadyChange: notificationConfig.setNotificationNotifyOnTaskReadyDraft,
    onNotificationNotifyOnWorkflowCompletedChange: notificationConfig.setNotificationNotifyOnWorkflowCompletedDraft,
    onSaveNotificationEmailConfiguration: notificationConfig.saveNotificationEmailConfiguration,
    onSendNotificationEmailTest: notificationConfig.sendNotificationEmailTest,
  };

  return {
    hasAnyData,
    handleSelectSection,
    workspaceContentProps,
  };
}
