import { useCallback, useEffect, useMemo, useState } from "react";
import {
  createAdminWorkflowDefinition,
  deleteAdminWorkflowDefinition,
  getAdminAutomationPropertyCatalog,
  getAdminWorkflowActionDefinitions,
  type AdminAutomationPropertyCatalog,
  getAdminWorkflowDefinitionVersion,
  ensureAdminWorkflowDefinitionWorkingDraft,
  getAdminWorkflowDefinitions,
  publishAdminWorkflowDefinitionVersion,
  replaceAdminWorkflowDefinitionVersion,
  updateAdminWorkflowDefinition,
} from "../services/adminConfigApi";
import { getAdminResponsibilityOwners } from "../services/adminApi";
import type {
  AdminWorkflowActionDefinition,
  AdminResponsibilityOwner,
  AdminWorkflowDefinitionSummary,
  AdminWorkflowDefinitionVersionDetail,
} from "../types/auth";
import { useAdminWorkflowVersionReferenceData } from "./useAdminWorkflowVersionReferenceData";
import {
  buildVersionReplacePayload,
  createEmptyActionDraft,
  createEmptyDefinitionDraft,
  createEmptyEdgeDraft,
  createEmptyNodeDraft,
  createEmptyVersionDraft,
  findVersionSummary,
  isDefinitionMetadataChanged,
  toVersionDraft,
  type WorkflowBuilderLocalIssue,
  type WorkflowBuilderNodeDraft,
  type WorkflowBuilderVersionDraft,
  validateWorkflowBuilderDraft,
} from "./adminWorkflowBuilderModel";
import { useConfirmationDialog } from "../components/feedback/useConfirmationDialog";

type UseAdminWorkflowBuilderOptions = {
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
  canManageAdvanced: boolean;
};

function dedupeIssues(issues: WorkflowBuilderLocalIssue[]): WorkflowBuilderLocalIssue[] {
  const seen = new Set<string>();
  const result: WorkflowBuilderLocalIssue[] = [];
  for (const issue of issues) {
    const key = `${issue.scope}|${issue.referenceKey ?? ""}|${issue.message}`;
    if (seen.has(key)) continue;
    seen.add(key);
    result.push(issue);
  }
  return result;
}

export function useAdminWorkflowBuilder({ onNotice, onError, canManageAdvanced }: UseAdminWorkflowBuilderOptions) {
  const confirm = useConfirmationDialog();
  const [definitions, setDefinitions] = useState<AdminWorkflowDefinitionSummary[]>([]);
  const [actionDefinitions, setActionDefinitions] = useState<AdminWorkflowActionDefinition[]>([]);
  const [automationPropertyCatalog, setAutomationPropertyCatalog] = useState<AdminAutomationPropertyCatalog | null>(null);
  const [responsibilityOwners, setResponsibilityOwners] = useState<AdminResponsibilityOwner[]>([]);
  const [selectedDefinitionId, setSelectedDefinitionId] = useState<number | null>(null);
  const [selectedVersionId, setSelectedVersionId] = useState<number | null>(null);
  const [versionDetail, setVersionDetail] = useState<AdminWorkflowDefinitionVersionDetail | null>(null);
  const [definitionDraft, setDefinitionDraft] = useState(createEmptyDefinitionDraft);
  const [newDefinitionDraft, setNewDefinitionDraft] = useState(createEmptyDefinitionDraft);
  const [versionDraft, setVersionDraft] = useState<WorkflowBuilderVersionDraft>(createEmptyVersionDraft);
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

  const collectValidationIssues = useCallback((draft: WorkflowBuilderVersionDraft): WorkflowBuilderLocalIssue[] => {
    const issues: WorkflowBuilderLocalIssue[] = [...validateWorkflowBuilderDraft(draft)];

    if (actionDefinitions.length === 0) {
      return dedupeIssues(issues);
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
          issues.push({ scope: "action", message: `Node '${nodeKey}' referenziert unbekannte Action '${actionKey}'.`, referenceKey: nodeKey });
          continue;
        }

        if (!definition.isActive) {
          issues.push({ scope: "action", message: `Node '${nodeKey}' referenziert inaktive Action '${actionKey}'.`, referenceKey: nodeKey });
        }
      }
    }

    return dedupeIssues(issues);
  }, [actionDefinitions.length, activeActionDefinitionsByKey]);

  // Live-Validation: re-evaluate on every draft / action-catalog change so badges and markers
  // reflect the current state without an explicit "Lokal pruefen" click.
  const localValidationIssues = useMemo(
    () => collectValidationIssues(versionDraft),
    [collectValidationIssues, versionDraft]
  );

  // Aufgaben-/Antwort-/Conditions-/Dependencies-Referenzdaten kommen aus einem
  // dedizierten Hook (HQ2-Z3): er beobachtet versionDraft + definitions und
  // re-laedt automatisch, wenn legacyProcessTypeKey-Referenzen sich aendern.
  const {
    taskTemplates,
    answerDefinitions,
    taskTemplateConditions,
    taskTemplateDependencies,
  } = useAdminWorkflowVersionReferenceData(versionDraft, definitions);

  const loadDefinitions = useCallback(async (options?: {
    selectedDefinitionId?: number | null;
    selectedVersionId?: number | null;
    keepSelection?: boolean;
  }) => {
    const loadedDefinitions = await getAdminWorkflowDefinitions();
    let loadedActionDefinitions: AdminWorkflowActionDefinition[] = [];
    let loadedResponsibilityOwners: AdminResponsibilityOwner[] = [];
    let actionCatalogError: string | null = null;

    let loadedAutomationPropertyCatalog: AdminAutomationPropertyCatalog | null = null;

    if (canManageAdvanced) {
      try {
        loadedActionDefinitions = await getAdminWorkflowActionDefinitions();
      } catch (err) {
        actionCatalogError = err instanceof Error
          ? err.message
          : "Der Aktionskatalog konnte nicht geladen werden.";
      }

      try {
        loadedAutomationPropertyCatalog = await getAdminAutomationPropertyCatalog();
      } catch {
        // Property-Katalog ist optional fuer den Editor — er hat statische
        // Fallback-Listen. Fehler hier nicht eskalieren.
        loadedAutomationPropertyCatalog = null;
      }
    }

    try {
      loadedResponsibilityOwners = (await getAdminResponsibilityOwners({ limit: 200 })).items;
    } catch {
      loadedResponsibilityOwners = [];
    }

    setDefinitions(loadedDefinitions);
    setActionDefinitions(loadedActionDefinitions);
    setAutomationPropertyCatalog(loadedAutomationPropertyCatalog);
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

  const resetLoadedVersion = useCallback(() => {
    setVersionDetail(null);
    setVersionDraft(createEmptyVersionDraft());
    // taskTemplates / answerDefinitions / taskTemplateConditions / taskTemplateDependencies
    // werden vom useAdminWorkflowVersionReferenceData-Hook geliefert und resetten
    // sich automatisch, sobald der versionDraft keine referenzierten Process-Keys
    // mehr enthaelt.
    setIsDirty(false);
    setSelectedVersionId(null);
  }, []);

  const applyLoadedVersionDetail = useCallback((detail: AdminWorkflowDefinitionVersionDetail) => {
    const nextDraft = toVersionDraft(detail);
    setVersionDetail(detail);
    setVersionDraft(nextDraft);
    setIsDirty(false);
    setSelectedVersionId(detail.id);
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
      const detail = await ensureAdminWorkflowDefinitionWorkingDraft(definitionId);
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


  const confirmSelectionChange = useCallback(async () => {
    if (!hasUnsavedChanges) {
      return true;
    }

    return confirm({
      title: "Ungespeicherte Änderungen verwerfen?",
      description: "Ihre aktuellen Änderungen würden verworfen. Nur fortfahren, wenn der aktuelle Entwurf nicht behalten werden soll.",
      confirmLabel: "Verwerfen",
      cancelLabel: "Weiter bearbeiten",
      tone: "danger",
    });
  }, [confirm, hasUnsavedChanges]);

  const selectDefinition = useCallback(async (definitionId: number) => {
    if (!(await confirmSelectionChange())) {
      return;
    }

    resetLoadedVersion();
    setSelectedDefinitionId(definitionId);
  }, [confirmSelectionChange, resetLoadedVersion]);

  const selectVersion = useCallback(async (versionId: number) => {
    if (!(await confirmSelectionChange())) {
      return;
    }

    setSelectedVersionId(versionId);
  }, [confirmSelectionChange]);

  const discardChanges = useCallback(async () => {
    if (!selectedDefinition) {
      return false;
    }

    if (!(await confirmSelectionChange())) {
      return false;
    }

    onError(null);
    onNotice(null);
    await resolveWorkingDraft(selectedDefinition.id, { refreshDefinitions: true });
    return true;
  }, [confirmSelectionChange, onError, onNotice, resolveWorkingDraft, selectedDefinition]);

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

  const addNode = useCallback((nodeType: WorkflowBuilderNodeDraft["nodeType"] = "task") => {
    const nextNode = {
      ...createEmptyNodeDraft(nodeType, versionDraft.nodes.length + 1),
      positionX: null,
      positionY: null,
    };
    setVersionDraft((current) => ({ ...current, nodes: [...current.nodes, nextNode] }));
    setIsDirty(true);
  }, [versionDraft.nodes]);

  const reorderNode = useCallback((nodeId: string, beforeNodeId: string | null) => {
    setVersionDraft((current) => {
      const fromIndex = current.nodes.findIndex((node) => node.id === nodeId);
      if (fromIndex < 0) return current;
      if (beforeNodeId === nodeId) return current;
      const moved = current.nodes[fromIndex];
      if (!moved) return current;
      const without = current.nodes.filter((_, idx) => idx !== fromIndex);
      if (beforeNodeId === null) {
        return { ...current, nodes: [...without, moved] };
      }
      const insertAt = without.findIndex((node) => node.id === beforeNodeId);
      if (insertAt < 0) {
        return { ...current, nodes: [...without, moved] };
      }
      return {
        ...current,
        nodes: [...without.slice(0, insertAt), moved, ...without.slice(insertAt)],
      };
    });
    setIsDirty(true);
  }, []);

  const moveNode = useCallback((nodeId: string, direction: "up" | "down") => {
    setVersionDraft((current) => {
      const index = current.nodes.findIndex((node) => node.id === nodeId);
      if (index < 0) {
        return current;
      }

      const targetIndex = direction === "up" ? index - 1 : index + 1;
      if (targetIndex < 0 || targetIndex >= current.nodes.length) {
        return current;
      }

      const nextNodes = [...current.nodes];
      const [moved] = nextNodes.splice(index, 1);
      nextNodes.splice(targetIndex, 0, moved!);

      return {
        ...current,
        nodes: nextNodes.map((node, idx) => ({ ...node, sortOrder: String(idx + 1) })),
      };
    });
    setIsDirty(true);
  }, []);

  const removeNode = useCallback((nodeId: string) => {
    const nodeToRemove = versionDraft.nodes.find((node) => node.id === nodeId) ?? null;
    if (!canManageAdvanced && nodeToRemove?.nodeType === "automation") {
      onError("Automatisierungen koennen nur im Admin-Modus entfernt werden.");
      return;
    }

    setVersionDraft((current) => {
      const removedNode = current.nodes.find((node) => node.id === nodeId);
      const removedNodeKey = removedNode?.nodeKey.trim();
      const nextNodes = current.nodes.filter((node) => node.id !== nodeId);

      return {
        ...current,
        nodes: nextNodes,
        edges: current.edges.filter((edge) => edge.sourceNodeKey !== removedNodeKey && edge.targetNodeKey !== removedNodeKey),
      };
    });
    setIsDirty(true);
    onError(null);
  }, [canManageAdvanced, onError, versionDraft.nodes]);

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

  const addEdge = useCallback((initial?: { sourceNodeKey?: string; targetNodeKey?: string }) => {
    let createdEdgeId = "";
    setVersionDraft((current) => {
      const draft = createEmptyEdgeDraft();
      if (initial?.sourceNodeKey) {
        draft.sourceNodeKey = initial.sourceNodeKey;
      }
      if (initial?.targetNodeKey) {
        draft.targetNodeKey = initial.targetNodeKey;
      }
      createdEdgeId = draft.id;
      return { ...current, edges: [...current.edges, draft] };
    });
    setIsDirty(true);
    return createdEdgeId;
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
    setIsDirty(true);
  }, []);

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

    const shouldDelete = await confirm({
      title: "Workflow löschen?",
      description: `Der Ablauf '${selectedDefinition.name}' wird dauerhaft entfernt. Dieser Schritt lässt sich nicht rückgängig machen.`,
      confirmLabel: "Workflow löschen",
      cancelLabel: "Abbrechen",
      tone: "danger",
    });

    if (!shouldDelete) {
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
  }, [canManageAdvanced, confirm, loadDefinitions, onError, onNotice, resetLoadedVersion, selectedDefinition]);

  const saveVersion = useCallback(async () => {
    if (!selectedDefinition || !selectedVersionId) {
      onError("Bitte zuerst einen Ablauf und einen Stand auswaehlen.");
      return;
    }

    if (localValidationIssues.length > 0) {
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

      // FE-13: Optimistic-Concurrency-Loop. Erst-Save mit `expectedUpdatedAt`,
      // damit der Server 409 wirft, falls jemand parallel geschrieben hat.
      // Nach Bestätigung wiederholen wir ohne Token (= Force-Overwrite).
      let expectedUpdatedAt: string | null = versionDetail?.updatedAt ?? null;
      while (true) {
        try {
          const saved = await replaceAdminWorkflowDefinitionVersion(
            selectedVersionId,
            buildVersionReplacePayload(versionDraft, expectedUpdatedAt)
          );
          await loadDefinitions({
            keepSelection: true,
            selectedDefinitionId: selectedDefinition.id,
            selectedVersionId,
          });
          const nextDraft = toVersionDraft(saved);
          setVersionDetail(saved);
          setVersionDraft(nextDraft);
          setIsDirty(false);
          onNotice(`Stand ${saved.versionNumber} wurde gespeichert.`);
          break;
        } catch (err) {
          const status = (err as { status?: number } | null | undefined)?.status;
          if (status !== 409) {
            throw err;
          }

          const shouldOverwrite = await confirm({
            title: "Entwurf wurde zwischenzeitlich geändert",
            description:
              "Seit Ihrem letzten Laden hat jemand anderes diesen Entwurf geändert (z. B. über den Aufgabenvorlagen-Editor). " +
              "Wenn Sie jetzt speichern, werden diese neueren Änderungen überschrieben.",
            confirmLabel: "Trotzdem speichern",
            cancelLabel: "Abbrechen und neu laden",
            tone: "danger",
          });

          if (!shouldOverwrite) {
            try {
              const fresh = await getAdminWorkflowDefinitionVersion(selectedVersionId);
              applyLoadedVersionDetail(fresh);
              onNotice("Entwurf wurde neu geladen – bitte Änderungen prüfen.");
            } catch {
              // Reload-Fehler ignorieren; der Nutzer behält zumindest seinen Stand
            }
            return;
          }

          // Force-Overwrite: ohne Token erneut speichern
          expectedUpdatedAt = null;
        }
      }
    } catch (err) {
      onError(err instanceof Error ? err.message : "Stand konnte nicht gespeichert werden.");
    } finally {
      setIsSaving(false);
    }
  }, [applyLoadedVersionDetail, canManageAdvanced, confirm, definitionDraft.description, definitionDraft.name, hasDefinitionMetadataChanges, loadDefinitions, localValidationIssues.length, onError, onNotice, selectedDefinition, selectedVersionId, versionDetail, versionDraft]);

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

    if (localValidationIssues.length > 0) {
      onError(`Der aktuelle Stand enthaelt ${localValidationIssues.length} lokale Issues.`);
      onNotice(null);
      return;
    }

    onError(null);
    onNotice("Lokale Pruefung erfolgreich.");
  }, [localValidationIssues.length, onError, onNotice, selectedVersionSummary]);

  return {
    definitions,
    actionDefinitions,
    automationPropertyCatalog,
    responsibilityOwners,
    taskTemplates,
    answerDefinitions,
    taskTemplateConditions,
    taskTemplateDependencies,
    selectedDefinition,
    selectedVersionSummary,
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
    discardChanges,
    updateDefinitionDraft,
    updateNewDefinitionDraft,
    updateVersionDraftField,
    addNode,
    moveNode,
    reorderNode,
    updateNode,
    removeNode,
    addActionFromDefinition,
    updateAction,
    removeAction,
    addEdge,
    updateEdge,
    removeEdge,
    createDefinition,
    deleteDefinition,
    saveVersion,
    validateDraft,
    publishVersion,
  };
}
