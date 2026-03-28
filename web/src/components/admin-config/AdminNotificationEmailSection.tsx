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
  const tenantIdMissing = notificationEnabledDraft && notificationTenantIdDraft.trim().length === 0;
  const clientIdMissing = notificationEnabledDraft && notificationClientIdDraft.trim().length === 0;
  const clientSecretMissing =
    notificationEnabledDraft
    && !notificationEmailConfiguration?.hasClientSecret
    && notificationClientSecretDraft.trim().length === 0;
  const senderMissing = notificationEnabledDraft && notificationSenderEmailDraft.trim().length === 0;
  const frontendBaseUrlMissing = notificationEnabledDraft && notificationFrontendBaseUrlDraft.trim().length === 0;
  const saveBlockers = [
    ...(!notificationEmailConfiguration ? ["Die gespeicherte Mail-Konfiguration ist noch nicht geladen."] : []),
    ...(!hasNotificationEmailDraftChanges ? ["Es gibt aktuell keine ungespeicherten Änderungen."] : []),
    ...(tenantIdMissing ? ["Tenant ID fehlt für aktiven Mailversand."] : []),
    ...(clientIdMissing ? ["Client ID fehlt für aktiven Mailversand."] : []),
    ...(clientSecretMissing ? ["Client Secret fehlt für aktiven Mailversand."] : []),
    ...(senderMissing ? ["Sender-Mailadresse fehlt für aktiven Mailversand."] : []),
    ...(frontendBaseUrlMissing ? ["Frontend-Basis-URL fehlt für aktiven Mailversand."] : []),
  ];
  const canSave =
    !isSavingNotificationEmailConfiguration &&
    !isLoading &&
    hasNotificationEmailDraftChanges &&
    !tenantIdMissing &&
    !clientIdMissing &&
    !clientSecretMissing &&
    !senderMissing &&
    !frontendBaseUrlMissing;
  const testBlockers = [
    ...(!notificationEmailConfiguration ? ["Die gespeicherte Konfiguration ist noch nicht geladen."] : []),
    ...(isLoading ? ["Warten Sie, bis die Konfiguration vollständig geladen ist."] : []),
    ...(hasNotificationEmailDraftChanges ? ["Speichern Sie zuerst die Änderungen, bevor Sie eine Testmail senden."] : []),
    ...(notificationEmailConfiguration?.configurationStatus === "incomplete"
      ? ["Die gespeicherte Konfiguration ist noch unvollständig."]
      : []),
  ];
  const canSendTest =
    Boolean(notificationEmailConfiguration) &&
    !isLoading &&
    !isSendingNotificationEmailTest &&
    !isSavingNotificationEmailConfiguration &&
    !hasNotificationEmailDraftChanges;

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Konfiguration: Mailversand</h2>
        <p>Begrenzter Admin-Bereich für Mail-Konfiguration, Testversand und den aktuellen Versandstatus.</p>
      </div>

      <div className="dashboard-grid" aria-label="Mailversand Status">
        <article className="dashboard-stat-card card-stat">
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

        <article className="dashboard-stat-card card-stat">
          <div>
            <h2>Letzter Test</h2>
            <p>{notificationTestStatusLabel(notificationEmailConfiguration)}</p>
          </div>
          <p className="panel-note">
            Zuletzt geprüft: {formatTimestamp(notificationEmailConfiguration?.lastTestAt ?? null)}
          </p>
        </article>

        <article className="dashboard-stat-card card-stat">
          <div>
            <h2>Secret-Status</h2>
            <p>{notificationEmailConfiguration?.hasClientSecret ? "Hinterlegt" : "Fehlt"}</p>
          </div>
          <p className="panel-note">
            Das Secret wird nicht im Klartext geladen. Hier kann nur ein neues Secret gesetzt oder ein bestehendes ersetzt werden.
          </p>
        </article>
      </div>

      <div className="dashboard-card card-primary">
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

        <label className={`field compact ${tenantIdMissing ? "field-invalid" : ""}`}>
          <span>Tenant ID</span>
          <input
            type="text"
            value={notificationTenantIdDraft}
            onChange={(event) => onNotificationTenantIdChange(event.target.value)}
            placeholder="Microsoft Entra Tenant ID"
          />
          {tenantIdMissing ? <small className="field-error">Tenant ID wird für aktiven Mailversand benötigt.</small> : null}
        </label>

        <label className={`field compact ${clientIdMissing ? "field-invalid" : ""}`}>
          <span>Client ID</span>
          <input
            type="text"
            value={notificationClientIdDraft}
            onChange={(event) => onNotificationClientIdChange(event.target.value)}
            placeholder="App Registration Client ID"
          />
          {clientIdMissing ? <small className="field-error">Client ID wird für aktiven Mailversand benötigt.</small> : null}
        </label>

        <label className={`field compact ${clientSecretMissing ? "field-invalid" : ""}`}>
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
          {clientSecretMissing ? <small className="field-error">Für den ersten aktiven Versand muss ein Client Secret hinterlegt werden.</small> : null}
        </label>

        <p className="panel-note">
          {notificationEmailConfiguration?.hasClientSecret
            ? "Leer lassen, um das bestehende Secret unverändert zu behalten."
            : "Speichern Sie hier ein neues Client Secret."}
        </p>

        <label className={`field compact ${senderMissing ? "field-invalid" : ""}`}>
          <span>Sender-Mailadresse</span>
          <input
            type="email"
            value={notificationSenderEmailDraft}
            onChange={(event) => onNotificationSenderEmailChange(event.target.value)}
            placeholder="onboarding@example.com"
          />
          {senderMissing ? <small className="field-error">Sender-Mailadresse wird für aktiven Mailversand benötigt.</small> : null}
        </label>

        <label className={`field compact ${frontendBaseUrlMissing ? "field-invalid" : ""}`}>
          <span>Frontend-Basis-URL</span>
          <input
            type="url"
            value={notificationFrontendBaseUrlDraft}
            onChange={(event) => onNotificationFrontendBaseUrlChange(event.target.value)}
            placeholder="https://onboarding.example.com"
          />
          {frontendBaseUrlMissing ? <small className="field-error">Frontend-Basis-URL wird für Links in Benachrichtigungen benötigt.</small> : null}
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
            disabled={!canSave}
          >
            {isSavingNotificationEmailConfiguration ? "Speichern..." : "Mail-Konfiguration speichern"}
          </button>

          <button
            type="button"
            className="btn btn-secondary"
            onClick={() => {
              void onSendTest();
            }}
            disabled={!canSendTest}
          >
            {isSendingNotificationEmailTest ? "Sende Testmail..." : "Testmail senden"}
          </button>
        </div>

        {saveBlockers.length > 0 ? (
          <div className="panel panel-muted">
            <h3 className="panel-title">Speichern aktuell blockiert</h3>
            <ul className="validation-list">
              {saveBlockers.map((blocker) => (
                <li key={blocker}>{blocker}</li>
              ))}
            </ul>
          </div>
        ) : null}

        {testBlockers.length > 0 ? (
          <div className="panel panel-muted">
            <h3 className="panel-title">Testmail aktuell blockiert</h3>
            <ul className="validation-list">
              {testBlockers.map((blocker) => (
                <li key={blocker}>{blocker}</li>
              ))}
            </ul>
          </div>
        ) : null}
      </div>
    </section>
  );
}
