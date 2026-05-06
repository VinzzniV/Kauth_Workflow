import { useMemo, useState } from "react";
import type {
  AdminDepartmentAssignment,
  AdminPermission,
  AdminPermissionAuditEntry,
  AdminRole,
  AdminUser,
} from "../../types/auth";
import LoadingState from "../feedback/LoadingState";

export type AdminPermissionOverrideDraft = {
  permissionId: number;
  effect: string;
  scope: string;
  scopeDepartmentId: number | null;
};

type AdminPermissionsView = "standards" | "exceptions" | "all";

type AdminPermissionsSectionProps = {
  roles: AdminRole[];
  permissions: AdminPermission[];
  auditEntries: AdminPermissionAuditEntry[];
  hasMoreAudit: boolean;
  isLoadingMoreAudit: boolean;
  onLoadMoreAudit: () => void | Promise<void>;
  departments: AdminDepartmentAssignment[];
  selectedRoleId: number | null;
  selectedRolePermissionIds: number[];
  selectedUser: AdminUser | null;
  userOverrideDrafts: AdminPermissionOverrideDraft[];
  isLoading: boolean;
  isSavingRolePermissions: boolean;
  isSavingUserOverrides: boolean;
  view?: AdminPermissionsView;
  onSelectRole: (roleId: number | null) => void;
  onToggleRolePermission: (permissionId: number) => void;
  onSaveRolePermissions: () => void | Promise<void>;
  onUserOverrideDraftsChange: (drafts: AdminPermissionOverrideDraft[]) => void;
  onSaveUserOverrides: () => void | Promise<void>;
};

function formatScope(scope: string, departmentName: string | null | undefined): string {
  return scope === "department" ? `Abteilung: ${departmentName ?? "unbekannt"}` : "Global";
}

export function AdminPermissionsSection({
  roles,
  permissions,
  auditEntries,
  hasMoreAudit,
  isLoadingMoreAudit,
  onLoadMoreAudit,
  departments,
  selectedRoleId,
  selectedRolePermissionIds,
  selectedUser,
  userOverrideDrafts,
  isLoading,
  isSavingRolePermissions,
  isSavingUserOverrides,
  view = "all",
  onSelectRole,
  onToggleRolePermission,
  onSaveRolePermissions,
  onUserOverrideDraftsChange,
  onSaveUserOverrides,
}: AdminPermissionsSectionProps) {
  const showStandards = view === "all" || view === "standards";
  const showExceptions = view === "all" || view === "exceptions";
  const showAudit = view === "all" || view === "standards";
  const [newOverridePermissionId, setNewOverridePermissionId] = useState<string>("");
  const [newOverrideEffect, setNewOverrideEffect] = useState<string>("allow");
  const [newOverrideScope, setNewOverrideScope] = useState<string>("global");
  const [newOverrideScopeDepartmentId, setNewOverrideScopeDepartmentId] = useState<string>("");

  const sortedPermissions = useMemo(
    () =>
      permissions
        .slice()
        .sort((left, right) => `${left.category}|${left.permissionName}`.localeCompare(`${right.category}|${right.permissionName}`, "de")),
    [permissions]
  );

  const selectedRole = roles.find((role) => role.roleId === selectedRoleId) ?? null;

  if (isLoading) {
    return <LoadingState title="Berechtigungen werden geladen..." />;
  }

  const addOverride = () => {
    const permissionId = Number(newOverridePermissionId);
    if (!permissionId) {
      return;
    }

    const nextScopeDepartmentId =
      newOverrideScope === "department" && newOverrideScopeDepartmentId
        ? Number(newOverrideScopeDepartmentId)
        : null;

    const nextDraft: AdminPermissionOverrideDraft = {
      permissionId,
      effect: newOverrideEffect,
      scope: newOverrideScope,
      scopeDepartmentId: nextScopeDepartmentId,
    };

    const exists = userOverrideDrafts.some(
      (item) =>
        item.permissionId === nextDraft.permissionId
        && item.effect === nextDraft.effect
        && item.scope === nextDraft.scope
        && item.scopeDepartmentId === nextDraft.scopeDepartmentId
    );
    if (exists) {
      return;
    }

    onUserOverrideDraftsChange([...userOverrideDrafts, nextDraft]);
    setNewOverridePermissionId("");
    setNewOverrideEffect("allow");
    setNewOverrideScope("global");
    setNewOverrideScopeDepartmentId("");
  };

  const removeOverride = (overrideToRemove: AdminPermissionOverrideDraft) => {
    onUserOverrideDraftsChange(
      userOverrideDrafts.filter(
        (item) =>
          !(
            item.permissionId === overrideToRemove.permissionId
            && item.effect === overrideToRemove.effect
            && item.scope === overrideToRemove.scope
            && item.scopeDepartmentId === overrideToRemove.scopeDepartmentId
          )
      )
    );
  };

  return (
    <section className="panel">
      {view === "all" ? (
        <div className="panel-head">
          <h2>Standardrechte und gezielte Ausnahmen</h2>
        </div>
      ) : null}

      <div className="content-stack">
        {showStandards ? (
        <section className="panel panel-muted">
          <div className="panel-head">
            <h2>Rollen als Standardzugriff</h2>
          </div>

          <div className="action-row">
            <select
              value={selectedRoleId ?? ""}
              onChange={(event) => onSelectRole(event.target.value ? Number(event.target.value) : null)}
            >
              <option value="">Rolle wählen</option>
              {roles.map((role) => (
                <option key={`permission-role-${role.roleId}`} value={role.roleId}>
                  {role.roleName} ({role.roleKey})
                </option>
              ))}
            </select>
            <button
              type="button"
              className="btn btn-secondary"
              disabled={!selectedRole || isSavingRolePermissions}
              onClick={() => {
                void onSaveRolePermissions();
              }}
            >
              {isSavingRolePermissions ? "Speichert..." : "Standardrechte speichern"}
            </button>
          </div>

          {selectedRole ? (
            <>
              <p className="panel-note">
                Aktuelle Berechtigungen für {selectedRole.roleName}: {selectedRole.permissions.map((permission) => permission.permissionName).join(", ") || "keine"}
              </p>
              <div className="chips-row">
                {sortedPermissions.map((permission) => {
                  const isActive = selectedRolePermissionIds.includes(permission.permissionId);
                  return (
                    <button
                      key={`role-permission-${selectedRole.roleId}-${permission.permissionId}`}
                      type="button"
                      className={`chip chip-action ${isActive ? "active" : ""}`}
                      onClick={() => onToggleRolePermission(permission.permissionId)}
                    >
                      {permission.permissionName}
                    </button>
                  );
                })}
              </div>
            </>
          ) : null}
        </section>
        ) : null}

        {showExceptions ? (
        <section className="panel panel-muted">
          <div className="panel-head">
            <h2>Gezielte Ausnahmen für einzelne Personen</h2>
          </div>

          {!selectedUser ? <p className="panel-note">Wählen Sie oben zuerst eine Person aus.</p> : null}

          {selectedUser ? (
            <>
              <p className="panel-note">
                Definieren Sie hier explizite Erlaube/Entziehe-Ausnahmen, die zusätzlich zu Rollen und Gruppen wirken.
                Die aktuell wirksamen Rollen und Berechtigungen sind oben unter „Aktuelle Berechtigungen" zu sehen.
              </p>

              <div className="form-grid">
                <label className="field">
                  <span>Berechtigung</span>
                  <select
                    value={newOverridePermissionId}
                    onChange={(event) => setNewOverridePermissionId(event.target.value)}
                  >
                    <option value="">Berechtigung wählen</option>
                    {sortedPermissions.map((permission) => (
                      <option key={`override-permission-${permission.permissionId}`} value={permission.permissionId}>
                        {permission.permissionName} ({permission.permissionKey})
                      </option>
                    ))}
                  </select>
                </label>

                <label className="field">
                  <span>Effekt</span>
                  <select value={newOverrideEffect} onChange={(event) => setNewOverrideEffect(event.target.value)}>
                    <option value="allow">Erlauben</option>
                    <option value="deny">Entziehen</option>
                  </select>
                </label>

                <label className="field">
                  <span>Geltungsbereich</span>
                  <select value={newOverrideScope} onChange={(event) => setNewOverrideScope(event.target.value)}>
                    <option value="global">Global</option>
                    <option value="department">Abteilung</option>
                  </select>
                </label>

                {newOverrideScope === "department" ? (
                  <label className="field">
                    <span>Abteilung</span>
                    <select
                      value={newOverrideScopeDepartmentId}
                      onChange={(event) => setNewOverrideScopeDepartmentId(event.target.value)}
                    >
                      <option value="">Abteilung wählen</option>
                      {departments.map((department) => (
                        <option key={`override-department-${department.departmentId}`} value={department.departmentId}>
                          {department.departmentName}
                        </option>
                      ))}
                    </select>
                  </label>
                ) : null}
              </div>

              <div className="action-row">
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={addOverride}
                  disabled={!newOverridePermissionId || (newOverrideScope === "department" && !newOverrideScopeDepartmentId)}
                >
                  Ausnahme hinzufügen
                </button>
                <button
                  type="button"
                  className="btn btn-primary"
                  onClick={() => {
                    void onSaveUserOverrides();
                  }}
                  disabled={isSavingUserOverrides}
                >
                  {isSavingUserOverrides ? "Speichert..." : "Ausnahmen speichern"}
                </button>
              </div>

              <div className="chips-row">
                {userOverrideDrafts.length === 0 ? <span className="panel-note">Keine individuellen Ausnahmen.</span> : null}
                {userOverrideDrafts.map((overrideDraft) => {
                  const permission = permissions.find((item) => item.permissionId === overrideDraft.permissionId);
                  const departmentName =
                    departments.find((item) => item.departmentId === overrideDraft.scopeDepartmentId)?.departmentName ?? null;

                  return (
                    <button
                      key={`user-override-${overrideDraft.permissionId}-${overrideDraft.effect}-${overrideDraft.scope}-${overrideDraft.scopeDepartmentId ?? "global"}`}
                      type="button"
                      className={`chip chip-action ${overrideDraft.effect === "deny" ? "" : "active"}`}
                      onClick={() => removeOverride(overrideDraft)}
                      title="Override entfernen"
                    >
                      {overrideDraft.effect.toUpperCase()}: {permission?.permissionName ?? overrideDraft.permissionId} (
                      {formatScope(overrideDraft.scope, departmentName)})
                    </button>
                  );
                })}
              </div>
            </>
          ) : null}
        </section>
        ) : null}

        {showAudit && auditEntries.length > 0 ? (
          <section className="panel panel-muted">
            <div className="panel-head">
              <h2>Änderungsprotokoll</h2>
            </div>
            <table className="table">
              <thead>
                <tr>
                  <th>Zeitpunkt</th>
                  <th>Aktion</th>
                  <th>Entität</th>
                  <th>Akteur</th>
                  <th>Detail</th>
                </tr>
              </thead>
              <tbody>
                {auditEntries.map((entry) => (
                  <tr key={entry.auditEntryId}>
                    <td>{new Date(entry.createdAt).toLocaleString("de-DE")}</td>
                    <td>{entry.eventType}</td>
                    <td>{entry.entityType}</td>
                    <td>{entry.actorDisplayName ?? "System"}</td>
                    <td>{entry.detail ?? "-"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            {hasMoreAudit ? (
              <div style={{ padding: "0.75rem 1rem" }}>
                <button
                  className="btn btn-secondary btn-sm"
                  onClick={() => void onLoadMoreAudit()}
                  disabled={isLoadingMoreAudit}
                >
                  {isLoadingMoreAudit ? "Wird geladen…" : "Mehr laden"}
                </button>
              </div>
            ) : null}
          </section>
        ) : null}
      </div>
    </section>
  );
}
