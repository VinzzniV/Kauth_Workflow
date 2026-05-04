import { useCallback, useEffect, useMemo, useState, type ReactElement } from "react";
import { AlertTriangle, ChevronDown, ChevronRight, Link2, Pencil, X } from "lucide-react";
import { useCurrentUser } from "../../auth/useCurrentUser";
import { useConfirmationDialog } from "../feedback/useConfirmationDialog";
import { useAdminWorkflowBuilder } from "../../hooks/useAdminWorkflowBuilder";
import {
  topologicallyOrderNodes,
  type WorkflowBuilderEdgeDraft,
  type WorkflowBuilderLocalIssue,
  type WorkflowBuilderNodeDraft,
  type WorkflowBuilderVersionDraft,
} from "../../hooks/adminWorkflowBuilderModel";
import { WorkflowBuilderStepCard } from "./WorkflowBuilderStepCard";
import { WorkflowBuilderConditionEditor } from "./WorkflowBuilderConditionEditor";
import { WorkflowBuilderGraphPreview } from "./WorkflowBuilderGraphPreview";
import { summarizeCondition } from "./workflowBuilderEditorHelpers";
import { getWorkflowBuilderNodeTypeLabel } from "./workflowBuilderLabels";
import {
  buildWorkflowBuilderIssueIndex,
  highestSeverity,
  type WorkflowBuilderIssueIndex,
  type WorkflowBuilderIssueRef,
} from "./workflowBuilderIssues";

// ─── Anchors / jump-to ────────────────────────────────────────────────────────

function stepAnchorId(node: WorkflowBuilderNodeDraft): string {
  return `wf-step-${node.id}`;
}

function edgeAnchorId(edge: WorkflowBuilderEdgeDraft): string {
  return `wf-edge-${edge.id}`;
}

function jumpToAnchor(anchorId: string | null) {
  if (!anchorId) return;
  const el = typeof document !== "undefined" ? document.getElementById(anchorId) : null;
  if (!el) return;
  el.scrollIntoView({ behavior: "smooth", block: "center" });
  el.classList.add("wf-jump-highlight");
  window.setTimeout(() => el.classList.remove("wf-jump-highlight"), 1600);
}

function resolveLocalIssueAnchor(
  issue: WorkflowBuilderLocalIssue,
  draft: WorkflowBuilderVersionDraft
): string | null {
  const ref = issue.referenceKey?.trim().toLowerCase() ?? "";

  if ((issue.scope === "node" || issue.scope === "action") && ref) {
    const match = draft.nodes.find((n) => n.nodeKey.trim().toLowerCase() === ref);
    return match ? stepAnchorId(match) : null;
  }

  if (issue.scope === "edge" && ref) {
    const match = draft.edges.find((e) => e.sourceNodeKey.trim().toLowerCase() === ref);
    return match ? edgeAnchorId(match) : null;
  }

  return null;
}

function resolveBackendIssueAnchor(
  scope: string,
  referenceKey: string | null,
  draft: WorkflowBuilderVersionDraft
): string | null {
  const ref = referenceKey?.trim().toLowerCase() ?? "";
  if (!ref) return null;
  const normalizedScope = scope.trim().toLowerCase();

  if (normalizedScope === "node" || normalizedScope === "action") {
    const match = draft.nodes.find((n) => n.nodeKey.trim().toLowerCase() === ref);
    return match ? stepAnchorId(match) : null;
  }

  if (normalizedScope === "edge") {
    const match = draft.edges.find((e) => e.sourceNodeKey.trim().toLowerCase() === ref);
    return match ? edgeAnchorId(match) : null;
  }

  return null;
}

type AdminWorkflowBuilderFormSectionProps = {
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
};

const PROCESS_TYPE_OPTIONS: { key: string; label: string }[] = [
  { key: "onboarding", label: "Onboarding" },
  { key: "offboarding", label: "Offboarding" },
  { key: "department_change", label: "Abteilungswechsel" },
  { key: "position_change", label: "Positionswechsel" },
  { key: "role_change", label: "Rollenwechsel" },
  { key: "name_change", label: "Namensänderung" },
];

type StepCategoryGroup = {
  label: string;
  items: { key: WorkflowBuilderNodeDraft["nodeType"]; label: string }[];
};

const STEP_CATEGORY_GROUPS: StepCategoryGroup[] = [
  {
    label: "Steuerung",
    items: [
      { key: "decision", label: "Entscheidung" },
      { key: "parallel_split", label: "Parallel-Split" },
      { key: "parallel_join", label: "Parallel-Join" },
      { key: "end", label: "Ende" },
    ],
  },
  {
    label: "Bearbeitung",
    items: [
      { key: "form", label: "Formular" },
      { key: "approval", label: "Freigabe" },
      { key: "task", label: "Aufgabe" },
    ],
  },
  {
    label: "Maßnahmen",
    items: [
      { key: "measure_provision", label: "Bereitstellung" },
      { key: "measure_deprovision", label: "Entzug" },
      { key: "measure_change", label: "Änderung" },
      { key: "measure_rename", label: "Umbenennung" },
    ],
  },
  {
    label: "Technisch",
    items: [{ key: "automation", label: "Automatisierung" }],
  },
];

export function AdminWorkflowBuilderFormSection({
  onNotice,
  onError,
}: AdminWorkflowBuilderFormSectionProps) {
  const { capabilities } = useCurrentUser();
  const canManageAdvanced = capabilities.canManageAdminConfiguration;
  const builder = useAdminWorkflowBuilder({ onNotice, onError, canManageAdvanced });

  const [rawSelectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [rawSelectedEdgeId, setSelectedEdgeId] = useState<string | null>(null);
  const [rawConnectingFromNodeId, setConnectingFromNodeId] = useState<string | null>(null);

  const versionNodes = builder.versionDraft.nodes;
  const versionEdges = builder.versionDraft.edges;
  const issueIndex = useMemo(
    () => buildWorkflowBuilderIssueIndex(
      builder.localValidationIssues,
      builder.versionDetail?.validationIssues ?? [],
      builder.versionDraft
    ),
    [builder.localValidationIssues, builder.versionDetail, builder.versionDraft]
  );

  // Treat selection as null if the underlying draft object disappears (delete / version switch).
  const selectedNodeId = rawSelectedNodeId
    && versionNodes.some((node) => node.id === rawSelectedNodeId)
    ? rawSelectedNodeId
    : null;
  const selectedEdgeId = rawSelectedEdgeId
    && versionEdges.some((edge) => edge.id === rawSelectedEdgeId)
    ? rawSelectedEdgeId
    : null;
  const connectingFromNodeId = rawConnectingFromNodeId
    && versionNodes.some((node) => node.id === rawConnectingFromNodeId)
    ? rawConnectingFromNodeId
    : null;

  const selectEdge = useCallback((edgeId: string) => {
    setSelectedEdgeId(edgeId);
    setSelectedNodeId(null);
    setConnectingFromNodeId(null);
  }, []);
  const clearSelection = useCallback(() => {
    setSelectedNodeId(null);
    setSelectedEdgeId(null);
    setConnectingFromNodeId(null);
  }, []);
  const cancelConnect = useCallback(() => {
    setConnectingFromNodeId(null);
  }, []);

  const connectingNode = connectingFromNodeId
    ? versionNodes.find((node) => node.id === connectingFromNodeId) ?? null
    : null;
  const isConnecting = Boolean(connectingNode);

  const startConnectFromNode = useCallback((nodeId: string) => {
    const sourceNode = versionNodes.find((node) => node.id === nodeId);
    if (!sourceNode) {
      return;
    }
    if (!sourceNode.nodeKey.trim()) {
      onError("Der Quell-Schritt braucht zuerst einen Schritt-Key, bevor Verbindungen entstehen können.");
      return;
    }
    onError(null);
    setSelectedNodeId(nodeId);
    setSelectedEdgeId(null);
    setConnectingFromNodeId(nodeId);
  }, [onError, versionNodes]);

  const selectNode = useCallback((nodeId: string) => {
    if (connectingFromNodeId && connectingFromNodeId !== nodeId) {
      const sourceNode = versionNodes.find((node) => node.id === connectingFromNodeId);
      const targetNode = versionNodes.find((node) => node.id === nodeId);
      if (sourceNode && targetNode) {
        const sourceKey = sourceNode.nodeKey.trim();
        const targetKey = targetNode.nodeKey.trim();
        if (!sourceKey) {
          onError("Der Quell-Schritt braucht einen Schritt-Key, bevor Verbindungen entstehen können.");
          setConnectingFromNodeId(null);
          return;
        }
        if (!targetKey) {
          onError("Der Ziel-Schritt braucht einen Schritt-Key, bevor er Verbindungen empfangen kann.");
          setConnectingFromNodeId(null);
          setSelectedNodeId(nodeId);
          setSelectedEdgeId(null);
          return;
        }
        const newEdgeId = builder.addEdge({ sourceNodeKey: sourceKey, targetNodeKey: targetKey });
        setConnectingFromNodeId(null);
        if (newEdgeId) {
          setSelectedEdgeId(newEdgeId);
          setSelectedNodeId(null);
          onError(null);
          onNotice(`Verbindung '${sourceKey} → ${targetKey}' wurde angelegt.`);
        }
        return;
      }
    }
    setConnectingFromNodeId(null);
    setSelectedNodeId(nodeId);
    setSelectedEdgeId(null);
  }, [builder, connectingFromNodeId, onError, onNotice, versionNodes]);

  // Esc bricht den Connect-Mode global ab — bleibt unaufdringlich, weil ein
  // aktiver Connect-Mode visuell hervorgehoben wird.
  useEffect(() => {
    if (!isConnecting) return;
    const handler = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setConnectingFromNodeId(null);
      }
    };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, [isConnecting]);

  return (
    <div className="wf-form-editor">
      <WorkflowSelectorBar builder={builder} canManageAdvanced={canManageAdvanced} />

      {builder.isLoading ? (
        <div className="panel panel-muted">
          <p className="panel-text text-secondary">Ablaufdefinitionen werden geladen …</p>
        </div>
      ) : !builder.selectedDefinition ? (
        <BuilderEmptyState canManageAdvanced={canManageAdvanced} hasDefinitions={builder.definitions.length > 0} />
      ) : (
        <>
          <BuilderActionToolbar builder={builder} canManageAdvanced={canManageAdvanced} />
          <PublishedVersionBanner builder={builder} />
          <div className="wf-form-body">
            <Section1Stammdaten builder={builder} canManageAdvanced={canManageAdvanced} />
            <Section2Steps
              builder={builder}
              canManageAdvanced={canManageAdvanced}
              selectedNodeId={selectedNodeId}
              selectedEdgeId={selectedEdgeId}
              onSelectNode={selectNode}
              onSelectEdge={selectEdge}
              onClearSelection={clearSelection}
              issueIndex={issueIndex}
              connectingFromNodeId={connectingFromNodeId}
              connectingFromNodeLabel={connectingNode
                ? connectingNode.title.trim() || connectingNode.nodeKey.trim() || "Schritt"
                : null}
              onStartConnect={startConnectFromNode}
              onCancelConnect={cancelConnect}
            />
            <Section3Edges
              builder={builder}
              canManageAdvanced={canManageAdvanced}
              selectedEdgeId={selectedEdgeId}
              onSelectEdge={selectEdge}
              issueIndex={issueIndex}
            />
            <Section4Validation builder={builder} issueIndex={issueIndex} />
          </div>
        </>
      )}
    </div>
  );
}

// ─── Empty state ─────────────────────────────────────────────────────────────

function BuilderEmptyState({
  canManageAdvanced,
  hasDefinitions,
}: {
  canManageAdvanced: boolean;
  hasDefinitions: boolean;
}) {
  if (hasDefinitions) {
    return (
      <div className="wf-builder-empty">
        <div className="wf-builder-empty-icon" aria-hidden="true">
          <ChevronRight size={24} />
        </div>
        <h3 className="wf-builder-empty-title">Wählen Sie einen Workflow aus</h3>
        <p className="wf-builder-empty-description">
          Oben in der Auswahl steht jeder bestehende Workflow zur Verfügung. Aktionen wirken nur
          auf den ausgewählten Workflow.
        </p>
      </div>
    );
  }

  return (
    <div className="wf-builder-empty">
      <div className="wf-builder-empty-icon" aria-hidden="true">
        <ChevronRight size={24} />
      </div>
      <h3 className="wf-builder-empty-title">Noch kein Workflow vorhanden</h3>
      <p className="wf-builder-empty-description">
        {canManageAdvanced
          ? "Legen Sie den ersten Workflow an. Sie können später Versionen erzeugen, Schritte hinzufügen und veröffentlichen."
          : "Aktuell ist kein Workflow konfiguriert. Bitte wenden Sie sich an einen Administrator."}
      </p>
      {canManageAdvanced ? (
        <p className="wf-builder-empty-hint">
          Nutzen Sie oben den Button <strong>+ Neuer Workflow</strong>, um zu starten.
        </p>
      ) : null}
    </div>
  );
}

// ─── Selector bar ────────────────────────────────────────────────────────────

function WorkflowSelectorBar({
  builder,
  canManageAdvanced,
}: {
  builder: ReturnType<typeof useAdminWorkflowBuilder>;
  canManageAdvanced: boolean;
}) {
  const [createOpen, setCreateOpen] = useState(false);

  return (
    <div className="wf-form-selector-bar">
      <div className="wf-form-selector-group">
        <label className="form-label" htmlFor="wf-def-select">Ablauf</label>
        <select
          id="wf-def-select"
          className="form-select"
          value={builder.selectedDefinition?.id ?? ""}
          onChange={(e) => {
            const id = Number(e.target.value);
            if (id) {
              void builder.selectDefinition(id);
            }
          }}
        >
          {builder.definitions.length === 0 && (
            <option value="">– keine Definitionen –</option>
          )}
          {builder.definitions.map((def) => (
            <option key={def.id} value={def.id}>{def.name || def.key}</option>
          ))}
        </select>
      </div>

      <div className="wf-form-selector-group">
        <label className="form-label" htmlFor="wf-ver-select">Version</label>
        <select
          id="wf-ver-select"
          className="form-select"
          value={builder.selectedVersionSummary?.id ?? ""}
          onChange={(e) => {
            const id = Number(e.target.value);
            if (id) {
              void builder.selectVersion(id);
            }
          }}
        >
          {(builder.selectedDefinition?.versions ?? []).map((ver) => (
            <option key={ver.id} value={ver.id}>
              Version {ver.versionNumber} ({ver.status})
            </option>
          ))}
        </select>
      </div>

      {builder.selectedVersionSummary && (
        <span className={`badge ${builder.selectedVersionSummary.status === "published" ? "badge-success" : "badge-neutral"}`}>
          {builder.selectedVersionSummary.status === "published" ? "Veröffentlicht" : "Entwurf"}
        </span>
      )}

      {canManageAdvanced && (
        <div className="wf-form-selector-actions">
          <button
            type="button"
            className="btn btn-secondary"
            onClick={() => setCreateOpen((v) => !v)}
            disabled={builder.isCreatingDefinition}
            aria-expanded={createOpen}
          >
            {createOpen ? "Abbrechen" : "+ Neuer Workflow"}
          </button>
          <button
            type="button"
            className="btn btn-ghost"
            onClick={() => void builder.deleteDefinition()}
            disabled={!builder.selectedDefinition || builder.isDeletingDefinition || builder.hasUnsavedChanges}
            title={
              !builder.selectedDefinition ? "Kein Workflow ausgewählt"
              : builder.hasUnsavedChanges ? "Bitte zuerst speichern oder verwerfen"
              : "Workflow löschen"
            }
          >
            {builder.isDeletingDefinition ? "Lösche …" : "Workflow löschen"}
          </button>
        </div>
      )}

      {createOpen && (
        <CreateDefinitionForm
          builder={builder}
          onClose={() => setCreateOpen(false)}
        />
      )}
    </div>
  );
}

function CreateDefinitionForm({
  builder,
  onClose,
}: {
  builder: ReturnType<typeof useAdminWorkflowBuilder>;
  onClose: () => void;
}) {
  const draft = builder.newDefinitionDraft;
  const canSubmit = draft.name.trim().length > 0 && !builder.isCreatingDefinition;

  const handleSubmit = async () => {
    if (!canSubmit) return;
    await builder.createDefinition();
    if (!draft.name.trim()) {
      onClose();
    }
  };

  return (
    <form
      className="wf-form-create-definition"
      onSubmit={(e) => {
        e.preventDefault();
        void handleSubmit();
      }}
    >
      <div className="wf-form-field">
        <label className="form-label" htmlFor="wf-new-name">Name <span className="text-error">*</span></label>
        <input
          id="wf-new-name"
          className="form-input"
          type="text"
          value={draft.name}
          onChange={(e) => builder.updateNewDefinitionDraft("name", e.target.value)}
          placeholder="z. B. Onboarding 2026"
          autoFocus
        />
      </div>
      <div className="wf-form-field">
        <label className="form-label" htmlFor="wf-new-description">Beschreibung</label>
        <textarea
          id="wf-new-description"
          className="form-textarea"
          rows={2}
          value={draft.description}
          onChange={(e) => builder.updateNewDefinitionDraft("description", e.target.value)}
          placeholder="Optional"
        />
      </div>
      <div className="wf-form-create-definition-actions">
        <button type="button" className="btn btn-ghost" onClick={onClose} disabled={builder.isCreatingDefinition}>
          Abbrechen
        </button>
        <button type="submit" className="btn btn-primary" disabled={!canSubmit}>
          {builder.isCreatingDefinition ? "Lege an …" : "Anlegen"}
        </button>
      </div>
    </form>
  );
}

// ─── Sektion 1: Stammdaten ───────────────────────────────────────────────────

function Section1Stammdaten({
  builder,
  canManageAdvanced,
}: {
  builder: ReturnType<typeof useAdminWorkflowBuilder>;
  canManageAdvanced: boolean;
}) {
  const [techOpen, setTechOpen] = useState(false);

  return (
    <section className="wf-form-section">
      <div className="wf-form-section-head">
        <h2 className="wf-form-section-title">1 — Stammdaten</h2>
        <p className="wf-form-section-subtitle">Was ist diese Workflow-Definition?</p>
      </div>

      <div className="wf-form-fields">
        <div className="wf-form-field">
          <label className="form-label" htmlFor="wf-name">
            Name <span className="text-error">*</span>
          </label>
          <input
            id="wf-name"
            className="form-input"
            type="text"
            value={builder.definitionDraft.name}
            onChange={(e) => builder.updateDefinitionDraft("name", e.target.value)}
            placeholder="z. B. Onboarding 2026 Q2"
            disabled={!canManageAdvanced}
          />
        </div>

        <div className="wf-form-field">
          <label className="form-label" htmlFor="wf-description">Beschreibung</label>
          <textarea
            id="wf-description"
            className="form-textarea"
            rows={3}
            value={builder.definitionDraft.description}
            onChange={(e) => builder.updateDefinitionDraft("description", e.target.value)}
            placeholder="Zweck, Owner, Anlass …"
            disabled={!canManageAdvanced}
          />
        </div>

        <div className="wf-form-field">
          <label className="form-label" htmlFor="wf-process-type">
            Prozessbezug <span className="text-error">*</span>
          </label>
          <select
            id="wf-process-type"
            className="form-select"
            value={builder.versionDraft.primaryLegacyProcessTypeKey}
            onChange={(e) => builder.updateVersionDraftField("primaryLegacyProcessTypeKey", e.target.value)}
            disabled={!canManageAdvanced}
          >
            <option value="">– bitte wählen –</option>
            {PROCESS_TYPE_OPTIONS.map((opt) => (
              <option key={opt.key} value={opt.key}>{opt.label}</option>
            ))}
          </select>
        </div>

        <div className="wf-form-field">
          <label className="form-label">Version</label>
          <p className="wf-form-readonly-value">
            {builder.selectedVersionSummary
              ? `Version ${builder.selectedVersionSummary.versionNumber} (${builder.selectedVersionSummary.status === "published" ? "Veröffentlicht" : "Entwurf"})`
              : "–"}
          </p>
        </div>
      </div>

      <button
        type="button"
        className="wf-form-tech-toggle"
        onClick={() => setTechOpen((v) => !v)}
        aria-expanded={techOpen}
      >
        {techOpen
          ? <ChevronDown size={14} aria-hidden="true" />
          : <ChevronRight size={14} aria-hidden="true" />}
        <span>Technische Details</span>
      </button>

      {techOpen && (
        <div className="wf-form-tech-block wf-form-fields">
          <div className="wf-form-field">
            <label className="form-label" htmlFor="wf-def-key">Definition-Schlüssel</label>
            <input
              id="wf-def-key"
              className="form-input"
              type="text"
              value={builder.definitionDraft.key}
              readOnly
              disabled
            />
            <p className="wf-form-field-hint">Automatisch vom Namen abgeleitet. Nicht änderbar.</p>
          </div>

          <div className="wf-form-field">
            <label className="form-label" htmlFor="wf-legacy-key">Primärer Prozesstyp-Schlüssel</label>
            <input
              id="wf-legacy-key"
              className="form-input"
              type="text"
              value={builder.versionDraft.primaryLegacyProcessTypeKey}
              onChange={(e) => builder.updateVersionDraftField("primaryLegacyProcessTypeKey", e.target.value)}
              placeholder="z. B. onboarding"
              disabled={!canManageAdvanced}
            />
            <p className="wf-form-field-hint">Technischer Schlüssel für die Maßnahmen-Zuordnung.</p>
          </div>
        </div>
      )}
    </section>
  );
}

// ─── Platzhalter-Sektionen ────────────────────────────────────────────────────

function Section2Steps({
  builder,
  canManageAdvanced,
  selectedNodeId,
  selectedEdgeId,
  onSelectNode,
  onSelectEdge,
  onClearSelection,
  issueIndex,
  connectingFromNodeId,
  connectingFromNodeLabel,
  onStartConnect,
  onCancelConnect,
}: {
  builder: ReturnType<typeof useAdminWorkflowBuilder>;
  canManageAdvanced: boolean;
  selectedNodeId: string | null;
  selectedEdgeId: string | null;
  onSelectNode: (nodeId: string) => void;
  onSelectEdge: (edgeId: string) => void;
  onClearSelection: () => void;
  issueIndex: WorkflowBuilderIssueIndex;
  connectingFromNodeId: string | null;
  connectingFromNodeLabel: string | null;
  onStartConnect: (nodeId: string) => void;
  onCancelConnect: () => void;
}) {
  const confirm = useConfirmationDialog();
  const [addOpen, setAddOpen] = useState(false);
  const [listOpen, setListOpen] = useState(false);
  const [draggingNodeId, setDraggingNodeId] = useState<string | null>(null);
  const [dropBeforeNodeId, setDropBeforeNodeId] = useState<string | null>(null);
  const arrayNodes = builder.versionDraft.nodes;
  const edges = builder.versionDraft.edges;
  const nodes = useMemo(
    () => topologicallyOrderNodes(arrayNodes, edges),
    [arrayNodes, edges]
  );
  const hasStart = nodes.some((node) => node.nodeType === "start");
  const selectedNodeIndex = selectedNodeId
    ? nodes.findIndex((node) => node.id === selectedNodeId)
    : -1;
  const selectedNode = selectedNodeIndex >= 0 ? nodes[selectedNodeIndex] : null;
  const selectedEdge = selectedEdgeId
    ? edges.find((edge) => edge.id === selectedEdgeId) ?? null
    : null;

  const nodeIssueCounts = useMemo(() => {
    const map = new Map<string, number>();
    for (const [id, list] of issueIndex.byNodeId) {
      map.set(id, list.length);
    }
    return map;
  }, [issueIndex]);
  const edgeIssueCounts = useMemo(() => {
    const map = new Map<string, number>();
    for (const [id, list] of issueIndex.byEdgeId) {
      map.set(id, list.length);
    }
    return map;
  }, [issueIndex]);

  const handleDragStart = (nodeId: string) => {
    setDraggingNodeId(nodeId);
  };
  const handleDragOver = (targetNodeId: string | null, event: React.DragEvent<HTMLDivElement>) => {
    if (!draggingNodeId) return;
    event.preventDefault();
    event.dataTransfer.dropEffect = "move";
    if (targetNodeId !== draggingNodeId) {
      setDropBeforeNodeId(targetNodeId);
    }
  };
  const handleDragEnd = () => {
    if (draggingNodeId) {
      const target = dropBeforeNodeId === draggingNodeId ? null : dropBeforeNodeId;
      builder.reorderNode(draggingNodeId, target);
    }
    setDraggingNodeId(null);
    setDropBeforeNodeId(null);
  };

  const handleAdd = (nodeType: WorkflowBuilderNodeDraft["nodeType"]) => {
    builder.addNode(nodeType);
    setAddOpen(false);
  };

  const handleRemove = async (node: WorkflowBuilderNodeDraft, position: number) => {
    const shouldRemove = await confirm({
      title: "Schritt entfernen?",
      description: `Schritt #${position} (${getWorkflowBuilderNodeTypeLabel(node.nodeType)}) und seine Verknüpfungen werden aus dem Entwurf entfernt.`,
      confirmLabel: "Schritt entfernen",
      cancelLabel: "Abbrechen",
      tone: "danger",
    });

    if (!shouldRemove) {
      return;
    }

    builder.removeNode(node.id);
  };

  const renderStepCard = (
    node: WorkflowBuilderNodeDraft,
    index: number,
    options?: { dnd?: boolean }
  ) => {
    const dndEnabled = Boolean(options?.dnd);
    return (
      <WorkflowBuilderStepCard
        node={node}
        index={index}
        totalCount={nodes.length}
        canManageAdvanced={canManageAdvanced}
        versionDraft={builder.versionDraft}
        workflowDefinitions={builder.definitions}
        actionDefinitions={builder.actionDefinitions}
        automationPropertyCatalog={builder.automationPropertyCatalog}
        taskTemplates={builder.taskTemplates}
        answerDefinitions={builder.answerDefinitions}
        taskTemplateConditions={builder.taskTemplateConditions}
        taskTemplateDependencies={builder.taskTemplateDependencies}
        responsibilityOwners={builder.responsibilityOwners}
        onUpdate={(patch) => builder.updateNode(node.id, patch)}
        onMoveUp={() => builder.moveNode(node.id, "up")}
        onMoveDown={() => builder.moveNode(node.id, "down")}
        onRemove={() => void handleRemove(node, index + 1)}
        onAddAction={(actionKey) => builder.addActionFromDefinition(node.id, actionKey)}
        onUpdateAction={(actionId, patch) => builder.updateAction(node.id, actionId, patch)}
        onRemoveAction={(actionId) => builder.removeAction(node.id, actionId)}
        onAddOutgoingEdge={() => {
          const newEdgeId = builder.addEdge({ sourceNodeKey: node.nodeKey });
          if (newEdgeId) {
            onSelectEdge(newEdgeId);
          }
        }}
        onJumpToEdge={(edgeId) => onSelectEdge(edgeId)}
        onDragStart={dndEnabled ? () => handleDragStart(node.id) : undefined}
        onDragEnd={dndEnabled ? handleDragEnd : undefined}
        isDragging={dndEnabled && draggingNodeId === node.id}
      />
    );
  };

  return (
    <section className="wf-form-section wf-form-section--canvas">
      <div className="wf-form-section-head">
        <h2 className="wf-form-section-title">2 — Schritte</h2>
        <p className="wf-form-section-subtitle">
          Schritt im Diagramm anklicken, um ihn rechts zu bearbeiten. Übergänge können direkt im
          Diagramm angeklickt werden. Für eine neue Verbindung den „+"-Anker am rechten Rand eines
          Schritts klicken und anschließend den Zielschritt anwählen. Die Reihenfolge folgt aus den
          Übergängen — Hoch/Runter wirkt nur als Tie-Breaker.
        </p>
      </div>

      {nodes.length === 0 ? (
        <p className="wf-step-card-hint wf-step-card-hint--info">
          Noch keine Schritte definiert.
          {!hasStart && " Beginne mit einem Start-Schritt über das Dropdown unten."}
        </p>
      ) : (
        <div className="wf-builder-canvas-grid">
          <div className="wf-builder-canvas-area">
            {connectingFromNodeId ? (
              <div className="wf-builder-connect-banner" role="status">
                <Link2 size={14} aria-hidden="true" />
                <span>
                  Verbindung von{" "}
                  <strong>{connectingFromNodeLabel ?? "Schritt"}</strong>: jetzt einen Zielschritt
                  im Diagramm anklicken.
                </span>
                <button
                  type="button"
                  className="btn btn-ghost wf-builder-connect-banner-cancel"
                  onClick={onCancelConnect}
                >
                  Abbrechen (Esc)
                </button>
              </div>
            ) : null}
            <WorkflowBuilderGraphPreview
              nodes={nodes}
              edges={edges}
              variant="canvas"
              selectedNodeId={selectedNodeId}
              selectedEdgeId={selectedEdgeId}
              onNodeClick={(nodeId) => onSelectNode(nodeId)}
              onEdgeClick={(edgeId) => onSelectEdge(edgeId)}
              nodeIssueCounts={nodeIssueCounts}
              edgeIssueCounts={edgeIssueCounts}
              connectingFromNodeId={canManageAdvanced ? connectingFromNodeId : null}
              onStartConnect={canManageAdvanced ? onStartConnect : undefined}
              onCancelConnect={onCancelConnect}
            />
          </div>
          <aside className="wf-builder-properties-panel" aria-label="Eigenschaften">
            <BuilderPropertiesPanel
              builder={builder}
              canManageAdvanced={canManageAdvanced}
              selectedNode={selectedNode}
              selectedNodeIndex={selectedNodeIndex}
              selectedEdge={selectedEdge}
              nodes={nodes}
              renderStepCard={renderStepCard}
              onSelectNode={onSelectNode}
              onClearSelection={onClearSelection}
              issueIndex={issueIndex}
            />
          </aside>
        </div>
      )}

      <div className="wf-step-add">
        {!hasStart && (
          <button
            type="button"
            className="btn btn-secondary wf-step-add-start"
            onClick={() => handleAdd("start")}
            disabled={!canManageAdvanced}
          >
            + Start-Schritt anlegen
          </button>
        )}

        <div className="wf-step-add-dropdown">
          <button
            type="button"
            className="btn btn-primary"
            onClick={() => setAddOpen((v) => !v)}
            aria-expanded={addOpen}
            aria-haspopup="menu"
            disabled={!canManageAdvanced}
          >
            <span>+ Schritt hinzufügen</span>
            <ChevronDown size={14} aria-hidden="true" />
          </button>
          {addOpen && (
            <div className="wf-step-add-menu" role="menu">
              {STEP_CATEGORY_GROUPS.map((group) => (
                <div key={group.label} className="wf-builder-toolbar-add-group">
                  <div className="wf-builder-toolbar-add-group-label">{group.label}</div>
                  {group.items.map((opt) => (
                    <button
                      key={opt.key}
                      type="button"
                      className="wf-step-add-menu-item"
                      role="menuitem"
                      onClick={() => handleAdd(opt.key)}
                    >
                      {opt.label}
                    </button>
                  ))}
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {nodes.length > 0 ? (
        <details
          className="wf-form-collapsible"
          open={listOpen}
          onToggle={(event) => setListOpen((event.target as HTMLDetailsElement).open)}
        >
          <summary className="wf-form-collapsible-summary">
            Listenansicht aller Schritte ({nodes.length})
          </summary>
          <div
            className="wf-step-list wf-form-collapsible-body"
            onDragOver={(event) => {
              if (!draggingNodeId) return;
              event.preventDefault();
              event.dataTransfer.dropEffect = "move";
            }}
            onDrop={(event) => {
              event.preventDefault();
              handleDragEnd();
            }}
          >
            {nodes.map((node, index) => {
              const issues = issueIndex.byNodeId.get(node.id);
              const issueCount = issues?.length ?? 0;
              const severity = highestSeverity(issues);
              return (
                <div
                  key={node.id}
                  id={stepAnchorId(node)}
                  className={`wf-step-list-item${draggingNodeId === node.id ? " wf-step-list-item--dragging" : ""}${dropBeforeNodeId === node.id && draggingNodeId && draggingNodeId !== node.id ? " wf-step-list-item--drop-before" : ""}${selectedNodeId === node.id ? " wf-step-list-item--selected" : ""}${issueCount > 0 ? ` wf-step-list-item--issue wf-step-list-item--issue-${severity ?? "info"}` : ""}`}
                  onDragOver={(event) => handleDragOver(node.id, event)}
                  onClick={() => onSelectNode(node.id)}
                >
                  {issueCount > 0 ? (
                    <span
                      className={`wf-list-issue-badge wf-list-issue-badge--${severity ?? "info"}`}
                      title={`${issueCount} ${issueCount === 1 ? "Issue" : "Issues"} an diesem Schritt`}
                    >
                      <AlertTriangle size={11} aria-hidden="true" />
                      <span>{issueCount}</span>
                    </span>
                  ) : null}
                  {renderStepCard(node, index, { dnd: true })}
                </div>
              );
            })}
          </div>
        </details>
      ) : null}
    </section>
  );
}

function BuilderPropertiesPanel({
  builder,
  canManageAdvanced,
  selectedNode,
  selectedNodeIndex,
  selectedEdge,
  nodes,
  renderStepCard,
  onSelectNode,
  onClearSelection,
  issueIndex,
}: {
  builder: ReturnType<typeof useAdminWorkflowBuilder>;
  canManageAdvanced: boolean;
  selectedNode: WorkflowBuilderNodeDraft | null;
  selectedNodeIndex: number;
  selectedEdge: WorkflowBuilderEdgeDraft | null;
  nodes: WorkflowBuilderNodeDraft[];
  renderStepCard: (node: WorkflowBuilderNodeDraft, index: number) => ReactElement;
  onSelectNode: (nodeId: string) => void;
  onClearSelection: () => void;
  issueIndex: WorkflowBuilderIssueIndex;
}) {
  if (selectedNode) {
    const nodeIssues = issueIndex.byNodeId.get(selectedNode.id) ?? [];
    return (
      <div className="wf-properties-panel-inner">
        <header className="wf-properties-panel-head">
          <span className="wf-properties-panel-eyebrow">Schritt-Eigenschaften</span>
          <button
            type="button"
            className="wf-properties-panel-close"
            onClick={onClearSelection}
            aria-label="Auswahl aufheben"
          >
            <X size={14} aria-hidden="true" />
          </button>
        </header>
        <div className="wf-properties-panel-body">
          <PropertiesPanelIssueList issues={nodeIssues} subject="Schritt" />
          {renderStepCard(selectedNode, selectedNodeIndex)}
        </div>
      </div>
    );
  }

  if (selectedEdge) {
    const edgeIssues = issueIndex.byEdgeId.get(selectedEdge.id) ?? [];
    return (
      <div className="wf-properties-panel-inner">
        <header className="wf-properties-panel-head">
          <span className="wf-properties-panel-eyebrow">Übergangs-Eigenschaften</span>
          <button
            type="button"
            className="wf-properties-panel-close"
            onClick={onClearSelection}
            aria-label="Auswahl aufheben"
          >
            <X size={14} aria-hidden="true" />
          </button>
        </header>
        <div className="wf-properties-panel-body">
          <PropertiesPanelIssueList issues={edgeIssues} subject="Übergang" />
          <EdgePropertiesPanel
            builder={builder}
            edge={selectedEdge}
            nodes={nodes}
            canManageAdvanced={canManageAdvanced}
            onSelectNode={onSelectNode}
            onAfterDelete={onClearSelection}
          />
        </div>
      </div>
    );
  }

  return (
    <div className="wf-properties-panel-inner wf-properties-panel-inner--empty">
      <p className="wf-properties-panel-hint">
        Schritt oder Übergang im Diagramm anklicken, um Eigenschaften hier zu bearbeiten.
      </p>
      <p className="wf-properties-panel-hint wf-properties-panel-hint--muted">
        Eine Listenansicht aller Schritte steht weiter unten als Detailbereich zur Verfügung.
      </p>
    </div>
  );
}

function PropertiesPanelIssueList({
  issues,
  subject,
}: {
  issues: WorkflowBuilderIssueRef[];
  subject: "Schritt" | "Übergang";
}) {
  if (issues.length === 0) return null;
  const severity = highestSeverity(issues) ?? "info";
  return (
    <div className={`wf-properties-issues wf-properties-issues--${severity}`} role="status">
      <div className="wf-properties-issues-head">
        <AlertTriangle size={13} aria-hidden="true" />
        <span>
          {issues.length} {issues.length === 1 ? "Issue" : "Issues"} an diesem {subject}
        </span>
      </div>
      <ul className="wf-properties-issues-list">
        {issues.map((issue, idx) => (
          <li
            key={`${issue.origin}-${idx}`}
            className={`wf-properties-issues-item wf-properties-issues-item--${issue.severity}`}
          >
            <span className="wf-properties-issues-item-message">{issue.message}</span>
            <span className="wf-properties-issues-item-meta">
              {issue.origin === "local" ? "Lokal" : "Backend"}
              {issue.code ? ` · ${issue.code}` : ""}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}

function EdgePropertiesPanel({
  builder,
  edge,
  nodes,
  canManageAdvanced,
  onSelectNode,
  onAfterDelete,
}: {
  builder: ReturnType<typeof useAdminWorkflowBuilder>;
  edge: WorkflowBuilderEdgeDraft;
  nodes: WorkflowBuilderNodeDraft[];
  canManageAdvanced: boolean;
  onSelectNode: (nodeId: string) => void;
  onAfterDelete: () => void;
}) {
  const [conditionOpen, setConditionOpen] = useState(false);
  const confirm = useConfirmationDialog();

  const sourceNode = nodes.find(
    (n) => n.nodeKey.trim().toLowerCase() === edge.sourceNodeKey.trim().toLowerCase()
  );
  const targetNode = nodes.find(
    (n) => n.nodeKey.trim().toLowerCase() === edge.targetNodeKey.trim().toLowerCase()
  );
  const isDecisionSource = sourceNode?.nodeType === "decision";

  const nodeOptions = nodes
    .map((node) => {
      const key = node.nodeKey.trim();
      if (!key) return null;
      const title = node.title.trim();
      return { key, label: title ? `${title} (${key})` : key };
    })
    .filter((opt): opt is { key: string; label: string } => opt !== null);

  const handleDelete = async () => {
    const shouldRemove = await confirm({
      title: "Übergang entfernen?",
      description: "Dieser Übergang wird aus dem Entwurf entfernt.",
      confirmLabel: "Übergang entfernen",
      cancelLabel: "Abbrechen",
      tone: "danger",
    });
    if (!shouldRemove) return;
    builder.removeEdge(edge.id);
    onAfterDelete();
  };

  return (
    <div className="wf-edge-panel">
      <div className="wf-edge-panel-route">
        {sourceNode ? (
          <button
            type="button"
            className="wf-edge-panel-chip"
            onClick={() => onSelectNode(sourceNode.id)}
            title="Quell-Schritt auswählen"
          >
            {sourceNode.title.trim() || sourceNode.nodeKey.trim() || "Quelle"}
          </button>
        ) : (
          <span className="wf-edge-panel-chip wf-edge-panel-chip--missing">{edge.sourceNodeKey || "?"}</span>
        )}
        <span className="wf-edge-panel-arrow" aria-hidden="true">→</span>
        {targetNode ? (
          <button
            type="button"
            className="wf-edge-panel-chip"
            onClick={() => onSelectNode(targetNode.id)}
            title="Ziel-Schritt auswählen"
          >
            {targetNode.title.trim() || targetNode.nodeKey.trim() || "Ziel"}
          </button>
        ) : (
          <span className="wf-edge-panel-chip wf-edge-panel-chip--missing">{edge.targetNodeKey || "?"}</span>
        )}
      </div>

      <div className="wf-form-fields">
        <div className="wf-form-field">
          <label className="form-label" htmlFor={`wf-edge-source-${edge.id}`}>Von Schritt</label>
          <select
            id={`wf-edge-source-${edge.id}`}
            className="form-select"
            value={edge.sourceNodeKey}
            onChange={(e) => builder.updateEdge(edge.id, { sourceNodeKey: e.target.value })}
            disabled={!canManageAdvanced}
          >
            <option value="">– wählen –</option>
            {nodeOptions.map((opt) => (
              <option key={opt.key} value={opt.key}>{opt.label}</option>
            ))}
          </select>
        </div>

        <div className="wf-form-field">
          <label className="form-label" htmlFor={`wf-edge-target-${edge.id}`}>Zu Schritt</label>
          <select
            id={`wf-edge-target-${edge.id}`}
            className="form-select"
            value={edge.targetNodeKey}
            onChange={(e) => builder.updateEdge(edge.id, { targetNodeKey: e.target.value })}
            disabled={!canManageAdvanced}
          >
            <option value="">– wählen –</option>
            {nodeOptions.map((opt) => (
              <option key={opt.key} value={opt.key}>{opt.label}</option>
            ))}
          </select>
        </div>

        <div className="wf-form-field">
          <label className="form-label" htmlFor={`wf-edge-priority-${edge.id}`}>Pfad-Reihenfolge</label>
          <input
            id={`wf-edge-priority-${edge.id}`}
            className="form-input wf-edge-priority-input"
            type="number"
            min={1}
            value={edge.priority}
            onChange={(e) => builder.updateEdge(edge.id, { priority: e.target.value })}
            disabled={!canManageAdvanced}
          />
          <p className="wf-form-field-hint">
            Pfade an einer Verzweigung werden in dieser Reihenfolge geprüft.
          </p>
        </div>

        <div className="wf-form-field">
          <label className="form-label">Bedingung</label>
          {isDecisionSource ? (
            <button
              type="button"
              className="wf-edge-condition-trigger"
              onClick={() => setConditionOpen(true)}
              aria-label="Bedingung bearbeiten"
              disabled={!canManageAdvanced}
            >
              <span className="wf-edge-condition-summary">
                {summarizeCondition(edge.conditionExpression)}
              </span>
              <Pencil size={12} aria-hidden="true" />
            </button>
          ) : (
            <p className="wf-form-readonly-value text-secondary">
              Nur bei Entscheidung-Schritten relevant.
            </p>
          )}
        </div>
      </div>

      <div className="wf-edge-panel-actions">
        <button
          type="button"
          className="btn btn-ghost btn-danger"
          onClick={() => void handleDelete()}
          disabled={!canManageAdvanced}
        >
          Übergang entfernen
        </button>
      </div>

      {conditionOpen ? (
        <ConditionEditorDrawer
          edge={edge}
          nodes={nodes}
          answerDefinitions={builder.answerDefinitions}
          onClose={() => setConditionOpen(false)}
          onChange={(next) => builder.updateEdge(edge.id, { conditionExpression: next })}
        />
      ) : null}
    </div>
  );
}

function Section3Edges({
  builder,
  canManageAdvanced,
  selectedEdgeId,
  onSelectEdge,
  issueIndex,
}: {
  builder: ReturnType<typeof useAdminWorkflowBuilder>;
  canManageAdvanced: boolean;
  selectedEdgeId: string | null;
  onSelectEdge: (edgeId: string) => void;
  issueIndex: WorkflowBuilderIssueIndex;
}) {
  const [conditionEdgeId, setConditionEdgeId] = useState<string | null>(null);
  const [tableOpen, setTableOpen] = useState(false);
  const nodes = builder.versionDraft.nodes;
  const edges = builder.versionDraft.edges;

  const nodeOptions = nodes
    .map((node) => {
      const key = node.nodeKey.trim();
      if (!key) return null;
      const title = node.title.trim();
      return { key, label: title ? `${title} (${key})` : key };
    })
    .filter((opt): opt is { key: string; label: string } => opt !== null);

  const decisionKeys = new Set(
    nodes
      .filter((node) => node.nodeType === "decision")
      .map((node) => node.nodeKey.trim().toLowerCase())
  );

  const sortedEdges = [...edges]
    .map((edge, originalIndex) => ({ edge, originalIndex }))
    .sort((a, b) => {
      const sa = a.edge.sourceNodeKey.trim().toLowerCase();
      const sb = b.edge.sourceNodeKey.trim().toLowerCase();
      if (sa !== sb) return sa.localeCompare(sb);
      const pa = Number(a.edge.priority);
      const pb = Number(b.edge.priority);
      return (Number.isFinite(pa) ? pa : 999) - (Number.isFinite(pb) ? pb : 999);
    });

  return (
    <section className="wf-form-section">
      <div className="wf-form-section-head">
        <h2 className="wf-form-section-title">3 — Übergänge</h2>
        <p className="wf-form-section-subtitle">
          Übergänge werden primär im Diagramm in Sektion 2 angeklickt und im rechten Eigenschaften-Panel
          bearbeitet. Die Tabelle hier ist eine Detail-/Listenansicht.
        </p>
      </div>

      {nodeOptions.length < 2 ? (
        <p className="wf-step-card-hint wf-step-card-hint--info">
          Mindestens zwei Schritte mit Schritt-Key in Sektion 2 anlegen, bevor Übergänge gepflegt werden können.
        </p>
      ) : edges.length === 0 ? (
        <p className="wf-step-card-hint wf-step-card-hint--info">
          Noch keine Übergänge definiert. Über „+ Übergang hinzufügen" eine erste Verbindung anlegen.
        </p>
      ) : (
      <details
        className="wf-form-collapsible"
        open={tableOpen}
        onToggle={(event) => setTableOpen((event.target as HTMLDetailsElement).open)}
      >
        <summary className="wf-form-collapsible-summary">
          Tabellenansicht aller Übergänge ({edges.length})
        </summary>
        <div className="wf-edge-table-wrap wf-form-collapsible-body">
          <table className="wf-edge-table">
            <thead>
              <tr>
                <th>Von Schritt</th>
                <th aria-hidden="true" className="wf-edge-table-arrow">→</th>
                <th>Zu Schritt</th>
                <th>Pfad-Reihenfolge</th>
                <th>Bedingung</th>
                <th aria-label="Aktionen" />
              </tr>
            </thead>
            <tbody>
              {sortedEdges.map(({ edge }) => {
                const sourceNormalized = edge.sourceNodeKey.trim().toLowerCase();
                const isDecision = decisionKeys.has(sourceNormalized);
                const edgeIssues = issueIndex.byEdgeId.get(edge.id);
                const issueCount = edgeIssues?.length ?? 0;
                const severity = highestSeverity(edgeIssues);
                const rowClass = [
                  selectedEdgeId === edge.id ? "wf-edge-table-row--selected" : null,
                  issueCount > 0 ? `wf-edge-table-row--issue wf-edge-table-row--issue-${severity ?? "info"}` : null,
                ].filter(Boolean).join(" ") || undefined;
                return (
                  <tr
                    key={edge.id}
                    id={edgeAnchorId(edge)}
                    className={rowClass}
                    onClick={() => onSelectEdge(edge.id)}
                  >
                    <td data-label="Von Schritt">
                      {issueCount > 0 ? (
                        <span
                          className={`wf-list-issue-badge wf-list-issue-badge--${severity ?? "info"} wf-list-issue-badge--inline`}
                          title={`${issueCount} ${issueCount === 1 ? "Issue" : "Issues"} an diesem Übergang`}
                        >
                          <AlertTriangle size={11} aria-hidden="true" />
                          <span>{issueCount}</span>
                        </span>
                      ) : null}
                      <select
                        className="form-select"
                        value={edge.sourceNodeKey}
                        onChange={(e) => builder.updateEdge(edge.id, { sourceNodeKey: e.target.value })}
                      >
                        <option value="">– wählen –</option>
                        {nodeOptions.map((opt) => (
                          <option key={opt.key} value={opt.key}>{opt.label}</option>
                        ))}
                      </select>
                    </td>
                    <td className="wf-edge-table-arrow" aria-hidden="true">→</td>
                    <td data-label="Zu Schritt">
                      <select
                        className="form-select"
                        value={edge.targetNodeKey}
                        onChange={(e) => builder.updateEdge(edge.id, { targetNodeKey: e.target.value })}
                      >
                        <option value="">– wählen –</option>
                        {nodeOptions.map((opt) => (
                          <option key={opt.key} value={opt.key}>{opt.label}</option>
                        ))}
                      </select>
                    </td>
                    <td data-label="Pfad-Reihenfolge">
                      <input
                        className="form-input wf-edge-priority-input"
                        type="number"
                        min={1}
                        value={edge.priority}
                        onChange={(e) => builder.updateEdge(edge.id, { priority: e.target.value })}
                      />
                    </td>
                    <td data-label="Bedingung">
                      {isDecision ? (
                        <button
                          type="button"
                          className="wf-edge-condition-trigger"
                          onClick={() => setConditionEdgeId(edge.id)}
                          aria-label="Bedingung bearbeiten"
                        >
                          <span className="wf-edge-condition-summary">
                            {summarizeCondition(edge.conditionExpression)}
                          </span>
                          <Pencil size={12} aria-hidden="true" />
                        </button>
                      ) : (
                        <span
                          className="text-secondary"
                          style={{ fontSize: "0.8rem" }}
                          title="Nur bei Entscheidung-Schritten relevant."
                        >
                          —
                        </span>
                      )}
                    </td>
                    <td data-label="Aktionen">
                      <button
                        type="button"
                        className="wf-step-card-iconbtn wf-step-card-iconbtn--danger"
                        onClick={(event) => {
                          event.stopPropagation();
                          builder.removeEdge(edge.id);
                        }}
                        aria-label="Übergang entfernen"
                      ><X size={16} aria-hidden="true" /></button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </details>
      )}

      <div className="wf-edge-table-footer">
        <button
          type="button"
          className="btn btn-secondary"
          onClick={() => builder.addEdge()}
          disabled={!canManageAdvanced || nodeOptions.length < 2}
        >
          + Übergang hinzufügen
        </button>
      </div>

      {conditionEdgeId ? (
        <ConditionEditorDrawer
          edge={edges.find((e) => e.id === conditionEdgeId) ?? null}
          nodes={nodes}
          answerDefinitions={builder.answerDefinitions}
          onClose={() => setConditionEdgeId(null)}
          onChange={(next) => builder.updateEdge(conditionEdgeId, { conditionExpression: next })}
        />
      ) : null}
    </section>
  );
}

function ConditionEditorDrawer({
  edge,
  nodes,
  answerDefinitions,
  onClose,
  onChange,
}: {
  edge: WorkflowBuilderEdgeDraft | null;
  nodes: WorkflowBuilderNodeDraft[];
  answerDefinitions: ReturnType<typeof useAdminWorkflowBuilder>["answerDefinitions"];
  onClose: () => void;
  onChange: (next: string) => void;
}) {
  if (!edge) return null;

  const sourceNode = nodes.find(
    (n) => n.nodeKey.trim().toLowerCase() === edge.sourceNodeKey.trim().toLowerCase()
  );
  const targetNode = nodes.find(
    (n) => n.nodeKey.trim().toLowerCase() === edge.targetNodeKey.trim().toLowerCase()
  );
  const sourceLabel = sourceNode?.title.trim() || sourceNode?.nodeKey.trim() || edge.sourceNodeKey || "?";
  const targetLabel = targetNode?.title.trim() || targetNode?.nodeKey.trim() || edge.targetNodeKey || "?";

  return (
    <>
      <div className="admin-drawer-backdrop" onClick={onClose} aria-hidden="true" />
      <aside
        className="admin-drawer wf-condition-drawer"
        role="dialog"
        aria-modal="true"
        aria-label="Bedingung bearbeiten"
      >
        <header className="admin-drawer-head">
          <div className="admin-drawer-title">
            <h2>Bedingung für Übergang</h2>
            <span className="wf-condition-drawer-route">
              <strong>{sourceLabel}</strong>
              <span aria-hidden="true">→</span>
              <strong>{targetLabel}</strong>
            </span>
          </div>
          <button type="button" className="admin-drawer-close" onClick={onClose} aria-label="Schließen">
            ×
          </button>
        </header>

        <div className="admin-drawer-body content-stack">
          <p className="wf-form-field-hint">
            Diese Bedingung steuert, ob dieser Pfad ausgeführt wird, wenn der Decision-Knoten erreicht wird.
            Pfade werden in der Reihenfolge ihrer „Pfad-Reihenfolge" geprüft.
          </p>

          <WorkflowBuilderConditionEditor
            conditionExpression={edge.conditionExpression}
            answerDefinitions={answerDefinitions}
            onChange={onChange}
          />
        </div>
      </aside>
    </>
  );
}

function Section4Validation({
  builder,
  issueIndex,
}: {
  builder: ReturnType<typeof useAdminWorkflowBuilder>;
  issueIndex: WorkflowBuilderIssueIndex;
}) {
  const localIssues = builder.localValidationIssues;
  const backendIssues = builder.versionDetail?.validationIssues ?? [];
  const totalCount = localIssues.length + backendIssues.length;
  const mappedCount = totalCount - issueIndex.general.length;
  const draft = builder.versionDraft;

  return (
    <section className="wf-form-section">
      <div className="wf-form-section-head">
        <h2 className="wf-form-section-title">4 — Validierung</h2>
        <p className="wf-form-section-subtitle">Funktioniert das, was ich gebaut habe?</p>
      </div>

      <div className="wf-validation-toolbar">
        <button
          type="button"
          className="btn btn-secondary"
          onClick={() => builder.validateDraft()}
          disabled={!builder.selectedVersionSummary}
        >
          Lokal prüfen
        </button>
        <span className="wf-form-field-hint" style={{ margin: 0 }}>
          Backend-Issues stammen aus dem zuletzt gespeicherten Stand.
          {mappedCount > 0 ? ` ${mappedCount} ${mappedCount === 1 ? "Issue ist" : "Issues sind"} direkt am Schritt oder Übergang im Diagramm markiert.` : ""}
        </span>
      </div>

      {totalCount === 0 ? (
        <div className="panel panel-success">
          <p className="panel-text">Keine offenen Validierungs-Issues — bereit zum Veröffentlichen.</p>
        </div>
      ) : (
        <div className="wf-validation-list">
          {localIssues.length > 0 && (
            <div>
              <h3 className="wf-validation-group-title">Lokal ({localIssues.length})</h3>
              <ul className="wf-validation-items">
                {localIssues.map((issue, idx) => {
                  const anchor = resolveLocalIssueAnchor(issue, draft);
                  return (
                    <li
                      key={`local-${idx}`}
                      className={`wf-validation-item wf-validation-item--local${anchor ? " wf-validation-item--clickable" : ""}`}
                    >
                      <span className="wf-validation-bullet">●</span>
                      <div className="wf-validation-item-body">
                        {anchor ? (
                          <button type="button" className="wf-validation-link" onClick={() => jumpToAnchor(anchor)}>
                            {issue.message}
                          </button>
                        ) : (
                          <span>{issue.message}</span>
                        )}
                        <span className="wf-validation-meta">
                          {issue.scope}
                          {issue.referenceKey ? ` · ${issue.referenceKey}` : ""}
                        </span>
                      </div>
                    </li>
                  );
                })}
              </ul>
            </div>
          )}

          {backendIssues.length > 0 && (
            <div>
              <h3 className="wf-validation-group-title">Backend ({backendIssues.length})</h3>
              <ul className="wf-validation-items">
                {backendIssues.map((issue, idx) => {
                  const anchor = resolveBackendIssueAnchor(issue.scope, issue.referenceKey, draft);
                  return (
                    <li
                      key={`backend-${idx}`}
                      className={`wf-validation-item wf-validation-item--${issue.severity || "info"}${anchor ? " wf-validation-item--clickable" : ""}`}
                    >
                      <span className="wf-validation-bullet">●</span>
                      <div className="wf-validation-item-body">
                        {anchor ? (
                          <button type="button" className="wf-validation-link" onClick={() => jumpToAnchor(anchor)}>
                            {issue.message}
                          </button>
                        ) : (
                          <span>{issue.message}</span>
                        )}
                        <span className="wf-validation-meta">
                          {issue.scope} · {issue.code}
                          {issue.referenceKey ? ` · ${issue.referenceKey}` : ""}
                        </span>
                      </div>
                    </li>
                  );
                })}
              </ul>
            </div>
          )}
        </div>
      )}
    </section>
  );
}

// ─── Published Version Banner ───────────────────────────────────────────────

function PublishedVersionBanner({
  builder,
}: {
  builder: ReturnType<typeof useAdminWorkflowBuilder>;
}) {
  const summary = builder.selectedVersionSummary;
  if (!summary || summary.status !== "published") return null;

  const drafts = (builder.selectedDefinition?.versions ?? []).filter((v) => v.status !== "published");
  const latestDraft = drafts.length > 0 ? drafts[drafts.length - 1] : null;

  return (
    <div className="wf-published-banner" role="note">
      <div className="wf-published-banner-icon" aria-hidden="true">!</div>
      <div className="wf-published-banner-body">
        <strong>Version {summary.versionNumber} ist veröffentlicht.</strong>
        <span>
          {" "}
          Änderungen wirken sich direkt auf laufende Workflows aus. Für sicherheitsrelevante Anpassungen
          {latestDraft ? " auf einen vorhandenen Entwurf wechseln" : " einen neuen Workflow anlegen"}.
        </span>
      </div>
      {latestDraft ? (
        <button
          type="button"
          className="btn btn-secondary"
          onClick={() => void builder.selectVersion(latestDraft.id)}
        >
          Zum Entwurf (Version {latestDraft.versionNumber})
        </button>
      ) : null}
    </div>
  );
}

// ─── Sticky Action Toolbar ───────────────────────────────────────────────────

function BuilderActionToolbar({
  builder,
  canManageAdvanced,
}: {
  builder: ReturnType<typeof useAdminWorkflowBuilder>;
  canManageAdvanced: boolean;
}) {
  const [addOpen, setAddOpen] = useState(false);
  const hasChanges = builder.hasUnsavedChanges;
  const versionStatus = builder.selectedVersionSummary?.status ?? "draft";
  const isPublished = versionStatus === "published";
  const versionNumber = builder.selectedVersionSummary?.versionNumber;
  const localIssueCount = builder.localValidationIssues.length;
  const backendIssueCount = builder.versionDetail?.validationIssues.length ?? 0;
  const issueCount = localIssueCount + backendIssueCount;
  const canPublish = Boolean(
    canManageAdvanced
    && builder.selectedVersionSummary?.canPublish
    && !builder.hasUnsavedChanges
  );

  const handleDiscard = async () => {
    await builder.discardChanges();
  };

  const handleAddStep = (nodeType: WorkflowBuilderNodeDraft["nodeType"]) => {
    builder.addNode(nodeType);
    setAddOpen(false);
  };

  const handleScrollToValidation = () => {
    const validationSection = document.querySelector(".wf-form-section:last-of-type");
    if (validationSection) {
      validationSection.scrollIntoView({ behavior: "smooth", block: "start" });
    }
  };

  return (
    <div className="wf-builder-toolbar">
      <div className="wf-builder-toolbar-status">
        <h2 className="wf-builder-toolbar-title">
          {builder.selectedDefinition?.name || builder.selectedDefinition?.key || "Workflow"}
        </h2>
        {versionNumber !== undefined ? (
          <span className={`wf-builder-toolbar-badge wf-builder-toolbar-badge--${isPublished ? "published" : "draft"}`}>
            Version {versionNumber} · {isPublished ? "Veröffentlicht" : "Entwurf"}
          </span>
        ) : null}
        {hasChanges ? (
          <span className="wf-builder-toolbar-dirty" title="Ungespeicherte Änderungen">
            <span className="wf-builder-toolbar-dirty-dot" aria-hidden="true" />
            <span>Ungespeichert</span>
          </span>
        ) : null}
      </div>

      <div className="wf-builder-toolbar-actions">
        <button
          type="button"
          className={`wf-builder-toolbar-issues ${issueCount === 0 ? "wf-builder-toolbar-issues--ok" : "wf-builder-toolbar-issues--warn"}`}
          onClick={handleScrollToValidation}
          aria-label={issueCount === 0 ? "Keine Validierungs-Issues" : `${issueCount} Validierungs-Issues anzeigen`}
        >
          {issueCount === 0 ? (
            <>✓ Keine Issues</>
          ) : (
            <>⚠ {issueCount} {issueCount === 1 ? "Issue" : "Issues"}</>
          )}
        </button>

        <div className="wf-builder-toolbar-add">
          <button
            type="button"
            className="btn btn-secondary"
            onClick={() => setAddOpen((v) => !v)}
            aria-expanded={addOpen}
            aria-haspopup="menu"
            disabled={!canManageAdvanced}
          >
            <span>+ Schritt</span>
            <ChevronDown size={14} aria-hidden="true" />
          </button>
          {addOpen ? (
            <div className="wf-builder-toolbar-add-menu" role="menu">
              {STEP_CATEGORY_GROUPS.map((group) => (
                <div key={group.label} className="wf-builder-toolbar-add-group">
                  <div className="wf-builder-toolbar-add-group-label">{group.label}</div>
                  {group.items.map((item) => (
                    <button
                      key={item.key}
                      type="button"
                      className="wf-builder-toolbar-add-item"
                      role="menuitem"
                      onClick={() => handleAddStep(item.key)}
                    >
                      {item.label}
                    </button>
                  ))}
                </div>
              ))}
            </div>
          ) : null}
        </div>

        {hasChanges ? (
          <button
            type="button"
            className="btn btn-ghost"
            onClick={() => void handleDiscard()}
            disabled={builder.isSaving || builder.isPublishing}
          >
            Verwerfen
          </button>
        ) : null}

        <button
          type="button"
          className="btn btn-secondary"
          onClick={() => void builder.saveVersion()}
          disabled={!hasChanges || builder.isSaving || builder.isPublishing || !builder.selectedDefinition}
        >
          {builder.isSaving ? "Speichert …" : "Speichern"}
        </button>

        <button
          type="button"
          className="btn btn-primary"
          onClick={() => void builder.publishVersion()}
          disabled={!canPublish || builder.isSaving || builder.isPublishing}
          title={
            !canManageAdvanced
              ? "Nur im Admin-Modus verfügbar"
              : hasChanges
                ? "Erst speichern, dann veröffentlichbar"
                : !builder.selectedVersionSummary?.canPublish
                  ? "Entwurf hat offene Validierungs-Issues"
                  : undefined
          }
        >
          {builder.isPublishing ? "Veröffentlichen …" : "Veröffentlichen"}
        </button>
      </div>
    </div>
  );
}
