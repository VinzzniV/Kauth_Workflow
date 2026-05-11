import { useEffect, useMemo, useState } from "react";
import {
  getAdminAnswerDefinitions,
  getAdminTaskTemplateConditions,
  getAdminTaskTemplateDependencies,
  getAdminTaskTemplates,
} from "../services/adminConfigApi";
import type {
  AdminAnswerDefinition,
  AdminTaskSpec,
  AdminTaskSpecCondition,
  AdminTaskSpecDependency,
  AdminWorkflowDefinitionSummary,
} from "../types/auth";
import type { WorkflowBuilderVersionDraft } from "./adminWorkflowBuilderModel";

export type AdminWorkflowVersionReferenceData = {
  taskTemplates: AdminTaskSpec[];
  answerDefinitions: AdminAnswerDefinition[];
  taskTemplateConditions: AdminTaskSpecCondition[];
  taskTemplateDependencies: AdminTaskSpecDependency[];
};

const EMPTY: AdminWorkflowVersionReferenceData = {
  taskTemplates: [],
  answerDefinitions: [],
  taskTemplateConditions: [],
  taskTemplateDependencies: [],
};

// Lädt Templates + AnswerDefinitions + zugehörige Conditions/Dependencies fuer
// die Workflow-Definitionen, die der aktuelle Builder-Draft via
// `workflowDefinitionKey` ODER via `node.config.workflowDefinitionKey`
// (mit `legacyProcessTypeKey` als read-only Fallback fuer Alt-Daten)
// referenziert. Re-lädt bei Änderung von `definitions` oder relevanten
// Draft-Feldern. Fehler werden swallowed (Fallback: leere Listen) — das ist
// Anzeigedaten fuer den Editor, kein kritischer Pfad.
export function useAdminWorkflowVersionReferenceData(
  versionDraft: WorkflowBuilderVersionDraft,
  definitions: AdminWorkflowDefinitionSummary[],
): AdminWorkflowVersionReferenceData {
  // Derived: welche workflow_definition_ids muessen geladen werden? Sync rein aus
  // Draft + Definitions herleitbar, gehoert daher in useMemo statt in useEffect.
  const workflowDefinitionIdsToLoad = useMemo(() => {
    const referencedDefinitionKeys = new Set<string>();

    if (versionDraft.workflowDefinitionKey.trim()) {
      referencedDefinitionKeys.add(versionDraft.workflowDefinitionKey.trim().toLowerCase());
    }

    for (const node of versionDraft.nodes) {
      try {
        const parsed = node.configText.trim() ? JSON.parse(node.configText) as Record<string, unknown> : null;
        const rawKey = typeof parsed?.workflowDefinitionKey === "string"
          ? parsed.workflowDefinitionKey
          : typeof parsed?.legacyProcessTypeKey === "string"
            ? parsed.legacyProcessTypeKey
            : "";
        const nodeWorkflowDefinitionKey = rawKey.trim().toLowerCase();
        if (nodeWorkflowDefinitionKey) {
          referencedDefinitionKeys.add(nodeWorkflowDefinitionKey);
        }
      } catch {
        continue;
      }
    }

    const ids = new Set<number>();
    for (const definition of definitions) {
      if (referencedDefinitionKeys.has(definition.key.trim().toLowerCase())) {
        ids.add(definition.id);
      }
    }
    return ids;
  }, [definitions, versionDraft.nodes, versionDraft.workflowDefinitionKey]);

  const [data, setData] = useState<AdminWorkflowVersionReferenceData>(EMPTY);

  useEffect(() => {
    if (workflowDefinitionIdsToLoad.size === 0) {
      // Daten zuruecksetzen, falls ein vorheriger Lauf welche befuellt hat.
      // Async, damit der `react-hooks/set-state-in-effect`-Linter nicht meckert
      // — synchron-im-Effekt-Body waere ein cascading render.
      let cancelled = false;
      void Promise.resolve().then(() => {
        if (!cancelled) setData(EMPTY);
      });
      return () => {
        cancelled = true;
      };
    }

    let cancelled = false;
    void Promise.all(
      [...workflowDefinitionIdsToLoad].map(async (workflowDefinitionId) =>
        Promise.all([
          getAdminTaskTemplates(workflowDefinitionId, { limit: 200 }),
          getAdminAnswerDefinitions(workflowDefinitionId, { limit: 200 }),
        ])
      )
    )
      .then(async (loadedGroups) => {
        if (cancelled) {
          return;
        }

        const templatesByKey = new Map<string, AdminTaskSpec>();
        const answerDefinitionsByCompositeKey = new Map<string, AdminAnswerDefinition>();
        for (const [templatesPage, answerDefinitionsPage] of loadedGroups) {
          const loadedTemplates = templatesPage.items;
          const loadedAnswerDefinitions = answerDefinitionsPage.items;
          for (const template of loadedTemplates) {
            const normalizedKey = template.specKey.trim().toLowerCase();
            if (normalizedKey && !templatesByKey.has(normalizedKey)) {
              templatesByKey.set(normalizedKey, template);
            }
          }

          for (const definition of loadedAnswerDefinitions) {
            const compositeKey = `${definition.workflowDefinitionId}:${definition.answerKey.trim().toLowerCase()}`;
            if (definition.answerKey.trim() && !answerDefinitionsByCompositeKey.has(compositeKey)) {
              answerDefinitionsByCompositeKey.set(compositeKey, definition);
            }
          }
        }

        const dedupedTemplates = [...templatesByKey.values()];
        const templatesWithConditions = dedupedTemplates.filter((template) => template.conditionCount > 0);
        const templatesWithDependencies = dedupedTemplates.filter((template) => template.dependencyCount > 0);

        const [loadedConditions, loadedDependencies] = await Promise.all([
          Promise.all(templatesWithConditions.map((template) => getAdminTaskTemplateConditions(template.id, { limit: 200 }))),
          Promise.all(templatesWithDependencies.map((template) => getAdminTaskTemplateDependencies(template.id, { limit: 200 }))),
        ]);

        if (cancelled) {
          return;
        }

        setData({
          taskTemplates: dedupedTemplates,
          answerDefinitions: [...answerDefinitionsByCompositeKey.values()],
          taskTemplateConditions: loadedConditions.flatMap((page) => page.items),
          taskTemplateDependencies: loadedDependencies.flatMap((page) => page.items),
        });
      })
      .catch(() => {
        if (!cancelled) {
          setData(EMPTY);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [workflowDefinitionIdsToLoad]);

  return data;
}
