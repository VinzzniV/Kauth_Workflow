import { useMemo } from "react";
import type { Department, Role } from "../../types/workflow";
import EmptyState from "../feedback/EmptyState";
import LoadingState from "../feedback/LoadingState";

type Props = {
  roles: Role[];
  departments: Department[];
  selectedDepartmentId: number | null;
  selectedRoleId: number | null;
  isLoading: boolean;
  error: string | null;
  onDepartmentChange: (departmentId: number | null) => void;
  onRoleChange: (roleId: number | null) => void;
  onRetry: () => void;
};

export default function RoleSelection({
  roles,
  departments,
  selectedDepartmentId,
  selectedRoleId,
  isLoading,
  error,
  onDepartmentChange,
  onRoleChange,
  onRetry,
}: Props) {
  const visibleRoles = useMemo(() => {
    if (selectedDepartmentId === null) {
      return [];
    }

    return roles.filter((role) => role.departmentId === selectedDepartmentId && role.isActive);
  }, [roles, selectedDepartmentId]);

  const selectedDepartmentName = useMemo(
    () => departments.find((department) => department.id === selectedDepartmentId)?.name ?? null,
    [departments, selectedDepartmentId]
  );

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Stelle und Abteilung</h2>
        <p>
          Abteilung und Stelle werden als Stammdaten gespeichert. Der konkrete Bedarf wird später von der
          Abteilungsleitung festgelegt.
        </p>
      </div>

      {isLoading ? (
        <LoadingState
          title="Stellen und Abteilungen werden geladen..."
          description="Die Stammdaten werden geladen."
        />
      ) : null}

      {!isLoading && error ? (
        <EmptyState
          title="Stellen oder Abteilungen konnten nicht geladen werden."
          description={error}
          actionLabel="Erneut laden"
          onAction={onRetry}
        />
      ) : null}

      {!isLoading && !error ? (
        <>
          <div className="form-grid">
            <label className="field">
              <span>Abteilung *</span>
              <select
                value={selectedDepartmentId ?? ""}
                onChange={(event) =>
                  onDepartmentChange(event.target.value ? Number(event.target.value) : null)
                }
              >
                <option value="">Bitte auswählen</option>
                {departments.map((department) => (
                  <option key={department.id} value={department.id}>
                    {department.name}
                  </option>
                ))}
              </select>
            </label>

            <label className="field">
              <span>Stelle *</span>
              <select
                value={selectedRoleId ?? ""}
                disabled={selectedDepartmentId === null}
                onChange={(event) => onRoleChange(event.target.value ? Number(event.target.value) : null)}
              >
                <option value="">Bitte auswählen</option>
                {visibleRoles.map((role) => (
                  <option key={role.id} value={role.id}>
                    {role.name}
                  </option>
                ))}
              </select>
            </label>
          </div>

          <p className="panel-note metadata-note">
            {selectedDepartmentName
              ? `Verfügbare Stellen in ${selectedDepartmentName}: ${visibleRoles.length}`
              : "Bitte zuerst eine Abteilung auswählen."}
          </p>
        </>
      ) : null}
    </section>
  );
}
