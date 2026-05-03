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
    case "measure_provision":
      return "Bereitstellung";
    case "measure_deprovision":
      return "Entzug";
    case "measure_change":
      return "Änderung";
    case "measure_rename":
      return "Umbenennung";
    case "task":
      return "Aufgabe";
    case "decision":
      return "Entscheidung";
    case "automation":
      return "Automatisierung";
    case "parallel_split":
      return "Parallel-Split";
    case "parallel_join":
      return "Parallel-Join";
    case "end":
      return "Ende";
    default:
      return "Schritt";
  }
}

export const WORKFLOW_BUILDER_TECHNICAL_LABELS = {
  sourceNode: "Ausgangsschritt",
  targetNode: "Nächster Schritt",
  priority: "Pfad-Reihenfolge",
  conditionExpression: "Bedingung",
  processTypeKey: "Prozessbezug",
  specKey: "Vorlage",
  nodeKey: "Technischer Schritt-Key",
  sortOrder: "Sortierung",
  configJson: "Technische Konfiguration",
  executionOrder: "Aktions-Reihenfolge",
  inputMappingJson: "Eingabe-Mapping (JSON)",
  incoming: "Eingänge",
  outgoing: "Ausgänge",
} as const;
