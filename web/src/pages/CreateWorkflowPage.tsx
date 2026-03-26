// HR startet hier einen neuen Vorgang und uebergibt ihn danach in den Fachprozess.
import { Link } from "react-router-dom";
import CreateWorkflowButton from "../components/workflows/CreateWorkflowButton";
import EmployeeForm from "../components/workflows/EmployeeForm";
import TargetPersonSelection from "../components/workflows/TargetPersonSelection";
import RoleSelection from "../components/workflows/RoleSelection";
import PageHeader from "../components/layout/PageHeader";
import { useWorkflowCreation } from "../hooks/useWorkflowCreation";

export default function CreateWorkflowPage() {
  const {
    processTypes,
    processTypesLoading,
    selectedProcessTypeKey,
    employee,
    selectedDepartmentId,
    selectedRoleId,
    selectedTargetPerson,
    targetPersonSearch,
    targetPeople,
    targetPeopleLoading,
    targetPeopleError,
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
    linkableWorkflows,
    selectedSourceWorkflowUid,
    setProcessType,
    setEmployeeField,
    setSelectedDepartment,
    setSelectedRole,
    setTargetPersonSearch,
    setSelectedTargetPerson,
    setSelectedSourceWorkflow,
    submitWorkflow,
    canSubmit,
    reloadRoles,
  } = useWorkflowCreation();

  const selectedProcessType = processTypes.find((pt) => pt.key === selectedProcessTypeKey) ?? null;
  const requiresTargetPerson = selectedProcessType?.requiresTargetPerson ?? false;
  const roleRecommendationCount =
    (workflowConfig?.roleRecommendations.defaultValues.length ?? 0) +
    (workflowConfig?.roleRecommendations.defaultSelectedOptions.length ?? 0);
  const hasDerivedContextGap = Boolean(
    requiresTargetPerson &&
    selectedTargetPerson &&
    (!selectedTargetPerson.departmentId ||
      !selectedTargetPerson.roleId ||
      !selectedTargetPerson.employeeNumber ||
      !selectedTargetPerson.badgeNumber)
  );

  return (
    <main className="app-shell">
      <div className="page-container">
        <PageHeader
          title="Neuer Vorgang"
          description="Legen Sie einen neuen Vorgang in klaren, nachvollziehbaren Schritten an."
        />

        {!processTypesLoading && processTypes.length > 1 ? (
          <section className="panel">
            <h2>Prozesstyp wählen</h2>
            <div className="process-type-grid">
              {processTypes.map((processType) => (
                <button
                  key={processType.key}
                  type="button"
                  className={`process-type-card${selectedProcessTypeKey === processType.key ? " process-type-card--selected" : ""}`}
                  onClick={() => setProcessType(processType.key)}
                >
                  <span className="process-type-card__name">{processType.name}</span>
                </button>
              ))}
            </div>
          </section>
        ) : null}

        {selectedProcessTypeKey ? (
          <>
            <section className="panel panel-intro">
              <h2>{requiresTargetPerson ? "Bestehende Person auswählen" : "Neue Person anlegen"}</h2>
              <p>
                HR erfasst hier
                {requiresTargetPerson ? " die Zielperson und den bestehenden Kontext" : " nur die Stammdaten"}
                {selectedProcessType ? ` für das ${selectedProcessType.name}` : ""}. Der konkrete Bedarf wird danach im zuständigen
                Prozessschritt weiterbearbeitet.
              </p>
            </section>

            <div className="content-stack">
              {requiresTargetPerson ? (
                <TargetPersonSelection
                  processTypeName={selectedProcessType?.name ?? "den Vorgang"}
                  searchValue={targetPersonSearch}
                  onSearchChange={setTargetPersonSearch}
                  people={targetPeople}
                  selectedPersonId={selectedTargetPerson?.personId ?? null}
                  selectedPerson={selectedTargetPerson}
                  isLoading={targetPeopleLoading}
                  error={targetPeopleError}
                  onSelectPerson={setSelectedTargetPerson}
                />
              ) : (
                <>
                  <EmployeeForm value={employee} onChange={setEmployeeField} />

                  <RoleSelection
                    roles={roles}
                    departments={departments}
                    selectedDepartmentId={selectedDepartmentId}
                    selectedRoleId={selectedRoleId}
                    isLoading={rolesLoading}
                    error={rolesError}
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

              {linkableWorkflows.length > 0 ? (
                <section className="panel">
                  <h3>Bestehende Vorgänge für diese Personalnummer</h3>
                  <p className="text-muted">
                    Wählen Sie einen Quell-Vorgang aus, um diesen {selectedProcessType?.name ?? "Vorgang"} automatisch zu verknüpfen.
                    Anforderungen können daraus abgeleitet werden.
                  </p>
                  <div style={{ marginTop: "0.75rem" }}>
                    {linkableWorkflows.map((workflow) => (
                      <label
                        key={workflow.uid}
                        style={{ display: "flex", alignItems: "center", gap: "0.5rem", marginBottom: "0.5rem", cursor: "pointer" }}
                      >
                        <input
                          type="radio"
                          name="sourceWorkflow"
                          checked={selectedSourceWorkflowUid === workflow.uid}
                          onChange={() => setSelectedSourceWorkflow(workflow.uid)}
                        />
                        <span>
                          <strong>{workflow.processType.name}</strong> - {workflow.firstName} {workflow.lastName} - {workflow.departmentName} -{" "}
                          {workflow.workflowStatus} ({new Date(workflow.createdAt).toLocaleDateString("de-DE")})
                        </span>
                      </label>
                    ))}
                    <label style={{ display: "flex", alignItems: "center", gap: "0.5rem", marginBottom: "0.5rem", cursor: "pointer" }}>
                      <input
                        type="radio"
                        name="sourceWorkflow"
                        checked={selectedSourceWorkflowUid === null}
                        onChange={() => setSelectedSourceWorkflow(null)}
                      />
                      <span className="text-muted">Keine Verknüpfung</span>
                    </label>
                  </div>
                </section>
              ) : null}

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

              <CreateWorkflowButton
                isLoading={submitState === "loading"}
                disabled={!canSubmit}
                onSubmit={submitWorkflow}
              />
            </div>
          </>
        ) : null}
      </div>
    </main>
  );
}
