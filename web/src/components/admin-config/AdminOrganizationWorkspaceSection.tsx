import { useMemo } from "react";
import AdminOrganizationRelationsPanel from "./AdminOrganizationRelationsPanel";
import { AdminOrganizationDepartmentEditor } from "./AdminOrganizationDepartmentEditor";
import { AdminOrganizationResponsibilityEditor } from "./AdminOrganizationResponsibilityEditor";
import { AdminOrganizationSidebar } from "./AdminOrganizationSidebar";
import { AdminOrganizationUserEditor } from "./AdminOrganizationUserEditor";
import type {
  AdminDepartmentAssignment,
  AdminResponsibilityOwner,
  AdminUser,
} from "../../types/auth";
import {
  getDepartmentRelations,
  getResponsibilityRelations,
  getUserRelations,
  hasDepartmentAssignmentChanges,
  hasResponsibilityAssignmentChanges,
  hasUserMasterDataChanges,
  type AdminOrganizationEntity,
} from "./adminWorkspaceModel";
import type { DepartmentDraft, ResponsibilityDraft } from "./adminOrganizationTypes";

type AdminOrganizationWorkspaceSectionProps = {
  organizationEntity: AdminOrganizationEntity;
  selectedEntityId: number | null;
  sortedUsers: AdminUser[];
  sortedDepartments: AdminDepartmentAssignment[];
  sortedResponsibilities: AdminResponsibilityOwner[];
  eligibleSupervisorUsers: AdminUser[];
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
  departmentDrafts: Record<number, DepartmentDraft>;
  responsibilityDrafts: Record<number, ResponsibilityDraft>;
  isCreatingDepartment: boolean;
  deletingDepartmentId: number | null;
  savingDepartmentId: number | null;
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
  onDepartmentDraftChange: (departmentId: number, draft: DepartmentDraft) => void;
  onCreateDepartment: () => void | Promise<void>;
  onSaveDepartmentAssignment: (departmentId: number) => void | Promise<void>;
  onRemoveDepartment: (department: AdminDepartmentAssignment) => void | Promise<void>;
  onResponsibilityDraftChange: (responsibilityId: number, draft: ResponsibilityDraft) => void;
  onSaveResponsibilityAssignment: (responsibilityId: number) => void | Promise<void>;
};

function buildSupervisorOptions(
  selectedUserId: string,
  sortedUsers: AdminUser[],
  eligibleSupervisorUsers: AdminUser[]
): AdminUser[] {
  if (!selectedUserId) {
    return eligibleSupervisorUsers;
  }

  const selectedUser = sortedUsers.find((user) => String(user.userId) === selectedUserId);
  if (!selectedUser) {
    return eligibleSupervisorUsers;
  }

  if (eligibleSupervisorUsers.some((user) => user.userId === selectedUser.userId)) {
    return eligibleSupervisorUsers;
  }

  return [...eligibleSupervisorUsers, selectedUser];
}

export function AdminOrganizationWorkspaceSection({
  organizationEntity,
  selectedEntityId,
  sortedUsers,
  sortedDepartments,
  sortedResponsibilities,
  eligibleSupervisorUsers,
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
  departmentDrafts,
  responsibilityDrafts,
  isCreatingDepartment,
  deletingDepartmentId,
  savingDepartmentId,
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
  onDepartmentDraftChange,
  onCreateDepartment,
  onSaveDepartmentAssignment,
  onRemoveDepartment,
  onResponsibilityDraftChange,
  onSaveResponsibilityAssignment,
}: AdminOrganizationWorkspaceSectionProps) {
  const selectedDepartment = useMemo(
    () =>
      organizationEntity === "department"
        ? sortedDepartments.find((department) => department.departmentId === selectedEntityId) ?? null
        : null,
    [organizationEntity, selectedEntityId, sortedDepartments]
  );
  const selectedResponsibility = useMemo(
    () =>
      organizationEntity === "responsibility"
        ? sortedResponsibilities.find((responsibility) => responsibility.responsibilityId === selectedEntityId) ?? null
        : null,
    [organizationEntity, selectedEntityId, sortedResponsibilities]
  );

  const userRelations = useMemo(
    () =>
      selectedUser
        ? getUserRelations({
            user: selectedUser,
            departments: sortedDepartments,
            responsibilities: sortedResponsibilities,
          })
        : null,
    [selectedUser, sortedDepartments, sortedResponsibilities]
  );
  const departmentRelations = useMemo(
    () =>
      selectedDepartment
        ? getDepartmentRelations({
            department: selectedDepartment,
            users: sortedUsers,
            responsibilities: sortedResponsibilities,
          })
        : null,
    [selectedDepartment, sortedResponsibilities, sortedUsers]
  );
  const responsibilityRelations = useMemo(
    () =>
      selectedResponsibility
        ? getResponsibilityRelations({
            responsibility: selectedResponsibility,
            users: sortedUsers,
            departments: sortedDepartments,
          })
        : null,
    [selectedResponsibility, sortedDepartments, sortedUsers]
  );

  const canCreateUser =
    !isCreatingUser
    && newUserDisplayNameDraft.trim().length > 0
    && newUserEmailDraft.trim().length > 0;
  const canSaveUser =
    selectedUser !== null
    && !isSavingUserMasterData
    && userDisplayNameDraft.trim().length > 0
    && userEmailDraft.trim().length > 0
    && hasUserMasterDataChanges({
      selectedUser,
      userExternalKeyDraft,
      userDisplayNameDraft,
      userEmailDraft,
      userNotificationEmailDraft,
      userDepartmentIdDraft,
      userIsActiveDraft,
    });

  const selectedDepartmentDraft = selectedDepartment
    ? departmentDrafts[selectedDepartment.departmentId] ?? {
        departmentLeadUserId: "",
        requirementOwnerUserId: "",
      }
    : null;
  const selectedDepartmentLeadOptions =
    selectedDepartmentDraft
      ? buildSupervisorOptions(
          selectedDepartmentDraft.departmentLeadUserId,
          sortedUsers,
          eligibleSupervisorUsers
        )
      : [];
  const selectedDepartmentOwnerOptions =
    selectedDepartmentDraft
      ? buildSupervisorOptions(
          selectedDepartmentDraft.requirementOwnerUserId,
          sortedUsers,
          eligibleSupervisorUsers
        )
      : [];
  const canSaveDepartment =
    selectedDepartment !== null
    && selectedDepartmentDraft !== null
    && hasDepartmentAssignmentChanges(selectedDepartment, selectedDepartmentDraft)
    && savingDepartmentId !== selectedDepartment.departmentId;

  const selectedResponsibilityDraft = selectedResponsibility
    ? responsibilityDrafts[selectedResponsibility.responsibilityId] ?? {
        appUserId: "",
        departmentId: "",
      }
    : null;
  const canSaveResponsibility =
    selectedResponsibility !== null
    && selectedResponsibilityDraft !== null
    && hasResponsibilityAssignmentChanges(selectedResponsibility, selectedResponsibilityDraft)
    && savingResponsibilityId !== selectedResponsibility.responsibilityId;

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
              selectedDepartmentLeadOptions={selectedDepartmentLeadOptions}
              selectedDepartmentOwnerOptions={selectedDepartmentOwnerOptions}
              eligibleSupervisorUsers={eligibleSupervisorUsers}
              newDepartmentNameDraft={newDepartmentNameDraft}
              isCreatingDepartment={isCreatingDepartment}
              deletingDepartmentId={deletingDepartmentId}
              savingDepartmentId={savingDepartmentId}
              canSaveDepartment={canSaveDepartment}
              onNewDepartmentNameChange={onNewDepartmentNameChange}
              onDepartmentDraftChange={onDepartmentDraftChange}
              onCreateDepartment={onCreateDepartment}
              onSaveDepartmentAssignment={onSaveDepartmentAssignment}
              onRemoveDepartment={onRemoveDepartment}
            />
          ) : null}

          {organizationEntity === "responsibility" ? (
            <AdminOrganizationResponsibilityEditor
              selectedResponsibility={selectedResponsibility}
              selectedResponsibilityDraft={selectedResponsibilityDraft}
              sortedDepartments={sortedDepartments}
              sortedUsers={sortedUsers}
              savingResponsibilityId={savingResponsibilityId}
              canSaveResponsibility={canSaveResponsibility}
              onResponsibilityDraftChange={onResponsibilityDraftChange}
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
