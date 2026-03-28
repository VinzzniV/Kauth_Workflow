import {
  ADMIN_WORKSPACE_AREA_META,
  getAdminWorkspaceArea,
  getAdminWorkspaceSectionsForArea,
  type AdminWorkspaceSection,
} from "./adminWorkspaceModel";

type AdminWorkspaceNavigationProps = {
  section: AdminWorkspaceSection;
  onSelectSection: (section: AdminWorkspaceSection) => void;
};

export function AdminWorkspaceNavigation({
  section,
  onSelectSection,
}: AdminWorkspaceNavigationProps) {
  const activeArea = getAdminWorkspaceArea(section);

  return (
    <nav className="admin-workspace-nav" aria-label="Arbeitsbereiche">
      {ADMIN_WORKSPACE_AREA_META.map((area) => (
        <button
          key={area.key}
          type="button"
          className={`admin-workspace-tab ${activeArea === area.key ? "active" : ""}`}
          aria-pressed={activeArea === area.key}
          onClick={() => onSelectSection(area.defaultSection)}
        >
          <span className="admin-workspace-tab-title">{area.label}</span>
        </button>
      ))}
    </nav>
  );
}

export function AdminWorkspaceSubNavigation({
  section,
  onSelectSection,
}: AdminWorkspaceNavigationProps) {
  const activeArea = getAdminWorkspaceArea(section);

  if (!activeArea) {
    return null;
  }

  const sections = getAdminWorkspaceSectionsForArea(activeArea);
  if (sections.length <= 1) {
    return null;
  }

  return (
    <nav className="admin-workspace-subnav" aria-label="Bereich wechseln">
      {sections.map((item) => (
        <button
          key={item.key}
          type="button"
          className={`admin-workspace-subtab ${section === item.key ? "active" : ""}`}
          aria-pressed={section === item.key}
          onClick={() => onSelectSection(item.key)}
        >
          {item.label}
        </button>
      ))}
    </nav>
  );
}
