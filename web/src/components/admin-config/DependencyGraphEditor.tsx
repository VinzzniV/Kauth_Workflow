import { useMemo, useState } from "react";
import { X } from "lucide-react";
import type { AdminDependencyGraph, AdminTaskSpec } from "../../types/auth";
import { useConfirmationDialog } from "../feedback/useConfirmationDialog";

type RequiredStatus = "open" | "ready" | "in_progress" | "blocked" | "done";

const REQUIRED_STATUS_OPTIONS: RequiredStatus[] = ["open", "ready", "in_progress", "blocked", "done"];

type DependencyGraphEditorProps = {
  graph: AdminDependencyGraph;
  templates: AdminTaskSpec[];
  selectedTemplateId: number | null;
  isLoading: boolean;
  isCreatingDependency: boolean;
  isDeletingDependency: boolean;
  onSelectTemplate: (templateId: number) => void;
  onCreateDependency: (
    sourceSpecId: number,
    targetSpecId: number,
    requiredStatus: RequiredStatus
  ) => Promise<void>;
  onDeleteDependency: (dependencyId: number) => Promise<void>;
};

export function DependencyGraphEditor({
  graph,
  templates,
  selectedTemplateId,
  isLoading,
  isCreatingDependency,
  isDeletingDependency,
  onSelectTemplate,
  onCreateDependency,
  onDeleteDependency,
}: DependencyGraphEditorProps) {
  const confirm = useConfirmationDialog();
  const [createOpen, setCreateOpen] = useState(false);
  const [draftSource, setDraftSource] = useState<number | "">("");
  const [draftTarget, setDraftTarget] = useState<number | "">("");
  const [draftStatus, setDraftStatus] = useState<RequiredStatus>("done");

  const templateIndex = useMemo(
    () => new Map(templates.map((t) => [t.id, t] as const)),
    [templates]
  );
  const nodeOptions = useMemo(
    () =>
      [...graph.nodes].sort((a, b) =>
        (a.title ?? "").localeCompare(b.title ?? "", "de")
      ),
    [graph.nodes]
  );
  const sortedEdges = useMemo(
    () =>
      [...graph.edges].sort((a, b) => {
        const sa = templateIndex.get(a.sourceSpecId)?.title ?? "";
        const sb = templateIndex.get(b.sourceSpecId)?.title ?? "";
        if (sa !== sb) return sa.localeCompare(sb, "de");
        const ta = templateIndex.get(a.targetSpecId)?.title ?? "";
        const tb = templateIndex.get(b.targetSpecId)?.title ?? "";
        return ta.localeCompare(tb, "de");
      }),
    [graph.edges, templateIndex]
  );

  if (isLoading) {
    return <p className="panel-note">Dependency-Graph wird geladen …</p>;
  }

  if (graph.nodes.length === 0) {
    return <p className="panel-note">Für diesen Prozesstyp sind noch keine Templates im Graph vorhanden.</p>;
  }

  const resetDraft = () => {
    setDraftSource("");
    setDraftTarget("");
    setDraftStatus("done");
  };

  const handleAdd = async () => {
    if (typeof draftSource !== "number" || typeof draftTarget !== "number") return;
    if (draftSource === draftTarget) return;
    const exists = graph.edges.some(
      (e) => e.sourceSpecId === draftSource && e.targetSpecId === draftTarget
    );
    if (exists) return;

    try {
      await onCreateDependency(draftSource, draftTarget, draftStatus);
      resetDraft();
      setCreateOpen(false);
    } catch {
      // Fehler kommt bereits als Notice/Error aus dem Hook.
    }
  };

  const handleDelete = async (dependencyId: number) => {
    const ok = await confirm({
      title: "Abhängigkeit löschen?",
      description: "Die Verbindung zwischen den beiden Aufgabenvorlagen wird entfernt.",
      confirmLabel: "Abhängigkeit löschen",
      tone: "danger",
    });
    if (!ok) return;
    void onDeleteDependency(dependencyId).catch(() => {
      // Fehler kommt bereits als Notice/Error aus dem Hook.
    });
  };

  const canSave =
    typeof draftSource === "number"
    && typeof draftTarget === "number"
    && draftSource !== draftTarget
    && !graph.edges.some((e) => e.sourceSpecId === draftSource && e.targetSpecId === draftTarget)
    && !isCreatingDependency;

  return (
    <div className="dep-editor">
      <div className="dep-editor-toolbar">
        <p className="dep-editor-summary">
          <strong>{graph.nodes.length}</strong> Vorlagen, <strong>{graph.edges.length}</strong> Abhängigkeit
          {graph.edges.length === 1 ? "" : "en"}
        </p>
        <button
          type="button"
          className="btn-secondary"
          onClick={() => setCreateOpen((v) => !v)}
          disabled={isCreatingDependency}
          aria-expanded={createOpen}
        >
          {createOpen ? "Abbrechen" : "+ Abhängigkeit hinzufügen"}
        </button>
      </div>

      {createOpen && (
        <div className="dep-editor-create">
          <div className="dep-editor-create-fields">
            <label className="form-label">
              Von
              <select
                className="form-select"
                value={draftSource === "" ? "" : String(draftSource)}
                onChange={(e) => setDraftSource(e.target.value ? Number(e.target.value) : "")}
              >
                <option value="">– Quelle wählen –</option>
                {nodeOptions.map((node) => (
                  <option key={node.id} value={node.id}>{node.title}</option>
                ))}
              </select>
            </label>

            <span className="dep-editor-arrow" aria-hidden="true">→</span>

            <label className="form-label">
              Nach
              <select
                className="form-select"
                value={draftTarget === "" ? "" : String(draftTarget)}
                onChange={(e) => setDraftTarget(e.target.value ? Number(e.target.value) : "")}
              >
                <option value="">– Ziel wählen –</option>
                {nodeOptions.map((node) => (
                  <option key={node.id} value={node.id} disabled={node.id === draftSource}>
                    {node.title}
                  </option>
                ))}
              </select>
            </label>

            <label className="form-label">
              Required Status
              <select
                className="form-select"
                value={draftStatus}
                onChange={(e) => setDraftStatus(e.target.value as RequiredStatus)}
              >
                {REQUIRED_STATUS_OPTIONS.map((s) => (
                  <option key={s} value={s}>{s}</option>
                ))}
              </select>
            </label>
          </div>

          <div className="dep-editor-create-actions">
            <button
              type="button"
              className="btn-primary"
              onClick={() => void handleAdd()}
              disabled={!canSave}
            >
              {isCreatingDependency ? "Wird angelegt …" : "Abhängigkeit anlegen"}
            </button>
          </div>
        </div>
      )}

      {sortedEdges.length === 0 ? (
        <p className="panel-note">Noch keine Abhängigkeiten erfasst.</p>
      ) : (
        <div className="dep-editor-table-wrap">
          <table className="dep-editor-table">
            <thead>
              <tr>
                <th>Quelle</th>
                <th aria-hidden="true">→</th>
                <th>Ziel</th>
                <th>Required Status</th>
                <th aria-label="Aktionen" />
              </tr>
            </thead>
            <tbody>
              {sortedEdges.map((edge) => {
                const sourceTpl = templateIndex.get(edge.sourceSpecId);
                const targetTpl = templateIndex.get(edge.targetSpecId);
                return (
                  <tr key={edge.id}>
                    <td>
                      <button
                        type="button"
                        className={`dep-editor-link${selectedTemplateId === edge.sourceSpecId ? " dep-editor-link--selected" : ""}`}
                        onClick={() => onSelectTemplate(edge.sourceSpecId)}
                        title="Template auswählen"
                      >
                        {sourceTpl?.title ?? `#${edge.sourceSpecId}`}
                      </button>
                    </td>
                    <td className="dep-editor-arrow-cell" aria-hidden="true">→</td>
                    <td>
                      <button
                        type="button"
                        className={`dep-editor-link${selectedTemplateId === edge.targetSpecId ? " dep-editor-link--selected" : ""}`}
                        onClick={() => onSelectTemplate(edge.targetSpecId)}
                        title="Template auswählen"
                      >
                        {targetTpl?.title ?? `#${edge.targetSpecId}`}
                      </button>
                    </td>
                    <td>
                      <span className={`badge dep-status-badge dep-status-badge--${edge.requiredStatus}`}>
                        {edge.requiredStatus}
                      </span>
                    </td>
                    <td>
                      <button
                        type="button"
                        className="btn-ghost btn-ghost--small"
                        onClick={() => void handleDelete(edge.id)}
                        disabled={isDeletingDependency}
                        aria-label={`Abhängigkeit ${sourceTpl?.title ?? edge.sourceSpecId} → ${targetTpl?.title ?? edge.targetSpecId} löschen`}
                      >
                        <X size={16} aria-hidden="true" />
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
