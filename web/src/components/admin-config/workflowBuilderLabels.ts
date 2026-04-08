import type { WorkflowBuilderNodeDraft } from "../../hooks/adminWorkflowBuilderModel";

export function getWorkflowBuilderNodeTypeLabel(
  nodeType: WorkflowBuilderNodeDraft["nodeType"]
): string {
  switch (nodeType) {
    case "start":
      return "Start";
    case "form":
      return "Formular";
    case "approval":
      return "Freigabe";
    case "task":
      return "Task";
    case "decision":
      return "Entscheidung";
    case "automation":
      return "Automation";
    case "end":
      return "Ende";
    default:
      return "Node";
  }
}

export const WORKFLOW_BUILDER_TECHNICAL_LABELS = {
  sourceNode: "Source Node",
  targetNode: "Target Node",
  priority: "Priority",
  conditionExpression: "Condition Expression",
  processTypeKey: "Process Type Key",
  templateKey: "Template Key",
  nodeKey: "Node Key",
  sortOrder: "Sort Order",
  configJson: "Config JSON",
  executionOrder: "Execution Order",
  inputMappingJson: "Input Mapping (JSON)",
  incoming: "Incoming",
  outgoing: "Outgoing",
} as const;
