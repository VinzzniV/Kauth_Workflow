// Detailansicht fuer einen einzelnen Onboarding-Fall mit Prozessstand, Antworten und Aufgaben.
import { useCallback, useEffect, useMemo, useState } from "react";
import { useParams } from "react-router-dom";
import { useCurrentUser } from "../auth/useCurrentUser";
import WorkflowAuditLog from "../components/workflow-detail/WorkflowAuditLog";
import WorkflowHeaderPanel from "../components/workflow-detail/WorkflowHeaderPanel";
import WorkflowProgressSection from "../components/workflow-detail/WorkflowProgressSection";
import WorkflowRequirementsPanel from "../components/workflow-detail/WorkflowRequirementsPanel";
import WorkflowTaskAreasSection from "../components/workflow-detail/WorkflowTaskAreasSection";
import {
  buildProcessSteps,
  buildTasksByArea,
  findCurrentTask,
  inferAreaFromTask,
  isDepartmentWorkflowPhase,
  toPhaseOwnerArea,
  toRegularEditingLabel,
  toTaskDisplayTitle,
  type ProcessAreaName,
  type ProcessStep,
} from "../components/workflow-detail/workflowDetailModel";
import EmptyState from "../components/feedback/EmptyState";
import LoadingState from "../components/feedback/LoadingState";
import PageHeader from "../components/layout/PageHeader";
import {
  applyRequirementBooleanEditorSelection,
  applyRequirementSingleSelectEditorSelection,
} from "../utils/requirementEditor";
import {
  addTaskComment as addTaskCommentApi,
  getWorkflowAuditLog,
  getWorkflowByUid,
  updateTaskStatus as updateTaskStatusApi,
  updateWorkflowSupervisorStep,
} from "../services/onboardingApi";
import type {
  RequirementSelectionState,
  WorkflowAuditEntry,
  WorkflowDetail,
  WorkflowTask,
} from "../types/workflow";
import {
  buildRequirementSelections,
  createEmptyRequirementSelection,
  toRequirementSelectionPayload,
} from "../utils/requirements";
import { getResponsibleResponsibilityLabel, getResponsibleUserLabel } from "../utils/taskAssignment";
import {
  mapVisibleTaskStatusToWorkflowStatus,
  type VisibleTaskStatus,
} from "../utils/taskStatus";
import { isWorkflowTerminalStatus } from "../utils/workflowStatus";

export default function WorkflowDetailPage() {
  const { uid = "" } = useParams<{ uid: string }>();
  const { capabilities } = useCurrentUser();
  const isReaderOnlyView =
    capabilities.hasReaderRole && !capabilities.hasProcessActorRole && !capabilities.canManageAdminConfiguration;

  const [workflow, setWorkflow] = useState<WorkflowDetail | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [auditEntries, setAuditEntries] = useState<WorkflowAuditEntry[]>([]);
  const [auditLogError, setAuditLogError] = useState<string | null>(null);
  const [isLoadingAuditLog, setIsLoadingAuditLog] = useState<boolean>(false);
  const [taskNotice, setTaskNotice] = useState<string | null>(null);
  const [taskError, setTaskError] = useState<string | null>(null);
  const [savingTaskIds, setSavingTaskIds] = useState<Record<number, boolean>>({});
  const [commentDrafts, setCommentDrafts] = useState<Record<number, string>>({});
  const [savingCommentTaskIds, setSavingCommentTaskIds] = useState<Record<number, boolean>>({});
  const [commentFeedbackTaskId, setCommentFeedbackTaskId] = useState<number | null>(null);
  const [commentFeedbackMessage, setCommentFeedbackMessage] = useState<string | null>(null);
  const [requirementSelections, setRequirementSelections] = useState<Record<number, RequirementSelectionState>>({});
  const [requirementsSaveError, setRequirementsSaveError] = useState<string | null>(null);
  const [requirementsSaveNotice, setRequirementsSaveNotice] = useState<string | null>(null);
  const [isSavingRequirements, setIsSavingRequirements] = useState<boolean>(false);

  const reload = useCallback(async () => {
    if (!uid.trim()) {
      setWorkflow(null);
      setError("Onboarding-ID fehlt.");
      setIsLoading(false);
      return;
    }

    setIsLoading(true);
    setError(null);
    setAuditLogError(null);

    try {
      const workflowData = await getWorkflowByUid(uid);
      setWorkflow(workflowData);

      if (capabilities.hasHrRole || capabilities.hasManagerRole || capabilities.canManageAdminConfiguration) {
        setIsLoadingAuditLog(true);

        try {
          const auditLogData = await getWorkflowAuditLog(uid);
          setAuditEntries(auditLogData);
        } catch (auditError) {
          const message = auditError instanceof Error ? auditError.message : "Audit-Log konnte nicht geladen werden.";
          setAuditLogError(message);
          setAuditEntries([]);
        } finally {
          setIsLoadingAuditLog(false);
        }
      } else {
        setAuditEntries([]);
        setIsLoadingAuditLog(false);
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : "Onboarding-Vorgang konnte nicht geladen werden.";
      setError(message);
      setWorkflow(null);
      setAuditEntries([]);
      setIsLoadingAuditLog(false);
    } finally {
      setIsLoading(false);
    }
  }, [capabilities.canManageAdminConfiguration, capabilities.hasHrRole, capabilities.hasManagerRole, uid]);

  useEffect(() => {
    void reload();
  }, [reload]);

  useEffect(() => {
    if (!workflow) {
      setRequirementSelections({});
      return;
    }

    setRequirementSelections(buildRequirementSelections(workflow.requirements));
  }, [workflow]);

  const sortedTasks = useMemo(() => {
    if (!workflow) {
      return [] as WorkflowTask[];
    }

    return workflow.tasks.slice().sort((left, right) => left.sortOrder - right.sortOrder || left.id - right.id);
  }, [workflow]);

  const activeAreaNames = useMemo(() => {
    if (!workflow) {
      return [] as ProcessAreaName[];
    }

    return workflow.taskAreas
      .filter((area) => area.isCurrentArea)
      .map((area) => area.name)
      .sort((left, right) => left.localeCompare(right, "de"));
  }, [workflow]);
  const activeTaskCount = workflow?.taskMetrics.overall.activeCount ?? 0;

  const currentTask = useMemo(() => findCurrentTask(sortedTasks), [sortedTasks]);
  const regularEditingText = useMemo(() => (workflow ? toRegularEditingLabel(workflow) : "-"), [workflow]);
  const usesAdminOverride = capabilities.canManageAdminConfiguration && !capabilities.canCreateWorkflow;
  const canEditSupervisorRequirements = useMemo(() => {
    return workflow?.workflowStatus === "waiting_for_supervisor" && capabilities.canManageAdminConfiguration;
  }, [capabilities.canManageAdminConfiguration, workflow?.workflowStatus]);
  const canSaveSupervisorRequirements = useMemo(() => {
    if (!workflow || !canEditSupervisorRequirements || isSavingRequirements) {
      return false;
    }
    return true;
  }, [canEditSupervisorRequirements, isSavingRequirements, workflow]);

  const currentArea = useMemo(() => (workflow ? toPhaseOwnerArea(workflow) : "-"), [workflow]);

  const currentOwnerText = useMemo(() => {
    if (!workflow) {
      return "-";
    }

    if (isWorkflowTerminalStatus(workflow.workflowStatus)) {
      return "Keine offene Aufgabe";
    }

    if (isDepartmentWorkflowPhase(workflow.workflowStatus)) {
      if (activeAreaNames.length > 1) {
        return `${activeAreaNames.length} Fachbereiche parallel`;
      }

      if (activeAreaNames.length === 1 && activeTaskCount > 1) {
        return `${activeAreaNames[0]} (${activeTaskCount} Aufgaben)`;
      }
    }

    if (workflow.workflowStatus === "draft" && activeAreaNames.length === 1 && activeTaskCount > 1) {
      return `${activeAreaNames[0]} (${activeTaskCount} Aufgaben)`;
    }

    if (currentTask) {
      if (isReaderOnlyView) {
        return inferAreaFromTask(currentTask) ?? currentArea;
      }

      const responsibility = getResponsibleResponsibilityLabel(currentTask);
      const user = getResponsibleUserLabel(currentTask);
      if (user === "Direkte Personenzuordnung ausgeblendet") {
        return responsibility;
      }

      return `${responsibility} (${user})`;
    }

    return currentArea;
  }, [activeAreaNames, activeTaskCount, currentArea, currentTask, isReaderOnlyView, workflow]);

  const nextActionText = useMemo(() => {
    if (!workflow) {
      return "-";
    }

    if (isWorkflowTerminalStatus(workflow.workflowStatus)) {
      return "Der Vorgang ist abgeschlossen";
    }

    if (isDepartmentWorkflowPhase(workflow.workflowStatus) && activeAreaNames.length > 1) {
      return workflow.workflowStatus === "in_progress"
        ? "Aufgaben parallel in mehreren Fachbereichen weiterbearbeiten"
        : "Offene Aufgaben parallel in mehreren Fachbereichen starten";
    }

    if (workflow.workflowStatus === "draft" && activeTaskCount > 1) {
      return "HR-Startaufgaben abschließen";
    }

    if (currentTask) {
      return `${toTaskDisplayTitle(currentTask)} (${inferAreaFromTask(currentTask) ?? "Bereich"})`;
    }

    if (workflow.workflowStatus === "draft") {
      return "HR-Startaufgaben bearbeiten";
    }

    if (workflow.workflowStatus === "waiting_for_supervisor") {
      return "Anforderungen durch Abteilungsleitung abschließen";
    }

    if (workflow.workflowStatus === "waiting_for_department") {
      return "Offene Aufgaben in den Fachbereichen starten";
    }

    if (workflow.workflowStatus === "in_progress") {
      return "Laufende Fachbereichsaufgaben weiterbearbeiten";
    }

    return "Nächsten Prozessschritt prüfen";
  }, [activeAreaNames.length, activeTaskCount, currentTask, workflow]);

  const handleTaskStatusChange = useCallback(
    async (taskId: number, status: VisibleTaskStatus, currentStatus: WorkflowTask["status"]) => {
      const nextStatus = mapVisibleTaskStatusToWorkflowStatus(status, currentStatus);
      if (nextStatus === currentStatus) {
        return;
      }

      setSavingTaskIds((current) => ({ ...current, [taskId]: true }));
      setTaskNotice(null);
      setTaskError(null);

      try {
        await updateTaskStatusApi(taskId, nextStatus);
        await reload();
        setTaskNotice("Aufgabenstatus wurde gespeichert.");
      } catch (err) {
        const message = err instanceof Error ? err.message : "Aufgabenstatus konnte nicht aktualisiert werden.";
        setTaskError(message);
      } finally {
        setSavingTaskIds((current) => ({ ...current, [taskId]: false }));
      }
    },
    [reload]
  );

  const handleCommentDraftChange = useCallback((taskId: number, value: string) => {
    setCommentDrafts((current) => ({ ...current, [taskId]: value }));
  }, []);

  const handleTaskCommentSubmit = useCallback(
    async (taskId: number) => {
      const draft = (commentDrafts[taskId] ?? "").trim();
      if (!draft) {
        return;
      }

      setSavingCommentTaskIds((current) => ({ ...current, [taskId]: true }));
      setCommentFeedbackTaskId(taskId);
      setCommentFeedbackMessage(null);
      setTaskError(null);
      setTaskNotice(null);

      try {
        await addTaskCommentApi(taskId, draft);
        await reload();
        setCommentDrafts((current) => ({ ...current, [taskId]: "" }));
        setCommentFeedbackMessage("Kommentar wurde gespeichert.");
      } catch (err) {
        const message = err instanceof Error ? err.message : "Kommentar konnte nicht gespeichert werden.";
        setCommentFeedbackMessage(message);
      } finally {
        setSavingCommentTaskIds((current) => ({ ...current, [taskId]: false }));
      }
    },
    [commentDrafts, reload]
  );

  const setRequirementBoolean = useCallback(
    (requirementId: number, value: boolean | null) => {
      if (!workflow) {
        return;
      }

      setRequirementSelections((current) =>
        applyRequirementBooleanEditorSelection(workflow.requirements, current, requirementId, value)
      );
    },
    [workflow]
  );

  const setRequirementText = useCallback((requirementId: number, value: string) => {
    setRequirementSelections((current) => ({
      ...current,
      [requirementId]: {
        ...(current[requirementId] ?? createEmptyRequirementSelection()),
        valueText: value,
      },
    }));
  }, []);

  const setRequirementSelectedOption = useCallback((requirementId: number, optionId: number | null) => {
    if (!workflow) {
      return;
    }

    setRequirementSelections((current) =>
      applyRequirementSingleSelectEditorSelection(workflow.requirements, current, requirementId, optionId)
    );
  }, [workflow]);

  const toggleRequirementSelectedOption = useCallback((requirementId: number, optionId: number) => {
    setRequirementSelections((current) => {
      const existing = current[requirementId] ?? createEmptyRequirementSelection();
      const isActive = existing.selectedOptionIds.includes(optionId);

      return {
        ...current,
        [requirementId]: {
          ...existing,
          selectedOptionIds: isActive
            ? existing.selectedOptionIds.filter((id) => id !== optionId)
            : [...existing.selectedOptionIds, optionId],
        },
      };
    });
  }, []);

  const handleRequirementSave = useCallback(async () => {
    if (!workflow || !canEditSupervisorRequirements) {
      return;
    }

    setIsSavingRequirements(true);
    setRequirementsSaveError(null);
    setRequirementsSaveNotice(null);

    try {
      await updateWorkflowSupervisorStep(
        workflow.uid,
        toRequirementSelectionPayload(workflow.requirements, requirementSelections)
      );
      await reload();
      setRequirementsSaveNotice("Anforderungen wurden per Admin-Override gespeichert.");
    } catch (err) {
      const message = err instanceof Error ? err.message : "Anforderungen konnten nicht gespeichert werden.";
      setRequirementsSaveError(message);
    } finally {
      setIsSavingRequirements(false);
    }
  }, [canEditSupervisorRequirements, reload, requirementSelections, workflow]);

  const processSteps = useMemo<ProcessStep[]>(() => {
    if (!workflow) {
      return [] as ProcessStep[];
    }

    return buildProcessSteps(workflow);
  }, [workflow]);

  const tasksByArea = useMemo(
    () => (workflow ? buildTasksByArea(sortedTasks, workflow.taskAreas) : []),
    [sortedTasks, workflow]
  );

  return (
    <main className="onboarding-shell">
      <div className="page-container">
        <PageHeader
          title="Onboarding-Prozess"
          description="Zentraler Überblick über Person, Prozessstand, Zuständigkeiten und offene Aufgaben."
        />

        {isLoading ? <LoadingState title="Onboarding-Vorgang wird geladen..." /> : null}

        {!isLoading && error ? (
          <EmptyState
            title="Onboarding-Vorgang konnte nicht geladen werden."
            description={error}
            actionLabel="Erneut versuchen"
            onAction={reload}
          />
        ) : null}

        {!isLoading && !error && workflow ? (
          <>
            <WorkflowHeaderPanel
              workflow={workflow}
              regularEditingText={regularEditingText}
              nextActionText={nextActionText}
              currentArea={currentArea}
              currentOwnerText={currentOwnerText}
              canManageAdminConfiguration={capabilities.canManageAdminConfiguration}
            />

            <WorkflowProgressSection processSteps={processSteps} />

            <WorkflowRequirementsPanel
              workflow={workflow}
              canEditSupervisorRequirements={canEditSupervisorRequirements}
              requirementSelections={requirementSelections}
              requirementsSaveError={requirementsSaveError}
              requirementsSaveNotice={requirementsSaveNotice}
              isSavingRequirements={isSavingRequirements}
              canSaveSupervisorRequirements={canSaveSupervisorRequirements}
              onToggleBoolean={setRequirementBoolean}
              onTextChange={setRequirementText}
              onSelectOption={setRequirementSelectedOption}
              onToggleMultiOption={toggleRequirementSelectedOption}
              onSave={handleRequirementSave}
            />

            <WorkflowTaskAreasSection
              tasksByArea={tasksByArea}
              taskError={taskError}
              taskNotice={taskNotice}
              savingTaskIds={savingTaskIds}
              commentDrafts={commentDrafts}
              savingCommentTaskIds={savingCommentTaskIds}
              commentFeedbackTaskId={commentFeedbackTaskId}
              commentFeedbackMessage={commentFeedbackMessage}
              usesAdminOverride={usesAdminOverride}
              canManageAdminConfiguration={capabilities.canManageAdminConfiguration}
              isReaderOnlyView={isReaderOnlyView}
              onTaskStatusChange={handleTaskStatusChange}
              onCommentDraftChange={handleCommentDraftChange}
              onTaskCommentSubmit={handleTaskCommentSubmit}
            />

            {capabilities.hasHrRole || capabilities.hasManagerRole || capabilities.canManageAdminConfiguration ? (
              <WorkflowAuditLog
                entries={auditEntries}
                isLoading={isLoadingAuditLog}
                error={auditLogError}
              />
            ) : null}
          </>
        ) : null}
      </div>
    </main>
  );
}
