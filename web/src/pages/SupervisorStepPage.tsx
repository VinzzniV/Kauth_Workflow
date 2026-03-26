// Ansicht fuer die Abteilungsleitung, um Anforderungen eines freigabepflichtigen Vorgangs zu bestaetigen oder zu ergaenzen.
import { useCallback, useEffect, useMemo, useState } from "react";
import { useCurrentUser } from "../auth/useCurrentUser";
import EmptyState from "../components/feedback/EmptyState";
import LoadingState from "../components/feedback/LoadingState";
import PageHeader from "../components/layout/PageHeader";
import RequirementsSelection from "../components/workflows/RequirementsSelection";
import {
  applyRequirementBooleanEditorSelection,
  applyRequirementSingleSelectEditorSelection,
} from "../utils/requirementEditor";
import {
  getSupervisorStepWorkflows,
  getWorkflowSupervisorStep,
  updateWorkflowSupervisorStep,
} from "../services/lifecycleApi";
import type {
  RequirementSelectionState,
  WorkflowRequirementSnapshot,
  WorkflowSummary,
} from "../types/workflow";
import {
  buildRequirementSelections,
  createEmptyRequirementSelection,
  toRequirementSelectionPayload,
} from "../utils/requirements";
import { formatDateTime } from "../utils/dateFormat";

export default function SupervisorStepPage() {
  const { capabilities } = useCurrentUser();
  const [assignedWorkflows, setAssignedWorkflows] = useState<WorkflowSummary[]>([]);
  const [queueLoading, setQueueLoading] = useState<boolean>(true);
  const [queueError, setQueueError] = useState<string | null>(null);

  const [selectedWorkflow, setSelectedWorkflow] = useState<WorkflowSummary | null>(null);
  const [requirements, setRequirements] = useState<WorkflowRequirementSnapshot[]>([]);
  const [selections, setSelections] = useState<Record<number, RequirementSelectionState>>({});
  const [isLoadingStep, setIsLoadingStep] = useState<boolean>(false);
  const [isSaving, setIsSaving] = useState<boolean>(false);
  const [stepError, setStepError] = useState<string | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saveSuccess, setSaveSuccess] = useState<string | null>(null);
  const usesAdminOverride = capabilities.canManageAdminConfiguration;

  // Zuerst wird die Queue der zugewiesenen Faelle geladen.
  const reloadAssignedWorkflows = useCallback(async () => {
    setQueueLoading(true);
    setQueueError(null);

    try {
      const workflows = await getSupervisorStepWorkflows();
      setAssignedWorkflows(workflows.filter((workflow) => workflow.workflowStatus === "waiting_for_supervisor"));
    } catch (err) {
      const message = err instanceof Error ? err.message : "Liste der Vorgänge konnte nicht geladen werden.";
      setQueueError(message);
      setAssignedWorkflows([]);
    } finally {
      setQueueLoading(false);
    }
  }, []);

  useEffect(() => {
    void reloadAssignedWorkflows();
  }, [reloadAssignedWorkflows]);

  // Danach laedt die Seite fuer einen ausgewaehlten Fall die konkreten Anforderungen.
  const loadSupervisorStep = useCallback(async (workflow: WorkflowSummary) => {
    setIsLoadingStep(true);
    setStepError(null);
    setSaveError(null);
    setSaveSuccess(null);

    try {
      const data = await getWorkflowSupervisorStep(workflow.uid);
      setRequirements(data);
      setSelections(buildRequirementSelections(data));
      setSelectedWorkflow(workflow);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Rückmeldung der Abteilungsleitung konnte nicht geladen werden.";
      setStepError(message);
      setRequirements([]);
      setSelections({});
      setSelectedWorkflow(null);
    } finally {
      setIsLoadingStep(false);
    }
  }, []);

  const setRequirementBoolean = useCallback(
    (requirementId: number, value: boolean | null) => {
      setSelections((current) =>
        applyRequirementBooleanEditorSelection(requirements, current, requirementId, value)
      );
    },
    [requirements]
  );

  const setRequirementText = useCallback((requirementId: number, value: string) => {
    setSelections((current) => ({
      ...current,
      [requirementId]: {
        ...(current[requirementId] ?? createEmptyRequirementSelection()),
        valueText: value,
      },
    }));
  }, []);

  const setRequirementSelectedOption = useCallback((requirementId: number, optionId: number | null) => {
    setSelections((current) =>
      applyRequirementSingleSelectEditorSelection(requirements, current, requirementId, optionId)
    );
  }, [requirements]);

  const toggleRequirementSelectedOption = useCallback((requirementId: number, optionId: number) => {
    setSelections((current) => {
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

  const saveChanges = useCallback(async () => {
    if (!selectedWorkflow) {
      setSaveError("Bitte zuerst einen Vorgang auswählen.");
      return;
    }

    setIsSaving(true);
    setSaveError(null);
    setSaveSuccess(null);

    try {
      await updateWorkflowSupervisorStep(selectedWorkflow.uid, toRequirementSelectionPayload(requirements, selections));
      setSaveSuccess(
        usesAdminOverride
          ? "Auswahl wurde per Admin-Override gespeichert. Der Vorgang wurde in die nächste Phase überführt."
          : "Auswahl wurde gespeichert. Der Vorgang wurde in die nächste Phase überführt."
      );
      setSelectedWorkflow(null);
      setRequirements([]);
      setSelections({});
      await reloadAssignedWorkflows();
    } catch (err) {
      const message = err instanceof Error ? err.message : "Auswahl konnte nicht gespeichert werden.";
      setSaveError(message);
    } finally {
      setIsSaving(false);
    }
  }, [reloadAssignedWorkflows, requirements, selections, selectedWorkflow, usesAdminOverride]);

  const canSave = useMemo(
    () => selectedWorkflow !== null && requirements.length > 0 && !isSaving,
    [selectedWorkflow, requirements, isSaving]
  );

  return (
    <main className="app-shell">
      <div className="page-container">
        <PageHeader
          title="Bedarf festlegen"
          description="Legen Sie pro Vorgang fest, welche Zugänge und welche Ausstattung benötigt werden."
        />

        <section className="panel">
          <div className="panel-head">
            <h2>Offene Freigaben</h2>
            <p>Hier sehen Sie nur Vorgänge, deren Prozesstyp einen Freigabeschritt der Abteilungsleitung enthält.</p>
          </div>
          <div className="next-action-callout">
            <p className="next-action-label">Nächste nötige Aktion</p>
            <p className="next-action-text">Öffnen Sie den ältesten offenen Fall und schließen Sie die Angaben ab.</p>
          </div>

          {usesAdminOverride ? (
            <p className="panel-note">
              Admin-Override aktiv. Die reguläre Bearbeitung dieses Schritts liegt fachlich bei der zuständigen
              Abteilungsleitung.
            </p>
          ) : null}

          <div className="action-row">
            <button type="button" className="btn btn-secondary" onClick={reloadAssignedWorkflows} disabled={queueLoading}>
              Aktualisieren
            </button>
          </div>
        </section>

        {queueLoading ? <LoadingState title="Zugewiesene Vorgänge werden geladen..." /> : null}

        {!queueLoading && queueError ? (
          <EmptyState title="Liste der Vorgänge konnte nicht geladen werden." description={queueError} onAction={reloadAssignedWorkflows} actionLabel="Erneut laden" />
        ) : null}

        {!queueLoading && !queueError && assignedWorkflows.length === 0 ? (
          <EmptyState
            title="Keine zugewiesenen Freigaben"
            description="Aktuell gibt es keine offenen Vorgänge mit Freigabeschritt für die Rückmeldung durch die Abteilungsleitung."
          />
        ) : null}

        {!queueLoading && !queueError && assignedWorkflows.length > 0 ? (
          <section className="workflow-grid" aria-label="Zugewiesene Vorgänge für Abteilungsleitungen">
            {assignedWorkflows.map((workflow) => (
              <article key={workflow.uid} className="workflow-card">
                <div className="workflow-card-top">
                  <div>
                    <h3>
                      {workflow.firstName} {workflow.lastName}
                    </h3>
                    <p className="panel-note">{workflow.processType.name}</p>
                  </div>
                  <div className="stacked-status">
                    <span className="status-pill status-pill-neutral">{workflow.processType.name}</span>
                    <span className="status-pill running">Wartet auf Abteilungsleitung</span>
                  </div>
                </div>

                <dl className="workflow-meta">
                  <div>
                    <dt>Personalnummer</dt>
                    <dd>{workflow.employeeNumber}</dd>
                  </div>
                  <div>
                    <dt>Abteilung</dt>
                    <dd>{workflow.departmentName}</dd>
                  </div>
                  <div>
                    <dt>Stelle</dt>
                    <dd>{workflow.roleName}</dd>
                  </div>
                  <div>
                    <dt>Workflow-ID</dt>
                    <dd>{workflow.uid}</dd>
                  </div>
                  <div>
                    <dt>Erstellt</dt>
                    <dd>{formatDateTime(workflow.createdAt)}</dd>
                  </div>
                </dl>

                <div className="action-row">
                  <button type="button" className="btn btn-primary" onClick={() => void loadSupervisorStep(workflow)}>
                    Angaben öffnen
                  </button>
                </div>
              </article>
            ))}
          </section>
        ) : null}

        {isLoadingStep ? <LoadingState title="Freigabeschritt wird geladen..." /> : null}

        {!isLoadingStep && stepError ? (
          <EmptyState title="Freigabeschritt konnte nicht geladen werden." description={stepError} />
        ) : null}

        {!isLoadingStep && !stepError && selectedWorkflow && requirements.length > 0 ? (
          <>
            <RequirementsSelection
              requirements={requirements}
              mode="edit"
              selections={selections}
              onToggleBoolean={setRequirementBoolean}
              onTextChange={setRequirementText}
              onSelectOption={setRequirementSelectedOption}
              onToggleMultiOption={toggleRequirementSelectedOption}
              isLoading={false}
              error={null}
              title={`Bedarf festlegen: ${selectedWorkflow.firstName} ${selectedWorkflow.lastName}`}
              description={
                usesAdminOverride
                  ? "Admin-Override: Nach dem Speichern entstehen die Aufgaben für die beteiligten Bereiche automatisch."
                  : "Nach dem Speichern entstehen die Aufgaben für die beteiligten Bereiche automatisch."
              }
            />

            {saveError ? (
              <section className="panel panel-muted">
                <p className="panel-text">{saveError}</p>
              </section>
            ) : null}

            {saveSuccess ? (
              <section className="panel panel-success">
                <p className="panel-text">{saveSuccess}</p>
              </section>
            ) : null}

            <section className="panel">
              <div className="action-row">
                <button type="button" className="btn btn-primary" disabled={!canSave} onClick={saveChanges}>
                  {isSaving ? "Speichern..." : usesAdminOverride ? "Angaben per Admin-Override abschließen" : "Angaben abschließen"}
                </button>
              </div>
            </section>
          </>
        ) : null}
      </div>
    </main>
  );
}
