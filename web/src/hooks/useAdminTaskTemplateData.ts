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
  getAdminTaskTemplateConditions,
  getAdminTaskTemplateDependencies,
  getAdminTaskTemplates,
  getAdminWorkflowDefinitions,
} from "../services/adminConfigApi";
import type {
  AdminAnswerDefinition,
  AdminDependencyGraph,
  AdminWorkflowDefinitionSummary,
  AdminTaskSpec,
  AdminTaskSpecCondition,
  AdminTaskSpecDependency,
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
  workflowDefinitions: AdminWorkflowDefinitionSummary[];
  selectedWorkflowDefinitionId: number | null;
  templates: AdminTaskSpec[];
  dependencyGraph: AdminDependencyGraph;
  selectedTemplate: AdminTaskSpec | null;
  selectedTemplateId: number | null;
  answerDefinitions: AdminAnswerDefinition[];
  draft: TemplateDraft;
  conditions: AdminTaskSpecCondition[];
  dependencies: AdminTaskSpecDependency[];
  groupedConditions: ReturnType<typeof groupConditions>;
  conditionDraft: ConditionDraft;
  dependencyDraft: DependencyDraft;
  isCreatingNew: boolean;
  setTemplates: Dispatch<SetStateAction<AdminTaskSpec[]>>;
  setDependencyGraph: Dispatch<SetStateAction<AdminDependencyGraph>>;
  setSelectedTemplateId: Dispatch<SetStateAction<number | null>>;
  setDraft: Dispatch<SetStateAction<TemplateDraft>>;
  setConditions: Dispatch<SetStateAction<AdminTaskSpecCondition[]>>;
  setDependencies: Dispatch<SetStateAction<AdminTaskSpecDependency[]>>;
  setConditionDraft: Dispatch<SetStateAction<ConditionDraft>>;
  setDependencyDraft: Dispatch<SetStateAction<DependencyDraft>>;
  setIsCreatingNew: Dispatch<SetStateAction<boolean>>;
  selectWorkflowDefinition: (nextValue: string) => void;
  selectTemplate: (template: AdminTaskSpec) => void;
  startCreatingTemplate: () => void;
  updateDraft: <K extends keyof TemplateDraft>(key: K, value: TemplateDraft[K]) => void;
  updateConditionDraft: <K extends keyof ConditionDraft>(key: K, value: ConditionDraft[K]) => void;
  updateDependencyDraft: <K extends keyof DependencyDraft>(key: K, value: DependencyDraft[K]) => void;
};

export function useAdminTaskTemplateData({
  onError,
  updateOperationState,
}: UseAdminTaskTemplateDataOptions): AdminTaskTemplateDataController {
  const [workflowDefinitions, setWorkflowDefinitions] = useState<AdminWorkflowDefinitionSummary[]>([]);
  const [selectedWorkflowDefinitionId, setSelectedWorkflowDefinitionId] = useState<number | null>(null);
  const [templates, setTemplates] = useState<AdminTaskSpec[]>([]);
  const [dependencyGraph, setDependencyGraph] = useState<AdminDependencyGraph>({ nodes: [], edges: [] });
  const [selectedTemplateId, setSelectedTemplateId] = useState<number | null>(null);
  const [draft, setDraft] = useState<TemplateDraft>(EMPTY_DRAFT);
  const [conditions, setConditions] = useState<AdminTaskSpecCondition[]>([]);
  const [dependencies, setDependencies] = useState<AdminTaskSpecDependency[]>([]);
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

  const loadDependencyGraph = useCallback(async (workflowDefinitionId: number) => {
    updateOperationState({ isLoadingDependencyGraph: true });

    try {
      const loadedGraph = await getAdminDependencyGraph(workflowDefinitionId);
      setDependencyGraph(loadedGraph);
      return loadedGraph;
    } catch (err) {
      setDependencyGraph({ nodes: [], edges: [] });
      throw err;
    } finally {
      updateOperationState({ isLoadingDependencyGraph: false });
    }
  }, [updateOperationState]);

  const loadTemplates = useCallback(async (workflowDefinitionId: number, templateIdToSelect?: number | null) => {
    updateOperationState({ isLoadingTemplates: true });

    try {
      const [loadedTemplates, loadedAnswerDefinitions, loadedGraph] = await Promise.all([
        getAdminTaskTemplates(workflowDefinitionId),
        getAdminAnswerDefinitions(workflowDefinitionId),
        loadDependencyGraph(workflowDefinitionId),
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
    getAdminWorkflowDefinitions()
      .then((loadedDefinitions) => {
        setWorkflowDefinitions(loadedDefinitions);
        setSelectedWorkflowDefinitionId((current) => current ?? loadedDefinitions[0]?.id ?? null);
      })
      .catch(() => {
        setWorkflowDefinitions([]);
        setSelectedWorkflowDefinitionId(null);
      })
      .finally(() => updateOperationState({ isLoadingProcessTypes: false }));
  }, [updateOperationState]);

  useEffect(() => {
    if (!selectedWorkflowDefinitionId) {
      setTemplates([]);
      setDependencyGraph({ nodes: [], edges: [] });
      setAnswerDefinitions([]);
      resetTemplateEditor();
      return;
    }

    onError(null);
    void loadTemplates(selectedWorkflowDefinitionId).catch((err) => {
      const message = err instanceof Error ? err.message : "Task-Templates konnten nicht geladen werden.";
      onError(message);
    });
  }, [loadTemplates, onError, resetTemplateEditor, selectedWorkflowDefinitionId]);

  const selectWorkflowDefinition = useCallback((nextValue: string) => {
    const parsed = Number(nextValue);
    setSelectedWorkflowDefinitionId(Number.isFinite(parsed) && parsed > 0 ? parsed : null);
  }, []);

  const selectTemplate = useCallback((template: AdminTaskSpec) => {
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
    workflowDefinitions,
    selectedWorkflowDefinitionId,
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
    selectWorkflowDefinition,
    selectTemplate,
    startCreatingTemplate,
    updateDraft,
    updateConditionDraft,
    updateDependencyDraft,
  };
}
