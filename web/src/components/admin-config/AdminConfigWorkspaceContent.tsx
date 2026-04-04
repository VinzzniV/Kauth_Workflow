import {
  renderAccessWorkspace,
  renderAnswerWorkspace,
  renderDefaultWorkspace,
  renderDirectoryWorkspace,
  renderOperationsWorkspace,
  renderOrganizationWorkspace,
  renderOverviewWorkspace,
  renderSystemWorkspace,
  renderTemplateWorkspace,
} from "./AdminConfigWorkspaceSections";
import type { AdminConfigWorkspaceContentProps } from "./adminConfigWorkspaceContentTypes";

export function AdminConfigWorkspaceContent(props: AdminConfigWorkspaceContentProps) {
  switch (props.section) {
    case "overview":
      return renderOverviewWorkspace(props);
    case "organization":
      return renderOrganizationWorkspace(props);
    case "access":
      return renderAccessWorkspace(props);
    case "directory":
      return renderDirectoryWorkspace(props);
    case "templates":
      return renderTemplateWorkspace(props);
    case "answers":
      return renderAnswerWorkspace(props);
    case "defaults":
      return renderDefaultWorkspace(props);
    case "system":
      return renderSystemWorkspace(props);
    case "operations":
      return renderOperationsWorkspace(props);
    default:
      return null;
  }
}
