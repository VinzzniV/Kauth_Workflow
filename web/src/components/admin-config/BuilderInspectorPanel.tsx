import { useMemo, useState } from "react";
import type { AdminResponsibilityOwner, AdminWorkflowActionDefinition } from "../../types/auth";
import type { WorkflowBuilderNodeDraft, WorkflowBuilderVersionDraft } from "../../hooks/adminWorkflowBuilderModel";
import { getWorkflowBuilderNodeTypeLabel, WORKFLOW_BUILDER_TECHNICAL_LABELS } from "./workflowBuilderLabels";

type BuilderInspectorPanelProps = {
  selectedNode: WorkflowBuilderNodeDraft;
  availableNodes: Array<{ key: string; label: string }>;
  versionDraft: WorkflowBuilderVersionDraft;
  actionDefinitions: AdminWorkflowActionDefinition[];
  responsibilityOwners: AdminResponsibilityOwner[];
  canManageAdvanced: boolean;
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

const NODE_TYPE_OPTIONS: WorkflowBuilderNodeDraft["nodeType"][] = [
  "start",
  "form",
  "approval",
  "task",
  "decision",
  "automation",
  "end",
];

export function BuilderInspectorPanel({
  selectedNode,
  availableNodes,
  versionDraft,
  actionDefinitions,
  responsibilityOwners,
  canManageAdvanced,
  onUpdateNode,
  onUpdateEdge,
  onRemoveEdge,
  onAddActionFromDefinition,
  onUpdateAction,
  onRemoveAction,
}: BuilderInspectorPanelProps) {
  const [showAdvanced, setShowAdvanced] = useState(false);
  const actionDefinitionsByKey = useMemo(() => {
    return new Map(
      actionDefinitions.map((definition) => [definition.actionKey.trim().toLowerCase(), definition] as const)
    );
  }, [actionDefinitions]);

  const outgoingEdges = useMemo(() => {
    const selectedKey = selectedNode.nodeKey.trim().toLowerCase();
    return versionDraft.edges.filter((edge) => edge.sourceNodeKey.trim().toLowerCase() === selectedKey);
  }, [selectedNode, versionDraft.edges]);

  const summaryText = useMemo(() => {
    switch (selectedNode.nodeType) {
      case "start":
        return "Dieser Schritt startet den Ablauf und fuehrt in die ersten Folgeschritte.";
      case "end":
        return "Dieser Schritt beendet den aktuellen Pfad.";
      case "form":
        return "Hier werden Eingaben gesammelt, die den weiteren Ablauf steuern.";
      case "approval":
        return "Hier holt ihr eine Freigabe fuer den naechsten Schritt ein.";
      case "decision":
        return "Hier verzweigt ihr den Ablauf in unterschiedliche Wege.";
      case "automation":
        return "Dieser Schritt laeuft automatisch und fuehrt hinterlegte Aktionen aus.";
      case "task":
      default:
        return "Dieser Schritt beschreibt eine manuelle Aufgabe im Ablauf.";
    }
  }, [selectedNode]);

  const configuredActions = selectedNode.nodeType === "automation" ? selectedNode.actions : [];
  const isLockedAutomationNode = selectedNode.nodeType === "automation" && !canManageAdvanced;
  const allowedNodeTypeOptions = canManageAdvanced
    ? NODE_TYPE_OPTIONS
    : selectedNode.nodeType === "automation"
      ? (["automation"] as WorkflowBuilderNodeDraft["nodeType"][])
      : NODE_TYPE_OPTIONS.filter((nodeType) => nodeType !== "automation");

  return (
    <section className="builder-sidebar-panel content-stack" aria-label="Eigenschaften">
      <div className="builder-sidebar-panel__header">
        <span className="builder-sidebar-panel__eyebrow">Eigenschaften</span>
        <h3>{selectedNode.title.trim() || selectedNode.nodeKey.trim() || "Neuer Schritt"}</h3>
        <p className="text-muted">{summaryText}</p>
      </div>

      <div className="builder-inspector-hero">
        <span className="badge badge--default">{getWorkflowBuilderNodeTypeLabel(selectedNode.nodeType)}</span>
        <span className="builder-inspector-hero__meta">
          {selectedNode.nodeType === "automation"
            ? "Laeuft automatisch"
            : selectedNode.nodeType === "decision"
              ? "Steuert mehrere Wege"
              : "Teil des Ablaufs"}
        </span>
      </div>

      <section className="content-stack">
        <div>
          <h4>Schritt</h4>
          <p className="text-muted" style={{ margin: 0 }}>
            Die wichtigsten Angaben fuer den ausgewaehlten Ablaufschritt.
          </p>
        </div>
        <label>
          <span>Titel</span>
          <input
            className="form-input"
            value={selectedNode.title}
            onChange={(event) => onUpdateNode(selectedNode.id, { title: event.target.value })}
          />
        </label>
        <label>
          <span>Baustein</span>
          <select
            className="form-select"
            value={selectedNode.nodeType}
            disabled={isLockedAutomationNode}
            onChange={(event) => onUpdateNode(selectedNode.id, {
              nodeType: event.target.value as WorkflowBuilderNodeDraft["nodeType"],
              actions: event.target.value === "automation" ? selectedNode.actions : [],
            })}
          >
            {allowedNodeTypeOptions.map((nodeType) => (
              <option key={nodeType} value={nodeType}>{getWorkflowBuilderNodeTypeLabel(nodeType)}</option>
            ))}
          </select>
        </label>
        {isLockedAutomationNode ? (
          <p className="text-muted" style={{ margin: 0 }}>
            Automatisierungen koennen nur im Admin-Modus veraendert werden.
          </p>
        ) : null}
      </section>

      <section className="content-stack">
        <div>
          <h4>Details</h4>
          <p className="text-muted" style={{ margin: 0 }}>
            Fachliche Angaben fuer diesen Schritt.
          </p>
        </div>
        {renderNodeConfigurationSection(selectedNode, responsibilityOwners, onUpdateNode)}
      </section>

      <section className="content-stack">
        <div>
          <h4>Naechste Schritte</h4>
          <p className="text-muted" style={{ margin: 0 }}>
            Hier bearbeitet ihr, wohin der Ablauf von diesem Schritt weitergeht. Neue Verbindungen legt ihr direkt im Canvas an.
          </p>
        </div>
        {outgoingEdges.length === 0 ? (
          <p className="text-muted">Von diesem Schritt fuehrt aktuell noch kein weiterer Weg ab.</p>
        ) : (
          outgoingEdges.map((edge) => (
            <div key={edge.id} className="panel content-stack">
              <div className="grid-two-columns">
                <label>
                  <span>{WORKFLOW_BUILDER_TECHNICAL_LABELS.targetNode}</span>
                  <select
                    className="form-select"
                    value={edge.targetNodeKey}
                    onChange={(event) => onUpdateEdge(edge.id, { targetNodeKey: event.target.value })}
                  >
                    <option value="">Bitte waehlen</option>
                    {availableNodes.map((node) => (
                      <option key={`sidebar-target-${edge.id}-${node.key}`} value={node.key}>{node.label}</option>
                    ))}
                  </select>
                </label>
                <label>
                  <span>{WORKFLOW_BUILDER_TECHNICAL_LABELS.priority}</span>
                  <input
                    className="form-input"
                    type="number"
                    min="1"
                    value={edge.priority}
                    onChange={(event) => onUpdateEdge(edge.id, { priority: event.target.value })}
                  />
                </label>
              </div>
              <label>
                <span>{WORKFLOW_BUILDER_TECHNICAL_LABELS.conditionExpression}</span>
                <textarea
                  className="form-input"
                  rows={4}
                  value={edge.conditionExpression}
                  onChange={(event) => onUpdateEdge(edge.id, { conditionExpression: event.target.value })}
                />
              </label>
              <button type="button" className="button-danger" onClick={() => onRemoveEdge(edge.id)}>
                Verbindung loeschen
              </button>
            </div>
          ))
        )}
      </section>

      <section className="content-stack">
        <div>
          <h4>Automatische Aktionen</h4>
          <p className="text-muted" style={{ margin: 0 }}>
            Automatische Schritte bleiben sichtbar und nutzen weiterhin den vorhandenen Aktionskatalog.
          </p>
        </div>
        {selectedNode.nodeType !== "automation" ? (
          <div className="panel panel-info">
            <p className="panel-text">
              Automatische Aktionen werden nur auf Schritten vom Typ `{getWorkflowBuilderNodeTypeLabel("automation")}` gepflegt. Im Katalog sind aktuell {actionDefinitions.length} Aktion(en) verfuegbar.
            </p>
          </div>
        ) : (
          <div className="content-stack">
            {!canManageAdvanced ? (
              <div className="panel panel-warning">
                <p className="panel-text">
                  Dieser automatische Schritt ist sichtbar, aber nur im Admin-Modus editierbar. Andere Schritte koennt ihr weiterhin bearbeiten.
                </p>
              </div>
            ) : null}
            <div className="content-stack">
              <div>
                <h5 style={{ margin: 0 }}>Verfuegbare Aktionen</h5>
                <p className="text-muted" style={{ margin: "0.25rem 0 0" }}>
                  Hier koennt ihr nur freigegebene Aktionen verwenden. Freie Skripte oder direkte technische Eingaben sind bewusst nicht vorgesehen.
                </p>
              </div>
              {!canManageAdvanced ? (
                <div className="panel panel-info">
                  <p className="panel-text">
                    Der Aktionskatalog ist in diesem Modus gesperrt. Fuer Automatisierungen und Freigabe ist der Admin-Modus erforderlich.
                  </p>
                </div>
              ) : actionDefinitions.length === 0 ? (
                <div className="panel panel-warning">
                  <p className="panel-text">Es sind aktuell keine Aktionen geladen.</p>
                </div>
              ) : (
                <div style={{ display: "grid", gap: "0.75rem" }}>
                  {actionDefinitions.map((definition) => (
                    <div key={`catalog-${definition.actionKey}`} className="panel content-stack" style={{ gap: "0.65rem" }}>
                      <div className="content-stack" style={{ gap: "0.4rem" }}>
                        <div className="flex-row" style={{ justifyContent: "space-between", gap: "0.75rem", alignItems: "flex-start" }}>
                          <div className="content-stack" style={{ gap: "0.2rem" }}>
                            <strong>{definition.displayName}</strong>
                            <span className="text-muted" style={{ fontSize: "0.82rem" }}>
                              {definition.actionKey}
                            </span>
                          </div>
                          <button
                            type="button"
                            className="button-secondary"
                            aria-label={`${definition.displayName} hinzufuegen`}
                            disabled={!definition.isActive}
                            onClick={() => onAddActionFromDefinition(selectedNode.id, definition.actionKey)}
                          >
                            Aktion hinzufuegen
                          </button>
                        </div>
                        {definition.description ? (
                          <p className="text-muted" style={{ margin: 0 }}>{definition.description}</p>
                        ) : null}
                      </div>
                      <div className="flex-row" style={{ gap: "0.45rem", flexWrap: "wrap" }}>
                        {renderActionBadge(definition.isActive ? "aktiv" : "inaktiv", definition.isActive ? "success" : "warning")}
                        {renderActionBadge(definition.isIdempotent ? "wiederholbar" : "einmalig", definition.isIdempotent ? "info" : "neutral")}
                        {definition.requiresApproval ? renderActionBadge("braucht Freigabe", "warning") : null}
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>

            <div className="content-stack">
              <div>
                <h5 style={{ margin: 0 }}>Hinterlegte Aktionen</h5>
                <p className="text-muted" style={{ margin: "0.25rem 0 0" }}>
                  Reihenfolge und Eingabe-Mapping bleiben technisch, werden hier aber gesammelt bearbeitet.
                </p>
              </div>
              {configuredActions.length === 0 ? (
                <div className="panel panel-warning">
                  <p className="panel-text">Dieser automatische Schritt hat noch keine Aktionen.</p>
                </div>
              ) : null}
              {configuredActions.map((action) => {
                const actionDefinition = actionDefinitionsByKey.get(action.actionKey.trim().toLowerCase()) ?? null;

                return (
                  <div key={action.id} className="panel content-stack">
                    <div className="content-stack" style={{ gap: "0.35rem" }}>
                      <div className="flex-row" style={{ justifyContent: "space-between", gap: "0.75rem", alignItems: "flex-start" }}>
                        <div className="content-stack" style={{ gap: "0.18rem" }}>
                          <strong>{actionDefinition?.displayName ?? action.actionKey ?? "Unbekannte Aktion"}</strong>
                          <span className="text-muted" style={{ fontSize: "0.82rem" }}>
                            {actionDefinition?.actionKey ?? action.actionKey ?? "ohne Aktion-Key"}
                          </span>
                        </div>
                        <div className="flex-row" style={{ gap: "0.45rem", flexWrap: "wrap" }}>
                          {actionDefinition
                            ? renderActionBadge(actionDefinition.isActive ? "aktiv" : "inaktiv", actionDefinition.isActive ? "success" : "warning")
                            : renderActionBadge("unbekannt", "warning")}
                          {actionDefinition?.isIdempotent ? renderActionBadge("wiederholbar", "info") : null}
                          {actionDefinition?.requiresApproval ? renderActionBadge("braucht Freigabe", "warning") : null}
                        </div>
                      </div>
                      {actionDefinition?.description ? (
                        <p className="text-muted" style={{ margin: 0 }}>{actionDefinition.description}</p>
                      ) : null}
                    </div>
                    {!canManageAdvanced ? (
                      <div className="builder-readonly-grid">
                        <div className="panel panel-muted content-stack" style={{ gap: "0.35rem" }}>
                          <span className="text-muted" style={{ fontSize: "0.75rem" }}>{WORKFLOW_BUILDER_TECHNICAL_LABELS.executionOrder}</span>
                          <strong>{action.executionOrder}</strong>
                        </div>
                        <div className="panel panel-muted content-stack" style={{ gap: "0.35rem" }}>
                          <span className="text-muted" style={{ fontSize: "0.75rem" }}>{WORKFLOW_BUILDER_TECHNICAL_LABELS.inputMappingJson}</span>
                          <code style={{ whiteSpace: "pre-wrap", overflowWrap: "anywhere", fontSize: "0.78rem" }}>
                            {action.inputMappingText.trim() || "{}"}
                          </code>
                        </div>
                      </div>
                    ) : (
                      <>
                        <div className="grid-two-columns">
                          <label>
                            <span>Aktion</span>
                            <select
                              className="form-select"
                              value={action.actionKey}
                              onChange={(event) => onUpdateAction(selectedNode.id, action.id, { actionKey: event.target.value })}
                            >
                              <option value="">Bitte waehlen</option>
                              {actionDefinitions.map((definition) => (
                                <option key={definition.actionKey} value={definition.actionKey}>
                                  {definition.displayName}{definition.isActive ? "" : " (inaktiv)"}
                                </option>
                              ))}
                            </select>
                          </label>
                          <label>
                            <span>{WORKFLOW_BUILDER_TECHNICAL_LABELS.executionOrder}</span>
                            <input
                              className="form-input"
                              type="number"
                              min="1"
                              value={action.executionOrder}
                              onChange={(event) => onUpdateAction(selectedNode.id, action.id, { executionOrder: event.target.value })}
                            />
                          </label>
                        </div>
                        <label>
                          <span>{WORKFLOW_BUILDER_TECHNICAL_LABELS.inputMappingJson}</span>
                          <textarea
                            className="form-input"
                            rows={5}
                            value={action.inputMappingText}
                            onChange={(event) => onUpdateAction(selectedNode.id, action.id, { inputMappingText: event.target.value })}
                          />
                        </label>
                        <button type="button" className="button-danger" onClick={() => onRemoveAction(selectedNode.id, action.id)}>
                          Aktion loeschen
                        </button>
                      </>
                    )}
                  </div>
                );
              })}
            </div>
          </div>
        )}
      </section>

      <section className="content-stack">
        <button type="button" className="button-secondary" onClick={() => setShowAdvanced((current) => !current)}>
          {showAdvanced ? "Erweitert ausblenden" : "Erweitert anzeigen"}
        </button>
        {showAdvanced ? (
          <div className="panel content-stack">
            <div>
              <h4>Erweitert</h4>
              <p className="text-muted" style={{ margin: 0 }}>
                Technische Felder bleiben verfuegbar, stehen aber bewusst hinter den fachlichen Angaben und nur hier im erweiterten Bereich.
              </p>
            </div>
            <div className="grid-two-columns">
              <label>
                <span>{WORKFLOW_BUILDER_TECHNICAL_LABELS.nodeKey}</span>
                <input
                  className="form-input"
                  value={selectedNode.nodeKey}
                  disabled={isLockedAutomationNode}
                  onChange={(event) => onUpdateNode(selectedNode.id, { nodeKey: event.target.value })}
                />
              </label>
              <label>
                <span>{WORKFLOW_BUILDER_TECHNICAL_LABELS.sortOrder}</span>
                <input
                  className="form-input"
                  type="number"
                  min="1"
                  value={selectedNode.sortOrder}
                  disabled={isLockedAutomationNode}
                  onChange={(event) => onUpdateNode(selectedNode.id, { sortOrder: event.target.value })}
                />
              </label>
              <label>
                <span>Position X</span>
                <input className="form-input" value={selectedNode.positionX ?? ""} disabled />
              </label>
              <label>
                <span>Position Y</span>
                <input className="form-input" value={selectedNode.positionY ?? ""} disabled />
              </label>
            </div>
            {selectedNode.nodeType !== "start" && selectedNode.nodeType !== "end" ? (
              <label>
                <span>{WORKFLOW_BUILDER_TECHNICAL_LABELS.configJson}</span>
                <textarea
                  className="form-input"
                  rows={6}
                  value={selectedNode.configText}
                  disabled={isLockedAutomationNode}
                  onChange={(event) => onUpdateNode(selectedNode.id, { configText: event.target.value })}
                />
              </label>
            ) : null}
          </div>
        ) : null}
      </section>
    </section>
  );
}

function renderActionBadge(label: string, tone: "success" | "info" | "warning" | "neutral") {
  const backgroundByTone = {
    success: "rgba(220, 252, 231, 0.98)",
    info: "rgba(224, 231, 255, 0.98)",
    warning: "rgba(254, 243, 199, 0.98)",
    neutral: "rgba(241, 245, 249, 0.98)",
  } as const;
  const borderByTone = {
    success: "rgba(21, 128, 61, 0.18)",
    info: "rgba(29, 78, 216, 0.18)",
    warning: "rgba(180, 83, 9, 0.2)",
    neutral: "rgba(71, 85, 105, 0.18)",
  } as const;
  const textByTone = {
    success: "#166534",
    info: "#1e40af",
    warning: "#92400e",
    neutral: "#334155",
  } as const;

  return (
    <span
      className="badge badge--default"
      style={{
        background: backgroundByTone[tone],
        borderColor: borderByTone[tone],
        color: textByTone[tone],
      }}
    >
      {label}
    </span>
  );
}

function renderNodeConfigurationSection(
  node: WorkflowBuilderNodeDraft,
  responsibilityOwners: AdminResponsibilityOwner[],
  onUpdateNode: (nodeId: string, patch: Partial<WorkflowBuilderNodeDraft>) => void
) {
  const currentConfig = tryParseConfigObject(node.configText);
  const responsibilityKey = readStringConfigValue(currentConfig, "responsibilityKey");
  const notificationLabel = readStringConfigValue(currentConfig, "notificationLabel");
  const summaryText = readStringConfigValue(currentConfig, "summaryText");
  const commonFields = (
    <>
      {node.nodeType !== "start" && node.nodeType !== "end" ? (
        <label>
          <span>Zustaendige</span>
          <select
            className="form-select"
            value={responsibilityKey}
            onChange={(event) =>
              onUpdateNode(node.id, { configText: updateStringConfigValue(node.configText, "responsibilityKey", event.target.value) })
            }
          >
            <option value="">Bitte waehlen</option>
            {responsibilityOwners.map((responsibility) => (
              <option key={responsibility.responsibilityId} value={responsibility.responsibilityKey}>
                {responsibility.responsibilityName}
              </option>
            ))}
          </select>
        </label>
      ) : null}
      <label>
        <span>Benachrichtigte</span>
        <input
          className="form-input"
          value={notificationLabel}
          onChange={(event) =>
            onUpdateNode(node.id, { configText: updateStringConfigValue(node.configText, "notificationLabel", event.target.value) })
          }
        />
      </label>
      <label>
        <span>Kurzbeschreibung</span>
        <textarea
          className="form-input"
          rows={3}
          value={summaryText}
          onChange={(event) =>
            onUpdateNode(node.id, { configText: updateStringConfigValue(node.configText, "summaryText", event.target.value) })
          }
        />
      </label>
    </>
  );

  switch (node.nodeType) {
    case "start":
      return (
        <div className="content-stack">
          {commonFields}
          <p className="text-muted">Der Startschritt braucht keine weiteren fachlichen Angaben.</p>
        </div>
      );
    case "end":
      return (
        <div className="content-stack">
          {commonFields}
          <p className="text-muted">Der Endschritt beendet den Pfad und braucht keine weiteren fachlichen Angaben.</p>
        </div>
      );
    case "form":
      return (
        <div className="content-stack">
          {commonFields}
          <label>
            <span>{WORKFLOW_BUILDER_TECHNICAL_LABELS.processTypeKey}</span>
            <input
              className="form-input"
              value={readStringConfigValue(currentConfig, "legacyProcessTypeKey")}
              onChange={(event) =>
                onUpdateNode(node.id, { configText: updateStringConfigValue(node.configText, "legacyProcessTypeKey", event.target.value) })
              }
            />
          </label>
        </div>
      );
    case "task":
    case "approval":
      return (
        <div className="content-stack">
          {commonFields}
          <label>
            <span>{WORKFLOW_BUILDER_TECHNICAL_LABELS.templateKey}</span>
            <input
              className="form-input"
              value={readStringConfigValue(currentConfig, "legacyTemplateKey")}
              onChange={(event) =>
                onUpdateNode(node.id, { configText: updateStringConfigValue(node.configText, "legacyTemplateKey", event.target.value) })
              }
            />
          </label>
        </div>
      );
    case "decision":
      return (
        <div className="content-stack">
          {commonFields}
          <div className="panel panel-info">
            <p className="panel-text">
              Entscheidungen steuern Bedingungen und Folgewege. Die konkreten Verbindungen pflegst du unten im Bereich `Naechste Schritte`.
            </p>
          </div>
        </div>
      );
    case "automation":
      return (
        <div className="content-stack">
          {commonFields}
          <div className="panel panel-info">
            <p className="panel-text">
              Automatische Schritte beziehen ihr Verhalten aus den hinterlegten Aktionen weiter unten.
            </p>
          </div>
        </div>
      );
    default:
      return (
        <div className="content-stack">
          {commonFields}
          <p className="text-muted">Fuer diesen Schritt sind aktuell keine weiteren Angaben notwendig.</p>
        </div>
      );
  }
}

function tryParseConfigObject(configText: string): Record<string, unknown> | null {
  if (!configText.trim()) {
    return null;
  }

  try {
    const parsed = JSON.parse(configText) as unknown;
    if (parsed && typeof parsed === "object" && !Array.isArray(parsed)) {
      return parsed as Record<string, unknown>;
    }
  } catch {
    return null;
  }

  return null;
}

function readStringConfigValue(config: Record<string, unknown> | null, key: string): string {
  const value = config?.[key];
  return typeof value === "string" ? value : "";
}

function updateStringConfigValue(configText: string, key: string, nextValue: string): string {
  const config = tryParseConfigObject(configText) ?? {};
  if (nextValue.trim()) {
    config[key] = nextValue.trim();
  } else {
    delete config[key];
  }

  if (Object.keys(config).length === 0) {
    return "";
  }

  return JSON.stringify(config, null, 2);
}
