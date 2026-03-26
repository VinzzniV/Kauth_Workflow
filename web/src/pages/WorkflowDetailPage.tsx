// Detailansicht fuer einen einzelnen Vorgang mit Prozessstand, Antworten und Aufgaben.
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
import {
  addTaskComment as addTaskCommentApi,
  getWorkflowAuditLog,
  getWorkflowByUid,
  getWorkflowLinks,
  createWorkflowLink as createWorkflowLinkApi,
  deleteWorkflowLink as deleteWorkflowLinkApi,
  findLinkableWorkflows,
  updateTaskStatus as updateTaskStatusApi,
  updateWorkflowSupervisorStep,
} from "../services/lifecycleApi";
import type {
  RequirementSelectionState,
  WorkflowAuditEntry,
  WorkflowDetail,
  WorkflowLink,
  LinkableWorkflow,
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
  const [workflowLinks, setWorkflowLinks] = useState<WorkflowLink[]>([]);
  const [linkableWorkflows, setLinkableWorkflows] = useState<LinkableWorkflow[]>([]);
  const [showLinkDialog, setShowLinkDialog] = useState(false);
  const [linkError, setLinkError] = useState<string | null>(null);

  const reload = useCallback(async () => {
    if (!uid.trim()) {
      setWorkflow(null);
      setError("Workflow-ID fehlt.");
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

      // Verknüpfte Workflows laden
      try {
        const links = await getWorkflowLinks(uid);
        setWorkflowLinks(links);
      } catch {
        setWorkflowLinks([]);
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : "Vorgang konnte nicht geladen werden.";
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

  const handleOpenLinkDialog = useCallback(async () => {
    if (!workflow) return;
    setShowLinkDialog(true);
    setLinkError(null);
    try {
      const linkable = await findLinkableWorkflows(workflow.employeeNumber, workflow.uid);
      setLinkableWorkflows(linkable);
    } catch {
      setLinkableWorkflows([]);
    }
  }, [workflow]);

  const handleCreateLink = useCallback(async (sourceUid: string) => {
    if (!uid.trim()) return;
    setLinkError(null);
    try {
      await createWorkflowLinkApi(uid, { sourceWorkflowUid: sourceUid, linkType: "derived_from" });
      const links = await getWorkflowLinks(uid);
      setWorkflowLinks(links);
      setShowLinkDialog(false);
    } catch (err) {
      setLinkError(err instanceof Error ? err.message : "Verknüpfung konnte nicht erstellt werden.");
    }
  }, [uid]);

  const handleDeleteLink = useCallback(async (linkId: number) => {
    if (!uid.trim()) return;
    try {
      await deleteWorkflowLinkApi(uid, linkId);
      setWorkflowLinks((prev) => prev.filter((l) => l.id !== linkId));
    } catch {
      // silent
    }
  }, [uid]);

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

            <WorkflowProgressSection processSteps={processSteps} processTypeName={workflow.processType.name} />

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

            {capabilities.hasHrRole || capabilities.canManageAdminConfiguration ? (
              <section className="panel">
                <div className="panel-head">
                  <h2>Verknüpfte Vorgänge</h2>
                  <p>Beziehungen zu anderen Workflows desselben Mitarbeiters.</p>
                </div>
                <div className="panel-body">
                  {workflowLinks.length === 0 && !showLinkDialog ? (
                    <p className="text-muted">Keine Verknüpfungen vorhanden.</p>
                  ) : null}

                  {workflowLinks.length > 0 ? (
                    <table className="table">
                      <thead>
                        <tr>
                          <th>Prozesstyp</th>
                          <th>Person</th>
                          <th>Status</th>
                          <th>Beziehung</th>
                          <th>Erstellt</th>
                          <th></th>
                        </tr>
                      </thead>
                      <tbody>
                        {workflowLinks.map((link) => (
                          <tr key={link.id}>
                            <td>{link.linkedWorkflowProcessType.name}</td>
                            <td>{link.linkedWorkflowFirstName} {link.linkedWorkflowLastName}</td>
                            <td>
                              <span className={`badge badge--${link.linkedWorkflowStatus === "completed" ? "success" : "default"}`}>
                                {link.linkedWorkflowStatus}
                              </span>
                            </td>
                            <td>{link.linkType === "derived_from" ? "Abgeleitet von" : link.linkType === "supersedes" ? "Ersetzt" : "Verwandt"}</td>
                            <td>{new Date(link.createdAt).toLocaleDateString("de-DE")}</td>
                            <td>
                              <button className="btn btn-sm btn-outline" onClick={() => void handleDeleteLink(link.id)}>
                                Entfernen
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  ) : null}

                  {showLinkDialog ? (
                    <div style={{ marginTop: "1rem" }}>
                      {linkError ? <p className="text-error">{linkError}</p> : null}
                      {linkableWorkflows.length === 0 ? (
                        <p className="text-muted">Keine verknüpfbaren Vorgänge für diese Personalnummer gefunden.</p>
                      ) : (
                        <table className="table">
                          <thead>
                            <tr>
                              <th>Prozesstyp</th>
                              <th>Person</th>
                              <th>Status</th>
                              <th>Erstellt</th>
                              <th></th>
                            </tr>
                          </thead>
                          <tbody>
                            {linkableWorkflows.map((lw) => (
                              <tr key={lw.uid}>
                                <td>{lw.processType.name}</td>
                                <td>{lw.firstName} {lw.lastName}</td>
                                <td>{lw.workflowStatus}</td>
                                <td>{new Date(lw.createdAt).toLocaleDateString("de-DE")}</td>
                                <td>
                                  <button className="btn btn-sm btn-primary" onClick={() => void handleCreateLink(lw.uid)}>
                                    Verknüpfen
                                  </button>
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      )}
                      <button className="btn btn-sm btn-outline" style={{ marginTop: "0.5rem" }} onClick={() => setShowLinkDialog(false)}>
                        Abbrechen
                      </button>
                    </div>
                  ) : (
                    <button className="btn btn-sm btn-outline" style={{ marginTop: "0.5rem" }} onClick={() => void handleOpenLinkDialog()}>
                      Vorgang verknüpfen
                    </button>
                  )}
                </div>
              </section>
            ) : null}

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
