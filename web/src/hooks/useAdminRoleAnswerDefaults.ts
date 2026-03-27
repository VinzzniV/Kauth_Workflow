import { useCallback, useEffect, useMemo, useState } from "react";
import {
  getAdminAnswerDefinitions,
  getAdminProcessTypes,
  getAdminRoleAnswerDefaults,
  updateAdminRoleAnswerDefaults,
} from "../services/adminConfigApi";
import { getAdminRoles } from "../services/lifecycleApi";
import type {
  AdminAnswerDefinition,
  AdminProcessType,
  AdminRole,
  AdminRoleAnswerDefault,
} from "../types/auth";
import { toNullableText } from "../components/admin-config/adminConfigHelpers";

type DefaultDraftValue = {
  text: string;
  boolean: boolean | null;
};

type UseAdminRoleAnswerDefaultsOptions = {
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
};

function getDraftKey(appRoleId: number, answerKey: string) {
  return `${appRoleId}::${answerKey}`;
}

export function useAdminRoleAnswerDefaults({
  onNotice,
  onError,
}: UseAdminRoleAnswerDefaultsOptions) {
  const [processTypes, setProcessTypes] = useState<AdminProcessType[]>([]);
  const [selectedProcessTypeId, setSelectedProcessTypeId] = useState<number | null>(null);
  const [roles, setRoles] = useState<AdminRole[]>([]);
  const [definitions, setDefinitions] = useState<AdminAnswerDefinition[]>([]);
  const [defaults, setDefaults] = useState<AdminRoleAnswerDefault[]>([]);
  const [drafts, setDrafts] = useState<Record<string, DefaultDraftValue>>({});
  const [isLoadingProcessTypes, setIsLoadingProcessTypes] = useState(true);
  const [isLoadingMatrix, setIsLoadingMatrix] = useState(false);
  const [isSaving, setIsSaving] = useState(false);

  const sortedRoles = useMemo(
    () =>
      roles.slice().sort((left, right) => {
        const leftKey = `${left.roleKind}|${left.departmentName ?? ""}|${left.roleName}`;
        const rightKey = `${right.roleKind}|${right.departmentName ?? ""}|${right.roleName}`;
        return leftKey.localeCompare(rightKey, "de");
      }),
    [roles]
  );

  const sortedDefinitions = useMemo(
    () =>
      definitions
        .slice()
        .sort((left, right) =>
          left.sortOrder === right.sortOrder
            ? left.title.localeCompare(right.title, "de")
            : left.sortOrder - right.sortOrder
        ),
    [definitions]
  );

  const loadMatrixData = useCallback(async (processTypeId: number) => {
    setIsLoadingMatrix(true);

    try {
      const [loadedRoles, loadedDefinitions, loadedDefaults] = await Promise.all([
        getAdminRoles(),
        getAdminAnswerDefinitions(processTypeId),
        getAdminRoleAnswerDefaults(processTypeId),
      ]);

      setRoles(loadedRoles);
      setDefinitions(loadedDefinitions);
      setDefaults(loadedDefaults);
      setDrafts(
        Object.fromEntries(
          loadedDefaults.map((item) => [
            getDraftKey(item.appRoleId, item.answerKey),
            {
              text: item.defaultValueText ?? "",
              boolean: item.defaultValueBoolean,
            },
          ])
        )
      );
    } catch (err) {
      setRoles([]);
      setDefinitions([]);
      setDefaults([]);
      setDrafts({});
      throw err;
    } finally {
      setIsLoadingMatrix(false);
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
      setRoles([]);
      setDefinitions([]);
      setDefaults([]);
      setDrafts({});
      return;
    }

    onError(null);
    void loadMatrixData(selectedProcessTypeId).catch((err) => {
      const message = err instanceof Error ? err.message : "Role Answer Defaults konnten nicht geladen werden.";
      onError(message);
    });
  }, [loadMatrixData, onError, selectedProcessTypeId]);

  const selectProcessType = useCallback((nextValue: string) => {
    const parsed = Number(nextValue);
    setSelectedProcessTypeId(Number.isFinite(parsed) && parsed > 0 ? parsed : null);
  }, []);

  const getCellDraft = useCallback((appRoleId: number, answerKey: string): DefaultDraftValue => {
    return drafts[getDraftKey(appRoleId, answerKey)] ?? { text: "", boolean: null };
  }, [drafts]);

  const updateTextDraft = useCallback((appRoleId: number, answerKey: string, value: string) => {
    const key = getDraftKey(appRoleId, answerKey);
    setDrafts((current) => ({
      ...current,
      [key]: {
        text: value,
        boolean: current[key]?.boolean ?? null,
      },
    }));
  }, []);

  const updateBooleanDraft = useCallback((appRoleId: number, answerKey: string, value: boolean | null) => {
    const key = getDraftKey(appRoleId, answerKey);
    setDrafts((current) => ({
      ...current,
      [key]: {
        text: current[key]?.text ?? "",
        boolean: value,
      },
    }));
  }, []);

  const hasChanges = useMemo(() => {
    for (const definition of definitions) {
      for (const role of roles) {
        const key = getDraftKey(role.roleId, definition.answerKey);
        const draft = drafts[key] ?? { text: "", boolean: null };
        const persisted = defaults.find(
          (item) => item.appRoleId === role.roleId && item.answerKey === definition.answerKey
        );

        if ((persisted?.defaultValueText ?? "") !== draft.text) {
          return true;
        }

        if ((persisted?.defaultValueBoolean ?? null) !== draft.boolean) {
          return true;
        }
      }
    }

    return false;
  }, [defaults, definitions, drafts, roles]);

  const saveDefaults = useCallback(async () => {
    if (!selectedProcessTypeId) {
      onNotice(null);
      onError("Bitte zuerst einen Prozesstyp auswählen.");
      return;
    }

    setIsSaving(true);
    onNotice(null);
    onError(null);

    try {
      const items = definitions.flatMap((definition) =>
        roles.map((role) => {
          const draft = drafts[getDraftKey(role.roleId, definition.answerKey)] ?? { text: "", boolean: null };
          return {
            appRoleId: role.roleId,
            answerKey: definition.answerKey,
            defaultValueText: toNullableText(draft.text),
            defaultValueBoolean: draft.boolean,
          };
        })
      );

      const updatedDefaults = await updateAdminRoleAnswerDefaults({
        processTypeId: selectedProcessTypeId,
        items,
      });

      setDefaults(updatedDefaults);
      setDrafts(
        Object.fromEntries(
          updatedDefaults.map((item) => [
            getDraftKey(item.appRoleId, item.answerKey),
            {
              text: item.defaultValueText ?? "",
              boolean: item.defaultValueBoolean,
            },
          ])
        )
      );
      onNotice("Role Answer Defaults wurden gespeichert.");
    } catch (err) {
      const message = err instanceof Error ? err.message : "Role Answer Defaults konnten nicht gespeichert werden.";
      onError(message);
    } finally {
      setIsSaving(false);
    }
  }, [definitions, drafts, onError, onNotice, roles, selectedProcessTypeId]);

  return {
    processTypes,
    selectedProcessTypeId,
    sortedRoles,
    sortedDefinitions,
    isLoadingProcessTypes,
    isLoadingMatrix,
    isSaving,
    hasChanges,
    selectProcessType,
    getCellDraft,
    updateTextDraft,
    updateBooleanDraft,
    saveDefaults,
  };
}
