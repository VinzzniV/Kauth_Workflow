import { useMemo, useState } from "react";
import EmptyState from "../feedback/EmptyState";
import AdminOrganizationRelationsPanel from "./AdminOrganizationRelationsPanel";
import type {
  AdminDepartmentAssignment,
  AdminResponsibilityOwner,
  AdminUser,
} from "../../types/auth";
import {
  formatTimestamp,
  responsibilityAreaLabel,
  responsibilityTypeLabel,
  roleDisplayName,
  userOptionLabel,
} from "./adminConfigHelpers";
import {
  filterOrganizationDepartments,
  filterOrganizationResponsibilities,
  filterOrganizationUsers,
  getDepartmentRelations,
  getResponsibilityRelations,
  getUserRelations,
  hasDepartmentAssignmentChanges,
  hasResponsibilityAssignmentChanges,
  hasUserMasterDataChanges,
  isEligibleSupervisorSelection,
  type AdminOrganizationEntity,
} from "./adminWorkspaceModel";

type DepartmentDraft = {
  departmentLeadUserId: string;
  requirementOwnerUserId: string;
};

type ResponsibilityDraft = {
  appUserId: string;
  departmentId: string;
};

type AdminOrganizationWorkspaceSectionProps = {
  organizationEntity: AdminOrganizationEntity;
  selectedEntityId: number | null;
  sortedUsers: AdminUser[];
  sortedDepartments: AdminDepartmentAssignment[];
  sortedResponsibilities: AdminResponsibilityOwner[];
  eligibleSupervisorUsers: AdminUser[];
  selectedUser: AdminUser | null;
  selectedUserId: number | null;
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

type UserActivityFilter = "all" | "active" | "inactive";
type ValidityFilter = "all" | "valid" | "invalid";
type ResponsibilityTypeFilter = "all" | "process" | "application";
type ResponsibilityPresenceFilter = "all" | "with_person" | "without_person";
type ResponsibilityDepartmentFilter = "all" | "with_department" | "without_department";

const ENTITY_LABELS: Record<AdminOrganizationEntity, string> = {
  user: "Personen",
  department: "Abteilungen",
  responsibility: "Fachbereiche / Zuständigkeiten",
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
  selectedUserId,
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
  const [organizationSearch, setOrganizationSearch] = useState<string>("");
  const [userActivityFilter, setUserActivityFilter] = useState<UserActivityFilter>("all");
  const [userDepartmentFilter, setUserDepartmentFilter] = useState<string>("");
  const [departmentLeadFilter, setDepartmentLeadFilter] = useState<ValidityFilter>("all");
  const [departmentOwnerFilter, setDepartmentOwnerFilter] = useState<ValidityFilter>("all");
  const [responsibilityTypeFilter, setResponsibilityTypeFilter] =
    useState<ResponsibilityTypeFilter>("all");
  const [responsibilityPersonFilter, setResponsibilityPersonFilter] =
    useState<ResponsibilityPresenceFilter>("all");
  const [responsibilityDepartmentFilter, setResponsibilityDepartmentFilter] =
    useState<ResponsibilityDepartmentFilter>("all");

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

  const filteredUsers = useMemo(
    () =>
      filterOrganizationUsers({
        users: sortedUsers,
        search: organizationSearch,
        activityFilter: userActivityFilter,
        departmentFilter: userDepartmentFilter,
      }),
    [organizationSearch, sortedUsers, userActivityFilter, userDepartmentFilter]
  );
  const filteredDepartments = useMemo(
    () =>
      filterOrganizationDepartments({
        departments: sortedDepartments,
        search: organizationSearch,
        leadFilter: departmentLeadFilter,
        ownerFilter: departmentOwnerFilter,
        eligibleSupervisorUsers,
      }),
    [
      departmentLeadFilter,
      departmentOwnerFilter,
      eligibleSupervisorUsers,
      organizationSearch,
      sortedDepartments,
    ]
  );
  const filteredResponsibilities = useMemo(
    () =>
      filterOrganizationResponsibilities({
        responsibilities: sortedResponsibilities,
        search: organizationSearch,
        typeFilter: responsibilityTypeFilter,
        personFilter: responsibilityPersonFilter,
        departmentFilter: responsibilityDepartmentFilter,
      }),
    [
      organizationSearch,
      responsibilityDepartmentFilter,
      responsibilityPersonFilter,
      responsibilityTypeFilter,
      sortedResponsibilities,
    ]
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
  const hasInvalidDepartmentLeadSelection =
    selectedDepartmentDraft !== null
    && !isEligibleSupervisorSelection(
      selectedDepartmentDraft.departmentLeadUserId,
      eligibleSupervisorUsers
    );
  const hasInvalidDepartmentOwnerSelection =
    selectedDepartmentDraft !== null
    && !isEligibleSupervisorSelection(
      selectedDepartmentDraft.requirementOwnerUserId,
      eligibleSupervisorUsers
    );
  const canSaveDepartment =
    selectedDepartment !== null
    && selectedDepartmentDraft !== null
    && !hasInvalidDepartmentLeadSelection
    && !hasInvalidDepartmentOwnerSelection
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

  function renderSidebarFilters() {
    if (organizationEntity === "user") {
      return (
        <>
          <label className="field">
            <span>Suche</span>
            <input
              type="search"
              value={organizationSearch}
              onChange={(event) => setOrganizationSearch(event.target.value)}
              placeholder="Name, Mail oder Abteilung"
            />
          </label>

          <label className="field">
            <span>Status</span>
            <select
              value={userActivityFilter}
              onChange={(event) => setUserActivityFilter(event.target.value as UserActivityFilter)}
            >
              <option value="all">Alle</option>
              <option value="active">Nur aktiv</option>
              <option value="inactive">Nur inaktiv</option>
            </select>
          </label>

          <label className="field">
            <span>Abteilung</span>
            <select
              value={userDepartmentFilter}
              onChange={(event) => setUserDepartmentFilter(event.target.value)}
            >
              <option value="">Alle Abteilungen</option>
              {sortedDepartments.map((department) => (
                <option key={`user-filter-department-${department.departmentId}`} value={department.departmentId}>
                  {department.departmentName}
                </option>
              ))}
            </select>
          </label>
        </>
      );
    }

    if (organizationEntity === "department") {
      return (
        <>
          <label className="field">
            <span>Suche</span>
            <input
              type="search"
              value={organizationSearch}
              onChange={(event) => setOrganizationSearch(event.target.value)}
              placeholder="Abteilungsname"
            />
          </label>

          <label className="field">
            <span>Leitung</span>
            <select
              value={departmentLeadFilter}
              onChange={(event) => setDepartmentLeadFilter(event.target.value as ValidityFilter)}
            >
              <option value="all">Alle</option>
              <option value="valid">Mit gültiger Leitung</option>
              <option value="invalid">Ohne gültige Leitung</option>
            </select>
          </label>

          <label className="field">
            <span>Anforderungsverantwortung</span>
            <select
              value={departmentOwnerFilter}
              onChange={(event) => setDepartmentOwnerFilter(event.target.value as ValidityFilter)}
            >
              <option value="all">Alle</option>
              <option value="valid">Mit gültiger Person</option>
              <option value="invalid">Ohne gültige Person</option>
            </select>
          </label>
        </>
      );
    }

    return (
      <>
        <label className="field">
          <span>Suche</span>
          <input
            type="search"
            value={organizationSearch}
            onChange={(event) => setOrganizationSearch(event.target.value)}
            placeholder="Name, Bereich oder System-Key"
          />
        </label>

        <label className="field">
          <span>Typ</span>
          <select
            value={responsibilityTypeFilter}
            onChange={(event) =>
              setResponsibilityTypeFilter(event.target.value as ResponsibilityTypeFilter)
            }
          >
            <option value="all">Alle</option>
            <option value="process">Nur Prozess</option>
            <option value="application">Nur System</option>
          </select>
        </label>

        <label className="field">
          <span>Feste Person</span>
          <select
            value={responsibilityPersonFilter}
            onChange={(event) =>
              setResponsibilityPersonFilter(event.target.value as ResponsibilityPresenceFilter)
            }
          >
            <option value="all">Alle</option>
            <option value="with_person">Mit fester Person</option>
            <option value="without_person">Ohne feste Person</option>
          </select>
        </label>

        <label className="field">
          <span>Bereich</span>
          <select
            value={responsibilityDepartmentFilter}
            onChange={(event) =>
              setResponsibilityDepartmentFilter(event.target.value as ResponsibilityDepartmentFilter)
            }
          >
            <option value="all">Alle</option>
            <option value="with_department">Mit Bereich</option>
            <option value="without_department">Ohne Bereich</option>
          </select>
        </label>
      </>
    );
  }

  function renderSidebarList() {
    if (organizationEntity === "user") {
      if (filteredUsers.length === 0) {
        return <p className="panel-note">Keine Personen für den aktuellen Filter.</p>;
      }

      return (
        <div className="admin-entity-list" aria-label="Personenliste">
          {filteredUsers.map((user) => (
            <button
              key={user.userId}
              type="button"
              className={`admin-entity-list-item ${selectedUserId === user.userId ? "active" : ""}`}
              onClick={() => {
                onSelectUser(user);
                onSelectOrganizationEntity("user", user.userId);
              }}
            >
              <strong>{user.displayName}</strong>
              <span>{user.email}</span>
              <span>
                {user.departmentName ?? "Keine Abteilung"} | {user.isActive ? "Aktiv" : "Inaktiv"}
              </span>
            </button>
          ))}
        </div>
      );
    }

    if (organizationEntity === "department") {
      if (filteredDepartments.length === 0) {
        return <p className="panel-note">Keine Abteilungen für den aktuellen Filter.</p>;
      }

      return (
        <div className="admin-entity-list" aria-label="Abteilungsliste">
          {filteredDepartments.map((department) => (
            <button
              key={department.departmentId}
              type="button"
              className={`admin-entity-list-item ${selectedDepartment?.departmentId === department.departmentId ? "active" : ""}`}
              onClick={() => onSelectOrganizationEntity("department", department.departmentId)}
            >
              <strong>{department.departmentName}</strong>
              <span>Leitung: {department.departmentLeadDisplayName ?? "nicht festgelegt"}</span>
              <span>Anforderung: {department.requirementOwnerDisplayName ?? "nicht festgelegt"}</span>
            </button>
          ))}
        </div>
      );
    }

    if (filteredResponsibilities.length === 0) {
      return <p className="panel-note">Keine Zuständigkeiten für den aktuellen Filter.</p>;
    }

    return (
      <div className="admin-entity-list" aria-label="Zuständigkeitsliste">
        {filteredResponsibilities.map((responsibility) => (
          <button
            key={responsibility.responsibilityId}
            type="button"
            className={`admin-entity-list-item ${selectedResponsibility?.responsibilityId === responsibility.responsibilityId ? "active" : ""}`}
            onClick={() => onSelectOrganizationEntity("responsibility", responsibility.responsibilityId)}
          >
            <strong>{responsibility.responsibilityName}</strong>
            <span>
              {responsibilityAreaLabel(responsibility)} | {responsibilityTypeLabel(responsibility)}
            </span>
            <span>Person: {responsibility.appUserDisplayName ?? "keine feste Person"}</span>
          </button>
        ))}
      </div>
    );
  }

  function renderMainContent() {
    if (organizationEntity === "user") {
      if (!selectedUser) {
        return (
          <section className="panel">
            <div className="panel-head">
              <h2>Neue Person</h2>
              <p>Personen-Stammdaten bleiben hier bewusst getrennt von Rollen und Gruppen.</p>
            </div>

            <div className="form-grid">
              <label className="field">
                <span>Anzeigename</span>
                <input
                  type="text"
                  value={newUserDisplayNameDraft}
                  onChange={(event) => onNewUserDisplayNameChange(event.target.value)}
                  placeholder="Max Mustermann"
                />
              </label>

              <label className="field">
                <span>Login-E-Mail</span>
                <input
                  type="email"
                  value={newUserEmailDraft}
                  onChange={(event) => onNewUserEmailChange(event.target.value)}
                  placeholder="max.mustermann@example.com"
                />
              </label>

              <label className="field">
                <span>Benachrichtigungs-Mail</span>
                <input
                  type="email"
                  value={newUserNotificationEmailDraft}
                  onChange={(event) => onNewUserNotificationEmailChange(event.target.value)}
                  placeholder="optional für Demo-Verteiler"
                />
              </label>

              <label className="field">
                <span>Anmeldename</span>
                <input
                  type="text"
                  value={newUserExternalKeyDraft}
                  onChange={(event) => onNewUserExternalKeyChange(event.target.value)}
                  placeholder="optional"
                />
              </label>

              <label className="field">
                <span>Abteilung</span>
                <select
                  value={newUserDepartmentIdDraft}
                  onChange={(event) => onNewUserDepartmentIdChange(event.target.value)}
                >
                  <option value="">Keine Abteilung</option>
                  {sortedDepartments.map((department) => (
                    <option key={`new-user-department-${department.departmentId}`} value={department.departmentId}>
                      {department.departmentName}
                    </option>
                  ))}
                </select>
              </label>

              <label className="field">
                <span>Status</span>
                <select
                  value={newUserIsActiveDraft ? "active" : "inactive"}
                  onChange={(event) => onNewUserIsActiveChange(event.target.value === "active")}
                >
                  <option value="active">Aktiv</option>
                  <option value="inactive">Inaktiv</option>
                </select>
              </label>
            </div>

            <div className="action-row">
              <button
                type="button"
                className="btn btn-primary"
                onClick={() => {
                  void onCreateUser();
                }}
                disabled={!canCreateUser}
              >
                {isCreatingUser ? "Anlegen..." : "Person anlegen"}
              </button>
            </div>
          </section>
        );
      }

      return (
        <section className="panel">
          <div className="panel-head">
            <h2>Person pflegen: {selectedUser.displayName}</h2>
            <p>Verknüpfte Organisationsbeziehungen werden rechts kontextbezogen eingeblendet.</p>
          </div>

          <div className="form-grid">
            <label className="field">
              <span>Anzeigename</span>
              <input
                type="text"
                value={userDisplayNameDraft}
                onChange={(event) => onUserDisplayNameChange(event.target.value)}
              />
            </label>

            <label className="field">
              <span>Login-E-Mail</span>
              <input
                type="email"
                value={userEmailDraft}
                onChange={(event) => onUserEmailChange(event.target.value)}
              />
            </label>

            <label className="field">
              <span>Benachrichtigungs-Mail</span>
              <input
                type="email"
                value={userNotificationEmailDraft}
                onChange={(event) => onUserNotificationEmailChange(event.target.value)}
                placeholder="leer = Login-E-Mail verwenden"
              />
            </label>

            <label className="field">
              <span>Anmeldename</span>
              <input
                type="text"
                value={userExternalKeyDraft}
                onChange={(event) => onUserExternalKeyChange(event.target.value)}
                placeholder="optional"
              />
            </label>

            <label className="field">
              <span>Abteilung</span>
              <select
                value={userDepartmentIdDraft}
                onChange={(event) => onUserDepartmentIdChange(event.target.value)}
              >
                <option value="">Keine Abteilung</option>
                {sortedDepartments.map((department) => (
                  <option key={`user-department-${department.departmentId}`} value={department.departmentId}>
                    {department.departmentName}
                  </option>
                ))}
              </select>
            </label>

            <label className="field">
              <span>Status</span>
              <select
                value={userIsActiveDraft ? "active" : "inactive"}
                onChange={(event) => onUserIsActiveChange(event.target.value === "active")}
              >
                <option value="active">Aktiv</option>
                <option value="inactive">Inaktiv</option>
              </select>
            </label>
          </div>

          <p className="panel-note">
            Rollen: {selectedUser.roles.map(roleDisplayName).join(", ") || "keine"} | Gruppen:{" "}
            {selectedUser.groups.map((group) => group.groupName).join(", ") || "keine"}
          </p>

          {userFormError ? <p className="panel-note">{userFormError}</p> : null}
          {userFormNotice ? <p className="panel-note">{userFormNotice}</p> : null}

          <div className="action-row">
            <button
              type="button"
              className="btn btn-primary"
              onClick={() => {
                void onSaveUserMasterData();
              }}
              disabled={!canSaveUser}
            >
              {isSavingUserMasterData ? "Speichern..." : "Person speichern"}
            </button>
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => {
                void onRemoveUser(selectedUser);
              }}
              disabled={deletingUserId === selectedUser.userId}
            >
              {deletingUserId === selectedUser.userId ? "Löschen..." : "Person löschen"}
            </button>
          </div>
        </section>
      );
    }

    if (organizationEntity === "department") {
      if (!selectedDepartment) {
        return (
          <section className="panel">
            <div className="panel-head">
              <h2>Neue Abteilung</h2>
              <p>Neue Abteilungen entstehen im selben Organisations-Workspace und sind danach direkt verknüpfbar.</p>
            </div>

            <label className="field">
              <span>Name</span>
              <input
                type="text"
                value={newDepartmentNameDraft}
                onChange={(event) => onNewDepartmentNameChange(event.target.value)}
                placeholder="z. B. Einkauf"
              />
            </label>

            <div className="action-row">
              <button
                type="button"
                className="btn btn-primary"
                onClick={() => {
                  void onCreateDepartment();
                }}
                disabled={isCreatingDepartment || newDepartmentNameDraft.trim().length === 0}
              >
                {isCreatingDepartment ? "Anlegen..." : "Abteilung anlegen"}
              </button>
            </div>
          </section>
        );
      }

      if (!selectedDepartmentDraft) {
        return null;
      }

      return (
        <section className="panel">
          <div className="panel-head">
            <h2>Abteilung pflegen: {selectedDepartment.departmentName}</h2>
            <p>Leitung und Anforderungsverantwortung werden bewusst gemeinsam gepflegt.</p>
          </div>

          <div className="form-grid">
            <label className="field">
              <span>Abteilungsleitung</span>
              <select
                value={selectedDepartmentDraft.departmentLeadUserId}
                onChange={(event) =>
                  onDepartmentDraftChange(selectedDepartment.departmentId, {
                    ...selectedDepartmentDraft,
                    departmentLeadUserId: event.target.value,
                  })
                }
              >
                <option value="">Nicht fest hinterlegt</option>
                {selectedDepartmentLeadOptions.map((user) => (
                  <option key={`lead-${selectedDepartment.departmentId}-${user.userId}`} value={user.userId}>
                    {userOptionLabel(user)}
                    {!eligibleSupervisorUsers.some((candidate) => candidate.userId === user.userId)
                      ? " | aktuell ungültig"
                      : ""}
                  </option>
                ))}
              </select>
            </label>

            <label className="field">
              <span>Anforderungsverantwortliche Person</span>
              <select
                value={selectedDepartmentDraft.requirementOwnerUserId}
                onChange={(event) =>
                  onDepartmentDraftChange(selectedDepartment.departmentId, {
                    ...selectedDepartmentDraft,
                    requirementOwnerUserId: event.target.value,
                  })
                }
              >
                <option value="">Nicht fest hinterlegt</option>
                {selectedDepartmentOwnerOptions.map((user) => (
                  <option key={`owner-${selectedDepartment.departmentId}-${user.userId}`} value={user.userId}>
                    {userOptionLabel(user)}
                    {!eligibleSupervisorUsers.some((candidate) => candidate.userId === user.userId)
                      ? " | aktuell ungültig"
                      : ""}
                  </option>
                ))}
              </select>
            </label>
          </div>

          <p className="panel-note">
            Aktuell gespeichert: Leitung {selectedDepartment.departmentLeadDisplayName ?? "keine feste Person"} |
            Anforderungsverantwortung {selectedDepartment.requirementOwnerDisplayName ?? "keine feste Person"} |
            Zuletzt gespeichert {formatTimestamp(selectedDepartment.updatedAt)}
          </p>

          {hasInvalidDepartmentLeadSelection || hasInvalidDepartmentOwnerSelection ? (
            <p className="panel-note">
              Ungültige Zuordnung: Gespeicherte Personen ohne aktive Manager-Berechtigung bleiben sichtbar, müssen
              aber vor dem Speichern ersetzt oder entfernt werden.
            </p>
          ) : null}

          <div className="action-row">
            <button
              type="button"
              className="btn btn-primary"
              onClick={() => {
                void onSaveDepartmentAssignment(selectedDepartment.departmentId);
              }}
              disabled={!canSaveDepartment}
            >
              {savingDepartmentId === selectedDepartment.departmentId ? "Speichern..." : "Zuständigkeit speichern"}
            </button>
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => {
                void onRemoveDepartment(selectedDepartment);
              }}
              disabled={deletingDepartmentId === selectedDepartment.departmentId}
            >
              {deletingDepartmentId === selectedDepartment.departmentId ? "Löschen..." : "Abteilung löschen"}
            </button>
          </div>
        </section>
      );
    }

    if (!selectedResponsibility) {
      return (
        <section className="panel">
          <EmptyState
            title="Zuständigkeit auswählen"
            description="Wählen Sie links eine fachliche Zuständigkeit, um Person und Bereich zu pflegen."
          />
        </section>
      );
    }

    if (!selectedResponsibilityDraft) {
      return null;
    }

    return (
      <section className="panel">
        <div className="panel-head">
          <h2>Zuständigkeit pflegen: {selectedResponsibility.responsibilityName}</h2>
          <p>
            {responsibilityAreaLabel(selectedResponsibility)} | {responsibilityTypeLabel(selectedResponsibility)}
          </p>
        </div>

        <div className="form-grid">
          <label className="field">
            <span>Zuständige Abteilung</span>
            <select
              value={selectedResponsibilityDraft.departmentId}
              onChange={(event) =>
                onResponsibilityDraftChange(selectedResponsibility.responsibilityId, {
                  ...selectedResponsibilityDraft,
                  departmentId: event.target.value,
                })
              }
            >
              <option value="">Standard-Abteilung verwenden</option>
              {sortedDepartments.map((department) => (
                <option
                  key={`responsibility-department-${selectedResponsibility.responsibilityId}-${department.departmentId}`}
                  value={department.departmentId}
                >
                  {department.departmentName}
                </option>
              ))}
            </select>
          </label>

          <label className="field">
            <span>Zuständige Person</span>
            <select
              value={selectedResponsibilityDraft.appUserId}
              onChange={(event) =>
                onResponsibilityDraftChange(selectedResponsibility.responsibilityId, {
                  ...selectedResponsibilityDraft,
                  appUserId: event.target.value,
                })
              }
            >
              <option value="">Keine feste Person</option>
              {sortedUsers.map((user) => (
                <option
                  key={`responsibility-user-${selectedResponsibility.responsibilityId}-${user.userId}`}
                  value={user.userId}
                >
                  {userOptionLabel(user)}
                </option>
              ))}
            </select>
          </label>
        </div>

        <p className="panel-note">
          Aktuell gespeichert: {selectedResponsibility.appUserDisplayName ?? "keine feste Person"} | Bereich:{" "}
          {selectedResponsibility.departmentName ?? "Standard"} | Zuletzt gespeichert{" "}
          {formatTimestamp(selectedResponsibility.updatedAt)}
        </p>

        <div className="action-row">
          <button
            type="button"
            className="btn btn-primary"
            onClick={() => {
              void onSaveResponsibilityAssignment(selectedResponsibility.responsibilityId);
            }}
            disabled={!canSaveResponsibility}
          >
            {savingResponsibilityId === selectedResponsibility.responsibilityId ? "Speichern..." : "Zuständigkeit speichern"}
          </button>
        </div>
      </section>
    );
  }

  return (
    <div className="content-stack">
      <section className="panel panel-muted">
        <div className="panel-head">
          <h2>Organisation</h2>
          <p>Personen, Abteilungen und fachliche Zuständigkeiten werden hier als zusammenhängendes Modell gepflegt.</p>
        </div>
      </section>

      <div className="admin-workspace-grid">
        <section className="panel admin-organization-sidebar">
          <div className="panel-head">
            <h2>{ENTITY_LABELS[organizationEntity]}</h2>
            <p>Ein primäres Objekt bleibt im Fokus, die Liste links bleibt stabil.</p>
          </div>

          <div className="admin-entity-switcher" role="tablist" aria-label="Organisationsobjekte">
            {(["user", "department", "responsibility"] as const).map((entity) => (
              <button
                key={entity}
                type="button"
                className={`admin-entity-switch ${organizationEntity === entity ? "active" : ""}`}
                onClick={() => onSelectOrganizationEntity(entity, null)}
              >
                {ENTITY_LABELS[entity]}
              </button>
            ))}
          </div>

          {renderSidebarFilters()}
          {renderSidebarList()}
        </section>

        <div className="admin-organization-main">{renderMainContent()}</div>
        <aside className="admin-organization-aside">
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
        </aside>
      </div>
    </div>
  );
}
