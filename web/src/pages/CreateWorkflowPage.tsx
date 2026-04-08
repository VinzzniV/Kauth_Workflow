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
    selectedWorkflowDefinition: workflowCreation.selectedWorkflowDefinition,
    selectedWorkflowDefinitionKey: workflowCreation.selectedWorkflowDefinitionKey,
    workflowDefinitions: workflowCreation.workflowDefinitions,
    requiresTargetPerson: workflowCreation.requiresTargetPerson,
    employee: workflowCreation.employee,
    selectedDepartmentId: workflowCreation.selectedDepartmentId,
    selectedRoleId: workflowCreation.selectedRoleId,
    selectedTargetPersonSource: workflowCreation.selectedTargetPersonSource,
    targetPersonSourcesLoading: workflowCreation.targetPersonSourcesLoading,
    targetPersonSourcesError: workflowCreation.targetPersonSourcesError,
    rolesLoading: workflowCreation.rolesLoading,
    rolesError: workflowCreation.rolesError,
    availableRoles: workflowCreation.availableRoles,
    workflowConfig: workflowCreation.workflowConfig,
  });

  return (
    <main className="app-shell">
      <div className="page-container">
        <PageHeader
          variant="workspace"
          title={view.pageTitle}
        />

        <WorkflowCreationStepper steps={view.steps} currentStepIndex={view.currentStepIndex} />

        {workflowCreation.currentStep === "process" ? (
          <CreateWorkflowProcessStep
            workflowDefinitionsLoading={workflowCreation.workflowDefinitionsLoading}
            workflowDefinitions={workflowCreation.workflowDefinitions}
            selectedWorkflowDefinitionKey={workflowCreation.selectedWorkflowDefinitionKey}
            canGoToContextStep={workflowCreation.canGoToContextStep}
            requiresTargetPerson={workflowCreation.requiresTargetPerson}
            hasAttemptedProcessNext={hasAttemptedProcessNext}
            processStepIssues={view.processStepIssues}
            onSelectWorkflowDefinition={workflowCreation.setWorkflowDefinition}
            onGoToContextStep={workflowCreation.goToContextStep}
            onAttemptBlockedNext={() => setHasAttemptedProcessNext(true)}
          />
        ) : null}

        {workflowCreation.currentStep === "context" ? (
          <CreateWorkflowContextStep
            contextStepTitle={view.contextStepTitle}
            requiresTargetPerson={workflowCreation.requiresTargetPerson}
            selectedWorkflowDefinition={workflowCreation.selectedWorkflowDefinition}
            targetPersonSourceSearch={workflowCreation.targetPersonSourceSearch}
            targetPersonSources={workflowCreation.targetPersonSources}
            selectedTargetPersonSource={workflowCreation.selectedTargetPersonSource}
            targetPersonSourcesLoading={workflowCreation.targetPersonSourcesLoading}
            targetPersonSourcesError={workflowCreation.targetPersonSourcesError}
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
            onSearchChange={workflowCreation.setTargetPersonSourceSearch}
            onSelectTargetPersonSource={workflowCreation.setSelectedTargetPersonSource}
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
            selectedWorkflowDefinition={workflowCreation.selectedWorkflowDefinition}
            selectedTargetPersonSource={workflowCreation.selectedTargetPersonSource}
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
