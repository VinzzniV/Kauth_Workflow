import type {
  AdminDepartmentAssignment,
  AdminResponsibilityOwner,
  AdminUser,
} from "../../types/auth";
import {
  formatTimestamp,
  responsibilityAreaLabel,
  responsibilityTypeLabel,
  userOptionLabel,
} from "./adminConfigHelpers";

type AdminResponsibilitiesSectionProps = {
  sortedResponsibilities: AdminResponsibilityOwner[];
  sortedDepartments: AdminDepartmentAssignment[];
  sortedUsers: AdminUser[];
  responsibilityDrafts: Record<number, { appUserId: string; departmentId: string }>;
  savingResponsibilityId: number | null;
  onResponsibilityDraftChange: (
    responsibilityId: number,
    draft: { appUserId: string; departmentId: string }
  ) => void;
  onSaveResponsibilityAssignment: (responsibilityId: number) => void | Promise<void>;
};

export function AdminResponsibilitiesSection({
  sortedResponsibilities,
  sortedDepartments,
  sortedUsers,
  responsibilityDrafts,
  savingResponsibilityId,
  onResponsibilityDraftChange,
  onSaveResponsibilityAssignment,
}: AdminResponsibilitiesSectionProps) {
  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Systemverantwortliche und Zuständigkeiten</h2>
        <p>Pflegen Sie je Zuständigkeit die verantwortliche Person oder Abteilung. Ohne feste Person bleibt die Aufgabe bei der gewählten Abteilung.</p>
      </div>

      <div className="dashboard-grid" aria-label="Fachliche Zuständigkeiten">
        {sortedResponsibilities.map((item) => (
          <article key={item.responsibilityId} className="dashboard-card">
            <div>
              <h2>{item.responsibilityName}</h2>
              <p>
                {responsibilityAreaLabel(item)} | {responsibilityTypeLabel(item)}
              </p>
            </div>

            <label className="field compact">
              <span>Zuständige Abteilung</span>
              <select
                value={responsibilityDrafts[item.responsibilityId]?.departmentId ?? ""}
                onChange={(event) =>
                  onResponsibilityDraftChange(item.responsibilityId, {
                    appUserId: responsibilityDrafts[item.responsibilityId]?.appUserId ?? "",
                    departmentId: event.target.value,
                  })
                }
              >
                <option value="">Standard-Abteilung verwenden</option>
                {sortedDepartments.map((department) => (
                  <option
                    key={`responsibility-department-${item.responsibilityId}-${department.departmentId}`}
                    value={department.departmentId}
                  >
                    {department.departmentName}
                  </option>
                ))}
              </select>
            </label>

            <label className="field compact">
              <span>Zuständige Person</span>
              <select
                value={responsibilityDrafts[item.responsibilityId]?.appUserId ?? ""}
                onChange={(event) =>
                  onResponsibilityDraftChange(item.responsibilityId, {
                    appUserId: event.target.value,
                    departmentId: responsibilityDrafts[item.responsibilityId]?.departmentId ?? "",
                  })
                }
              >
                <option value="">Keine feste Person</option>
                {sortedUsers.map((user) => (
                  <option key={`responsibility-${item.responsibilityId}-${user.userId}`} value={user.userId}>
                    {userOptionLabel(user)}
                  </option>
                ))}
              </select>
            </label>

            <p className="panel-note">
              Aktuell: {item.appUserDisplayName ?? "keine feste Person"} | Abteilung: {item.departmentName ?? "Standard"} | Zuletzt gespeichert:{" "}
              {formatTimestamp(item.updatedAt)}
            </p>

            <button
              type="button"
              className="btn btn-primary"
              onClick={() => {
                void onSaveResponsibilityAssignment(item.responsibilityId);
              }}
              disabled={savingResponsibilityId === item.responsibilityId}
            >
              {savingResponsibilityId === item.responsibilityId ? "Speichern..." : "Zuständigkeit speichern"}
            </button>
          </article>
        ))}
      </div>
    </section>
  );
}
