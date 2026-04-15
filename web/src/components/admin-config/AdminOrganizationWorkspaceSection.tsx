import AdminOrganizationRelationsPanel from "./AdminOrganizationRelationsPanel";
import { AdminOrganizationDepartmentEditor } from "./AdminOrganizationDepartmentEditor";
import { AdminOrganizationResponsibilityEditor } from "./AdminOrganizationResponsibilityEditor";
import { AdminOrganizationSidebar } from "./AdminOrganizationSidebar";
import { AdminOrganizationUserEditor } from "./AdminOrganizationUserEditor";
import type {
  AdminDepartmentAssignment,
  AdminResponsibilityOwner,
  AdminRole,
  AdminUser,
} from "../../types/auth";
import { type AdminOrganizationEntity } from "./adminWorkspaceModel";
import type { DepartmentDraft, NewResponsibilityDraft, PositionDraft, ResponsibilityDraft } from "./adminOrganizationTypes";
import { useAdminOrganizationWorkspaceView } from "./useAdminOrganizationWorkspaceView";

type AdminOrganizationWorkspaceSectionProps = {
  organizationEntity: AdminOrganizationEntity;
  selectedEntityId: number | null;
  sortedUsers: AdminUser[];
  sortedDepartmentPositions: AdminRole[];
  sortedDepartments: AdminDepartmentAssignment[];
  sortedResponsibilities: AdminResponsibilityOwner[];
  eligibleSupervisorUsers: AdminUser[];
  eligibleRequirementOwnerUsers: AdminUser[];
  selectedUser: AdminUser | null;
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
  onSelectOrganizationEntity: (entity: AdminOrganizationEntity, id?: number | null) => void;
  onSelectUser: (user: AdminUser) => void;
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
  onNewPositionNameChange: (value: string) => void;
  onNewResponsibilityDraftChange: (draft: NewResponsibilityDraft) => void;
  onDepartmentDraftChange: (departmentId: number, draft: DepartmentDraft) => void;
  onCreateDepartment: () => void | Promise<void>;
  onCreateDepartmentPosition: (departmentId: number) => void | Promise<void>;
  onCreateResponsibility: () => void | Promise<AdminResponsibilityOwner | null> | AdminResponsibilityOwner | null;
  onSaveDepartmentAssignment: (departmentId: number) => void | Promise<void>;
  onRemoveDepartment: (department: AdminDepartmentAssignment) => void | Promise<void>;
  onPositionDraftChange: (positionId: number, draft: PositionDraft) => void;
  onSaveDepartmentPosition: (positionId: number) => void | Promise<void>;
  onRemoveDepartmentPosition: (position: AdminRole) => void | Promise<void>;
  onResponsibilityDraftChange: (responsibilityId: number, draft: ResponsibilityDraft) => void;
  onRemoveResponsibility: (responsibility: AdminResponsibilityOwner) => void | Promise<boolean> | boolean;
  onSaveResponsibilityAssignment: (responsibilityId: number) => void | Promise<void>;
};

export function AdminOrganizationWorkspaceSection({
  organizationEntity,
  selectedEntityId,
  sortedUsers,
  sortedDepartmentPositions,
  sortedDepartments,
  sortedResponsibilities,
  eligibleSupervisorUsers,
  eligibleRequirementOwnerUsers,
  selectedUser,
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
  onSelectOrganizationEntity,
  onSelectUser,
  onNewUserDisplayNameChange,
  onNewUserEmailChange,
  onNewUserNotificationEmailChange,
  onNewUserExternalKeyChange,
  onNewUserDepartmentIdChange,
  onNewUserIsActiveChange,
  onUserDisplayNameChange,
  onUserEmailChange,
  onUserNotificationEmailChange,
  onUserExternalKeyChange,
  onUserDepartmentIdChange,
  onUserIsActiveChange,
  onCreateUser,
  onSaveUserMasterData,
  onRemoveUser,
  onNewDepartmentNameChange,
  onNewPositionNameChange,
  onNewResponsibilityDraftChange,
  onDepartmentDraftChange,
  onCreateDepartment,
  onCreateDepartmentPosition,
  onCreateResponsibility,
  onSaveDepartmentAssignment,
  onRemoveDepartment,
  onPositionDraftChange,
  onSaveDepartmentPosition,
  onRemoveDepartmentPosition,
  onResponsibilityDraftChange,
  onRemoveResponsibility,
  onSaveResponsibilityAssignment,
}: AdminOrganizationWorkspaceSectionProps) {
  const {
    selectedDepartment,
    selectedResponsibility,
    userRelations,
    departmentRelations,
    responsibilityRelations,
    canCreateUser,
    canSaveUser,
    selectedDepartmentDraft,
    selectedDepartmentPositions,
    selectedDepartmentLeadOptions,
    selectedDepartmentOwnerOptions,
    canSaveDepartment,
    selectedResponsibilityDraft,
    canSaveResponsibility,
  } = useAdminOrganizationWorkspaceView({
    organizationEntity,
    selectedEntityId,
    sortedUsers,
    sortedDepartmentPositions,
    sortedDepartments,
    sortedResponsibilities,
    eligibleSupervisorUsers,
    eligibleRequirementOwnerUsers,
    selectedUser,
    userDisplayNameDraft,
    userEmailDraft,
    userNotificationEmailDraft,
    userExternalKeyDraft,
    userDepartmentIdDraft,
    userIsActiveDraft,
    newUserDisplayNameDraft,
    newUserEmailDraft,
    isCreatingUser,
    isSavingUserMasterData,
    departmentDrafts,
    responsibilityDrafts,
    savingDepartmentId,
    savingResponsibilityId,
  });

  return (
    <div className="content-stack">
      <div className="master-detail-layout">
        <AdminOrganizationSidebar
          organizationEntity={organizationEntity}
          selectedEntityId={selectedEntityId}
          sortedUsers={sortedUsers}
          sortedDepartments={sortedDepartments}
          sortedResponsibilities={sortedResponsibilities}
          eligibleSupervisorUsers={eligibleSupervisorUsers}
          eligibleRequirementOwnerUsers={eligibleRequirementOwnerUsers}
          onSelectOrganizationEntity={onSelectOrganizationEntity}
          onSelectUser={onSelectUser}
        />

        <div className="content-stack admin-organization-main master-detail-main">
          {organizationEntity === "user" ? (
            <AdminOrganizationUserEditor
              sortedDepartments={sortedDepartments}
              selectedUser={selectedUser}
              newUserDisplayNameDraft={newUserDisplayNameDraft}
              newUserEmailDraft={newUserEmailDraft}
              newUserNotificationEmailDraft={newUserNotificationEmailDraft}
              newUserExternalKeyDraft={newUserExternalKeyDraft}
              newUserDepartmentIdDraft={newUserDepartmentIdDraft}
              newUserIsActiveDraft={newUserIsActiveDraft}
              isCreatingUser={isCreatingUser}
              userDisplayNameDraft={userDisplayNameDraft}
              userEmailDraft={userEmailDraft}
              userNotificationEmailDraft={userNotificationEmailDraft}
              userExternalKeyDraft={userExternalKeyDraft}
              userDepartmentIdDraft={userDepartmentIdDraft}
              userIsActiveDraft={userIsActiveDraft}
              userFormError={userFormError}
              userFormNotice={userFormNotice}
              isSavingUserMasterData={isSavingUserMasterData}
              deletingUserId={deletingUserId}
              canCreateUser={canCreateUser}
              canSaveUser={canSaveUser}
              onNewUserDisplayNameChange={onNewUserDisplayNameChange}
              onNewUserEmailChange={onNewUserEmailChange}
              onNewUserNotificationEmailChange={onNewUserNotificationEmailChange}
              onNewUserExternalKeyChange={onNewUserExternalKeyChange}
              onNewUserDepartmentIdChange={onNewUserDepartmentIdChange}
              onNewUserIsActiveChange={onNewUserIsActiveChange}
              onUserDisplayNameChange={onUserDisplayNameChange}
              onUserEmailChange={onUserEmailChange}
              onUserNotificationEmailChange={onUserNotificationEmailChange}
              onUserExternalKeyChange={onUserExternalKeyChange}
              onUserDepartmentIdChange={onUserDepartmentIdChange}
              onUserIsActiveChange={onUserIsActiveChange}
              onCreateUser={onCreateUser}
              onSaveUserMasterData={onSaveUserMasterData}
              onRemoveUser={onRemoveUser}
            />
          ) : null}

          {organizationEntity === "department" ? (
            <AdminOrganizationDepartmentEditor
              selectedDepartment={selectedDepartment}
              selectedDepartmentDraft={selectedDepartmentDraft}
              selectedDepartmentPositions={selectedDepartmentPositions}
              positionDrafts={positionDrafts}
              selectedDepartmentLeadOptions={selectedDepartmentLeadOptions}
              selectedDepartmentOwnerOptions={selectedDepartmentOwnerOptions}
              eligibleSupervisorUsers={eligibleSupervisorUsers}
              eligibleRequirementOwnerUsers={eligibleRequirementOwnerUsers}
              newDepartmentNameDraft={newDepartmentNameDraft}
              newPositionNameDraft={newPositionNameDraft}
              isCreatingDepartment={isCreatingDepartment}
              creatingPositionDepartmentId={creatingPositionDepartmentId}
              deletingDepartmentId={deletingDepartmentId}
              deletingPositionId={deletingPositionId}
              savingDepartmentId={savingDepartmentId}
              savingPositionId={savingPositionId}
              canSaveDepartment={canSaveDepartment}
              onNewDepartmentNameChange={onNewDepartmentNameChange}
              onNewPositionNameChange={onNewPositionNameChange}
              onDepartmentDraftChange={onDepartmentDraftChange}
              onCreateDepartment={onCreateDepartment}
              onCreateDepartmentPosition={onCreateDepartmentPosition}
              onSaveDepartmentAssignment={onSaveDepartmentAssignment}
              onRemoveDepartment={onRemoveDepartment}
              onPositionDraftChange={onPositionDraftChange}
              onSaveDepartmentPosition={onSaveDepartmentPosition}
              onRemoveDepartmentPosition={onRemoveDepartmentPosition}
            />
          ) : null}

          {organizationEntity === "responsibility" ? (
            <AdminOrganizationResponsibilityEditor
              selectedResponsibility={selectedResponsibility}
              selectedResponsibilityDraft={selectedResponsibilityDraft}
              sortedDepartments={sortedDepartments}
              sortedUsers={sortedUsers}
              newResponsibilityDraft={newResponsibilityDraft}
              isCreatingResponsibility={isCreatingResponsibility}
              deletingResponsibilityId={deletingResponsibilityId}
              savingResponsibilityId={savingResponsibilityId}
              canSaveResponsibility={canSaveResponsibility}
              onNewResponsibilityDraftChange={onNewResponsibilityDraftChange}
              onCreateResponsibility={onCreateResponsibility}
              onResponsibilityDraftChange={onResponsibilityDraftChange}
              onRemoveResponsibility={onRemoveResponsibility}
              onSaveResponsibilityAssignment={onSaveResponsibilityAssignment}
            />
          ) : null}

          <AdminOrganizationRelationsPanel
            organizationEntity={organizationEntity}
            selectedUser={selectedUser}
            userRelations={userRelations}
            selectedDepartment={selectedDepartment}
            departmentRelations={departmentRelations}
            selectedResponsibility={selectedResponsibility}
            responsibilityRelations={responsibilityRelations}
            onSelectOrganizationEntity={onSelectOrganizationEntity}
          />
        </div>
      </div>
    </div>
  );
}
