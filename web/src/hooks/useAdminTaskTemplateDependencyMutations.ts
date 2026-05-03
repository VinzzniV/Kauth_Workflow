import { useCallback, useMemo } from "react";
import {
  createAdminTaskTemplateDependency,
  deleteAdminTaskTemplateDependency,
} from "../services/adminConfigApi";
import type { AdminTaskTemplateDataController } from "./useAdminTaskTemplateData";
import { sortDependencies, type DependencyStatus, type OperationState } from "./adminTaskTemplateManagementModel";

type UseAdminTaskTemplateDependencyMutationsOptions = {
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
  updateOperationState: (patch: Partial<OperationState>) => void;
  data: AdminTaskTemplateDataController;
};

export function useAdminTaskTemplateDependencyMutations({
  onNotice,
  onError,
  updateOperationState,
  data,
}: UseAdminTaskTemplateDependencyMutationsOptions) {
  const availableDependencyTemplates = useMemo(() => {
    if (!data.selectedTemplate) {
      return [];
    }

    const existingDependencyIds = new Set(data.dependencies.map((dependency) => dependency.dependsOnTaskSpecId));
    return data.templates.filter(
      (template) => template.id !== data.selectedTemplate!.id && !existingDependencyIds.has(template.id)
    );
  }, [data.dependencies, data.selectedTemplate, data.templates]);

  const addDependency = useCallback(async () => {
    if (!data.selectedTemplate) {
      onNotice(null);
      onError("Bitte zuerst ein Task-Template auswählen.");
      return;
    }

    const dependsOnTaskSpecId = Number(data.dependencyDraft.dependsOnTaskSpecId);
    if (!Number.isFinite(dependsOnTaskSpecId) || dependsOnTaskSpecId <= 0) {
      onNotice(null);
      onError("Bitte ein gültiges abhängiges Template auswählen.");
      return;
    }

    updateOperationState({ isSavingDependency: true });
    onNotice(null);
    onError(null);

    try {
      const createdDependency = await createAdminTaskTemplateDependency(data.selectedTemplate.id, {
        dependsOnTaskSpecId,
        requiredStatus: data.dependencyDraft.requiredStatus,
      });

      data.setDependencies((current) => sortDependencies(current.concat(createdDependency)));
      data.setTemplates((current) =>
        current.map((item) =>
          item.id === data.selectedTemplate!.id ? { ...item, dependencyCount: item.dependencyCount + 1 } : item
        )
      );
      data.setDependencyGraph((current) => ({
        ...current,
        edges: current.edges.concat({
          id: createdDependency.id,
          sourceSpecId: createdDependency.dependsOnTaskSpecId,
          targetSpecId: createdDependency.taskSpecId,
          requiredStatus: createdDependency.requiredStatus,
        }),
      }));
      data.setDependencyDraft((current) => ({
        ...current,
        dependsOnTaskSpecId: "",
      }));
      onNotice("Abhängigkeit wurde angelegt.");
    } catch (err) {
      const message = err instanceof Error ? err.message : "Abhängigkeit konnte nicht angelegt werden.";
      onError(message);
    } finally {
      updateOperationState({ isSavingDependency: false });
    }
  }, [data, onError, onNotice, updateOperationState]);

  const createDependencyFromGraph = useCallback(async (
    sourceSpecId: number,
    targetSpecId: number,
    requiredStatus: DependencyStatus
  ) => {
    if (sourceSpecId === targetSpecId) {
      onNotice(null);
      onError("Ein Template kann nicht von sich selbst abhängen.");
      return;
    }

    updateOperationState({ isSavingDependency: true });
    onNotice(null);
    onError(null);

    try {
      const createdDependency = await createAdminTaskTemplateDependency(targetSpecId, {
        dependsOnTaskSpecId: sourceSpecId,
        requiredStatus,
      });

      data.setTemplates((current) =>
        current.map((item) =>
          item.id === targetSpecId ? { ...item, dependencyCount: item.dependencyCount + 1 } : item
        )
      );
      data.setDependencyGraph((current) => ({
        ...current,
        edges: current.edges.concat({
          id: createdDependency.id,
          sourceSpecId: createdDependency.dependsOnTaskSpecId,
          targetSpecId: createdDependency.taskSpecId,
          requiredStatus: createdDependency.requiredStatus,
        }),
      }));

      if (data.selectedTemplate?.id === targetSpecId) {
        data.setDependencies((current) => sortDependencies(current.concat(createdDependency)));
      }

      onNotice("Abhängigkeit wurde angelegt.");
    } catch (err) {
      const message = err instanceof Error ? err.message : "Abhängigkeit konnte nicht angelegt werden.";
      onError(message);
      throw err;
    } finally {
      updateOperationState({ isSavingDependency: false });
    }
  }, [data, onError, onNotice, updateOperationState]);

  const removeDependency = useCallback(async (dependencyId: number) => {
    if (!data.selectedTemplate) {
      return;
    }

    const selectedTemplate = data.selectedTemplate;
    updateOperationState({ deletingDependencyId: dependencyId });
    onNotice(null);
    onError(null);

    try {
      await deleteAdminTaskTemplateDependency(selectedTemplate.id, dependencyId);
      data.setDependencies((current) => current.filter((item) => item.id !== dependencyId));
      data.setTemplates((current) =>
        current.map((item) =>
          item.id === selectedTemplate.id ? { ...item, dependencyCount: Math.max(0, item.dependencyCount - 1) } : item
        )
      );
      data.setDependencyGraph((current) => ({
        ...current,
        edges: current.edges.filter((edge) => edge.id !== dependencyId),
      }));
      onNotice("Abhängigkeit wurde gelöscht.");
    } catch (err) {
      const message = err instanceof Error ? err.message : "Abhängigkeit konnte nicht gelöscht werden.";
      onError(message);
    } finally {
      updateOperationState({ deletingDependencyId: null });
    }
  }, [data, onError, onNotice, updateOperationState]);

  const removeDependencyFromGraph = useCallback(async (dependencyId: number) => {
    const edge = data.dependencyGraph.edges.find((entry) => entry.id === dependencyId);
    if (!edge) {
      return;
    }

    updateOperationState({ deletingDependencyId: dependencyId });
    onNotice(null);
    onError(null);

    try {
      await deleteAdminTaskTemplateDependency(edge.targetSpecId, dependencyId);
      data.setDependencyGraph((current) => ({
        ...current,
        edges: current.edges.filter((currentEdge) => currentEdge.id !== dependencyId),
      }));
      data.setTemplates((current) =>
        current.map((item) =>
          item.id === edge.targetSpecId ? { ...item, dependencyCount: Math.max(0, item.dependencyCount - 1) } : item
        )
      );

      if (data.selectedTemplate?.id === edge.targetSpecId) {
        data.setDependencies((current) => current.filter((item) => item.id !== dependencyId));
      }

      onNotice("Abhängigkeit wurde gelöscht.");
    } catch (err) {
      const message = err instanceof Error ? err.message : "Abhängigkeit konnte nicht gelöscht werden.";
      onError(message);
      throw err;
    } finally {
      updateOperationState({ deletingDependencyId: null });
    }
  }, [data, onError, onNotice, updateOperationState]);

  return {
    availableDependencyTemplates,
    addDependency,
    createDependencyFromGraph,
    removeDependency,
    removeDependencyFromGraph,
  };
}
