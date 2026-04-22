import { useCallback, useEffect, useMemo, useState } from "react";
import {
  getAdminNotificationTemplates,
  previewAdminNotificationTemplate,
  searchAdminNotificationTemplateRotationPlans,
  searchAdminNotificationTemplateWorkflows,
  updateAdminNotificationTemplate,
} from "../services/adminApi";
import type {
  AdminNotificationTemplate,
  AdminNotificationTemplatePreviewResponse,
  AdminNotificationTemplateRotationPlanPreviewTarget,
  AdminNotificationTemplateWorkflowPreviewTarget,
} from "../types/auth";

type UseAdminNotificationTemplatesOptions = {
  enabled: boolean;
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
};

type NotificationTemplateDraft = {
  subjectTemplate: string;
  bodyTemplate: string;
};

export function useAdminNotificationTemplates({
  enabled,
  onNotice,
  onError,
}: UseAdminNotificationTemplatesOptions) {
  const [notificationTemplates, setNotificationTemplates] = useState<AdminNotificationTemplate[]>([]);
  const [templateDrafts, setTemplateDrafts] = useState<Record<string, NotificationTemplateDraft>>({});
  const [selectedTemplateKey, setSelectedTemplateKey] = useState<string | null>(null);
  const [workflowPreviewSearch, setWorkflowPreviewSearch] = useState("");
  const [rotationPlanPreviewSearch, setRotationPlanPreviewSearch] = useState("");
  const [workflowPreviewTargets, setWorkflowPreviewTargets] = useState<AdminNotificationTemplateWorkflowPreviewTarget[]>([]);
  const [rotationPlanPreviewTargets, setRotationPlanPreviewTargets] = useState<AdminNotificationTemplateRotationPlanPreviewTarget[]>([]);
  const [selectedWorkflowPreviewUid, setSelectedWorkflowPreviewUid] = useState<string | null>(null);
  const [selectedRotationPlanPreviewId, setSelectedRotationPlanPreviewId] = useState<number | null>(null);
  const [previewResponse, setPreviewResponse] = useState<AdminNotificationTemplatePreviewResponse | null>(null);
  const [selectedPreviewVariantIndex, setSelectedPreviewVariantIndex] = useState(0);
  const [isLoadingNotificationTemplates, setIsLoadingNotificationTemplates] = useState(false);
  const [isSavingNotificationTemplate, setIsSavingNotificationTemplate] = useState(false);
  const [isLoadingPreviewTargets, setIsLoadingPreviewTargets] = useState(false);
  const [isLoadingPreview, setIsLoadingPreview] = useState(false);

  const loadTemplates = useCallback(async () => {
    if (!enabled) {
      return;
    }

    setIsLoadingNotificationTemplates(true);
    onError(null);

    try {
      const templates = await getAdminNotificationTemplates();
      setNotificationTemplates(templates);
      setTemplateDrafts(
        templates.reduce<Record<string, NotificationTemplateDraft>>((accumulator, template) => {
          accumulator[template.templateKey] = {
            subjectTemplate: template.subjectTemplate,
            bodyTemplate: template.bodyTemplate,
          };
          return accumulator;
        }, {})
      );
      setSelectedTemplateKey((current) => current ?? templates[0]?.templateKey ?? null);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Mail-Vorlagen konnten nicht geladen werden.";
      onError(message);
      setNotificationTemplates([]);
      setTemplateDrafts({});
      setSelectedTemplateKey(null);
    } finally {
      setIsLoadingNotificationTemplates(false);
    }
  }, [enabled, onError]);

  useEffect(() => {
    if (!enabled) {
      return;
    }

    void loadTemplates();
  }, [enabled, loadTemplates]);

  const selectedTemplate = useMemo(
    () => notificationTemplates.find((template) => template.templateKey === selectedTemplateKey) ?? null,
    [notificationTemplates, selectedTemplateKey]
  );
  const selectedTemplateDraft = selectedTemplateKey ? templateDrafts[selectedTemplateKey] ?? null : null;

  const hasSelectedTemplateChanges = useMemo(() => {
    if (!selectedTemplate || !selectedTemplateDraft) {
      return false;
    }

    return (
      selectedTemplateDraft.subjectTemplate !== selectedTemplate.subjectTemplate
      || selectedTemplateDraft.bodyTemplate !== selectedTemplate.bodyTemplate
    );
  }, [selectedTemplate, selectedTemplateDraft]);

  useEffect(() => {
    setPreviewResponse(null);
    setSelectedPreviewVariantIndex(0);
    setSelectedWorkflowPreviewUid(null);
    setSelectedRotationPlanPreviewId(null);
  }, [selectedTemplateKey]);

  useEffect(() => {
    if (!enabled || !selectedTemplate) {
      return;
    }

    const isWorkflowPreview = selectedTemplate.previewTargetType === "workflow";
    const query = isWorkflowPreview ? workflowPreviewSearch : rotationPlanPreviewSearch;
    let isCancelled = false;

    setIsLoadingPreviewTargets(true);

    const loadTargets = async () => {
      try {
        if (isWorkflowPreview) {
          const targets = await searchAdminNotificationTemplateWorkflows(query, 20);
          if (isCancelled) {
            return;
          }

          setWorkflowPreviewTargets(targets);
          setRotationPlanPreviewTargets([]);
          setSelectedWorkflowPreviewUid((current) =>
            current && targets.some((target) => target.workflowUid === current)
              ? current
              : targets[0]?.workflowUid ?? null
          );
          return;
        }

        const targets = await searchAdminNotificationTemplateRotationPlans(query, 20);
        if (isCancelled) {
          return;
        }

        setRotationPlanPreviewTargets(targets);
        setWorkflowPreviewTargets([]);
        setSelectedRotationPlanPreviewId((current) =>
          current && targets.some((target) => target.rotationPlanId === current)
            ? current
            : targets[0]?.rotationPlanId ?? null
        );
      } catch (err) {
        if (isCancelled) {
          return;
        }

        const message = err instanceof Error ? err.message : "Preview-Ziele konnten nicht geladen werden.";
        onError(message);
        setWorkflowPreviewTargets([]);
        setRotationPlanPreviewTargets([]);
      } finally {
        if (!isCancelled) {
          setIsLoadingPreviewTargets(false);
        }
      }
    };

    void loadTargets();
    return () => {
      isCancelled = true;
    };
  }, [
    enabled,
    onError,
    rotationPlanPreviewSearch,
    selectedTemplate,
    workflowPreviewSearch,
  ]);

  const updateSelectedDraft = useCallback((patch: Partial<NotificationTemplateDraft>) => {
    setTemplateDrafts((current) => {
      if (!selectedTemplateKey) {
        return current;
      }

      const existingDraft = current[selectedTemplateKey] ?? { subjectTemplate: "", bodyTemplate: "" };
      return {
        ...current,
        [selectedTemplateKey]: {
          ...existingDraft,
          ...patch,
        },
      };
    });
  }, [selectedTemplateKey]);

  const saveSelectedTemplate = useCallback(async () => {
    if (!selectedTemplate || !selectedTemplateDraft) {
      return;
    }

    setIsSavingNotificationTemplate(true);
    onNotice(null);
    onError(null);

    try {
      const updated = await updateAdminNotificationTemplate(selectedTemplate.templateKey, selectedTemplateDraft);
      setNotificationTemplates((current) =>
        current.map((template) => template.templateKey === updated.templateKey ? updated : template)
      );
      setTemplateDrafts((current) => ({
        ...current,
        [updated.templateKey]: {
          subjectTemplate: updated.subjectTemplate,
          bodyTemplate: updated.bodyTemplate,
        },
      }));
      onNotice(`Mail-Vorlage "${updated.displayName}" wurde gespeichert.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Mail-Vorlage konnte nicht gespeichert werden.";
      onError(message);
    } finally {
      setIsSavingNotificationTemplate(false);
    }
  }, [onError, onNotice, selectedTemplate, selectedTemplateDraft]);

  const renderPreview = useCallback(async () => {
    if (!selectedTemplate) {
      return;
    }

    const payload = selectedTemplate.previewTargetType === "workflow"
      ? { workflowUid: selectedWorkflowPreviewUid, rotationPlanId: null }
      : { workflowUid: null, rotationPlanId: selectedRotationPlanPreviewId };
    if (selectedTemplate.previewTargetType === "workflow" && !selectedWorkflowPreviewUid) {
      onError("Bitte wählen Sie zuerst einen Workflow für die Vorschau aus.");
      return;
    }

    if (selectedTemplate.previewTargetType === "rotation_plan" && !selectedRotationPlanPreviewId) {
      onError("Bitte wählen Sie zuerst einen Durchlaufplan für die Vorschau aus.");
      return;
    }

    setIsLoadingPreview(true);
    onError(null);

    try {
      const response = await previewAdminNotificationTemplate(selectedTemplate.templateKey, payload);
      setPreviewResponse(response);
      setSelectedPreviewVariantIndex(0);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Preview konnte nicht geladen werden.";
      onError(message);
      setPreviewResponse(null);
    } finally {
      setIsLoadingPreview(false);
    }
  }, [
    onError,
    selectedRotationPlanPreviewId,
    selectedTemplate,
    selectedWorkflowPreviewUid,
  ]);

  return {
    notificationTemplates,
    selectedTemplate,
    selectedTemplateKey,
    setSelectedTemplateKey,
    selectedTemplateSubjectDraft: selectedTemplateDraft?.subjectTemplate ?? "",
    selectedTemplateBodyDraft: selectedTemplateDraft?.bodyTemplate ?? "",
    setSelectedTemplateSubjectDraft: (value: string) => updateSelectedDraft({ subjectTemplate: value }),
    setSelectedTemplateBodyDraft: (value: string) => updateSelectedDraft({ bodyTemplate: value }),
    hasSelectedTemplateChanges,
    workflowPreviewSearch,
    setWorkflowPreviewSearch,
    rotationPlanPreviewSearch,
    setRotationPlanPreviewSearch,
    workflowPreviewTargets,
    rotationPlanPreviewTargets,
    selectedWorkflowPreviewUid,
    setSelectedWorkflowPreviewUid,
    selectedRotationPlanPreviewId,
    setSelectedRotationPlanPreviewId,
    previewResponse,
    selectedPreviewVariantIndex,
    setSelectedPreviewVariantIndex,
    isLoadingNotificationTemplates,
    isSavingNotificationTemplate,
    isLoadingPreviewTargets,
    isLoadingPreview,
    reloadNotificationTemplates: loadTemplates,
    saveSelectedTemplate,
    renderPreview,
  };
}
