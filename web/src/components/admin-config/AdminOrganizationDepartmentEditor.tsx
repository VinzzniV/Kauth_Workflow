import type {
  AdminDepartmentAssignment,
  AdminUser,
} from "../../types/auth";
import {
  formatTimestamp,
  userOptionLabel,
} from "./adminConfigHelpers";
import { isEligibleSupervisorSelection } from "./adminWorkspaceModel";
import type { DepartmentDraft } from "./adminOrganizationTypes";

type AdminOrganizationDepartmentEditorProps = {
  selectedDepartment: AdminDepartmentAssignment | null;
  selectedDepartmentDraft: DepartmentDraft | null;
  selectedDepartmentLeadOptions: AdminUser[];
  selectedDepartmentOwnerOptions: AdminUser[];
  eligibleSupervisorUsers: AdminUser[];
  eligibleRequirementOwnerUsers: AdminUser[];
  newDepartmentNameDraft: string;
  isCreatingDepartment: boolean;
  deletingDepartmentId: number | null;
  savingDepartmentId: number | null;
  canSaveDepartment: boolean;
  onNewDepartmentNameChange: (value: string) => void;
  onDepartmentDraftChange: (departmentId: number, draft: DepartmentDraft) => void;
  onCreateDepartment: () => void | Promise<void>;
  onSaveDepartmentAssignment: (departmentId: number) => void | Promise<void>;
  onRemoveDepartment: (department: AdminDepartmentAssignment) => void | Promise<void>;
};

export function AdminOrganizationDepartmentEditor(props: AdminOrganizationDepartmentEditorProps) {
  if (!props.selectedDepartment) {
    return (
      <section className="panel">
        <div className="panel-head">
          <h2>Neue Abteilung</h2>
          <p>Neue Abteilungen entstehen im selben Organisations-Workspace und sind danach direkt verknüpfbar.</p>
        </div>

        <label className="field">
          <span>Name</span>
          <input
            type="text"
            value={props.newDepartmentNameDraft}
            onChange={(event) => props.onNewDepartmentNameChange(event.target.value)}
            placeholder="z. B. Einkauf"
          />
        </label>

        <div className="action-row">
          <button
            type="button"
            className="btn btn-primary"
            onClick={() => {
              void props.onCreateDepartment();
            }}
            disabled={props.isCreatingDepartment || props.newDepartmentNameDraft.trim().length === 0}
          >
            {props.isCreatingDepartment ? "Anlegen..." : "Abteilung anlegen"}
          </button>
        </div>
      </section>
    );
  }

  if (!props.selectedDepartmentDraft) {
    return null;
  }

  const selectedDepartment = props.selectedDepartment;
  const selectedDepartmentDraft = props.selectedDepartmentDraft;

  const hasInvalidDepartmentLeadSelection = !isEligibleSupervisorSelection(
    selectedDepartmentDraft.departmentLeadUserId,
    props.eligibleSupervisorUsers
  );
  const hasInvalidDepartmentOwnerSelection = !isEligibleSupervisorSelection(
    selectedDepartmentDraft.requirementOwnerUserId,
    props.eligibleRequirementOwnerUsers
  );

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Abteilung pflegen: {selectedDepartment.departmentName}</h2>
        <p>Leitung und Anforderungsverantwortung werden bewusst gemeinsam gepflegt.</p>
      </div>

      <div className="form-grid">
        <label className="field">
          <span>Abteilungsleitung</span>
          <select
            value={selectedDepartmentDraft.departmentLeadUserId}
            onChange={(event) =>
              props.onDepartmentDraftChange(selectedDepartment.departmentId, {
                ...selectedDepartmentDraft,
                departmentLeadUserId: event.target.value,
              })
            }
          >
            <option value="">Nicht fest hinterlegt</option>
            {props.selectedDepartmentLeadOptions.map((user) => (
              <option key={`lead-${selectedDepartment.departmentId}-${user.userId}`} value={user.userId}>
                {userOptionLabel(user)}
                {!props.eligibleSupervisorUsers.some((candidate) => candidate.userId === user.userId)
                  ? " | aktuell ungültig"
                  : ""}
              </option>
            ))}
          </select>
        </label>

        <label className="field">
          <span>Anforderungsverantwortliche Person</span>
          <select
            value={selectedDepartmentDraft.requirementOwnerUserId}
            onChange={(event) =>
              props.onDepartmentDraftChange(selectedDepartment.departmentId, {
                ...selectedDepartmentDraft,
                requirementOwnerUserId: event.target.value,
              })
            }
          >
            <option value="">Nicht fest hinterlegt</option>
            {props.selectedDepartmentOwnerOptions.map((user) => (
              <option key={`owner-${selectedDepartment.departmentId}-${user.userId}`} value={user.userId}>
                {userOptionLabel(user)}
                {!props.eligibleRequirementOwnerUsers.some((candidate) => candidate.userId === user.userId)
                  ? " | aktuell ungültig"
                  : ""}
              </option>
            ))}
          </select>
        </label>
      </div>

      <p className="panel-note">
        Aktuell gespeichert: Leitung {selectedDepartment.departmentLeadDisplayName ?? "keine feste Person"} |
        Anforderungsverantwortung {selectedDepartment.requirementOwnerDisplayName ?? "keine feste Person"} |
        Zuletzt gespeichert {formatTimestamp(selectedDepartment.updatedAt)}
      </p>

      {hasInvalidDepartmentLeadSelection || hasInvalidDepartmentOwnerSelection ? (
        <p className="panel-note">
          Ungültige Zuordnung: Für die Leitung ist aktive Supervisor-Berechtigung nötig. Für die
          anforderungsverantwortliche Person reicht ein aktiver Benutzer. Ungültige gespeicherte Personen bleiben
          sichtbar, müssen aber vor dem Speichern ersetzt oder entfernt werden.
        </p>
      ) : null}

      <div className="action-row">
        <button
          type="button"
          className="btn btn-primary"
          onClick={() => {
            void props.onSaveDepartmentAssignment(selectedDepartment.departmentId);
          }}
          disabled={!props.canSaveDepartment}
        >
          {props.savingDepartmentId === selectedDepartment.departmentId ? "Speichern..." : "Zuständigkeit speichern"}
        </button>
        <button
          type="button"
          className="btn btn-secondary"
          onClick={() => {
            void props.onRemoveDepartment(selectedDepartment);
          }}
          disabled={props.deletingDepartmentId === selectedDepartment.departmentId}
        >
          {props.deletingDepartmentId === selectedDepartment.departmentId ? "Löschen..." : "Abteilung löschen"}
        </button>
      </div>
    </section>
  );
}
