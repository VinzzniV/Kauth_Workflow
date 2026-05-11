// Detailansicht fuer einen einzelnen Vorgang mit Antworten, Aufgaben und Verwaltungsinformationen.
import { useCallback, useMemo } from "react";
import { Link, useParams } from "react-router-dom";
import { useCurrentUser } from "../auth/useCurrentUser";
import WorkflowAuditLog from "../components/workflow-detail/WorkflowAuditLog";
import WorkflowHeaderPanel from "../components/workflow-detail/WorkflowHeaderPanel";
import WorkflowLinksPanel from "../components/workflow-detail/WorkflowLinksPanel";
import WorkflowManagementPanel from "../components/workflow-detail/WorkflowManagementPanel";
import WorkflowNotificationsPanel from "../components/workflow-detail/WorkflowNotificationsPanel";
import WorkflowRequirementsPanel from "../components/workflow-detail/WorkflowRequirementsPanel";
import WorkflowTaskAreasSection from "../components/workflow-detail/WorkflowTaskAreasSection";
import {
  useWorkflowAuditLog,
  useWorkflowDetail,
  useWorkflowTasks,
} from "../services/queries/workflowQueries";
import {
  buildTasksByArea,
  findCurrentTask,
  inferAreaFromTask,
  isDepartmentWorkflowPhase,
  toPhaseOwnerArea,
  toRegularEditingLabel,
  toTaskDisplayTitle,
  type ProcessAreaName,
} from "../components/workflow-detail/workflowDetailModel";
import { AppErrorBoundary } from "../components/feedback/AppErrorBoundary";
import EmptyState from "../components/feedback/EmptyState";
import LoadingState from "../components/feedback/LoadingState";
import PageHeader from "../components/layout/PageHeader";
import type { WorkflowDetail, WorkflowTask } from "../types/workflow";
import { getResponsibleResponsibilityLabel, getResponsibleUserLabel } from "../utils/taskAssignment";
import { isWorkflowTerminalStatus } from "../utils/workflowStatus";
import { useTaskInteraction } from "../hooks/useTaskInteraction";
import { useRequirementEditor } from "../hooks/useRequirementEditor";

export default function WorkflowDetailPage() {
  const { uid = "" } = useParams<{ uid: string }>();
  const { capabilities } = useCurrentUser();
  const isReaderOnlyView =
    capabilities.hasReaderRole && !capabilities.hasProcessActorRole && !capabilities.canManageAdminConfiguration;
  const canViewAuditLog =
    capabilities.hasHrRole || capabilities.hasManagerRole || capabilities.canManageAdminConfiguration;
  const {
    savingTaskIds,
    savingApprovalTaskIds,
    commentDrafts,
    savingCommentTaskIds,
    handleStatusChange,
    handleApprovalDecision,
    handleCommentDraftChange,
    handleTaskCommentSubmit,
  } = useTaskInteraction();
  const workflowDetailQuery = useWorkflowDetail(uid);
  const workflowTasksQuery = useWorkflowTasks(uid);
  const workflowAuditLogQuery = useWorkflowAuditLog(uid, 50, 0, canViewAuditLog);
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
    return workflow?.workflowStatus === "waiting_for_supervisor"
      && (capabilities.canAccessSupervisorStep || capabilities.canManageAdminConfiguration);
  }, [capabilities.canAccessSupervisorStep, capabilities.canManageAdminConfiguration, workflow?.workflowStatus]);
  const {
    requirementSelections,
    isSavingRequirements,
    canSaveSupervisorRequirements,
    setRequirementBoolean,
    setRequirementText,
    setRequirementSelectedOption,
    toggleRequirementSelectedOption,
    handleRequirementSave,
  } = useRequirementEditor({
    workflowUid: uid,
    workflow,
    canEditSupervisorRequirements,
  });

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
        return `${activeAreaNames.slice(0, 3).join(", ")}${activeAreaNames.length > 3 ? " ..." : ""}`;
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

  const tasksByArea = useMemo(
    () => (workflow ? buildTasksByArea(sortedTasks, workflow.taskAreas) : []),
    [sortedTasks, workflow]
  );

  const taskAreasEmptyStateDescription = useMemo(() => {
    if (!workflow) {
      return "Noch keine Aufgaben vorhanden.";
    }

    if (workflow.workflowStatus === "waiting_for_supervisor") {
      return "Noch keine Bereichsaufgaben vorhanden. Die Aufgaben werden erst erzeugt, wenn die zuständige Abteilungsleitung die Anforderungen abgeschlossen hat.";
    }

    if (workflow.workflowStatus === "draft") {
      return "Noch keine Bereichsaufgaben vorhanden. Der Vorgang muss erst gestartet und die Anforderungen müssen erfasst werden.";
    }

    return "Für diesen Vorgang sind aktuell keine Bereichsaufgaben vorhanden.";
  }, [workflow]);

  return (
    <main className="app-shell">
      <div className="page-container">
        <nav className="breadcrumb" aria-label="Breadcrumb">
          <Link to="/workflows">Laufende Vorgänge</Link>
          <span className="breadcrumb-separator" aria-hidden="true">/</span>
          <span>Vorgangsdetails</span>
        </nav>

        <PageHeader
          variant="detail"
          eyebrow={workflow?.workflowDefinition.name}
          title={
            workflow
              ? `${workflow.firstName} ${workflow.lastName}`
              : isLoading
                ? "Vorgang wird geladen …"
                : "Vorgangsdetails"
          }
          actions={
            !isLoading ? (
              <button type="button" className="btn btn-secondary" onClick={() => void reload()}>
                Aktualisieren
              </button>
            ) : undefined
          }
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
            />

            <WorkflowRequirementsPanel
              workflow={workflow}
              canEditSupervisorRequirements={canEditSupervisorRequirements}
              requirementSelections={requirementSelections}
              isSavingRequirements={isSavingRequirements}
              canSaveSupervisorRequirements={canSaveSupervisorRequirements}
              onToggleBoolean={setRequirementBoolean}
              onTextChange={setRequirementText}
              onSelectOption={setRequirementSelectedOption}
              onToggleMultiOption={toggleRequirementSelectedOption}
              onSave={handleRequirementSave}
            />

            <AppErrorBoundary scope="WorkflowDetailPage/tasks" inline>
              <WorkflowTaskAreasSection
                tasksByArea={tasksByArea}
                savingTaskIds={savingTaskIds}
                savingApprovalTaskIds={savingApprovalTaskIds}
                commentDrafts={commentDrafts}
                savingCommentTaskIds={savingCommentTaskIds}
                usesAdminOverride={usesAdminOverride}
                canManageAdminConfiguration={capabilities.canManageAdminConfiguration}
                isReaderOnlyView={isReaderOnlyView}
                onTaskStatusChange={(taskId, status, currentStatus) =>
                  handleStatusChange({ taskId, workflowUid: uid, status, currentStatus })
                }
                onTaskApprovalDecision={(taskId, approved) =>
                  handleApprovalDecision({ taskId, workflowUid: uid, approved })
                }
                onCommentDraftChange={handleCommentDraftChange}
                onTaskCommentSubmit={(taskId) => handleTaskCommentSubmit({ taskId, workflowUid: uid })}
                emptyStateDescription={taskAreasEmptyStateDescription}
              />
            </AppErrorBoundary>

            <AppErrorBoundary scope="WorkflowDetailPage/secondary" inline>
              <section className="workflow-detail-secondary-stack">
                <WorkflowLinksPanel uid={uid} />

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
            </AppErrorBoundary>
          </>
        ) : null}
      </div>
    </main>
  );
}
