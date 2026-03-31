import type { AdminGraphApplicationConfiguration, AdminNotificationEmailConfiguration } from "../../types/auth";
import type { WorkflowConfig } from "../../types/workflow";
import { AdminGraphApplicationSection } from "./AdminGraphApplicationSection";
import { AdminNotificationEmailSection } from "./AdminNotificationEmailSection";
import { AdminProcessTypeSection } from "./AdminProcessTypeSection";
import { AdminWorkflowConfigurationSection } from "./AdminWorkflowConfigurationSection";

type AdminSystemWorkspaceSectionProps = {
  graphApplicationConfiguration: AdminGraphApplicationConfiguration | null;
  graphTenantIdDraft: string;
  graphClientIdDraft: string;
  graphClientSecretDraft: string;
  notificationEmailConfiguration: AdminNotificationEmailConfiguration | null;
  notificationEnabledDraft: boolean;
  notificationSenderEmailDraft: string;
  notificationFrontendBaseUrlDraft: string;
  notificationTestRecipientDraft: string;
  notificationSandboxRedirectDraft: string;
  notificationNotifyOnWorkflowCreatedDraft: boolean;
  notificationNotifyOnTaskReadyDraft: boolean;
  notificationNotifyOnWorkflowCompletedDraft: boolean;
  isSavingGraphApplicationConfiguration: boolean;
  isSavingNotificationEmailConfiguration: boolean;
  isSendingNotificationEmailTest: boolean;
  isLoading: boolean;
  hasGraphApplicationDraftChanges: boolean;
  hasNotificationEmailDraftChanges: boolean;
  workflowConfig: WorkflowConfig | null;
  onGraphTenantIdChange: (value: string) => void;
  onGraphClientIdChange: (value: string) => void;
  onGraphClientSecretChange: (value: string) => void;
  onNotificationEnabledChange: (enabled: boolean) => void;
  onNotificationSenderEmailChange: (value: string) => void;
  onNotificationFrontendBaseUrlChange: (value: string) => void;
  onNotificationTestRecipientChange: (value: string) => void;
  onNotificationSandboxRedirectChange: (value: string) => void;
  onNotificationNotifyOnWorkflowCreatedChange: (value: boolean) => void;
  onNotificationNotifyOnTaskReadyChange: (value: boolean) => void;
  onNotificationNotifyOnWorkflowCompletedChange: (value: boolean) => void;
  onSaveGraphApplicationConfiguration: () => void | Promise<void>;
  onSaveNotificationEmailConfiguration: () => void | Promise<void>;
  onSendNotificationEmailTest: () => void | Promise<void>;
};

export function AdminSystemWorkspaceSection(props: AdminSystemWorkspaceSectionProps) {
  return (
    <div className="content-stack">
      <AdminGraphApplicationSection
        graphApplicationConfiguration={props.graphApplicationConfiguration}
        graphTenantIdDraft={props.graphTenantIdDraft}
        graphClientIdDraft={props.graphClientIdDraft}
        graphClientSecretDraft={props.graphClientSecretDraft}
        isSavingGraphApplicationConfiguration={props.isSavingGraphApplicationConfiguration}
        hasGraphApplicationDraftChanges={props.hasGraphApplicationDraftChanges}
        onGraphTenantIdChange={props.onGraphTenantIdChange}
        onGraphClientIdChange={props.onGraphClientIdChange}
        onGraphClientSecretChange={props.onGraphClientSecretChange}
        onSave={props.onSaveGraphApplicationConfiguration}
      />

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

      <AdminProcessTypeSection />

      <AdminWorkflowConfigurationSection workflowConfig={props.workflowConfig} isLoading={props.isLoading} />
    </div>
  );
}
