import type {
  AdminAnswerDefinition,
  AdminResponsibilityOwner,
  AdminTaskSpec,
  AdminTaskSpecCondition,
  AdminTaskSpecDependency,
} from "../../types/auth";
import { getDefaultWorkflowBuilderNodeTitle, type WorkflowBuilderNodeDraft } from "../../hooks/adminWorkflowBuilderModel";

export type MeasureConditionSummary = {
  id: number;
  label: string;
};

export type MeasureConditionGroupSummary = {
  group: number;
  explanation: string;
  conditions: MeasureConditionSummary[];
};

export type MeasureDependencySummary = {
  id: number;
  label: string;
};

export type MeasureTemplateCard = {
  templateId: number;
  title: string;
  description: string;
  areaLabel: string;
  responsibilityLabel: string;
  isRequired: boolean;
  conditionGroups: MeasureConditionGroupSummary[];
  dependencies: MeasureDependencySummary[];
};

export type MeasureNodeDetails = {
  workflowDefinitionId: number | null;
  processTypeKey: string | null;
  processTypeName: string | null;
  measureTypeLabel: string;
  measureSummary: string;
  templates: MeasureTemplateCard[];
};

export function buildMeasureNodeDetails(
  node: WorkflowBuilderNodeDraft,
  workflowDefinitionId: number | null,
  processTypeKey: string | null,
  processTypeName: string | null,
  taskTemplates: AdminTaskSpec[],
  answerDefinitions: AdminAnswerDefinition[],
  taskTemplateConditions: AdminTaskSpecCondition[],
  taskTemplateDependencies: AdminTaskSpecDependency[],
  responsibilityOwners: AdminResponsibilityOwner[]
): MeasureNodeDetails {
  const measureTypeLabel = getMeasureTypeLabel(node.nodeType, processTypeKey, processTypeName);
  const measureSummary = getMeasureProcessSummary(node.nodeType, processTypeKey);
  const relevantTemplates = workflowDefinitionId
    ? taskTemplates
        .filter((tpl) => tpl.workflowDefinitionId === workflowDefinitionId && tpl.isActive && tpl.isDepartmentPhaseTask)
        .sort((l, r) => l.sortOrder - r.sortOrder || l.title.localeCompare(r.title, "de"))
    : [];
  const answerByKey = new Map(
    answerDefinitions.map((d) => [`${d.workflowDefinitionId}:${d.answerKey.trim().toLowerCase()}`, d] as const)
  );
  const templatesById = new Map(relevantTemplates.map((t) => [t.id, t] as const));
  const ownersById = new Map(responsibilityOwners.map((o) => [o.responsibilityId, o] as const));

  const templates = relevantTemplates.map<MeasureTemplateCard>((template) => {
    const conditionGroups = buildConditionGroupSummaries(template.id, workflowDefinitionId, answerByKey, taskTemplateConditions);
    const dependencies = buildDependencySummaries(template.id, templatesById, taskTemplateDependencies);
    const owner = template.defaultResponsibilityId ? ownersById.get(template.defaultResponsibilityId) ?? null : null;
    return {
      templateId: template.id,
      title: template.title,
      description: template.description ?? "",
      areaLabel: owner?.departmentName?.trim() || owner?.responsibilityName?.trim() || "Mehrere Bereiche",
      responsibilityLabel: owner?.responsibilityName?.trim() || "Noch nicht festgelegt",
      isRequired: template.isRequired,
      conditionGroups,
      dependencies,
    };
  });

  return { workflowDefinitionId, processTypeKey, processTypeName, measureTypeLabel, measureSummary, templates };
}

function getMeasureTypeLabel(
  nodeType: WorkflowBuilderNodeDraft["nodeType"],
  processTypeKey: string | null,
  processTypeName: string | null
): string {
  if (nodeType === "measure_change") {
    if (processTypeKey === "position_change") {
      return processTypeName ? `Änderungsmaßnahmen für ${processTypeName}` : "Änderungsmaßnahmen für Positionswechsel";
    }
    if (processTypeKey === "role_change") {
      return processTypeName ? `Änderungsmaßnahmen für ${processTypeName}` : "Änderungsmaßnahmen für Rollenwechsel";
    }
  }
  if (nodeType === "measure_rename" && processTypeName) {
    return `Umbenennungsmaßnahmen für ${processTypeName}`;
  }
  return getDefaultWorkflowBuilderNodeTitle(nodeType);
}

function getMeasureProcessSummary(
  nodeType: WorkflowBuilderNodeDraft["nodeType"],
  processTypeKey: string | null
): string {
  switch (processTypeKey) {
    case "name_change":
      return "Bündelt Namens-, Anzeigenamen-, Mail- und Verzeichnisumstellungen.";
    case "position_change":
      return "Bündelt positionsbezogene Berechtigungs-, Zugriffs-, Schulungs- und Systemanpassungen.";
    case "role_change":
      return "Bündelt rollenbezogene Rollen-, Berechtigungs- und Systemanpassungen.";
    default:
      switch (nodeType) {
        case "measure_provision":
          return "Bündelt Bereitstellungsmaßnahmen für betroffene Bereiche.";
        case "measure_deprovision":
          return "Bündelt Entzugsmaßnahmen für betroffene Bereiche.";
        case "measure_change":
          return "Bündelt Änderungsmaßnahmen für betroffene Bereiche.";
        case "measure_rename":
          return "Bündelt Umbenennungsmaßnahmen für Identitäts- und Systemdaten.";
        default:
          return "Bündelt interne Maßnahmen.";
      }
  }
}

function buildConditionGroupSummaries(
  templateId: number,
  workflowDefinitionId: number | null,
  answerByCompositeKey: Map<string, AdminAnswerDefinition>,
  conditions: AdminTaskSpecCondition[]
): MeasureConditionGroupSummary[] {
  const grouped = new Map<number, AdminTaskSpecCondition[]>();
  for (const condition of conditions.filter((c) => c.taskSpecId === templateId)) {
    const current = grouped.get(condition.conditionGroup) ?? [];
    current.push(condition);
    grouped.set(condition.conditionGroup, current);
  }

  return [...grouped.entries()]
    .sort(([l], [r]) => l - r)
    .map(([group, group_conditions]) => ({
      group,
      explanation: group_conditions.length > 1
        ? "Alle Bedingungen dieser Gruppe müssen erfüllt sein."
        : "Diese Bedingung reicht aus.",
      conditions: group_conditions.map((condition) => {
        const answer = workflowDefinitionId !== null
          ? answerByCompositeKey.get(`${workflowDefinitionId}:${condition.answerKey.trim().toLowerCase()}`) ?? null
          : null;
        return { id: condition.id, label: formatConditionLabel(condition, answer) };
      }),
    }));
}

function buildDependencySummaries(
  templateId: number,
  templatesById: Map<number, AdminTaskSpec>,
  dependencies: AdminTaskSpecDependency[]
): MeasureDependencySummary[] {
  return dependencies
    .filter((d) => d.taskSpecId === templateId)
    .map((dependency) => {
      const sourceTitle = templatesById.get(dependency.dependsOnTaskSpecId)?.title
        ?? dependency.dependsOnSpecTitle
        ?? "vorgelagerte Maßnahme";
      return { id: dependency.id, label: formatDependencyLabel(dependency.requiredStatus, sourceTitle) };
    });
}

function formatConditionLabel(condition: AdminTaskSpecCondition, answer: AdminAnswerDefinition | null): string {
  const answerLabel = answer?.title ?? "passende Anforderung";
  switch (condition.operator) {
    case "is_true": return `${answerLabel} ist ausgewählt`;
    case "is_false": return `${answerLabel} ist nicht ausgewählt`;
    case "is_not_null": return `${answerLabel} ist gepflegt`;
    case "is_null": return `${answerLabel} ist leer`;
    case "eq":
      if (condition.expectedValueText?.trim()) return `${answerLabel} = '${condition.expectedValueText.trim()}'`;
      if (condition.expectedValueBoolean !== null) return `${answerLabel} = ${condition.expectedValueBoolean ? "Ja" : "Nein"}`;
      if (condition.expectedValueNumber !== null) return `${answerLabel} = ${condition.expectedValueNumber}`;
      return `${answerLabel} hat passenden Wert`;
    case "neq":
      if (condition.expectedValueText?.trim()) return `${answerLabel} ≠ '${condition.expectedValueText.trim()}'`;
      if (condition.expectedValueBoolean !== null) return `${answerLabel} ≠ ${condition.expectedValueBoolean ? "Ja" : "Nein"}`;
      if (condition.expectedValueNumber !== null) return `${answerLabel} ≠ ${condition.expectedValueNumber}`;
      return `${answerLabel} hat abweichenden Wert`;
    default: return `${answerLabel} passt`;
  }
}

function formatDependencyLabel(requiredStatus: AdminTaskSpecDependency["requiredStatus"], source: string): string {
  switch (requiredStatus) {
    case "done": return `Wartet auf Abschluss von ${source}`;
    case "in_progress": return `Startet sobald ${source} in Bearbeitung ist`;
    case "ready": return `Startet sobald ${source} bereitsteht`;
    case "blocked": return `Wird relevant wenn ${source} blockiert ist`;
    case "open":
    default: return `Startet sobald ${source} eröffnet wurde`;
  }
}
