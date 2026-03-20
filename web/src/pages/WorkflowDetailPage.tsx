// Detailansicht fuer einen einzelnen Onboarding-Fall mit Prozessstand, Antworten und Aufgaben.
import { useCallback, useEffect, useMemo, useState } from "react";
import { useParams } from "react-router-dom";
import { useCurrentUser } from "../auth/useCurrentUser";
import WorkflowRequirementsPanel from "../components/workflow-detail/WorkflowRequirementsPanel";
import WorkflowTaskAreasSection from "../components/workflow-detail/WorkflowTaskAreasSection";
import {
  buildProcessSteps,
  buildTasksByArea,
  findCurrentTask,
  formatDate,
  inferAreaFromTask,
  isDepartmentWorkflowPhase,
  toPhaseOwnerArea,
  toProcessStepClass,
  toReadAccessLabel,
  toRegularEditingLabel,
  toRuntimeStatusLabel,
  toTaskDisplayTitle,
  type ProcessAreaName,
  type ProcessStep,
} from "../components/workflow-detail/workflowDetailModel";
import EmptyState from "../components/feedback/EmptyState";
import LoadingState from "../components/feedback/LoadingState";
import PageHeader from "../components/layout/PageHeader";
import {
  getWorkflowByUid,
  updateTaskStatus as updateTaskStatusApi,
  updateWorkflowSupervisorStep,
} from "../services/onboardingApi";
import type {
  RequirementSelectionState,
  WorkflowDetail,
  WorkflowTask,
} from "../types/workflow";
import {
  applyRequirementBooleanSelection,
  applyRequirementSingleSelectSelection,
  buildRequirementSelections,
  createEmptyRequirementSelection,
  toRequirementSelectionPayload,
  validateRequirementSelections,
} from "../utils/requirements";
import { getResponsibleResponsibilityLabel, getResponsibleUserLabel } from "../utils/taskAssignment";
import {
  mapVisibleTaskStatusToWorkflowStatus,
  type VisibleTaskStatus,
} from "../utils/taskStatus";
import {
  getWorkflowLegacyStatusLabel,
  getWorkflowLegacyStatusPillClass,
  isWorkflowTerminalStatus,
} from "../utils/workflowStatus";

export default function WorkflowDetailPage() {
  const { uid = "" } = useParams<{ uid: string }>();
  const { capabilities } = useCurrentUser();
  const isReaderOnlyView =
    capabilities.hasReaderRole && !capabilities.hasProcessActorRole && !capabilities.canManageAdminConfiguration;

  const [workflow, setWorkflow] = useState<WorkflowDetail | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [taskNotice, setTaskNotice] = useState<string | null>(null);
  const [taskError, setTaskError] = useState<string | null>(null);
  const [savingTaskIds, setSavingTaskIds] = useState<Record<number, boolean>>({});
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

    try {
      const workflowData = await getWorkflowByUid(uid);
      setWorkflow(workflowData);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Onboarding-Vorgang konnte nicht geladen werden.";
      setError(message);
      setWorkflow(null);
    } finally {
      setIsLoading(false);
    }
  }, [uid]);

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
  const readAccessText = useMemo(() => (workflow ? toReadAccessLabel(workflow) : "-"), [workflow]);
  const usesAdminOverride = capabilities.canManageAdminConfiguration && !capabilities.canCreateWorkflow;
  const canEditSupervisorRequirements = useMemo(() => {
    return workflow?.workflowStatus === "waiting_for_supervisor" && capabilities.canManageAdminConfiguration;
  }, [capabilities.canManageAdminConfiguration, workflow?.workflowStatus]);
  const canSaveSupervisorRequirements = useMemo(() => {
    if (!workflow || !canEditSupervisorRequirements || isSavingRequirements) {
      return false;
    }

    return validateRequirementSelections(workflow.requirements, requirementSelections) === null;
  }, [canEditSupervisorRequirements, isSavingRequirements, requirementSelections, workflow]);

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
        return inferAreaFromTask(currentTask) ?? regularEditingText;
      }

      const responsibility = getResponsibleResponsibilityLabel(currentTask);
      const user = getResponsibleUserLabel(currentTask);
      if (user === "Direkte Personenzuordnung ausgeblendet") {
        return responsibility;
      }

      return `${responsibility} (${user})`;
    }

    return regularEditingText;
  }, [activeAreaNames, activeTaskCount, currentTask, isReaderOnlyView, regularEditingText, workflow]);

  const nextActionText = useMemo(() => {
    if (!workflow) {
      return "-";
    }

    if (workflow.workflowStatus === "completed") {
      return "Der Vorgang ist abgeschlossen";
    }

    if (workflow.workflowStatus === "cancelled") {
      return "Der Vorgang wurde abgebrochen";
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

  const setRequirementBoolean = useCallback(
    (requirementId: number, value: boolean | null) => {
      if (!workflow) {
        return;
      }

      setRequirementSelections((current) =>
        applyRequirementBooleanSelection(workflow.requirements, current, requirementId, value)
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
      applyRequirementSingleSelectSelection(workflow.requirements, current, requirementId, optionId)
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

    const validationError = validateRequirementSelections(workflow.requirements, requirementSelections);
    if (validationError) {
      setRequirementsSaveError(validationError);
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
            <section className="panel panel-intro">
              <div className="panel-head">
                <h2>
                  {workflow.firstName} {workflow.lastName}
                </h2>
                <p>
                  Neue Person in {workflow.departmentName} | {workflow.roleName}
                </p>
              </div>

              <div className="action-row">
                <span className={`status-pill ${getWorkflowLegacyStatusPillClass(workflow.workflowStatus)}`}>
                  Status: {getWorkflowLegacyStatusLabel(workflow.workflowStatus)}
                </span>
                <span className="chip">Prozessstand: {toRuntimeStatusLabel(workflow.workflowStatus)}</span>
              </div>

              <p className="panel-note">
                Reguläre Bearbeitung: {regularEditingText} | Lesend: {readAccessText}
                {capabilities.canManageAdminConfiguration ? " | Admin kann bei Bedarf eingreifen." : ""}
              </p>

              <div className="next-action-callout" role="status" aria-live="polite">
                <p className="next-action-label">Nächste nötige Aktion</p>
                <p className="next-action-text">{nextActionText}</p>
              </div>

              <div className="workflow-detail-summary-grid">
                <article className="workflow-detail-kpi">
                  <p className="workflow-detail-kpi-label">Abteilung</p>
                  <p className="workflow-detail-kpi-value">{workflow.departmentName}</p>
                  <p className="workflow-detail-kpi-note">Geplanter Einsatzbereich der neuen Person.</p>
                </article>

                <article className="workflow-detail-kpi">
                  <p className="workflow-detail-kpi-label">Rolle / Position</p>
                  <p className="workflow-detail-kpi-value">{workflow.roleName}</p>
                  <p className="workflow-detail-kpi-note">Hinterlegte Zielposition für das Onboarding.</p>
                </article>

                <article className="workflow-detail-kpi">
                  <p className="workflow-detail-kpi-label">Startdatum</p>
                  <p className="workflow-detail-kpi-value">{formatDate(workflow.createdAt)}</p>
                  <p className="workflow-detail-kpi-note">Onboarding angelegt durch HR.</p>
                </article>

                <article className="workflow-detail-kpi">
                  <p className="workflow-detail-kpi-label">Aktueller Status</p>
                  <p className="workflow-detail-kpi-value">{getWorkflowLegacyStatusLabel(workflow.workflowStatus)}</p>
                  <p className="workflow-detail-kpi-note">Gesamtstand des Vorgangs.</p>
                </article>

                <article className="workflow-detail-kpi">
                  <p className="workflow-detail-kpi-label">Aktuell zuständiger Bereich</p>
                  <p className="workflow-detail-kpi-value">{currentArea}</p>
                  <p className="workflow-detail-kpi-note">Wer diese Workflow-Phase regulär bearbeitet.</p>
                </article>

                <article className="workflow-detail-kpi">
                  <p className="workflow-detail-kpi-label">Aktuell dran</p>
                  <p className="workflow-detail-kpi-value">{currentOwnerText}</p>
                  <p className="workflow-detail-kpi-note">Konkrete Zuständigkeit für die aktuell offenen Aufgaben.</p>
                </article>

                <article className="workflow-detail-kpi">
                  <p className="workflow-detail-kpi-label">Offene Aufgaben</p>
                  <p className="workflow-detail-kpi-value">{workflow.taskMetrics.overall.activeCount}</p>
                  <p className="workflow-detail-kpi-note">
                    Offen: {workflow.taskMetrics.overall.openCount} | In Bearbeitung: {workflow.taskMetrics.overall.inProgressCount} | Erledigt: {workflow.taskMetrics.overall.completedCount}
                  </p>
                </article>
              </div>
            </section>

            <section className="panel panel-muted">
              <div className="panel-head">
                <h2>Prozessfortschritt</h2>
                <p>So weit ist der Onboarding-Vorgang aktuell.</p>
              </div>

              <ol className="process-step-list" aria-label="Fortschritt Onboarding">
                {processSteps.map((step) => (
                  <li key={step.key} className="process-step-item">
                    <span className={`process-step-state ${toProcessStepClass(step.state)}`} aria-hidden="true" />
                    <div className="process-step-content">
                      <p className="process-step-title">{step.title}</p>
                      <p className="process-step-detail">{step.detail}</p>
                    </div>
                  </li>
                ))}
              </ol>
            </section>

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
              usesAdminOverride={usesAdminOverride}
              canManageAdminConfiguration={capabilities.canManageAdminConfiguration}
              isReaderOnlyView={isReaderOnlyView}
              onTaskStatusChange={handleTaskStatusChange}
            />
          </>
        ) : null}
      </div>
    </main>
  );
}
