import AdminOrganizationRelationsPanel from "./AdminOrganizationRelationsPanel";
import { AdminOrganizationDepartmentEditor } from "./AdminOrganizationDepartmentEditor";
import { AdminOrganizationSidebar } from "./AdminOrganizationSidebar";
import { AdminOrganizationUserEditor } from "./AdminOrganizationUserEditor";
import type {
  AdminDepartmentAssignment,
  AdminRole,
  AdminUser,
} from "../../types/auth";
import { type AdminOrganizationEntity } from "./adminWorkspaceModel";
import type { DepartmentDraft, PositionDraft } from "./adminOrganizationTypes";
import { useAdminOrganizationWorkspaceView } from "./useAdminOrganizationWorkspaceView";

type AdminOrganizationWorkspaceSectionProps = {
  organizationEntity: AdminOrganizationEntity;
  selectedEntityId: number | null;
  sortedUsers: AdminUser[];
  sortedDepartmentPositions: AdminRole[];
  sortedDepartments: AdminDepartmentAssignment[];
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
  departmentDrafts: Record<number, DepartmentDraft>;
  positionDrafts: Record<number, PositionDraft>;
  isCreatingDepartment: boolean;
  creatingPositionDepartmentId: number | null;
  deletingDepartmentId: number | null;
  deletingPositionId: number | null;
  savingDepartmentId: number | null;
  savingPositionId: number | null;
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
  onDepartmentDraftChange: (departmentId: number, draft: DepartmentDraft) => void;
  onCreateDepartment: () => void | Promise<void>;
  onCreateDepartmentPosition: (departmentId: number) => void | Promise<void>;
  onSaveDepartmentAssignment: (departmentId: number) => void | Promise<void>;
  onRemoveDepartment: (department: AdminDepartmentAssignment) => void | Promise<void>;
  onPositionDraftChange: (positionId: number, draft: PositionDraft) => void;
  onSaveDepartmentPosition: (positionId: number) => void | Promise<void>;
  onRemoveDepartmentPosition: (position: AdminRole) => void | Promise<void>;
};

export function AdminOrganizationWorkspaceSection({
  organizationEntity,
  selectedEntityId,
  sortedUsers,
  sortedDepartmentPositions,
  sortedDepartments,
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
  departmentDrafts,
  positionDrafts,
  isCreatingDepartment,
  creatingPositionDepartmentId,
  deletingDepartmentId,
  deletingPositionId,
  savingDepartmentId,
  savingPositionId,
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
  onDepartmentDraftChange,
  onCreateDepartment,
  onCreateDepartmentPosition,
  onSaveDepartmentAssignment,
  onRemoveDepartment,
  onPositionDraftChange,
  onSaveDepartmentPosition,
  onRemoveDepartmentPosition,
}: AdminOrganizationWorkspaceSectionProps) {
  const {
    selectedDepartment,
    userRelations,
    departmentRelations,
    canCreateUser,
    canSaveUser,
    selectedDepartmentDraft,
    selectedDepartmentPositions,
    selectedDepartmentLeadOptions,
    selectedDepartmentOwnerOptions,
    canSaveDepartment,
  } = useAdminOrganizationWorkspaceView({
    organizationEntity,
    selectedEntityId,
    sortedUsers,
    sortedDepartmentPositions,
    sortedDepartments,
    sortedResponsibilities: [],
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
    responsibilityDrafts: {},
    savingDepartmentId,
    savingResponsibilityId: null,
  });

  return (
    <div className="content-stack">
      <div className="master-detail-layout">
          <AdminOrganizationSidebar
            organizationEntity={organizationEntity}
            selectedEntityId={selectedEntityId}
            sortedUsers={sortedUsers}
            sortedDepartments={sortedDepartments}
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

          <AdminOrganizationRelationsPanel
            organizationEntity={organizationEntity}
            selectedUser={selectedUser}
            userRelations={userRelations}
            selectedDepartment={selectedDepartment}
            departmentRelations={departmentRelations}
            selectedResponsibility={null}
            responsibilityRelations={null}
            onSelectOrganizationEntity={onSelectOrganizationEntity}
          />
        </div>
      </div>
    </div>
  );
}
