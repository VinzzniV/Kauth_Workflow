import {
  renderBuilderWorkspace,
  renderAccessWorkspace,
  renderAnswerWorkspace,
  renderDefaultWorkspace,
  renderDirectoryWorkspace,
  renderOrganizationWorkspace,
  renderOverviewWorkspace,
  renderRotationRequirementsWorkspace,
  renderSystemConfigurationWorkspace,
  renderSystemMailTemplatesWorkspace,
  renderSystemLogsWorkspace,
  renderTemplateWorkspace,
} from "./AdminConfigWorkspaceSections";
import type { AdminConfigWorkspaceContentProps } from "./adminConfigWorkspaceContentTypes";

export function AdminConfigWorkspaceContent(props: AdminConfigWorkspaceContentProps) {
  switch (props.section) {
    case "overview":
      return renderOverviewWorkspace(props);
    case "organization":
      return renderOrganizationWorkspace(props);
    case "rotation_requirements":
      return renderRotationRequirementsWorkspace(props);
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
      return renderSystemLogsWorkspace(props);
    case "system_mail_templates":
      return renderSystemMailTemplatesWorkspace(props);
    case "system_configuration":
      return renderSystemConfigurationWorkspace(props);
    default:
      return null;
  }
}
