import type { AdminWorkspaceSection } from "./adminWorkspaceModel";

type AdminWorkspaceNavigationProps = {
  section: AdminWorkspaceSection;
  onSelectSection: (section: AdminWorkspaceSection) => void;
};

const ITEMS: Array<{ key: AdminWorkspaceSection; label: string; description: string }> = [
  { key: "overview", label: "Übersicht", description: "Status, Warnungen und Einstiege" },
  { key: "organization", label: "Organisation", description: "Personen, Abteilungen und Zuständigkeiten" },
  { key: "templates", label: "Templates", description: "Task-Vorlagen pro Prozesstyp verwalten" },
  { key: "answers", label: "Answers", description: "Answer Definitions pro Prozesstyp verwalten" },
  { key: "defaults", label: "Defaults", description: "Rollenbasierte Answer-Defaults verwalten" },
  { key: "access", label: "Rechte & Gruppen", description: "Advanced-Bereich für technische Zugriffe" },
  { key: "system", label: "System", description: "Mail und Workflow-Konfiguration" },
  { key: "operations", label: "Operationen", description: "Massen-Aktionen wie Abteilungswechsel" },
];

export function AdminWorkspaceNavigation({
  section,
  onSelectSection,
}: AdminWorkspaceNavigationProps) {
  return (
    <section className="panel panel-muted">
      <div className="panel-head">
        <h2>Admin-Workspace</h2>
        <p>Wählen Sie den passenden Verwaltungsbereich. Personen, Abteilungen und Zuständigkeiten werden gemeinsam unter Organisation gepflegt.</p>
      </div>

      <div className="admin-workspace-nav" role="tablist" aria-label="Admin-Bereiche">
        {ITEMS.map((item) => (
          <button
            key={item.key}
            type="button"
            className={`admin-workspace-tab ${section === item.key ? "active" : ""}`}
            role="tab"
            aria-selected={section === item.key}
            onClick={() => onSelectSection(item.key)}
          >
            <span className="admin-workspace-tab-title">{item.label}</span>
            <span className="admin-workspace-tab-note">{item.description}</span>
          </button>
        ))}
      </div>
    </section>
  );
}
