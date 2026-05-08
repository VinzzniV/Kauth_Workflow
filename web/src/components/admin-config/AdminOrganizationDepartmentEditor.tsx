import { useEffect, useRef } from "react";
import type {
  AdminDepartmentAssignment,
  AdminRole,
  AdminUser,
} from "../../types/auth";
import {
  formatTimestamp,
  userOptionLabel,
} from "./adminConfigHelpers";
import { isEligibleSupervisorSelection } from "./adminWorkspaceModel";
import type { DepartmentDraft, PositionDraft } from "./adminOrganizationTypes";
import { AdminDepartmentEntraImportSection } from "./AdminDepartmentEntraImportSection";

type AdminOrganizationDepartmentEditorProps = {
  selectedDepartment: AdminDepartmentAssignment | null;
  selectedDepartmentDraft: DepartmentDraft | null;
  selectedDepartmentPositions: AdminRole[];
  positionDrafts: Record<number, PositionDraft>;
  selectedDepartmentLeadOptions: AdminUser[];
  selectedDepartmentOwnerOptions: AdminUser[];
  eligibleSupervisorUsers: AdminUser[];
  eligibleRequirementOwnerUsers: AdminUser[];
  newDepartmentNameDraft: string;
  newPositionNameDraft: string;
  isCreatingDepartment: boolean;
  creatingPositionDepartmentId: number | null;
  deletingDepartmentId: number | null;
  deletingPositionId: number | null;
  savingDepartmentId: number | null;
  savingPositionId: number | null;
  canSaveDepartment: boolean;
  onNewDepartmentNameChange: (value: string) => void;
  onNewPositionNameChange: (value: string) => void;
  onDepartmentDraftChange: (departmentId: number, draft: DepartmentDraft) => void;
  onCreateDepartment: () => void | Promise<void>;
  onCreateDepartmentPosition: (departmentId: number) => void | Promise<void>;
  onSaveDepartmentAssignment: (departmentId: number) => void | Promise<void>;
  onRemoveDepartment: (department: AdminDepartmentAssignment) => void | Promise<void>;
  onPositionDraftChange: (positionId: number, draft: PositionDraft) => void;
  onSaveDepartmentPosition: (positionId: number) => void | Promise<void>;
  onRemoveDepartmentPosition: (position: AdminRole) => void | Promise<void>;
};

function toSyncStateLabel(syncState: string): string {
  switch (syncState) {
    case "resolved":
      return "Entra synchronisiert";
    case "missing":
      return "Entra-Zuordnung fehlt";
    case "conflict":
      return "Entra-Konflikt";
    default:
      return "Manuell gepflegt";
  }
}

export function AdminOrganizationDepartmentEditor(props: AdminOrganizationDepartmentEditorProps) {
  const panelRef = useRef<HTMLElement | null>(null);
  const selectedDepartmentId = props.selectedDepartment?.departmentId ?? null;

  useEffect(() => {
    if (selectedDepartmentId === null) {
      return;
    }

    const panel = panelRef.current;
    if (panel && typeof panel.scrollIntoView === "function") {
      panel.scrollIntoView({ behavior: "smooth", block: "start" });
    }
  }, [selectedDepartmentId]);

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
  ) && Boolean(selectedDepartmentDraft.requirementOwnerUserId);

  return (
    <section
      ref={panelRef}
      className="panel"
    >
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

      <p className="panel-note">
        Quelle {selectedDepartment.assignmentSource === "entra_managed" ? "Entra-geführt" : "manuell"} |
        Sync-Status {toSyncStateLabel(selectedDepartment.syncState)}
      </p>

      {selectedDepartment.syncDetail ? (
        <p className="panel-note">{selectedDepartment.syncDetail}</p>
      ) : null}

      {selectedDepartment.assignmentSource === "entra_managed" ? (
        <p className="panel-note">
          Entra ist führend. Manuelle Änderungen werden beim nächsten Directory-Sync überschrieben.
        </p>
      ) : null}

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

      <section className="panel panel-muted">
        <div className="panel-head">
          <h3 className="panel-title">Stellen dieser Abteilung</h3>
          <p>Diese Stellen werden bei neuen Vorgängen als auswählbare Stelle der Abteilung angeboten.</p>
        </div>

        <div className="form-grid">
          <label className="field">
            <span>Neue Stelle</span>
            <input
              type="text"
              value={props.newPositionNameDraft}
              onChange={(event) => props.onNewPositionNameChange(event.target.value)}
              placeholder="z. B. Teamleitung Produktion"
            />
          </label>
          <div className="field">
            <span>Aktion</span>
            <button
              type="button"
              className="btn btn-primary"
              onClick={() => {
                void props.onCreateDepartmentPosition(selectedDepartment.departmentId);
              }}
              disabled={
                props.creatingPositionDepartmentId === selectedDepartment.departmentId ||
                props.newPositionNameDraft.trim().length === 0
              }
            >
              {props.creatingPositionDepartmentId === selectedDepartment.departmentId ? "Anlegen..." : "Stelle anlegen"}
            </button>
          </div>
        </div>

        {props.selectedDepartmentPositions.length === 0 ? (
          <p className="panel-note">Für diese Abteilung sind noch keine Stellen hinterlegt.</p>
        ) : (
          <div className="task-list">
            {props.selectedDepartmentPositions.map((position) => {
              const draft = props.positionDrafts[position.roleId] ?? {
                roleName: position.roleName,
                isActive: position.isActive,
              };
              const hasChanges =
                draft.roleName.trim() !== position.roleName ||
                draft.isActive !== position.isActive;
              const canSave =
                hasChanges &&
                draft.roleName.trim().length > 0 &&
                props.savingPositionId !== position.roleId;

              return (
                <article key={position.roleId} className="task-card">
                  <div className="form-grid">
                    <label className="field compact">
                      <span>Stellenname</span>
                      <input
                        type="text"
                        value={draft.roleName}
                        onChange={(event) =>
                          props.onPositionDraftChange(position.roleId, {
                            ...draft,
                            roleName: event.target.value,
                          })
                        }
                      />
                    </label>
                    <label className="checkbox-row">
                      <input
                        type="checkbox"
                        checked={draft.isActive}
                        onChange={(event) =>
                          props.onPositionDraftChange(position.roleId, {
                            ...draft,
                            isActive: event.target.checked,
                          })
                        }
                      />
                      <span>Aktiv und in neuen Vorgängen auswählbar</span>
                    </label>
                  </div>

                  <div className="action-row">
                    <button
                      type="button"
                      className="btn btn-primary"
                      onClick={() => {
                        void props.onSaveDepartmentPosition(position.roleId);
                      }}
                      disabled={!canSave}
                    >
                      {props.savingPositionId === position.roleId ? "Speichern..." : "Stelle speichern"}
                    </button>
                    <button
                      type="button"
                      className="btn btn-secondary"
                      onClick={() => {
                        void props.onRemoveDepartmentPosition(position);
                      }}
                      disabled={props.deletingPositionId === position.roleId}
                    >
                      {props.deletingPositionId === position.roleId ? "Löschen..." : "Stelle löschen"}
                    </button>
                  </div>
                </article>
              );
            })}
          </div>
        )}
      </section>

      <AdminDepartmentEntraImportSection
        departmentId={selectedDepartment.departmentId}
        existingPositions={props.selectedDepartmentPositions}
      />
    </section>
  );
}
