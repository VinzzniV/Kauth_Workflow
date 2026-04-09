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
import type { NewResponsibilityDraft, ResponsibilityDraft } from "./adminOrganizationTypes";

type AdminOrganizationResponsibilityEditorProps = {
  newResponsibilityDraft: NewResponsibilityDraft;
  selectedResponsibility: AdminResponsibilityOwner | null;
  selectedResponsibilityDraft: ResponsibilityDraft | null;
  sortedDepartments: AdminDepartmentAssignment[];
  sortedUsers: AdminUser[];
  isCreatingResponsibility: boolean;
  deletingResponsibilityId: number | null;
  savingResponsibilityId: number | null;
  canSaveResponsibility: boolean;
  onNewResponsibilityDraftChange: (draft: NewResponsibilityDraft) => void;
  onCreateResponsibility: () => void | Promise<AdminResponsibilityOwner | null> | AdminResponsibilityOwner | null;
  onResponsibilityDraftChange: (responsibilityId: number, draft: ResponsibilityDraft) => void;
  onRemoveResponsibility: (responsibility: AdminResponsibilityOwner) => void | Promise<boolean> | boolean;
  onSaveResponsibilityAssignment: (responsibilityId: number) => void | Promise<void>;
};

export function AdminOrganizationResponsibilityEditor(props: AdminOrganizationResponsibilityEditorProps) {
  const selectedResponsibility = props.selectedResponsibility;
  const selectedResponsibilityDraft = props.selectedResponsibilityDraft;

  return (
    <div className="content-stack">
      <section className="panel">
        <div className="panel-head">
          <h2>Neue Zuständigkeit</h2>
          <p>
            Der Name bleibt bewusst fachlich kurz. Die Abteilung wird separat über das Feld
            &nbsp;&quot;Zuständige Abteilung&quot; zugeordnet und später daraus angezeigt.
          </p>
        </div>

        <div className="form-grid">
          <label className="field">
            <span>Name der Zuständigkeit</span>
            <input
              type="text"
              value={props.newResponsibilityDraft.responsibilityName}
              onChange={(event) =>
                props.onNewResponsibilityDraftChange({
                  ...props.newResponsibilityDraft,
                  responsibilityName: event.target.value,
                })
              }
              placeholder="z. B. AD, Hardware, Mailbox"
            />
          </label>

          <label className="field">
            <span>Zuständige Abteilung</span>
            <select
              value={props.newResponsibilityDraft.departmentId}
              onChange={(event) =>
                props.onNewResponsibilityDraftChange({
                  ...props.newResponsibilityDraft,
                  departmentId: event.target.value,
                })
              }
            >
              <option value="">Noch keine feste Abteilung</option>
              {props.sortedDepartments.map((department) => (
                <option key={`new-responsibility-department-${department.departmentId}`} value={department.departmentId}>
                  {department.departmentName}
                </option>
              ))}
            </select>
          </label>
        </div>

        <div className="action-row">
          <button
            type="button"
            className="btn btn-primary"
            onClick={() => {
              void props.onCreateResponsibility();
            }}
            disabled={
              props.isCreatingResponsibility || props.newResponsibilityDraft.responsibilityName.trim().length === 0
            }
          >
            {props.isCreatingResponsibility ? "Anlegen..." : "Zuständigkeit anlegen"}
          </button>
        </div>
      </section>

      {!selectedResponsibility ? (
        <section className="panel">
          <EmptyState
            title="Zuständigkeit auswählen"
            description="Wählen Sie links eine fachliche Zuständigkeit, um Person und Bereich zu pflegen oder zu löschen."
          />
        </section>
      ) : selectedResponsibilityDraft ? (
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
              {props.savingResponsibilityId === selectedResponsibility.responsibilityId
                ? "Speichern..."
                : "Zuständigkeit speichern"}
            </button>
            <button
              type="button"
              className="btn btn-danger"
              onClick={() => {
                void props.onRemoveResponsibility(selectedResponsibility);
              }}
              disabled={props.deletingResponsibilityId === selectedResponsibility.responsibilityId}
            >
              {props.deletingResponsibilityId === selectedResponsibility.responsibilityId
                ? "Löschen..."
                : "Zuständigkeit löschen"}
            </button>
          </div>
        </section>
      ) : null}
    </div>
  );
}
