import { useState } from "react";
import { useCurrentUser } from "../auth/useCurrentUser";
import PageHeader from "../components/layout/PageHeader";
import { useWorkflowCreation } from "../hooks/useWorkflowCreation";
import { buildCreateWorkflowPageViewModel } from "./createWorkflowPageModel";
import {
  CreateWorkflowContextStep,
  CreateWorkflowProcessStep,
  CreateWorkflowReviewStep,
  WorkflowCreationStepper,
} from "./CreateWorkflowPageSections";

export default function CreateWorkflowPage() {
  const { capabilities } = useCurrentUser();
  const workflowCreation = useWorkflowCreation();
  const [hasAttemptedProcessNext, setHasAttemptedProcessNext] = useState(false);
  const [hasAttemptedContextNext, setHasAttemptedContextNext] = useState(false);
  const view = buildCreateWorkflowPageViewModel({
    currentStep: workflowCreation.currentStep,
    capabilities,
    selectedProcessType: workflowCreation.selectedProcessType,
    selectedProcessTypeKey: workflowCreation.selectedProcessTypeKey,
    processTypes: workflowCreation.processTypes,
    requiresTargetPerson: workflowCreation.requiresTargetPerson,
    employee: workflowCreation.employee,
    selectedDepartmentId: workflowCreation.selectedDepartmentId,
    selectedRoleId: workflowCreation.selectedRoleId,
    selectedCompletedOnboarding: workflowCreation.selectedCompletedOnboarding,
    completedOnboardingsLoading: workflowCreation.completedOnboardingsLoading,
    completedOnboardingsError: workflowCreation.completedOnboardingsError,
    rolesLoading: workflowCreation.rolesLoading,
    rolesError: workflowCreation.rolesError,
    availableRoles: workflowCreation.availableRoles,
    workflowConfig: workflowCreation.workflowConfig,
  });

  return (
    <main className="app-shell">
      <div className="page-container">
        <PageHeader title={view.pageTitle} />

        <WorkflowCreationStepper steps={view.steps} currentStepIndex={view.currentStepIndex} />

        {workflowCreation.currentStep === "process" ? (
          <CreateWorkflowProcessStep
            processTypesLoading={workflowCreation.processTypesLoading}
            processTypes={workflowCreation.processTypes}
            selectedProcessTypeKey={workflowCreation.selectedProcessTypeKey}
            canGoToContextStep={workflowCreation.canGoToContextStep}
            requiresTargetPerson={workflowCreation.requiresTargetPerson}
            hasAttemptedProcessNext={hasAttemptedProcessNext}
            processStepIssues={view.processStepIssues}
            onSelectProcessType={workflowCreation.setProcessType}
            onGoToContextStep={workflowCreation.goToContextStep}
            onAttemptBlockedNext={() => setHasAttemptedProcessNext(true)}
          />
        ) : null}

        {workflowCreation.currentStep === "context" ? (
          <CreateWorkflowContextStep
            contextStepTitle={view.contextStepTitle}
            requiresTargetPerson={workflowCreation.requiresTargetPerson}
            selectedProcessType={workflowCreation.selectedProcessType}
            completedOnboardingSearch={workflowCreation.completedOnboardingSearch}
            completedOnboardings={workflowCreation.completedOnboardings}
            selectedCompletedOnboarding={workflowCreation.selectedCompletedOnboarding}
            completedOnboardingsLoading={workflowCreation.completedOnboardingsLoading}
            completedOnboardingsError={workflowCreation.completedOnboardingsError}
            targetPersonSelectionError={view.targetPersonSelectionError}
            employee={workflowCreation.employee}
            employeeFieldErrors={view.employeeFieldErrors}
            roles={workflowCreation.roles}
            departments={workflowCreation.departments}
            selectedDepartmentId={workflowCreation.selectedDepartmentId}
            selectedRoleId={workflowCreation.selectedRoleId}
            rolesLoading={workflowCreation.rolesLoading}
            rolesError={workflowCreation.rolesError}
            departmentError={view.departmentError}
            roleError={view.roleError}
            availableRoles={workflowCreation.availableRoles}
            hasDerivedContextGap={view.hasDerivedContextGap}
            hasAttemptedContextNext={hasAttemptedContextNext}
            contextStepIssues={view.contextStepIssues}
            onSearchChange={workflowCreation.setCompletedOnboardingSearch}
            onSelectOnboarding={workflowCreation.setSelectedCompletedOnboarding}
            onEmployeeChange={workflowCreation.setEmployeeField}
            onDepartmentChange={workflowCreation.setSelectedDepartment}
            onRoleChange={workflowCreation.setSelectedRole}
            onRetryRoles={() => void workflowCreation.reloadRoles()}
            onGoBack={workflowCreation.goToProcessStep}
            onGoReview={workflowCreation.goToReviewStep}
            canGoToReviewStep={workflowCreation.canGoToReviewStep}
            onAttemptBlockedNext={() => setHasAttemptedContextNext(true)}
          />
        ) : null}

        {workflowCreation.currentStep === "review" ? (
          <CreateWorkflowReviewStep
            requiresTargetPerson={workflowCreation.requiresTargetPerson}
            selectedProcessType={workflowCreation.selectedProcessType}
            selectedCompletedOnboarding={workflowCreation.selectedCompletedOnboarding}
            employee={workflowCreation.employee}
            selectedDepartment={workflowCreation.selectedDepartment}
            selectedRole={workflowCreation.selectedRole}
            workflowConfig={workflowCreation.workflowConfig}
            roleRecommendationCount={view.roleRecommendationCount}
            reviewPersonLabel={view.reviewPersonLabel}
            reviewDepartmentLabel={view.reviewDepartmentLabel}
            reviewRoleLabel={view.reviewRoleLabel}
            submitError={workflowCreation.submitError}
            submitSuccessMessage={workflowCreation.submitSuccessMessage}
            createdWorkflowUid={workflowCreation.createdWorkflowUid}
            submitState={workflowCreation.submitState}
            canSubmit={workflowCreation.canSubmit}
            isHrEntry={capabilities.hasHrRole || capabilities.hasAdminRole}
            onGoBack={workflowCreation.goToContextStep}
            onSubmit={workflowCreation.submitWorkflow}
          />
        ) : null}
      </div>
    </main>
  );
}
