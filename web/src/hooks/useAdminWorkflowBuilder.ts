import { useCallback, useEffect, useMemo, useState } from "react";
import {
  createAdminWorkflowDefinition,
  createAdminWorkflowDefinitionVersion,
  getAdminWorkflowActionDefinitions,
  getAdminWorkflowDefinitionVersion,
  getAdminWorkflowDefinitions,
  publishAdminWorkflowDefinitionVersion,
  replaceAdminWorkflowDefinitionVersion,
  updateAdminWorkflowDefinition,
} from "../services/adminConfigApi";
import type {
  AdminWorkflowActionDefinition,
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
  createEmptyVersionCreateDraft,
  createEmptyVersionDraft,
  findVersionSummary,
  isDefinitionMetadataChanged,
  toVersionDraft,
  withFallbackNodePositions,
  type WorkflowBuilderNodeDraft,
  type WorkflowBuilderVersionDraft,
  validateWorkflowBuilderDraft,
} from "./adminWorkflowBuilderModel";

type UseAdminWorkflowBuilderOptions = {
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
  canManageAdvanced: boolean;
};

export function useAdminWorkflowBuilder({ onNotice, onError, canManageAdvanced }: UseAdminWorkflowBuilderOptions) {
  const [definitions, setDefinitions] = useState<AdminWorkflowDefinitionSummary[]>([]);
  const [actionDefinitions, setActionDefinitions] = useState<AdminWorkflowActionDefinition[]>([]);
  const [selectedDefinitionId, setSelectedDefinitionId] = useState<number | null>(null);
  const [selectedVersionId, setSelectedVersionId] = useState<number | null>(null);
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [selectedEdgeId, setSelectedEdgeId] = useState<string | null>(null);
  const [versionDetail, setVersionDetail] = useState<AdminWorkflowDefinitionVersionDetail | null>(null);
  const [definitionDraft, setDefinitionDraft] = useState(createEmptyDefinitionDraft);
  const [newDefinitionDraft, setNewDefinitionDraft] = useState(createEmptyDefinitionDraft);
  const [newVersionDraft, setNewVersionDraft] = useState(createEmptyVersionCreateDraft);
  const [versionDraft, setVersionDraft] = useState<WorkflowBuilderVersionDraft>(createEmptyVersionDraft);
  const [localValidationIssues, setLocalValidationIssues] = useState<string[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isLoadingVersion, setIsLoadingVersion] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [isCreatingDefinition, setIsCreatingDefinition] = useState(false);
  const [isCreatingVersion, setIsCreatingVersion] = useState(false);
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
    let actionCatalogError: string | null = null;

    if (canManageAdvanced) {
      try {
        loadedActionDefinitions = await getAdminWorkflowActionDefinitions();
      } catch (err) {
        actionCatalogError = err instanceof Error
          ? err.message
          : "Der Action-Katalog konnte nicht geladen werden.";
      }
    }

    setDefinitions(loadedDefinitions);
    setActionDefinitions(loadedActionDefinitions);
    if (actionCatalogError) {
      onError(`Builder geladen, aber der Action-Katalog ist derzeit nicht verfuegbar. ${actionCatalogError}`);
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
        ?? availableVersions[0]?.id
        ?? null
      : availableVersions[0]?.id ?? null;

    setSelectedVersionId(nextVersionId);
  }, [canManageAdvanced, onError]);

  const loadVersionDetail = useCallback(async (versionId: number | null) => {
    if (!versionId) {
      setVersionDetail(null);
      setVersionDraft(createEmptyVersionDraft());
      setSelectedNodeId(null);
      setSelectedEdgeId(null);
      setLocalValidationIssues([]);
      setIsDirty(false);
      return;
    }

    setIsLoadingVersion(true);
    try {
      const detail = await getAdminWorkflowDefinitionVersion(versionId);
      const nextDraft = toVersionDraft(detail);
      setVersionDetail(detail);
      setVersionDraft(nextDraft);
      setSelectedNodeId(null);
      setSelectedEdgeId(null);
      setLocalValidationIssues([]);
      setIsDirty(false);
    } finally {
      setIsLoadingVersion(false);
    }
  }, []);

  useEffect(() => {
    setIsLoading(true);
    onError(null);
    void loadDefinitions({ keepSelection: false })
      .catch((err) => {
        onError(err instanceof Error ? err.message : "Workflow-Definitionen konnten nicht geladen werden.");
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
    void loadVersionDetail(selectedVersionId).catch((err) => {
      onError(err instanceof Error ? err.message : "Workflow-Version konnte nicht geladen werden.");
      setVersionDetail(null);
      setVersionDraft(createEmptyVersionDraft());
      setSelectedNodeId(null);
      setSelectedEdgeId(null);
    });
  }, [loadVersionDetail, onError, selectedVersionId]);

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

    const definition = definitions.find((item) => item.id === definitionId) ?? null;
    setSelectedDefinitionId(definitionId);
    setSelectedVersionId(definition?.versions[0]?.id ?? null);
  }, [confirmSelectionChange, definitions]);

  const selectVersion = useCallback((versionId: number) => {
    if (!confirmSelectionChange()) {
      return;
    }

    setSelectedVersionId(versionId);
  }, [confirmSelectionChange]);

  const selectNode = useCallback((nodeId: string | null) => {
    setSelectedNodeId(nodeId);
    setSelectedEdgeId(null);
  }, []);

  const selectEdge = useCallback((edgeId: string | null) => {
    setSelectedEdgeId(edgeId);
    if (edgeId) {
      setSelectedNodeId(null);
    }
  }, []);

  const updateDefinitionDraft = useCallback((key: "name" | "description", value: string) => {
    setDefinitionDraft((current) => ({ ...current, [key]: value }));
  }, []);

  const updateNewDefinitionDraft = useCallback((key: "key" | "name" | "description", value: string) => {
    setNewDefinitionDraft((current) => ({ ...current, [key]: value }));
  }, []);

  const updateNewVersionDraft = useCallback((key: "name" | "description", value: string) => {
    setNewVersionDraft((current) => ({ ...current, [key]: value }));
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
        onError("Automation-Nodes und Automation-Typwechsel erfordern Admin Builder Rechte.");
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

  const addNode = useCallback(() => {
    const maxPositionX = versionDraft.nodes.reduce((currentMax, node) => {
      return Math.max(currentMax, node.positionX ?? 0);
    }, -280);
    const nextNode = {
      ...createEmptyNodeDraft("task", versionDraft.nodes.length + 1),
      positionX: maxPositionX + 280,
      positionY: 0,
    };
    setVersionDraft((current) => ({ ...current, nodes: [...current.nodes, nextNode] }));
    setSelectedNodeId(nextNode.id);
    setSelectedEdgeId(null);
    setIsDirty(true);
  }, [versionDraft.nodes]);

  const removeNode = useCallback((nodeId: string) => {
    const nodeToRemove = versionDraft.nodes.find((node) => node.id === nodeId) ?? null;
    if (!canManageAdvanced && nodeToRemove?.nodeType === "automation") {
      onError("Automation-Nodes koennen nur im Admin Builder entfernt werden.");
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
      onError("Bitte zuerst einen Node im Canvas auswaehlen.");
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
      onError("Der Action Layer ist nur im Admin Builder editierbar.");
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
      onError("Action-Aenderungen erfordern Admin Builder Rechte.");
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
      onError("Action-Aenderungen erfordern Admin Builder Rechte.");
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
        errorMessage = "Edges brauchen valide Quelle und Ziel mit Node Key.";
        return current;
      }

      if (sourceNodeKey.toLowerCase() === targetNodeKey.toLowerCase()) {
        errorMessage = "Self-Loops sind im Builder nicht erlaubt.";
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
      onError("Neue Definitionen koennen nur im Admin Builder angelegt werden.");
      return;
    }

    setIsCreatingDefinition(true);
    onError(null);
    onNotice(null);

    try {
      const created = await createAdminWorkflowDefinition({
        key: newDefinitionDraft.key.trim(),
        name: newDefinitionDraft.name.trim(),
        description: newDefinitionDraft.description.trim() || null,
      });
      await loadDefinitions({ keepSelection: false });
      setSelectedDefinitionId(created.id);
      setSelectedVersionId(created.versions[0]?.id ?? null);
      setNewDefinitionDraft(createEmptyDefinitionDraft());
      onNotice(`Workflow-Definition '${created.name}' wurde angelegt.`);
    } catch (err) {
      onError(err instanceof Error ? err.message : "Workflow-Definition konnte nicht angelegt werden.");
    } finally {
      setIsCreatingDefinition(false);
    }
  }, [canManageAdvanced, loadDefinitions, newDefinitionDraft.description, newDefinitionDraft.key, newDefinitionDraft.name, onError, onNotice]);

  const createVersion = useCallback(async () => {
    if (!canManageAdvanced) {
      onError("Neue Versionen koennen nur im Admin Builder angelegt werden.");
      return;
    }

    if (!selectedDefinitionId) {
      onError("Bitte zuerst eine Workflow-Definition auswaehlen.");
      return;
    }

    setIsCreatingVersion(true);
    onError(null);
    onNotice(null);

    try {
      const created = await createAdminWorkflowDefinitionVersion(selectedDefinitionId, {
        name: newVersionDraft.name.trim() || null,
        description: newVersionDraft.description.trim() || null,
      });
      await loadDefinitions({
        keepSelection: true,
        selectedDefinitionId,
      });
      setSelectedVersionId(created.id);
      setNewVersionDraft(createEmptyVersionCreateDraft());
      onNotice(`Version ${created.versionNumber} wurde als Draft angelegt.`);
    } catch (err) {
      onError(err instanceof Error ? err.message : "Workflow-Version konnte nicht angelegt werden.");
    } finally {
      setIsCreatingVersion(false);
    }
  }, [canManageAdvanced, loadDefinitions, newVersionDraft.description, newVersionDraft.name, onError, onNotice, selectedDefinitionId]);

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
      onError("Bitte zuerst eine Definition und Version auswaehlen.");
      return;
    }

    const normalizedDraft = {
      ...versionDraft,
      nodes: withFallbackNodePositions(versionDraft.nodes, versionDraft.edges),
    };

    const localValidationMessages = collectValidationMessages(normalizedDraft);
    setLocalValidationIssues(localValidationMessages);
    if (localValidationMessages.length > 0) {
      onError("Der Draft enthaelt lokale Validierungsfehler.");
      return;
    }

    setIsSaving(true);
    onError(null);
    onNotice(null);

    try {
      if (hasDefinitionMetadataChanges) {
        if (!canManageAdvanced) {
          onError("Definition-Metadaten koennen nur im Admin Builder gespeichert werden.");
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
      onNotice(`Draft-Version ${saved.versionNumber} wurde gespeichert.`);
    } catch (err) {
      onError(err instanceof Error ? err.message : "Draft konnte nicht gespeichert werden.");
    } finally {
      setIsSaving(false);
    }
  }, [canManageAdvanced, collectValidationMessages, definitionDraft.description, definitionDraft.name, hasDefinitionMetadataChanges, loadDefinitions, onError, onNotice, selectedDefinition, selectedVersionId, versionDraft]);

  const publishVersion = useCallback(async () => {
    if (!canManageAdvanced) {
      onError("Publish ist nur im Admin Builder erlaubt.");
      return;
    }

    if (!selectedVersionId || !selectedVersionSummary?.canPublish) {
      onError("Diese Version ist noch nicht publish-faehig.");
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
        selectedVersionId,
      });
      const nextDraft = toVersionDraft(published);
      setVersionDetail(published);
      setVersionDraft(nextDraft);
      setSelectedNodeId(null);
      setSelectedEdgeId(null);
      setIsDirty(false);
      onNotice(`Version ${published.versionNumber} wurde veroeffentlicht.`);
    } catch (err) {
      onError(err instanceof Error ? err.message : "Version konnte nicht veroeffentlicht werden.");
    } finally {
      setIsPublishing(false);
    }
  }, [canManageAdvanced, loadDefinitions, onError, onNotice, selectedDefinition?.id, selectedVersionId, selectedVersionSummary?.canPublish]);

  const validateDraft = useCallback(() => {
    if (!selectedVersionSummary) {
      onError("Bitte zuerst eine Version auswaehlen.");
      return;
    }

    const normalizedDraft = {
      ...versionDraft,
      nodes: withFallbackNodePositions(versionDraft.nodes, versionDraft.edges),
    };

    const localValidationMessages = collectValidationMessages(normalizedDraft);
    setLocalValidationIssues(localValidationMessages);

    if (localValidationMessages.length > 0) {
      onError("Der Draft enthaelt lokale Validierungsfehler.");
      onNotice(null);
      return;
    }

    onError(null);
    onNotice("Lokale Builder-Validierung erfolgreich.");
  }, [collectValidationMessages, onError, onNotice, selectedVersionSummary, versionDraft]);

  return {
    definitions,
    actionDefinitions,
    selectedDefinition,
    selectedVersionSummary,
    selectedNode,
    selectedEdge,
    versionDetail,
    definitionDraft,
    newDefinitionDraft,
    newVersionDraft,
    versionDraft,
    localValidationIssues,
    isLoading,
    isLoadingVersion,
    isSaving,
    isCreatingDefinition,
    isCreatingVersion,
    isPublishing,
    isDirty,
    hasUnsavedChanges,
    selectDefinition,
    selectVersion,
    selectNode,
    selectEdge,
    updateDefinitionDraft,
    updateNewDefinitionDraft,
    updateNewVersionDraft,
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
    createVersion,
    saveVersion,
    validateDraft,
    publishVersion,
  };
}
