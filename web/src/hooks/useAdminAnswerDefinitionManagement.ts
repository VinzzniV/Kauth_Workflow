import { useCallback, useEffect, useMemo, useState } from "react";
import {
  createAdminAnswerDefinition,
  deleteAdminAnswerDefinition,
  getAdminAnswerDefinitions,
  getAdminWorkflowDefinitions,
  updateAdminAnswerDefinition,
} from "../services/adminConfigApi";
import type { AdminAnswerDefinition, AdminWorkflowDefinitionSummary } from "../types/auth";
import { toNullableText } from "../components/admin-config/adminConfigHelpers";
import { useConfirmationDialog } from "../components/feedback/useConfirmationDialog";

type AnswerDefinitionDraft = {
  answerKey: string;
  title: string;
  category: string;
  description: string;
  iconKey: string;
  // Slice 5: person_lookup als neuer Form-InputType (Werte in valueNumber).
  inputType: "boolean" | "text" | "select" | "multi_select" | "person_lookup";
  isRequired: boolean;
  sortOrder: string;
  isActive: boolean;
};

type UseAdminAnswerDefinitionManagementOptions = {
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
};

type OperationState = {
  isLoadingProcessTypes: boolean;
  isLoadingDefinitions: boolean;
  isSaving: boolean;
  isDeleting: boolean;
};

const EMPTY_DRAFT: AnswerDefinitionDraft = {
  answerKey: "",
  title: "",
  category: "general",
  description: "",
  iconKey: "berechtigungen",
  inputType: "boolean",
  isRequired: false,
  sortOrder: "0",
  isActive: true,
};

const INITIAL_OPERATION_STATE: OperationState = {
  isLoadingProcessTypes: true,
  isLoadingDefinitions: false,
  isSaving: false,
  isDeleting: false,
};

function toDraft(definition: AdminAnswerDefinition): AnswerDefinitionDraft {
  return {
    answerKey: definition.answerKey,
    title: definition.title,
    category: definition.category,
    description: definition.description,
    iconKey: definition.iconKey ?? "",
    inputType: definition.inputType,
    isRequired: definition.isRequired,
    sortOrder: String(definition.sortOrder),
    isActive: definition.isActive,
  };
}

export function useAdminAnswerDefinitionManagement({
  onNotice,
  onError,
}: UseAdminAnswerDefinitionManagementOptions) {
  const confirm = useConfirmationDialog();
  const [workflowDefinitions, setWorkflowDefinitions] = useState<AdminWorkflowDefinitionSummary[]>([]);
  const [selectedWorkflowDefinitionId, setSelectedWorkflowDefinitionId] = useState<number | null>(null);
  const [definitions, setDefinitions] = useState<AdminAnswerDefinition[]>([]);
  const [selectedDefinitionId, setSelectedDefinitionId] = useState<number | null>(null);
  const [draft, setDraft] = useState<AnswerDefinitionDraft>(EMPTY_DRAFT);
  const [isCreatingNew, setIsCreatingNew] = useState(false);
  const [operationState, setOperationState] = useState<OperationState>(INITIAL_OPERATION_STATE);

  const updateOperationState = useCallback((patch: Partial<OperationState>) => {
    setOperationState((current) => ({ ...current, ...patch }));
  }, []);

  const selectedDefinition = useMemo(
    () => definitions.find((definition) => definition.id === selectedDefinitionId) ?? null,
    [definitions, selectedDefinitionId]
  );

  const loadDefinitions = useCallback(async (workflowDefinitionId: number, definitionIdToSelect?: number | null) => {
    updateOperationState({ isLoadingDefinitions: true });

    try {
      const page = await getAdminAnswerDefinitions(workflowDefinitionId, { limit: 200 });
      const loadedDefinitions = page.items;
      setDefinitions(loadedDefinitions);

      if (typeof definitionIdToSelect === "number") {
        const matchingDefinition = loadedDefinitions.find((definition) => definition.id === definitionIdToSelect) ?? null;
        setSelectedDefinitionId(matchingDefinition?.id ?? null);
        setIsCreatingNew(false);
        setDraft(matchingDefinition ? toDraft(matchingDefinition) : EMPTY_DRAFT);
        return loadedDefinitions;
      }

      setSelectedDefinitionId(null);
      setIsCreatingNew(false);
      setDraft(EMPTY_DRAFT);
      return loadedDefinitions;
    } catch (err) {
      setDefinitions([]);
      setSelectedDefinitionId(null);
      setIsCreatingNew(false);
      setDraft(EMPTY_DRAFT);
      throw err;
    } finally {
      updateOperationState({ isLoadingDefinitions: false });
    }
  }, [updateOperationState]);

  useEffect(() => {
    updateOperationState({ isLoadingProcessTypes: true });
    getAdminWorkflowDefinitions({ limit: 200 })
      .then((page) => {
        const loadedDefinitions = page.items;
        setWorkflowDefinitions(loadedDefinitions);
        setSelectedWorkflowDefinitionId((current) => current ?? loadedDefinitions[0]?.id ?? null);
      })
      .catch((err) => {
        setWorkflowDefinitions([]);
        setSelectedWorkflowDefinitionId(null);
        onError(err instanceof Error ? err.message : "Workflow-Definitionen konnten nicht geladen werden.");
      })
      .finally(() => updateOperationState({ isLoadingProcessTypes: false }));
  }, [onError, updateOperationState]);

  useEffect(() => {
    if (!selectedWorkflowDefinitionId) {
      setDefinitions([]);
      setSelectedDefinitionId(null);
      setIsCreatingNew(false);
      setDraft(EMPTY_DRAFT);
      return;
    }

    onError(null);
    void loadDefinitions(selectedWorkflowDefinitionId).catch((err) => {
      const message = err instanceof Error ? err.message : "Answer Definitions konnten nicht geladen werden.";
      onError(message);
    });
  }, [loadDefinitions, onError, selectedWorkflowDefinitionId]);

  const selectWorkflowDefinition = useCallback((nextValue: string) => {
    const parsed = Number(nextValue);
    setSelectedWorkflowDefinitionId(Number.isFinite(parsed) && parsed > 0 ? parsed : null);
  }, []);

  const selectDefinition = useCallback((definition: AdminAnswerDefinition) => {
    setSelectedDefinitionId(definition.id);
    setIsCreatingNew(false);
    setDraft(toDraft(definition));
    onNotice(null);
    onError(null);
  }, [onError, onNotice]);

  const startCreatingDefinition = useCallback(() => {
    setSelectedDefinitionId(null);
    setIsCreatingNew(true);
    setDraft(EMPTY_DRAFT);
    onNotice(null);
    onError(null);
  }, [onError, onNotice]);

  const updateDraft = useCallback(<K extends keyof AnswerDefinitionDraft>(key: K, value: AnswerDefinitionDraft[K]) => {
    setDraft((current) => ({ ...current, [key]: value }));
  }, []);

  const buildPayload = useCallback(() => {
    if (!selectedWorkflowDefinitionId) {
      throw new Error("Bitte zuerst einen Prozesstyp auswählen.");
    }

    if (!draft.answerKey.trim()) {
      throw new Error("Answer Key ist erforderlich.");
    }

    if (!draft.title.trim()) {
      throw new Error("Titel ist erforderlich.");
    }

    const sortOrder = Number(draft.sortOrder);
    if (Number.isNaN(sortOrder)) {
      throw new Error("Sortierung muss eine Zahl sein.");
    }

    return {
      workflowDefinitionId: selectedWorkflowDefinitionId,
      answerKey: draft.answerKey.trim(),
      title: draft.title.trim(),
      category: draft.category.trim() || "general",
      description: draft.description,
      iconKey: toNullableText(draft.iconKey),
      inputType: draft.inputType,
      isRequired: draft.isRequired,
      sortOrder,
      isActive: draft.isActive,
    };
  }, [draft, selectedWorkflowDefinitionId]);

  const sortDefinitions = useCallback((items: AdminAnswerDefinition[]) => {
    return items.slice().sort((left, right) =>
      left.sortOrder === right.sortOrder
        ? left.title.localeCompare(right.title, "de")
        : left.sortOrder - right.sortOrder
    );
  }, []);

  const createDefinition = useCallback(async () => {
    let payload;
    try {
      payload = buildPayload();
    } catch (err) {
      onNotice(null);
      onError(err instanceof Error ? err.message : "Answer Definition konnte nicht vorbereitet werden.");
      return;
    }

    updateOperationState({ isSaving: true });
    onNotice(null);
    onError(null);

    try {
      const createdDefinition = await createAdminAnswerDefinition(payload);
      setDefinitions((current) => sortDefinitions(current.concat(createdDefinition)));
      setSelectedDefinitionId(createdDefinition.id);
      setIsCreatingNew(false);
      setDraft(toDraft(createdDefinition));
      onNotice(`Answer Definition ${createdDefinition.title} wurde angelegt.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Answer Definition konnte nicht angelegt werden.";
      onError(message);
    } finally {
      updateOperationState({ isSaving: false });
    }
  }, [buildPayload, onError, onNotice, sortDefinitions, updateOperationState]);

  const saveDefinition = useCallback(async () => {
    if (!selectedDefinitionId) {
      return;
    }

    let payload;
    try {
      payload = buildPayload();
    } catch (err) {
      onNotice(null);
      onError(err instanceof Error ? err.message : "Answer Definition konnte nicht vorbereitet werden.");
      return;
    }

    updateOperationState({ isSaving: true });
    onNotice(null);
    onError(null);

    try {
      const updatedDefinition = await updateAdminAnswerDefinition(selectedDefinitionId, payload);
      setDefinitions((current) =>
        sortDefinitions(current.map((item) => (item.id === updatedDefinition.id ? updatedDefinition : item)))
      );
      setDraft(toDraft(updatedDefinition));
      onNotice(`Answer Definition ${updatedDefinition.title} wurde gespeichert.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Answer Definition konnte nicht gespeichert werden.";
      onError(message);
    } finally {
      updateOperationState({ isSaving: false });
    }
  }, [buildPayload, onError, onNotice, selectedDefinitionId, sortDefinitions, updateOperationState]);

  const removeDefinition = useCallback(async () => {
    if (!selectedDefinition) {
      return;
    }

    const shouldDelete = await confirm({
      title: "Antwortfeld löschen?",
      description: `Das Antwortfeld "${selectedDefinition.title}" wird aus der Konfiguration entfernt. Prüfen Sie vorher, ob Vorlagen oder Standardwerte davon abhängen.`,
      confirmLabel: "Antwortfeld löschen",
      tone: "danger",
    });
    if (!shouldDelete) {
      return;
    }

    updateOperationState({ isDeleting: true });
    onNotice(null);
    onError(null);

    try {
      await deleteAdminAnswerDefinition(selectedDefinition.id);
      setDefinitions((current) => current.filter((item) => item.id !== selectedDefinition.id));
      setSelectedDefinitionId(null);
      setIsCreatingNew(false);
      setDraft(EMPTY_DRAFT);
      onNotice(`Answer Definition ${selectedDefinition.title} wurde gelöscht.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Answer Definition konnte nicht gelöscht werden.";
      onError(message);
    } finally {
      updateOperationState({ isDeleting: false });
    }
  }, [confirm, onError, onNotice, selectedDefinition, updateOperationState]);

  return {
    workflowDefinitions,
    selectedWorkflowDefinitionId,
    definitions,
    selectedDefinition,
    draft,
    isCreatingNew,
    isLoadingProcessTypes: operationState.isLoadingProcessTypes,
    isLoadingDefinitions: operationState.isLoadingDefinitions,
    isSaving: operationState.isSaving,
    isDeleting: operationState.isDeleting,
    selectWorkflowDefinition,
    selectDefinition,
    startCreatingDefinition,
    updateDraft,
    createDefinition,
    saveDefinition,
    removeDefinition,
  };
}
