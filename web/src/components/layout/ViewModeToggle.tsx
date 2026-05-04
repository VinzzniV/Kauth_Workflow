import { LayoutGrid, Table2 } from "lucide-react";

export type ViewMode = "cards" | "table";

type ViewModeToggleProps = {
  value: ViewMode;
  onChange: (value: ViewMode) => void;
  label?: string;
};

export default function ViewModeToggle({
  value,
  onChange,
  label = "Ansicht",
}: ViewModeToggleProps) {
  return (
    <div className="view-mode-toggle" aria-label={label}>
      <button
        type="button"
        className="view-mode-toggle__button"
        aria-pressed={value === "cards"}
        onClick={() => onChange("cards")}
      >
        <LayoutGrid size={16} aria-hidden="true" />
        <span>Karten</span>
      </button>
      <button
        type="button"
        className="view-mode-toggle__button"
        aria-pressed={value === "table"}
        onClick={() => onChange("table")}
      >
        <Table2 size={16} aria-hidden="true" />
        <span>Tabelle</span>
      </button>
    </div>
  );
}
