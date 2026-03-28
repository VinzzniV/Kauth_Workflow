import { Link } from "react-router-dom";
import { useCurrentUser } from "../auth/useCurrentUser";
import PageHeader from "../components/layout/PageHeader";
import CreateWorkflowButton from "../components/workflows/CreateWorkflowButton";
import EmployeeForm from "../components/workflows/EmployeeForm";
import RoleSelection from "../components/workflows/RoleSelection";
import TargetPersonSelection from "../components/workflows/TargetPersonSelection";
import { useWorkflowCreation, type WorkflowCreationStep } from "../hooks/useWorkflowCreation";

type StepDefinition = {
  key: WorkflowCreationStep;
  title: string;
  detail: string;
};

export default function CreateWorkflowPage() {
  const { capabilities } = useCurrentUser();
  const {
    currentStep,
    processTypes,
    processTypesLoading,
    selectedProcessTypeKey,
    selectedProcessType,
    requiresTargetPerson,
    employee,
    selectedDepartmentId,
    selectedRoleId,
    selectedDepartment,
    selectedRole,
    selectedCompletedOnboarding,
    completedOnboardingSearch,
    completedOnboardings,
    completedOnboardingsLoading,
    completedOnboardingsError,
    roles,
    departments,
    availableRoles,
    rolesLoading,
    rolesError,
    workflowConfig,
    workflowConfigLoading,
    workflowConfigError,
    submitState,
    submitError,
    submitSuccessMessage,
    createdWorkflowUid,
    setProcessType,
    goToProcessStep,
    goToContextStep,
    goToReviewStep,
    setEmployeeField,
    setSelectedDepartment,
    setSelectedRole,
    setCompletedOnboardingSearch,
    setSelectedCompletedOnboarding,
    submitWorkflow,
    canGoToContextStep,
    canGoToReviewStep,
    canSubmit,
    reloadRoles,
  } = useWorkflowCreation();

  const isHrEntry = capabilities.hasHrRole || capabilities.hasAdminRole;
  const pageTitle = isHrEntry ? "Neuer Vorgang" : "Änderung starten";
  const pageDescription = isHrEntry
    ? "Wählen Sie zuerst den Vorgang und erfassen Sie danach nur die dafür nötigen Angaben."
    : "Wählen Sie zuerst den zulässigen Änderungsvorgang und danach den betroffenen Mitarbeitenden.";
  const contextStepTitle = requiresTargetPerson ? "Bestehende Person wählen" : "Neue Person erfassen";
  const contextStepDetail = requiresTargetPerson
    ? "Abgeschlossenes Onboarding und Zielperson auswählen."
    : "Stammdaten, Abteilung und Stelle erfassen.";
  const reviewPersonLabel = requiresTargetPerson ? "Zielperson" : "Neue Person";
  const reviewDepartmentLabel = requiresTargetPerson ? "Aktuelle Abteilung" : "Abteilung";
  const reviewRoleLabel = requiresTargetPerson ? "Aktuelle Stelle" : "Stelle";
  const roleRecommendationCount =
    (workflowConfig?.roleRecommendations.defaultValues.length ?? 0) +
    (workflowConfig?.roleRecommendations.defaultSelectedOptions.length ?? 0);
  const hasDerivedContextGap = Boolean(
    requiresTargetPerson &&
      selectedCompletedOnboarding &&
      (!selectedCompletedOnboarding.departmentId ||
        !selectedCompletedOnboarding.roleId ||
        selectedCompletedOnboarding.employeeNumber <= 0 ||
        selectedCompletedOnboarding.badgeNumber <= 0)
  );
  const processStepIssues = selectedProcessType
    ? []
    : ["Wählen Sie einen Vorgang aus, damit der Wizard den passenden Kontext laden kann."];
  const employeeFieldErrors = requiresTargetPerson
    ? {}
    : {
        firstName: employee.firstName.trim() ? undefined : "Vorname ist erforderlich.",
        lastName: employee.lastName.trim() ? undefined : "Nachname ist erforderlich.",
        employeeNumber: employee.employeeNumber > 0 ? undefined : "Positive Personalnummer eingeben.",
        badgeNumber: employee.badgeNumber > 0 ? undefined : "Positive Kartennummer eingeben.",
      };
  const departmentError =
    !requiresTargetPerson && selectedDepartmentId === null ? "Bitte eine Abteilung auswählen." : null;
  const roleError =
    !requiresTargetPerson && selectedDepartmentId !== null && selectedRoleId === null
      ? !rolesLoading && !rolesError && availableRoles.length === 0
        ? "In der gewählten Abteilung ist keine aktive Stelle hinterlegt."
        : "Bitte eine Stelle auswählen."
      : null;
  const targetPersonSelectionError =
    requiresTargetPerson && !selectedCompletedOnboarding && !completedOnboardingsLoading
      ? "Bitte ein abgeschlossenes Onboarding auswählen."
      : null;
  const contextStepIssues = requiresTargetPerson
    ? [
        ...(completedOnboardingsError ? ["Die Suche nach abgeschlossenen Onboardings ist fehlgeschlagen."] : []),
        ...(targetPersonSelectionError ? [targetPersonSelectionError] : []),
        ...(hasDerivedContextGap
          ? ["Für die gewählte Person fehlen vollständige Angaben zu Abteilung, Stelle, Personalnummer oder Kartennummer."]
          : []),
      ]
    : [
        ...(rolesError ? ["Stellen und Abteilungen konnten nicht geladen werden."] : []),
        ...Object.values(employeeFieldErrors).filter((value): value is string => Boolean(value)),
        ...(departmentError ? [departmentError] : []),
        ...(roleError ? [roleError] : []),
      ];

  const steps: StepDefinition[] = [
    {
      key: "process",
      title: "Vorgang wählen",
      detail: "Welcher Prozess soll gestartet werden?",
    },
    {
      key: "context",
      title: contextStepTitle,
      detail: contextStepDetail,
    },
    {
      key: "review",
      title: "Prüfen und anlegen",
      detail: "Auswahl prüfen und Vorgang erstellen.",
    },
  ];

  const currentStepIndex = steps.findIndex((step) => step.key === currentStep);

  return (
    <main className="app-shell">
      <div className="page-container">
        <PageHeader title={pageTitle} description={pageDescription} />

        <section className="panel">
          <div className="panel-head">
            <h2>Vorgangserstellung</h2>
            <p>Der Wizard führt Sie in drei klaren Schritten durch die Vorgangserstellung.</p>
          </div>

          <ol className="process-step-list">
            {steps.map((step, index) => {
              const stateClass =
                index < currentStepIndex
                  ? "process-step-state process-step-state-done"
                  : index === currentStepIndex
                    ? "process-step-state process-step-state-active"
                    : "process-step-state process-step-state-pending";

              return (
                <li key={step.key} className="process-step-item" aria-current={index === currentStepIndex ? "step" : undefined}>
                  <span className={stateClass} aria-hidden="true" />
                  <div className="process-step-content">
                    <p className="process-step-title">{step.title}</p>
                    <p className="process-step-detail">{step.detail}</p>
                  </div>
                </li>
              );
            })}
          </ol>
        </section>

        {currentStep === "process" ? (
          <section className="panel">
            <div className="panel-head">
              <h2>Schritt 1: Vorgang wählen</h2>
              <p>
                Wählen Sie zuerst den fachlichen Vorgang. Danach zeigt der Wizard nur die dazu passenden Eingaben an.
              </p>
            </div>

            {processTypesLoading ? <p className="panel-text">Verfügbare Vorgänge werden geladen...</p> : null}

            {!processTypesLoading && processTypes.length === 0 ? (
              <div className="panel panel-muted">
                <h3 className="panel-title">Für Ihre Rolle ist aktuell kein Vorgang freigegeben.</h3>
                <p className="panel-text">Bitte prüfen Sie die Prozessfreigaben oder melden Sie sich bei der Administration.</p>
              </div>
            ) : null}

            {!processTypesLoading && processTypes.length > 0 ? (
              <>
                <div className="process-type-grid">
                  {processTypes.map((processType) => (
                    <button
                      key={processType.key}
                      type="button"
                      className={`process-type-card${selectedProcessTypeKey === processType.key ? " process-type-card--selected" : ""}`}
                      onClick={() => setProcessType(processType.key)}
                    >
                      <span className="process-type-card__name">{processType.name}</span>
                      <span className="process-type-card__meta">
                        {processType.requiresTargetPerson ? "Bestehender Mitarbeiter" : "Neue Person"}
                      </span>
                      <span className="process-type-card__description">
                        {processType.description?.trim() || "Keine zusätzliche Beschreibung hinterlegt."}
                      </span>
                    </button>
                  ))}
                </div>

                {selectedProcessType ? (
                  <div className="panel panel-muted wizard-inline-note">
                    <h3 className="panel-title">Ausgewählter Vorgang: {selectedProcessType.name}</h3>
                    <p className="panel-text">
                      {requiresTargetPerson
                        ? "Im nächsten Schritt wählen Sie eine bestehende Person über ein abgeschlossenes Onboarding aus."
                        : "Im nächsten Schritt erfassen Sie die Daten der neuen Person und die zugehörige Stelle."}
                    </p>
                  </div>
                ) : null}

                {processStepIssues.length > 0 ? (
                  <section className="panel panel-muted wizard-inline-note" role="status" aria-live="polite">
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
                  <button type="button" className="btn btn-primary" disabled={!canGoToContextStep} onClick={goToContextStep}>
                    Weiter zu Schritt 2
                  </button>
                </div>
              </>
            ) : null}
          </section>
        ) : null}

        {currentStep === "context" ? (
          <>
            <section className="panel panel-intro">
              <h2>Schritt 2: {contextStepTitle}</h2>
              <p>
                {requiresTargetPerson
                  ? `Für ${selectedProcessType?.name ?? "diesen Vorgang"} wird eine bestehende Person aus einem abgeschlossenen Onboarding übernommen.`
                  : `Für ${selectedProcessType?.name ?? "diesen Vorgang"} werden jetzt die Stammdaten der neuen Person erfasst.`}
              </p>
            </section>

            <div className="content-stack">
              {requiresTargetPerson ? (
                <TargetPersonSelection
                  processTypeName={selectedProcessType?.name ?? "den Vorgang"}
                  searchValue={completedOnboardingSearch}
                  onSearchChange={setCompletedOnboardingSearch}
                  completedOnboardings={completedOnboardings}
                  selectedWorkflowUid={selectedCompletedOnboarding?.workflowUid ?? null}
                  selectedOnboarding={selectedCompletedOnboarding}
                  isLoading={completedOnboardingsLoading}
                  error={completedOnboardingsError}
                  selectionError={targetPersonSelectionError}
                  onSelectOnboarding={(onboarding) => setSelectedCompletedOnboarding(onboarding)}
                />
              ) : (
                <>
                  <EmployeeForm value={employee} onChange={setEmployeeField} fieldErrors={employeeFieldErrors} />

                  <RoleSelection
                    roles={roles}
                    departments={departments}
                    selectedDepartmentId={selectedDepartmentId}
                    selectedRoleId={selectedRoleId}
                    isLoading={rolesLoading}
                    error={rolesError}
                    departmentError={departmentError}
                    roleError={roleError}
                    onDepartmentChange={setSelectedDepartment}
                    onRoleChange={setSelectedRole}
                    onRetry={reloadRoles}
                  />

                  {!rolesLoading && !rolesError && selectedDepartmentId !== null && availableRoles.length === 0 ? (
                    <section className="panel panel-muted" role="status" aria-live="polite">
                      <h3 className="panel-title">In dieser Abteilung ist keine Stelle hinterlegt.</h3>
                      <p className="panel-text">
                        Bitte wählen Sie eine andere Abteilung oder prüfen Sie die hinterlegten Stammdaten.
                      </p>
                    </section>
                  ) : null}
                </>
              )}

              <section className="panel panel-muted">
                <h3 className="panel-title">Prozesskonfiguration</h3>
                {workflowConfigLoading ? (
                  <p className="panel-text">Prozesstyp-spezifische Anforderungen werden geladen...</p>
                ) : workflowConfigError ? (
                  <p className="panel-text text-error">{workflowConfigError}</p>
                ) : (
                  <p className="panel-text">
                    {workflowConfig?.requirements.length ?? 0} Anforderungen geladen, {roleRecommendationCount} Vorbelegungen für{" "}
                    {selectedProcessType?.name ?? "diesen Vorgang"} vorbereitet.
                  </p>
                )}
              </section>

              {hasDerivedContextGap ? (
                <section className="panel panel-muted" role="status" aria-live="polite">
                  <h3 className="panel-title">Kontext der Zielperson ist unvollständig</h3>
                  <p className="panel-text">
                    Für die gewählte Person fehlen aktuelle Angaben zu Abteilung, Stelle, Personalnummer oder Kartennummer. Bitte
                    pflegen Sie zuerst einen bestehenden Vorgang oder die zugehörigen Stammdaten.
                  </p>
                </section>
              ) : null}

              {contextStepIssues.length > 0 ? (
                <section className="panel panel-muted" role="status" aria-live="polite">
                  <h3 className="panel-title">Vor Schritt 3 bitte noch prüfen</h3>
                  <ul className="validation-list">
                    {contextStepIssues.map((issue) => (
                      <li key={issue}>{issue}</li>
                    ))}
                  </ul>
                </section>
              ) : (
                <section className="panel panel-success" role="status" aria-live="polite">
                  <h3 className="panel-title">Kontext vollständig</h3>
                  <p className="panel-text">Alle erforderlichen Angaben für Schritt 3 sind vorhanden.</p>
                </section>
              )}

              <section className="panel">
                <div className="wizard-actions">
                  <button type="button" className="btn btn-secondary" onClick={goToProcessStep}>
                    Zurück zu Schritt 1
                  </button>
                  <button
                    type="button"
                    className="btn btn-primary"
                    disabled={!canGoToReviewStep}
                    onClick={goToReviewStep}
                  >
                    Weiter zu Schritt 3
                  </button>
                </div>
              </section>
            </div>
          </>
        ) : null}

        {currentStep === "review" ? (
          <div className="content-stack">
            <section className="panel">
              <div className="panel-head">
                <h2>Schritt 3: Prüfen und anlegen</h2>
                <p>Prüfen Sie den gewählten Vorgang und den erfassten Kontext, bevor Sie den Workflow anlegen.</p>
              </div>

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
                    <div>
                      <dt>Beschreibung</dt>
                      <dd>{selectedProcessType?.description?.trim() || "Keine Beschreibung hinterlegt."}</dd>
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
                          ? selectedCompletedOnboarding?.displayName ?? "-"
                          : `${employee.firstName} ${employee.lastName}`.trim() || "-"}
                      </dd>
                    </div>
                    <div>
                      <dt>Personalnummer</dt>
                      <dd>{requiresTargetPerson ? selectedCompletedOnboarding?.employeeNumber ?? "-" : employee.employeeNumber || "-"}</dd>
                    </div>
                    <div>
                      <dt>Kartennummer</dt>
                      <dd>{requiresTargetPerson ? selectedCompletedOnboarding?.badgeNumber ?? "-" : employee.badgeNumber || "-"}</dd>
                    </div>
                    <div>
                      <dt>Deadline</dt>
                      <dd>{employee.deadlineDate || "Keine Deadline gesetzt"}</dd>
                    </div>
                    {requiresTargetPerson ? (
                      <div>
                        <dt>Quell-Onboarding</dt>
                        <dd>{selectedCompletedOnboarding?.workflowUid ?? "-"}</dd>
                      </div>
                    ) : null}
                  </dl>
                </div>

                <div className="panel panel-muted">
                  <h3 className="panel-title">Zuordnung</h3>
                  <dl className="workflow-kv-grid">
                    <div>
                      <dt>{reviewDepartmentLabel}</dt>
                      <dd>{requiresTargetPerson ? selectedCompletedOnboarding?.departmentName ?? "-" : selectedDepartment?.name ?? "-"}</dd>
                    </div>
                    <div>
                      <dt>{reviewRoleLabel}</dt>
                      <dd>{requiresTargetPerson ? selectedCompletedOnboarding?.roleName ?? "-" : selectedRole?.name ?? "-"}</dd>
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
              <section className="panel panel-muted" role="status" aria-live="polite">
                <h3 className="panel-title">Vorgang konnte nicht gestartet werden.</h3>
                <p className="panel-text">{submitError}</p>
              </section>
            ) : null}

            {submitSuccessMessage ? (
              <section className="panel panel-success" role="status" aria-live="polite">
                <h3 className="panel-title">Vorgang erfolgreich gestartet.</h3>
                <p className="panel-text">{submitSuccessMessage}</p>
                {createdWorkflowUid ? (
                  <p className="panel-note">
                    <Link className="btn btn-secondary" to="/workflows">
                      Zur Übersicht
                    </Link>
                  </p>
                ) : null}
              </section>
            ) : null}

            <section className="panel">
              {canSubmit ? (
                <p className="panel-note">Alles bereit. Beim Anlegen wird der Vorgang sofort mit dem gezeigten Kontext gestartet.</p>
              ) : null}
              <div className="wizard-actions">
                <button type="button" className="btn btn-secondary" onClick={goToContextStep}>
                  Zurück zu Schritt 2
                </button>
                <CreateWorkflowButton
                  isLoading={submitState === "loading"}
                  disabled={!canSubmit || submitState === "success"}
                  label={isHrEntry ? "Vorgang anlegen" : "Änderung anlegen"}
                  onSubmit={submitWorkflow}
                />
              </div>
            </section>
          </div>
        ) : null}
      </div>
    </main>
  );
}
