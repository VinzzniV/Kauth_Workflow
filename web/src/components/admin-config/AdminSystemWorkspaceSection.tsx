import type { AdminNotificationEmailConfiguration } from "../../types/auth";
import type { WorkflowConfig } from "../../types/workflow";
import { AdminNotificationEmailSection } from "./AdminNotificationEmailSection";
import { AdminProcessTypeSection } from "./AdminProcessTypeSection";
import { AdminWorkflowConfigurationSection } from "./AdminWorkflowConfigurationSection";

type AdminSystemWorkspaceSectionProps = {
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
  workflowConfig: WorkflowConfig | null;
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
  onSaveNotificationEmailConfiguration: () => void | Promise<void>;
  onSendNotificationEmailTest: () => void | Promise<void>;
};

export function AdminSystemWorkspaceSection(props: AdminSystemWorkspaceSectionProps) {
  return (
    <div className="content-stack">
      <AdminNotificationEmailSection
        notificationEmailConfiguration={props.notificationEmailConfiguration}
        notificationEnabledDraft={props.notificationEnabledDraft}
        notificationTenantIdDraft={props.notificationTenantIdDraft}
        notificationClientIdDraft={props.notificationClientIdDraft}
        notificationClientSecretDraft={props.notificationClientSecretDraft}
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
        onNotificationTenantIdChange={props.onNotificationTenantIdChange}
        onNotificationClientIdChange={props.onNotificationClientIdChange}
        onNotificationClientSecretChange={props.onNotificationClientSecretChange}
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

      <AdminProcessTypeSection />

      <AdminWorkflowConfigurationSection workflowConfig={props.workflowConfig} isLoading={props.isLoading} />
    </div>
  );
}
