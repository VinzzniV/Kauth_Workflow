import { useCallback, useEffect, useMemo, useState } from "react";
import { toNullableText } from "../components/admin-config/adminConfigHelpers";
import {
  sendAdminNotificationEmailTest,
  updateAdminNotificationEmailConfiguration,
} from "../services/onboardingApi";
import type { AdminNotificationEmailConfiguration } from "../types/auth";

type UseAdminNotificationEmailConfigurationOptions = {
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
};

export function useAdminNotificationEmailConfiguration({
  onNotice,
  onError,
}: UseAdminNotificationEmailConfigurationOptions) {
  const [notificationEmailConfiguration, setNotificationEmailConfiguration] =
    useState<AdminNotificationEmailConfiguration | null>(null);
  const [notificationEnabledDraft, setNotificationEnabledDraft] = useState<boolean>(false);
  const [notificationTenantIdDraft, setNotificationTenantIdDraft] = useState<string>("");
  const [notificationClientIdDraft, setNotificationClientIdDraft] = useState<string>("");
  const [notificationClientSecretDraft, setNotificationClientSecretDraft] = useState<string>("");
  const [notificationSenderEmailDraft, setNotificationSenderEmailDraft] = useState<string>("");
  const [notificationFrontendBaseUrlDraft, setNotificationFrontendBaseUrlDraft] = useState<string>("");
  const [notificationTestRecipientDraft, setNotificationTestRecipientDraft] = useState<string>("");
  const [notificationSandboxRedirectDraft, setNotificationSandboxRedirectDraft] = useState<string>("");
  const [notificationNotifyOnWorkflowCreatedDraft, setNotificationNotifyOnWorkflowCreatedDraft] = useState<boolean>(true);
  const [notificationNotifyOnTaskReadyDraft, setNotificationNotifyOnTaskReadyDraft] = useState<boolean>(true);
  const [notificationNotifyOnWorkflowCompletedDraft, setNotificationNotifyOnWorkflowCompletedDraft] = useState<boolean>(true);
  const [isSavingNotificationEmailConfiguration, setIsSavingNotificationEmailConfiguration] =
    useState<boolean>(false);
  const [isSendingNotificationEmailTest, setIsSendingNotificationEmailTest] = useState<boolean>(false);

  useEffect(() => {
    if (!notificationEmailConfiguration) {
      setNotificationEnabledDraft(false);
      setNotificationTenantIdDraft("");
      setNotificationClientIdDraft("");
      setNotificationClientSecretDraft("");
      setNotificationSenderEmailDraft("");
      setNotificationFrontendBaseUrlDraft("");
      setNotificationTestRecipientDraft("");
      setNotificationSandboxRedirectDraft("");
      setNotificationNotifyOnWorkflowCreatedDraft(true);
      setNotificationNotifyOnTaskReadyDraft(true);
      setNotificationNotifyOnWorkflowCompletedDraft(true);
      return;
    }

    setNotificationEnabledDraft(notificationEmailConfiguration.enabled);
    setNotificationTenantIdDraft(notificationEmailConfiguration.tenantId ?? "");
    setNotificationClientIdDraft(notificationEmailConfiguration.clientId ?? "");
    setNotificationClientSecretDraft("");
    setNotificationSenderEmailDraft(notificationEmailConfiguration.senderEmail ?? "");
    setNotificationFrontendBaseUrlDraft(notificationEmailConfiguration.frontendBaseUrl);
    setNotificationTestRecipientDraft(notificationEmailConfiguration.testRecipientEmail ?? "");
    setNotificationSandboxRedirectDraft(notificationEmailConfiguration.sandboxRedirectEmail ?? "");
    setNotificationNotifyOnWorkflowCreatedDraft(notificationEmailConfiguration.notifyOnWorkflowCreated);
    setNotificationNotifyOnTaskReadyDraft(notificationEmailConfiguration.notifyOnTaskReady);
    setNotificationNotifyOnWorkflowCompletedDraft(notificationEmailConfiguration.notifyOnWorkflowCompleted);
  }, [notificationEmailConfiguration]);

  const hasNotificationEmailDraftChanges = useMemo(() => {
    if (!notificationEmailConfiguration) {
      return false;
    }

    const hasClientSecretReplacement = notificationClientSecretDraft.trim().length > 0;

    return (
      notificationEnabledDraft !== notificationEmailConfiguration.enabled
      || toNullableText(notificationTenantIdDraft) !== notificationEmailConfiguration.tenantId
      || toNullableText(notificationClientIdDraft) !== notificationEmailConfiguration.clientId
      || hasClientSecretReplacement
      || toNullableText(notificationSenderEmailDraft) !== notificationEmailConfiguration.senderEmail
      || notificationFrontendBaseUrlDraft.trim() !== notificationEmailConfiguration.frontendBaseUrl
      || toNullableText(notificationTestRecipientDraft) !== notificationEmailConfiguration.testRecipientEmail
      || toNullableText(notificationSandboxRedirectDraft) !== notificationEmailConfiguration.sandboxRedirectEmail
      || notificationNotifyOnWorkflowCreatedDraft !== notificationEmailConfiguration.notifyOnWorkflowCreated
      || notificationNotifyOnTaskReadyDraft !== notificationEmailConfiguration.notifyOnTaskReady
      || notificationNotifyOnWorkflowCompletedDraft !== notificationEmailConfiguration.notifyOnWorkflowCompleted
    );
  }, [
    notificationClientIdDraft,
    notificationClientSecretDraft,
    notificationEmailConfiguration,
    notificationEnabledDraft,
    notificationFrontendBaseUrlDraft,
    notificationNotifyOnTaskReadyDraft,
    notificationNotifyOnWorkflowCompletedDraft,
    notificationNotifyOnWorkflowCreatedDraft,
    notificationSandboxRedirectDraft,
    notificationSenderEmailDraft,
    notificationTenantIdDraft,
    notificationTestRecipientDraft,
  ]);

  const saveNotificationEmailConfiguration = useCallback(async () => {
    setIsSavingNotificationEmailConfiguration(true);
    onNotice(null);
    onError(null);

    try {
      const clientSecret = toNullableText(notificationClientSecretDraft);
      const updatedConfiguration = await updateAdminNotificationEmailConfiguration({
        enabled: notificationEnabledDraft,
        tenantId: toNullableText(notificationTenantIdDraft),
        clientId: toNullableText(notificationClientIdDraft),
        ...(clientSecret ? { clientSecret } : {}),
        senderEmail: toNullableText(notificationSenderEmailDraft),
        frontendBaseUrl: notificationFrontendBaseUrlDraft.trim(),
        testRecipientEmail: toNullableText(notificationTestRecipientDraft),
        sandboxRedirectEmail: toNullableText(notificationSandboxRedirectDraft),
        notifyOnWorkflowCreated: notificationNotifyOnWorkflowCreatedDraft,
        notifyOnTaskReady: notificationNotifyOnTaskReadyDraft,
        notifyOnWorkflowCompleted: notificationNotifyOnWorkflowCompletedDraft,
      });
      setNotificationEmailConfiguration(updatedConfiguration);
      setNotificationClientSecretDraft("");
      onNotice("Mail-Konfiguration wurde gespeichert.");
    } catch (err) {
      const message = err instanceof Error ? err.message : "Mail-Konfiguration konnte nicht gespeichert werden.";
      onError(message);
    } finally {
      setIsSavingNotificationEmailConfiguration(false);
    }
  }, [
    notificationClientIdDraft,
    notificationClientSecretDraft,
    notificationEnabledDraft,
    notificationFrontendBaseUrlDraft,
    notificationNotifyOnTaskReadyDraft,
    notificationNotifyOnWorkflowCompletedDraft,
    notificationNotifyOnWorkflowCreatedDraft,
    notificationSandboxRedirectDraft,
    notificationSenderEmailDraft,
    notificationTenantIdDraft,
    notificationTestRecipientDraft,
    onError,
    onNotice,
  ]);

  const sendNotificationEmailTest = useCallback(async () => {
    setIsSendingNotificationEmailTest(true);
    onNotice(null);
    onError(null);

    try {
      const response = await sendAdminNotificationEmailTest(toNullableText(notificationTestRecipientDraft));
      setNotificationEmailConfiguration(response.configuration);
      onNotice(response.result.message);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Testmail konnte nicht versendet werden.";
      onError(message);
    } finally {
      setIsSendingNotificationEmailTest(false);
    }
  }, [notificationTestRecipientDraft, onError, onNotice]);

  return {
    notificationEmailConfiguration,
    setNotificationEmailConfiguration,
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
    hasNotificationEmailDraftChanges,
    setNotificationEnabledDraft,
    setNotificationTenantIdDraft,
    setNotificationClientIdDraft,
    setNotificationClientSecretDraft,
    setNotificationSenderEmailDraft,
    setNotificationFrontendBaseUrlDraft,
    setNotificationTestRecipientDraft,
    setNotificationSandboxRedirectDraft,
    setNotificationNotifyOnWorkflowCreatedDraft,
    setNotificationNotifyOnTaskReadyDraft,
    setNotificationNotifyOnWorkflowCompletedDraft,
    saveNotificationEmailConfiguration,
    sendNotificationEmailTest,
  };
}
