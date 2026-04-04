import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  type Dispatch,
  type SetStateAction,
} from "react";
import {
  getAdminAnswerDefinitions,
  getAdminDependencyGraph,
  getAdminProcessTypes,
  getAdminTaskTemplateConditions,
  getAdminTaskTemplateDependencies,
  getAdminTaskTemplates,
} from "../services/adminConfigApi";
import type {
  AdminAnswerDefinition,
  AdminDependencyGraph,
  AdminProcessType,
  AdminTaskTemplate,
  AdminTaskTemplateCondition,
  AdminTaskTemplateDependency,
} from "../types/auth";
import {
  EMPTY_CONDITION_DRAFT,
  EMPTY_DEPENDENCY_DRAFT,
  EMPTY_DRAFT,
  type ConditionDraft,
  type DependencyDraft,
  groupConditions,
  type OperationState,
  type TemplateDraft,
  toDraft,
} from "./adminTaskTemplateManagementModel";

type UseAdminTaskTemplateDataOptions = {
  onError: (message: string | null) => void;
  updateOperationState: (patch: Partial<OperationState>) => void;
};

export type AdminTaskTemplateDataController = {
  processTypes: AdminProcessType[];
  selectedProcessTypeId: number | null;
  templates: AdminTaskTemplate[];
  dependencyGraph: AdminDependencyGraph;
  selectedTemplate: AdminTaskTemplate | null;
  selectedTemplateId: number | null;
  answerDefinitions: AdminAnswerDefinition[];
  draft: TemplateDraft;
  conditions: AdminTaskTemplateCondition[];
  dependencies: AdminTaskTemplateDependency[];
  groupedConditions: ReturnType<typeof groupConditions>;
  conditionDraft: ConditionDraft;
  dependencyDraft: DependencyDraft;
  isCreatingNew: boolean;
  setTemplates: Dispatch<SetStateAction<AdminTaskTemplate[]>>;
  setDependencyGraph: Dispatch<SetStateAction<AdminDependencyGraph>>;
  setSelectedTemplateId: Dispatch<SetStateAction<number | null>>;
  setDraft: Dispatch<SetStateAction<TemplateDraft>>;
  setConditions: Dispatch<SetStateAction<AdminTaskTemplateCondition[]>>;
  setDependencies: Dispatch<SetStateAction<AdminTaskTemplateDependency[]>>;
  setConditionDraft: Dispatch<SetStateAction<ConditionDraft>>;
  setDependencyDraft: Dispatch<SetStateAction<DependencyDraft>>;
  setIsCreatingNew: Dispatch<SetStateAction<boolean>>;
  selectProcessType: (nextValue: string) => void;
  selectTemplate: (template: AdminTaskTemplate) => void;
  startCreatingTemplate: () => void;
  updateDraft: <K extends keyof TemplateDraft>(key: K, value: TemplateDraft[K]) => void;
  updateConditionDraft: <K extends keyof ConditionDraft>(key: K, value: ConditionDraft[K]) => void;
  updateDependencyDraft: <K extends keyof DependencyDraft>(key: K, value: DependencyDraft[K]) => void;
};

export function useAdminTaskTemplateData({
  onError,
  updateOperationState,
}: UseAdminTaskTemplateDataOptions): AdminTaskTemplateDataController {
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

  const selectedTemplate = useMemo(
    () => templates.find((template) => template.id === selectedTemplateId) ?? null,
    [selectedTemplateId, templates]
  );

  const groupedConditions = useMemo(() => groupConditions(conditions), [conditions]);

  const resetTemplateEditor = useCallback(() => {
    setSelectedTemplateId(null);
    setIsCreatingNew(false);
    setDraft(EMPTY_DRAFT);
    setConditions([]);
    setConditionDraft(EMPTY_CONDITION_DRAFT);
    setDependencies([]);
    setDependencyDraft(EMPTY_DEPENDENCY_DRAFT);
  }, []);

  const loadConditions = useCallback(async (templateId: number) => {
    updateOperationState({ isLoadingConditions: true });

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
      updateOperationState({ isLoadingConditions: false });
    }
  }, [updateOperationState]);

  const loadDependencies = useCallback(async (templateId: number) => {
    updateOperationState({ isLoadingDependencies: true });

    try {
      const loadedDependencies = await getAdminTaskTemplateDependencies(templateId);
      setDependencies(loadedDependencies);
    } catch (err) {
      setDependencies([]);
      throw err;
    } finally {
      updateOperationState({ isLoadingDependencies: false });
    }
  }, [updateOperationState]);

  const loadDependencyGraph = useCallback(async (processTypeId: number) => {
    updateOperationState({ isLoadingDependencyGraph: true });

    try {
      const loadedGraph = await getAdminDependencyGraph(processTypeId);
      setDependencyGraph(loadedGraph);
      return loadedGraph;
    } catch (err) {
      setDependencyGraph({ nodes: [], edges: [] });
      throw err;
    } finally {
      updateOperationState({ isLoadingDependencyGraph: false });
    }
  }, [updateOperationState]);

  const loadTemplates = useCallback(async (processTypeId: number, templateIdToSelect?: number | null) => {
    updateOperationState({ isLoadingTemplates: true });

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
        return;
      }

      resetTemplateEditor();
    } catch (err) {
      setTemplates([]);
      setDependencyGraph({ nodes: [], edges: [] });
      setAnswerDefinitions([]);
      resetTemplateEditor();
      throw err;
    } finally {
      updateOperationState({ isLoadingTemplates: false });
    }
  }, [loadConditions, loadDependencies, loadDependencyGraph, resetTemplateEditor, updateOperationState]);

  useEffect(() => {
    updateOperationState({ isLoadingProcessTypes: true });
    getAdminProcessTypes()
      .then((loadedProcessTypes) => {
        setProcessTypes(loadedProcessTypes);
        setSelectedProcessTypeId((current) => current ?? loadedProcessTypes[0]?.id ?? null);
      })
      .catch(() => {
        setProcessTypes([]);
        setSelectedProcessTypeId(null);
      })
      .finally(() => updateOperationState({ isLoadingProcessTypes: false }));
  }, [updateOperationState]);

  useEffect(() => {
    if (!selectedProcessTypeId) {
      setTemplates([]);
      setDependencyGraph({ nodes: [], edges: [] });
      setAnswerDefinitions([]);
      resetTemplateEditor();
      return;
    }

    onError(null);
    void loadTemplates(selectedProcessTypeId).catch((err) => {
      const message = err instanceof Error ? err.message : "Task-Templates konnten nicht geladen werden.";
      onError(message);
    });
  }, [loadTemplates, onError, resetTemplateEditor, selectedProcessTypeId]);

  const selectProcessType = useCallback((nextValue: string) => {
    const parsed = Number(nextValue);
    setSelectedProcessTypeId(Number.isFinite(parsed) && parsed > 0 ? parsed : null);
  }, []);

  const selectTemplate = useCallback((template: AdminTaskTemplate) => {
    setSelectedTemplateId(template.id);
    setIsCreatingNew(false);
    setDraft(toDraft(template));
    onError(null);
    void Promise.all([
      loadConditions(template.id),
      loadDependencies(template.id),
    ]).catch((err) => {
      const message = err instanceof Error ? err.message : "Template-Details konnten nicht geladen werden.";
      onError(message);
    });
  }, [loadConditions, loadDependencies, onError]);

  const startCreatingTemplate = useCallback(() => {
    resetTemplateEditor();
    setIsCreatingNew(true);
    onError(null);
  }, [onError, resetTemplateEditor]);

  const updateDraft = useCallback(<K extends keyof TemplateDraft>(key: K, value: TemplateDraft[K]) => {
    setDraft((current) => ({ ...current, [key]: value }));
  }, []);

  const updateConditionDraft = useCallback(<K extends keyof ConditionDraft>(key: K, value: ConditionDraft[K]) => {
    setConditionDraft((current) => ({ ...current, [key]: value }));
  }, []);

  const updateDependencyDraft = useCallback(<K extends keyof DependencyDraft>(key: K, value: DependencyDraft[K]) => {
    setDependencyDraft((current) => ({ ...current, [key]: value }));
  }, []);

  return {
    processTypes,
    selectedProcessTypeId,
    templates,
    dependencyGraph,
    selectedTemplate,
    selectedTemplateId,
    answerDefinitions,
    draft,
    conditions,
    dependencies,
    groupedConditions,
    conditionDraft,
    dependencyDraft,
    isCreatingNew,
    setTemplates,
    setDependencyGraph,
    setSelectedTemplateId,
    setDraft,
    setConditions,
    setDependencies,
    setConditionDraft,
    setDependencyDraft,
    setIsCreatingNew,
    selectProcessType,
    selectTemplate,
    startCreatingTemplate,
    updateDraft,
    updateConditionDraft,
    updateDependencyDraft,
  };
}
