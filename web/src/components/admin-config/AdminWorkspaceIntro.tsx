import type { AdminWorkspaceSectionMeta } from "./adminWorkspaceModel";

type AdminWorkspaceIntroProps = {
  meta: AdminWorkspaceSectionMeta;
};

type IntroBadgeTone = "default" | "brand" | "info" | "success" | "warning";

function getIntroBadges(meta: AdminWorkspaceSectionMeta): Array<{ label: string; tone: IntroBadgeTone }> {
  switch (meta.key) {
    case "organization":
      return [
        { label: "Live", tone: "brand" },
        { label: "Struktur", tone: "default" },
      ];
    case "templates":
      return [
        { label: "Neue Vorgänge", tone: "info" },
        { label: "Logik", tone: "default" },
      ];
    case "answers":
    case "defaults":
      return [
        { label: "Neue Vorgänge", tone: "info" },
        { label: "Formular", tone: "default" },
      ];
    case "access":
      return [
        { label: "Mehrere Nutzer", tone: "success" },
        { label: "Ausnahmen", tone: "default" },
      ];
    case "directory":
      return [
        { label: "Sync", tone: "success" },
        { label: "Gruppenwirkung", tone: "warning" },
      ];
    case "system_logs":
      return [
        { label: "Live", tone: "warning" },
        { label: "Monitoring", tone: "default" },
      ];
    case "system_configuration":
      return [
        { label: "Live", tone: "warning" },
        { label: "System", tone: "default" },
      ];
    default:
      return [
        { label: "Hub", tone: "brand" },
        { label: "Überblick", tone: "default" },
      ];
  }
}

export function AdminWorkspaceIntro({ meta }: AdminWorkspaceIntroProps) {
  const badges = getIntroBadges(meta);
  const chips = meta.affectedObjects.slice(0, 3);

  return (
    <section className="panel panel-muted admin-workspace-intro">
      <div className="admin-workspace-intro-head">
        <div>
          <p className="admin-workspace-intro-eyebrow">Bereich</p>
          <h2>{meta.navLabel}</h2>
        </div>

        <div className="admin-workspace-intro-badges" aria-label="Bereichsmerkmale">
          {badges.map((badge) => (
            <span
              key={badge.label}
              className={`admin-workspace-intro-badge admin-workspace-intro-badge--${badge.tone}`}
            >
              {badge.label}
            </span>
          ))}
        </div>
      </div>

      <div className="admin-workspace-intro-chips" aria-label="Betroffene Objekte">
        {chips.map((chip) => (
          <span key={chip} className="admin-workspace-intro-chip">
            {chip}
          </span>
        ))}
      </div>
    </section>
  );
}
