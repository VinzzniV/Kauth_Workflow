import type { AdminDepartmentAssignment, AdminResponsibilityOwner } from "../../types/auth";
import { useAdminTaskTemplateManagement } from "../../hooks/useAdminTaskTemplateManagement";
import { AdminTaskTemplateConditionsPanel } from "./AdminTaskTemplateConditionsPanel";
import { AdminTaskTemplateDependenciesPanel } from "./AdminTaskTemplateDependenciesPanel";
import { AdminTaskTemplateEditor } from "./AdminTaskTemplateEditor";
import { AdminTaskTemplateSidebar } from "./AdminTaskTemplateSidebar";

type AdminTaskTemplateSectionProps = {
  departments: AdminDepartmentAssignment[];
  responsibilities: AdminResponsibilityOwner[];
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
};

export function AdminTaskTemplateSection({
  departments,
  responsibilities,
  onNotice,
  onError,
}: AdminTaskTemplateSectionProps) {
  const management = useAdminTaskTemplateManagement({
    onNotice,
    onError,
  });

  const panelTitle = management.isCreatingNew
    ? "Neue Aufgabenvorlage"
    : management.selectedTemplate
      ? `Aufgabenvorlage bearbeiten: ${management.selectedTemplate.title}`
      : "Aufgabenvorlage auswählen";

  return (
    <div className="content-stack">
      <div className="master-detail-layout">
        <AdminTaskTemplateSidebar
          workflowDefinitions={management.workflowDefinitions}
          selectedWorkflowDefinitionId={management.selectedWorkflowDefinitionId}
          templates={management.templates}
          selectedTemplateId={management.selectedTemplate?.id ?? null}
          isCreatingNew={management.isCreatingNew}
          isLoadingProcessTypes={management.isLoadingProcessTypes}
          isLoadingTemplates={management.isLoadingTemplates}
          isSaving={management.isSaving}
          isDeleting={management.isDeleting}
          onSelectWorkflowDefinition={management.selectWorkflowDefinition}
          onStartCreatingTemplate={management.startCreatingTemplate}
          onSelectTemplate={management.selectTemplate}
        />

        <div className="content-stack admin-detail-main master-detail-main">
          <AdminTaskTemplateEditor
            panelTitle={panelTitle}
            selectedWorkflowDefinitionId={management.selectedWorkflowDefinitionId}
            selectedTemplate={management.selectedTemplate}
            isCreatingNew={management.isCreatingNew}
            draft={management.draft}
            departments={departments}
            responsibilities={responsibilities}
            isSaving={management.isSaving}
            isDeleting={management.isDeleting}
            onUpdateDraft={management.updateDraft}
            onCreateTemplate={management.createTemplate}
            onSaveTemplate={management.saveTemplate}
            onRemoveTemplate={management.removeTemplate}
          />

          {management.selectedTemplate && !management.isCreatingNew ? (
            <AdminTaskTemplateDependenciesPanel
              selectedTemplate={management.selectedTemplate}
              templates={management.templates}
              dependencyGraph={management.dependencyGraph}
              dependencies={management.dependencies}
              isLoadingDependencyGraph={management.isLoadingDependencyGraph}
              isLoadingTemplates={management.isLoadingTemplates}
              isLoadingDependencies={management.isLoadingDependencies}
              isSavingDependency={management.isSavingDependency}
              deletingDependencyId={management.deletingDependencyId}
              onSelectTemplate={management.selectTemplate}
              onCreateDependencyFromGraph={management.createDependencyFromGraph}
              onRemoveDependencyFromGraph={management.removeDependencyFromGraph}
              onRemoveDependency={management.removeDependency}
            />
          ) : null}

          {management.selectedTemplate && !management.isCreatingNew ? (
            <AdminTaskTemplateConditionsPanel
              groupedConditions={management.groupedConditions}
              conditionDraft={management.conditionDraft}
              answerDefinitions={management.answerDefinitions}
              isLoadingConditions={management.isLoadingConditions}
              isSavingCondition={management.isSavingCondition}
              deletingConditionId={management.deletingConditionId}
              onUpdateConditionDraft={management.updateConditionDraft}
              onAddConditionGroup={management.addConditionGroup}
              onAddCondition={management.addCondition}
              onRemoveCondition={management.removeCondition}
            />
          ) : null}
        </div>
      </div>
    </div>
  );
}
