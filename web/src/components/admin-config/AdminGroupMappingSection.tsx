import { useMemo, useState } from "react";
import type {
  AdminDepartmentAssignment,
  AdminDirectoryGroup,
  AdminRole,
} from "../../types/auth";
import EmptyState from "../feedback/EmptyState";
import LoadingState from "../feedback/LoadingState";
import { roleDisplayName } from "./adminConfigHelpers";

type AdminGroupMappingSectionProps = {
  groups: AdminDirectoryGroup[];
  roles: AdminRole[];
  departments: AdminDepartmentAssignment[];
  isLoading: boolean;
  savingGroupId: number | null;
  deletingMappingId: number | null;
  onCreateMapping: (
    directoryGroupId: number,
    appRoleId: number,
    scope: string,
    scopeDepartmentId: number | null
  ) => void | Promise<void>;
  onDeleteMapping: (mappingId: number) => void | Promise<void>;
};

function formatMappingScope(scope: string, departmentName: string | null | undefined): string {
  return scope === "department" ? `Abteilung: ${departmentName ?? "unbekannt"}` : "Global";
}

export function AdminGroupMappingSection({
  groups,
  roles,
  departments,
  isLoading,
  savingGroupId,
  deletingMappingId,
  onCreateMapping,
  onDeleteMapping,
}: AdminGroupMappingSectionProps) {
  const [roleDrafts, setRoleDrafts] = useState<Record<number, string>>({});
  const [scopeDrafts, setScopeDrafts] = useState<Record<number, string>>({});
  const [scopeDepartmentDrafts, setScopeDepartmentDrafts] = useState<Record<number, string>>({});

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
            const selectedScope = scopeDrafts[group.directoryGroupId] ?? "global";
            const selectedScopeDepartmentId =
              selectedScope === "department"
                ? Number(scopeDepartmentDrafts[group.directoryGroupId] ?? "")
                : null;
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
                          {mapping.appRoleName}{" "}
                          <span>
                            (
                            {formatMappingScope(
                              mapping.scope,
                              departments.find((department) => department.departmentId === mapping.scopeDepartmentId)?.departmentName
                            )}
                            )
                          </span>
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
                    <select
                      value={selectedScope}
                      onChange={(event) =>
                        setScopeDrafts((current) => ({
                          ...current,
                          [group.directoryGroupId]: event.target.value,
                        }))
                      }
                    >
                      <option value="global">Global</option>
                      <option value="department">Abteilung</option>
                    </select>
                    {selectedScope === "department" ? (
                      <select
                        value={scopeDepartmentDrafts[group.directoryGroupId] ?? ""}
                        onChange={(event) =>
                          setScopeDepartmentDrafts((current) => ({
                            ...current,
                            [group.directoryGroupId]: event.target.value,
                          }))
                        }
                      >
                        <option value="">Abteilung wählen</option>
                        {departments.map((department) => (
                          <option key={`directory-mapping-department-${department.departmentId}`} value={department.departmentId}>
                            {department.departmentName}
                          </option>
                        ))}
                      </select>
                    ) : null}

                    <button
                      type="button"
                      className="btn btn-secondary"
                      disabled={isSaving || !selectedRoleId || (selectedScope === "department" && !selectedScopeDepartmentId)}
                      onClick={() => {
                        void Promise.resolve(
                          onCreateMapping(group.directoryGroupId, selectedRoleId, selectedScope, selectedScopeDepartmentId)
                        ).then(() => {
                          setRoleDrafts((current) => ({ ...current, [group.directoryGroupId]: "" }));
                          setScopeDrafts((current) => ({ ...current, [group.directoryGroupId]: "global" }));
                          setScopeDepartmentDrafts((current) => ({ ...current, [group.directoryGroupId]: "" }));
                        });
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
