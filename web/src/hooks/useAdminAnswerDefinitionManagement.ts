import { useCallback, useEffect, useMemo, useState } from "react";
import {
  createAdminAnswerDefinition,
  deleteAdminAnswerDefinition,
  getAdminAnswerDefinitions,
  getAdminProcessTypes,
  updateAdminAnswerDefinition,
} from "../services/adminConfigApi";
import type { AdminAnswerDefinition, AdminProcessType } from "../types/auth";
import { toNullableText } from "../components/admin-config/adminConfigHelpers";
import { useConfirmationDialog } from "../components/feedback/useConfirmationDialog";

type AnswerDefinitionDraft = {
  answerKey: string;
  title: string;
  category: string;
  description: string;
  iconKey: string;
  inputType: "boolean" | "text" | "select" | "multi_select";
  isRequired: boolean;
  sortOrder: string;
  isActive: boolean;
};

type UseAdminAnswerDefinitionManagementOptions = {
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
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
  const [processTypes, setProcessTypes] = useState<AdminProcessType[]>([]);
  const [selectedProcessTypeId, setSelectedProcessTypeId] = useState<number | null>(null);
  const [definitions, setDefinitions] = useState<AdminAnswerDefinition[]>([]);
  const [selectedDefinitionId, setSelectedDefinitionId] = useState<number | null>(null);
  const [draft, setDraft] = useState<AnswerDefinitionDraft>(EMPTY_DRAFT);
  const [isCreatingNew, setIsCreatingNew] = useState(false);
  const [isLoadingProcessTypes, setIsLoadingProcessTypes] = useState(true);
  const [isLoadingDefinitions, setIsLoadingDefinitions] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);

  const selectedDefinition = useMemo(
    () => definitions.find((definition) => definition.id === selectedDefinitionId) ?? null,
    [definitions, selectedDefinitionId]
  );

  const loadDefinitions = useCallback(async (processTypeId: number, definitionIdToSelect?: number | null) => {
    setIsLoadingDefinitions(true);

    try {
      const loadedDefinitions = await getAdminAnswerDefinitions(processTypeId);
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
      setIsLoadingDefinitions(false);
    }
  }, []);

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
      setDefinitions([]);
      setSelectedDefinitionId(null);
      setIsCreatingNew(false);
      setDraft(EMPTY_DRAFT);
      return;
    }

    onError(null);
    void loadDefinitions(selectedProcessTypeId).catch((err) => {
      const message = err instanceof Error ? err.message : "Answer Definitions konnten nicht geladen werden.";
      onError(message);
    });
  }, [loadDefinitions, onError, selectedProcessTypeId]);

  const selectProcessType = useCallback((nextValue: string) => {
    const parsed = Number(nextValue);
    setSelectedProcessTypeId(Number.isFinite(parsed) && parsed > 0 ? parsed : null);
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
    if (!selectedProcessTypeId) {
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
      processTypeId: selectedProcessTypeId,
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
  }, [draft, selectedProcessTypeId]);

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

    setIsSaving(true);
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
      setIsSaving(false);
    }
  }, [buildPayload, onError, onNotice, sortDefinitions]);

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

    setIsSaving(true);
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
      setIsSaving(false);
    }
  }, [buildPayload, onError, onNotice, selectedDefinitionId, sortDefinitions]);

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

    setIsDeleting(true);
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
      setIsDeleting(false);
    }
  }, [confirm, onError, onNotice, selectedDefinition]);

  return {
    processTypes,
    selectedProcessTypeId,
    definitions,
    selectedDefinition,
    draft,
    isCreatingNew,
    isLoadingProcessTypes,
    isLoadingDefinitions,
    isSaving,
    isDeleting,
    selectProcessType,
    selectDefinition,
    startCreatingDefinition,
    updateDraft,
    createDefinition,
    saveDefinition,
    removeDefinition,
  };
}
