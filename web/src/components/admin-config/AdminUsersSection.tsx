import EmptyState from "../feedback/EmptyState";
import type { AdminDepartmentAssignment, AdminUser } from "../../types/auth";
import { roleDisplayName } from "./adminConfigHelpers";

type AdminUsersSectionProps = {
  isExternalIdentityMode: boolean;
  sortedUsers: AdminUser[];
  sortedDepartments: AdminDepartmentAssignment[];
  selectedUserId: number | null;
  selectedUser: AdminUser | null;
  newUserDisplayNameDraft: string;
  newUserEmailDraft: string;
  newUserNotificationEmailDraft: string;
  newUserExternalKeyDraft: string;
  newUserDepartmentIdDraft: string;
  newUserIsActiveDraft: boolean;
  userDisplayNameDraft: string;
  userEmailDraft: string;
  userNotificationEmailDraft: string;
  userExternalKeyDraft: string;
  userDepartmentIdDraft: string;
  userIsActiveDraft: boolean;
  userFormError: string | null;
  userFormNotice: string | null;
  isCreatingUser: boolean;
  isSavingUserMasterData: boolean;
  deletingUserId: number | null;
  onSelectUser: (user: AdminUser) => void;
  onNewUserDisplayNameChange: (value: string) => void;
  onNewUserEmailChange: (value: string) => void;
  onNewUserNotificationEmailChange: (value: string) => void;
  onNewUserExternalKeyChange: (value: string) => void;
  onNewUserDepartmentIdChange: (value: string) => void;
  onNewUserIsActiveChange: (value: boolean) => void;
  onUserDisplayNameChange: (value: string) => void;
  onUserEmailChange: (value: string) => void;
  onUserNotificationEmailChange: (value: string) => void;
  onUserExternalKeyChange: (value: string) => void;
  onUserDepartmentIdChange: (value: string) => void;
  onUserIsActiveChange: (value: boolean) => void;
  onCreateUser: () => void | Promise<void>;
  onSaveUserMasterData: () => void | Promise<void>;
  onRemoveUser: (user: AdminUser) => void | Promise<void>;
};

export function AdminUsersSection({
  isExternalIdentityMode,
  sortedUsers,
  sortedDepartments,
  selectedUserId,
  selectedUser,
  newUserDisplayNameDraft,
  newUserEmailDraft,
  newUserNotificationEmailDraft,
  newUserExternalKeyDraft,
  newUserDepartmentIdDraft,
  newUserIsActiveDraft,
  userDisplayNameDraft,
  userEmailDraft,
  userNotificationEmailDraft,
  userExternalKeyDraft,
  userDepartmentIdDraft,
  userIsActiveDraft,
  userFormError,
  userFormNotice,
  isCreatingUser,
  isSavingUserMasterData,
  deletingUserId,
  onSelectUser,
  onNewUserDisplayNameChange,
  onNewUserEmailChange,
  onNewUserNotificationEmailChange,
  onNewUserExternalKeyChange,
  onNewUserDepartmentIdChange,
  onNewUserIsActiveChange,
  onUserDisplayNameChange,
  onUserEmailChange,
  onUserNotificationEmailChange,
  onUserExternalKeyChange,
  onUserDepartmentIdChange,
  onUserIsActiveChange,
  onCreateUser,
  onSaveUserMasterData,
  onRemoveUser,
}: AdminUsersSectionProps) {
  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Personen</h2>
        <p>
          {isExternalIdentityMode
            ? "Personen und Logins werden im Regelfall extern verwaltet. Lokale Benutzerpflege bleibt nur für Ausnahmen und Übergangsfälle sichtbar."
            : "Pflegen Sie Name, Login-E-Mail, optionale Benachrichtigungs-Mail, Anmeldename, Abteilung und Aktiv-Status oder legen Sie neue Personen an."}
        </p>
      </div>

      {isExternalIdentityMode ? (
        <section className="panel panel-muted">
          <div className="panel-head">
            <h2>Extern verwaltete Identitäten</h2>
            <p>Bei aktivem Entra-Modus sollten Benutzerkonten primär aus dem Verzeichnis kommen. Manuelle Anlage ist nur für Ausnahmen gedacht.</p>
          </div>
        </section>
      ) : null}

      <div className="dashboard-card card-primary">
        <div>
          <h2>{isExternalIdentityMode ? "Neue Ausnahme-Person" : "Neue Person"}</h2>
          <p>
            {isExternalIdentityMode
              ? "Nur verwenden, wenn eine Person bewusst lokal gepflegt werden muss. Standardfall bleibt die Synchronisierung aus dem Verzeichnis."
              : "Die Login-E-Mail bleibt eindeutig. Für Demo-Mails kann zusätzlich eine separate Benachrichtigungs-Mail gepflegt werden, die auch bei mehreren Personen identisch sein darf."}
          </p>
        </div>

        <label className="field compact">
          <span>Anzeigename</span>
          <input
            type="text"
            value={newUserDisplayNameDraft}
            onChange={(event) => onNewUserDisplayNameChange(event.target.value)}
            placeholder="Max Mustermann"
          />
        </label>

        <label className="field compact">
          <span>E-Mail</span>
          <input
            type="email"
            value={newUserEmailDraft}
            onChange={(event) => onNewUserEmailChange(event.target.value)}
            placeholder="max.mustermann@example.com"
          />
        </label>

        <label className="field compact">
          <span>Benachrichtigungs-Mail</span>
          <input
            type="email"
            value={newUserNotificationEmailDraft}
            onChange={(event) => onNewUserNotificationEmailChange(event.target.value)}
            placeholder="optional fuer Demo-Verteiler"
          />
        </label>

        <label className="field compact">
          <span>Anmeldename</span>
          <input
            type="text"
            value={newUserExternalKeyDraft}
            onChange={(event) => onNewUserExternalKeyChange(event.target.value)}
            placeholder="optional"
          />
        </label>

        <label className="field compact">
          <span>Abteilung</span>
          <select
            value={newUserDepartmentIdDraft}
            onChange={(event) => onNewUserDepartmentIdChange(event.target.value)}
          >
            <option value="">Keine Abteilung</option>
            {sortedDepartments.map((department) => (
              <option key={`new-user-department-${department.departmentId}`} value={department.departmentId}>
                {department.departmentName}
              </option>
            ))}
          </select>
        </label>

        <label className="field compact">
          <span>Status</span>
          <select
            value={newUserIsActiveDraft ? "active" : "inactive"}
            onChange={(event) => onNewUserIsActiveChange(event.target.value === "active")}
          >
            <option value="active">Aktiv</option>
            <option value="inactive">Inaktiv</option>
          </select>
        </label>

        <button
          type="button"
          className="btn btn-primary"
          onClick={() => {
            void onCreateUser();
          }}
          disabled={isCreatingUser || newUserDisplayNameDraft.trim().length === 0 || newUserEmailDraft.trim().length === 0}
        >
          {isCreatingUser ? "Anlegen..." : isExternalIdentityMode ? "Ausnahme-Person anlegen" : "Person anlegen"}
        </button>
      </div>

      {sortedUsers.length === 0 ? (
        <EmptyState title="Keine Personen vorhanden" description="Es sind keine Personendaten vorhanden." />
      ) : (
        <>
          <div className="role-grid" aria-label="Personenliste">
            {sortedUsers.map((user) => (
              <button
                key={user.userId}
                type="button"
                className={`role-card ${selectedUserId === user.userId ? "selected" : ""}`}
                onClick={() => onSelectUser(user)}
              >
                <strong>{user.displayName}</strong>
                <small>{user.email}</small>
                <small>
                  Benachrichtigung: {user.notificationEmail?.trim() ? user.notificationEmail : "wie Login-E-Mail"}
                </small>
                <small>{user.externalKey ? `Anmeldename: ${user.externalKey}` : "Kein Anmeldename"}</small>
                <small>{user.departmentName ?? "Keine Abteilung"}</small>
                <small>{user.isActive ? "Aktiv" : "Inaktiv"}</small>
              </button>
            ))}
          </div>

          {selectedUser ? (
            <div className="content-stack">
              <section key={`selected-user-${selectedUser.userId}`} className="panel">
                <div className="panel-head">
                  <h2>Person pflegen: {selectedUser.displayName}</h2>
                  <p>Die Login-E-Mail bleibt eindeutig. Für Demo- oder Sammelpostfächer können Sie zusätzlich eine separate Benachrichtigungs-Mail pflegen.</p>
                </div>

                <label className="field compact">
                  <span>Anzeigename</span>
                  <input type="text" value={userDisplayNameDraft} onChange={(event) => onUserDisplayNameChange(event.target.value)} />
                </label>

                <label className="field compact">
                  <span>Login-E-Mail</span>
                  <input type="email" value={userEmailDraft} onChange={(event) => onUserEmailChange(event.target.value)} />
                </label>

                <label className="field compact">
                  <span>Benachrichtigungs-Mail</span>
                  <input
                    type="email"
                    value={userNotificationEmailDraft}
                    onChange={(event) => onUserNotificationEmailChange(event.target.value)}
                    placeholder="leer = Login-E-Mail verwenden"
                  />
                </label>

                <label className="field compact">
                  <span>Anmeldename</span>
                  <input
                    type="text"
                    value={userExternalKeyDraft}
                    onChange={(event) => onUserExternalKeyChange(event.target.value)}
                    placeholder="optional"
                  />
                </label>

                <label className="field compact">
                  <span>Abteilung</span>
                  <select value={userDepartmentIdDraft} onChange={(event) => onUserDepartmentIdChange(event.target.value)}>
                    <option value="">Keine Abteilung</option>
                    {sortedDepartments.map((department) => (
                      <option key={`department-${department.departmentId}`} value={department.departmentId}>
                        {department.departmentName}
                      </option>
                    ))}
                  </select>
                </label>

                <label className="field compact">
                  <span>Status</span>
                  <select
                    value={userIsActiveDraft ? "active" : "inactive"}
                    onChange={(event) => onUserIsActiveChange(event.target.value === "active")}
                  >
                    <option value="active">Aktiv</option>
                    <option value="inactive">Inaktiv</option>
                  </select>
                </label>

                <p className="panel-note">
                  Demo-Versand: {userNotificationEmailDraft.trim() || userEmailDraft.trim() || "keine Mail gepflegt"} | Rollen:{" "}
                  {selectedUser.roles.map(roleDisplayName).join(", ") || "keine"} | Gruppen:{" "}
                  {selectedUser.groups.map((group) => group.groupName).join(", ") || "keine"} | Klick auf einen Mail-Link meldet die Session als diese Person an.
                </p>

                {userFormError ? <p className="panel-note">{userFormError}</p> : null}
                {userFormNotice ? <p className="panel-note">{userFormNotice}</p> : null}

                <div className="action-row">
                  <button
                    type="button"
                    className="btn btn-primary"
                    onClick={() => {
                      void onSaveUserMasterData();
                    }}
                    disabled={isSavingUserMasterData || userDisplayNameDraft.trim().length === 0 || userEmailDraft.trim().length === 0}
                  >
                    {isSavingUserMasterData ? "Speichern..." : "Person speichern"}
                  </button>

                  <button
                    type="button"
                    className="btn btn-secondary"
                    onClick={() => {
                      void onRemoveUser(selectedUser);
                    }}
                    disabled={deletingUserId === selectedUser.userId}
                  >
                    {deletingUserId === selectedUser.userId ? "Löschen..." : "Person löschen"}
                  </button>
                </div>
              </section>
            </div>
          ) : null}
        </>
      )}
    </section>
  );
}
