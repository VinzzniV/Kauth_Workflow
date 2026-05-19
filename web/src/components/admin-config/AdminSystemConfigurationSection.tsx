import type { AdminGraphApplicationConfiguration, AdminNotificationEmailConfiguration } from "../../types/auth";
import { AdminGraphApplicationSection } from "./AdminGraphApplicationSection";
import { AdminNotificationEmailSection } from "./AdminNotificationEmailSection";
import { AdminRuntimeConfigSnapshotSection } from "./AdminRuntimeConfigSnapshotSection";

type AdminSystemConfigurationSectionProps = {
  graphApplicationConfiguration: AdminGraphApplicationConfiguration | null;
  notificationEmailConfiguration: AdminNotificationEmailConfiguration | null;
  notificationEnabledDraft: boolean;
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
  onNotificationSenderEmailChange: (value: string) => void;
  onNotificationFrontendBaseUrlChange: (value: string) => void;
  onNotificationTestRecipientChange: (value: string) => void;
  onNotificationSandboxRedirectChange: (value: string) => void;
  onNotificationNotifyOnWorkflowCreatedChange: (value: boolean) => void;
  onNotificationNotifyOnTaskReadyChange: (value: boolean) => void;
  onNotificationNotifyOnWorkflowCompletedChange: (value: boolean) => void;
  onSaveNotificationEmailConfiguration: () => void | Promise<void>;
  onSendNotificationEmailTest: () => void | Promise<void>;
};

export function AdminSystemConfigurationSection(props: AdminSystemConfigurationSectionProps) {
  return (
    <div className="content-stack">
      <AdminGraphApplicationSection graphApplicationConfiguration={props.graphApplicationConfiguration} />

      <AdminNotificationEmailSection
        notificationEmailConfiguration={props.notificationEmailConfiguration}
        notificationEnabledDraft={props.notificationEnabledDraft}
        notificationSenderEmailDraft={props.notificationSenderEmailDraft}
        notificationFrontendBaseUrlDraft={props.notificationFrontendBaseUrlDraft}
        notificationTestRecipientDraft={props.notificationTestRecipientDraft}
        notificationSandboxRedirectDraft={props.notificationSandboxRedirectDraft}
        notificationNotifyOnWorkflowCreatedDraft={props.notificationNotifyOnWorkflowCreatedDraft}
        notificationNotifyOnTaskReadyDraft={props.notificationNotifyOnTaskReadyDraft}
        notificationNotifyOnWorkflowCompletedDraft={props.notificationNotifyOnWorkflowCompletedDraft}
        isSavingNotificationEmailConfiguration={props.isSavingNotificationEmailConfiguration}
        isSendingNotificationEmailTest={props.isSendingNotificationEmailTest}
        isLoading={props.isLoading}
        hasNotificationEmailDraftChanges={props.hasNotificationEmailDraftChanges}
        onNotificationEnabledChange={props.onNotificationEnabledChange}
        onNotificationSenderEmailChange={props.onNotificationSenderEmailChange}
        onNotificationFrontendBaseUrlChange={props.onNotificationFrontendBaseUrlChange}
        onNotificationTestRecipientChange={props.onNotificationTestRecipientChange}
        onNotificationSandboxRedirectChange={props.onNotificationSandboxRedirectChange}
        onNotificationNotifyOnWorkflowCreatedChange={props.onNotificationNotifyOnWorkflowCreatedChange}
        onNotificationNotifyOnTaskReadyChange={props.onNotificationNotifyOnTaskReadyChange}
        onNotificationNotifyOnWorkflowCompletedChange={props.onNotificationNotifyOnWorkflowCompletedChange}
        onSave={props.onSaveNotificationEmailConfiguration}
        onSendTest={props.onSendNotificationEmailTest}
      />

      <AdminRuntimeConfigSnapshotSection />
    </div>
  );
}
