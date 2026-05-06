import { useMemo, useState } from "react";
import AdminOrganizationRelationsPanel from "./AdminOrganizationRelationsPanel";
import { AdminOrganizationUserEditor } from "./AdminOrganizationUserEditor";
import { useAdminOrganizationWorkspaceView } from "./useAdminOrganizationWorkspaceView";
import {
  filterOrganizationUsers,
  type AdminOrganizationEntity,
} from "./adminWorkspaceModel";
import type {
  AdminDepartmentAssignment,
  AdminResponsibilityOwner,
  AdminRole,
  AdminUser,
} from "../../types/auth";
import type { DepartmentDraft, PositionDraft } from "./adminOrganizationTypes";

type ActivityFilter = "all" | "active" | "inactive";

type AdminPersonenSectionProps = {
  selectedEntityId: number | null;
  sortedUsers: AdminUser[];
  sortedDepartments: AdminDepartmentAssignment[];
  sortedResponsibilities: AdminResponsibilityOwner[];
  sortedDepartmentPositions: AdminRole[];
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
  departmentDrafts: Record<number, DepartmentDraft>;
  positionDrafts: Record<number, PositionDraft>;
  savingDepartmentId: number | null;
  onSelectOrganizationEntity: (entity: AdminOrganizationEntity, id?: number | null) => void;
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

type DrawerMode = "edit" | "create" | null;

export function AdminPersonenSection(props: AdminPersonenSectionProps) {
  const [search, setSearch] = useState("");
  const [activityFilter, setActivityFilter] = useState<ActivityFilter>("all");
  const [departmentFilter, setDepartmentFilter] = useState<string>("");
  const [includeTechnical, setIncludeTechnical] = useState(false);
  const [isCreateDrawerOpen, setIsCreateDrawerOpen] = useState(false);

  const filteredUsers = useMemo(
    () =>
      filterOrganizationUsers({
        users: props.sortedUsers,
        search,
        activityFilter,
        departmentFilter,
        includeTechnicalActors: includeTechnical,
      }),
    [activityFilter, departmentFilter, includeTechnical, props.sortedUsers, search]
  );

  const view = useAdminOrganizationWorkspaceView({
    organizationEntity: "user",
    selectedEntityId: props.selectedEntityId,
    sortedUsers: props.sortedUsers,
    sortedDepartmentPositions: props.sortedDepartmentPositions,
    sortedDepartments: props.sortedDepartments,
    sortedResponsibilities: props.sortedResponsibilities,
    eligibleSupervisorUsers: props.eligibleSupervisorUsers,
    eligibleRequirementOwnerUsers: props.eligibleRequirementOwnerUsers,
    selectedUser: props.selectedUser,
    userDisplayNameDraft: props.userDisplayNameDraft,
    userEmailDraft: props.userEmailDraft,
    userNotificationEmailDraft: props.userNotificationEmailDraft,
    userExternalKeyDraft: props.userExternalKeyDraft,
    userDepartmentIdDraft: props.userDepartmentIdDraft,
    userIsActiveDraft: props.userIsActiveDraft,
    newUserDisplayNameDraft: props.newUserDisplayNameDraft,
    newUserEmailDraft: props.newUserEmailDraft,
    isCreatingUser: props.isCreatingUser,
    isSavingUserMasterData: props.isSavingUserMasterData,
    departmentDrafts: props.departmentDrafts,
    responsibilityDrafts: {},
    savingDepartmentId: props.savingDepartmentId,
    savingResponsibilityId: null,
  });

  const closeDrawer = () => {
    setIsCreateDrawerOpen(false);
    if (props.selectedEntityId) {
      props.onSelectOrganizationEntity("user", null);
    }
  };

  const openCreate = () => {
    if (props.selectedEntityId) {
      props.onSelectOrganizationEntity("user", null);
    }
    setIsCreateDrawerOpen(true);
  };

  const openEdit = (user: AdminUser) => {
    setIsCreateDrawerOpen(false);
    props.onSelectOrganizationEntity("user", user.userId);
    props.onSelectUser(user);
  };

  const drawerMode: DrawerMode = isCreateDrawerOpen ? "create" : props.workspaceSelectedUser ? "edit" : null;
  const drawerSelectedUser = drawerMode === "edit" ? props.workspaceSelectedUser : null;

  return (
    <div className="content-stack admin-list-page">
      <div className="admin-list-toolbar">
        <label className="field compact grow">
          <span>Suche</span>
          <input
            type="search"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Name, Mail oder Abteilung"
          />
        </label>

        <label className="field compact">
          <span>Status</span>
          <select
            value={activityFilter}
            onChange={(event) => setActivityFilter(event.target.value as ActivityFilter)}
          >
            <option value="all">Alle</option>
            <option value="active">Nur aktiv</option>
            <option value="inactive">Nur inaktiv</option>
          </select>
        </label>

        <label className="field compact">
          <span>Abteilung</span>
          <select value={departmentFilter} onChange={(event) => setDepartmentFilter(event.target.value)}>
            <option value="">Alle Abteilungen</option>
            {props.sortedDepartments.map((department) => (
              <option key={department.departmentId} value={String(department.departmentId)}>
                {department.departmentName}
              </option>
            ))}
          </select>
        </label>

        <label className="field compact admin-list-toolbar-checkbox">
          <input
            type="checkbox"
            checked={includeTechnical}
            onChange={(event) => setIncludeTechnical(event.target.checked)}
          />
          <span>Technische Konten anzeigen</span>
        </label>

        <button type="button" className="btn btn-primary" onClick={openCreate}>
          Neue Person
        </button>
      </div>

      <p className="panel-note admin-list-summary">
        {filteredUsers.length} von {props.sortedUsers.length} {props.sortedUsers.length === 1 ? "Person" : "Personen"}
      </p>

      <div className="table-scroll">
        <table className="table admin-data-table">
          <thead>
            <tr>
              <th>Name</th>
              <th>E-Mail</th>
              <th>Abteilung</th>
              <th>Status</th>
              <th>Quelle</th>
            </tr>
          </thead>
          <tbody>
            {filteredUsers.map((user) => {
              const isSelected = props.workspaceSelectedUser?.userId === user.userId;
              return (
                <tr
                  key={user.userId}
                  className={`admin-data-row${isSelected ? " admin-data-row--selected" : ""}`}
                  onClick={() => openEdit(user)}
                >
                  <td>
                    <div className="admin-data-cell-strong">{user.displayName}</div>
                    {user.isTechnicalActor ? (
                      <div className="panel-note">Technisches Konto</div>
                    ) : null}
                  </td>
                  <td>{user.email}</td>
                  <td>{user.departmentName ?? <span className="meta-empty">Keine</span>}</td>
                  <td>
                    <span className={`badge badge--${user.isActive ? "success" : "default"}`}>
                      {user.isActive ? "Aktiv" : "Inaktiv"}
                    </span>
                  </td>
                  <td>
                    <span className="panel-note">
                      {user.directorySynced ? "Verzeichnis" : "Lokal"}
                    </span>
                  </td>
                </tr>
              );
            })}
            {filteredUsers.length === 0 ? (
              <tr>
                <td colSpan={5} className="admin-data-empty">
                  Keine Personen passen zu den Filtern.
                </td>
              </tr>
            ) : null}
          </tbody>
        </table>
      </div>

      {drawerMode ? (
        <>
          <div className="admin-drawer-backdrop" onClick={closeDrawer} aria-hidden="true" />
          <aside className="admin-drawer" role="dialog" aria-modal="true" aria-label={drawerMode === "edit" ? "Person bearbeiten" : "Neue Person"}>
            <header className="admin-drawer-head">
              <div className="admin-drawer-title">
                <h2>
                  {drawerMode === "edit"
                    ? props.workspaceSelectedUser?.displayName ?? "Person bearbeiten"
                    : "Neue Person"}
                </h2>
                {drawerMode === "edit" && props.workspaceSelectedUser ? (
                  <span className={`badge badge--${props.workspaceSelectedUser.isActive ? "success" : "default"}`}>
                    {props.workspaceSelectedUser.isActive ? "Aktiv" : "Inaktiv"}
                  </span>
                ) : null}
              </div>
              <button type="button" className="admin-drawer-close" onClick={closeDrawer} aria-label="Schließen">
                ×
              </button>
            </header>

            <div className="admin-drawer-body content-stack">
              <AdminOrganizationUserEditor
                sortedDepartments={props.sortedDepartments}
                selectedUser={drawerSelectedUser}
                newUserDisplayNameDraft={props.newUserDisplayNameDraft}
                newUserEmailDraft={props.newUserEmailDraft}
                newUserNotificationEmailDraft={props.newUserNotificationEmailDraft}
                newUserExternalKeyDraft={props.newUserExternalKeyDraft}
                newUserDepartmentIdDraft={props.newUserDepartmentIdDraft}
                newUserIsActiveDraft={props.newUserIsActiveDraft}
                isCreatingUser={props.isCreatingUser}
                userDisplayNameDraft={props.userDisplayNameDraft}
                userEmailDraft={props.userEmailDraft}
                userNotificationEmailDraft={props.userNotificationEmailDraft}
                userExternalKeyDraft={props.userExternalKeyDraft}
                userDepartmentIdDraft={props.userDepartmentIdDraft}
                userIsActiveDraft={props.userIsActiveDraft}
                userFormError={props.userFormError}
                userFormNotice={props.userFormNotice}
                isSavingUserMasterData={props.isSavingUserMasterData}
                deletingUserId={props.deletingUserId}
                canCreateUser={view.canCreateUser}
                canSaveUser={view.canSaveUser}
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
              />

              {drawerMode === "edit" && drawerSelectedUser ? (
                <AdminOrganizationRelationsPanel
                  organizationEntity="user"
                  selectedUser={drawerSelectedUser}
                  userRelations={view.userRelations}
                  selectedDepartment={null}
                  departmentRelations={null}
                  selectedResponsibility={null}
                  responsibilityRelations={null}
                  onSelectOrganizationEntity={props.onSelectOrganizationEntity}
                />
              ) : null}
            </div>
          </aside>
        </>
      ) : null}
    </div>
  );
}
