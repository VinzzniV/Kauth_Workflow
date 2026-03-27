import { useCallback, useEffect, useMemo, useState } from "react";
import {
  createAdminTaskTemplateDependency,
  createAdminTaskTemplateCondition,
  deleteAdminTaskTemplateDependency,
  deleteAdminTaskTemplateCondition,
  getAdminDependencyGraph,
  getAdminAnswerDefinitions,
  createAdminTaskTemplate,
  deleteAdminTaskTemplate,
  getAdminProcessTypes,
  getAdminTaskTemplateDependencies,
  getAdminTaskTemplateConditions,
  getAdminTaskTemplates,
  updateAdminTaskTemplate,
} from "../services/adminConfigApi";
import type {
  AdminAnswerDefinition,
  AdminDependencyGraph,
  AdminProcessType,
  AdminTaskTemplate,
  AdminTaskTemplateCondition,
  AdminTaskTemplateDependency,
} from "../types/auth";
import { toNullableNumber, toNullableText } from "../components/admin-config/adminConfigHelpers";

type TemplateDraft = {
  templateKey: string;
  title: string;
  category: string;
  description: string;
  iconKey: string;
  owningDepartmentId: string;
  defaultResponsibilityId: string;
  processAreaLabel: string;
  dueInDays: string;
  sortOrder: string;
  isDepartmentPhaseTask: boolean;
  isRequired: boolean;
  isActive: boolean;
};

type UseAdminTaskTemplateManagementOptions = {
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
};

type ConditionDraft = {
  conditionGroup: string;
  answerKey: string;
  operator: "eq" | "neq" | "is_true" | "is_false" | "is_null" | "is_not_null";
  expectedValueText: string;
  expectedValueBoolean: "true" | "false" | "";
  expectedValueNumber: string;
};

type DependencyDraft = {
  dependsOnTaskTemplateId: string;
  requiredStatus: "open" | "ready" | "in_progress" | "blocked" | "done";
};

type DependencyStatus = DependencyDraft["requiredStatus"];

const EMPTY_DRAFT: TemplateDraft = {
  templateKey: "",
  title: "",
  category: "general",
  description: "",
  iconKey: "berechtigungen",
  owningDepartmentId: "",
  defaultResponsibilityId: "",
  processAreaLabel: "",
  dueInDays: "",
  sortOrder: "0",
  isDepartmentPhaseTask: true,
  isRequired: true,
  isActive: true,
};

const EMPTY_CONDITION_DRAFT: ConditionDraft = {
  conditionGroup: "1",
  answerKey: "",
  operator: "eq",
  expectedValueText: "",
  expectedValueBoolean: "",
  expectedValueNumber: "",
};

const EMPTY_DEPENDENCY_DRAFT: DependencyDraft = {
  dependsOnTaskTemplateId: "",
  requiredStatus: "done",
};

function toDraft(template: AdminTaskTemplate): TemplateDraft {
  return {
    templateKey: template.templateKey,
    title: template.title,
    category: template.category,
    description: template.description,
    iconKey: template.iconKey ?? "",
    owningDepartmentId: template.owningDepartmentId ? String(template.owningDepartmentId) : "",
    defaultResponsibilityId: template.defaultResponsibilityId ? String(template.defaultResponsibilityId) : "",
    processAreaLabel: template.processAreaLabel ?? "",
    dueInDays: template.dueInDays === null ? "" : String(template.dueInDays),
    sortOrder: String(template.sortOrder),
    isDepartmentPhaseTask: template.isDepartmentPhaseTask,
    isRequired: template.isRequired,
    isActive: template.isActive,
  };
}

export function useAdminTaskTemplateManagement({
  onNotice,
  onError,
}: UseAdminTaskTemplateManagementOptions) {
  const [processTypes, setProcessTypes] = useState<AdminProcessType[]>([]);
  const [selectedProcessTypeId, setSelectedProcessTypeId] = useState<number | null>(null);
  const [templates, setTemplates] = useState<AdminTaskTemplate[]>([]);
  const [dependencyGraph, setDependencyGraph] = useState<AdminDependencyGraph>({ nodes: [], edges: [] });
  const [selectedTemplateId, setSelectedTemplateId] = useState<number | null>(null);
  const [draft, setDraft] = useState<TemplateDraft>(EMPTY_DRAFT);
  const [conditions, setConditions] = useState<AdminTaskTemplateCondition[]>([]);
  const [dependencies, setDependencies] = useState<AdminTaskTemplateDependency[]>([]);
  const [answerDefinitions, setAnswerDefinitions] = useState<AdminAnswerDefinition[]>([]);
  const [conditionDraft, setConditionDraft] = useState<ConditionDraft>(EMPTY_CONDITION_DRAFT);
  const [dependencyDraft, setDependencyDraft] = useState<DependencyDraft>(EMPTY_DEPENDENCY_DRAFT);
  const [isCreatingNew, setIsCreatingNew] = useState(false);
  const [isLoadingProcessTypes, setIsLoadingProcessTypes] = useState(true);
  const [isLoadingTemplates, setIsLoadingTemplates] = useState(false);
  const [isLoadingDependencyGraph, setIsLoadingDependencyGraph] = useState(false);
  const [isLoadingConditions, setIsLoadingConditions] = useState(false);
  const [isLoadingDependencies, setIsLoadingDependencies] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);
  const [isSavingCondition, setIsSavingCondition] = useState(false);
  const [deletingConditionId, setDeletingConditionId] = useState<number | null>(null);
  const [isSavingDependency, setIsSavingDependency] = useState(false);
  const [deletingDependencyId, setDeletingDependencyId] = useState<number | null>(null);

  const selectedTemplate = useMemo(
    () => templates.find((template) => template.id === selectedTemplateId) ?? null,
    [selectedTemplateId, templates]
  );

  const groupedConditions = useMemo(() => {
    const groups = new Map<number, AdminTaskTemplateCondition[]>();
    for (const condition of conditions) {
      const current = groups.get(condition.conditionGroup) ?? [];
      current.push(condition);
      groups.set(condition.conditionGroup, current);
    }

    return Array.from(groups.entries())
      .sort((left, right) => left[0] - right[0])
      .map(([group, items]) => ({
        group,
        items: items.slice().sort((left, right) => left.id - right.id),
      }));
  }, [conditions]);

  const loadConditions = useCallback(async (templateId: number) => {
    setIsLoadingConditions(true);

    try {
      const loadedConditions = await getAdminTaskTemplateConditions(templateId);
      setConditions(loadedConditions);

      const maxGroup = loadedConditions.reduce((current, item) => Math.max(current, item.conditionGroup), 0);
      setConditionDraft((current) => ({
        ...current,
        conditionGroup: current.conditionGroup || String(Math.max(maxGroup, 1)),
      }));
    } catch (err) {
      setConditions([]);
      throw err;
    } finally {
      setIsLoadingConditions(false);
    }
  }, []);

  const loadDependencies = useCallback(async (templateId: number) => {
    setIsLoadingDependencies(true);

    try {
      const loadedDependencies = await getAdminTaskTemplateDependencies(templateId);
      setDependencies(loadedDependencies);
    } catch (err) {
      setDependencies([]);
      throw err;
    } finally {
      setIsLoadingDependencies(false);
    }
  }, []);

  const loadDependencyGraph = useCallback(async (processTypeId: number) => {
    setIsLoadingDependencyGraph(true);

    try {
      const loadedGraph = await getAdminDependencyGraph(processTypeId);
      setDependencyGraph(loadedGraph);
      return loadedGraph;
    } catch (err) {
      setDependencyGraph({ nodes: [], edges: [] });
      throw err;
    } finally {
      setIsLoadingDependencyGraph(false);
    }
  }, []);

  const loadTemplates = useCallback(async (processTypeId: number, templateIdToSelect?: number | null) => {
    setIsLoadingTemplates(true);

    try {
      const [loadedTemplates, loadedAnswerDefinitions, loadedGraph] = await Promise.all([
        getAdminTaskTemplates(processTypeId),
        getAdminAnswerDefinitions(processTypeId),
        loadDependencyGraph(processTypeId),
      ]);
      setTemplates(loadedTemplates);
      setAnswerDefinitions(loadedAnswerDefinitions);
      setDependencyGraph(loadedGraph);

      if (typeof templateIdToSelect === "number") {
        const matchingTemplate = loadedTemplates.find((template) => template.id === templateIdToSelect) ?? null;
        setSelectedTemplateId(matchingTemplate?.id ?? null);
        setIsCreatingNew(false);
        setDraft(matchingTemplate ? toDraft(matchingTemplate) : EMPTY_DRAFT);
        if (matchingTemplate) {
          await Promise.all([
            loadConditions(matchingTemplate.id),
            loadDependencies(matchingTemplate.id),
          ]);
        } else {
          setConditions([]);
          setDependencies([]);
        }
        return loadedTemplates;
      }

      setSelectedTemplateId(null);
      setIsCreatingNew(false);
      setDraft(EMPTY_DRAFT);
      setConditions([]);
      setConditionDraft(EMPTY_CONDITION_DRAFT);
      setDependencies([]);
      setDependencyDraft(EMPTY_DEPENDENCY_DRAFT);
      return loadedTemplates;
    } catch (err) {
      setTemplates([]);
      setDependencyGraph({ nodes: [], edges: [] });
      setAnswerDefinitions([]);
      setSelectedTemplateId(null);
      setIsCreatingNew(false);
      setDraft(EMPTY_DRAFT);
      setConditions([]);
      setConditionDraft(EMPTY_CONDITION_DRAFT);
      setDependencies([]);
      setDependencyDraft(EMPTY_DEPENDENCY_DRAFT);
      throw err;
    } finally {
      setIsLoadingTemplates(false);
    }
  }, [loadConditions, loadDependencies, loadDependencyGraph]);

  useEffect(() => {
    setIsLoadingProcessTypes(true);
    getAdminProcessTypes()
      .then((loadedProcessTypes) => {
        setProcessTypes(loadedProcessTypes);
        setSelectedProcessTypeId((current) => current ?? loadedProcessTypes[0]?.id ?? null);
      })
      .catch(() => {
        setProcessTypes([]);
        setSelectedProcessTypeId(null);
      })
      .finally(() => setIsLoadingProcessTypes(false));
  }, []);

  useEffect(() => {
    if (!selectedProcessTypeId) {
      setTemplates([]);
      setDependencyGraph({ nodes: [], edges: [] });
      setAnswerDefinitions([]);
      setConditions([]);
      setDependencies([]);
      setSelectedTemplateId(null);
      setIsCreatingNew(false);
      setDraft(EMPTY_DRAFT);
      setConditionDraft(EMPTY_CONDITION_DRAFT);
      setDependencyDraft(EMPTY_DEPENDENCY_DRAFT);
      return;
    }

    onError(null);
    void loadTemplates(selectedProcessTypeId).catch((err) => {
      const message = err instanceof Error ? err.message : "Task-Templates konnten nicht geladen werden.";
      onError(message);
    });
  }, [loadTemplates, onError, selectedProcessTypeId]);

  const selectProcessType = useCallback((nextValue: string) => {
    const parsed = Number(nextValue);
    setSelectedProcessTypeId(Number.isFinite(parsed) && parsed > 0 ? parsed : null);
  }, []);

  const selectTemplate = useCallback((template: AdminTaskTemplate) => {
    setSelectedTemplateId(template.id);
    setIsCreatingNew(false);
    setDraft(toDraft(template));
    onNotice(null);
    onError(null);
    void Promise.all([
      loadConditions(template.id),
      loadDependencies(template.id),
    ]).catch((err) => {
      const message = err instanceof Error ? err.message : "Template-Details konnten nicht geladen werden.";
      onError(message);
    });
  }, [loadConditions, loadDependencies, onError, onNotice]);

  const startCreatingTemplate = useCallback(() => {
    setSelectedTemplateId(null);
    setIsCreatingNew(true);
    setDraft(EMPTY_DRAFT);
    setConditions([]);
    setConditionDraft(EMPTY_CONDITION_DRAFT);
    setDependencies([]);
    setDependencyDraft(EMPTY_DEPENDENCY_DRAFT);
    onNotice(null);
    onError(null);
  }, [onError, onNotice]);

  const updateDraft = useCallback(<K extends keyof TemplateDraft>(key: K, value: TemplateDraft[K]) => {
    setDraft((current) => ({ ...current, [key]: value }));
  }, []);

  const updateConditionDraft = useCallback(<K extends keyof ConditionDraft>(key: K, value: ConditionDraft[K]) => {
    setConditionDraft((current) => ({ ...current, [key]: value }));
  }, []);

  const updateDependencyDraft = useCallback(<K extends keyof DependencyDraft>(key: K, value: DependencyDraft[K]) => {
    setDependencyDraft((current) => ({ ...current, [key]: value }));
  }, []);

  const buildPayload = useCallback(() => {
    if (!selectedProcessTypeId) {
      throw new Error("Bitte zuerst einen Prozesstyp auswählen.");
    }

    if (!draft.templateKey.trim()) {
      throw new Error("Template Key ist erforderlich.");
    }

    if (!draft.title.trim()) {
      throw new Error("Titel ist erforderlich.");
    }

    const sortOrder = Number(draft.sortOrder);
    if (Number.isNaN(sortOrder)) {
      throw new Error("Sortierung muss eine Zahl sein.");
    }

    const dueInDays = draft.dueInDays.trim();
    const parsedDueInDays = dueInDays ? Number(dueInDays) : null;
    if (dueInDays && (parsedDueInDays === null || Number.isNaN(parsedDueInDays) || parsedDueInDays < 0)) {
      throw new Error("Fällig in Tagen muss leer oder eine nicht negative Zahl sein.");
    }

    return {
      processTypeId: selectedProcessTypeId,
      templateKey: draft.templateKey.trim(),
      title: draft.title.trim(),
      category: draft.category.trim() || "general",
      description: draft.description,
      iconKey: toNullableText(draft.iconKey),
      owningDepartmentId: toNullableNumber(draft.owningDepartmentId),
      defaultResponsibilityId: toNullableNumber(draft.defaultResponsibilityId),
      processAreaLabel: toNullableText(draft.processAreaLabel),
      isDepartmentPhaseTask: draft.isDepartmentPhaseTask,
      isRequired: draft.isRequired,
      dueInDays: parsedDueInDays,
      sortOrder,
      isActive: draft.isActive,
    };
  }, [draft, selectedProcessTypeId]);

  const createTemplate = useCallback(async () => {
    let payload;
    try {
      payload = buildPayload();
    } catch (err) {
      onNotice(null);
      onError(err instanceof Error ? err.message : "Task-Template konnte nicht vorbereitet werden.");
      return;
    }

    setIsSaving(true);
    onNotice(null);
    onError(null);

    try {
      const createdTemplate = await createAdminTaskTemplate(payload);
      setTemplates((current) =>
        current.concat(createdTemplate).sort((left, right) =>
          left.sortOrder === right.sortOrder
            ? left.title.localeCompare(right.title, "de")
            : left.sortOrder - right.sortOrder
        )
      );
      setDependencyGraph((current) => ({
        ...current,
        nodes: current.nodes
          .concat({
            id: createdTemplate.id,
            title: createdTemplate.title,
            category: createdTemplate.category,
          })
          .sort((left, right) => left.title.localeCompare(right.title, "de")),
      }));
      setSelectedTemplateId(createdTemplate.id);
      setIsCreatingNew(false);
      setDraft(toDraft(createdTemplate));
      setConditions([]);
      setConditionDraft(EMPTY_CONDITION_DRAFT);
      setDependencies([]);
      setDependencyDraft(EMPTY_DEPENDENCY_DRAFT);
      onNotice(`Task-Template ${createdTemplate.title} wurde angelegt.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Task-Template konnte nicht angelegt werden.";
      onError(message);
    } finally {
      setIsSaving(false);
    }
  }, [buildPayload, onError, onNotice]);

  const saveTemplate = useCallback(async () => {
    if (!selectedTemplateId) {
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

    setIsSaving(true);
    onNotice(null);
    onError(null);

    try {
      const updatedTemplate = await updateAdminTaskTemplate(selectedTemplateId, payload);
      setTemplates((current) =>
        current
          .map((item) => (item.id === updatedTemplate.id ? updatedTemplate : item))
          .sort((left, right) =>
            left.sortOrder === right.sortOrder
              ? left.title.localeCompare(right.title, "de")
              : left.sortOrder - right.sortOrder
          )
      );
      setDependencyGraph((current) => ({
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
      setDraft(toDraft(updatedTemplate));
      onNotice(`Task-Template ${updatedTemplate.title} wurde gespeichert.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Task-Template konnte nicht gespeichert werden.";
      onError(message);
    } finally {
      setIsSaving(false);
    }
  }, [buildPayload, onError, onNotice, selectedTemplateId]);

  const removeTemplate = useCallback(async () => {
    if (!selectedTemplate) {
      return;
    }

    if (typeof window !== "undefined" && !window.confirm(`Task-Template "${selectedTemplate.title}" wirklich löschen?`)) {
      return;
    }

    setIsDeleting(true);
    onNotice(null);
    onError(null);

    try {
      await deleteAdminTaskTemplate(selectedTemplate.id);
      setTemplates((current) => current.filter((item) => item.id !== selectedTemplate.id));
      setDependencyGraph((current) => ({
        nodes: current.nodes.filter((node) => node.id !== selectedTemplate.id),
        edges: current.edges.filter(
          (edge) => edge.sourceTemplateId !== selectedTemplate.id && edge.targetTemplateId !== selectedTemplate.id
        ),
      }));
      setConditions([]);
      setDependencies([]);
      setSelectedTemplateId(null);
      setIsCreatingNew(false);
      setDraft(EMPTY_DRAFT);
      setConditionDraft(EMPTY_CONDITION_DRAFT);
      setDependencyDraft(EMPTY_DEPENDENCY_DRAFT);
      onNotice(`Task-Template ${selectedTemplate.title} wurde gelöscht.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Task-Template konnte nicht gelöscht werden.";
      onError(message);
    } finally {
      setIsDeleting(false);
    }
  }, [onError, onNotice, selectedTemplate]);

  const addCondition = useCallback(async () => {
    if (!selectedTemplate) {
      onNotice(null);
      onError("Bitte zuerst ein Task-Template auswählen.");
      return;
    }

    const parsedGroup = Number(conditionDraft.conditionGroup);
    if (!Number.isFinite(parsedGroup) || parsedGroup <= 0) {
      onNotice(null);
      onError("Condition Group muss eine positive Zahl sein.");
      return;
    }

    const numberValue = conditionDraft.expectedValueNumber.trim();
    const parsedNumberValue = numberValue ? Number(numberValue) : null;
    if (numberValue && Number.isNaN(parsedNumberValue)) {
      onNotice(null);
      onError("Expected Number muss leer oder eine Zahl sein.");
      return;
    }

    setIsSavingCondition(true);
    onNotice(null);
    onError(null);

    try {
      const createdCondition = await createAdminTaskTemplateCondition(selectedTemplate.id, {
        conditionGroup: parsedGroup,
        answerKey: conditionDraft.answerKey.trim(),
        operator: conditionDraft.operator,
        expectedValueText: toNullableText(conditionDraft.expectedValueText),
        expectedValueBoolean:
          conditionDraft.expectedValueBoolean === ""
            ? null
            : conditionDraft.expectedValueBoolean === "true",
        expectedValueNumber: parsedNumberValue,
      });

      setConditions((current) =>
        current
          .concat(createdCondition)
          .sort((left, right) =>
            left.conditionGroup === right.conditionGroup
              ? left.id - right.id
              : left.conditionGroup - right.conditionGroup
          )
      );
      setTemplates((current) =>
        current.map((item) =>
          item.id === selectedTemplate.id
            ? { ...item, conditionCount: item.conditionCount + 1 }
            : item
        )
      );
      setConditionDraft((current) => ({
        ...EMPTY_CONDITION_DRAFT,
        conditionGroup: current.conditionGroup,
      }));
      onNotice("Bedingung wurde angelegt.");
    } catch (err) {
      const message = err instanceof Error ? err.message : "Bedingung konnte nicht angelegt werden.";
      onError(message);
    } finally {
      setIsSavingCondition(false);
    }
  }, [conditionDraft, onError, onNotice, selectedTemplate]);

  const removeCondition = useCallback(async (conditionId: number) => {
    if (!selectedTemplate) {
      return;
    }

    setDeletingConditionId(conditionId);
    onNotice(null);
    onError(null);

    try {
      await deleteAdminTaskTemplateCondition(selectedTemplate.id, conditionId);
      setConditions((current) => current.filter((item) => item.id !== conditionId));
      setTemplates((current) =>
        current.map((item) =>
          item.id === selectedTemplate.id
            ? { ...item, conditionCount: Math.max(0, item.conditionCount - 1) }
            : item
        )
      );
      onNotice("Bedingung wurde gelöscht.");
    } catch (err) {
      const message = err instanceof Error ? err.message : "Bedingung konnte nicht gelöscht werden.";
      onError(message);
    } finally {
      setDeletingConditionId(null);
    }
  }, [onError, onNotice, selectedTemplate]);

  const addConditionGroup = useCallback(() => {
    const nextGroup = groupedConditions.length === 0
      ? 1
      : Math.max(...groupedConditions.map((group) => group.group)) + 1;
    setConditionDraft((current) => ({ ...current, conditionGroup: String(nextGroup) }));
  }, [groupedConditions]);

  const availableDependencyTemplates = useMemo(() => {
    if (!selectedTemplate) {
      return [];
    }

    const existingDependencyIds = new Set(dependencies.map((dependency) => dependency.dependsOnTaskTemplateId));
    return templates.filter((template) => template.id !== selectedTemplate.id && !existingDependencyIds.has(template.id));
  }, [dependencies, selectedTemplate, templates]);

  const addDependency = useCallback(async () => {
    if (!selectedTemplate) {
      onNotice(null);
      onError("Bitte zuerst ein Task-Template auswählen.");
      return;
    }

    const dependsOnTaskTemplateId = Number(dependencyDraft.dependsOnTaskTemplateId);
    if (!Number.isFinite(dependsOnTaskTemplateId) || dependsOnTaskTemplateId <= 0) {
      onNotice(null);
      onError("Bitte ein gültiges abhängiges Template auswählen.");
      return;
    }

    setIsSavingDependency(true);
    onNotice(null);
    onError(null);

    try {
      const createdDependency = await createAdminTaskTemplateDependency(selectedTemplate.id, {
        dependsOnTaskTemplateId,
        requiredStatus: dependencyDraft.requiredStatus,
      });

      setDependencies((current) =>
        current
          .concat(createdDependency)
          .sort((left, right) => left.dependsOnTemplateTitle.localeCompare(right.dependsOnTemplateTitle, "de"))
      );
      setTemplates((current) =>
        current.map((item) =>
          item.id === selectedTemplate.id
            ? { ...item, dependencyCount: item.dependencyCount + 1 }
            : item
        )
      );
      setDependencyGraph((current) => ({
        ...current,
        edges: current.edges.concat({
          id: createdDependency.id,
          sourceTemplateId: createdDependency.dependsOnTaskTemplateId,
          targetTemplateId: createdDependency.taskTemplateId,
          requiredStatus: createdDependency.requiredStatus,
        }),
      }));
      setDependencyDraft((current) => ({
        ...current,
        dependsOnTaskTemplateId: "",
      }));
      onNotice("Abhängigkeit wurde angelegt.");
    } catch (err) {
      const message = err instanceof Error ? err.message : "Abhängigkeit konnte nicht angelegt werden.";
      onError(message);
    } finally {
      setIsSavingDependency(false);
    }
  }, [dependencyDraft, onError, onNotice, selectedTemplate]);

  const createDependencyFromGraph = useCallback(async (
    sourceTemplateId: number,
    targetTemplateId: number,
    requiredStatus: DependencyStatus
  ) => {
    if (sourceTemplateId === targetTemplateId) {
      onNotice(null);
      onError("Ein Template kann nicht von sich selbst abhängen.");
      return;
    }

    setIsSavingDependency(true);
    onNotice(null);
    onError(null);

    try {
      const createdDependency = await createAdminTaskTemplateDependency(targetTemplateId, {
        dependsOnTaskTemplateId: sourceTemplateId,
        requiredStatus,
      });

      setTemplates((current) =>
        current.map((item) =>
          item.id === targetTemplateId
            ? { ...item, dependencyCount: item.dependencyCount + 1 }
            : item
        )
      );
      setDependencyGraph((current) => ({
        ...current,
        edges: current.edges.concat({
          id: createdDependency.id,
          sourceTemplateId: createdDependency.dependsOnTaskTemplateId,
          targetTemplateId: createdDependency.taskTemplateId,
          requiredStatus: createdDependency.requiredStatus,
        }),
      }));

      if (selectedTemplate?.id === targetTemplateId) {
        setDependencies((current) =>
          current
            .concat(createdDependency)
            .sort((left, right) => left.dependsOnTemplateTitle.localeCompare(right.dependsOnTemplateTitle, "de"))
        );
      }

      onNotice("Abhängigkeit wurde angelegt.");
    } catch (err) {
      const message = err instanceof Error ? err.message : "Abhängigkeit konnte nicht angelegt werden.";
      onError(message);
      throw err;
    } finally {
      setIsSavingDependency(false);
    }
  }, [onError, onNotice, selectedTemplate]);

  const removeDependency = useCallback(async (dependencyId: number) => {
    if (!selectedTemplate) {
      return;
    }

    setDeletingDependencyId(dependencyId);
    onNotice(null);
    onError(null);

    try {
      await deleteAdminTaskTemplateDependency(selectedTemplate.id, dependencyId);
      setDependencies((current) => current.filter((item) => item.id !== dependencyId));
      setTemplates((current) =>
        current.map((item) =>
          item.id === selectedTemplate.id
            ? { ...item, dependencyCount: Math.max(0, item.dependencyCount - 1) }
            : item
        )
      );
      setDependencyGraph((current) => ({
        ...current,
        edges: current.edges.filter((edge) => edge.id !== dependencyId),
      }));
      onNotice("Abhängigkeit wurde gelöscht.");
    } catch (err) {
      const message = err instanceof Error ? err.message : "Abhängigkeit konnte nicht gelöscht werden.";
      onError(message);
    } finally {
      setDeletingDependencyId(null);
    }
  }, [onError, onNotice, selectedTemplate]);

  const removeDependencyFromGraph = useCallback(async (dependencyId: number) => {
    const edge = dependencyGraph.edges.find((entry) => entry.id === dependencyId);
    if (!edge) {
      return;
    }

    setDeletingDependencyId(dependencyId);
    onNotice(null);
    onError(null);

    try {
      await deleteAdminTaskTemplateDependency(edge.targetTemplateId, dependencyId);
      setDependencyGraph((current) => ({
        ...current,
        edges: current.edges.filter((currentEdge) => currentEdge.id !== dependencyId),
      }));
      setTemplates((current) =>
        current.map((item) =>
          item.id === edge.targetTemplateId
            ? { ...item, dependencyCount: Math.max(0, item.dependencyCount - 1) }
            : item
        )
      );

      if (selectedTemplate?.id === edge.targetTemplateId) {
        setDependencies((current) => current.filter((item) => item.id !== dependencyId));
      }

      onNotice("Abhängigkeit wurde gelöscht.");
    } catch (err) {
      const message = err instanceof Error ? err.message : "Abhängigkeit konnte nicht gelöscht werden.";
      onError(message);
      throw err;
    } finally {
      setDeletingDependencyId(null);
    }
  }, [dependencyGraph.edges, onError, onNotice, selectedTemplate]);

  return {
    processTypes,
    selectedProcessTypeId,
    templates,
    dependencyGraph,
    selectedTemplate,
    answerDefinitions,
    draft,
    conditions,
    dependencies,
    groupedConditions,
    conditionDraft,
    dependencyDraft,
    isCreatingNew,
    isLoadingProcessTypes,
    isLoadingTemplates,
    isLoadingDependencyGraph,
    isLoadingConditions,
    isLoadingDependencies,
    isSaving,
    isDeleting,
    isSavingCondition,
    deletingConditionId,
    isSavingDependency,
    deletingDependencyId,
    availableDependencyTemplates,
    selectProcessType,
    selectTemplate,
    startCreatingTemplate,
    updateDraft,
    updateConditionDraft,
    updateDependencyDraft,
    createTemplate,
    saveTemplate,
    removeTemplate,
    addCondition,
    removeCondition,
    addConditionGroup,
    addDependency,
    createDependencyFromGraph,
    removeDependency,
    removeDependencyFromGraph,
  };
}
