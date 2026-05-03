import { useCallback } from "react";
import {
  createAdminTaskTemplate,
  deleteAdminTaskTemplate,
  updateAdminTaskTemplate,
} from "../services/adminConfigApi";
import { toNullableNumber, toNullableText } from "../components/admin-config/adminConfigHelpers";
import type { AdminTaskTemplateDataController } from "./useAdminTaskTemplateData";
import {
  EMPTY_CONDITION_DRAFT,
  EMPTY_DEPENDENCY_DRAFT,
  EMPTY_DRAFT,
  sortTemplates,
  toDraft,
  type OperationState,
} from "./adminTaskTemplateManagementModel";

type UseAdminTaskTemplateTemplateMutationsOptions = {
  confirm: ReturnType<typeof import("../components/feedback/useConfirmationDialog").useConfirmationDialog>;
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
  updateOperationState: (patch: Partial<OperationState>) => void;
  data: AdminTaskTemplateDataController;
};

export function useAdminTaskTemplateTemplateMutations({
  confirm,
  onNotice,
  onError,
  updateOperationState,
  data,
}: UseAdminTaskTemplateTemplateMutationsOptions) {
  const buildPayload = useCallback(() => {
    if (!data.selectedWorkflowDefinitionId) {
      throw new Error("Bitte zuerst einen Prozesstyp auswählen.");
    }

    if (!data.draft.specKey.trim()) {
      throw new Error("Template Key ist erforderlich.");
    }

    if (!data.draft.title.trim()) {
      throw new Error("Titel ist erforderlich.");
    }

    const sortOrder = Number(data.draft.sortOrder);
    if (Number.isNaN(sortOrder)) {
      throw new Error("Sortierung muss eine Zahl sein.");
    }

    const dueInDays = data.draft.dueInDays.trim();
    const parsedDueInDays = dueInDays ? Number(dueInDays) : null;
    if (dueInDays && (parsedDueInDays === null || Number.isNaN(parsedDueInDays) || parsedDueInDays < 0)) {
      throw new Error("Fällig in Tagen muss leer oder eine nicht negative Zahl sein.");
    }

    return {
      workflowDefinitionId: data.selectedWorkflowDefinitionId,
      specKey: data.draft.specKey.trim(),
      title: data.draft.title.trim(),
      category: data.draft.category.trim() || "general",
      description: data.draft.description,
      iconKey: toNullableText(data.draft.iconKey),
      owningDepartmentId: toNullableNumber(data.draft.owningDepartmentId),
      defaultResponsibilityId: toNullableNumber(data.draft.defaultResponsibilityId),
      processAreaLabel: toNullableText(data.draft.processAreaLabel),
      isDepartmentPhaseTask: data.draft.isDepartmentPhaseTask,
      isRequired: data.draft.isRequired,
      dueInDays: parsedDueInDays,
      sortOrder,
      isActive: data.draft.isActive,
    };
  }, [data]);

  const createTemplate = useCallback(async () => {
    let payload;
    try {
      payload = buildPayload();
    } catch (err) {
      onNotice(null);
      onError(err instanceof Error ? err.message : "Task-Template konnte nicht vorbereitet werden.");
      return;
    }

    updateOperationState({ isSaving: true });
    onNotice(null);
    onError(null);

    try {
      const createdTemplate = await createAdminTaskTemplate(payload);
      data.setTemplates((current) => sortTemplates(current.concat(createdTemplate)));
      data.setDependencyGraph((current) => ({
        ...current,
        nodes: current.nodes
          .concat({
            id: createdTemplate.id,
            title: createdTemplate.title,
            category: createdTemplate.category,
          })
          .sort((left, right) => left.title.localeCompare(right.title, "de")),
      }));
      data.setSelectedTemplateId(createdTemplate.id);
      data.setIsCreatingNew(false);
      data.setDraft(toDraft(createdTemplate));
      data.setConditions([]);
      data.setConditionDraft(EMPTY_CONDITION_DRAFT);
      data.setDependencies([]);
      data.setDependencyDraft(EMPTY_DEPENDENCY_DRAFT);
      onNotice(`Task-Template ${createdTemplate.title} wurde angelegt.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Task-Template konnte nicht angelegt werden.";
      onError(message);
    } finally {
      updateOperationState({ isSaving: false });
    }
  }, [buildPayload, data, onError, onNotice, updateOperationState]);

  const saveTemplate = useCallback(async () => {
    if (!data.selectedTemplateId) {
      return;
    }

    let payload;
    try {
      payload = buildPayload();
    } catch (err) {
      onNotice(null);
      onError(err instanceof Error ? err.message : "Task-Template konnte nicht vorbereitet werden.");
      return;
    }

    updateOperationState({ isSaving: true });
    onNotice(null);
    onError(null);

    try {
      const updatedTemplate = await updateAdminTaskTemplate(data.selectedTemplateId, payload);
      data.setTemplates((current) =>
        sortTemplates(current.map((item) => (item.id === updatedTemplate.id ? updatedTemplate : item)))
      );
      data.setDependencyGraph((current) => ({
        ...current,
        nodes: current.nodes.map((node) =>
          node.id === updatedTemplate.id
            ? {
                ...node,
                title: updatedTemplate.title,
                category: updatedTemplate.category,
              }
            : node
        ),
      }));
      data.setDraft(toDraft(updatedTemplate));
      onNotice(`Task-Template ${updatedTemplate.title} wurde gespeichert.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Task-Template konnte nicht gespeichert werden.";
      onError(message);
    } finally {
      updateOperationState({ isSaving: false });
    }
  }, [buildPayload, data, onError, onNotice, updateOperationState]);

  const removeTemplate = useCallback(async () => {
    if (!data.selectedTemplate) {
      return;
    }

    const selectedTemplate = data.selectedTemplate;
    const shouldDelete = await confirm({
      title: "Aufgabenvorlage löschen?",
      description: `Die Vorlage "${selectedTemplate.title}" wird aus der Konfiguration entfernt. Bestehende Workflow-Tasks bleiben bestehen, neue Vorgänge nutzen sie dann nicht mehr.`,
      confirmLabel: "Aufgabenvorlage löschen",
      tone: "danger",
    });
    if (!shouldDelete) {
      return;
    }

    updateOperationState({ isDeleting: true });
    onNotice(null);
    onError(null);

    try {
      await deleteAdminTaskTemplate(selectedTemplate.id);
      data.setTemplates((current) => current.filter((item) => item.id !== selectedTemplate.id));
      data.setDependencyGraph((current) => ({
        nodes: current.nodes.filter((node) => node.id !== selectedTemplate.id),
        edges: current.edges.filter(
          (edge) => edge.sourceSpecId !== selectedTemplate.id && edge.targetSpecId !== selectedTemplate.id
        ),
      }));
      data.setConditions([]);
      data.setDependencies([]);
      data.setSelectedTemplateId(null);
      data.setIsCreatingNew(false);
      data.setDraft(EMPTY_DRAFT);
      data.setConditionDraft(EMPTY_CONDITION_DRAFT);
      data.setDependencyDraft(EMPTY_DEPENDENCY_DRAFT);
      onNotice(`Task-Template ${selectedTemplate.title} wurde gelöscht.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Task-Template konnte nicht gelöscht werden.";
      onError(message);
    } finally {
      updateOperationState({ isDeleting: false });
    }
  }, [confirm, data, onError, onNotice, updateOperationState]);

  return {
    createTemplate,
    saveTemplate,
    removeTemplate,
  };
}
