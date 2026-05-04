import {
  CheckSquare,
  CircleDot,
  Edit3,
  FileText,
  GitBranch,
  GitMerge,
  PenLine,
  Play,
  Square,
  Type,
  UserMinus,
  UserPlus,
  Wrench,
  Zap,
  type LucideIcon,
} from "lucide-react";
import type { WorkflowBuilderNodeDraft } from "../../hooks/adminWorkflowBuilderModel";

export function getWorkflowBuilderNodeTypeLabel(
  nodeType: WorkflowBuilderNodeDraft["nodeType"]
): string {
  return getWorkflowBuilderNodeMeta(nodeType).label;
}

export type WorkflowBuilderNodeKind = WorkflowBuilderNodeDraft["nodeType"];

export type WorkflowBuilderNodeCategory = "control" | "user" | "measure" | "automation";

export type WorkflowBuilderNodeMeta = {
  label: string;
  category: WorkflowBuilderNodeCategory;
  icon: LucideIcon;
};

export const WORKFLOW_BUILDER_NODE_META: Record<WorkflowBuilderNodeKind, WorkflowBuilderNodeMeta> = {
  start: { label: "Start", category: "control", icon: Play },
  end: { label: "Ende", category: "control", icon: Square },
  decision: { label: "Entscheidung", category: "control", icon: GitBranch },
  parallel_split: { label: "Parallel-Split", category: "control", icon: GitBranch },
  parallel_join: { label: "Parallel-Join", category: "control", icon: GitMerge },
  form: { label: "Formular", category: "user", icon: FileText },
  approval: { label: "Freigabe", category: "user", icon: CheckSquare },
  task: { label: "Aufgabe", category: "user", icon: CircleDot },
  measure_provision: { label: "Bereitstellung", category: "measure", icon: UserPlus },
  measure_deprovision: { label: "Entzug", category: "measure", icon: UserMinus },
  measure_change: { label: "Änderung", category: "measure", icon: Edit3 },
  measure_rename: { label: "Umbenennung", category: "measure", icon: Type },
  automation: { label: "Automatisierung", category: "automation", icon: Zap },
};

export function getWorkflowBuilderNodeMeta(nodeType: WorkflowBuilderNodeKind): WorkflowBuilderNodeMeta {
  return WORKFLOW_BUILDER_NODE_META[nodeType] ?? { label: "Schritt", category: "control", icon: PenLine };
}

export function getWorkflowBuilderNodeIcon(nodeType: WorkflowBuilderNodeKind): LucideIcon {
  return getWorkflowBuilderNodeMeta(nodeType).icon;
}

export function getWorkflowBuilderNodeCategory(nodeType: WorkflowBuilderNodeKind): WorkflowBuilderNodeCategory {
  return getWorkflowBuilderNodeMeta(nodeType).category;
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
