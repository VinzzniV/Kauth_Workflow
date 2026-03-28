import { useMemo, useState } from "react";
import type {
  AdminDirectoryGroup,
  AdminRole,
} from "../../types/auth";
import EmptyState from "../feedback/EmptyState";
import LoadingState from "../feedback/LoadingState";
import { roleDisplayName } from "./adminConfigHelpers";

type AdminGroupMappingSectionProps = {
  groups: AdminDirectoryGroup[];
  roles: AdminRole[];
  isLoading: boolean;
  savingGroupId: number | null;
  deletingMappingId: number | null;
  onCreateMapping: (directoryGroupId: number, appRoleId: number) => void | Promise<void>;
  onDeleteMapping: (mappingId: number) => void | Promise<void>;
};

export function AdminGroupMappingSection({
  groups,
  roles,
  isLoading,
  savingGroupId,
  deletingMappingId,
  onCreateMapping,
  onDeleteMapping,
}: AdminGroupMappingSectionProps) {
  const [roleDrafts, setRoleDrafts] = useState<Record<number, string>>({});

  const sortedRoles = useMemo(
    () =>
      roles
        .slice()
        .sort((left, right) => roleDisplayName(left).localeCompare(roleDisplayName(right), "de")),
    [roles]
  );

  if (isLoading) {
    return <LoadingState title="Gruppen-Mappings werden geladen..." />;
  }

  if (groups.length === 0) {
    return (
      <EmptyState
        title="Keine Verzeichnisgruppen vorhanden"
        description="Nach einer Synchronisierung können hier Entra-Gruppen mit App-Rollen verknüpft werden."
      />
    );
  }

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Gruppen mit Rollen verbinden</h2>
        <p>
          Jede Verzeichnisgruppe kann eine oder mehrere App-Rollen erhalten. Die Rolle wird beim Login über die Gruppenmitgliedschaft wirksam.
        </p>
      </div>

      <p className="panel-note">
        Eine Gruppen-Rollen-Zuordnung gilt für alle Mitglieder der jeweiligen Gruppe. Neue Verknüpfungen deshalb zuerst in kleineren Gruppen oder nach einem gezielten Sync prüfen.
      </p>

      <table className="table">
        <thead>
          <tr>
            <th>Verzeichnisgruppe</th>
            <th>Mitglieder</th>
            <th>Aktuelle Rollen</th>
            <th>Neue Rolle verknüpfen</th>
          </tr>
        </thead>
        <tbody>
          {groups.map((group) => {
            const selectedRoleId = Number(roleDrafts[group.directoryGroupId] ?? "");
            const isSaving = savingGroupId === group.directoryGroupId;

            return (
              <tr key={group.directoryGroupId}>
                <td>
                  <strong>{group.displayName}</strong>
                  <div className="panel-note">{group.externalGroupId}</div>
                  {group.description ? <div className="panel-note">{group.description}</div> : null}
                </td>
                <td>{group.memberCount}</td>
                <td>
                  {group.roleMappings.length === 0 ? (
                    <span className="panel-note">Noch keine Rollen verknüpft.</span>
                  ) : (
                    <div className="chips-row">
                      {group.roleMappings.map((mapping) => (
                        <button
                          key={mapping.mappingId}
                          type="button"
                          className="chip chip-action active"
                          disabled={deletingMappingId === mapping.mappingId}
                          onClick={() => {
                            void onDeleteMapping(mapping.mappingId);
                          }}
                          title="Mapping entfernen"
                        >
                          {mapping.roleDepartmentName ? `${mapping.roleDepartmentName} / ` : ""}
                          {mapping.appRoleName}
                        </button>
                      ))}
                    </div>
                  )}
                </td>
                <td>
                  <div className="action-row">
                    <select
                      value={roleDrafts[group.directoryGroupId] ?? ""}
                      onChange={(event) =>
                        setRoleDrafts((current) => ({
                          ...current,
                          [group.directoryGroupId]: event.target.value,
                        }))
                      }
                    >
                      <option value="">Rolle wählen</option>
                      {sortedRoles.map((role) => (
                        <option key={`directory-role-${group.directoryGroupId}-${role.roleId}`} value={role.roleId}>
                          {roleDisplayName(role)} ({role.roleKey})
                        </option>
                      ))}
                    </select>

                    <button
                      type="button"
                      className="btn btn-secondary"
                      disabled={isSaving || !selectedRoleId}
                      onClick={() => {
                        void onCreateMapping(group.directoryGroupId, selectedRoleId);
                      }}
                    >
                      {isSaving ? "Speichert..." : "Rolle verknüpfen"}
                    </button>
                  </div>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </section>
  );
}
