import type { AdminNotificationEmailConfiguration } from "../../types/auth";
import {
  formatTimestamp,
  notificationConfigurationStatusLabel,
  notificationModeLabel,
  notificationTestStatusLabel,
} from "./adminConfigHelpers";

function notificationModePillClass(mode: AdminNotificationEmailConfiguration["mode"] | undefined): string {
  switch (mode) {
    case "enabled":
      return "status-pill completed";
    case "sandbox":
      return "status-pill running";
    default:
      return "status-pill cancelled";
  }
}

type AdminNotificationEmailSectionProps = {
  notificationEmailConfiguration: AdminNotificationEmailConfiguration | null;
  notificationEnabledDraft: boolean;
  notificationTenantIdDraft: string;
  notificationClientIdDraft: string;
  notificationClientSecretDraft: string;
  notificationSenderEmailDraft: string;
  notificationFrontendBaseUrlDraft: string;
  notificationTestRecipientDraft: string;
  notificationSandboxRedirectDraft: string;
  notificationNotifyOnWorkflowCreatedDraft: boolean;
  notificationNotifyOnTaskReadyDraft: boolean;
  notificationNotifyOnWorkflowCompletedDraft: boolean;
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
  onNotificationSandboxRedirectChange: (value: string) => void;
  onNotificationNotifyOnWorkflowCreatedChange: (value: boolean) => void;
  onNotificationNotifyOnTaskReadyChange: (value: boolean) => void;
  onNotificationNotifyOnWorkflowCompletedChange: (value: boolean) => void;
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
  notificationSandboxRedirectDraft,
  notificationNotifyOnWorkflowCreatedDraft,
  notificationNotifyOnTaskReadyDraft,
  notificationNotifyOnWorkflowCompletedDraft,
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
  onNotificationSandboxRedirectChange,
  onNotificationNotifyOnWorkflowCreatedChange,
  onNotificationNotifyOnTaskReadyChange,
  onNotificationNotifyOnWorkflowCompletedChange,
  onSave,
  onSendTest,
}: AdminNotificationEmailSectionProps) {
  const hasSandboxRedirectDraft = notificationSandboxRedirectDraft.trim().length > 0;
  const showsDirectDeliveryWarning = notificationEnabledDraft && !hasSandboxRedirectDraft;

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
          <p>
            <span className={notificationModePillClass(notificationEmailConfiguration?.mode)}>
              {notificationModeLabel(notificationEmailConfiguration)}
            </span>
          </p>
          <p className="panel-note">
            Status: {notificationConfigurationStatusLabel(notificationEmailConfiguration)}
            {notificationEmailConfiguration?.configurationMessage
              ? ` | ${notificationEmailConfiguration.configurationMessage}`
              : ""}
          </p>
          {notificationEmailConfiguration?.mode === "sandbox" ? (
            <p className="panel-note">Sandbox aktiv: alle Mails werden an die Testadresse umgeleitet.</p>
          ) : null}
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

        <label className="field compact">
          <span>Sandbox-Weiterleitungsadresse</span>
          <input
            type="email"
            value={notificationSandboxRedirectDraft}
            onChange={(event) => onNotificationSandboxRedirectChange(event.target.value)}
            placeholder="demo-mailbox@example.com"
          />
        </label>

        <p className="panel-note">
          Wenn gesetzt, werden alle Benachrichtigungen an diese Adresse weitergeleitet. Der Versand bleibt dabei nur aktiv, wenn der Modus nicht deaktiviert ist.
        </p>

        {showsDirectDeliveryWarning ? (
          <p className="panel-note">
            Achtung: Aktivierter Versand ohne Sandbox-Weiterleitung sendet an die in der Konfiguration bzw. an den Workflows hinterlegten Empfänger.
          </p>
        ) : null}

        <div className="field">
          <span>Benachrichtigungstypen</span>
          <label className="checkbox-row">
            <input
              type="checkbox"
              checked={notificationNotifyOnWorkflowCreatedDraft}
              onChange={(event) => onNotificationNotifyOnWorkflowCreatedChange(event.target.checked)}
            />
            <span>Workflow gestartet</span>
          </label>
          <label className="checkbox-row">
            <input
              type="checkbox"
              checked={notificationNotifyOnTaskReadyDraft}
              onChange={(event) => onNotificationNotifyOnTaskReadyChange(event.target.checked)}
            />
            <span>Aufgabe bereit</span>
          </label>
          <label className="checkbox-row">
            <input
              type="checkbox"
              checked={notificationNotifyOnWorkflowCompletedDraft}
              onChange={(event) => onNotificationNotifyOnWorkflowCompletedChange(event.target.checked)}
            />
            <span>Workflow abgeschlossen</span>
          </label>
        </div>

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
            disabled={isSavingNotificationEmailConfiguration || isLoading || !hasNotificationEmailDraftChanges}
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
        ) : (
          <p className="panel-note">Keine ungespeicherten Änderungen in der Mail-Konfiguration.</p>
        )}
      </div>
    </section>
  );
}
