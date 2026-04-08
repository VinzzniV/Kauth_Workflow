import { useMemo, useState } from "react";
import type { AdminWorkflowActionDefinition } from "../../types/auth";
import type { WorkflowBuilderNodeDraft, WorkflowBuilderVersionDraft } from "../../hooks/adminWorkflowBuilderModel";
import { getWorkflowBuilderNodeTypeLabel, WORKFLOW_BUILDER_TECHNICAL_LABELS } from "./workflowBuilderLabels";

type WorkflowBuilderSidebarProps = {
  selectedNode: WorkflowBuilderNodeDraft | null;
  availableNodeKeys: string[];
  versionDraft: WorkflowBuilderVersionDraft;
  actionDefinitions: AdminWorkflowActionDefinition[];
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

export function WorkflowBuilderSidebar({
  selectedNode,
  availableNodeKeys,
  versionDraft,
  actionDefinitions,
  canManageAdvanced,
  onUpdateNode,
  onUpdateEdge,
  onRemoveEdge,
  onAddActionFromDefinition,
  onUpdateAction,
  onRemoveAction,
}: WorkflowBuilderSidebarProps) {
  const [showAdvanced, setShowAdvanced] = useState(false);
  const actionDefinitionsByKey = useMemo(() => {
    return new Map(
      actionDefinitions.map((definition) => [definition.actionKey.trim().toLowerCase(), definition] as const)
    );
  }, [actionDefinitions]);

  const outgoingEdges = useMemo(() => {
    if (!selectedNode) {
      return [];
    }

    const selectedKey = selectedNode.nodeKey.trim().toLowerCase();
    return versionDraft.edges.filter((edge) => edge.sourceNodeKey.trim().toLowerCase() === selectedKey);
  }, [selectedNode, versionDraft.edges]);

  const summaryText = useMemo(() => {
    if (!selectedNode) {
      return null;
    }

    switch (selectedNode.nodeType) {
      case "start":
        return "Startet den Workflow und fuehrt in die ersten verbundenen Schritte.";
      case "end":
        return "Beendet den aktuellen Pfad und benoetigt keine weitere Fachkonfiguration.";
      case "form":
        return "Sammelt fachliche Eingaben und verbindet sie mit einem Process-Type-Kontext.";
      case "approval":
        return "Fordert eine Freigabe an und verweist auf die dazugehoerige fachliche Vorlage.";
      case "decision":
        return "Steuert die Weiterleitung ueber Bedingungen und ausgehende Pfade.";
      case "automation":
        return "Definiert technische Aktionen, die sichtbar im Builder gepflegt werden.";
      case "task":
      default:
        return "Beschreibt einen Human-Task mit fachlicher Vorlage und naechstem Arbeitsschritt.";
    }
  }, [selectedNode]);

  const configuredActions = selectedNode?.nodeType === "automation" ? selectedNode.actions : [];
  const isLockedAutomationNode = selectedNode?.nodeType === "automation" && !canManageAdvanced;
  const allowedNodeTypeOptions = canManageAdvanced
    ? NODE_TYPE_OPTIONS
    : selectedNode?.nodeType === "automation"
      ? (["automation"] as WorkflowBuilderNodeDraft["nodeType"][])
      : NODE_TYPE_OPTIONS.filter((nodeType) => nodeType !== "automation");

  if (!selectedNode) {
    return (
      <aside className="panel master-detail-sidebar" aria-label="Builder Sidebar">
        <div className="content-stack">
          <div>
            <h3>Node Sidebar</h3>
            <p className="text-muted">
              Waehle einen Node im Canvas aus, um fachliche Konfiguration, Weiterleitung und Actions im Builder-Kontext zu bearbeiten.
            </p>
          </div>
          <div className="panel panel-info">
            <p className="panel-text">
              Noch kein Node ausgewaehlt. Die Sidebar oeffnet sich erst bei expliziter Auswahl im Canvas.
            </p>
          </div>
        </div>
      </aside>
    );
  }

  return (
    <aside className="panel master-detail-sidebar" aria-label="Builder Sidebar">
      <div className="content-stack">
        <section className="content-stack">
          <div className="content-stack" style={{ gap: "0.35rem" }}>
            <span className="badge badge--default">{getWorkflowBuilderNodeTypeLabel(selectedNode.nodeType)}</span>
            <h3 style={{ margin: 0 }}>{selectedNode.title.trim() || selectedNode.nodeKey.trim() || "Neuer Node"}</h3>
            <p className="text-muted" style={{ margin: 0 }}>{summaryText}</p>
          </div>
        </section>

        <section className="content-stack">
          <div>
            <h4>Basis</h4>
            <p className="text-muted" style={{ margin: 0 }}>
              Fachliche Grunddaten fuer den ausgewaehlten Builder-Baustein.
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
                <span>Node Type</span>
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
              Automation-Struktur kann nur im Admin Builder veraendert werden.
            </p>
          ) : null}
        </section>

        <section className="content-stack">
          <div>
            <h4>Node-Konfiguration</h4>
            <p className="text-muted" style={{ margin: 0 }}>
              Typbezogene Konfiguration statt generischer Datensatzpflege.
            </p>
          </div>
          {renderNodeConfigurationSection(selectedNode, onUpdateNode)}
        </section>

        <section className="content-stack">
          <div>
            <h4>Weiterleitung</h4>
            <p className="text-muted" style={{ margin: 0 }}>
              Ausgehende Pfade des selektierten Nodes. Neue Verbindungen entstehen weiter direkt im Canvas.
            </p>
          </div>
          {outgoingEdges.length === 0 ? (
            <p className="text-muted">Noch keine ausgehenden Verbindungen fuer diesen Node vorhanden.</p>
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
                      {availableNodeKeys.map((nodeKey) => (
                        <option key={`sidebar-target-${edge.id}-${nodeKey}`} value={nodeKey}>{nodeKey}</option>
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
                  Weiterleitung loeschen
                </button>
              </div>
            ))
          )}
        </section>

        <section className="content-stack">
          <div>
            <h4>Action Layer</h4>
            <p className="text-muted" style={{ margin: 0 }}>
              Kontrollierte technische Actions bleiben als eigener Builder-Bestandteil sichtbar und nutzen den vorhandenen Action-Katalog.
            </p>
          </div>
          {selectedNode.nodeType !== "automation" ? (
            <div className="panel panel-info">
              <p className="panel-text">
                Technische Actions werden auf Automation-Nodes ausgefuehrt. Der Katalog enthaelt aktuell {actionDefinitions.length} kontrollierte Action(s).
              </p>
            </div>
          ) : (
            <div className="content-stack">
              {!canManageAdvanced ? (
                <div className="panel panel-warning">
                  <p className="panel-text">
                    Dieser Automation-Node ist sichtbar, aber nur im Admin Builder editierbar. Draft-Bearbeitung bleibt fuer Nodes, Edges und nicht-technische Bausteine moeglich.
                  </p>
                </div>
              ) : null}
              <div className="content-stack">
                <div>
                  <h5 style={{ margin: 0 }}>Verfuegbarer Action-Katalog</h5>
                  <p className="text-muted" style={{ margin: "0.25rem 0 0" }}>
                    Nur vordefinierte Actions sind zulaessig. Freie Skript-, SQL- oder HTTP-Eingaben sind bewusst nicht moeglich.
                  </p>
                </div>
                {!canManageAdvanced ? (
                  <div className="panel panel-info">
                    <p className="panel-text">
                      Der Action-Katalog ist in diesem Modus gesperrt. Fuer technische Actions, Automation und Publish ist ein Admin Builder erforderlich.
                    </p>
                  </div>
                ) : actionDefinitions.length === 0 ? (
                  <div className="panel panel-warning">
                    <p className="panel-text">Es sind aktuell keine Action-Definitionen geladen.</p>
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
                              disabled={!definition.isActive}
                              onClick={() => onAddActionFromDefinition(selectedNode.id, definition.actionKey)}
                            >
                              {definition.displayName} hinzufuegen
                            </button>
                          </div>
                          {definition.description ? (
                            <p className="text-muted" style={{ margin: 0 }}>{definition.description}</p>
                          ) : null}
                        </div>
                        <div className="flex-row" style={{ gap: "0.45rem", flexWrap: "wrap" }}>
                          {renderActionBadge(definition.isActive ? "aktiv" : "inaktiv", definition.isActive ? "success" : "warning")}
                          {renderActionBadge(definition.isIdempotent ? "idempotent" : "non-idempotent", definition.isIdempotent ? "info" : "neutral")}
                          {definition.requiresApproval ? renderActionBadge("approval required", "warning") : null}
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>

              <div className="content-stack">
                <div>
                  <h5 style={{ margin: 0 }}>Konfigurierte Actions</h5>
                  <p className="text-muted" style={{ margin: "0.25rem 0 0" }}>
                    Reihenfolge und Mapping bleiben am bestehenden technischen Modell haengen, werden aber hier sichtbar gepflegt.
                  </p>
                </div>
              {configuredActions.length === 0 ? (
                <div className="panel panel-warning">
                  <p className="panel-text">Dieser Automation-Node hat noch keine Actions.</p>
                </div>
              ) : null}
              {configuredActions.map((action) => {
                const actionDefinition = actionDefinitionsByKey.get(action.actionKey.trim().toLowerCase()) ?? null;

                return (
                <div key={action.id} className="panel content-stack">
                  <div className="content-stack" style={{ gap: "0.35rem" }}>
                    <div className="flex-row" style={{ justifyContent: "space-between", gap: "0.75rem", alignItems: "flex-start" }}>
                      <div className="content-stack" style={{ gap: "0.18rem" }}>
                        <strong>{actionDefinition?.displayName ?? action.actionKey ?? "Unbekannte Action"}</strong>
                        <span className="text-muted" style={{ fontSize: "0.82rem" }}>
                          {actionDefinition?.actionKey ?? action.actionKey ?? "ohne Action Key"}
                        </span>
                      </div>
                      <div className="flex-row" style={{ gap: "0.45rem", flexWrap: "wrap" }}>
                        {actionDefinition
                          ? renderActionBadge(actionDefinition.isActive ? "aktiv" : "inaktiv", actionDefinition.isActive ? "success" : "warning")
                          : renderActionBadge("unbekannt", "warning")}
                        {actionDefinition?.isIdempotent ? renderActionBadge("idempotent", "info") : null}
                        {actionDefinition?.requiresApproval ? renderActionBadge("approval required", "warning") : null}
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
                          <span>Action</span>
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
                        Action loeschen
                      </button>
                    </>
                  )}
                </div>
              )})}
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
                  Technische Felder bleiben verfuegbar, stehen aber nicht mehr im Vordergrund der Builder-Bedienung.
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
      </div>
    </aside>
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
  onUpdateNode: (nodeId: string, patch: Partial<WorkflowBuilderNodeDraft>) => void
) {
  const currentConfig = tryParseConfigObject(node.configText);

  switch (node.nodeType) {
    case "start":
      return <p className="text-muted">Der Start-Node braucht keine weitere Fachkonfiguration.</p>;
    case "end":
      return <p className="text-muted">Der End-Node beendet den Pfad und braucht keine weitere Fachkonfiguration.</p>;
    case "form":
      return (
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
      );
    case "task":
    case "approval":
      return (
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
      );
    case "decision":
      return (
        <div className="panel panel-info">
          <p className="panel-text">
            Decision-Nodes steuern Bedingungen und Weiterleitung. Die konkreten Pfade pflegst du im Bereich `Weiterleitung`.
          </p>
        </div>
      );
    case "automation":
      return (
        <div className="panel panel-info">
          <p className="panel-text">
            Automation-Nodes definieren ihr Verhalten ueber sichtbare Actions. Die Action-Konfiguration liegt direkt im Abschnitt `Action Layer`.
          </p>
        </div>
      );
    default:
      return <p className="text-muted">Keine zusaetzliche Konfiguration fuer diesen Node-Typ verfuegbar.</p>;
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

const NODE_TYPE_OPTIONS: WorkflowBuilderNodeDraft["nodeType"][] = [
  "start",
  "form",
  "approval",
  "task",
  "decision",
  "automation",
  "end",
];
