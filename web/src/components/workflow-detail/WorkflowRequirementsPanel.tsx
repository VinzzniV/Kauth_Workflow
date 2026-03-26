import RequirementsSelection from "../workflows/RequirementsSelection";
import type {
  RequirementSelectionState,
  WorkflowDetail,
} from "../../types/workflow";
import { hasSupervisorStep } from "./workflowDetailModel";

type WorkflowRequirementsPanelProps = {
  workflow: WorkflowDetail;
  canEditSupervisorRequirements: boolean;
  requirementSelections: Record<number, RequirementSelectionState>;
  requirementsSaveError: string | null;
  requirementsSaveNotice: string | null;
  isSavingRequirements: boolean;
  canSaveSupervisorRequirements: boolean;
  onToggleBoolean: (requirementId: number, value: boolean | null) => void;
  onTextChange: (requirementId: number, value: string) => void;
  onSelectOption: (requirementId: number, optionId: number | null) => void;
  onToggleMultiOption: (requirementId: number, optionId: number) => void;
  onSave: () => void | Promise<void>;
};

function getRequirementsDescription(
  workflow: WorkflowDetail,
  canEditSupervisorRequirements: boolean
): string {
  if (canEditSupervisorRequirements) {
    return "Admin-Override: Die reguläre Bearbeitung liegt in dieser Phase bei der zuständigen Abteilungsleitung.";
  }

  if (workflow.workflowStatus === "draft") {
    return hasSupervisorStep(workflow)
      ? "Die Anforderungen werden im nächsten Schritt regulär durch die zuständige Abteilungsleitung festgelegt."
      : "Die Anforderungen sind die Grundlage für die nachfolgenden Aufgaben dieses Vorgangs.";
  }

  if (workflow.workflowStatus === "waiting_for_supervisor") {
    return "In dieser Phase werden die Anforderungen regulär durch die zuständige Abteilungsleitung gepflegt. Diese Ansicht ist nur lesend.";
  }

  return `Gespeicherte Anforderungen für diesen ${workflow.processType.name}-Vorgang.`;
}

export default function WorkflowRequirementsPanel({
  workflow,
  canEditSupervisorRequirements,
  requirementSelections,
  requirementsSaveError,
  requirementsSaveNotice,
  isSavingRequirements,
  canSaveSupervisorRequirements,
  onToggleBoolean,
  onTextChange,
  onSelectOption,
  onToggleMultiOption,
  onSave,
}: WorkflowRequirementsPanelProps) {
  if (workflow.requirements.length === 0) {
    return null;
  }

  return (
    <section className="panel panel-muted">
      <RequirementsSelection
        requirements={workflow.requirements}
        mode={canEditSupervisorRequirements ? "edit" : "view"}
        selections={canEditSupervisorRequirements ? requirementSelections : undefined}
        onToggleBoolean={canEditSupervisorRequirements ? onToggleBoolean : undefined}
        onTextChange={canEditSupervisorRequirements ? onTextChange : undefined}
        onSelectOption={canEditSupervisorRequirements ? onSelectOption : undefined}
        onToggleMultiOption={canEditSupervisorRequirements ? onToggleMultiOption : undefined}
        isLoading={false}
        error={null}
        title="Anforderungen"
        description={getRequirementsDescription(workflow, canEditSupervisorRequirements)}
      />

      {requirementsSaveError ? <p className="panel-note">{requirementsSaveError}</p> : null}
      {requirementsSaveNotice ? <p className="panel-note">{requirementsSaveNotice}</p> : null}

      {canEditSupervisorRequirements ? (
        <div className="action-row">
          <button
            type="button"
            className="btn btn-secondary"
            disabled={!canSaveSupervisorRequirements}
            onClick={() => {
              void onSave();
            }}
          >
            {isSavingRequirements ? "Speichern..." : "Anforderungen per Admin-Override abschließen"}
          </button>
        </div>
      ) : null}
    </section>
  );
}
