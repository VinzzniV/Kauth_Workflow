import { useCallback, useEffect, useMemo, useState } from "react";
import {
  createAdminWorkflowDefinition,
  deleteAdminWorkflowDefinition,
  getAdminAnswerDefinitions,
  getAdminWorkflowActionDefinitions,
  getAdminProcessTypes,
  getAdminTaskTemplateConditions,
  getAdminTaskTemplateDependencies,
  getAdminTaskTemplates,
  getAdminWorkflowDefinitionVersion,
  getOrCreateAdminWorkflowDefinitionWorkingDraft,
  getAdminWorkflowDefinitions,
  publishAdminWorkflowDefinitionVersion,
  replaceAdminWorkflowDefinitionVersion,
  updateAdminWorkflowDefinition,
} from "../services/adminConfigApi";
import { getAdminResponsibilityOwners } from "../services/adminApi";
import type {
  AdminAnswerDefinition,
  AdminWorkflowActionDefinition,
  AdminProcessType,
  AdminResponsibilityOwner,
  AdminTaskTemplate,
  AdminTaskTemplateCondition,
  AdminTaskTemplateDependency,
  AdminWorkflowDefinitionSummary,
  AdminWorkflowDefinitionVersionDetail,
} from "../types/auth";
import {
  autoLayoutVersionDraft,
  buildVersionReplacePayload,
  createEmptyActionDraft,
  createEmptyDefinitionDraft,
  createEmptyEdgeDraft,
  createEmptyNodeDraft,
  createEmptyVersionDraft,
  findVersionSummary,
  isDefinitionMetadataChanged,
  toVersionDraft,
  type WorkflowBuilderNodeDraft,
  type WorkflowBuilderVersionDraft,
  validateWorkflowBuilderDraft,
} from "./adminWorkflowBuilderModel";

export type BuilderInspectorFocusMode =
  | "none"
  | "process_type"
  | "answer_definition"
  | "task_template"
  | "task_template_conditions"
  | "task_template_dependencies";

export type BuilderInspectorFocusSection = "details" | "conditions" | "dependencies" | null;

export type BuilderInspectorFocusTarget = {
  mode: BuilderInspectorFocusMode;
  processTypeId: number | null;
  answerDefinitionId: number | null;
  templateId: number | null;
  templateSection: BuilderInspectorFocusSection;
};

type UseAdminWorkflowBuilderOptions = {
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
  canManageAdvanced: boolean;
};

function createEmptyInspectorFocus(): BuilderInspectorFocusTarget {
  return {
    mode: "none",
    processTypeId: null,
    answerDefinitionId: null,
    templateId: null,
    templateSection: null,
  };
}

export function useAdminWorkflowBuilder({ onNotice, onError, canManageAdvanced }: UseAdminWorkflowBuilderOptions) {
  const [definitions, setDefinitions] = useState<AdminWorkflowDefinitionSummary[]>([]);
  const [actionDefinitions, setActionDefinitions] = useState<AdminWorkflowActionDefinition[]>([]);
  const [processTypes, setProcessTypes] = useState<AdminProcessType[]>([]);
  const [responsibilityOwners, setResponsibilityOwners] = useState<AdminResponsibilityOwner[]>([]);
  const [taskTemplates, setTaskTemplates] = useState<AdminTaskTemplate[]>([]);
  const [answerDefinitions, setAnswerDefinitions] = useState<AdminAnswerDefinition[]>([]);
  const [taskTemplateConditions, setTaskTemplateConditions] = useState<AdminTaskTemplateCondition[]>([]);
  const [taskTemplateDependencies, setTaskTemplateDependencies] = useState<AdminTaskTemplateDependency[]>([]);
  const [selectedDefinitionId, setSelectedDefinitionId] = useState<number | null>(null);
  const [selectedVersionId, setSelectedVersionId] = useState<number | null>(null);
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [selectedEdgeId, setSelectedEdgeId] = useState<string | null>(null);
  const [versionDetail, setVersionDetail] = useState<AdminWorkflowDefinitionVersionDetail | null>(null);
  const [definitionDraft, setDefinitionDraft] = useState(createEmptyDefinitionDraft);
  const [newDefinitionDraft, setNewDefinitionDraft] = useState(createEmptyDefinitionDraft);
  const [versionDraft, setVersionDraft] = useState<WorkflowBuilderVersionDraft>(createEmptyVersionDraft);
  const [localValidationIssues, setLocalValidationIssues] = useState<string[]>([]);
  const [inspectorFocus, setInspectorFocus] = useState<BuilderInspectorFocusTarget>(createEmptyInspectorFocus);
  const [referenceReloadTick, setReferenceReloadTick] = useState(0);
  const [isLoading, setIsLoading] = useState(true);
  const [isLoadingVersion, setIsLoadingVersion] = useState(false);
  const [isDeletingDefinition, setIsDeletingDefinition] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [isCreatingDefinition, setIsCreatingDefinition] = useState(false);
  const [isPublishing, setIsPublishing] = useState(false);
  const [isDirty, setIsDirty] = useState(false);

  const selectedDefinition = useMemo(
    () => definitions.find((definition) => definition.id === selectedDefinitionId) ?? null,
    [definitions, selectedDefinitionId]
  );
  const selectedVersionSummary = useMemo(
    () => findVersionSummary(definitions, selectedVersionId),
    [definitions, selectedVersionId]
  );
  const selectedNode = useMemo(
    () => versionDraft.nodes.find((node) => node.id === selectedNodeId) ?? null,
    [selectedNodeId, versionDraft.nodes]
  );
  const selectedEdge = useMemo(
    () => versionDraft.edges.find((edge) => edge.id === selectedEdgeId) ?? null,
    [selectedEdgeId, versionDraft.edges]
  );
  const hasDefinitionMetadataChanges = useMemo(
    () => isDefinitionMetadataChanged(selectedDefinition, definitionDraft.name, definitionDraft.description),
    [definitionDraft.description, definitionDraft.name, selectedDefinition]
  );
  const hasUnsavedChanges = isDirty || hasDefinitionMetadataChanges;
  const activeActionDefinitionsByKey = useMemo(() => {
    return new Map(
      actionDefinitions.map((definition) => [definition.actionKey.trim().toLowerCase(), definition] as const)
    );
  }, [actionDefinitions]);

  const loadDefinitions = useCallback(async (options?: {
    selectedDefinitionId?: number | null;
    selectedVersionId?: number | null;
    keepSelection?: boolean;
  }) => {
    const loadedDefinitions = await getAdminWorkflowDefinitions();
    let loadedActionDefinitions: AdminWorkflowActionDefinition[] = [];
    let loadedProcessTypes: AdminProcessType[] = [];
    let loadedResponsibilityOwners: AdminResponsibilityOwner[] = [];
    let actionCatalogError: string | null = null;

    if (canManageAdvanced) {
      try {
        loadedActionDefinitions = await getAdminWorkflowActionDefinitions();
      } catch (err) {
        actionCatalogError = err instanceof Error
          ? err.message
          : "Der Aktionskatalog konnte nicht geladen werden.";
      }
    }

    try {
      loadedProcessTypes = await getAdminProcessTypes();
    } catch {
      loadedProcessTypes = [];
    }

    try {
      loadedResponsibilityOwners = await getAdminResponsibilityOwners();
    } catch {
      loadedResponsibilityOwners = [];
    }

    setDefinitions(loadedDefinitions);
    setActionDefinitions(loadedActionDefinitions);
    setProcessTypes(loadedProcessTypes);
    setResponsibilityOwners(loadedResponsibilityOwners);
    if (actionCatalogError) {
      onError(`Der Ablauf-Editor wurde geladen, aber der Aktionskatalog ist derzeit nicht verfuegbar. ${actionCatalogError}`);
    }

    const keepSelection = options?.keepSelection ?? true;
    const preferredDefinitionId = options?.selectedDefinitionId ?? null;
    const preferredVersionId = options?.selectedVersionId ?? null;

    const nextDefinitionId = keepSelection
      ? loadedDefinitions.find((definition) => definition.id === preferredDefinitionId)?.id
        ?? loadedDefinitions[0]?.id
        ?? null
      : loadedDefinitions[0]?.id ?? null;

    setSelectedDefinitionId(nextDefinitionId);

    const availableVersions =
      loadedDefinitions.find((definition) => definition.id === nextDefinitionId)?.versions ?? [];
    const nextVersionId = keepSelection
      ? availableVersions.find((version) => version.id === preferredVersionId)?.id
        ?? availableVersions.find((version) => version.status.trim().toLowerCase() === "draft")?.id
        ?? availableVersions[0]?.id
        ?? null
      : availableVersions.find((version) => version.status.trim().toLowerCase() === "draft")?.id
        ?? availableVersions[0]?.id
        ?? null;

    setSelectedVersionId(nextVersionId);
  }, [canManageAdvanced, onError]);

  const closeInspectorFocus = useCallback(() => {
    setInspectorFocus(createEmptyInspectorFocus());
  }, []);

  const openInspectorFocus = useCallback((focus: BuilderInspectorFocusTarget) => {
    setInspectorFocus(focus);
  }, []);

  const resetLoadedVersion = useCallback(() => {
    setVersionDetail(null);
    setVersionDraft(createEmptyVersionDraft());
    setTaskTemplates([]);
    setAnswerDefinitions([]);
    setTaskTemplateConditions([]);
    setTaskTemplateDependencies([]);
    setSelectedNodeId(null);
    setSelectedEdgeId(null);
    setLocalValidationIssues([]);
    setIsDirty(false);
    setSelectedVersionId(null);
    setInspectorFocus(createEmptyInspectorFocus());
  }, []);

  const applyLoadedVersionDetail = useCallback((detail: AdminWorkflowDefinitionVersionDetail) => {
    const nextDraft = toVersionDraft(detail);
    setVersionDetail(detail);
    setVersionDraft(nextDraft);
    setTaskTemplates([]);
    setAnswerDefinitions([]);
    setTaskTemplateConditions([]);
    setTaskTemplateDependencies([]);
    setSelectedNodeId(null);
    setSelectedEdgeId(null);
    setLocalValidationIssues([]);
    setIsDirty(false);
    setSelectedVersionId(detail.id);
    setInspectorFocus(createEmptyInspectorFocus());
  }, []);

  const loadVersionDetail = useCallback(async (versionId: number | null) => {
    if (!versionId) {
      resetLoadedVersion();
      return;
    }

    setIsLoadingVersion(true);
    try {
      const detail = await getAdminWorkflowDefinitionVersion(versionId);
      applyLoadedVersionDetail(detail);
    } finally {
      setIsLoadingVersion(false);
    }
  }, [applyLoadedVersionDetail, resetLoadedVersion]);

  const resolveWorkingDraft = useCallback(async (
    definitionId: number | null,
    options?: { refreshDefinitions?: boolean }
  ) => {
    if (!definitionId) {
      resetLoadedVersion();
      return null;
    }

    setIsLoadingVersion(true);
    try {
      const detail = await getOrCreateAdminWorkflowDefinitionWorkingDraft(definitionId);
      applyLoadedVersionDetail(detail);

      if (options?.refreshDefinitions ?? true) {
        await loadDefinitions({
          keepSelection: true,
          selectedDefinitionId: definitionId,
          selectedVersionId: detail.id,
        });
      }

      return detail;
    } finally {
      setIsLoadingVersion(false);
    }
  }, [applyLoadedVersionDetail, loadDefinitions, resetLoadedVersion]);

  useEffect(() => {
    setIsLoading(true);
    onError(null);
    void loadDefinitions({ keepSelection: false })
      .catch((err) => {
        onError(err instanceof Error ? err.message : "Die Ablaufvorlagen konnten nicht geladen werden.");
        setDefinitions([]);
        setActionDefinitions([]);
      })
      .finally(() => setIsLoading(false));
  }, [loadDefinitions, onError]);

  useEffect(() => {
    if (!selectedDefinition) {
      setDefinitionDraft(createEmptyDefinitionDraft());
      return;
    }

    setDefinitionDraft({
      key: selectedDefinition.key,
      name: selectedDefinition.name,
      description: selectedDefinition.description ?? "",
    });
  }, [selectedDefinition]);

  useEffect(() => {
    onError(null);
    void resolveWorkingDraft(selectedDefinitionId).catch((err) => {
      onError(err instanceof Error ? err.message : "Der ausgewaehlte Ablauf konnte nicht geladen werden.");
      resetLoadedVersion();
    });
  }, [onError, resetLoadedVersion, resolveWorkingDraft, selectedDefinitionId]);

  useEffect(() => {
    const processTypeIdsToLoad = new Set<number>();
    const referencedProcessKeys = new Set<string>();

    if (versionDraft.primaryLegacyProcessTypeKey.trim()) {
      referencedProcessKeys.add(versionDraft.primaryLegacyProcessTypeKey.trim().toLowerCase());
    }

    for (const node of versionDraft.nodes) {
      try {
        const parsed = node.configText.trim() ? JSON.parse(node.configText) as Record<string, unknown> : null;
        const legacyProcessTypeKey = typeof parsed?.legacyProcessTypeKey === "string"
          ? parsed.legacyProcessTypeKey.trim().toLowerCase()
          : "";
        if (legacyProcessTypeKey) {
          referencedProcessKeys.add(legacyProcessTypeKey);
        }
      } catch {
        continue;
      }
    }

    for (const processType of processTypes) {
      if (referencedProcessKeys.has(processType.key.trim().toLowerCase())) {
        processTypeIdsToLoad.add(processType.id);
      }
    }

    if (processTypeIdsToLoad.size === 0) {
      setTaskTemplates([]);
      setAnswerDefinitions([]);
      setTaskTemplateConditions([]);
      setTaskTemplateDependencies([]);
      return;
    }

    let cancelled = false;
    void Promise.all(
      [...processTypeIdsToLoad].map(async (processTypeId) =>
        Promise.all([
          getAdminTaskTemplates(processTypeId),
          getAdminAnswerDefinitions(processTypeId),
        ])
      )
    )
      .then(async (loadedGroups) => {
        if (cancelled) {
          return;
        }

        const templatesByKey = new Map<string, AdminTaskTemplate>();
        const answerDefinitionsByCompositeKey = new Map<string, AdminAnswerDefinition>();
        for (const [loadedTemplates, loadedAnswerDefinitions] of loadedGroups) {
          for (const template of loadedTemplates) {
            const normalizedKey = template.templateKey.trim().toLowerCase();
            if (normalizedKey && !templatesByKey.has(normalizedKey)) {
              templatesByKey.set(normalizedKey, template);
            }
          }

          for (const definition of loadedAnswerDefinitions) {
            const compositeKey = `${definition.processTypeId}:${definition.answerKey.trim().toLowerCase()}`;
            if (definition.answerKey.trim() && !answerDefinitionsByCompositeKey.has(compositeKey)) {
              answerDefinitionsByCompositeKey.set(compositeKey, definition);
            }
          }
        }

        const dedupedTemplates = [...templatesByKey.values()];
        const templatesWithConditions = dedupedTemplates.filter((template) => template.conditionCount > 0);
        const templatesWithDependencies = dedupedTemplates.filter((template) => template.dependencyCount > 0);

        const [loadedConditions, loadedDependencies] = await Promise.all([
          Promise.all(templatesWithConditions.map((template) => getAdminTaskTemplateConditions(template.id))),
          Promise.all(templatesWithDependencies.map((template) => getAdminTaskTemplateDependencies(template.id))),
        ]);

        if (cancelled) {
          return;
        }

        setTaskTemplates(dedupedTemplates);
        setAnswerDefinitions([...answerDefinitionsByCompositeKey.values()]);
        setTaskTemplateConditions(loadedConditions.flat());
        setTaskTemplateDependencies(loadedDependencies.flat());
      })
      .catch(() => {
        if (!cancelled) {
          setTaskTemplates([]);
          setAnswerDefinitions([]);
          setTaskTemplateConditions([]);
          setTaskTemplateDependencies([]);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [processTypes, referenceReloadTick, versionDraft.nodes, versionDraft.primaryLegacyProcessTypeKey]);

  const refreshReferenceData = useCallback(async () => {
    onError(null);

    try {
      const [loadedProcessTypes, loadedResponsibilityOwners] = await Promise.all([
        getAdminProcessTypes().catch(() => [] as AdminProcessType[]),
        getAdminResponsibilityOwners().catch(() => [] as AdminResponsibilityOwner[]),
      ]);
      setProcessTypes(loadedProcessTypes);
      setResponsibilityOwners(loadedResponsibilityOwners);
      setReferenceReloadTick((current) => current + 1);
    } catch (err) {
      onError(err instanceof Error ? err.message : "Die Builder-Referenzdaten konnten nicht aktualisiert werden.");
    }
  }, [onError]);

  const confirmSelectionChange = useCallback(() => {
    if (!hasUnsavedChanges) {
      return true;
    }

    return window.confirm("Ungespeicherte Aenderungen verwerfen?");
  }, [hasUnsavedChanges]);

  const selectDefinition = useCallback((definitionId: number) => {
    if (!confirmSelectionChange()) {
      return;
    }

    resetLoadedVersion();
    setSelectedDefinitionId(definitionId);
  }, [confirmSelectionChange, resetLoadedVersion]);

  const selectVersion = useCallback((versionId: number) => {
    if (!confirmSelectionChange()) {
      return;
    }

    setSelectedVersionId(versionId);
  }, [confirmSelectionChange]);

  const selectNode = useCallback((nodeId: string | null) => {
    setSelectedNodeId(nodeId);
    setSelectedEdgeId(null);
    setInspectorFocus(createEmptyInspectorFocus());
  }, []);

  const selectEdge = useCallback((edgeId: string | null) => {
    setSelectedEdgeId(edgeId);
    if (edgeId) {
      setSelectedNodeId(null);
    }
    setInspectorFocus(createEmptyInspectorFocus());
  }, []);

  const updateDefinitionDraft = useCallback((key: "name" | "description", value: string) => {
    setDefinitionDraft((current) => ({ ...current, [key]: value }));
  }, []);

  const updateNewDefinitionDraft = useCallback((key: "key" | "name" | "description", value: string) => {
    setNewDefinitionDraft((current) => ({ ...current, [key]: value }));
  }, []);

  const updateVersionDraftField = useCallback((key: "name" | "description" | "primaryLegacyProcessTypeKey", value: string) => {
    setVersionDraft((current) => ({ ...current, [key]: value }));
    setIsDirty(true);
  }, []);

  const updateNode = useCallback((nodeId: string, patch: Partial<WorkflowBuilderNodeDraft>) => {
    const currentNode = versionDraft.nodes.find((node) => node.id === nodeId) ?? null;
    if (!canManageAdvanced && currentNode) {
      const nextNodeType = typeof patch.nodeType === "string" ? patch.nodeType : currentNode.nodeType;
      const touchesAutomationType =
        currentNode.nodeType === "automation"
        || nextNodeType === "automation";

      if (touchesAutomationType && nextNodeType !== currentNode.nodeType) {
        onError("Automatisierungen und Typwechsel auf automatische Schritte erfordern den Admin-Modus.");
        return;
      }
    }

    setVersionDraft((current) => {
      const existingNode = current.nodes.find((node) => node.id === nodeId);
      const nextNodes = current.nodes.map((node) => node.id === nodeId ? { ...node, ...patch } : node);

      const requestedNodeKey = typeof patch.nodeKey === "string" ? patch.nodeKey.trim() : null;
      const currentNodeKey = existingNode?.nodeKey.trim() ?? null;
      const nextEdges = requestedNodeKey && currentNodeKey && requestedNodeKey !== currentNodeKey
        ? current.edges.map((edge) => ({
            ...edge,
            sourceNodeKey: edge.sourceNodeKey.trim() === currentNodeKey ? requestedNodeKey : edge.sourceNodeKey,
            targetNodeKey: edge.targetNodeKey.trim() === currentNodeKey ? requestedNodeKey : edge.targetNodeKey,
          }))
        : current.edges;

      return {
        ...current,
        nodes: nextNodes,
        edges: nextEdges,
      };
    });
    setIsDirty(true);
    onError(null);
  }, [canManageAdvanced, onError, versionDraft.nodes]);

  const updateNodePosition = useCallback((nodeId: string, position: { x: number; y: number }) => {
    setVersionDraft((current) => ({
      ...current,
      nodes: current.nodes.map((node) =>
        node.id === nodeId
          ? {
              ...node,
              positionX: Math.round(position.x),
              positionY: Math.round(position.y),
            }
          : node),
    }));
    setIsDirty(true);
  }, []);

  const addNode = useCallback((nodeType: WorkflowBuilderNodeDraft["nodeType"] = "task") => {
    const nextNode = {
      ...createEmptyNodeDraft(nodeType, versionDraft.nodes.length + 1),
      positionX: null,
      positionY: null,
    };
    setVersionDraft((current) => ({ ...current, nodes: [...current.nodes, nextNode] }));
    setSelectedNodeId(nextNode.id);
    setSelectedEdgeId(null);
    setIsDirty(true);
  }, [versionDraft.nodes]);

  const removeNode = useCallback((nodeId: string) => {
    const nodeToRemove = versionDraft.nodes.find((node) => node.id === nodeId) ?? null;
    if (!canManageAdvanced && nodeToRemove?.nodeType === "automation") {
      onError("Automatisierungen koennen nur im Admin-Modus entfernt werden.");
      return;
    }

    let shouldClearSelectedEdge = false;
    setVersionDraft((current) => {
      const removedNode = current.nodes.find((node) => node.id === nodeId);
      const removedNodeKey = removedNode?.nodeKey.trim();
      const nextNodes = current.nodes.filter((node) => node.id !== nodeId);
      const removedEdgeIds = new Set(
        current.edges
          .filter((edge) => edge.sourceNodeKey === removedNodeKey || edge.targetNodeKey === removedNodeKey)
          .map((edge) => edge.id)
      );

      if (selectedNodeId === nodeId) {
        setSelectedNodeId(null);
      }

      if (selectedEdgeId && removedEdgeIds.has(selectedEdgeId)) {
        shouldClearSelectedEdge = true;
      }

      return {
        ...current,
        nodes: nextNodes,
        edges: current.edges.filter((edge) => edge.sourceNodeKey !== removedNodeKey && edge.targetNodeKey !== removedNodeKey),
      };
    });
    if (shouldClearSelectedEdge) {
      setSelectedEdgeId(null);
    }
    setIsDirty(true);
    onError(null);
  }, [canManageAdvanced, onError, selectedEdgeId, selectedNodeId, versionDraft.nodes]);

  const removeSelectedNode = useCallback(() => {
    if (!selectedNode) {
      onError("Bitte zuerst einen Schritt im Canvas auswaehlen.");
      return;
    }

    removeNode(selectedNode.id);
    onError(null);
  }, [onError, removeNode, selectedNode]);

  const autoLayoutNodes = useCallback(() => {
    setVersionDraft((current) => autoLayoutVersionDraft(current));
    setIsDirty(true);
  }, []);

  const addActionFromDefinition = useCallback((nodeId: string, actionKey: string) => {
    if (!canManageAdvanced) {
      onError("Automatische Aktionen sind nur im Admin-Modus editierbar.");
      return;
    }

    const normalizedActionKey = actionKey.trim();
    const definition = activeActionDefinitionsByKey.get(normalizedActionKey.toLowerCase());
    if (!definition) {
      onError("Die ausgewaehlte Action ist im Katalog nicht verfuegbar.");
      return;
    }

    if (!definition.isActive) {
      onError(`Die Action '${definition.displayName}' ist inaktiv und kann nicht hinzugefuegt werden.`);
      return;
    }

    setVersionDraft((current) => ({
      ...current,
      nodes: current.nodes.map((node) => {
        if (node.id !== nodeId || node.nodeType !== "automation") {
          return node;
        }

        const usedOrders = node.actions
          .map((action) => Number(action.executionOrder))
          .filter((order) => Number.isInteger(order) && order > 0);
        const nextExecutionOrder = usedOrders.length > 0 ? Math.max(...usedOrders) + 1 : 1;

        return {
          ...node,
          actions: [
            ...node.actions,
            {
              ...createEmptyActionDraft(),
              actionKey: definition.actionKey,
              executionOrder: String(nextExecutionOrder),
            },
          ],
        };
      }),
    }));
    setIsDirty(true);
    onError(null);
  }, [activeActionDefinitionsByKey, canManageAdvanced, onError]);

  const updateAction = useCallback((nodeId: string, actionId: string, patch: Partial<WorkflowBuilderVersionDraft["nodes"][number]["actions"][number]>) => {
    if (!canManageAdvanced) {
      onError("Aenderungen an automatischen Aktionen erfordern den Admin-Modus.");
      return;
    }

    setVersionDraft((current) => ({
      ...current,
      nodes: current.nodes.map((node) =>
        node.id === nodeId
          ? {
              ...node,
              actions: node.actions.map((action) => action.id === actionId ? { ...action, ...patch } : action),
            }
          : node),
    }));
    setIsDirty(true);
    onError(null);
  }, [canManageAdvanced, onError]);

  const removeAction = useCallback((nodeId: string, actionId: string) => {
    if (!canManageAdvanced) {
      onError("Aenderungen an automatischen Aktionen erfordern den Admin-Modus.");
      return;
    }

    setVersionDraft((current) => ({
      ...current,
      nodes: current.nodes.map((node) =>
        node.id === nodeId
          ? { ...node, actions: node.actions.filter((action) => action.id !== actionId) }
          : node),
    }));
    setIsDirty(true);
    onError(null);
  }, [canManageAdvanced, onError]);

  const addEdge = useCallback(() => {
    setVersionDraft((current) => ({ ...current, edges: [...current.edges, createEmptyEdgeDraft()] }));
    setIsDirty(true);
  }, []);

  const updateEdge = useCallback((edgeId: string, patch: Partial<WorkflowBuilderVersionDraft["edges"][number]>) => {
    setVersionDraft((current) => ({
      ...current,
      edges: current.edges.map((edge) => edge.id === edgeId ? { ...edge, ...patch } : edge),
    }));
    setIsDirty(true);
  }, []);

  const removeEdge = useCallback((edgeId: string) => {
    setVersionDraft((current) => ({
      ...current,
      edges: current.edges.filter((edge) => edge.id !== edgeId),
    }));
    if (selectedEdgeId === edgeId) {
      setSelectedEdgeId(null);
    }
    setIsDirty(true);
  }, [selectedEdgeId]);

  const removeSelectedEdge = useCallback(() => {
    if (!selectedEdgeId) {
      return;
    }

    removeEdge(selectedEdgeId);
  }, [removeEdge, selectedEdgeId]);

  const connectNodes = useCallback((sourceNodeId: string | null, targetNodeId: string | null) => {
    if (!sourceNodeId || !targetNodeId) {
      return;
    }

    let createdEdgeId: string | null = null;
    let errorMessage: string | null = null;
    setVersionDraft((current) => {
      const sourceNode = current.nodes.find((node) => node.id === sourceNodeId);
      const targetNode = current.nodes.find((node) => node.id === targetNodeId);
      const sourceNodeKey = sourceNode?.nodeKey.trim() ?? "";
      const targetNodeKey = targetNode?.nodeKey.trim() ?? "";

      if (!sourceNodeKey || !targetNodeKey) {
        errorMessage = "Verbindungen brauchen gueltige Quelle und gueltiges Ziel mit Schritt-Key.";
        return current;
      }

      if (sourceNodeKey.toLowerCase() === targetNodeKey.toLowerCase()) {
        errorMessage = "Verbindungen duerfen nicht auf denselben Schritt zurueckzeigen.";
        return current;
      }

      const usedPriorities = new Set(
        current.edges
          .filter((edge) => edge.sourceNodeKey.trim().toLowerCase() === sourceNodeKey.toLowerCase())
          .map((edge) => Number(edge.priority))
          .filter((priority) => Number.isInteger(priority) && priority > 0)
      );

      let nextPriority = 1;
      while (usedPriorities.has(nextPriority)) {
        nextPriority += 1;
      }

      const nextEdge = {
        ...createEmptyEdgeDraft(),
        sourceNodeKey,
        targetNodeKey,
        priority: String(nextPriority),
      };
      createdEdgeId = nextEdge.id;

      return {
        ...current,
        edges: [...current.edges, nextEdge],
      };
    });

    if (errorMessage) {
      onError(errorMessage);
      return;
    }

    if (createdEdgeId) {
      setSelectedNodeId(null);
      setSelectedEdgeId(createdEdgeId);
      setIsDirty(true);
      onError(null);
    }
  }, [onError]);

  const createDefinition = useCallback(async () => {
    if (!canManageAdvanced) {
      onError("Neue Ablaeufe koennen nur im Admin-Modus angelegt werden.");
      return;
    }

    setIsCreatingDefinition(true);
    onError(null);
    onNotice(null);

    try {
      const created = await createAdminWorkflowDefinition({
        key: null,
        name: newDefinitionDraft.name.trim(),
        description: newDefinitionDraft.description.trim() || null,
      });
      await loadDefinitions({
        keepSelection: true,
        selectedDefinitionId: created.id,
        selectedVersionId: created.versions[0]?.id ?? null,
      });
      setSelectedDefinitionId(created.id);
      if (created.versions[0]?.id) {
        await loadVersionDetail(created.versions[0].id);
      } else {
        await resolveWorkingDraft(created.id, { refreshDefinitions: true });
      }
      setNewDefinitionDraft(createEmptyDefinitionDraft());
      onNotice(`Ablauf '${created.name}' wurde angelegt.`);
    } catch (err) {
      onError(err instanceof Error ? err.message : "Der Ablauf konnte nicht angelegt werden.");
    } finally {
      setIsCreatingDefinition(false);
    }
  }, [canManageAdvanced, loadDefinitions, loadVersionDetail, newDefinitionDraft.description, newDefinitionDraft.name, onError, onNotice, resolveWorkingDraft]);

  const deleteDefinition = useCallback(async () => {
    if (!canManageAdvanced) {
      onError("Ablaufe koennen nur im Admin-Modus geloescht werden.");
      return;
    }

    if (!selectedDefinition) {
      onError("Bitte zuerst einen Ablauf auswaehlen.");
      return;
    }

    if (!window.confirm(`Ablauf '${selectedDefinition.name}' wirklich loeschen?`)) {
      return;
    }

    setIsDeletingDefinition(true);
    onError(null);
    onNotice(null);

    try {
      await deleteAdminWorkflowDefinition(selectedDefinition.id);
      resetLoadedVersion();
      await loadDefinitions({ keepSelection: false });
      onNotice(`Ablauf '${selectedDefinition.name}' wurde geloescht.`);
    } catch (err) {
      onError(err instanceof Error ? err.message : "Der Ablauf konnte nicht geloescht werden.");
    } finally {
      setIsDeletingDefinition(false);
    }
  }, [canManageAdvanced, loadDefinitions, onError, onNotice, resetLoadedVersion, selectedDefinition]);

  const collectValidationMessages = useCallback((draft: WorkflowBuilderVersionDraft) => {
    const messages = validateWorkflowBuilderDraft(draft).map((issue) => issue.message);

    if (actionDefinitions.length === 0) {
      return messages;
    }

    for (const node of draft.nodes) {
      if (node.nodeType !== "automation") {
        continue;
      }

      const nodeKey = node.nodeKey.trim() || "?";
      for (const action of node.actions) {
        const actionKey = action.actionKey.trim();
        if (!actionKey) {
          continue;
        }

        const definition = activeActionDefinitionsByKey.get(actionKey.toLowerCase());
        if (!definition) {
          messages.push(`Node '${nodeKey}' referenziert unbekannte Action '${actionKey}'.`);
          continue;
        }

        if (!definition.isActive) {
          messages.push(`Node '${nodeKey}' referenziert inaktive Action '${actionKey}'.`);
        }
      }
    }

    return Array.from(new Set(messages));
  }, [actionDefinitions.length, activeActionDefinitionsByKey]);

  const saveVersion = useCallback(async () => {
    if (!selectedDefinition || !selectedVersionId) {
      onError("Bitte zuerst einen Ablauf und einen Stand auswaehlen.");
      return;
    }

    const normalizedDraft = autoLayoutVersionDraft(versionDraft);

    const localValidationMessages = collectValidationMessages(normalizedDraft);
    setLocalValidationIssues(localValidationMessages);
    if (localValidationMessages.length > 0) {
      onError("Der aktuelle Stand enthaelt lokale Fehler.");
      return;
    }

    setIsSaving(true);
    onError(null);
    onNotice(null);

    try {
      if (hasDefinitionMetadataChanges) {
        if (!canManageAdvanced) {
          onError("Erweiterte Details zur Ablaufvorlage koennen nur im Admin-Modus gespeichert werden.");
          return;
        }

        await updateAdminWorkflowDefinition(selectedDefinition.id, {
          name: definitionDraft.name.trim(),
          description: definitionDraft.description.trim() || null,
        });
      }

      const saved = await replaceAdminWorkflowDefinitionVersion(selectedVersionId, buildVersionReplacePayload(normalizedDraft));
      await loadDefinitions({
        keepSelection: true,
        selectedDefinitionId: selectedDefinition.id,
        selectedVersionId,
      });
      const nextDraft = toVersionDraft(saved);
      setVersionDetail(saved);
      setVersionDraft(nextDraft);
      setSelectedNodeId(null);
      setSelectedEdgeId(null);
      setLocalValidationIssues([]);
      setIsDirty(false);
      onNotice(`Stand ${saved.versionNumber} wurde gespeichert.`);
    } catch (err) {
      onError(err instanceof Error ? err.message : "Stand konnte nicht gespeichert werden.");
    } finally {
      setIsSaving(false);
    }
  }, [canManageAdvanced, collectValidationMessages, definitionDraft.description, definitionDraft.name, hasDefinitionMetadataChanges, loadDefinitions, onError, onNotice, selectedDefinition, selectedVersionId, versionDraft]);

  const publishVersion = useCallback(async () => {
    if (!canManageAdvanced) {
      onError("Freigeben ist nur im Admin-Modus erlaubt.");
      return;
    }

    if (!selectedVersionId || !selectedVersionSummary?.canPublish) {
      onError("Dieser Stand ist noch nicht freigabefaehig.");
      return;
    }

    setIsPublishing(true);
    onError(null);
    onNotice(null);

    try {
      const published = await publishAdminWorkflowDefinitionVersion(selectedVersionId);
      await loadDefinitions({
        keepSelection: true,
        selectedDefinitionId: selectedDefinition?.id ?? null,
        selectedVersionId: published.id,
      });
      if (selectedDefinition?.id) {
        await resolveWorkingDraft(selectedDefinition.id, { refreshDefinitions: true });
      } else {
        applyLoadedVersionDetail(published);
        setIsDirty(false);
      }
      onNotice(`Ablauf '${published.definitionName}' wurde freigegeben.`);
    } catch (err) {
      onError(err instanceof Error ? err.message : "Stand konnte nicht freigegeben werden.");
    } finally {
      setIsPublishing(false);
    }
  }, [applyLoadedVersionDetail, canManageAdvanced, loadDefinitions, onError, onNotice, resolveWorkingDraft, selectedDefinition?.id, selectedVersionId, selectedVersionSummary?.canPublish]);

  const validateDraft = useCallback(() => {
    if (!selectedVersionSummary) {
      onError("Bitte zuerst einen Stand auswaehlen.");
      return;
    }

    const normalizedDraft = autoLayoutVersionDraft(versionDraft);

    const localValidationMessages = collectValidationMessages(normalizedDraft);
    setLocalValidationIssues(localValidationMessages);

    if (localValidationMessages.length > 0) {
      onError("Der aktuelle Stand enthaelt lokale Fehler.");
      onNotice(null);
      return;
    }

    onError(null);
    onNotice("Lokale Pruefung erfolgreich.");
  }, [collectValidationMessages, onError, onNotice, selectedVersionSummary, versionDraft]);

  return {
    definitions,
    actionDefinitions,
    processTypes,
    responsibilityOwners,
    taskTemplates,
    answerDefinitions,
    taskTemplateConditions,
    taskTemplateDependencies,
    selectedDefinition,
    selectedVersionSummary,
    selectedNode,
    selectedEdge,
    versionDetail,
    definitionDraft,
    newDefinitionDraft,
    versionDraft,
    localValidationIssues,
    isLoading,
    isLoadingVersion,
    isDeletingDefinition,
    isSaving,
    isCreatingDefinition,
    isPublishing,
    isDirty,
    hasUnsavedChanges,
    selectDefinition,
    selectVersion,
    selectNode,
    selectEdge,
    updateDefinitionDraft,
    updateNewDefinitionDraft,
    updateVersionDraftField,
    addNode,
    updateNode,
    updateNodePosition,
    removeNode,
    removeSelectedNode,
    autoLayoutNodes,
    addActionFromDefinition,
    updateAction,
    removeAction,
    addEdge,
    updateEdge,
    removeEdge,
    removeSelectedEdge,
    connectNodes,
    createDefinition,
    deleteDefinition,
    saveVersion,
    validateDraft,
    publishVersion,
    inspectorFocus,
    openInspectorFocus,
    closeInspectorFocus,
    refreshReferenceData,
  };
}
