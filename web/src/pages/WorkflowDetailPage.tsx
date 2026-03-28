// Detailansicht fuer einen einzelnen Vorgang mit Prozessstand, Antworten und Aufgaben.
import { useCallback, useEffect, useMemo, useState } from "react";
import { useParams } from "react-router-dom";
import { useCurrentUser } from "../auth/useCurrentUser";
import WorkflowAuditLog from "../components/workflow-detail/WorkflowAuditLog";
import WorkflowHeaderPanel from "../components/workflow-detail/WorkflowHeaderPanel";
import WorkflowLinksPanel from "../components/workflow-detail/WorkflowLinksPanel";
import WorkflowManagementPanel from "../components/workflow-detail/WorkflowManagementPanel";
import WorkflowNotificationsPanel from "../components/workflow-detail/WorkflowNotificationsPanel";
import WorkflowProgressSection from "../components/workflow-detail/WorkflowProgressSection";
import WorkflowRequirementsPanel from "../components/workflow-detail/WorkflowRequirementsPanel";
import WorkflowTaskAreasSection from "../components/workflow-detail/WorkflowTaskAreasSection";
import {
  useAddTaskComment,
  useUpdateSupervisorStep,
  useUpdateTaskStatus,
} from "../services/mutations/workflowMutations";
import {
  useWorkflowAuditLog,
  useWorkflowDetail,
  useWorkflowTasks,
} from "../services/queries/workflowQueries";
import {
  buildProcessSteps,
  buildTasksByArea,
  findCurrentTask,
  hasSupervisorStep,
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
import type { RequirementSelectionState, WorkflowDetail, WorkflowTask } from "../types/workflow";
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
  const canViewAuditLog =
    capabilities.hasHrRole || capabilities.hasManagerRole || capabilities.canManageAdminConfiguration;

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
  const workflowDetailQuery = useWorkflowDetail(uid);
  const workflowTasksQuery = useWorkflowTasks(uid);
  const workflowAuditLogQuery = useWorkflowAuditLog(uid, 50, 0, canViewAuditLog);
  const updateTaskStatusMutation = useUpdateTaskStatus(uid);
  const addTaskCommentMutation = useAddTaskComment(uid);
  const updateSupervisorStepMutation = useUpdateSupervisorStep(uid);
  const workflow = useMemo<WorkflowDetail | null>(() => {
    const baseWorkflow = workflowDetailQuery.data ?? null;
    if (!baseWorkflow) {
      return null;
    }

    if (!workflowTasksQuery.data) {
      return baseWorkflow;
    }

    return {
      ...baseWorkflow,
      tasks: workflowTasksQuery.data,
    };
  }, [workflowDetailQuery.data, workflowTasksQuery.data]);
  const isLoading = workflowDetailQuery.isLoading;
  const error = !uid.trim()
    ? "Workflow-ID fehlt."
    : workflowDetailQuery.error instanceof Error
      ? workflowDetailQuery.error.message
      : workflowDetailQuery.error
        ? "Vorgang konnte nicht geladen werden."
        : null;
  const auditEntries = workflowAuditLogQuery.data ?? [];
  const auditLogError = workflowAuditLogQuery.error instanceof Error
    ? workflowAuditLogQuery.error.message
    : workflowAuditLogQuery.error
      ? "Audit-Log konnte nicht geladen werden."
      : null;
  const isLoadingAuditLog = workflowAuditLogQuery.isLoading;

  const reload = useCallback(async () => {
    await Promise.all([
      workflowDetailQuery.refetch(),
      workflowTasksQuery.refetch(),
      canViewAuditLog ? workflowAuditLogQuery.refetch() : Promise.resolve(),
    ]);
  }, [canViewAuditLog, workflowAuditLogQuery, workflowDetailQuery, workflowTasksQuery]);

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
  const usesAdminOverride = capabilities.hasAdminRole && !capabilities.hasHrRole && !capabilities.hasManagerRole;
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
        await updateTaskStatusMutation.mutateAsync({ taskId, status: nextStatus });
        setTaskNotice("Aufgabenstatus wurde gespeichert.");
      } catch (err) {
        const message = err instanceof Error ? err.message : "Aufgabenstatus konnte nicht aktualisiert werden.";
        setTaskError(message);
      } finally {
        setSavingTaskIds((current) => ({ ...current, [taskId]: false }));
      }
    },
    [updateTaskStatusMutation]
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
        await addTaskCommentMutation.mutateAsync({ taskId, text: draft });
        setCommentDrafts((current) => ({ ...current, [taskId]: "" }));
        setCommentFeedbackMessage("Kommentar wurde gespeichert.");
      } catch (err) {
        const message = err instanceof Error ? err.message : "Kommentar konnte nicht gespeichert werden.";
        setCommentFeedbackMessage(message);
      } finally {
        setSavingCommentTaskIds((current) => ({ ...current, [taskId]: false }));
      }
    },
    [addTaskCommentMutation, commentDrafts]
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
      await updateSupervisorStepMutation.mutateAsync(
        toRequirementSelectionPayload(workflow.requirements, requirementSelections)
      );
      setRequirementsSaveNotice("Anforderungen wurden per Admin-Override gespeichert.");
    } catch (err) {
      const message = err instanceof Error ? err.message : "Anforderungen konnten nicht gespeichert werden.";
      setRequirementsSaveError(message);
    } finally {
      setIsSavingRequirements(false);
    }
  }, [canEditSupervisorRequirements, requirementSelections, updateSupervisorStepMutation, workflow]);

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
    <main className="app-shell">
      <div className="page-container">
        <PageHeader
          title="Vorgangsdetails"
          description="Zentraler Überblick über Person, Prozesstyp, Prozessstand, Zuständigkeiten und offene Aufgaben."
        />

        {isLoading ? <LoadingState title="Vorgang wird geladen..." /> : null}

        {!isLoading && error ? (
          <EmptyState
            title="Vorgang konnte nicht geladen werden."
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

            {!hasSupervisorStep(workflow) && workflow.workflowStatus !== "waiting_for_supervisor" ? (
              <section className="panel panel-muted">
                <div className="panel-head">
                  <h2>Freigabeschritt</h2>
                  <p>Dieser Prozesstyp hat keinen separaten Schritt für die Abteilungsleitung.</p>
                </div>
              </section>
            ) : null}

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

            <section className="workflow-detail-secondary-stack">
              <WorkflowProgressSection processSteps={processSteps} processTypeName={workflow.processType.name} />

              {capabilities.hasHrRole || capabilities.canManageAdminConfiguration ? (
                <WorkflowLinksPanel uid={uid} workflow={workflow} />
              ) : null}

              <WorkflowManagementPanel
                uid={uid}
                workflow={workflow}
                capabilities={capabilities}
              />

              {capabilities.canManageAdminConfiguration ? (
                <WorkflowNotificationsPanel notifications={workflow.notifications} />
              ) : null}

              {capabilities.hasHrRole || capabilities.hasManagerRole || capabilities.canManageAdminConfiguration ? (
                <WorkflowAuditLog
                  entries={auditEntries}
                  isLoading={isLoadingAuditLog}
                  error={auditLogError}
                />
              ) : null}
            </section>
          </>
        ) : null}
      </div>
    </main>
  );
}
