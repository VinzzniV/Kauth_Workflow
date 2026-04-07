import { Link } from "react-router-dom";
import CreateWorkflowButton from "../components/workflows/CreateWorkflowButton";
import EmployeeForm from "../components/workflows/EmployeeForm";
import RoleSelection from "../components/workflows/RoleSelection";
import TargetPersonSelection from "../components/workflows/TargetPersonSelection";
import type { StepDefinition } from "./createWorkflowPageModel";
import type {
  Department,
  EmployeeFormData,
  ProcessType,
  Role,
  WorkflowTargetPersonSource,
  WorkflowConfig,
} from "../types/workflow";
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
  processTypesLoading,
  processTypes,
  selectedProcessTypeKey,
  canGoToContextStep,
  requiresTargetPerson,
  hasAttemptedProcessNext,
  processStepIssues,
  onSelectProcessType,
  onGoToContextStep,
  onAttemptBlockedNext,
}: {
  processTypesLoading: boolean;
  processTypes: ProcessType[];
  selectedProcessTypeKey: string | null;
  canGoToContextStep: boolean;
  requiresTargetPerson: boolean;
  hasAttemptedProcessNext: boolean;
  processStepIssues: string[];
  onSelectProcessType: (key: string) => void;
  onGoToContextStep: () => void;
  onAttemptBlockedNext: () => void;
}) {
  return (
    <section className="panel">
      <h2>Vorgang wählen</h2>

      {processTypesLoading ? <p className="panel-text">Verfügbare Vorgänge werden geladen...</p> : null}

      {!processTypesLoading && processTypes.length === 0 ? (
        <div className="panel panel-muted">
          <h3 className="panel-title">Für Ihre Rolle ist aktuell kein Vorgang freigegeben.</h3>
          <p className="panel-text">Bitte Prozessfreigaben prüfen oder Administration kontaktieren.</p>
        </div>
      ) : null}

      {!processTypesLoading && processTypes.length > 0 ? (
        <>
          <div className="process-type-grid">
            {processTypes.map((processType) => {
              const isSelected = selectedProcessTypeKey === processType.key;
              const processContextLabel = processType.requiresTargetPerson ? "Bestehende Person" : "Neue Person";
              const processDescription = processType.description?.trim() || processContextLabel;

              return (
                <button
                  key={processType.key}
                  type="button"
                  aria-pressed={isSelected}
                  className={`process-type-card${isSelected ? " process-type-card--selected" : ""}`}
                  onClick={() => onSelectProcessType(processType.key)}
                >
                  <div className="process-type-card__head">
                    <span className="process-type-card__name">{processType.name}</span>
                    <span className="process-type-card__meta">{processContextLabel}</span>
                  </div>
                  <span className="process-type-card__description">{processDescription}</span>
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
  selectedProcessType: ProcessType | null;
  targetPersonSourceSearch: string;
  targetPersonSources: WorkflowTargetPersonSource[];
  selectedTargetPersonSource: WorkflowTargetPersonSource | null;
  targetPersonSourcesLoading: boolean;
  targetPersonSourcesError: string | null;
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
  onSelectTargetPersonSource: (source: WorkflowTargetPersonSource | null) => void;
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
    selectedProcessType,
    targetPersonSourceSearch,
    targetPersonSources,
    selectedTargetPersonSource,
    targetPersonSourcesLoading,
    targetPersonSourcesError,
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
    onSelectTargetPersonSource,
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
          processTypeName={selectedProcessType?.name ?? "den Vorgang"}
          searchValue={targetPersonSourceSearch}
          onSearchChange={onSearchChange}
          targetPersonSources={targetPersonSources}
          selectedWorkflowUid={selectedTargetPersonSource?.workflowUid ?? null}
          selectedSource={selectedTargetPersonSource}
          isLoading={targetPersonSourcesLoading}
          error={targetPersonSourcesError}
          selectionError={targetPersonSelectionError}
          onSelectSource={onSelectTargetPersonSource}
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
          <h3 className="panel-title">Kontext der Zielperson ist unvollständig</h3>
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
            Zurück zur Vorgangsauswahl
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
  selectedProcessType: ProcessType | null;
  selectedTargetPersonSource: WorkflowTargetPersonSource | null;
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
    selectedProcessType,
    selectedTargetPersonSource,
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
            <h3 className="panel-title">Vorgang</h3>
            <dl className="workflow-kv-grid">
              <div>
                <dt>Prozesstyp</dt>
                <dd>{selectedProcessType?.name ?? "-"}</dd>
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
                    ? selectedTargetPersonSource?.displayName ?? "-"
                    : `${employee.firstName} ${employee.lastName}`.trim() || "-"}
                </dd>
              </div>
              <div>
                <dt>Personalnummer</dt>
                <dd>{requiresTargetPerson ? selectedTargetPersonSource?.employeeNumber ?? "-" : employee.employeeNumber || "-"}</dd>
              </div>
              <div>
                <dt>Kartennummer</dt>
                <dd>{requiresTargetPerson ? selectedTargetPersonSource?.badgeNumber ?? "-" : employee.badgeNumber || "-"}</dd>
              </div>
              <div>
                <dt>Deadline</dt>
                <dd>{employee.deadlineDate || "Keine Deadline gesetzt"}</dd>
              </div>
              {requiresTargetPerson ? (
                <div>
                  <dt>Quellworkflow</dt>
                  <dd>{selectedTargetPersonSource?.workflowUid ?? "-"}</dd>
                </div>
              ) : null}
            </dl>
          </div>

          <div className="panel panel-muted">
            <h3 className="panel-title">Zuordnung</h3>
            <dl className="workflow-kv-grid">
              <div>
                <dt>{reviewDepartmentLabel}</dt>
                <dd>{requiresTargetPerson ? selectedTargetPersonSource?.departmentName ?? "-" : selectedDepartment?.name ?? "-"}</dd>
              </div>
              <div>
                <dt>{reviewRoleLabel}</dt>
                <dd>{requiresTargetPerson ? selectedTargetPersonSource?.roleName ?? "-" : selectedRole?.name ?? "-"}</dd>
              </div>
              <div>
                <dt>Anforderungen</dt>
                <dd>{workflowConfig?.requirements.length ?? 0}</dd>
              </div>
              <div>
                <dt>Vorbelegungen</dt>
                <dd>{roleRecommendationCount}</dd>
              </div>
            </dl>
          </div>
        </div>
      </section>

      {submitError ? (
        <section className="panel panel-error" role="status" aria-live="polite">
          <h3 className="panel-title">Vorgang konnte nicht gestartet werden.</h3>
          <p className="panel-text">{submitError}</p>
        </section>
      ) : null}

      {submitSuccessMessage ? (
        <section className="panel panel-success" role="status" aria-live="polite">
          <h3 className="panel-title">Vorgang erfolgreich gestartet.</h3>
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
            label={isHrEntry ? "Vorgang anlegen" : "Änderung anlegen"}
            onSubmit={onSubmit}
          />
        </div>
      </section>
    </div>
  );
}
