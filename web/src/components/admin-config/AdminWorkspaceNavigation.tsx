import {
  ADMIN_WORKSPACE_AREA_META,
  getAdminWorkspaceArea,
  getAdminWorkspacePresentationSection,
  getAdminWorkspaceSectionsForArea,
  getAdminWorkspaceSectionMeta,
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
  const activeSection = getAdminWorkspacePresentationSection(section);
  const overviewMeta = getAdminWorkspaceSectionMeta("overview");

  return (
    <nav className="admin-workspace-nav" aria-label="Admin-Arbeitsbereiche">
      <div className="admin-workspace-nav-head">
        <p className="admin-workspace-nav-eyebrow">Settings</p>
        <h2 className="admin-workspace-nav-title">Arbeitsbereiche</h2>
      </div>

      <button
        type="button"
        className={`admin-workspace-tab admin-workspace-tab--overview ${activeSection === "overview" ? "active" : ""}`}
        aria-current={activeSection === "overview" ? "page" : undefined}
        onClick={() => onSelectSection("overview")}
      >
        <span className="admin-workspace-tab-title">{overviewMeta.navLabel}</span>
        <span className="admin-workspace-tab-description">{overviewMeta.navDescription}</span>
      </button>

      {ADMIN_WORKSPACE_AREA_META.map((area) => {
        const areaSections = getAdminWorkspaceSectionsForArea(area.key);
        const isAreaActive = activeArea === area.key;

        return (
          <div key={area.key} className="admin-workspace-nav-group">
            <button
              type="button"
              className={`admin-workspace-tab admin-workspace-tab--${area.key} ${isAreaActive ? "active" : ""}`}
              aria-expanded={isAreaActive}
              onClick={() => onSelectSection(area.defaultSection)}
            >
              <span className="admin-workspace-tab-title">{area.label}</span>
              <span className="admin-workspace-tab-description">{area.description}</span>
            </button>

            {isAreaActive && areaSections.length > 1 ? (
              <AdminWorkspaceSubNavigation
                section={section}
                onSelectSection={onSelectSection}
              />
            ) : null}
          </div>
        );
      })}
    </nav>
  );
}

export function AdminWorkspaceSubNavigation({
  section,
  onSelectSection,
}: AdminWorkspaceNavigationProps) {
  const activeArea = getAdminWorkspaceArea(section);
  const activeSection = getAdminWorkspacePresentationSection(section);

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
          className={`admin-workspace-subtab ${activeSection === item.key ? "active" : ""}`}
          aria-pressed={activeSection === item.key}
          onClick={() => onSelectSection(item.key)}
        >
          {item.navLabel}
        </button>
      ))}
    </nav>
  );
}
