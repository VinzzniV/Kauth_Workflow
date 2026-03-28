import type {
  AdminDepartmentAssignment,
  AdminResponsibilityOwner,
  AdminUser,
} from "../../types/auth";
import type {
  getDepartmentRelations,
  getResponsibilityRelations,
  getUserRelations,
} from "./adminWorkspaceModel";
import type { AdminOrganizationEntity } from "./adminWorkspaceModel";

interface AdminOrganizationRelationsPanelProps {
  organizationEntity: AdminOrganizationEntity;
  selectedUser: AdminUser | null;
  userRelations: ReturnType<typeof getUserRelations> | null;
  selectedDepartment: AdminDepartmentAssignment | null;
  departmentRelations: ReturnType<typeof getDepartmentRelations> | null;
  selectedResponsibility: AdminResponsibilityOwner | null;
  responsibilityRelations: ReturnType<typeof getResponsibilityRelations> | null;
  onSelectOrganizationEntity: (entity: AdminOrganizationEntity, id: number | null) => void;
}

export default function AdminOrganizationRelationsPanel({
  organizationEntity,
  selectedUser,
  userRelations,
  selectedDepartment,
  departmentRelations,
  selectedResponsibility,
  responsibilityRelations,
  onSelectOrganizationEntity,
}: AdminOrganizationRelationsPanelProps) {
  if (organizationEntity === "user") {
    if (!selectedUser || !userRelations) {
      return (
        <section className="panel">
          <div className="panel-head">
            <h2>Wird verwendet in ...</h2>
          </div>
        </section>
      );
    }

    return (
      <section className="panel">
        <div className="panel-head">
          <h2>Wird verwendet in ...</h2>
        </div>

        <div className="content-stack">
          <div>
            <h3 className="panel-title">Abteilung</h3>
            {selectedUser.departmentId ? (
              <div className="admin-relation-list">
                <button
                  type="button"
                  className="admin-relation-link"
                  onClick={() => onSelectOrganizationEntity("department", selectedUser.departmentId)}
                >
                  {selectedUser.departmentName ?? "Unbekannte Abteilung"}
                </button>
              </div>
            ) : (
              <p className="panel-note">Keine Abteilung hinterlegt.</p>
            )}
          </div>

          <div>
            <h3 className="panel-title">Als Leitung verwendet</h3>
            {userRelations.ledDepartments.length > 0 ? (
              <div className="admin-relation-list">
                {userRelations.ledDepartments.map((department) => (
                  <button
                    key={`led-department-${department.departmentId}`}
                    type="button"
                    className="admin-relation-link"
                    onClick={() => onSelectOrganizationEntity("department", department.departmentId)}
                  >
                    {department.departmentName}
                  </button>
                ))}
              </div>
            ) : (
              <p className="panel-note">Keine Abteilungen mit dieser Person als Leitung.</p>
            )}
          </div>

          <div>
            <h3 className="panel-title">Als Anforderungsverantwortung verwendet</h3>
            {userRelations.requirementDepartments.length > 0 ? (
              <div className="admin-relation-list">
                {userRelations.requirementDepartments.map((department) => (
                  <button
                    key={`owner-department-${department.departmentId}`}
                    type="button"
                    className="admin-relation-link"
                    onClick={() => onSelectOrganizationEntity("department", department.departmentId)}
                  >
                    {department.departmentName}
                  </button>
                ))}
              </div>
            ) : (
              <p className="panel-note">Keine Abteilungen mit dieser Person als Anforderungsverantwortung.</p>
            )}
          </div>

          <div>
            <h3 className="panel-title">Feste Zuständigkeiten</h3>
            {userRelations.responsibilities.length > 0 ? (
              <div className="admin-relation-list">
                {userRelations.responsibilities.map((responsibility) => (
                  <button
                    key={`responsibility-${responsibility.responsibilityId}`}
                    type="button"
                    className="admin-relation-link"
                    onClick={() =>
                      onSelectOrganizationEntity("responsibility", responsibility.responsibilityId)
                    }
                  >
                    {responsibility.responsibilityName}
                  </button>
                ))}
              </div>
            ) : (
              <p className="panel-note">Keine festen Zuständigkeiten auf diese Person.</p>
            )}
          </div>
        </div>
      </section>
    );
  }

  if (organizationEntity === "department") {
    if (!selectedDepartment || !departmentRelations) {
      return (
        <section className="panel">
          <div className="panel-head">
            <h2>Verknüpfte Organisation</h2>
          </div>
        </section>
      );
    }

    return (
      <section className="panel">
        <div className="panel-head">
          <h2>Verknüpfte Organisation</h2>
        </div>

        <div className="content-stack">
          <div>
            <h3 className="panel-title">Personen in dieser Abteilung</h3>
            {departmentRelations.users.length > 0 ? (
              <div className="admin-relation-list">
                {departmentRelations.users.map((user) => (
                  <button
                    key={`department-user-${user.userId}`}
                    type="button"
                    className="admin-relation-link"
                    onClick={() => onSelectOrganizationEntity("user", user.userId)}
                  >
                    {user.displayName}
                  </button>
                ))}
              </div>
            ) : (
              <p className="panel-note">Noch keine Personen dieser Abteilung zugeordnet.</p>
            )}
          </div>

          <div>
            <h3 className="panel-title">Fachliche Zuständigkeiten</h3>
            {departmentRelations.responsibilities.length > 0 ? (
              <div className="admin-relation-list">
                {departmentRelations.responsibilities.map((responsibility) => (
                  <button
                    key={`department-responsibility-${responsibility.responsibilityId}`}
                    type="button"
                    className="admin-relation-link"
                    onClick={() =>
                      onSelectOrganizationEntity("responsibility", responsibility.responsibilityId)
                    }
                  >
                    {responsibility.responsibilityName}
                  </button>
                ))}
              </div>
            ) : (
              <p className="panel-note">Keine fachlichen Zuständigkeiten dieser Abteilung zugeordnet.</p>
            )}
          </div>
        </div>
      </section>
    );
  }

  if (!selectedResponsibility || !responsibilityRelations) {
    return (
      <section className="panel">
        <div className="panel-head">
          <h2>Verknüpfte Organisation</h2>
        </div>
      </section>
    );
  }

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Verknüpfte Organisation</h2>
      </div>

      <div className="content-stack">
        <div>
          <h3 className="panel-title">Bereich</h3>
          {responsibilityRelations.department ? (
            <div className="admin-relation-list">
              <button
                type="button"
                className="admin-relation-link"
                onClick={() =>
                  onSelectOrganizationEntity("department", responsibilityRelations.department!.departmentId)
                }
              >
                {responsibilityRelations.department.departmentName}
              </button>
            </div>
          ) : (
            <p className="panel-note">Kein Bereich fest hinterlegt.</p>
          )}
        </div>

        <div>
          <h3 className="panel-title">Feste Person</h3>
          {responsibilityRelations.user ? (
            <div className="admin-relation-list">
              <button
                type="button"
                className="admin-relation-link"
                onClick={() => onSelectOrganizationEntity("user", responsibilityRelations.user!.userId)}
              >
                {responsibilityRelations.user.displayName}
              </button>
            </div>
          ) : (
            <p className="panel-note">Keine feste Person hinterlegt.</p>
          )}
        </div>

        {selectedResponsibility.systemKey ? (
          <div>
            <h3 className="panel-title">System-Key</h3>
            <p className="panel-note">{selectedResponsibility.systemKey}</p>
          </div>
        ) : null}
      </div>
    </section>
  );
}
