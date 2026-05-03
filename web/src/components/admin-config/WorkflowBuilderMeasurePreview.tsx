import { useMemo, useState } from "react";
import type {
  AdminAnswerDefinition,
  AdminResponsibilityOwner,
  AdminTaskTemplate,
  AdminTaskTemplateCondition,
  AdminTaskTemplateDependency,
  AdminWorkflowDefinitionSummary,
} from "../../types/auth";
import type {
  WorkflowBuilderNodeDraft,
  WorkflowBuilderVersionDraft,
} from "../../hooks/adminWorkflowBuilderModel";
import { buildMeasureNodeDetails } from "./workflowBuilderMeasureAnalysis";

export type WorkflowBuilderMeasurePreviewProps = {
  node: WorkflowBuilderNodeDraft;
  versionDraft: WorkflowBuilderVersionDraft;
  workflowDefinitions: AdminWorkflowDefinitionSummary[];
  taskTemplates: AdminTaskTemplate[];
  answerDefinitions: AdminAnswerDefinition[];
  taskTemplateConditions: AdminTaskTemplateCondition[];
  taskTemplateDependencies: AdminTaskTemplateDependency[];
  responsibilityOwners: AdminResponsibilityOwner[];
};

export function WorkflowBuilderMeasurePreview({
  node,
  versionDraft,
  workflowDefinitions,
  taskTemplates,
  answerDefinitions,
  taskTemplateConditions,
  taskTemplateDependencies,
  responsibilityOwners,
}: WorkflowBuilderMeasurePreviewProps) {
  const [open, setOpen] = useState(false);

  const details = useMemo(() => {
    const processKey = versionDraft.primaryLegacyProcessTypeKey.trim().toLowerCase();
    const def = processKey
      ? workflowDefinitions.find((d) => d.key.trim().toLowerCase() === processKey) ?? null
      : null;
    return buildMeasureNodeDetails(
      node,
      def?.id ?? null,
      def?.key ?? null,
      def?.name ?? null,
      taskTemplates,
      answerDefinitions,
      taskTemplateConditions,
      taskTemplateDependencies,
      responsibilityOwners
    );
  }, [
    node,
    versionDraft.primaryLegacyProcessTypeKey,
    workflowDefinitions,
    taskTemplates,
    answerDefinitions,
    taskTemplateConditions,
    taskTemplateDependencies,
    responsibilityOwners,
  ]);

  const templateCount = details.templates.length;
  const requiredCount = details.templates.filter((t) => t.isRequired).length;

  return (
    <div className="wf-measure-preview">
      <button
        type="button"
        className="wf-measure-preview-toggle"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
      >
        {open ? "▼" : "►"} Geplante Maßnahmen ({templateCount}
        {templateCount > 0 ? `, davon ${requiredCount} Pflicht` : ""})
      </button>

      {open && (
        <div className="wf-measure-preview-body">
          <p className="wf-measure-preview-summary">
            <strong>{details.measureTypeLabel}</strong>
            {details.processTypeName ? ` · Prozess: ${details.processTypeName}` : null}
          </p>
          <p className="wf-measure-preview-summary text-secondary">{details.measureSummary}</p>

          {templateCount === 0 ? (
            <p className="wf-step-card-hint wf-step-card-hint--info">
              Für den gewählten Prozess sind aktuell keine aktiven Fachbereichs-Vorlagen geladen.
            </p>
          ) : (
            <ul className="wf-measure-template-list">
              {details.templates.map((card) => (
                <li key={card.templateId} className="wf-measure-template-item">
                  <div className="wf-measure-template-head">
                    <span className="wf-measure-template-title">{card.title}</span>
                    <span
                      className={`badge ${card.isRequired ? "badge-warning" : "badge-neutral"}`}
                    >
                      {card.isRequired ? "Pflicht" : "Optional"}
                    </span>
                  </div>
                  <p className="wf-measure-template-meta">
                    {card.areaLabel} · {card.responsibilityLabel}
                  </p>
                  {card.conditionGroups.length > 0 && (
                    <div className="wf-measure-template-sub">
                      <strong>Bedingungen:</strong>{" "}
                      {card.conditionGroups
                        .map((g) => g.conditions.map((c) => c.label).join(" UND "))
                        .join(" ODER ")}
                    </div>
                  )}
                  {card.dependencies.length > 0 && (
                    <div className="wf-measure-template-sub">
                      <strong>Hängt ab von:</strong>{" "}
                      {card.dependencies.map((d) => d.label).join("; ")}
                    </div>
                  )}
                </li>
              ))}
            </ul>
          )}

          <p className="wf-form-field-hint">
            Read-only Vorschau aus den Prozessdefinitionen. Bedingungen und Bereiche werden in der
            Prozesstyp-Verwaltung gepflegt, nicht hier.
          </p>
        </div>
      )}
    </div>
  );
}
