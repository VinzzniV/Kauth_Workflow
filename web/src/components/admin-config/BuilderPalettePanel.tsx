import type { WorkflowBuilderNodeDraft } from "../../hooks/adminWorkflowBuilderModel";
import { getWorkflowBuilderNodeTypeLabel } from "./workflowBuilderLabels";

type BuilderPalettePanelProps = {
  canManageAdvanced: boolean;
  hasVersionSelected: boolean;
  onAddNode: (nodeType: WorkflowBuilderNodeDraft["nodeType"]) => void;
};

type NodeOption = {
  nodeType: WorkflowBuilderNodeDraft["nodeType"];
  title: string;
  description: string;
  toneClassName: string;
};

const NODE_OPTIONS: NodeOption[] = [
  {
    nodeType: "start",
    title: "Start",
    description: "Legt fest, womit der Ablauf beginnt.",
    toneClassName: "builder-palette-card--start",
  },
  {
    nodeType: "form",
    title: "Formular",
    description: "Sammelt Eingaben fuer den weiteren Ablauf.",
    toneClassName: "builder-palette-card--form",
  },
  {
    nodeType: "approval",
    title: "Freigabe",
    description: "Laesst einen Schritt bestaetigen oder ablehnen.",
    toneClassName: "builder-palette-card--approval",
  },
  {
    nodeType: "task",
    title: "Aufgabe",
    description: "Bildet einen manuellen Arbeitsschritt ab.",
    toneClassName: "builder-palette-card--task",
  },
  {
    nodeType: "decision",
    title: "Entscheidung",
    description: "Verzweigt den Ablauf in unterschiedliche Wege.",
    toneClassName: "builder-palette-card--decision",
  },
  {
    nodeType: "automation",
    title: "Automatisierung",
    description: "Fuehrt hinterlegte automatische Aktionen aus.",
    toneClassName: "builder-palette-card--automation",
  },
  {
    nodeType: "end",
    title: "Ende",
    description: "Markiert das Ende eines Ablaufs oder Teilpfads.",
    toneClassName: "builder-palette-card--end",
  },
];

export function BuilderPalettePanel({
  canManageAdvanced,
  hasVersionSelected,
  onAddNode,
}: BuilderPalettePanelProps) {
  return (
    <section className="builder-sidebar-panel content-stack" aria-label="Bausteine">
      <div className="builder-sidebar-panel__header">
        <span className="builder-sidebar-panel__eyebrow">Bausteine</span>
        <h3>Schritte fuer den Ablauf</h3>
        <p className="text-muted">
          Fuege neue Schritte direkt aus der rechten Seitenleiste hinzu. Die Details dazu erscheinen danach automatisch.
        </p>
      </div>

      <div className="builder-palette-grid">
        {NODE_OPTIONS.map((option) => {
          const isAutomationLocked = option.nodeType === "automation" && !canManageAdvanced;
          const isDisabled = !hasVersionSelected || isAutomationLocked;

          return (
            <button
              key={option.nodeType}
              type="button"
              className={`builder-palette-card ${option.toneClassName}`}
              disabled={isDisabled}
              onClick={() => onAddNode(option.nodeType)}
              title={
                !hasVersionSelected
                  ? "Bitte zuerst links einen Ablauf und einen Stand auswaehlen."
                  : isAutomationLocked
                    ? "Automatisierungen koennen nur im Admin-Modus angelegt werden."
                    : `${getWorkflowBuilderNodeTypeLabel(option.nodeType)} hinzufuegen`
              }
            >
              <div className="builder-palette-card__top">
                <span className="badge badge--default">{getWorkflowBuilderNodeTypeLabel(option.nodeType)}</span>
                {isAutomationLocked ? (
                  <span className="badge badge--default builder-mode-badge builder-mode-badge--locked">Gesperrt</span>
                ) : null}
              </div>
              <strong>{option.title}</strong>
              <p>{option.description}</p>
            </button>
          );
        })}
      </div>

      {!hasVersionSelected ? (
        <div className="panel panel-info">
          <p className="panel-text">Bitte zuerst links einen Ablauf und einen Stand auswaehlen.</p>
        </div>
      ) : null}
    </section>
  );
}
