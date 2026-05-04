import {
  renderAbteilungenWorkspace,
  renderBuilderWorkspace,
  renderAccessWorkspace,
  renderAnswerWorkspace,
  renderDefaultWorkspace,
  renderDirectoryWorkspace,
  renderMassnahmenvorlagenWorkspace,
  renderOverviewWorkspace,
  renderPersonenWorkspace,
  renderSystemConfigurationWorkspace,
  renderSystemMailTemplatesWorkspace,
  renderSystemLogsWorkspace,
  renderTemplateWorkspace,
  renderZustaendigkeitenWorkspace,
} from "./AdminConfigWorkspaceSections";
import type { AdminConfigWorkspaceContentProps } from "./adminConfigWorkspaceContentTypes";

export function AdminConfigWorkspaceContent(props: AdminConfigWorkspaceContentProps) {
  switch (props.meta.section) {
    case "overview":
      return renderOverviewWorkspace(props);
    case "personen":
      return renderPersonenWorkspace(props);
    case "abteilungen":
      return renderAbteilungenWorkspace(props);
    case "zustaendigkeiten":
      return renderZustaendigkeitenWorkspace();
    case "massnahmenvorlagen":
      return renderMassnahmenvorlagenWorkspace();
    case "access":
      return renderAccessWorkspace(props);
    case "directory":
      return renderDirectoryWorkspace(props);
    case "templates":
      return renderTemplateWorkspace(props);
    case "builder":
      return renderBuilderWorkspace(props);
    case "answers":
      return renderAnswerWorkspace(props);
    case "defaults":
      return renderDefaultWorkspace(props);
    case "system_logs":
      return renderSystemLogsWorkspace();
    case "system_mail_templates":
      return renderSystemMailTemplatesWorkspace(props);
    case "system_configuration":
      return renderSystemConfigurationWorkspace(props);
    default:
      return null;
  }
}
