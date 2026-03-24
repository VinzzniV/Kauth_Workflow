import EmptyState from "../feedback/EmptyState";
import LoadingState from "../feedback/LoadingState";
import type { AdminGroup, AdminRole, AdminUser } from "../../types/auth";
import { roleDisplayName } from "./adminConfigHelpers";

type AdminTechnicalAccessSectionProps = {
  isTechnicalAccessOpen: boolean;
  isLoadingTechnicalAccess: boolean;
  sortedUsers: AdminUser[];
  selectedUser: AdminUser | null;
  selectedUserRoleIds: number[];
  selectedUserGroupIds: number[];
  selectedGroupId: number | null;
  selectedGroup: AdminGroup | null;
  selectedGroupRoleIds: number[];
  sortedRoles: AdminRole[];
  groups: AdminGroup[];
  isSavingUserRoles: boolean;
  isSavingUserGroups: boolean;
  isSavingGroupRoles: boolean;
  onSelectUser: (user: AdminUser) => void;
  onToggleUserRole: (roleId: number) => void;
  onToggleUserGroup: (groupId: number) => void;
  onSelectGroup: (groupId: number | null) => void;
  onToggleGroupRole: (roleId: number) => void;
  onSaveUserRoles: () => void | Promise<void>;
  onSaveUserGroups: () => void | Promise<void>;
  onSaveGroupRoles: () => void | Promise<void>;
};

export function AdminTechnicalAccessSection({
  isTechnicalAccessOpen,
  isLoadingTechnicalAccess,
  sortedUsers,
  selectedUser,
  selectedUserRoleIds,
  selectedUserGroupIds,
  selectedGroupId,
  selectedGroup,
  selectedGroupRoleIds,
  sortedRoles,
  groups,
  isSavingUserRoles,
  isSavingUserGroups,
  isSavingGroupRoles,
  onSelectUser,
  onToggleUserRole,
  onToggleUserGroup,
  onSelectGroup,
  onToggleGroupRole,
  onSaveUserRoles,
  onSaveUserGroups,
  onSaveGroupRoles,
}: AdminTechnicalAccessSectionProps) {
  if (!isTechnicalAccessOpen) {
    return null;
  }

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Benutzerrechte und Gruppen</h2>
        <p>Dieser Bereich ist für Rollen, Gruppen und Berechtigungen gedacht und wird bei Bedarf separat geladen.</p>
      </div>

      {isLoadingTechnicalAccess ? <LoadingState title="Rechte werden geladen..." /> : null}

      {!isLoadingTechnicalAccess && sortedUsers.length > 0 ? (
        <label className="field compact">
          <span>Person</span>
          <select
            aria-label="Person"
            value={selectedUser?.userId ?? ""}
            onChange={(event) => {
              const nextUser = sortedUsers.find((user) => user.userId === Number(event.target.value));
              if (nextUser) {
                onSelectUser(nextUser);
              }
            }}
          >
            <option value="">Bitte wählen</option>
            {sortedUsers.map((user) => (
              <option key={user.userId} value={user.userId}>
                {user.displayName} ({user.email})
              </option>
            ))}
          </select>
        </label>
      ) : null}

      {!isLoadingTechnicalAccess && selectedUser ? (
        <div className="content-stack">
          <section className="panel">
            <div className="panel-head">
              <h2>Rollen zuweisen: {selectedUser.displayName}</h2>
              <p>Rollen direkt zuordnen.</p>
            </div>

            <div className="chips-row" aria-label="Rollen Auswahl">
              {sortedRoles.map((role) => {
                const isActive = selectedUserRoleIds.includes(role.roleId);
                return (
                  <button
                    key={role.roleId}
                    type="button"
                    className={`chip chip-action ${isActive ? "active" : ""}`}
                    onClick={() => onToggleUserRole(role.roleId)}
                  >
                    {roleDisplayName(role)} ({role.roleKey})
                  </button>
                );
              })}
            </div>

            <div className="action-row">
              <button
                type="button"
                className="btn btn-primary"
                onClick={() => {
                  void onSaveUserRoles();
                }}
                disabled={isSavingUserRoles}
              >
                {isSavingUserRoles ? "Speichern..." : "Rollen speichern"}
              </button>
            </div>
          </section>

          <section className="panel">
            <div className="panel-head">
              <h2>Gruppen zuweisen: {selectedUser.displayName}</h2>
              <p>Gruppen mit vererbten Rechten zuordnen.</p>
            </div>

            <div className="chips-row" aria-label="Gruppen Auswahl">
              {groups.map((group) => {
                const isActive = selectedUserGroupIds.includes(group.groupId);
                return (
                  <button
                    key={group.groupId}
                    type="button"
                    className={`chip chip-action ${isActive ? "active" : ""}`}
                    onClick={() => onToggleUserGroup(group.groupId)}
                  >
                    {group.groupName} ({group.groupKey})
                  </button>
                );
              })}
            </div>

            <div className="action-row">
              <button
                type="button"
                className="btn btn-primary"
                onClick={() => {
                  void onSaveUserGroups();
                }}
                disabled={isSavingUserGroups}
              >
                {isSavingUserGroups ? "Speichern..." : "Gruppen speichern"}
              </button>
            </div>
          </section>
        </div>
      ) : null}

      {!isLoadingTechnicalAccess && !selectedUser ? (
        <EmptyState
          title="Person auswählen"
          description="Wählen Sie in diesem Bereich zuerst eine Person für die Rechteverwaltung aus."
        />
      ) : null}

      {!isLoadingTechnicalAccess && groups.length > 0 ? (
        <section className="panel">
          <div className="panel-head">
            <h2>Gruppenrollen</h2>
            <p>Rollen pro Gruppe nur bei Bedarf anpassen.</p>
          </div>

          <label className="field compact">
            <span>Gruppe</span>
            <select
              value={selectedGroupId ?? ""}
              onChange={(event) => onSelectGroup(event.target.value ? Number(event.target.value) : null)}
            >
              <option value="">Bitte wählen</option>
              {groups.map((group) => (
                <option key={group.groupId} value={group.groupId}>
                  {group.groupName} ({group.groupKey})
                </option>
              ))}
            </select>
          </label>

          {selectedGroup ? (
            <>
              <p className="panel-note">
                Aktuelle Rollen: {selectedGroup.roles.map(roleDisplayName).join(", ") || "-"}
              </p>
              <div className="chips-row" aria-label="Gruppenrollen Auswahl">
                {sortedRoles.map((role) => {
                  const isActive = selectedGroupRoleIds.includes(role.roleId);
                  return (
                    <button
                      key={`group-role-${role.roleId}`}
                      type="button"
                      className={`chip chip-action ${isActive ? "active" : ""}`}
                      onClick={() => onToggleGroupRole(role.roleId)}
                    >
                      {roleDisplayName(role)} ({role.roleKey})
                    </button>
                  );
                })}
              </div>

              <div className="action-row">
                <button
                  type="button"
                  className="btn btn-primary"
                  onClick={() => {
                    void onSaveGroupRoles();
                  }}
                  disabled={isSavingGroupRoles}
                >
                  {isSavingGroupRoles ? "Speichern..." : "Gruppenrollen speichern"}
                </button>
              </div>
            </>
          ) : null}
        </section>
      ) : null}
    </section>
  );
}
