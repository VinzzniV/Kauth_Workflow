import type { AdminResponsibilityOwner, AdminWorkflowActionDefinition, AdminWorkflowValidationIssue } from "../../types/auth";
import type { WorkflowBuilderNodeDraft, WorkflowBuilderVersionDraft } from "../../hooks/adminWorkflowBuilderModel";
import { BuilderInspectorPanel } from "./BuilderInspectorPanel";
import { BuilderPalettePanel } from "./BuilderPalettePanel";

type WorkflowBuilderSidebarProps = {
  selectedNode: WorkflowBuilderNodeDraft | null;
  availableNodes: Array<{ key: string; label: string }>;
  versionDraft: WorkflowBuilderVersionDraft;
  actionDefinitions: AdminWorkflowActionDefinition[];
  responsibilityOwners: AdminResponsibilityOwner[];
  canManageAdvanced: boolean;
  hasVersionSelected: boolean;
  localValidationIssues: string[];
  serverValidationIssues: AdminWorkflowValidationIssue[];
  onAddNode: (nodeType: WorkflowBuilderNodeDraft["nodeType"]) => void;
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
  availableNodes,
  versionDraft,
  actionDefinitions,
  responsibilityOwners,
  canManageAdvanced,
  hasVersionSelected,
  localValidationIssues,
  serverValidationIssues,
  onAddNode,
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
          <BuilderInspectorPanel
            selectedNode={selectedNode}
            availableNodes={availableNodes}
            versionDraft={versionDraft}
            actionDefinitions={actionDefinitions}
            responsibilityOwners={responsibilityOwners}
            canManageAdvanced={canManageAdvanced}
            onUpdateNode={onUpdateNode}
            onUpdateEdge={onUpdateEdge}
            onRemoveEdge={onRemoveEdge}
            onAddActionFromDefinition={onAddActionFromDefinition}
            onUpdateAction={onUpdateAction}
            onRemoveAction={onRemoveAction}
          />
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
