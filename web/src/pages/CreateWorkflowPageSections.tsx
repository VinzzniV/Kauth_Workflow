import { Link } from "react-router-dom";
import CreateWorkflowButton from "../components/workflows/CreateWorkflowButton";
import EmployeeForm from "../components/workflows/EmployeeForm";
import RoleSelection from "../components/workflows/RoleSelection";
import TargetPersonSelection from "../components/workflows/TargetPersonSelection";
import type { StepDefinition } from "./createWorkflowPageModel";
import type {
  Department,
  EmployeeFormData,
  Role,
  StartableWorkflowDefinition,
  WorkflowConfig,
  WorkflowTargetPerson,
} from "../types/workflow";

function formatEmploymentStatus(status: string | null): string {
  switch (status) {
    case "planned":
      return "Geplant";
    case "active":
      return "Aktiv";
    case "inactive":
      return "Inaktiv";
    case "exited":
      return "Ausgetreten";
    default:
      return "-";
  }
}

function formatDirectoryLinkStatus(status: string | null): string {
  switch (status) {
    case "linked":
      return "Mit Verzeichnis verknüpft";
    case "user_only":
      return "Nur App-Benutzer verknüpft";
    case "unlinked":
      return "Noch nicht verknüpft";
    default:
      return "-";
  }
}

export function WorkflowCreationStepper({
  steps,
  currentStepIndex,
}: {
  steps: StepDefinition[];
  currentStepIndex: number;
}) {
  return (
    <ol className="wizard-stepper">
      {steps.map((step, index) => {
        const stateClass =
          index < currentStepIndex
            ? "wizard-stepper__item wizard-stepper__item--done"
            : index === currentStepIndex
              ? "wizard-stepper__item wizard-stepper__item--active"
              : "wizard-stepper__item wizard-stepper__item--pending";

        return (
          <li key={step.key} className={stateClass} aria-current={index === currentStepIndex ? "step" : undefined}>
            <p className="wizard-stepper__title">{step.title}</p>
          </li>
        );
      })}
    </ol>
  );
}

export function CreateWorkflowProcessStep({
  showRotationCreateEntry,
  workflowDefinitionsLoading,
  workflowDefinitions,
  selectedWorkflowDefinitionKey,
  canGoToContextStep,
  requiresTargetPerson,
  hasAttemptedProcessNext,
  processStepIssues,
  onSelectWorkflowDefinition,
  onGoToContextStep,
  onAttemptBlockedNext,
}: {
  showRotationCreateEntry: boolean;
  workflowDefinitionsLoading: boolean;
  workflowDefinitions: StartableWorkflowDefinition[];
  selectedWorkflowDefinitionKey: string | null;
  canGoToContextStep: boolean;
  requiresTargetPerson: boolean;
  hasAttemptedProcessNext: boolean;
  processStepIssues: string[];
  onSelectWorkflowDefinition: (key: string) => void;
  onGoToContextStep: () => void;
  onAttemptBlockedNext: () => void;
}) {
  return (
    <section className="panel">
      <h2>Workflow wählen</h2>

      {showRotationCreateEntry ? (
        <div className="panel panel-muted">
          <div className="workflow-card-top">
            <h3>Abteilungsdurchlauf starten</h3>
            <span className="status-pill open">HR</span>
          </div>
          <p className="panel-text">
            Startet einen neuen Durchlaufplan auf Basis einer bestehenden Person mit abgeschlossenem Onboarding.
          </p>
          <div className="action-row">
            <Link className="btn btn-secondary" to="/rotation?mode=create">
              Zum Abteilungsdurchlauf
            </Link>
          </div>
        </div>
      ) : null}

      {workflowDefinitionsLoading ? <p className="panel-text">Startbare Workflows werden geladen...</p> : null}

      {!workflowDefinitionsLoading && workflowDefinitions.length === 0 ? (
        <div className="panel panel-muted">
          <h3 className="panel-title">Für Ihre Rolle ist aktuell kein Workflow freigegeben.</h3>
          <p className="panel-text">Bitte Workflow-Freigaben prüfen oder Administration kontaktieren.</p>
        </div>
      ) : null}

      {!workflowDefinitionsLoading && workflowDefinitions.length > 0 ? (
        <>
          <div className="process-type-grid">
            {workflowDefinitions.map((workflowDefinition) => {
              const isSelected =
                selectedWorkflowDefinitionKey === workflowDefinition.definitionKey;
              const workflowContextLabel = workflowDefinition.requiresTargetPerson
                ? "Bestehende Person"
                : "Neue Person";
              const workflowDescription =
                workflowDefinition.description?.trim() || workflowContextLabel;

              return (
                <button
                  key={workflowDefinition.definitionKey}
                  type="button"
                  aria-pressed={isSelected}
                  className={`process-type-card${isSelected ? " process-type-card--selected" : ""}`}
                  onClick={() => onSelectWorkflowDefinition(workflowDefinition.definitionKey)}
                >
                  <div className="process-type-card__head">
                    <span className="process-type-card__name">{workflowDefinition.name}</span>
                    <span className="process-type-card__meta">{workflowContextLabel}</span>
                  </div>
                  <span className="process-type-card__description">{workflowDescription}</span>
                  <span className="process-type-card__selection">{isSelected ? "Ausgewählt" : "Auswählen"}</span>
                </button>
              );
            })}
          </div>

          {hasAttemptedProcessNext && processStepIssues.length > 0 ? (
            <section className="panel panel-warning wizard-inline-note" role="status" aria-live="polite">
              <h3 className="panel-title">Zum Weitergehen fehlt noch</h3>
              <ul className="validation-list">
                {processStepIssues.map((issue) => (
                  <li key={issue}>{issue}</li>
                ))}
              </ul>
            </section>
          ) : null}

          <div className="wizard-actions">
            <span />
            <button
              type="button"
              className="btn btn-primary"
              disabled={!canGoToContextStep}
              onClick={() => {
                if (canGoToContextStep) {
                  onGoToContextStep();
                } else {
                  onAttemptBlockedNext();
                }
              }}
            >
              {requiresTargetPerson ? "Weiter zur Personenauswahl" : "Weiter zur Person"}
            </button>
          </div>
        </>
      ) : null}
    </section>
  );
}

export function CreateWorkflowContextStep(props: {
  contextStepTitle: string;
  requiresTargetPerson: boolean;
  selectedWorkflowDefinition: StartableWorkflowDefinition | null;
  targetPersonSearch: string;
  targetPeople: WorkflowTargetPerson[];
  selectedTargetPerson: WorkflowTargetPerson | null;
  targetPeopleLoading: boolean;
  targetPeopleError: string | null;
  targetPersonSelectionError: string | null;
  employee: EmployeeFormData;
  employeeFieldErrors: Record<string, string | undefined>;
  roles: Role[];
  departments: Department[];
  selectedDepartmentId: number | null;
  selectedRoleId: number | null;
  rolesLoading: boolean;
  rolesError: string | null;
  departmentError: string | null;
  roleError: string | null;
  availableRoles: Role[];
  hasDerivedContextGap: boolean;
  hasAttemptedContextNext: boolean;
  contextStepIssues: string[];
  onSearchChange: (value: string) => void;
  onSelectTargetPerson: (person: WorkflowTargetPerson | null) => void;
  onEmployeeChange: (field: keyof EmployeeFormData, value: string | number) => void;
  onDepartmentChange: (departmentId: number | null) => void;
  onRoleChange: (roleId: number | null) => void;
  onRetryRoles: () => void;
  onGoBack: () => void;
  onGoReview: () => void;
  canGoToReviewStep: boolean;
  onAttemptBlockedNext: () => void;
}) {
  const {
    contextStepTitle,
    requiresTargetPerson,
    selectedWorkflowDefinition,
    targetPersonSearch,
    targetPeople,
    selectedTargetPerson,
    targetPeopleLoading,
    targetPeopleError,
    targetPersonSelectionError,
    employee,
    employeeFieldErrors,
    roles,
    departments,
    selectedDepartmentId,
    selectedRoleId,
    rolesLoading,
    rolesError,
    departmentError,
    roleError,
    availableRoles,
    hasDerivedContextGap,
    hasAttemptedContextNext,
    contextStepIssues,
    onSearchChange,
    onSelectTargetPerson,
    onEmployeeChange,
    onDepartmentChange,
    onRoleChange,
    onRetryRoles,
    onGoBack,
    onGoReview,
    canGoToReviewStep,
    onAttemptBlockedNext,
  } = props;

  return (
    <div className="content-stack">
      <section className="panel">
        <h2>{contextStepTitle}</h2>
      </section>

      {requiresTargetPerson ? (
        <TargetPersonSelection
          processTypeName={selectedWorkflowDefinition?.name ?? "den Workflow"}
          searchValue={targetPersonSearch}
          onSearchChange={onSearchChange}
          targetPeople={targetPeople}
          selectedPersonId={selectedTargetPerson?.personId ?? null}
          selectedPerson={selectedTargetPerson}
          isLoading={targetPeopleLoading}
          error={targetPeopleError}
          selectionError={targetPersonSelectionError}
          onSelectPerson={(person) => onSelectTargetPerson(person)}
        />
      ) : (
        <>
          <EmployeeForm value={employee} onChange={onEmployeeChange} fieldErrors={employeeFieldErrors} />

          <RoleSelection
            roles={roles}
            departments={departments}
            selectedDepartmentId={selectedDepartmentId}
            selectedRoleId={selectedRoleId}
            isLoading={rolesLoading}
            error={rolesError}
            departmentError={departmentError}
            roleError={roleError}
            onDepartmentChange={onDepartmentChange}
            onRoleChange={onRoleChange}
            onRetry={onRetryRoles}
          />

          {!rolesLoading && !rolesError && selectedDepartmentId !== null && availableRoles.length === 0 ? (
            <section className="panel panel-warning" role="status" aria-live="polite">
              <h3 className="panel-title">In dieser Abteilung ist keine Stelle hinterlegt.</h3>
              <p className="panel-text">Bitte andere Abteilung wählen oder Stammdaten prüfen.</p>
            </section>
          ) : null}
        </>
      )}

      {hasDerivedContextGap ? (
        <section className="panel panel-warning" role="status" aria-live="polite">
          <h3 className="panel-title">Person-Stammdaten sind unvollständig</h3>
          <p className="panel-text">
            Für die gewählte Person fehlen Angaben zu Abteilung, Stelle, Personalnummer oder Kartennummer.
          </p>
        </section>
      ) : null}

      {hasAttemptedContextNext && contextStepIssues.length > 0 ? (
        <section className="panel panel-warning" role="status" aria-live="polite">
          <h3 className="panel-title">Bitte noch prüfen</h3>
          <ul className="validation-list">
            {contextStepIssues.map((issue) => (
              <li key={issue}>{issue}</li>
            ))}
          </ul>
        </section>
      ) : null}

      <section className="panel">
        <div className="wizard-actions">
          <button type="button" className="btn btn-secondary" onClick={onGoBack}>
            Zurück zur Workflow-Auswahl
          </button>
          <button
            type="button"
            className="btn btn-primary"
            disabled={!canGoToReviewStep}
            onClick={() => {
              if (canGoToReviewStep) {
                onGoReview();
              } else {
                onAttemptBlockedNext();
              }
            }}
          >
            Zur Prüfung
          </button>
        </div>
      </section>
    </div>
  );
}

export function CreateWorkflowReviewStep(props: {
  requiresTargetPerson: boolean;
  selectedWorkflowDefinition: StartableWorkflowDefinition | null;
  selectedTargetPerson: WorkflowTargetPerson | null;
  employee: EmployeeFormData;
  selectedDepartment: Department | null;
  selectedRole: Role | null;
  workflowConfig: WorkflowConfig | null;
  roleRecommendationCount: number;
  reviewPersonLabel: string;
  reviewDepartmentLabel: string;
  reviewRoleLabel: string;
  submitError: string | null;
  submitSuccessMessage: string | null;
  createdWorkflowUid: string | null;
  submitState: "idle" | "loading" | "success" | "error";
  canSubmit: boolean;
  isHrEntry: boolean;
  onGoBack: () => void;
  onSubmit: () => Promise<void>;
}) {
  const {
    requiresTargetPerson,
    selectedWorkflowDefinition,
    selectedTargetPerson,
    employee,
    selectedDepartment,
    selectedRole,
    workflowConfig,
    roleRecommendationCount,
    reviewPersonLabel,
    reviewDepartmentLabel,
    reviewRoleLabel,
    submitError,
    submitSuccessMessage,
    createdWorkflowUid,
    submitState,
    canSubmit,
    isHrEntry,
    onGoBack,
    onSubmit,
  } = props;

  return (
    <div className="content-stack">
      <section className="panel">
        <h2>Prüfen und anlegen</h2>

        <div className="wizard-review-grid">
          <div className="panel panel-muted">
            <h3 className="panel-title">Workflow</h3>
            <dl className="workflow-kv-grid">
              <div>
                <dt>Definition</dt>
                <dd>{selectedWorkflowDefinition?.name ?? "-"}</dd>
              </div>
              <div>
                <dt>Kontext</dt>
                <dd>{requiresTargetPerson ? "Bestehende Person" : "Neue Person"}</dd>
              </div>
            </dl>
          </div>

          <div className="panel panel-muted">
            <h3 className="panel-title">{reviewPersonLabel}</h3>
            <dl className="workflow-kv-grid">
              <div>
                <dt>Name</dt>
                <dd>
                  {requiresTargetPerson
                    ? selectedTargetPerson?.displayName ?? "-"
                    : `${employee.firstName} ${employee.lastName}`.trim() || "-"}
                </dd>
              </div>
              <div>
                <dt>Personalnummer</dt>
                <dd>{requiresTargetPerson ? selectedTargetPerson?.employeeNumber ?? "-" : employee.employeeNumber || "-"}</dd>
              </div>
              <div>
                <dt>Kartennummer</dt>
                <dd>{requiresTargetPerson ? selectedTargetPerson?.badgeNumber ?? "-" : employee.badgeNumber || "-"}</dd>
              </div>
              <div>
                <dt>Deadline</dt>
                <dd>{employee.deadlineDate || "Keine Deadline gesetzt"}</dd>
              </div>
              {requiresTargetPerson ? (
                <>
                  <div>
                    <dt>Beschäftigungsstatus</dt>
                    <dd>{formatEmploymentStatus(selectedTargetPerson?.employmentStatus ?? null)}</dd>
                  </div>
                  <div>
                    <dt>Directory-Link</dt>
                    <dd>{formatDirectoryLinkStatus(selectedTargetPerson?.directoryLinkStatus ?? null)}</dd>
                  </div>
                </>
              ) : null}
            </dl>
          </div>

          <div className="panel panel-muted">
            <h3 className="panel-title">Zuordnung</h3>
            <dl className="workflow-kv-grid">
              <div>
                <dt>{reviewDepartmentLabel}</dt>
                <dd>{requiresTargetPerson ? selectedTargetPerson?.departmentName ?? "-" : selectedDepartment?.name ?? "-"}</dd>
              </div>
              <div>
                <dt>{reviewRoleLabel}</dt>
                <dd>{requiresTargetPerson ? selectedTargetPerson?.roleName ?? "-" : selectedRole?.name ?? "-"}</dd>
              </div>
              <div>
                <dt>Anforderungen</dt>
                <dd>{workflowConfig?.requirements.length ?? 0}</dd>
              </div>
              <div>
                <dt>Vorbelegungen</dt>
                <dd>{roleRecommendationCount}</dd>
              </div>
              {requiresTargetPerson ? (
                <div>
                  <dt>Letztes abgeschlossenes Onboarding</dt>
                  <dd>{selectedTargetPerson?.latestCompletedOnboardingWorkflowUid ?? "-"}</dd>
                </div>
              ) : null}
            </dl>
          </div>
        </div>
      </section>

      {submitError ? (
        <section className="panel panel-error" role="status" aria-live="polite">
          <h3 className="panel-title">Workflow konnte nicht gestartet werden.</h3>
          <p className="panel-text">{submitError}</p>
        </section>
      ) : null}

      {submitSuccessMessage ? (
        <section className="panel panel-success" role="status" aria-live="polite">
          <h3 className="panel-title">Workflow erfolgreich gestartet.</h3>
          <p className="panel-text">{submitSuccessMessage}</p>
          {createdWorkflowUid ? (
            <div className="action-row">
              <Link className="btn btn-primary" to={`/workflows/${createdWorkflowUid}`}>
                Zum neuen Vorgang
              </Link>
              <Link className="btn btn-secondary" to="/workflows">
                Zur Übersicht
              </Link>
            </div>
          ) : null}
        </section>
      ) : null}

      <section className="panel">
        <div className="wizard-actions">
          <button type="button" className="btn btn-secondary" onClick={onGoBack}>
            Zurück zur Person
          </button>
          <CreateWorkflowButton
            isLoading={submitState === "loading"}
            disabled={!canSubmit || submitState === "success"}
            label={isHrEntry ? "Workflow anlegen" : "Änderung anlegen"}
            onSubmit={onSubmit}
          />
        </div>
      </section>
    </div>
  );
}
