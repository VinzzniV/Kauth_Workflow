import { useCallback, useEffect, useMemo, useState } from "react";
import { toNullableText } from "../components/admin-config/adminConfigHelpers";
import {
  sendAdminNotificationEmailTest,
  updateAdminNotificationEmailConfiguration,
} from "../services/adminApi";
import type { AdminNotificationEmailConfiguration } from "../types/auth";

type UseAdminNotificationEmailConfigurationOptions = {
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
};

export type NotificationDraft = {
  enabled: boolean;
  senderEmail: string;
  frontendBaseUrl: string;
  testRecipientEmail: string;
  sandboxRedirectEmail: string;
  notifyOnWorkflowCreated: boolean;
  notifyOnTaskReady: boolean;
  notifyOnWorkflowCompleted: boolean;
};

const EMPTY_NOTIFICATION_DRAFT: NotificationDraft = {
  enabled: false,
  senderEmail: "",
  frontendBaseUrl: "",
  testRecipientEmail: "",
  sandboxRedirectEmail: "",
  notifyOnWorkflowCreated: true,
  notifyOnTaskReady: true,
  notifyOnWorkflowCompleted: true,
};

export function buildNotificationDraft(
  source: AdminNotificationEmailConfiguration | null
): NotificationDraft {
  if (!source) {
    return EMPTY_NOTIFICATION_DRAFT;
  }

  return {
    enabled: source.enabled,
    senderEmail: source.senderEmail ?? "",
    frontendBaseUrl: source.frontendBaseUrl,
    testRecipientEmail: source.testRecipientEmail ?? "",
    sandboxRedirectEmail: source.sandboxRedirectEmail ?? "",
    notifyOnWorkflowCreated: source.notifyOnWorkflowCreated,
    notifyOnTaskReady: source.notifyOnTaskReady,
    notifyOnWorkflowCompleted: source.notifyOnWorkflowCompleted,
  };
}

export function hasNotificationDraftChanges(
  draft: NotificationDraft,
  source: AdminNotificationEmailConfiguration | null
): boolean {
  if (!source) {
    return false;
  }

  return (
    draft.enabled !== source.enabled
    || toNullableText(draft.senderEmail) !== source.senderEmail
    || draft.frontendBaseUrl.trim() !== source.frontendBaseUrl
    || toNullableText(draft.testRecipientEmail) !== source.testRecipientEmail
    || toNullableText(draft.sandboxRedirectEmail) !== source.sandboxRedirectEmail
    || draft.notifyOnWorkflowCreated !== source.notifyOnWorkflowCreated
    || draft.notifyOnTaskReady !== source.notifyOnTaskReady
    || draft.notifyOnWorkflowCompleted !== source.notifyOnWorkflowCompleted
  );
}

export function useAdminNotificationEmailConfiguration({
  onNotice,
  onError,
}: UseAdminNotificationEmailConfigurationOptions) {
  const [notificationEmailConfiguration, setNotificationEmailConfiguration] =
    useState<AdminNotificationEmailConfiguration | null>(null);
  const [draft, setDraft] = useState<NotificationDraft>(EMPTY_NOTIFICATION_DRAFT);
  const [isSavingNotificationEmailConfiguration, setIsSavingNotificationEmailConfiguration] =
    useState<boolean>(false);
  const [isSendingNotificationEmailTest, setIsSendingNotificationEmailTest] = useState<boolean>(false);

  useEffect(() => {
    setDraft(buildNotificationDraft(notificationEmailConfiguration));
  }, [notificationEmailConfiguration]);

  const hasNotificationEmailDraftChanges = useMemo(() => {
    return hasNotificationDraftChanges(draft, notificationEmailConfiguration);
  }, [draft, notificationEmailConfiguration]);

  const saveNotificationEmailConfiguration = useCallback(async () => {
    setIsSavingNotificationEmailConfiguration(true);
    onNotice(null);
    onError(null);

    try {
      const updatedConfiguration = await updateAdminNotificationEmailConfiguration({
        enabled: draft.enabled,
        senderEmail: toNullableText(draft.senderEmail),
        frontendBaseUrl: draft.frontendBaseUrl.trim(),
        testRecipientEmail: toNullableText(draft.testRecipientEmail),
        sandboxRedirectEmail: toNullableText(draft.sandboxRedirectEmail),
        notifyOnWorkflowCreated: draft.notifyOnWorkflowCreated,
        notifyOnTaskReady: draft.notifyOnTaskReady,
        notifyOnWorkflowCompleted: draft.notifyOnWorkflowCompleted,
      });
      setNotificationEmailConfiguration(updatedConfiguration);
      onNotice("Mail-Konfiguration wurde gespeichert.");
    } catch (err) {
      const message = err instanceof Error ? err.message : "Mail-Konfiguration konnte nicht gespeichert werden.";
      onError(message);
    } finally {
      setIsSavingNotificationEmailConfiguration(false);
    }
  }, [
    draft,
    onError,
    onNotice,
  ]);

  const sendNotificationEmailTest = useCallback(async () => {
    setIsSendingNotificationEmailTest(true);
    onNotice(null);
    onError(null);

    try {
      const response = await sendAdminNotificationEmailTest(toNullableText(draft.testRecipientEmail));
      setNotificationEmailConfiguration(response.configuration);
      onNotice(response.result.message);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Testmail konnte nicht versendet werden.";
      onError(message);
    } finally {
      setIsSendingNotificationEmailTest(false);
    }
  }, [draft.testRecipientEmail, onError, onNotice]);

  const updateDraft = useCallback(<K extends keyof NotificationDraft>(key: K, value: NotificationDraft[K]) => {
    setDraft((current) => ({ ...current, [key]: value }));
  }, []);

  return {
    notificationEmailConfiguration,
    setNotificationEmailConfiguration,
    notificationEnabledDraft: draft.enabled,
    notificationSenderEmailDraft: draft.senderEmail,
    notificationFrontendBaseUrlDraft: draft.frontendBaseUrl,
    notificationTestRecipientDraft: draft.testRecipientEmail,
    notificationSandboxRedirectDraft: draft.sandboxRedirectEmail,
    notificationNotifyOnWorkflowCreatedDraft: draft.notifyOnWorkflowCreated,
    notificationNotifyOnTaskReadyDraft: draft.notifyOnTaskReady,
    notificationNotifyOnWorkflowCompletedDraft: draft.notifyOnWorkflowCompleted,
    isSavingNotificationEmailConfiguration,
    isSendingNotificationEmailTest,
    hasNotificationEmailDraftChanges,
    setNotificationEnabledDraft: (value: boolean) => updateDraft("enabled", value),
    setNotificationSenderEmailDraft: (value: string) => updateDraft("senderEmail", value),
    setNotificationFrontendBaseUrlDraft: (value: string) => updateDraft("frontendBaseUrl", value),
    setNotificationTestRecipientDraft: (value: string) => updateDraft("testRecipientEmail", value),
    setNotificationSandboxRedirectDraft: (value: string) => updateDraft("sandboxRedirectEmail", value),
    setNotificationNotifyOnWorkflowCreatedDraft: (value: boolean) => updateDraft("notifyOnWorkflowCreated", value),
    setNotificationNotifyOnTaskReadyDraft: (value: boolean) => updateDraft("notifyOnTaskReady", value),
    setNotificationNotifyOnWorkflowCompletedDraft: (value: boolean) => updateDraft("notifyOnWorkflowCompleted", value),
    saveNotificationEmailConfiguration,
    sendNotificationEmailTest,
  };
}
