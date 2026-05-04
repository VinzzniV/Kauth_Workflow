import { useMemo, useState } from "react";
import { ChevronDown, ChevronRight, X } from "lucide-react";
import { useCurrentUser } from "../../auth/useCurrentUser";
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
import { getWorkflowBuilderNodeTypeLabel } from "./workflowBuilderLabels";

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

const STEP_TYPE_OPTIONS: { key: WorkflowBuilderNodeDraft["nodeType"]; label: string }[] = [
  { key: "form", label: "Formular" },
  { key: "approval", label: "Freigabe" },
  { key: "task", label: "Aufgabe" },
  { key: "decision", label: "Entscheidung" },
  { key: "parallel_split", label: "Parallel-Split" },
  { key: "parallel_join", label: "Parallel-Join" },
  { key: "measure_provision", label: "Bereitstellung (Maßnahmen)" },
  { key: "measure_deprovision", label: "Entzug (Maßnahmen)" },
  { key: "measure_change", label: "Änderung (Maßnahmen)" },
  { key: "measure_rename", label: "Umbenennung (Maßnahmen)" },
  { key: "automation", label: "Automatisierung" },
  { key: "end", label: "Ende" },
];

export function AdminWorkflowBuilderFormSection({
  onNotice,
  onError,
}: AdminWorkflowBuilderFormSectionProps) {
  const { capabilities } = useCurrentUser();
  const canManageAdvanced = capabilities.canManageAdminConfiguration;
  const builder = useAdminWorkflowBuilder({ onNotice, onError, canManageAdvanced });

  return (
    <div className="wf-form-editor">
      <WorkflowSelectorBar builder={builder} canManageAdvanced={canManageAdvanced} />

      {builder.isLoading ? (
        <div className="panel panel-muted">
          <p className="panel-text text-secondary">Ablaufdefinitionen werden geladen …</p>
        </div>
      ) : !builder.selectedDefinition ? (
        <div className="panel panel-muted">
          <p className="panel-text text-secondary">Keine Ablaufdefinition ausgewählt.</p>
        </div>
      ) : (
        <div className="wf-form-body">
          <Section1Stammdaten builder={builder} canManageAdvanced={canManageAdvanced} />
          <Section2Steps builder={builder} canManageAdvanced={canManageAdvanced} />
          <Section3Edges builder={builder} canManageAdvanced={canManageAdvanced} />
          <Section4Validation builder={builder} />
        </div>
      )}

      <FormFooter builder={builder} canManageAdvanced={canManageAdvanced} />
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
            if (id) builder.selectDefinition(id);
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
            if (id) builder.selectVersion(id);
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
            className="btn-secondary"
            onClick={() => setCreateOpen((v) => !v)}
            disabled={builder.isCreatingDefinition}
            aria-expanded={createOpen}
          >
            {createOpen ? "Abbrechen" : "+ Neuer Workflow"}
          </button>
          <button
            type="button"
            className="btn-ghost"
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
        <button type="button" className="btn-ghost" onClick={onClose} disabled={builder.isCreatingDefinition}>
          Abbrechen
        </button>
        <button type="submit" className="btn-primary" disabled={!canSubmit}>
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
}: {
  builder: ReturnType<typeof useAdminWorkflowBuilder>;
  canManageAdvanced: boolean;
}) {
  const [addOpen, setAddOpen] = useState(false);
  const arrayNodes = builder.versionDraft.nodes;
  const edges = builder.versionDraft.edges;
  const nodes = useMemo(
    () => topologicallyOrderNodes(arrayNodes, edges),
    [arrayNodes, edges]
  );
  const hasStart = nodes.some((node) => node.nodeType === "start");

  const handleAdd = (nodeType: WorkflowBuilderNodeDraft["nodeType"]) => {
    builder.addNode(nodeType);
    setAddOpen(false);
  };

  const handleRemove = (node: WorkflowBuilderNodeDraft, index: number) => {
    if (!window.confirm(`Schritt #${index + 1} (${getWorkflowBuilderNodeTypeLabel(node.nodeType)}) wirklich entfernen?`)) {
      return;
    }
    builder.removeNode(node.id);
  };

  return (
    <section className="wf-form-section">
      <div className="wf-form-section-head">
        <h2 className="wf-form-section-title">2 — Schritte</h2>
        <p className="wf-form-section-subtitle">
          Welche Schritte hat der Workflow? Reihenfolge folgt aus den Übergängen (Sektion 3) — die Pfeil-Buttons
          (Hoch/Runter pro Schritt) wirken nur als Tie-Breaker bei mehrdeutigen Pfaden.
        </p>
      </div>

      {nodes.length === 0 ? (
        <p className="wf-step-card-hint wf-step-card-hint--info">
          Noch keine Schritte definiert.
          {!hasStart && " Beginne mit einem Start-Schritt über das Dropdown unten."}
        </p>
      ) : (
        <div className="wf-step-list">
          {nodes.map((node, index) => (
            <div key={node.id} id={stepAnchorId(node)}>
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
              onRemove={() => handleRemove(node, index)}
              onAddAction={(actionKey) => builder.addActionFromDefinition(node.id, actionKey)}
              onUpdateAction={(actionId, patch) => builder.updateAction(node.id, actionId, patch)}
              onRemoveAction={(actionId) => builder.removeAction(node.id, actionId)}
            />
            </div>
          ))}
        </div>
      )}

      <div className="wf-step-add">
        {!hasStart && (
          <button
            type="button"
            className="btn-secondary wf-step-add-start"
            onClick={() => handleAdd("start")}
            disabled={!canManageAdvanced}
          >
            + Start-Schritt anlegen
          </button>
        )}

        <div className="wf-step-add-dropdown">
          <button
            type="button"
            className="btn-primary"
            onClick={() => setAddOpen((v) => !v)}
            aria-expanded={addOpen}
            aria-haspopup="menu"
            disabled={!canManageAdvanced}
          >
            <span>+ Schritt hinzufügen</span>
            <ChevronDown size={14} aria-hidden="true" />
          </button>
          {addOpen && (
            <div className="wf-step-add-menu" role="menu" style={{ maxHeight: "320px", overflowY: "auto" }}>
              {STEP_TYPE_OPTIONS.map((opt) => (
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
          )}
        </div>
      </div>
    </section>
  );
}

function Section3Edges({
  builder,
  canManageAdvanced,
}: {
  builder: ReturnType<typeof useAdminWorkflowBuilder>;
  canManageAdvanced: boolean;
}) {
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
        <p className="wf-form-section-subtitle">Wie hängen die Schritte zusammen?</p>
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
        <div className="wf-edge-table-wrap">
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
                return (
                  <tr key={edge.id} id={edgeAnchorId(edge)}>
                    <td>
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
                    <td>
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
                    <td>
                      <input
                        className="form-input wf-edge-priority-input"
                        type="number"
                        min={1}
                        value={edge.priority}
                        onChange={(e) => builder.updateEdge(edge.id, { priority: e.target.value })}
                      />
                    </td>
                    <td>
                      {isDecision ? (
                        <WorkflowBuilderConditionEditor
                          conditionExpression={edge.conditionExpression}
                          answerDefinitions={builder.answerDefinitions}
                          onChange={(next) => builder.updateEdge(edge.id, { conditionExpression: next })}
                        />
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
                    <td>
                      <button
                        type="button"
                        className="wf-step-card-iconbtn wf-step-card-iconbtn--danger"
                        onClick={() => builder.removeEdge(edge.id)}
                        aria-label="Übergang entfernen"
                      ><X size={16} aria-hidden="true" /></button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      <div className="wf-edge-table-footer">
        <button
          type="button"
          className="btn-secondary"
          onClick={() => builder.addEdge()}
          disabled={!canManageAdvanced || nodeOptions.length < 2}
        >
          + Übergang hinzufügen
        </button>
      </div>
    </section>
  );
}

function Section4Validation({
  builder,
}: {
  builder: ReturnType<typeof useAdminWorkflowBuilder>;
}) {
  const localIssues = builder.localValidationIssues;
  const backendIssues = builder.versionDetail?.validationIssues ?? [];
  const totalCount = localIssues.length + backendIssues.length;
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
          className="btn-secondary"
          onClick={() => builder.validateDraft()}
          disabled={!builder.selectedVersionSummary}
        >
          Lokal prüfen
        </button>
        <span className="wf-form-field-hint" style={{ margin: 0 }}>
          Backend-Issues stammen aus dem zuletzt gespeicherten Stand.
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

// ─── Sticky Footer ────────────────────────────────────────────────────────────

function FormFooter({
  builder,
  canManageAdvanced,
}: {
  builder: ReturnType<typeof useAdminWorkflowBuilder>;
  canManageAdvanced: boolean;
}) {
  const hasChanges = builder.hasUnsavedChanges;
  const canPublish = Boolean(
    canManageAdvanced
    && builder.selectedVersionSummary?.canPublish
    && !builder.hasUnsavedChanges
  );

  const handleDiscard = () => {
    if (!window.confirm("Alle ungespeicherten Änderungen verwerfen?")) return;
    builder.selectDefinition(builder.selectedDefinition!.id);
  };

  return (
    <div className="wf-form-footer">
      <div className="wf-form-footer-inner">
        {hasChanges && (
          <button
            type="button"
            className="btn btn-ghost"
            onClick={handleDiscard}
            disabled={builder.isSaving || builder.isPublishing}
          >
            Verwerfen
          </button>
        )}

        <div className="wf-form-footer-actions">
          <button
            type="button"
            className="btn btn-secondary"
            onClick={() => void builder.saveVersion()}
            disabled={!hasChanges || builder.isSaving || builder.isPublishing || !builder.selectedDefinition}
          >
            {builder.isSaving ? "Speichern …" : "Speichern (Entwurf)"}
          </button>

          <button
            type="button"
            className="btn btn-primary"
            onClick={() => void builder.publishVersion()}
            disabled={!canPublish || builder.isSaving || builder.isPublishing}
            title={!canManageAdvanced ? "Nur im Admin-Modus verfügbar" : !builder.selectedVersionSummary?.canPublish ? "Entwurf hat offene Validierungs-Issues" : undefined}
          >
            {builder.isPublishing ? "Veröffentlichen …" : "Veröffentlichen"}
          </button>
        </div>
      </div>
    </div>
  );
}
