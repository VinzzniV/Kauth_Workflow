import { useCallback, useEffect, useMemo, useState } from "react";
import { toNullableText } from "../components/admin-config/adminConfigHelpers";
import {
  updateAdminGraphApplicationConfiguration,
} from "../services/adminApi";
import type { AdminGraphApplicationConfiguration } from "../types/auth";

type UseAdminGraphApplicationConfigurationOptions = {
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
};

export function useAdminGraphApplicationConfiguration({
  onNotice,
  onError,
}: UseAdminGraphApplicationConfigurationOptions) {
  const [graphApplicationConfiguration, setGraphApplicationConfiguration] =
    useState<AdminGraphApplicationConfiguration | null>(null);
  const [graphTenantIdDraft, setGraphTenantIdDraft] = useState<string>("");
  const [graphClientIdDraft, setGraphClientIdDraft] = useState<string>("");
  const [graphClientSecretDraft, setGraphClientSecretDraft] = useState<string>("");
  const [isSavingGraphApplicationConfiguration, setIsSavingGraphApplicationConfiguration] =
    useState<boolean>(false);

  useEffect(() => {
    if (!graphApplicationConfiguration) {
      setGraphTenantIdDraft("");
      setGraphClientIdDraft("");
      setGraphClientSecretDraft("");
      return;
    }

    setGraphTenantIdDraft(graphApplicationConfiguration.tenantId ?? "");
    setGraphClientIdDraft(graphApplicationConfiguration.clientId ?? "");
    setGraphClientSecretDraft("");
  }, [graphApplicationConfiguration]);

  const hasGraphApplicationDraftChanges = useMemo(() => {
    if (!graphApplicationConfiguration) {
      return false;
    }

    return (
      toNullableText(graphTenantIdDraft) !== graphApplicationConfiguration.tenantId
      || toNullableText(graphClientIdDraft) !== graphApplicationConfiguration.clientId
      || graphClientSecretDraft.trim().length > 0
    );
  }, [graphApplicationConfiguration, graphClientIdDraft, graphClientSecretDraft, graphTenantIdDraft]);

  const saveGraphApplicationConfiguration = useCallback(async () => {
    setIsSavingGraphApplicationConfiguration(true);
    onNotice(null);
    onError(null);

    try {
      const clientSecret = toNullableText(graphClientSecretDraft);
      const updatedConfiguration = await updateAdminGraphApplicationConfiguration({
        tenantId: toNullableText(graphTenantIdDraft),
        clientId: toNullableText(graphClientIdDraft),
        ...(clientSecret ? { clientSecret } : {}),
      });
      setGraphApplicationConfiguration(updatedConfiguration);
      setGraphClientSecretDraft("");
      onNotice("Graph-Anwendungskonfiguration wurde gespeichert.");
    } catch (err) {
      const message =
        err instanceof Error ? err.message : "Graph-Anwendungskonfiguration konnte nicht gespeichert werden.";
      onError(message);
    } finally {
      setIsSavingGraphApplicationConfiguration(false);
    }
  }, [graphClientIdDraft, graphClientSecretDraft, graphTenantIdDraft, onError, onNotice]);

  return {
    graphApplicationConfiguration,
    setGraphApplicationConfiguration,
    graphTenantIdDraft,
    graphClientIdDraft,
    graphClientSecretDraft,
    isSavingGraphApplicationConfiguration,
    hasGraphApplicationDraftChanges,
    setGraphTenantIdDraft,
    setGraphClientIdDraft,
    setGraphClientSecretDraft,
    saveGraphApplicationConfiguration,
  };
}
