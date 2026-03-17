// Ansicht fuer die Abteilungsleitung, um Anforderungen eines neuen Onboardings zu bestaetigen oder zu ergaenzen.
import { useCallback, useEffect, useMemo, useState } from "react";
import { useCurrentUser } from "../auth/useCurrentUser";
import EmptyState from "../components/feedback/EmptyState";
import LoadingState from "../components/feedback/LoadingState";
import PageHeader from "../components/layout/PageHeader";
import RequirementsSelection from "../components/workflows/RequirementsSelection";
import {
  getSupervisorStepWorkflows,
  getWorkflowSupervisorStep,
  updateWorkflowSupervisorStep,
} from "../services/onboardingApi";
import type {
  RequirementSelectionPayload,
  RequirementSelectionState,
  WorkflowRequirementSnapshot,
  WorkflowSummary,
} from "../types/workflow";
import {
  applyRequirementBooleanSelection,
  applyRequirementSingleSelectSelection,
  buildRequirementSelections,
  createEmptyRequirementSelection,
  validateRequirementSelections,
} from "../utils/requirements";

function formatDate(value: string): string {
  if (!value) {
    return "-";
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return "-";
  }

  return parsed.toLocaleString("de-DE");
}

// Wandelt den lokalen Formularzustand wieder in das Transportformat des Backends um.
function toPayload(
  requirements: WorkflowRequirementSnapshot[],
  selections: Record<number, RequirementSelectionState>
): RequirementSelectionPayload[] {
  return requirements.map((requirement) => {
    const selection = selections[requirement.id] ?? createEmptyRequirementSelection();

    if (requirement.inputType === "boolean") {
      return {
        requirementId: requirement.id,
        valueBoolean: selection.valueBoolean,
      };
    }

    if (requirement.inputType === "text") {
      return {
        requirementId: requirement.id,
        valueText: selection.valueText,
      };
    }

    if (requirement.inputType === "select") {
      return {
        requirementId: requirement.id,
        selectedOptionId: selection.selectedOptionId,
      };
    }

    return {
      requirementId: requirement.id,
      selectedOptionIds: [...selection.selectedOptionIds],
    };
  });
}

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
      setAssignedWorkflows(workflows);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Liste der Onboarding-Fälle konnte nicht geladen werden.";
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
      setSelections((current) => applyRequirementBooleanSelection(requirements, current, requirementId, value));
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
    setSelections((current) => applyRequirementSingleSelectSelection(requirements, current, requirementId, optionId));
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
      setSaveError("Bitte zuerst einen Onboarding-Fall auswählen.");
      return;
    }

    const validationError = validateRequirementSelections(requirements, selections);
    if (validationError) {
      setSaveError(validationError);
      return;
    }

    setIsSaving(true);
    setSaveError(null);
    setSaveSuccess(null);

    try {
      await updateWorkflowSupervisorStep(selectedWorkflow.uid, toPayload(requirements, selections));
      setSaveSuccess(
        usesAdminOverride
          ? "Auswahl wurde per Admin-Override gespeichert. Der Onboarding-Fall wurde in die nächste Phase überführt."
          : "Auswahl wurde gespeichert. Der Onboarding-Fall wurde in die nächste Phase überführt."
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
    () =>
      selectedWorkflow !== null &&
      requirements.length > 0 &&
      !isSaving &&
      validateRequirementSelections(requirements, selections) === null,
    [selectedWorkflow, requirements, isSaving, selections]
  );

  return (
    <main className="onboarding-shell">
      <div className="page-container">
        <PageHeader
          title="Bedarf festlegen"
          description="Legen Sie pro Onboarding fest, welche Zugänge und welche Ausstattung benötigt werden."
        />

        <section className="panel">
          <div className="panel-head">
            <h2>Offene Onboardings</h2>
            <p>Hier sehen Sie nur Fälle, die auf die Rückmeldung der Abteilungsleitung warten.</p>
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

        {queueLoading ? <LoadingState title="Zugewiesene Onboarding-Fälle werden geladen..." /> : null}

        {!queueLoading && queueError ? (
          <EmptyState title="Liste der Onboarding-Fälle konnte nicht geladen werden." description={queueError} onAction={reloadAssignedWorkflows} actionLabel="Erneut laden" />
        ) : null}

        {!queueLoading && !queueError && assignedWorkflows.length === 0 ? (
          <EmptyState
            title="Keine zugewiesenen Onboarding-Fälle"
            description="Aktuell gibt es keine offenen Fälle für die Rückmeldung durch die Abteilungsleitung."
          />
        ) : null}

        {!queueLoading && !queueError && assignedWorkflows.length > 0 ? (
          <section className="workflow-grid" aria-label="Zugewiesene Onboarding-Fälle für Abteilungsleitungen">
            {assignedWorkflows.map((workflow) => (
              <article key={workflow.uid} className="workflow-card">
                <div className="workflow-card-top">
                  <h3>
                    {workflow.firstName} {workflow.lastName}
                  </h3>
                  <span className="status-pill running">Wartet auf Abteilungsleitung</span>
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
                    <dt>Erstellt</dt>
                    <dd>{formatDate(workflow.createdAt)}</dd>
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

        {isLoadingStep ? <LoadingState title="Rückmeldung der Abteilungsleitung wird geladen..." /> : null}

        {!isLoadingStep && stepError ? (
          <EmptyState title="Rückmeldung der Abteilungsleitung konnte nicht geladen werden." description={stepError} />
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
