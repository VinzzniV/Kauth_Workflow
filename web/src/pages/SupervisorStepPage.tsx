// Ansicht fuer die Abteilungsleitung, um Anforderungen eines freigabepflichtigen Vorgangs zu bestaetigen oder zu ergaenzen.
import { useCallback, useMemo, useState } from "react";
import { useCurrentUser } from "../auth/useCurrentUser";
import EmptyState from "../components/feedback/EmptyState";
import LoadingState from "../components/feedback/LoadingState";
import { useToast } from "../components/feedback/useToast";
import PageHeader from "../components/layout/PageHeader";
import RequirementsSelection from "../components/workflows/RequirementsSelection";
import {
  applyRequirementBooleanEditorSelection,
  applyRequirementSingleSelectEditorSelection,
} from "../utils/requirementEditor";
import { useSupervisorStep, useSupervisorWorkflows } from "../hooks/useSupervisorWorkflows";
import type {
  RequirementSelectionState,
  WorkflowSummary,
} from "../types/workflow";
import {
  buildRequirementSelections,
  createEmptyRequirementSelection,
  toRequirementSelectionPayload,
  type RequirementEntry,
} from "../utils/requirements";
import { formatDateTime } from "../utils/dateFormat";

export default function SupervisorStepPage() {
  const { capabilities } = useCurrentUser();
  const { assignedWorkflows, isQueueLoading, queueError, refetchQueue, saveMutation } = useSupervisorWorkflows();

  const [selectedWorkflow, setSelectedWorkflow] = useState<WorkflowSummary | null>(null);
  const usesAdminOverride = capabilities.canManageAdminConfiguration;
  const { showError, showSuccess } = useToast();

  const stepQuery = useSupervisorStep(selectedWorkflow?.uid ?? null);
  const requirements = stepQuery.data ?? [];

  const openSupervisorStep = useCallback((workflow: WorkflowSummary) => {
    setSelectedWorkflow(workflow);
  }, []);

  const saveChanges = useCallback(async (selections: Record<number, RequirementSelectionState>) => {
    if (!selectedWorkflow) {
      showError("Bitte zuerst einen Vorgang auswählen.");
      return;
    }

    try {
      await saveMutation.mutateAsync({
        uid: selectedWorkflow.uid,
        payload: toRequirementSelectionPayload(requirements, selections),
      });
      showSuccess(
        usesAdminOverride
          ? "Auswahl wurde per Admin-Override gespeichert. Der Vorgang wurde in die nächste Phase überführt."
          : "Auswahl wurde gespeichert. Der Vorgang wurde in die nächste Phase überführt."
      );
      setSelectedWorkflow(null);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Auswahl konnte nicht gespeichert werden.";
      showError(message);
    }
  }, [requirements, saveMutation, selectedWorkflow, showError, showSuccess, usesAdminOverride]);

  const workflowGroups = useMemo(() => {
    const groups = assignedWorkflows.reduce<
      Array<{ processTypeKey: string; processTypeName: string; workflows: WorkflowSummary[] }>
    >((current, workflow) => {
      const existingGroup = current.find((group) => group.processTypeKey === workflow.workflowDefinition.key);
      if (existingGroup) {
        existingGroup.workflows.push(workflow);
        return current;
      }

      current.push({
        processTypeKey: workflow.workflowDefinition.key,
        processTypeName: workflow.workflowDefinition.name,
        workflows: [workflow],
      });
      return current;
    }, []);

    return groups;
  }, [assignedWorkflows]);

  const shouldGroupByProcessType = workflowGroups.length > 1;

  return (
    <main className="app-shell">
      <div className="page-container">
        <PageHeader
          variant="workspace"
          title="Anforderungen freigeben"
          description="Offene Freigaben prüfen und Anforderungen für neue Mitarbeiter bestätigen."
        />

        <section className="panel">
          <div className="panel-head">
            <h2>Offene Freigaben</h2>
          </div>
          <div className="next-action-callout">
            <p className="next-action-label">Nächste nötige Aktion</p>
            <p className="next-action-text">Ältesten offenen Fall bearbeiten.</p>
          </div>

          {usesAdminOverride ? (
            <p className="panel-note">
              Admin-Override aktiv. Fachlich bleibt dieser Schritt bei der Abteilungsleitung.
            </p>
          ) : null}

          <div className="action-row">
            <button type="button" className="btn btn-secondary" onClick={() => void refetchQueue()} disabled={isQueueLoading}>
              Aktualisieren
            </button>
          </div>
        </section>

        {isQueueLoading ? <LoadingState title="Zugewiesene Vorgänge werden geladen..." /> : null}

        {!isQueueLoading && queueError ? (
          <EmptyState title="Liste der Vorgänge konnte nicht geladen werden." description={queueError} onAction={() => void refetchQueue()} actionLabel="Erneut laden" />
        ) : null}

        {!isQueueLoading && !queueError && assignedWorkflows.length === 0 ? (
          <EmptyState
            title="Keine zugewiesenen Freigaben"
            description="Aktuell sind keine Freigaben offen."
          />
        ) : null}

        {!isQueueLoading && !queueError && assignedWorkflows.length > 0 ? (
          <div className={shouldGroupByProcessType ? "task-groups" : undefined}>
            {(shouldGroupByProcessType ? workflowGroups : [{ processTypeKey: "all", processTypeName: "", workflows: assignedWorkflows }]).map((group) => (
              <section
                key={group.processTypeKey}
                className={shouldGroupByProcessType ? "task-group" : undefined}
                aria-label={shouldGroupByProcessType ? `Vorgänge für ${group.processTypeName}` : "Zugewiesene Vorgänge für Abteilungsleitungen"}
              >
                {shouldGroupByProcessType ? (
                  <div className="task-group-head">
                    <div>
                      <h3>{group.processTypeName}</h3>
                      <p>{group.workflows.length} offene Freigabe{group.workflows.length === 1 ? "" : "n"}</p>
                    </div>
                    <span className="chip">{group.processTypeName}</span>
                  </div>
                ) : null}

                <div className="workflow-grid">
                  {group.workflows.map((workflow) => (
                    <article key={workflow.uid} className="workflow-card card-list">
                      <div className="workflow-card-top">
                        <div>
                          <h3>
                            {workflow.firstName} {workflow.lastName}
                          </h3>
                          <div className="chips-row" aria-label="Prozesstyp">
                            <span className="chip">{workflow.workflowDefinition.name}</span>
                          </div>
                        </div>
                        <div className="stacked-status">
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
                        <button type="button" className="btn btn-primary" onClick={() => openSupervisorStep(workflow)}>
                          Angaben öffnen
                        </button>
                      </div>
                    </article>
                  ))}
                </div>
              </section>
            ))}
          </div>
        ) : null}

        {stepQuery.isFetching ? <LoadingState title="Freigabeschritt wird geladen..." /> : null}

        {!stepQuery.isFetching && stepQuery.isError ? (
          <EmptyState
            title="Freigabeschritt konnte nicht geladen werden."
            description={stepQuery.error instanceof Error ? stepQuery.error.message : "Unbekannter Fehler."}
          />
        ) : null}

        {!stepQuery.isFetching && !stepQuery.isError && selectedWorkflow && requirements.length > 0 ? (
          <SupervisorStepEditor
            key={selectedWorkflow.uid}
            selectedWorkflow={selectedWorkflow}
            requirements={requirements}
            usesAdminOverride={usesAdminOverride}
            isSaving={saveMutation.isPending}
            onSave={saveChanges}
          />
        ) : null}
      </div>
    </main>
  );
}

function SupervisorStepEditor({
  selectedWorkflow,
  requirements,
  usesAdminOverride,
  isSaving,
  onSave,
}: {
  selectedWorkflow: WorkflowSummary;
  requirements: RequirementEntry[];
  usesAdminOverride: boolean;
  isSaving: boolean;
  onSave: (selections: Record<number, RequirementSelectionState>) => Promise<void>;
}) {
  const [selections, setSelections] = useState<Record<number, RequirementSelectionState>>(() =>
    buildRequirementSelections(requirements)
  );

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

  const canSave = requirements.length > 0 && !isSaving;

  return (
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
            ? "Admin-Override: Aufgaben werden nach dem Speichern automatisch erzeugt."
            : "Aufgaben werden nach dem Speichern automatisch erzeugt."
        }
      />
      <section className="panel">
        <div className="action-row">
          <button type="button" className="btn btn-primary" disabled={!canSave} onClick={() => void onSave(selections)}>
            {isSaving ? "Speichern..." : usesAdminOverride ? "Angaben per Admin-Override abschließen" : "Angaben abschließen"}
          </button>
        </div>
      </section>
    </>
  );
}
