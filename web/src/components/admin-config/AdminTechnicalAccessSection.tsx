import LoadingState from "../feedback/LoadingState";
import type { AdminGroup, AdminRole, AdminUser } from "../../types/auth";
import { roleDisplayName } from "./adminConfigHelpers";

function formatScopeShort(scope: string, departmentName: string | null | undefined): string {
  return scope === "department" ? `Abteilung: ${departmentName ?? "?"}` : "Global";
}

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
    <div className="content-stack">
      <section className="panel">
        <div className="panel-head">
          <h2>Direkte Rollen und Gruppen</h2>
          <p>
            {selectedUser
              ? `Direkte Zuweisungen für ${selectedUser.displayName} prüfen und bearbeiten.`
              : "Wählen Sie eine Person, um deren direkte Rollen und Gruppenzuordnungen zu prüfen."}
          </p>
        </div>

        {isLoadingTechnicalAccess ? <LoadingState title="Rechte werden geladen..." /> : null}

        {!isLoadingTechnicalAccess && sortedUsers.length > 0 ? (
          <label className="field compact">
            <span>Person für Einzelprüfung</span>
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
      </section>

      {!isLoadingTechnicalAccess && selectedUser ? (
        <div className="content-stack">
          <section className="panel admin-effective-summary">
            <div className="panel-head">
              <h2>Aktuelle Berechtigungen für {selectedUser.displayName}</h2>
              <p>Effektive Wirkung aus direkten Zuweisungen, Gruppen, Standardrechten und Ausnahmen.</p>
            </div>

            <dl className="admin-effective-grid">
              <div>
                <dt>Verzeichnisquelle</dt>
                <dd>
                  {selectedUser.directorySynced
                    ? selectedUser.userPrincipalName ?? selectedUser.directoryDisplayName ?? "Entra synchronisiert"
                    : "Nur lokal"}
                </dd>
              </div>
              <div>
                <dt>Abteilung</dt>
                <dd>
                  {selectedUser.departmentName ?? "keine"}
                  {selectedUser.departmentSource ? ` (${selectedUser.departmentSource}` : ""}
                  {selectedUser.departmentOverrideActive ? ", Override aktiv" : ""}
                  {selectedUser.departmentSource ? ")" : ""}
                </dd>
              </div>
              <div>
                <dt>Effektive Rollen ({selectedUser.effectiveRoles.length})</dt>
                <dd>
                  {selectedUser.effectiveRoles.length > 0 ? (
                    <div className="chips-row">
                      {selectedUser.effectiveRoles.map((role, index) => (
                        <span
                          key={`effective-role-${role.roleId}-${index}`}
                          className="chip chip-active-readonly"
                          title={formatScopeShort(role.scope, role.scopeDepartmentName)}
                        >
                          {role.roleName}
                        </span>
                      ))}
                    </div>
                  ) : (
                    <span className="meta-empty">Keine</span>
                  )}
                </dd>
              </div>
              <div>
                <dt>Effektive Berechtigungen ({selectedUser.effectivePermissions.length})</dt>
                <dd>
                  {selectedUser.effectivePermissions.length > 0 ? (
                    <div className="chips-row">
                      {selectedUser.effectivePermissions.map((permission, index) => (
                        <span
                          key={`effective-permission-${permission.permissionKey}-${index}`}
                          className="chip"
                          title={formatScopeShort(permission.scope, permission.scopeDepartmentName)}
                        >
                          {permission.permissionName}
                        </span>
                      ))}
                    </div>
                  ) : (
                    <span className="meta-empty">Keine</span>
                  )}
                </dd>
              </div>
            </dl>
          </section>

          <section className="panel">
            <div className="panel-head">
              <h2>Direkte Rollen für {selectedUser.displayName}</h2>
              <p>
                {selectedUserRoleIds.length} von {sortedRoles.length}{" "}
                {sortedRoles.length === 1 ? "Rolle" : "Rollen"} zugewiesen. Aktive sind farbig hervorgehoben.
              </p>
            </div>

            {sortedRoles.length > 0 ? (
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
            ) : (
              <p className="panel-note">Keine Rollen im System hinterlegt.</p>
            )}

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
              <h2>Gruppen für {selectedUser.displayName}</h2>
              <p>
                {groups.length === 0
                  ? "Im System sind keine Gruppen definiert. Gruppen lassen sich unter Verzeichnis & Gruppen anlegen oder über Verzeichnis-Sync übernehmen."
                  : `${selectedUserGroupIds.length} von ${groups.length} ${groups.length === 1 ? "Gruppe" : "Gruppen"} zugewiesen.`}
              </p>
            </div>

            {groups.length > 0 ? (
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
            ) : null}

            {groups.length > 0 ? (
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
            ) : null}
          </section>
        </div>
      ) : null}

      {!isLoadingTechnicalAccess && groups.length > 0 ? (
        <section className="panel">
          <div className="panel-head">
            <h2>Rollen je Gruppe</h2>
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
    </div>
  );
}
