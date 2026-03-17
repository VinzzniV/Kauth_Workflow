// HR startet hier einen neuen Onboarding-Fall und uebergibt ihn danach in den Fachprozess.
import CreateWorkflowButton from "../components/workflows/CreateWorkflowButton";
import { Link } from "react-router-dom";
import PageHeader from "../components/layout/PageHeader";
import EmployeeForm from "../components/workflows/EmployeeForm";
import RoleSelection from "../components/workflows/RoleSelection";
import { useWorkflowCreation } from "../hooks/useWorkflowCreation";

export default function CreateWorkflowPage() {
  const {
    employee,
    selectedDepartmentId,
    selectedRoleId,
    roles,
    departments,
    availableRoles,
    rolesLoading,
    rolesError,
    submitState,
    submitError,
    submitSuccessMessage,
    createdWorkflowUid,
    setEmployeeField,
    setSelectedDepartment,
    setSelectedRole,
    submitWorkflow,
    canSubmit,
    reloadRoles,
  } = useWorkflowCreation();

  // Die Seite bleibt bewusst schlank und delegiert Fachlogik an den Workflow-Hook.
  return (
    <main className="onboarding-shell">
      <div className="page-container">
        <PageHeader
          title="Neues Onboarding"
          description="Legen Sie ein neues Onboarding in klaren, nachvollziehbaren Schritten an."
        />

        <section className="panel panel-intro">
          <h2>Neue Person anlegen</h2>
          <p>
            HR erfasst hier nur die Stammdaten. Der konkrete Bedarf wird danach separat durch die Abteilungsleitung
            abgestimmt.
          </p>
        </section>

        <div className="content-stack">
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

          {submitError ? (
            <section className="panel panel-muted" role="status" aria-live="polite">
              <h3 className="panel-title">Onboarding konnte nicht gestartet werden.</h3>
              <p className="panel-text">{submitError}</p>
            </section>
          ) : null}

          {submitSuccessMessage ? (
            <section className="panel panel-success" role="status" aria-live="polite">
              <h3 className="panel-title">Onboarding erfolgreich gestartet.</h3>
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
      </div>
    </main>
  );
}
