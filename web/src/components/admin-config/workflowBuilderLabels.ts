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
      return "Aufgabe";
    case "decision":
      return "Entscheidung";
    case "automation":
      return "Automatisierung";
    case "end":
      return "Ende";
    default:
      return "Schritt";
  }
}

export const WORKFLOW_BUILDER_TECHNICAL_LABELS = {
  sourceNode: "Ausgangsschritt",
  targetNode: "Naechster Schritt",
  priority: "Pfad-Reihenfolge",
  conditionExpression: "Bedingung",
  processTypeKey: "Prozessbezug",
  templateKey: "Vorlage",
  nodeKey: "Technischer Schritt-Key",
  sortOrder: "Sortierung",
  configJson: "Technische Konfiguration",
  executionOrder: "Aktions-Reihenfolge",
  inputMappingJson: "Eingabe-Mapping (JSON)",
  incoming: "Eingaenge",
  outgoing: "Ausgaenge",
} as const;
