import EmptyState from "../feedback/EmptyState";
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
import type { ResponsibilityDraft } from "./adminOrganizationTypes";

type AdminOrganizationResponsibilityEditorProps = {
  selectedResponsibility: AdminResponsibilityOwner | null;
  selectedResponsibilityDraft: ResponsibilityDraft | null;
  sortedDepartments: AdminDepartmentAssignment[];
  sortedUsers: AdminUser[];
  savingResponsibilityId: number | null;
  canSaveResponsibility: boolean;
  onResponsibilityDraftChange: (responsibilityId: number, draft: ResponsibilityDraft) => void;
  onSaveResponsibilityAssignment: (responsibilityId: number) => void | Promise<void>;
};

export function AdminOrganizationResponsibilityEditor(props: AdminOrganizationResponsibilityEditorProps) {
  if (!props.selectedResponsibility) {
    return (
      <section className="panel">
        <EmptyState
          title="Zuständigkeit auswählen"
          description="Wählen Sie links eine fachliche Zuständigkeit, um Person und Bereich zu pflegen."
        />
      </section>
    );
  }

  if (!props.selectedResponsibilityDraft) {
    return null;
  }

  const selectedResponsibility = props.selectedResponsibility;
  const selectedResponsibilityDraft = props.selectedResponsibilityDraft;

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
              props.onResponsibilityDraftChange(selectedResponsibility.responsibilityId, {
                ...selectedResponsibilityDraft,
                departmentId: event.target.value,
              })
            }
          >
            <option value="">Standard-Abteilung verwenden</option>
            {props.sortedDepartments.map((department) => (
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
              props.onResponsibilityDraftChange(selectedResponsibility.responsibilityId, {
                ...selectedResponsibilityDraft,
                appUserId: event.target.value,
              })
            }
          >
            <option value="">Keine feste Person</option>
            {props.sortedUsers.map((user) => (
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
            void props.onSaveResponsibilityAssignment(selectedResponsibility.responsibilityId);
          }}
          disabled={!props.canSaveResponsibility}
        >
          {props.savingResponsibilityId === selectedResponsibility.responsibilityId ? "Speichern..." : "Zuständigkeit speichern"}
        </button>
      </div>
    </section>
  );
}
