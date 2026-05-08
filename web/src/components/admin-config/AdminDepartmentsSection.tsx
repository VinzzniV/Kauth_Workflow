import type { AdminDepartmentAssignment, AdminUser } from "../../types/auth";
import { formatTimestamp, userOptionLabel } from "./adminConfigHelpers";

type AdminDepartmentsSectionProps = {
  sortedDepartments: AdminDepartmentAssignment[];
  sortedUsers: AdminUser[];
  eligibleSupervisorUsers: AdminUser[];
  eligibleRequirementOwnerUsers: AdminUser[];
  departmentDrafts: Record<number, { departmentLeadUserId: string; requirementOwnerUserId: string }>;
  newDepartmentNameDraft: string;
  isCreatingDepartment: boolean;
  savingDepartmentId: number | null;
  deletingDepartmentId: number | null;
  onNewDepartmentNameChange: (value: string) => void;
  onDepartmentDraftChange: (
    departmentId: number,
    draft: { departmentLeadUserId: string; requirementOwnerUserId: string }
  ) => void;
  onCreateDepartment: () => void | Promise<void>;
  onSaveDepartmentAssignment: (departmentId: number) => void | Promise<void>;
  onRemoveDepartment: (department: AdminDepartmentAssignment) => void | Promise<void>;
};

function toStoredUserId(value: number | null): string {
  return value ? String(value) : "";
}

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

export function AdminDepartmentsSection({
  sortedDepartments,
  sortedUsers,
  eligibleSupervisorUsers,
  eligibleRequirementOwnerUsers,
  departmentDrafts,
  newDepartmentNameDraft,
  isCreatingDepartment,
  savingDepartmentId,
  deletingDepartmentId,
  onNewDepartmentNameChange,
  onDepartmentDraftChange,
  onCreateDepartment,
  onSaveDepartmentAssignment,
  onRemoveDepartment,
}: AdminDepartmentsSectionProps) {
function buildSelectableUserOptions(selectedUserId: string, eligibleUsers: AdminUser[]): AdminUser[] {
    if (!selectedUserId) {
      return eligibleUsers;
    }

    const selectedUser = sortedUsers.find((user) => String(user.userId) === selectedUserId);
    if (!selectedUser) {
      return eligibleUsers;
    }

    if (eligibleUsers.some((user) => user.userId === selectedUser.userId)) {
      return eligibleUsers;
    }

    return [...eligibleUsers, selectedUser];
  }

  function isEligibleSupervisorSelection(selectedUserId: string): boolean {
    return !selectedUserId || eligibleSupervisorUsers.some((user) => String(user.userId) === selectedUserId);
  }

  function hasDepartmentAssignmentChanges(
    department: AdminDepartmentAssignment,
    draft: { departmentLeadUserId: string; requirementOwnerUserId: string }
  ): boolean {
    return (
      draft.departmentLeadUserId !== toStoredUserId(department.departmentLeadUserId)
      || draft.requirementOwnerUserId !== toStoredUserId(department.requirementOwnerUserId)
    );
  }

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Abteilungen und Anforderungsverantwortung</h2>
        <p>Pflegen Sie je Abteilung die Abteilungsleitung und die anforderungsverantwortliche Person. Die Leitung braucht Supervisor-Berechtigung, für die Anforderungsverantwortung reicht ein aktiver Benutzer.</p>
      </div>

      <div className="card-primary card-form">
        <div>
          <h2>Neue Abteilung</h2>
          <p>Legen Sie zusätzliche Abteilungen für Mitarbeiterprozesse an.</p>
        </div>

        <label className="field compact">
          <span>Name</span>
          <input
            type="text"
            value={newDepartmentNameDraft}
            onChange={(event) => onNewDepartmentNameChange(event.target.value)}
            placeholder="z. B. Einkauf"
          />
        </label>

        <button
          type="button"
          className="btn btn-primary"
          onClick={() => {
            void onCreateDepartment();
          }}
          disabled={isCreatingDepartment || newDepartmentNameDraft.trim().length === 0}
        >
          {isCreatingDepartment ? "Anlegen..." : "Abteilung anlegen"}
        </button>
      </div>

      <div className="dashboard-grid" aria-label="Abteilungen">
        {sortedDepartments.map((department) => {
          const draft = departmentDrafts[department.departmentId] ?? {
            departmentLeadUserId: "",
            requirementOwnerUserId: "",
          };
          const departmentLeadOptions = buildSelectableUserOptions(draft.departmentLeadUserId, eligibleSupervisorUsers);
          const requirementOwnerOptions = buildSelectableUserOptions(
            draft.requirementOwnerUserId,
            eligibleRequirementOwnerUsers
          );
          const hasChanges = hasDepartmentAssignmentChanges(department, draft);
          const hasInvalidLeadSelection = !isEligibleSupervisorSelection(draft.departmentLeadUserId);
          const hasInvalidRequirementOwnerSelection = !eligibleRequirementOwnerUsers.some(
            (user) => String(user.userId) === draft.requirementOwnerUserId
          ) && Boolean(draft.requirementOwnerUserId);
          const canSaveAssignment =
            hasChanges
            && !hasInvalidLeadSelection
            && !hasInvalidRequirementOwnerSelection
            && savingDepartmentId !== department.departmentId;

          return (
            <article key={department.departmentId} className="dashboard-card card-list">
              <div>
                <h2>{department.departmentName}</h2>
                <p>Zuletzt gespeichert: {formatTimestamp(department.updatedAt)}</p>
              </div>

              <label className="field compact">
                <span>Abteilungsleitung</span>
                <select
                  value={draft.departmentLeadUserId}
                  onChange={(event) =>
                    onDepartmentDraftChange(department.departmentId, {
                      ...draft,
                      departmentLeadUserId: event.target.value,
                    })
                  }
                >
                  <option value="">Nicht fest hinterlegt</option>
                  {departmentLeadOptions.map((user) => (
                    <option key={`lead-${department.departmentId}-${user.userId}`} value={user.userId}>
                      {userOptionLabel(user)}
                      {!eligibleSupervisorUsers.some((candidate) => candidate.userId === user.userId)
                        ? " | aktuell ungültig"
                        : ""}
                    </option>
                  ))}
                </select>
              </label>

              <label className="field compact">
                <span>Anforderungsverantwortliche Person</span>
                <select
                  value={draft.requirementOwnerUserId}
                  onChange={(event) =>
                    onDepartmentDraftChange(department.departmentId, {
                      ...draft,
                      requirementOwnerUserId: event.target.value,
                    })
                  }
                >
                  <option value="">Nicht fest hinterlegt</option>
                  {requirementOwnerOptions.map((user) => (
                    <option key={`owner-${department.departmentId}-${user.userId}`} value={user.userId}>
                      {userOptionLabel(user)}
                      {!eligibleRequirementOwnerUsers.some((candidate) => candidate.userId === user.userId)
                        ? " | aktuell ungültig"
                        : ""}
                    </option>
                  ))}
                </select>
              </label>

              <p className="panel-note">
                Anforderungsverantwortung: {department.requirementOwnerDisplayName ?? "keine feste Person"} / Leitung:{" "}
                {department.departmentLeadDisplayName ?? "keine feste Person"}
              </p>

              <p className="panel-note">
                Quelle: {department.assignmentSource === "entra_managed" ? "Entra-geführt" : "manuell"} / Status:{" "}
                {toSyncStateLabel(department.syncState)}
              </p>

              {department.syncDetail ? <p className="panel-note">{department.syncDetail}</p> : null}

              {department.assignmentSource === "entra_managed" ? (
                <p className="panel-note">
                  Entra ist führend. Manuelle Änderungen werden beim nächsten Directory-Sync überschrieben.
                </p>
              ) : null}

              {hasChanges ? (
                <p className="panel-note">
                  Geplant: Anforderungsverantwortung{" "}
                  {draft.requirementOwnerUserId
                    ? requirementOwnerOptions.find((user) => String(user.userId) === draft.requirementOwnerUserId)?.displayName ?? "unbekannt"
                    : "keine feste Person"}{" "}
                  / Leitung{" "}
                  {draft.departmentLeadUserId
                    ? departmentLeadOptions.find((user) => String(user.userId) === draft.departmentLeadUserId)?.displayName ?? "unbekannt"
                    : "keine feste Person"}
                </p>
              ) : (
                <p className="panel-note">Keine ungespeicherten Änderungen.</p>
              )}

              {hasInvalidLeadSelection || hasInvalidRequirementOwnerSelection ? (
                <p className="panel-note">
                  Ungültige Zuordnung: Für die Leitung ist aktive Supervisor-Berechtigung nötig. Für die
                  anforderungsverantwortliche Person reicht ein aktiver Benutzer. Ungültige gespeicherte Personen
                  bleiben sichtbar, müssen aber vor dem Speichern ersetzt oder entfernt werden.
                </p>
              ) : null}

              <button
                type="button"
                className="btn btn-primary"
                onClick={() => {
                  void onSaveDepartmentAssignment(department.departmentId);
                }}
                disabled={!canSaveAssignment}
              >
                {savingDepartmentId === department.departmentId ? "Speichern..." : "Zuständigkeit speichern"}
              </button>

              <button
                type="button"
                className="btn btn-secondary"
                onClick={() => {
                  void onRemoveDepartment(department);
                }}
                disabled={deletingDepartmentId === department.departmentId}
              >
                {deletingDepartmentId === department.departmentId ? "Löschen..." : "Abteilung löschen"}
              </button>
            </article>
          );
        })}
      </div>
    </section>
  );
}
