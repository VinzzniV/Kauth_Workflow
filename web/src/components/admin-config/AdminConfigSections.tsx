import EmptyState from "../feedback/EmptyState";
import LoadingState from "../feedback/LoadingState";
import type {
  AdminDepartmentAssignment,
  AdminGroup,
  AdminNotificationEmailConfiguration,
  AdminResponsibilityOwner,
  AdminRole,
  AdminUser,
} from "../../types/auth";
import {
  formatTimestamp,
  notificationConfigurationStatusLabel,
  notificationModeLabel,
  notificationTestStatusLabel,
  responsibilityAreaLabel,
  responsibilityTypeLabel,
  roleDisplayName,
  userOptionLabel,
} from "./adminConfigHelpers";

type AdminNotificationEmailSectionProps = {
  notificationEmailConfiguration: AdminNotificationEmailConfiguration | null;
  notificationEnabledDraft: boolean;
  notificationTenantIdDraft: string;
  notificationClientIdDraft: string;
  notificationClientSecretDraft: string;
  notificationSenderEmailDraft: string;
  notificationFrontendBaseUrlDraft: string;
  notificationTestRecipientDraft: string;
  isSavingNotificationEmailConfiguration: boolean;
  isSendingNotificationEmailTest: boolean;
  isLoading: boolean;
  hasNotificationEmailDraftChanges: boolean;
  onNotificationEnabledChange: (enabled: boolean) => void;
  onNotificationTenantIdChange: (value: string) => void;
  onNotificationClientIdChange: (value: string) => void;
  onNotificationClientSecretChange: (value: string) => void;
  onNotificationSenderEmailChange: (value: string) => void;
  onNotificationFrontendBaseUrlChange: (value: string) => void;
  onNotificationTestRecipientChange: (value: string) => void;
  onSave: () => void | Promise<void>;
  onSendTest: () => void | Promise<void>;
};

export function AdminNotificationEmailSection({
  notificationEmailConfiguration,
  notificationEnabledDraft,
  notificationTenantIdDraft,
  notificationClientIdDraft,
  notificationClientSecretDraft,
  notificationSenderEmailDraft,
  notificationFrontendBaseUrlDraft,
  notificationTestRecipientDraft,
  isSavingNotificationEmailConfiguration,
  isSendingNotificationEmailTest,
  isLoading,
  hasNotificationEmailDraftChanges,
  onNotificationEnabledChange,
  onNotificationTenantIdChange,
  onNotificationClientIdChange,
  onNotificationClientSecretChange,
  onNotificationSenderEmailChange,
  onNotificationFrontendBaseUrlChange,
  onNotificationTestRecipientChange,
  onSave,
  onSendTest,
}: AdminNotificationEmailSectionProps) {
  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Konfiguration: Mailversand</h2>
        <p>Begrenzter Admin-Bereich für Mail-Konfiguration, Testversand und den aktuellen Versandstatus.</p>
      </div>

      <div className="dashboard-grid" aria-label="Mailversand Status">
        <article className="dashboard-card">
          <div>
            <h2>Aktueller Modus</h2>
            <p>{notificationModeLabel(notificationEmailConfiguration)}</p>
          </div>
          <p className="panel-note">
            Status: {notificationConfigurationStatusLabel(notificationEmailConfiguration)}
            {notificationEmailConfiguration?.configurationMessage
              ? ` | ${notificationEmailConfiguration.configurationMessage}`
              : ""}
          </p>
        </article>

        <article className="dashboard-card">
          <div>
            <h2>Letzter Test</h2>
            <p>{notificationTestStatusLabel(notificationEmailConfiguration)}</p>
          </div>
          <p className="panel-note">
            Zuletzt geprüft: {formatTimestamp(notificationEmailConfiguration?.lastTestAt ?? null)}
          </p>
        </article>

        <article className="dashboard-card">
          <div>
            <h2>Secret-Status</h2>
            <p>{notificationEmailConfiguration?.hasClientSecret ? "Hinterlegt" : "Fehlt"}</p>
          </div>
          <p className="panel-note">
            Das Secret wird nicht im Klartext geladen. Hier kann nur ein neues Secret gesetzt oder ein bestehendes ersetzt werden.
          </p>
        </article>
      </div>

      <div className="dashboard-card">
        <div>
          <h2>Mail-Einstellungen</h2>
          <p>Konfigurieren Sie die für Microsoft Graph benötigten Felder. Ein gespeichertes Client Secret wird aus Sicherheitsgründen nicht zurück an die UI übertragen.</p>
        </div>

        <label className="field compact">
          <span>Mailversand</span>
          <select
            value={notificationEnabledDraft ? "enabled" : "disabled"}
            onChange={(event) => onNotificationEnabledChange(event.target.value === "enabled")}
          >
            <option value="enabled">Aktiviert</option>
            <option value="disabled">Deaktiviert</option>
          </select>
        </label>

        <label className="field compact">
          <span>Tenant ID</span>
          <input
            type="text"
            value={notificationTenantIdDraft}
            onChange={(event) => onNotificationTenantIdChange(event.target.value)}
            placeholder="Microsoft Entra Tenant ID"
          />
        </label>

        <label className="field compact">
          <span>Client ID</span>
          <input
            type="text"
            value={notificationClientIdDraft}
            onChange={(event) => onNotificationClientIdChange(event.target.value)}
            placeholder="App Registration Client ID"
          />
        </label>

        <label className="field compact">
          <span>Client Secret</span>
          <input
            type="password"
            value={notificationClientSecretDraft}
            onChange={(event) => onNotificationClientSecretChange(event.target.value)}
            placeholder={
              notificationEmailConfiguration?.hasClientSecret
                ? "Neues Client Secret zum Ersetzen eingeben"
                : "Microsoft Graph Client Secret"
            }
          />
        </label>

        <p className="panel-note">
          {notificationEmailConfiguration?.hasClientSecret
            ? "Leer lassen, um das bestehende Secret unverändert zu behalten."
            : "Speichern Sie hier ein neues Client Secret."}
        </p>

        <label className="field compact">
          <span>Sender-Mailadresse</span>
          <input
            type="email"
            value={notificationSenderEmailDraft}
            onChange={(event) => onNotificationSenderEmailChange(event.target.value)}
            placeholder="onboarding@example.com"
          />
        </label>

        <label className="field compact">
          <span>Frontend-Basis-URL</span>
          <input
            type="url"
            value={notificationFrontendBaseUrlDraft}
            onChange={(event) => onNotificationFrontendBaseUrlChange(event.target.value)}
            placeholder="https://onboarding.example.com"
          />
        </label>

        <label className="field compact">
          <span>Testempfänger-Mailadresse</span>
          <input
            type="email"
            value={notificationTestRecipientDraft}
            onChange={(event) => onNotificationTestRecipientChange(event.target.value)}
            placeholder="optional"
          />
        </label>

        <p className="panel-note">
          Letzte Fehlermeldung: {notificationEmailConfiguration?.lastError ?? "Keine"}
        </p>

        <div className="action-row">
          <button
            type="button"
            className="btn btn-primary"
            onClick={() => {
              void onSave();
            }}
            disabled={isSavingNotificationEmailConfiguration || isLoading}
          >
            {isSavingNotificationEmailConfiguration ? "Speichern..." : "Mail-Konfiguration speichern"}
          </button>

          <button
            type="button"
            className="btn btn-secondary"
            onClick={() => {
              void onSendTest();
            }}
            disabled={
              !notificationEmailConfiguration
              || isLoading
              || isSendingNotificationEmailTest
              || isSavingNotificationEmailConfiguration
              || hasNotificationEmailDraftChanges
            }
          >
            {isSendingNotificationEmailTest ? "Sende Testmail..." : "Testmail senden"}
          </button>
        </div>

        {hasNotificationEmailDraftChanges ? (
          <p className="panel-note">
            Es gibt ungespeicherte Änderungen. Für den Testversand wird bewusst die gespeicherte Konfiguration verwendet.
          </p>
        ) : null}
      </div>
    </section>
  );
}

type AdminCoreDataSummarySectionProps = {
  departmentCount: number;
  userCount: number;
  responsibilityCount: number;
  error: string | null;
  notice: string | null;
  isLoading: boolean;
  isLoadingTechnicalAccess: boolean;
  isTechnicalAccessOpen: boolean;
  onReload: () => void | Promise<void>;
  onToggleTechnicalAccess: () => void;
};

export function AdminCoreDataSummarySection({
  departmentCount,
  userCount,
  responsibilityCount,
  error,
  notice,
  isLoading,
  isLoadingTechnicalAccess,
  isTechnicalAccessOpen,
  onReload,
  onToggleTechnicalAccess,
}: AdminCoreDataSummarySectionProps) {
  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Kern-Stammdaten</h2>
        <p>
          Abteilungen: {departmentCount} | Personen: {userCount} | Fachliche Zuständigkeiten: {responsibilityCount}
        </p>
      </div>

      <div className="action-row">
        <button
          type="button"
          className="btn btn-secondary"
          onClick={() => {
            void onReload();
          }}
          disabled={isLoading || isLoadingTechnicalAccess}
        >
          Stammdaten aktualisieren
        </button>
        <button type="button" className="btn btn-secondary" onClick={onToggleTechnicalAccess}>
          {isTechnicalAccessOpen ? "Rechte ausblenden" : "Rechte einblenden"}
        </button>
      </div>

      {error ? <p className="panel-note">{error}</p> : null}
      {notice ? <p className="panel-note">{notice}</p> : null}
    </section>
  );
}

type AdminDepartmentsSectionProps = {
  sortedDepartments: AdminDepartmentAssignment[];
  sortedUsers: AdminUser[];
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

export function AdminDepartmentsSection({
  sortedDepartments,
  sortedUsers,
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
  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Abteilungen und Anforderungsverantwortung</h2>
        <p>Pflegen Sie je Abteilung die Abteilungsleitung und die anforderungsverantwortliche Person.</p>
      </div>

      <div className="dashboard-card">
        <div>
          <h2>Neue Abteilung</h2>
          <p>Legen Sie zusätzliche Abteilungen für das Onboarding an.</p>
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
          disabled={isCreatingDepartment}
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

          return (
            <article key={department.departmentId} className="dashboard-card">
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
                  {sortedUsers.map((user) => (
                    <option key={`lead-${department.departmentId}-${user.userId}`} value={user.userId}>
                      {userOptionLabel(user)}
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
                  {sortedUsers.map((user) => (
                    <option key={`owner-${department.departmentId}-${user.userId}`} value={user.userId}>
                      {userOptionLabel(user)}
                    </option>
                  ))}
                </select>
              </label>

              <p className="panel-note">
                Anforderungsverantwortung: {department.requirementOwnerDisplayName ?? "keine feste Person"} / Leitung:{" "}
                {department.departmentLeadDisplayName ?? "keine feste Person"}
              </p>

              <button
                type="button"
                className="btn btn-primary"
                onClick={() => {
                  void onSaveDepartmentAssignment(department.departmentId);
                }}
                disabled={savingDepartmentId === department.departmentId}
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

type AdminUsersSectionProps = {
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
        <p>Pflegen Sie Name, Login-E-Mail, optionale Benachrichtigungs-Mail, Anmeldename, Abteilung und Aktiv-Status oder legen Sie neue Personen an.</p>
      </div>

      <div className="dashboard-card">
        <div>
          <h2>Neue Person</h2>
          <p>Die Login-E-Mail bleibt eindeutig. Für Demo-Mails kann zusätzlich eine separate Benachrichtigungs-Mail gepflegt werden, die auch bei mehreren Personen identisch sein darf.</p>
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
          {isCreatingUser ? "Anlegen..." : "Person anlegen"}
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

type AdminResponsibilitiesSectionProps = {
  sortedResponsibilities: AdminResponsibilityOwner[];
  sortedDepartments: AdminDepartmentAssignment[];
  sortedUsers: AdminUser[];
  responsibilityDrafts: Record<number, { appUserId: string; departmentId: string }>;
  savingResponsibilityId: number | null;
  onResponsibilityDraftChange: (responsibilityId: number, draft: { appUserId: string; departmentId: string }) => void;
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

type AdminTechnicalAccessSectionProps = {
  isTechnicalAccessOpen: boolean;
  isLoadingTechnicalAccess: boolean;
  selectedUser: AdminUser | null;
  selectedUserRoleIds: number[];
  selectedUserGroupIds: number[];
  selectedGroupId: number | null;
  selectedGroup: AdminGroup | null;
  selectedGroupRoleIds: number[];
  sortedRoles: AdminRole[];
  groups: AdminGroup[];
  isSavingUserRoles: boolean;
  isSavingUserGroups: boolean;
  isSavingGroupRoles: boolean;
  onToggleUserRole: (roleId: number) => void;
  onToggleUserGroup: (groupId: number) => void;
  onSelectGroup: (groupId: number | null) => void;
  onToggleGroupRole: (roleId: number) => void;
  onSaveUserRoles: () => void | Promise<void>;
  onSaveUserGroups: () => void | Promise<void>;
  onSaveGroupRoles: () => void | Promise<void>;
};

export function AdminTechnicalAccessSection({
  isTechnicalAccessOpen,
  isLoadingTechnicalAccess,
  selectedUser,
  selectedUserRoleIds,
  selectedUserGroupIds,
  selectedGroupId,
  selectedGroup,
  selectedGroupRoleIds,
  sortedRoles,
  groups,
  isSavingUserRoles,
  isSavingUserGroups,
  isSavingGroupRoles,
  onToggleUserRole,
  onToggleUserGroup,
  onSelectGroup,
  onToggleGroupRole,
  onSaveUserRoles,
  onSaveUserGroups,
  onSaveGroupRoles,
}: AdminTechnicalAccessSectionProps) {
  if (!isTechnicalAccessOpen) {
    return null;
  }

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Benutzerrechte und Gruppen</h2>
        <p>Dieser Bereich ist für Rollen, Gruppen und Berechtigungen gedacht und wird bei Bedarf separat geladen.</p>
      </div>

      {isLoadingTechnicalAccess ? <LoadingState title="Rechte werden geladen..." /> : null}

      {!isLoadingTechnicalAccess && selectedUser ? (
        <div className="content-stack">
          <section className="panel">
            <div className="panel-head">
              <h2>Rollen zuweisen: {selectedUser.displayName}</h2>
              <p>Rollen direkt zuordnen.</p>
            </div>

            <div className="chips-row" aria-label="Rollen Auswahl">
              {sortedRoles.map((role) => {
                const isActive = selectedUserRoleIds.includes(role.roleId);
                return (
                  <button
                    key={role.roleId}
                    type="button"
                    className={`chip chip-action ${isActive ? "active" : ""}`}
                    onClick={() => onToggleUserRole(role.roleId)}
                  >
                    {roleDisplayName(role)} ({role.roleKey})
                  </button>
                );
              })}
            </div>

            <div className="action-row">
              <button
                type="button"
                className="btn btn-primary"
                onClick={() => {
                  void onSaveUserRoles();
                }}
                disabled={isSavingUserRoles}
              >
                {isSavingUserRoles ? "Speichern..." : "Rollen speichern"}
              </button>
            </div>
          </section>

          <section className="panel">
            <div className="panel-head">
              <h2>Gruppen zuweisen: {selectedUser.displayName}</h2>
              <p>Gruppen mit vererbten Rechten zuordnen.</p>
            </div>

            <div className="chips-row" aria-label="Gruppen Auswahl">
              {groups.map((group) => {
                const isActive = selectedUserGroupIds.includes(group.groupId);
                return (
                  <button
                    key={group.groupId}
                    type="button"
                    className={`chip chip-action ${isActive ? "active" : ""}`}
                    onClick={() => onToggleUserGroup(group.groupId)}
                  >
                    {group.groupName} ({group.groupKey})
                  </button>
                );
              })}
            </div>

            <div className="action-row">
              <button
                type="button"
                className="btn btn-primary"
                onClick={() => {
                  void onSaveUserGroups();
                }}
                disabled={isSavingUserGroups}
              >
                {isSavingUserGroups ? "Speichern..." : "Gruppen speichern"}
              </button>
            </div>
          </section>
        </div>
      ) : null}

      {!isLoadingTechnicalAccess && !selectedUser ? (
        <EmptyState
          title="Person auswählen"
          description="Wählen Sie oben in der Personenliste zuerst eine Person für die Rechteverwaltung aus."
        />
      ) : null}

      {!isLoadingTechnicalAccess && groups.length > 0 ? (
        <section className="panel">
          <div className="panel-head">
            <h2>Gruppenrollen</h2>
            <p>Rollen pro Gruppe nur bei Bedarf anpassen.</p>
          </div>

          <label className="field compact">
            <span>Gruppe</span>
            <select
              value={selectedGroupId ?? ""}
              onChange={(event) => onSelectGroup(event.target.value ? Number(event.target.value) : null)}
            >
              <option value="">Bitte wählen</option>
              {groups.map((group) => (
                <option key={group.groupId} value={group.groupId}>
                  {group.groupName} ({group.groupKey})
                </option>
              ))}
            </select>
          </label>

          {selectedGroup ? (
            <>
              <p className="panel-note">
                Aktuelle Rollen: {selectedGroup.roles.map(roleDisplayName).join(", ") || "-"}
              </p>
              <div className="chips-row" aria-label="Gruppenrollen Auswahl">
                {sortedRoles.map((role) => {
                  const isActive = selectedGroupRoleIds.includes(role.roleId);
                  return (
                    <button
                      key={`group-role-${role.roleId}`}
                      type="button"
                      className={`chip chip-action ${isActive ? "active" : ""}`}
                      onClick={() => onToggleGroupRole(role.roleId)}
                    >
                      {roleDisplayName(role)} ({role.roleKey})
                    </button>
                  );
                })}
              </div>

              <div className="action-row">
                <button
                  type="button"
                  className="btn btn-primary"
                  onClick={() => {
                    void onSaveGroupRoles();
                  }}
                  disabled={isSavingGroupRoles}
                >
                  {isSavingGroupRoles ? "Speichern..." : "Gruppenrollen speichern"}
                </button>
              </div>
            </>
          ) : null}
        </section>
      ) : null}
    </section>
  );
}
