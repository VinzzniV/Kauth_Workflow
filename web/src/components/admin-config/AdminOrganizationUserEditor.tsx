import { isEntraMode } from "../../auth/IdentityProvider";
import type {
  AdminDepartmentAssignment,
  AdminUser,
} from "../../types/auth";
import { roleDisplayName } from "./adminConfigHelpers";

type AdminOrganizationUserEditorProps = {
  sortedDepartments: AdminDepartmentAssignment[];
  selectedUser: AdminUser | null;
  newUserDisplayNameDraft: string;
  newUserEmailDraft: string;
  newUserNotificationEmailDraft: string;
  newUserExternalKeyDraft: string;
  newUserDepartmentIdDraft: string;
  newUserIsActiveDraft: boolean;
  isCreatingUser: boolean;
  userDisplayNameDraft: string;
  userEmailDraft: string;
  userNotificationEmailDraft: string;
  userExternalKeyDraft: string;
  userDepartmentIdDraft: string;
  userIsActiveDraft: boolean;
  userFormError: string | null;
  userFormNotice: string | null;
  isSavingUserMasterData: boolean;
  deletingUserId: number | null;
  canCreateUser: boolean;
  canSaveUser: boolean;
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

export function AdminOrganizationUserEditor(props: AdminOrganizationUserEditorProps) {
  const externalIdentityMode = isEntraMode();

  if (!props.selectedUser) {
    return (
      <section className="panel">
        <div className="panel-head">
          <h2>{externalIdentityMode ? "Neue Ausnahme-Person" : "Neue Person"}</h2>
          <p>
            {externalIdentityMode
              ? "Identitäten kommen im Regelfall aus Entra. Die manuelle Anlage bleibt nur für Ausnahmen und Übergangsfälle sichtbar."
              : "Personen-Stammdaten bleiben hier bewusst getrennt von Rollen und Gruppen."}
          </p>
        </div>

        {externalIdentityMode ? (
          <p className="panel-note">
            Empfehlung: zuerst Verzeichnis-Sync und Gruppen-Mappings im Bereich „Verzeichnis“ nutzen.
          </p>
        ) : null}

        <div className="form-grid">
          <label className="field">
            <span>Anzeigename</span>
            <input
              type="text"
              value={props.newUserDisplayNameDraft}
              onChange={(event) => props.onNewUserDisplayNameChange(event.target.value)}
              placeholder="Max Mustermann"
            />
          </label>

          <label className="field">
            <span>Login-E-Mail</span>
            <input
              type="email"
              value={props.newUserEmailDraft}
              onChange={(event) => props.onNewUserEmailChange(event.target.value)}
              placeholder="max.mustermann@example.com"
            />
          </label>

          <label className="field">
            <span>Benachrichtigungs-Mail</span>
            <input
              type="email"
              value={props.newUserNotificationEmailDraft}
              onChange={(event) => props.onNewUserNotificationEmailChange(event.target.value)}
              placeholder="optional für Demo-Verteiler"
            />
          </label>

          <label className="field">
            <span>Anmeldename</span>
            <input
              type="text"
              value={props.newUserExternalKeyDraft}
              onChange={(event) => props.onNewUserExternalKeyChange(event.target.value)}
              placeholder="optional"
            />
          </label>

          <label className="field">
            <span>Abteilung</span>
            <select
              value={props.newUserDepartmentIdDraft}
              onChange={(event) => props.onNewUserDepartmentIdChange(event.target.value)}
            >
              <option value="">Keine Abteilung</option>
              {props.sortedDepartments.map((department) => (
                <option key={`new-user-department-${department.departmentId}`} value={department.departmentId}>
                  {department.departmentName}
                </option>
              ))}
            </select>
          </label>

          <label className="field">
            <span>Status</span>
            <select
              value={props.newUserIsActiveDraft ? "active" : "inactive"}
              onChange={(event) => props.onNewUserIsActiveChange(event.target.value === "active")}
            >
              <option value="active">Aktiv</option>
              <option value="inactive">Inaktiv</option>
            </select>
          </label>
        </div>

        <div className="action-row">
          <button
            type="button"
            className="btn btn-primary"
            onClick={() => {
              void props.onCreateUser();
            }}
            disabled={!props.canCreateUser}
          >
            {props.isCreatingUser ? "Anlegen..." : externalIdentityMode ? "Ausnahme-Person anlegen" : "Person anlegen"}
          </button>
        </div>
      </section>
    );
  }

  const selectedUser = props.selectedUser;

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Person pflegen: {selectedUser.displayName}</h2>
        <p>
          {externalIdentityMode
            ? "Lokale Pflege bleibt für Ausnahmen möglich. Verknüpfte Organisationsbeziehungen werden rechts kontextbezogen eingeblendet."
            : "Verknüpfte Organisationsbeziehungen werden rechts kontextbezogen eingeblendet."}
        </p>
      </div>

      {externalIdentityMode ? (
        <p className="panel-note">
          Im Entra-Modus sollten Login-Identität und Gruppen primär aus dem Verzeichnis kommen. Lokale Änderungen hier nur gezielt einsetzen.
        </p>
      ) : null}

      <div className="form-grid">
        <label className="field">
          <span>Anzeigename</span>
          <input
            type="text"
            value={props.userDisplayNameDraft}
            onChange={(event) => props.onUserDisplayNameChange(event.target.value)}
          />
        </label>

        <label className="field">
          <span>Login-E-Mail</span>
          <input
            type="email"
            value={props.userEmailDraft}
            onChange={(event) => props.onUserEmailChange(event.target.value)}
          />
        </label>

        <label className="field">
          <span>Benachrichtigungs-Mail</span>
          <input
            type="email"
            value={props.userNotificationEmailDraft}
            onChange={(event) => props.onUserNotificationEmailChange(event.target.value)}
            placeholder="leer = Login-E-Mail verwenden"
          />
        </label>

        <label className="field">
          <span>Anmeldename</span>
          <input
            type="text"
            value={props.userExternalKeyDraft}
            onChange={(event) => props.onUserExternalKeyChange(event.target.value)}
            placeholder="optional"
          />
        </label>

        <label className="field">
          <span>Abteilung</span>
          <select
            value={props.userDepartmentIdDraft}
            onChange={(event) => props.onUserDepartmentIdChange(event.target.value)}
          >
            <option value="">Keine Abteilung</option>
            {props.sortedDepartments.map((department) => (
              <option key={`user-department-${department.departmentId}`} value={department.departmentId}>
                {department.departmentName}
              </option>
            ))}
          </select>
        </label>

        <label className="field">
          <span>Status</span>
          <select
            value={props.userIsActiveDraft ? "active" : "inactive"}
            onChange={(event) => props.onUserIsActiveChange(event.target.value === "active")}
          >
            <option value="active">Aktiv</option>
            <option value="inactive">Inaktiv</option>
          </select>
        </label>
      </div>

      <p className="panel-note">
        Rollen: {selectedUser.roles.map(roleDisplayName).join(", ") || "keine"} | Gruppen:{" "}
        {selectedUser.groups.map((group) => group.groupName).join(", ") || "keine"}
      </p>

      {props.userFormError ? <p className="panel-note">{props.userFormError}</p> : null}
      {props.userFormNotice ? <p className="panel-note">{props.userFormNotice}</p> : null}

      <div className="action-row">
        <button
          type="button"
          className="btn btn-primary"
          onClick={() => {
            void props.onSaveUserMasterData();
          }}
          disabled={!props.canSaveUser}
        >
          {props.isSavingUserMasterData ? "Speichern..." : "Person speichern"}
        </button>
        <button
          type="button"
          className="btn btn-secondary"
          onClick={() => {
            void props.onRemoveUser(selectedUser);
          }}
          disabled={props.deletingUserId === selectedUser.userId}
        >
          {props.deletingUserId === selectedUser.userId ? "Löschen..." : "Person löschen"}
        </button>
      </div>
    </section>
  );
}
