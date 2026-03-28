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
    <div className="content-stack">
      <section className="panel">
        <div className="panel-head">
          <h2>Zugriffe & Gruppen</h2>
          <p>Dieser Bereich ist für Login-Rechte, Gruppen und Ausnahmen gedacht und wird bewusst nur bei Bedarf geladen.</p>
        </div>

        <div className="admin-guidance-grid">
          <article className="admin-guidance-card">
            <h3>Wann passt dieser Bereich?</h3>
            <p>Wenn eine Person zusätzliche Zugriffe braucht oder Gruppenrechte gezielt nachgezogen werden sollen.</p>
          </article>
          <article className="admin-guidance-card admin-guidance-card--caution">
            <h3>Worauf achten?</h3>
            <p>Direkte Rollen wirken sofort. Gruppenrollen wirken meist für mehrere Personen gleichzeitig und sollten bewusst eingesetzt werden.</p>
          </article>
        </div>

        {isLoadingTechnicalAccess ? <LoadingState title="Rechte werden geladen..." /> : null}

        {!isLoadingTechnicalAccess && sortedUsers.length > 0 ? (
          <label className="field compact">
            <span>Person für Einzelpflege</span>
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
          <section className="panel">
            <div className="panel-head">
              <h2>Direkte Rollen: {selectedUser.displayName}</h2>
              <p>Nur für gezielte Ausnahmen oder ergänzende Einzelrechte verwenden.</p>
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
              <h2>Gruppen für {selectedUser.displayName}</h2>
              <p>Bevorzugt für wiederkehrende Zugriffe, damit Rechte nicht einzeln nachgepflegt werden müssen.</p>
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
          description="Wählen Sie hier zuerst eine Person, wenn Sie Einzelrechte oder Gruppenzuordnungen prüfen möchten."
        />
      ) : null}

      {!isLoadingTechnicalAccess && groups.length > 0 ? (
        <section className="panel">
          <div className="panel-head">
            <h2>Gruppenrollen</h2>
            <p>Änderungen hier wirken für alle Mitglieder der gewählten Gruppe und sind deshalb bewusst separat geführt.</p>
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
