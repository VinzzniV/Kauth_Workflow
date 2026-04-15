import type {
  AdminAnswerDefinition,
  AdminProcessType,
  AdminResponsibilityOwner,
  AdminTaskTemplate,
  AdminTaskTemplateCondition,
  AdminTaskTemplateDependency,
  AdminWorkflowActionDefinition,
  AdminWorkflowValidationIssue,
} from "../../types/auth";
import type { WorkflowBuilderNodeDraft, WorkflowBuilderVersionDraft } from "../../hooks/adminWorkflowBuilderModel";
import type { BuilderInspectorFocusTarget } from "../../hooks/useAdminWorkflowBuilder";
import { BuilderInspectorFocusPanel } from "./BuilderInspectorFocusPanel";
import { BuilderInspectorPanel } from "./BuilderInspectorPanel";
import { BuilderPalettePanel } from "./BuilderPalettePanel";

type WorkflowBuilderSidebarProps = {
  selectedNode: WorkflowBuilderNodeDraft | null;
  inspectorFocus: BuilderInspectorFocusTarget;
  availableNodes: Array<{ key: string; label: string }>;
  versionDraft: WorkflowBuilderVersionDraft;
  actionDefinitions: AdminWorkflowActionDefinition[];
  processTypes: AdminProcessType[];
  responsibilityOwners: AdminResponsibilityOwner[];
  taskTemplates: AdminTaskTemplate[];
  answerDefinitions: AdminAnswerDefinition[];
  taskTemplateConditions: AdminTaskTemplateCondition[];
  taskTemplateDependencies: AdminTaskTemplateDependency[];
  canManageAdvanced: boolean;
  hasVersionSelected: boolean;
  localValidationIssues: string[];
  serverValidationIssues: AdminWorkflowValidationIssue[];
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
  onAddNode: (nodeType: WorkflowBuilderNodeDraft["nodeType"]) => void;
  onOpenInspectorFocus: (focus: BuilderInspectorFocusTarget) => void;
  onCloseInspectorFocus: () => void;
  onRefreshReferenceData: () => void | Promise<void>;
  onUpdateNode: (nodeId: string, patch: Partial<WorkflowBuilderNodeDraft>) => void;
  onUpdateEdge: (edgeId: string, patch: Partial<WorkflowBuilderVersionDraft["edges"][number]>) => void;
  onRemoveEdge: (edgeId: string) => void;
  onAddActionFromDefinition: (nodeId: string, actionKey: string) => void;
  onUpdateAction: (
    nodeId: string,
    actionId: string,
    patch: Partial<WorkflowBuilderVersionDraft["nodes"][number]["actions"][number]>
  ) => void;
  onRemoveAction: (nodeId: string, actionId: string) => void;
};

export function WorkflowBuilderSidebar({
  selectedNode,
  inspectorFocus,
  availableNodes,
  versionDraft,
  actionDefinitions,
  processTypes,
  responsibilityOwners,
  taskTemplates,
  answerDefinitions,
  taskTemplateConditions,
  taskTemplateDependencies,
  canManageAdvanced,
  hasVersionSelected,
  localValidationIssues,
  serverValidationIssues,
  onNotice,
  onError,
  onAddNode,
  onOpenInspectorFocus,
  onCloseInspectorFocus,
  onRefreshReferenceData,
  onUpdateNode,
  onUpdateEdge,
  onRemoveEdge,
  onAddActionFromDefinition,
  onUpdateAction,
  onRemoveAction,
}: WorkflowBuilderSidebarProps) {
  return (
    <aside className="builder-sidebar-shell" aria-label="Workflow-Seitenleiste">
      <div className="content-stack">
        {selectedNode ? (
          inspectorFocus.mode !== "none" ? (
            <BuilderInspectorFocusPanel
              focus={inspectorFocus}
              processTypes={processTypes}
              responsibilityOwners={responsibilityOwners}
              onClose={onCloseInspectorFocus}
              onNotice={onNotice}
              onError={onError}
              onDataChanged={onRefreshReferenceData}
            />
          ) : (
            <BuilderInspectorPanel
              selectedNode={selectedNode}
              availableNodes={availableNodes}
              versionDraft={versionDraft}
              actionDefinitions={actionDefinitions}
              processTypes={processTypes}
              responsibilityOwners={responsibilityOwners}
              taskTemplates={taskTemplates}
              answerDefinitions={answerDefinitions}
              taskTemplateConditions={taskTemplateConditions}
              taskTemplateDependencies={taskTemplateDependencies}
              canManageAdvanced={canManageAdvanced}
              onOpenInspectorFocus={onOpenInspectorFocus}
              onUpdateNode={onUpdateNode}
              onUpdateEdge={onUpdateEdge}
              onRemoveEdge={onRemoveEdge}
              onAddActionFromDefinition={onAddActionFromDefinition}
              onUpdateAction={onUpdateAction}
              onRemoveAction={onRemoveAction}
            />
          )
        ) : (
          <BuilderPalettePanel
            canManageAdvanced={canManageAdvanced}
            hasVersionSelected={hasVersionSelected}
            onAddNode={onAddNode}
          />
        )}

        <section className="builder-sidebar-panel content-stack" aria-label="Prüfung">
          <div className="builder-sidebar-panel__header">
            <span className="builder-sidebar-panel__eyebrow">Prüfung</span>
            <h3>Hinweise zum aktuellen Stand</h3>
            <p className="text-muted">
              Hinweise bleiben sichtbar, stehen aber bewusst hinter den Bausteinen und Eigenschaften.
            </p>
          </div>

          <div className="builder-validation-grid">
            <div className="builder-validation-card">
              <span>Lokal</span>
              <strong>{localValidationIssues.length}</strong>
            </div>
            <div className="builder-validation-card">
              <span>Server</span>
              <strong>{serverValidationIssues.length}</strong>
            </div>
          </div>

          <details className="builder-sidebar-disclosure">
            <summary>Details anzeigen</summary>
            <div className="content-stack">
              {localValidationIssues.length > 0 ? (
                <div className="panel panel-error" role="alert">
                  <p className="panel-text text-error">Lokale Prüfung</p>
                  <ul>
                    {localValidationIssues.map((issue) => (
                      <li key={issue}>{issue}</li>
                    ))}
                  </ul>
                </div>
              ) : (
                <div className="panel panel-info">
                  <p className="panel-text">Lokal wurden keine Probleme gefunden.</p>
                </div>
              )}

              {serverValidationIssues.length > 0 ? (
                <div className="panel panel-warning">
                  <p className="panel-text">Server-Hinweise</p>
                  <ul>
                    {serverValidationIssues.map((issue) => (
                      <li key={`${issue.code}-${issue.message}`}>
                        [{issue.scope}] {issue.message}
                      </li>
                    ))}
                  </ul>
                </div>
              ) : (
                <div className="panel panel-info">
                  <p className="panel-text">Keine Server-Hinweise geladen.</p>
                </div>
              )}
            </div>
          </details>
        </section>
      </div>
    </aside>
  );
}
